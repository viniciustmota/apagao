using Apagao.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Apagao.AI.Sentinela
{
    /// <summary>
    /// Sentinela: robô humanoide base. Patrulha -> Suspeito -> Investigando -> Combate -> Procurando.
    /// Este componente guarda os dados e os sentidos; o comportamento fica em uma classe por estado (pasta States).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(VisionSensor))]
    public class SentinelaAI : MonoBehaviour
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

        [Header("Investigação e busca")]
        [SerializeField] private float investigateSpeed = 2.2f;
        [SerializeField] private float lookAroundTime = 4f;
        [SerializeField] private float searchDuration = 12f;
        [SerializeField] private float searchRadius = 8f;

        [Header("Combate")]
        [SerializeField] private float chaseSpeed = 3.5f;
        [SerializeField] private float engageDistance = 8f;
        [SerializeField] private float loseSightTime = 2f;

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
        public float InvestigateSpeed => investigateSpeed;
        public float LookAroundTime => lookAroundTime;
        public float SearchDuration => searchDuration;
        public float SearchRadius => searchRadius;
        public float ChaseSpeed => chaseSpeed;
        public float EngageDistance => engageDistance;
        public float LoseSightTime => loseSightTime;

        public bool CanSeePlayer { get; private set; }
        public float PlayerVisibility { get; private set; }
        public float DistanceToPlayer { get; private set; } = float.MaxValue;
        public Vector3 LastKnownPosition { get; set; }
        public float Suspicion { get; set; }
        public bool HeightenedAlert { get; set; }
        public bool ShouldEngageImmediately => CanSeePlayer && DistanceToPlayer <= instantDetectDistance;
        public bool ReachedDestination =>
            !Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + 0.2f;

        // ---------- Estados ----------
        public PatrulhaState Patrulha { get; private set; }
        public SuspeitoState Suspeito { get; private set; }
        public InvestigandoState Investigando { get; private set; }
        public CombateState Combate { get; private set; }
        public ProcurandoState Procurando { get; private set; }

        public StateMachine<SentinelaAI> Fsm { get; } = new StateMachine<SentinelaAI>();
        public string StateName => Fsm.Current?.Name ?? "-";

        private PlayerController playerController;
        private MaterialPropertyBlock indicatorBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Vision = GetComponent<VisionSensor>();
            playerController = FindAnyObjectByType<PlayerController>();
            Player = playerController != null ? playerController.transform : null;
            indicatorBlock = new MaterialPropertyBlock();

            Patrulha = new PatrulhaState(this);
            Suspeito = new SuspeitoState(this);
            Investigando = new InvestigandoState(this);
            Combate = new CombateState(this);
            Procurando = new ProcurandoState(this);
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
            Sense();
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

        // ---------- Ações usadas pelos estados ----------

        public void FillSuspicion()
        {
            float mult = HeightenedAlert ? heightenedSuspicionMultiplier : 1f;
            Suspicion = Mathf.Clamp01(Suspicion + Time.deltaTime / suspicionFillTime * PlayerVisibility * mult);
        }

        public void DecaySuspicion() =>
            Suspicion = Mathf.Clamp01(Suspicion - suspicionDecay * Time.deltaTime);

        public void MoveTo(Vector3 destination, float speed)
        {
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
                $"{StateName}  {Suspicion:P0}{(HeightenedAlert ? "  [ALERTA]" : "")}");
        }
#endif
    }
}
