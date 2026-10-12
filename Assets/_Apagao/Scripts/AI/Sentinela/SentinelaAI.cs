using Apagao.AI.Hive;
using Apagao.Combat;
using Apagao.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Apagao.AI.Sentinela
{
    /// <summary>
    /// Sentinela: robô humanoide base (spec 05). Patrulha -> Suspeito -> Investigando -> Combate -> Procurando.
    /// Este componente guarda os dados, os sentidos e a conexão com a rede; o comportamento fica em uma classe por estado.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(VisionSensor))]
    public class SentinelaAI : MonoBehaviour, IHiveAware, ITakedownTarget
    {
        [Header("Patrulha")]
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float patrolSpeed = 1.6f;
        [SerializeField] private float waypointPause = 1.5f;

        [Header("Suspeita")]
        [Tooltip("Segundos para encher o medidor vendo o Caio com visibilidade máxima.")]
        [SerializeField] private float suspicionFillTime = 1.5f;
        [Tooltip("Quanto o medidor esvazia por segundo sem ver o Caio.")]
        [SerializeField] private float suspicionDecay = 0.25f;
        [Tooltip("Abaixo desta distância, ver o Caio é combate imediato.")]
        [SerializeField] private float instantDetectDistance = 3f;
        [Tooltip("Tempo mínimo encarando o ponto suspeito antes de voltar à patrulha.")]
        [SerializeField] private float minSuspiciousTime = 2.5f;

        [Header("Audição (spec 06)")]
        [Tooltip("Quanto da intensidade do som vira suspeita.")]
        [SerializeField] private float noiseToSuspicion = 0.6f;
        [Tooltip("Sons a partir desta intensidade levam direto a Investigando.")]
        [SerializeField] private float loudNoiseThreshold = 0.8f;

        [Header("Rede (spec 07)")]
        [Tooltip("Visibilidade mínima para reportar à rede um avistamento em Suspeito.")]
        [SerializeField] private float reportVisibility = 0.5f;

        [Header("Investigação e busca")]
        [SerializeField] private float investigateSpeed = 2.2f;
        [SerializeField] private float lookAroundTime = 4f;
        [SerializeField] private float searchDuration = 12f;
        [SerializeField] private float searchRadius = 8f;

        [Header("Combate")]
        [SerializeField] private float chaseSpeed = 3.5f;
        [SerializeField] private float engageDistance = 8f;
        [SerializeField] private float loseSightTime = 2f;

        [Header("Takedown (spec 09)")]
        [Tooltip("O Caio precisa estar dentro deste cone atrás da Sentinela.")]
        [SerializeField] private float takedownRearAngle = 120f;

        [Header("Alerta elevado (depois de ver o Caio uma vez)")]
        [SerializeField] private float heightenedVisionMultiplier = 1.25f;
        [SerializeField] private float heightenedSuspicionMultiplier = 1.5f;

        [Header("Feedback visual")]
        [SerializeField] private Renderer alertIndicator;
        [SerializeField] private LineRenderer laser;

        // ---------- Acesso para os estados ----------
        public NavMeshAgent Agent { get; private set; }
        public VisionSensor Vision { get; private set; }
        public Transform Player { get; private set; }

        public Transform[] Waypoints => waypoints;
        public float PatrolSpeed => patrolSpeed;
        public float WaypointPause => waypointPause;
        public float MinSuspiciousTime => minSuspiciousTime;
        public float LookAroundTime => lookAroundTime;
        public float SearchDuration => searchDuration;
        public float SearchRadius => searchRadius;
        public float InvestigateSpeed => investigateSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float EngageDistance => engageDistance;
        public float LoseSightTime => loseSightTime;
        /// <summary>Velocidade da investigação atual: rápida quando chamada pela rede.</summary>
        public float CurrentInvestigateSpeed => investigateFast ? chaseSpeed : investigateSpeed;

        public bool CanSeePlayer { get; private set; }
        public float PlayerVisibility { get; private set; }
        public float DistanceToPlayer { get; private set; } = float.MaxValue;
        public Vector3 LastKnownPosition { get; set; }
        public float Suspicion { get; set; }
        public bool HeightenedAlert { get; set; }
        public bool ShouldEngageImmediately => CanSeePlayer && DistanceToPlayer <= instantDetectDistance;
        public bool ReachedDestination =>
            !Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + 0.2f;

        /// <summary>Fora da patrulha normal. Destruída assim, avisa a rede na hora (spec 07 R7).</summary>
        public bool IsAware => Fsm.Current != null && Fsm.Current != Patrulha;

        // ---------- Estados ----------
        public PatrulhaState Patrulha { get; private set; }
        public SuspeitoState Suspeito { get; private set; }
        public InvestigandoState Investigando { get; private set; }
        public CombateState Combate { get; private set; }
        public ProcurandoState Procurando { get; private set; }

        public StateMachine<SentinelaAI> Fsm { get; } = new StateMachine<SentinelaAI>();
        public string StateName => Fsm.Current?.Name ?? "-";

        private PlayerController playerController;
        private HearingSensor hearing;
        private HiveMember hive;
        private bool investigateFast;
        private Health health;
        private SentinelaGun gun;
        private IDamageable playerDamageable;
        private bool frozen;
        private bool dead;
        private MaterialPropertyBlock indicatorBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Vision = GetComponent<VisionSensor>();
            hearing = GetComponent<HearingSensor>();
            hive = GetComponent<HiveMember>();
            playerController = FindAnyObjectByType<PlayerController>();
            Player = playerController != null ? playerController.transform : null;
            playerDamageable = Player != null ? Player.GetComponent<IDamageable>() : null;
            health = GetComponent<Health>();
            gun = GetComponent<SentinelaGun>();
            indicatorBlock = new MaterialPropertyBlock();

            Patrulha = new PatrulhaState(this);
            Suspeito = new SuspeitoState(this);
            Investigando = new InvestigandoState(this);
            Combate = new CombateState(this);
            Procurando = new ProcurandoState(this);
        }

        private void OnEnable()
        {
            if (hearing != null) hearing.Heard += OnNoiseHeard;
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += Destroyed;
            }
        }

        private void OnDisable()
        {
            if (hearing != null) hearing.Heard -= OnNoiseHeard;
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= Destroyed;
            }
        }

        private void Start()
        {
            // O agente começa desligado: o NavMesh só existe depois do Awake do NavMeshBaker.
            Agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                Agent.Warp(hit.position);

            Fsm.ChangeState(Patrulha);
        }

        private void Update()
        {
            if (frozen || dead) return;
            Sense();
            ReportToHive();
            Fsm.Tick();
            UpdateFeedback();
        }

        private void Sense()
        {
            Vision.RangeMultiplier = HeightenedAlert ? heightenedVisionMultiplier : 1f;

            CanSeePlayer = Vision.CanSee(Player, out float visibility);
            PlayerVisibility = visibility * StanceMultiplier();
            DistanceToPlayer = Player != null ? Vector3.Distance(transform.position, Player.position) : float.MaxValue;

            if (CanSeePlayer) LastKnownPosition = Player.position;
        }

        /// <summary>Agachado o Caio chama menos atenção; correndo, mais.</summary>
        private float StanceMultiplier()
        {
            if (playerController == null) return 1f;
            return playerController.Mode switch
            {
                MoveMode.Agachado => 0.5f,
                MoveMode.Correndo => 1.3f,
                _ => 1f
            };
        }

        // ---------- Rede da VIGÍLIA ----------

        private void ReportToHive()
        {
            if (hive == null || !CanSeePlayer) return;

            if (Fsm.Current == Combate)
                hive.ReportSighting(Player.position, confirmed: true);
            else if (Fsm.Current == Suspeito && PlayerVisibility >= reportVisibility)
                hive.ReportSighting(Player.position, confirmed: false);
        }

        public void OnHiveSighting(Vector3 position, bool confirmed, bool converge)
        {
            if (frozen || dead) return;
            HeightenedAlert = true;
            if (Fsm.Current == Combate)
            {
                if (!CanSeePlayer) LastKnownPosition = position;
                return;
            }

            LastKnownPosition = position;
            if (converge) InvestigatePoint(position, fast: true);
        }

        public void OnHiveMemberMissing(Vector3 lastPosition, bool investigate)
        {
            if (frozen || dead) return;
            HeightenedAlert = true;
            if (investigate && Fsm.Current != Combate)
                InvestigatePoint(lastPosition, fast: false);
        }

        // ---------- Audição ----------

        private void OnNoiseHeard(HeardNoise noise)
        {
            if (frozen || dead) return;
            var state = Fsm.Current;
            Vector3 p = noise.EstimatedPosition;

            if (state == Combate)
            {
                if (!CanSeePlayer) LastKnownPosition = p;
                return;
            }
            if (state == Procurando)
            {
                LastKnownPosition = p;
                Procurando.Retarget();
                return;
            }
            if (state == Investigando)
            {
                InvestigatePoint(p, investigateFast);
                return;
            }

            LastKnownPosition = p;
            Suspicion = Mathf.Clamp01(Suspicion + noise.Intensity * noiseToSuspicion);
            if (noise.Intensity >= loudNoiseThreshold || Suspicion >= 1f)
                InvestigatePoint(p, fast: false);
            else
                Fsm.ChangeState(Suspeito);
        }

        // ---------- Ações usadas pelos estados e pela rede ----------

        public void InvestigatePoint(Vector3 point, bool fast)
        {
            LastKnownPosition = point;
            investigateFast = fast;
            if (Fsm.Current == Investigando) Investigando.Restart();
            else Fsm.ChangeState(Investigando);
        }

        public void ClearInvestigateFast() => investigateFast = false;

        public void FillSuspicion()
        {
            float mult = HeightenedAlert ? heightenedSuspicionMultiplier : 1f;
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

        // ---------- Combate ----------

        /// <summary>Chamado pelo CombateState: atira se estiver vendo, no alcance e de frente para o Caio.</summary>
        public void TickGun(bool wantsToShoot)
        {
            if (gun == null) return;
            bool canShoot = wantsToShoot && CanSeePlayer && DistanceToPlayer <= gun.Range && IsFacingPlayer(20f);
            gun.Tick(canShoot, Vision.EyePosition + transform.forward * 0.3f, Player, playerDamageable, playerController);
        }

        public void ResetGun()
        {
            if (gun != null) gun.ResetAim();
        }

        private bool IsFacingPlayer(float maxAngle)
        {
            if (Player == null) return false;
            Vector3 to = Player.position - transform.position;
            to.y = 0f;
            return to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) <= maxAngle;
        }

        /// <summary>Levar dano revela o Caio: a Sentinela entra em combate.</summary>
        private void OnDamaged(DamageInfo info, float amount)
        {
            if (dead || frozen || health.Current <= 0f) return; // golpe fatal: quem cuida é o Died
            if (info.Source != null) LastKnownPosition = info.Source.transform.position;
            HeightenedAlert = true;

            // Recuo: o golpe empurra a Sentinela para trás.
            Vector3 push = info.Direction;
            push.y = 0f;
            if (Agent.isOnNavMesh && push.sqrMagnitude > 0.0001f)
                Agent.Move(push.normalized * Mathf.Clamp(amount * 0.01f, 0.15f, 0.5f));

            Fsm.ChangeState(Combate);
        }

        // ---------- Takedown (spec 09) ----------

        public bool CanBeTakenDown(Transform attacker)
        {
            if (dead || frozen || Fsm.Current == Combate) return false;
            Vector3 toAttacker = attacker.position - transform.position;
            toAttacker.y = 0f;
            // Atrás = o atacante está a mais de (180 - metade do cone) graus da frente.
            return Vector3.Angle(transform.forward, toAttacker) >= 180f - takedownRearAngle * 0.5f;
        }

        public void BeginTakedown()
        {
            frozen = true;
            Stop();
            ResetGun();
            if (laser != null) laser.enabled = false;
            if (alertIndicator != null) alertIndicator.enabled = false;
        }

        public void CompleteTakedown() => Destroyed();

        /// <summary>Destrói a Sentinela avisando a rede e deixa a carcaça no chão.</summary>
        public void Destroyed()
        {
            if (dead) return;
            dead = true;
            if (hive != null) hive.ReportDeath(IsAware);
            SpawnWreck();
            Destroy(gameObject);
        }

        /// <summary>Corpo e cabeça caem com física e ficam no chão por um tempo (a rede vai investigar ali).</summary>
        private void SpawnWreck()
        {
            foreach (var partName in new[] { "Corpo", "Cabeca" })
            {
                Transform part = transform.Find(partName);
                if (part == null) continue;
                part.SetParent(null, true);
                var r = part.GetComponent<Renderer>();
                if (r != null) r.SetPropertyBlock(null); // não fica branca se morrer no meio do brilho
                var col = part.gameObject.AddComponent<BoxCollider>();
                col.size = Vector3.one * 0.9f;
                var rb = part.gameObject.AddComponent<Rigidbody>();
                rb.mass = 60f;
                rb.AddForce(transform.forward * 2f + Random.insideUnitSphere, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
                Destroy(part.gameObject, 60f);
            }
        }

        // ---------- Feedback ----------

        private void UpdateFeedback()
        {
            if (alertIndicator != null)
            {
                var state = Fsm.Current;
                Color color;
                bool visible = true;
                if (state == Combate) color = new Color(1f, 0.1f, 0.1f);
                else if (state == Procurando) color = new Color(1f, 0.35f, 0.1f);
                else if (state == Investigando) color = new Color(1f, 0.6f, 0.1f);
                else if (state == Suspeito) color = Color.Lerp(new Color(1f, 1f, 0.6f), new Color(1f, 0.85f, 0f), Suspicion);
                else
                {
                    color = new Color(1f, 1f, 0.6f);
                    visible = Suspicion > 0.01f;
                }

                alertIndicator.enabled = visible;
                alertIndicator.GetPropertyBlock(indicatorBlock);
                indicatorBlock.SetColor(BaseColorId, color);
                alertIndicator.SetPropertyBlock(indicatorBlock);

                float s = state == Suspeito || state == Patrulha ? 0.15f + 0.2f * Suspicion : 0.35f;
                alertIndicator.transform.localScale = Vector3.one * s;
            }

            if (laser != null)
            {
                bool aiming = Fsm.Current == Combate && CanSeePlayer;
                laser.enabled = aiming;
                laser.widthMultiplier = gun != null && gun.IsAiming ? 2.5f : 1f; // aviso antes da rajada
                if (aiming)
                {
                    laser.SetPosition(0, Vision.EyePosition + transform.forward * 0.3f);
                    laser.SetPosition(1, Player.position + Vector3.up * 1.2f);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.8f,
                $"{StateName}  {Suspicion:P0}{(HeightenedAlert ? "  [ALERTA]" : "")}{(hive != null && !hive.Connected ? "  [OFFLINE]" : "")}");
        }
#endif
    }
}
