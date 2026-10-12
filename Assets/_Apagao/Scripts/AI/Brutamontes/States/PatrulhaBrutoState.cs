namespace Apagao.AI.Brutamontes
{
    /// <summary>Ronda lenta pelos waypoints. Ver o Caio -> Suspeito (ou Combate se estiver muito perto).</summary>
    public class PatrulhaBrutoState : State<BrutamontesAI>
    {
        private int index;
        private float pauseTimer;

        public PatrulhaBrutoState(BrutamontesAI owner) : base(owner) { }

        public override string Name => "Patrulha";

        public override void Enter()
        {
            pauseTimer = 0f;
            GoToCurrent();
        }

        public override void Tick()
        {
            if (Owner.ShouldEngageImmediately) { Owner.Fsm.ChangeState(Owner.Combate); return; }
            if (Owner.CanSeePlayer) { Owner.Fsm.ChangeState(Owner.Suspeito); return; }

            Owner.DecaySuspicion();
            var points = Owner.Waypoints;
            if (points == null || points.Length == 0 || !Owner.ReachedDestination) return;

            pauseTimer += UnityEngine.Time.deltaTime;
            if (pauseTimer < Owner.WaypointPause) return;
            pauseTimer = 0f;
            index = (index + 1) % points.Length;
            GoToCurrent();
        }

        private void GoToCurrent()
        {
            var points = Owner.Waypoints;
            if (points == null || points.Length == 0) { Owner.Stop(); return; }
            Owner.MoveTo(points[index].position, Owner.PatrolSpeed);
        }
    }
}
