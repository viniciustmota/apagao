using UnityEngine;

namespace Apagao.AI.Sentinela
{
    /// <summary>
    /// Sentinela: robô humanoide base (spec 05). Patrulha -> Suspeito -> Investigando -> Combate -> Procurando.
    /// Sentidos, rede, vida e takedown vêm de MachineAI; aqui ficam os parâmetros, as reações e o rifle.
    /// </summary>
    public class SentinelaAI : MachineAI
    {
        [Header("Patrulha")]
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float patrolSpeed = 1.6f;
        [SerializeField] private float waypointPause = 1.5f;

        [Header("Suspeito")]
        [Tooltip("Tempo mínimo encarando o ponto suspeito antes de voltar à patrulha.")]
        [SerializeField] private float minSuspiciousTime = 2.5f;

        [Header("Audição (spec 06)")]
        [Tooltip("Quanto da intensidade do som vira suspeita.")]
        [SerializeField] private float noiseToSuspicion = 0.6f;
        [Tooltip("Sons a partir desta intensidade levam direto a Investigando.")]
        [SerializeField] private float loudNoiseThreshold = 0.8f;

        [Header("Investigação e busca")]
        [SerializeField] private float investigateSpeed = 2.2f;
        [SerializeField] private float lookAroundTime = 4f;
        [SerializeField] private float searchDuration = 12f;
        [SerializeField] private float searchRadius = 8f;

        [Header("Combate")]
        [SerializeField] private float chaseSpeed = 3.5f;
        [SerializeField] private float engageDistance = 8f;
        [SerializeField] private float loseSightTime = 2f;

        [Header("Rifle")]
        [SerializeField] private LineRenderer laser;

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

        // ---------- Estados ----------
        public PatrulhaState Patrulha { get; private set; }
        public SuspeitoState Suspeito { get; private set; }
        public InvestigandoState Investigando { get; private set; }
        public CombateState Combate { get; private set; }
        public ProcurandoState Procurando { get; private set; }

        public StateMachine<SentinelaAI> Fsm { get; } = new StateMachine<SentinelaAI>();
        public override string StateName => Fsm.Current?.Name ?? "-";

        public override AlertLevel Alert
        {
            get
            {
                var s = Fsm.Current;
                if (s == Combate) return AlertLevel.Combate;
                if (s == Procurando) return AlertLevel.Procurando;
                if (s == Investigando) return AlertLevel.Investigando;
                if (s == Suspeito) return AlertLevel.Suspeito;
                return AlertLevel.Nenhum;
            }
        }

        private SentinelaGun gun;
        private bool investigateFast;

        protected override void Awake()
        {
            base.Awake();
            gun = GetComponent<SentinelaGun>();

            Patrulha = new PatrulhaState(this);
            Suspeito = new SuspeitoState(this);
            Investigando = new InvestigandoState(this);
            Combate = new CombateState(this);
            Procurando = new ProcurandoState(this);
        }

        protected override void BeginBehaviour() => Fsm.ChangeState(Patrulha);
        protected override void Think() => Fsm.Tick();
        protected override void EnterCombat() => Fsm.ChangeState(Combate);

        // ---------- Rede ----------

        protected override void HandleHiveSighting(Vector3 position, bool confirmed, bool converge)
        {
            if (Fsm.Current == Combate)
            {
                if (!CanSeePlayer) LastKnownPosition = position;
                return;
            }
            LastKnownPosition = position;
            if (converge) InvestigatePoint(position, fast: true);
        }

        protected override void HandleHiveMemberMissing(Vector3 lastPosition, bool investigate)
        {
            if (investigate && Fsm.Current != Combate)
                InvestigatePoint(lastPosition, fast: false);
        }

        // ---------- Audição ----------

        protected override void HandleNoise(HeardNoise noise)
        {
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

        // ---------- Ações usadas pelos estados ----------

        public void InvestigatePoint(Vector3 point, bool fast)
        {
            LastKnownPosition = point;
            investigateFast = fast;
            if (Fsm.Current == Investigando) Investigando.Restart();
            else Fsm.ChangeState(Investigando);
        }

        public void ClearInvestigateFast() => investigateFast = false;

        /// <summary>Chamado pelo CombateState: atira se estiver vendo, no alcance e de frente para o Caio.</summary>
        public void TickGun(bool wantsToShoot)
        {
            if (gun == null) return;
            bool canShoot = wantsToShoot && CanSeePlayer && DistanceToPlayer <= gun.Range && IsFacingPlayer(20f);
            gun.Tick(canShoot, Vision.EyePosition + transform.forward * 0.3f, Player, PlayerDamageable, PlayerMove);
        }

        public void ResetGun()
        {
            if (gun != null) gun.ResetAim();
        }

        protected override void OnFrozen()
        {
            ResetGun();
            if (laser != null) laser.enabled = false;
        }

        protected override void UpdateExtraFeedback()
        {
            if (laser == null) return;
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
}
