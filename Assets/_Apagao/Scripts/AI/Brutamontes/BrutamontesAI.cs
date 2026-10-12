using Apagao.Combat;
using Apagao.Player;
using Apagao.Systems;
using UnityEngine;

namespace Apagao.AI.Brutamontes
{
    /// <summary>
    /// Brutamontes, mini-chefe (spec 16): lento, blindado na frente, soco e investida com aviso.
    /// Investida contra parede = 2 s atordoado. PEM paralisa só 1,5 s. Takedown só fraco (&lt; 30%) e paralisado.
    /// </summary>
    public class BrutamontesAI : MachineAI
    {
        [Header("Patrulha")]
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float patrolSpeed = 1.2f;
        [SerializeField] private float waypointPause = 2.5f;

        [Header("Suspeita e busca")]
        [SerializeField] private float minSuspiciousTime = 2.5f;
        [SerializeField] private float noiseToSuspicion = 0.5f;
        [SerializeField] private float loudNoiseThreshold = 0.8f;
        [SerializeField] private float lookAroundTime = 4f;
        [SerializeField] private float searchDuration = 15f;
        [SerializeField] private float searchRadius = 8f;

        [Header("Combate (R2)")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float turnSpeed = 90f;
        [SerializeField] private float loseSightTime = 3f;

        [Header("Soco (R4)")]
        [SerializeField] private float punchDamage = 40f;
        [SerializeField] private float punchRange = 2.5f;
        [SerializeField] private float punchWindup = 0.8f;
        [SerializeField] private float punchRecovery = 0.8f;

        [Header("Investida (R4)")]
        [SerializeField] private float chargeDamage = 50f;
        [SerializeField] private float chargeMinDistance = 4.5f;
        [SerializeField] private float chargeMaxDistance = 12f;
        [SerializeField] private float chargeWindup = 1.2f;
        [Tooltip("Parte final do aviso em que a direção já está travada.")]
        [SerializeField] private float chargeLockBeforeEnd = 0.35f;
        [SerializeField] private float chargeDistance = 8f;
        [SerializeField] private float chargeSpeed = 12f;
        [SerializeField] private float chargeCooldown = 6f;
        [SerializeField] private float wallStunTime = 2f;

        [Header("Takedown (R6)")]
        [SerializeField, Range(0f, 1f)] private float takedownHealthThreshold = 0.3f;

        [Header("Passos (R7)")]
        [SerializeField] private float stepLength = 1.4f;
        [SerializeField] private float tremorRadius = 15f;
        [SerializeField] private float tremorAmplitude = 0.1f;

        [Header("Visual")]
        [SerializeField] private Transform rightArm;

        public Transform[] Waypoints => waypoints;
        public float PatrolSpeed => patrolSpeed;
        public float WaypointPause => waypointPause;
        public float MinSuspiciousTime => minSuspiciousTime;
        public float LookAroundTime => lookAroundTime;
        public float SearchDuration => searchDuration;
        public float SearchRadius => searchRadius;
        public float MoveSpeed => moveSpeed;
        public float TurnSpeed => turnSpeed;
        public float LoseSightTime => loseSightTime;
        public float WallStunTime => wallStunTime;

        // ---------- Estados ----------
        public PatrulhaBrutoState Patrulha { get; private set; }
        public SuspeitoBrutoState Suspeito { get; private set; }
        public InvestigandoBrutoState Investigando { get; private set; }
        public CombateBrutoState Combate { get; private set; }
        public AtordoadoState Atordoado { get; private set; }
        public ProcurandoBrutoState Procurando { get; private set; }

        public StateMachine<BrutamontesAI> Fsm { get; } = new StateMachine<BrutamontesAI>();
        public override string StateName => Fsm.Current == Combate ? $"Combate ({Combate.Phase})" : Fsm.Current?.Name ?? "-";

        public override AlertLevel Alert
        {
            get
            {
                var s = Fsm.Current;
                if (s == Combate || s == Atordoado) return AlertLevel.Combate;
                if (s == Procurando) return AlertLevel.Procurando;
                if (s == Investigando) return AlertLevel.Investigando;
                if (s == Suspeito) return AlertLevel.Suspeito;
                return AlertLevel.Nenhum;
            }
        }

        private ThirdPersonCamera cameraRig;
        private float stepProgress;
        private float nextChargeTime;
        private Quaternion armRest;

        protected override void Awake()
        {
            base.Awake();
            cameraRig = FindAnyObjectByType<ThirdPersonCamera>();
            if (rightArm != null) armRest = rightArm.localRotation;

            Patrulha = new PatrulhaBrutoState(this);
            Suspeito = new SuspeitoBrutoState(this);
            Investigando = new InvestigandoBrutoState(this);
            Combate = new CombateBrutoState(this);
            Atordoado = new AtordoadoState(this);
            Procurando = new ProcurandoBrutoState(this);
        }

        protected override void BeginBehaviour() => Fsm.ChangeState(Patrulha);
        protected override void Think() => Fsm.Tick();

        /// <summary>Levou dano ou foi chamado: combate, sem interromper o atordoamento.</summary>
        protected override void EnterCombat()
        {
            if (Fsm.Current == Atordoado || Fsm.Current == Combate) return;
            Fsm.ChangeState(Combate);
        }

        // ---------- Rede e audição ----------

        protected override void HandleHiveSighting(Vector3 position, bool confirmed, bool converge)
        {
            if (Fsm.Current == Combate || Fsm.Current == Atordoado)
            {
                if (!CanSeePlayer) LastKnownPosition = position;
                return;
            }
            LastKnownPosition = position;
            if (converge) InvestigatePoint(position);
        }

        protected override void HandleHiveMemberMissing(Vector3 lastPosition, bool investigate)
        {
            if (investigate && Alert != AlertLevel.Combate) InvestigatePoint(lastPosition);
        }

        protected override void HandleNoise(HeardNoise noise)
        {
            var state = Fsm.Current;
            Vector3 p = noise.EstimatedPosition;
            if (state == Combate || state == Atordoado)
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
                InvestigatePoint(p);
                return;
            }

            LastKnownPosition = p;
            Suspicion = Mathf.Clamp01(Suspicion + noise.Intensity * noiseToSuspicion);
            if (noise.Intensity >= loudNoiseThreshold || Suspicion >= 1f) InvestigatePoint(p);
            else Fsm.ChangeState(Suspeito);
        }

        public void InvestigatePoint(Vector3 point)
        {
            LastKnownPosition = point;
            if (Fsm.Current == Investigando) Investigando.Restart();
            else Fsm.ChangeState(Investigando);
        }

        // ---------- Ataques (usados pelo CombateBrutoState) ----------

        public bool PunchReady => CanSeePlayer && DistanceToPlayer <= punchRange && IsFacingPlayer(35f);
        public float PunchWindup => punchWindup;
        public float PunchRecovery => punchRecovery;

        public bool ChargeReady =>
            CanSeePlayer && Time.time >= nextChargeTime &&
            DistanceToPlayer >= chargeMinDistance && DistanceToPlayer <= chargeMaxDistance && IsFacingPlayer(25f);
        public float ChargeWindup => chargeWindup;
        public float ChargeLockBeforeEnd => chargeLockBeforeEnd;
        public float ChargeDistance => chargeDistance;
        public float ChargeSpeed => chargeSpeed;

        /// <summary>O golpe sai no fim do aviso: acerta se o Caio ainda estiver na frente e perto.</summary>
        public void Punch()
        {
            if (Player == null || PlayerDamageable == null || PlayerDamageable.IsDead) return;
            Vector3 to = Player.position - transform.position;
            to.y = 0f;
            bool inFront = Vector3.Angle(transform.forward, to) <= 50f;
            if (to.magnitude <= punchRange + 0.3f && inFront)
            {
                PlayerDamageable.TakeDamage(new DamageInfo(punchDamage, DamageType.Impacto, Player.position + Vector3.up, to.normalized, gameObject));
                if (cameraRig != null) cameraRig.Shake(0.25f, 0.25f);
            }
            NoiseSystem.Emit(transform.position, 10f, 0.6f, NoiseType.Impacto, gameObject);
        }

        public void StartChargeCooldown() => nextChargeTime = Time.time + chargeCooldown;

        /// <summary>Dano da investida no Caio (uma vez por investida).</summary>
        public bool TryChargeHit()
        {
            if (Player == null || PlayerDamageable == null || PlayerDamageable.IsDead) return false;
            Vector3 to = Player.position - transform.position;
            to.y = 0f;
            if (to.magnitude > 1.7f) return false;
            PlayerDamageable.TakeDamage(new DamageInfo(chargeDamage, DamageType.Impacto, Player.position + Vector3.up, transform.forward, gameObject));
            if (cameraRig != null) cameraRig.Shake(0.35f, 0.3f);
            return true;
        }

        /// <summary>Parede logo à frente (não conta o Caio, o terreno nem a si mesmo).</summary>
        public bool WallAhead(float distance)
        {
            var hits = Physics.SphereCastAll(transform.position + Vector3.up * 1.2f, 0.5f, transform.forward, distance, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                Transform t = h.collider.transform;
                if (t.IsChildOf(transform) || (Player != null && t.IsChildOf(Player))) continue;
                if (h.collider is TerrainCollider) continue;
                return true;
            }
            return false;
        }

        public void SlamWall()
        {
            NoiseSystem.Emit(transform.position, 20f, 1f, NoiseType.Impacto, gameObject);
            Tremor(0.35f, 0.4f);
        }

        /// <summary>Move sem rota (investida). O NavMesh ainda segura nas bordas.</summary>
        public Vector3 Shove(Vector3 delta)
        {
            if (!Agent.isOnNavMesh) return Vector3.zero;
            Vector3 before = transform.position;
            Agent.Move(delta);
            return transform.position - before;
        }

        public void HaltPath()
        {
            if (!Agent.isOnNavMesh) return;
            Agent.isStopped = true;
            Agent.ResetPath();
        }

        // ---------- PEM e takedown ----------

        /// <summary>R6: só com pouca vida e paralisado pelo PEM.</summary>
        public override bool CanBeTakenDown(Transform attacker) =>
            !IsDead && !IsFrozen && IsStunned && health != null && health.Normalized < takedownHealthThreshold;

        protected override void OnStunned() => SetArmRaise(0f);
        protected override void ResumeAfterStun()
        {
            if (Fsm.Current == Combate) Fsm.Restart(); // não retoma uma investida pela metade
            else Fsm.ChangeState(Combate);
        }
        protected override void OnFrozen() => SetArmRaise(0f);

        // ---------- Feedback ----------

        /// <summary>0 = braço solto, 1 = erguido para o soco.</summary>
        public void SetArmRaise(float t)
        {
            if (rightArm == null) return;
            rightArm.localRotation = armRest * Quaternion.Euler(-120f * t, 0f, 0f);
        }

        protected override void UpdateExtraFeedback()
        {
            // R7: cada passo faz o chão tremer perto dele.
            float speed = Agent.velocity.magnitude;
            if (speed < 0.2f) return;
            stepProgress += speed * Time.deltaTime;
            if (stepProgress < stepLength) return;
            stepProgress = 0f;
            Tremor(tremorAmplitude, 0.15f);
        }

        public void Tremor(float amplitude, float duration)
        {
            if (cameraRig == null || Player == null) return;
            float d = Vector3.Distance(transform.position, Player.position);
            if (d > tremorRadius) return;
            cameraRig.Shake(amplitude * (1f - d / tremorRadius), duration);
        }
    }
}
