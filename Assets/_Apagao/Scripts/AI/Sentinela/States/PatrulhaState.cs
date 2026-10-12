using UnityEngine;

namespace Apagao.AI.Sentinela
{
    /// <summary>Segue os waypoints em loop, parando um pouco em cada um. Ver o Caio -> Suspeito.</summary>
    public class PatrulhaState : State<SentinelaAI>
    {
        private int index;
        private float pauseTimer;

        public PatrulhaState(SentinelaAI owner) : base(owner) { }

        public override string Name => "Patrulha";

        public override void Enter()
        {
            pauseTimer = 0f;
            GoToCurrentWaypoint();
        }

        public override void Tick()
        {
            if (Owner.ShouldEngageImmediately) { Owner.Fsm.ChangeState(Owner.Combate); return; }
            if (Owner.CanSeePlayer) { Owner.Fsm.ChangeState(Owner.Suspeito); return; }

            Owner.DecaySuspicion();

            var points = Owner.Waypoints;
            if (points == null || points.Length == 0 || !Owner.ReachedDestination) return;

            pauseTimer += Time.deltaTime;
            if (pauseTimer < Owner.WaypointPause) return;

            pauseTimer = 0f;
            index = (index + 1) % points.Length;
            GoToCurrentWaypoint();
        }

        private void GoToCurrentWaypoint()
        {
            var points = Owner.Waypoints;
            if (points == null || points.Length == 0) { Owner.Stop(); return; }
            Owner.MoveTo(points[index].position, Owner.PatrolSpeed);
        }
    }
}
