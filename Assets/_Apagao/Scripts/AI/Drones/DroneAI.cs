using Apagao.AI.Hive;
using Apagao.Combat;
using Apagao.Crafting;
using Apagao.Player;
using UnityEngine;

namespace Apagao.AI.Drones
{
    /// <summary>
    /// Base dos drones (spec 15): vida, rede da VIGÍLIA, PEM, detecção de cima, queda com física e saque.
    /// O comportamento fica na subclasse, numa FSM por enum (exceção registrada na arquitetura).
    /// </summary>
    [RequireComponent(typeof(DroneMotor), typeof(Health))]
    public abstract class DroneAI : MonoBehaviour, IHiveAware, IStunnable
    {
        [Header("Visão de cima (spec 15 R4/R5)")]
        [Tooltip("Abaixo desta altura (Y do mundo) a água esconde o Caio.")]
        [SerializeField] protected float waterLevel = 4f;

        [Header("Feedback")]
        [SerializeField] protected Renderer alertIndicator;
        [SerializeField] protected Light statusLight;

        [Header("Saque (spec 12 R3)")]
        [SerializeField] protected LootEntry[] loot;

        public DroneMotor Motor { get; private set; }
        public Transform Player { get; private set; }
        public PlayerController PlayerMove { get; private set; }
        public Vector3 LastKnownPosition { get; protected set; }
        public bool IsDead { get; private set; }

        public abstract bool IsAware { get; }
        public abstract string StateName { get; }
        public virtual bool IsStunned => false;

        protected Health health;
        protected HiveMember hive;
        private readonly RaycastHit[] losHits = new RaycastHit[8];
        private MaterialPropertyBlock block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // ---------- Contrato da subclasse ----------
        protected abstract void Think();
        protected abstract void HandleHiveSighting(Vector3 position, bool confirmed, bool converge);
        /// <summary>Levou dano e sobreviveu.</summary>
        protected virtual void HandleDamaged(DamageInfo info) { }
        /// <summary>Vida chegou a zero. Padrão: cai.</summary>
        protected virtual void HandleKilled() => Crash();

        protected virtual void Awake()
        {
            Motor = GetComponent<DroneMotor>();
            health = GetComponent<Health>();
            hive = GetComponent<HiveMember>();
            PlayerMove = FindAnyObjectByType<PlayerController>();
            Player = PlayerMove != null ? PlayerMove.transform : null;
            block = new MaterialPropertyBlock();
        }

        protected virtual void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        protected virtual void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        protected virtual void Update()
        {
            if (IsDead) return;
            Think();
        }

        // ---------- Sentidos ----------

        /// <summary>
        /// Visão térmica para baixo: o Caio dentro do cone (medido a partir da vertical), no alcance,
        /// sem telhado no caminho e fora d'água.
        /// </summary>
        protected bool CanSeeFromAbove(float range, float halfAngleFromVertical)
        {
            if (Player == null || IsSubmerged()) return false;
            Vector3 eye = transform.position;
            Vector3 target = Player.position + Vector3.up * (PlayerMove.IsCrouching ? 0.6f : 1f);
            Vector3 to = target - eye;
            if (to.magnitude > range) return false;
            if (Vector3.Angle(Vector3.down, to) > halfAngleFromVertical) return false;
            return HasLineOfSight(eye, target);
        }

        /// <summary>Visão em qualquer direção (a Vespa no ar olha para todo lado).</summary>
        protected bool CanSeeAround(float range)
        {
            if (Player == null || IsSubmerged()) return false;
            Vector3 target = Player.position + Vector3.up * 1f;
            return Vector3.Distance(transform.position, target) <= range && HasLineOfSight(transform.position, target);
        }

        /// <summary>Cabeça do Caio abaixo da linha d'água (spec 15 R5).</summary>
        protected bool IsSubmerged()
        {
            float head = PlayerMove.IsCrouching ? 0.9f : 1.5f;
            return Player.position.y + head < waterLevel;
        }

        private bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            int count = Physics.RaycastNonAlloc(from, dir / dist, losHits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform t = losHits[i].collider.transform;
                if (t.IsChildOf(transform) || t.IsChildOf(Player)) continue;
                if (t.GetComponentInParent<DroneAI>() != null) continue; // outro drone não tapa a visão
                return false;
            }
            return true;
        }

        protected Vector3 PlayerChest => Player.position + Vector3.up * 1f;

        // ---------- Rede ----------

        public void OnHiveSighting(Vector3 position, bool confirmed, bool converge)
        {
            if (IsDead || IsStunned) return;
            HandleHiveSighting(position, confirmed, converge);
        }

        public void OnHiveMemberMissing(Vector3 lastPosition, bool investigate) { } // drones não vão ver desaparecidos

        protected void ReportSighting(Vector3 position, bool confirmed)
        {
            if (hive != null) hive.ReportSighting(position, confirmed);
        }

        protected bool PlayerMarked => hive != null && hive.Connected && hive.Network.PlayerSpotted;

        // ---------- PEM (spec 11 + 15 R3) ----------

        /// <summary>Padrão (Olho e Vespa): o PEM queima o drone e ele cai.</summary>
        public virtual void Stun(float seconds) => Crash();

        // ---------- Dano e morte ----------

        private void OnDamaged(DamageInfo info, float amount)
        {
            if (IsDead || health.Current <= 0f) return;
            if (info.Source != null) LastKnownPosition = info.Source.transform.position;
            HandleDamaged(info);
        }

        private void OnDied()
        {
            if (IsDead) return;
            HandleKilled();
        }

        /// <summary>Desliga o drone e deixa a física derrubá-lo. A carcaça some depois de 30 s.</summary>
        protected void Crash()
        {
            if (IsDead) return;
            IsDead = true;
            if (hive != null) hive.ReportDeath(IsAware);
            Apagao.Systems.SaveId.ReportGone(gameObject);
            SpawnLoot();

            Motor.enabled = false;
            if (statusLight != null) statusLight.enabled = false;
            foreach (var l in GetComponentsInChildren<Light>()) l.enabled = false;
            if (alertIndicator != null) alertIndicator.enabled = false;

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 5f;
            rb.linearVelocity = Motor.Velocity;
            rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.VelocityChange);
            Destroy(gameObject, 30f);
        }

        /// <summary>Some na hora, sem carcaça (explosão).</summary>
        protected void Vanish(bool reportAsMissing)
        {
            if (IsDead) return;
            IsDead = true;
            if (hive != null)
            {
                if (reportAsMissing) hive.ReportDeath(true);
                else hive.Leave();
            }
            Apagao.Systems.SaveId.ReportGone(gameObject);
            Destroy(gameObject);
        }

        private void SpawnLoot()
        {
            if (loot == null) return;
            Vector3 ground = transform.position;
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
                ground = hit.point;
            else
                ground.y = DroneMotor.GroundHeight(ground);

            foreach (var entry in loot)
            {
                if (entry.amount <= 0 || Random.value > entry.chance) continue;
                Vector2 offset = Random.insideUnitCircle * 0.8f;
                Apagao.Crafting.Pickup.Spawn(entry.type, entry.amount, ground + new Vector3(offset.x, 0f, offset.y));
            }
        }

        // ---------- Feedback ----------

        protected void SetStatusColor(Color color, float lightIntensity = -1f)
        {
            if (alertIndicator != null)
            {
                alertIndicator.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                alertIndicator.SetPropertyBlock(block);
            }
            if (statusLight != null)
            {
                statusLight.color = color;
                if (lightIntensity >= 0f) statusLight.intensity = lightIntensity;
            }
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            if (!Application.isPlaying || IsDead) return;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f,
                $"{StateName}{(hive != null && !hive.Connected ? "  [OFFLINE]" : "")}");
        }
#endif
    }
}
