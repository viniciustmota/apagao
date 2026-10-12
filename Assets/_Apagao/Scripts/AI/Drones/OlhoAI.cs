using Apagao.Combat;
using UnityEngine;

namespace Apagao.AI.Drones
{
    /// <summary>
    /// Olho, drone de busca (spec 15 R4–R8): patrulha alto, enxerga com térmica para baixo e não ataca.
    /// Ao travar no Caio, segue por cima e reporta à rede a cada segundo (avistamento confirmado).
    /// </summary>
    public class OlhoAI : DroneAI
    {
        private enum State { Patrulha, Travando, Seguindo, Procurando }

        [Header("Rota")]
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float patrolAltitude = 12f;
        [SerializeField] private float patrolSpeed = 4f;

        [Header("Térmica")]
        [SerializeField] private float viewRange = 20f;
        [Tooltip("Metade do cone, a partir da vertical (cone total de 80°).")]
        [SerializeField] private float viewHalfAngle = 40f;
        [SerializeField] private float lockTime = 0.6f;

        [Header("Perseguição")]
        [SerializeField] private float followAltitude = 10f;
        [SerializeField] private float followSpeed = 7f;
        [SerializeField] private float reportInterval = 1f;
        [Tooltip("Sem ver o Caio por este tempo, passa a procurar.")]
        [SerializeField] private float loseTime = 4f;
        [SerializeField] private float searchTime = 8f;
        [SerializeField] private float searchRadius = 6f;
        [Tooltip("Avistamentos da rede até esta distância fazem o Olho ir procurar.")]
        [SerializeField] private float respondRadius = 40f;

        [Header("Holofote")]
        [SerializeField] private Light spotlight;

        private State state;
        private int waypointIndex;
        private float stateTime;
        private float lockProgress;
        private float lastSeen = float.NegativeInfinity;
        private float nextReport;

        public override bool IsAware => state != State.Patrulha;
        public override string StateName => state == State.Travando ? $"Travando {lockProgress / lockTime:P0}" : state.ToString();

        protected override void Awake()
        {
            base.Awake();
            if (spotlight != null) spotlight.spotAngle = viewHalfAngle * 2f; // o cone de luz é o cone de visão (R7)
        }

        private void ChangeState(State next)
        {
            state = next;
            stateTime = 0f;
        }

        protected override void Think()
        {
            stateTime += Time.deltaTime;
            bool sees = CanSeeFromAbove(viewRange, viewHalfAngle);
            if (sees)
            {
                LastKnownPosition = Player.position;
                lastSeen = Time.time;
            }

            switch (state)
            {
                case State.Patrulha: Patrol(sees); break;
                case State.Travando: Lock(sees); break;
                case State.Seguindo: Follow(sees); break;
                case State.Procurando: Search(sees); break;
            }
            UpdateFeedback();
        }

        private void Patrol(bool sees)
        {
            if (sees)
            {
                lockProgress = 0f;
                ChangeState(State.Travando);
                return;
            }
            if (waypoints == null || waypoints.Length == 0)
            {
                Motor.Hover();
                return;
            }
            Vector3 target = DroneMotor.AtAltitude(waypoints[waypointIndex].position, patrolAltitude);
            Motor.FlyTo(target, patrolSpeed);
            if (Motor.Arrived(target, 1.5f)) waypointIndex = (waypointIndex + 1) % waypoints.Length;
        }

        private void Lock(bool sees)
        {
            // Diminui a velocidade enquanto confirma o que viu.
            Motor.FlyTo(DroneMotor.AtAltitude(LastKnownPosition, patrolAltitude), patrolSpeed * 0.4f);
            lockProgress += sees ? Time.deltaTime : -Time.deltaTime;
            if (lockProgress >= lockTime)
            {
                nextReport = 0f;
                ChangeState(State.Seguindo);
            }
            else if (lockProgress <= 0f)
            {
                ChangeState(State.Patrulha);
            }
        }

        private void Follow(bool sees)
        {
            Motor.FlyTo(DroneMotor.AtAltitude(LastKnownPosition, followAltitude), followSpeed);

            if (sees && Time.time >= nextReport)
            {
                nextReport = Time.time + reportInterval;
                ReportSighting(LastKnownPosition, confirmed: true); // R6: liga "uma vez visto, marcado"
            }
            if (Time.time - lastSeen > loseTime) ChangeState(State.Procurando);
        }

        private void Search(bool sees)
        {
            if (sees)
            {
                nextReport = 0f;
                ChangeState(State.Seguindo); // já sabe quem é: não precisa travar de novo
                return;
            }
            // Círculos sobre a última posição conhecida.
            float angle = stateTime * (followSpeed / searchRadius);
            Vector3 center = LastKnownPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * searchRadius;
            Motor.FlyTo(DroneMotor.AtAltitude(center, followAltitude), followSpeed);
            if (stateTime >= searchTime) ChangeState(State.Patrulha);
        }

        protected override void HandleHiveSighting(Vector3 position, bool confirmed, bool converge)
        {
            if (state == State.Seguindo) return; // já está vendo com os próprios olhos
            if (!confirmed || Vector3.Distance(transform.position, position) > respondRadius) return;
            LastKnownPosition = position;
            ChangeState(State.Procurando);
        }

        protected override void HandleDamaged(DamageInfo info)
        {
            ReportSighting(LastKnownPosition, confirmed: true); // levou tiro: o Caio está ali
            if (state != State.Seguindo) ChangeState(State.Procurando);
        }

        private void UpdateFeedback()
        {
            Color color = state switch
            {
                State.Seguindo => new Color(1f, 0.15f, 0.1f),
                State.Travando => Color.Lerp(new Color(1f, 0.95f, 0.75f), new Color(1f, 0.75f, 0.1f), lockProgress / lockTime),
                State.Procurando => new Color(1f, 0.5f, 0.1f),
                _ => new Color(1f, 0.95f, 0.75f)
            };
            SetStatusColor(color);
            if (spotlight != null) spotlight.color = color;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Círculo da térmica no chão.
            float h = transform.position.y - DroneMotor.GroundHeight(transform.position);
            float r = Mathf.Min(h, viewRange) * Mathf.Tan(viewHalfAngle * Mathf.Deg2Rad);
            UnityEditor.Handles.color = new Color(1f, 0.9f, 0.3f, 0.6f);
            UnityEditor.Handles.DrawWireDisc(DroneMotor.AtAltitude(transform.position, 0.1f), Vector3.up, r);
        }
#endif
    }
}
