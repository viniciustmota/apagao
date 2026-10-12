using Apagao.AI.Hive;
using Apagao.Combat;
using Apagao.Crafting;
using Apagao.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Apagao.AI
{
    /// <summary>Nível de alerta comum a todas as máquinas: dirige o indicador, a rede e o takedown.</summary>
    public enum AlertLevel { Nenhum, Suspeito, Investigando, Combate, Procurando }

    /// <summary>
    /// Base de toda máquina terrestre (Sentinela, Matilha, Brutamontes): sentidos, suspeita, rede da VIGÍLIA,
    /// vida, recuo, takedown, carcaça e indicador de alerta. O comportamento fica na subclasse e nos estados.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(VisionSensor))]
    public abstract class MachineAI : MonoBehaviour, IHiveAware, ITakedownTarget, IStunnable
    {
        [Header("Suspeita")]
        [Tooltip("Segundos para encher o medidor vendo o Caio com visibilidade máxima.")]
        [SerializeField] protected float suspicionFillTime = 1.5f;
        [Tooltip("Quanto o medidor esvazia por segundo sem ver o Caio.")]
        [SerializeField] protected float suspicionDecay = 0.25f;
        [Tooltip("Abaixo desta distância, ver o Caio é combate imediato.")]
        [SerializeField] protected float instantDetectDistance = 3f;

        [Header("Rede (spec 07)")]
        [Tooltip("Visibilidade mínima para reportar à rede um avistamento em Suspeito.")]
        [SerializeField] protected float reportVisibility = 0.5f;

        [Header("Alerta elevado (depois de ver o Caio uma vez)")]
        [SerializeField] protected float heightenedVisionMultiplier = 1.25f;
        [SerializeField] protected float heightenedSuspicionMultiplier = 1.5f;

        [Header("Takedown (spec 09)")]
        [Tooltip("O Caio precisa estar dentro deste cone atrás da máquina.")]
        [SerializeField] protected float takedownRearAngle = 120f;

        [Header("PEM (spec 11)")]
        [Tooltip("Multiplica a duração da paralisia (Brutamontes: 0,5).")]
        [SerializeField] protected float stunDurationMultiplier = 1f;
        [Tooltip("Depois de acordar, fica imune ao PEM por este tempo.")]
        [SerializeField] protected float stunImmunity = 5f;

        [Header("Feedback e carcaça")]
        [SerializeField] protected Renderer alertIndicator;
        [Tooltip("Partes que caem como carcaça quando a máquina é destruída.")]
        [SerializeField] protected Transform[] wreckParts;

        [Header("Saque (spec 12 R3)")]
        [SerializeField] protected LootEntry[] loot;

        // ---------- Sentidos e estado comum ----------
        public NavMeshAgent Agent { get; private set; }
        public VisionSensor Vision { get; private set; }
        public Transform Player { get; private set; }
        public PlayerController PlayerMove { get; private set; }
        public IDamageable PlayerDamageable { get; private set; }

        public bool CanSeePlayer { get; private set; }
        public float PlayerVisibility { get; private set; }
        public float DistanceToPlayer { get; private set; } = float.MaxValue;
        public Vector3 LastKnownPosition { get; set; }
        public float Suspicion { get; set; }
        public bool HeightenedAlert { get; set; }
        public bool IsDead { get; private set; }
        public bool IsFrozen { get; private set; }
        public bool IsStunned => Time.time < stunnedUntil;

        public bool ShouldEngageImmediately => CanSeePlayer && DistanceToPlayer <= instantDetectDistance;
        public bool ReachedDestination =>
            Agent.isOnNavMesh && !Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + 0.2f;

        /// <summary>Fora da patrulha normal. Destruída assim, avisa a rede na hora (spec 07 R7).</summary>
        public bool IsAware => Alert != AlertLevel.Nenhum;

        // ---------- Contrato da subclasse ----------
        public abstract AlertLevel Alert { get; }
        public abstract string StateName { get; }
        protected abstract void BeginBehaviour();
        protected abstract void Think();
        protected abstract void EnterCombat();
        protected abstract void HandleNoise(HeardNoise noise);
        protected abstract void HandleHiveSighting(Vector3 position, bool confirmed, bool converge);
        protected abstract void HandleHiveMemberMissing(Vector3 lastPosition, bool investigate);
        protected virtual void OnFrozen() { }
        protected virtual void OnStunned() { }
        /// <summary>Ao acordar da paralisia: por padrão a subclasse reinicia o estado atual.</summary>
        protected virtual void ResumeAfterStun() { }
        protected virtual void UpdateExtraFeedback() { }
        /// <summary>Quanto o modo de movimento do Caio pesa na visibilidade.</summary>
        protected virtual float StanceMultiplier(MoveMode mode) => mode switch
        {
            MoveMode.Agachado => 0.5f,
            MoveMode.Correndo => 1.3f,
            _ => 1f
        };

        protected Health health;
        protected HearingSensor hearing;
        protected HiveMember hive;
        private MaterialPropertyBlock indicatorBlock;
        private float stunnedUntil = float.NegativeInfinity;
        private float immuneUntil = float.NegativeInfinity;
        private bool wasStunned;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Vision = GetComponent<VisionSensor>();
            hearing = GetComponent<HearingSensor>();
            hive = GetComponent<HiveMember>();
            health = GetComponent<Health>();
            PlayerMove = FindAnyObjectByType<PlayerController>();
            Player = PlayerMove != null ? PlayerMove.transform : null;
            PlayerDamageable = Player != null ? Player.GetComponent<IDamageable>() : null;
            indicatorBlock = new MaterialPropertyBlock();
        }

        protected virtual void OnEnable()
        {
            if (hearing != null) hearing.Heard += OnNoiseHeard;
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += Destroyed;
            }
        }

        protected virtual void OnDisable()
        {
            if (hearing != null) hearing.Heard -= OnNoiseHeard;
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= Destroyed;
            }
        }

        protected virtual void Start()
        {
            // O agente começa desligado: o NavMesh só existe depois do Awake do NavMeshBaker.
            Agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                Agent.Warp(hit.position);
            BeginBehaviour();
        }

        protected virtual void Update()
        {
            if (IsFrozen || IsDead) return;

            if (IsStunned)
            {
                UpdateIndicator();
                return;
            }
            if (wasStunned)
            {
                wasStunned = false;
                immuneUntil = Time.time + stunImmunity;
                HeightenedAlert = true;
                ResumeAfterStun();
            }

            Sense();
            ReportToHive();
            Think();
            UpdateIndicator();
            UpdateExtraFeedback();
        }

        private void Sense()
        {
            Vision.RangeMultiplier = HeightenedAlert ? heightenedVisionMultiplier : 1f;

            CanSeePlayer = Vision.CanSee(Player, out float visibility);
            PlayerVisibility = visibility * (PlayerMove != null ? StanceMultiplier(PlayerMove.Mode) : 1f);
            DistanceToPlayer = Player != null ? Vector3.Distance(transform.position, Player.position) : float.MaxValue;

            if (CanSeePlayer) LastKnownPosition = Player.position;
        }

        // ---------- Rede da VIGÍLIA ----------

        protected virtual void ReportToHive()
        {
            if (hive == null || !CanSeePlayer) return;
            if (Alert == AlertLevel.Combate)
                hive.ReportSighting(Player.position, confirmed: true);
            else if (Alert == AlertLevel.Suspeito && PlayerVisibility >= reportVisibility)
                hive.ReportSighting(Player.position, confirmed: false);
        }

        public void OnHiveSighting(Vector3 position, bool confirmed, bool converge)
        {
            if (IsFrozen || IsDead) return;
            HeightenedAlert = true;
            HandleHiveSighting(position, confirmed, converge);
        }

        public void OnHiveMemberMissing(Vector3 lastPosition, bool investigate)
        {
            if (IsFrozen || IsDead) return;
            HeightenedAlert = true;
            HandleHiveMemberMissing(lastPosition, investigate);
        }

        private void OnNoiseHeard(HeardNoise noise)
        {
            if (IsFrozen || IsDead) return;
            HandleNoise(noise);
        }

        // ---------- Ações comuns ----------

        public void FillSuspicion(float rateMultiplier = 1f)
        {
            float mult = (HeightenedAlert ? heightenedSuspicionMultiplier : 1f) * rateMultiplier;
            Suspicion = Mathf.Clamp01(Suspicion + Time.deltaTime / suspicionFillTime * PlayerVisibility * mult);
        }

        public void DecaySuspicion() =>
            Suspicion = Mathf.Clamp01(Suspicion - suspicionDecay * Time.deltaTime);

        public void MoveTo(Vector3 destination, float speed)
        {
            if (!Agent.isOnNavMesh) return;
            // Destinos fora do NavMesh (ex.: Caio no telhado) vão para o ponto alcançável mais próximo.
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                destination = hit.position;
            Agent.speed = speed;
            Agent.isStopped = false;
            Agent.SetDestination(destination);
        }

        public void Stop()
        {
            if (Agent.isOnNavMesh) Agent.isStopped = true;
        }

        public void FaceTowards(Vector3 point, float degreesPerSecond = 360f)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, degreesPerSecond * Time.deltaTime);
        }

        public bool IsFacingPlayer(float maxAngle)
        {
            if (Player == null) return false;
            Vector3 to = Player.position - transform.position;
            to.y = 0f;
            return to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) <= maxAngle;
        }

        /// <summary>Ponto aleatório alcançável perto de <paramref name="center"/>.</summary>
        public bool RandomPointNear(Vector3 center, float radius, out Vector3 point)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    point = hit.position;
                    return true;
                }
            }
            point = center;
            return false;
        }

        // ---------- PEM (spec 11) ----------

        /// <summary>Paralisa: para tudo, sai da rede e libera o takedown de qualquer ângulo.</summary>
        public void Stun(float seconds)
        {
            if (IsDead || IsFrozen || Time.time < immuneUntil) return;
            float duration = seconds * stunDurationMultiplier;
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
            wasStunned = true;
            Stop();
            if (hive != null) hive.GoOffline(duration);
            OnStunned();
        }

        // ---------- Dano ----------

        /// <summary>Levar dano revela o Caio: recuo e combate.</summary>
        private void OnDamaged(DamageInfo info, float amount)
        {
            if (IsDead || IsFrozen || health.Current <= 0f) return; // golpe fatal: quem cuida é o Died
            if (info.Source != null) LastKnownPosition = info.Source.transform.position;
            HeightenedAlert = true;

            Vector3 push = info.Direction;
            push.y = 0f;
            if (Agent.isOnNavMesh && push.sqrMagnitude > 0.0001f)
                Agent.Move(push.normalized * Mathf.Clamp(amount * 0.01f, 0.15f, 0.5f));

            EnterCombat();
        }

        // ---------- Takedown (spec 09) ----------

        public virtual bool CanBeTakenDown(Transform attacker)
        {
            if (IsDead || IsFrozen) return false;
            if (IsStunned) return true; // paralisado: takedown de qualquer ângulo, mesmo em combate
            if (Alert == AlertLevel.Combate) return false;
            Vector3 toAttacker = attacker.position - transform.position;
            toAttacker.y = 0f;
            // Atrás = o atacante está a mais de (180 - metade do cone) graus da frente.
            return Vector3.Angle(transform.forward, toAttacker) >= 180f - takedownRearAngle * 0.5f;
        }

        public void BeginTakedown()
        {
            IsFrozen = true;
            Stop();
            if (alertIndicator != null) alertIndicator.enabled = false;
            OnFrozen();
        }

        public void CompleteTakedown() => Destroyed();

        /// <summary>Destrói a máquina avisando a rede e deixa a carcaça no chão.</summary>
        public void Destroyed()
        {
            if (IsDead) return;
            IsDead = true;
            if (hive != null) hive.ReportDeath(IsAware);
            SpawnLoot();
            SpawnWreck();
            Destroy(gameObject);
        }

        private void SpawnLoot()
        {
            if (loot == null) return;
            foreach (var entry in loot)
            {
                if (entry.amount <= 0 || Random.value > entry.chance) continue;
                Vector2 offset = Random.insideUnitCircle * 0.8f;
                Apagao.Crafting.Pickup.Spawn(entry.type, entry.amount, transform.position + new Vector3(offset.x, 0f, offset.y));
            }
        }

        private void SpawnWreck()
        {
            if (wreckParts == null) return;
            foreach (var part in wreckParts)
            {
                if (part == null) continue;
                part.SetParent(null, true);
                var r = part.GetComponent<Renderer>();
                if (r != null) r.SetPropertyBlock(null); // não fica branca se morrer no meio do brilho
                var col = part.gameObject.AddComponent<BoxCollider>();
                col.size = Vector3.one * 0.9f;
                var rb = part.gameObject.AddComponent<Rigidbody>();
                rb.mass = 40f;
                rb.AddForce(transform.forward * 2f + Random.insideUnitSphere, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
                Destroy(part.gameObject, 60f);
            }
        }

        // ---------- Indicador ----------

        private void UpdateIndicator()
        {
            if (alertIndicator == null) return;

            Color color;
            bool visible = true;
            float scale = 0.35f;
            if (IsStunned)
            {
                alertIndicator.enabled = true;
                alertIndicator.GetPropertyBlock(indicatorBlock);
                indicatorBlock.SetColor(BaseColorId, new Color(0.3f, 0.8f, 1f));
                alertIndicator.SetPropertyBlock(indicatorBlock);
                alertIndicator.transform.localScale = Vector3.one * (0.3f + 0.05f * Mathf.Sin(Time.time * 30f));
                return;
            }

            switch (Alert)
            {
                case AlertLevel.Combate: color = new Color(1f, 0.1f, 0.1f); break;
                case AlertLevel.Procurando: color = new Color(1f, 0.35f, 0.1f); break;
                case AlertLevel.Investigando: color = new Color(1f, 0.6f, 0.1f); break;
                case AlertLevel.Suspeito:
                    color = Color.Lerp(new Color(1f, 1f, 0.6f), new Color(1f, 0.85f, 0f), Suspicion);
                    scale = 0.15f + 0.2f * Suspicion;
                    break;
                default:
                    color = new Color(1f, 1f, 0.6f);
                    visible = Suspicion > 0.01f;
                    scale = 0.15f + 0.2f * Suspicion;
                    break;
            }

            alertIndicator.enabled = visible;
            alertIndicator.GetPropertyBlock(indicatorBlock);
            indicatorBlock.SetColor(BaseColorId, color);
            alertIndicator.SetPropertyBlock(indicatorBlock);
            alertIndicator.transform.localScale = Vector3.one * scale;
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.8f,
                $"{(IsStunned ? "PARALISADO" : StateName)}  {Suspicion:P0}{(HeightenedAlert ? "  [ALERTA]" : "")}{(hive != null && !hive.Connected ? "  [OFFLINE]" : "")}");
        }
#endif
    }
}
