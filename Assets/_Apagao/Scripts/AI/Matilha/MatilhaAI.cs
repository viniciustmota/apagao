using Apagao.Combat;
using Apagao.Player;
using UnityEngine;

namespace Apagao.AI.Matilha
{
    /// <summary>
    /// Cão robô (spec 14). Caça em grupo: ouve longe, fareja o rastro do Caio, cerca e dá o bote.
    /// Sentidos, rede, vida e takedown vêm de MachineAI.
    /// </summary>
    public class MatilhaAI : MachineAI, ILureable
    {
        [Header("Movimento (m/s)")]
        [SerializeField] private float patrolSpeed = 2.2f;
        [SerializeField] private float followSpeed = 3f;
        [SerializeField] private float trackSpeed = 4.5f;
        [SerializeField] private float chaseSpeed = 7f;

        [Header("Suspeito")]
        [SerializeField] private float minSuspiciousTime = 2f;
        [Tooltip("Sons abaixo desta intensidade só deixam o cão desconfiado; acima, ele vai rastrear.")]
        [SerializeField] private float quietNoiseThreshold = 0.25f;

        [Header("Faro (spec 14 R3)")]
        [SerializeField] private float sniffRadius = 6f;
        [Tooltip("Sem achar rastro por este tempo, desiste e procura em volta.")]
        [SerializeField] private float trackGiveUp = 6f;

        [Header("Busca")]
        [SerializeField] private float searchDuration = 10f;
        [SerializeField] private float searchRadius = 8f;
        [SerializeField] private float loseSightTime = 3f;

        [Header("Combate em grupo (spec 14 R6)")]
        [SerializeField] private float holdDistance = 4f;
        [SerializeField] private float holdCreepSpeed = 2.5f;
        [SerializeField] private float flankDistance = 4f;
        [SerializeField] private float flankAngle = 110f;

        [Header("Bote (spec 14 R7)")]
        [SerializeField] private float attackRange = 2.2f;
        [SerializeField] private float telegraphTime = 0.4f;
        [SerializeField] private float pounceSpeed = 9f;
        [SerializeField] private float pounceTime = 0.3f;
        [SerializeField] private float biteDamage = 20f;
        [SerializeField] private float biteRange = 1.3f;
        [SerializeField] private float attackCooldown = 1f;

        [Header("Visual")]
        [SerializeField] private Transform head;
        [SerializeField] private Transform body;

        public float PatrolSpeed => patrolSpeed;
        public float FollowSpeed => followSpeed;
        public float TrackSpeed => trackSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float MinSuspiciousTime => minSuspiciousTime;
        public float SniffRadius => sniffRadius;
        public float TrackGiveUp => trackGiveUp;
        public float SearchDuration => searchDuration;
        public float SearchRadius => searchRadius;
        public float LoseSightTime => loseSightTime;
        public float HoldDistance => holdDistance;
        public float HoldCreepSpeed => holdCreepSpeed;
        public float FlankDistance => flankDistance;
        public float FlankAngle => flankAngle;
        public float AttackRange => attackRange;
        public float TelegraphTime => telegraphTime;
        public float PounceSpeed => pounceSpeed;
        public float PounceTime => pounceTime;
        public float AttackCooldown => attackCooldown;

        public MatilhaGroup Group { get; private set; }
        public PlayerScent Scent { get; private set; }
        public bool IsLeader => Group == null || Group.Leader == this;
        /// <summary>Cabeça inclinada "escutando" ou farejando.</summary>
        public bool Listening { get; set; }

        // ---------- Estados ----------
        public PatrulhaCaoState Patrulha { get; private set; }
        public SuspeitoCaoState Suspeito { get; private set; }
        public RastreandoState Rastreando { get; private set; }
        public CombateCaoState Combate { get; private set; }
        public ProcurandoCaoState Procurando { get; private set; }

        public StateMachine<MatilhaAI> Fsm { get; } = new StateMachine<MatilhaAI>();
        public override string StateName => Fsm.Current?.Name ?? "-";

        public override AlertLevel Alert
        {
            get
            {
                var s = Fsm.Current;
                if (s == Combate) return AlertLevel.Combate;
                if (s == Procurando) return AlertLevel.Procurando;
                if (s == Rastreando) return AlertLevel.Investigando;
                if (s == Suspeito) return AlertLevel.Suspeito;
                return AlertLevel.Nenhum;
            }
        }

        private Vector3 bodyScale;
        private Quaternion headRest;
        private bool squatting;
        private float listenTimer;

        protected override void Awake()
        {
            base.Awake();
            Group = GetComponentInParent<MatilhaGroup>();
            Scent = Player != null ? Player.GetComponent<PlayerScent>() : null;
            if (body != null) bodyScale = body.localScale;
            if (head != null) headRest = head.localRotation;

            Patrulha = new PatrulhaCaoState(this);
            Suspeito = new SuspeitoCaoState(this);
            Rastreando = new RastreandoState(this);
            Combate = new CombateCaoState(this);
            Procurando = new ProcurandoCaoState(this);
        }

        protected override void BeginBehaviour() => Fsm.ChangeState(Patrulha);
        protected override void Think() => Fsm.Tick();
        protected override void EnterCombat() => Fsm.ChangeState(Combate);

        // ---------- Grupo ----------

        /// <summary>Chamado pelo grupo quando outro cão entra em combate ou começa a rastrear.</summary>
        public void JoinPack(Vector3 position, bool combat)
        {
            if (IsDead || IsFrozen) return;
            HeightenedAlert = true;

            if (combat)
            {
                if (Fsm.Current == Combate) return;
                LastKnownPosition = position;
                Fsm.ChangeState(Combate);
                return;
            }

            if (Fsm.Current == Combate || Fsm.Current == Rastreando) return;
            TrackTo(position);
        }

        /// <summary>Vai até o ponto e começa a farejar.</summary>
        public void TrackTo(Vector3 position)
        {
            LastKnownPosition = position;
            if (Fsm.Current == Rastreando) Rastreando.Retarget();
            else Fsm.ChangeState(Rastreando);
        }

        // ---------- Rede e audição ----------

        protected override void HandleHiveSighting(Vector3 position, bool confirmed, bool converge)
        {
            if (Fsm.Current == Combate)
            {
                if (!CanSeePlayer) LastKnownPosition = position;
                return;
            }
            LastKnownPosition = position;
            if (converge) TrackTo(position);
        }

        protected override void HandleHiveMemberMissing(Vector3 lastPosition, bool investigate)
        {
            if (investigate && Fsm.Current != Combate) TrackTo(lastPosition);
        }

        protected override void HandleNoise(HeardNoise noise)
        {
            listenTimer = 1.2f; // inclina a cabeça para escutar
            Vector3 p = noise.EstimatedPosition;

            if (Fsm.Current == Combate)
            {
                if (!CanSeePlayer) LastKnownPosition = p;
                return;
            }
            if (Fsm.Current == Rastreando)
            {
                TrackTo(p);
                return;
            }

            LastKnownPosition = p;
            if (noise.Intensity < quietNoiseThreshold && Fsm.Current != Procurando)
            {
                Suspicion = Mathf.Clamp01(Suspicion + noise.Intensity);
                Fsm.ChangeState(Suspeito);
            }
            else
            {
                TrackTo(p);
            }
        }

        // ---------- Ataque ----------

        /// <summary>Mordida no fim do bote, se o Caio estiver perto.</summary>
        public bool TryBite()
        {
            if (Player == null || PlayerDamageable == null || DistanceToPlayer > biteRange) return false;
            Vector3 dir = Player.position - transform.position;
            dir.y = 0f;
            PlayerDamageable.TakeDamage(new DamageInfo(biteDamage, DamageType.Corte, Player.position + Vector3.up, dir.normalized, gameObject));
            return true;
        }

        /// <summary>Agacha antes do bote: o aviso visual de 0,4 s.</summary>
        public void Squat(bool on) => squatting = on;

        protected override void OnFrozen() => Squat(false);
        protected override void OnStunned() => Squat(false);

        /// <summary>A isca elétrica prende o cão (spec 12 R6).</summary>
        public void Lure(float stunSeconds) => Stun(stunSeconds);
        protected override void ResumeAfterStun() => Fsm.Restart();

        protected override void UpdateExtraFeedback()
        {
            listenTimer -= Time.deltaTime;

            if (head != null)
            {
                bool tilt = Listening || listenTimer > 0f || Fsm.Current == Suspeito;
                Quaternion target = tilt ? headRest * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 3f) * 25f) : headRest;
                head.localRotation = Quaternion.Slerp(head.localRotation, target, Time.deltaTime * 8f);
            }

            if (body != null)
            {
                Vector3 target = squatting
                    ? new Vector3(bodyScale.x * 1.1f, bodyScale.y * 0.7f, bodyScale.z) + Random.insideUnitSphere * 0.02f
                    : bodyScale;
                body.localScale = Vector3.Lerp(body.localScale, target, Time.deltaTime * 20f);
            }
        }
    }
}
