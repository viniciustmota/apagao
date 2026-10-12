using UnityEngine;

namespace Apagao.AI.Matilha
{
    /// <summary>O líder segue os waypoints do grupo; os outros andam em formação atrás dele.</summary>
    public class PatrulhaCaoState : State<MatilhaAI>
    {
        private int index;
        private float pauseTimer;
        private float followTimer;

        public PatrulhaCaoState(MatilhaAI owner) : base(owner) { }

        public override string Name => "Patrulha";

        public override void Enter()
        {
            pauseTimer = 0f;
            followTimer = 0f;
            Owner.Listening = false;
            if (Owner.IsLeader) GoToWaypoint();
        }

        public override void Tick()
        {
            if (Owner.ShouldEngageImmediately) { Owner.Fsm.ChangeState(Owner.Combate); return; }
            if (Owner.CanSeePlayer) { Owner.Fsm.ChangeState(Owner.Suspeito); return; }
            Owner.DecaySuspicion();

            if (!Owner.IsLeader)
            {
                followTimer -= Time.deltaTime;
                if (followTimer > 0f) return;
                followTimer = 0.3f;
                Owner.MoveTo(Owner.Group.FormationPoint(Owner), Owner.FollowSpeed);
                return;
            }

            var points = Owner.Group != null ? Owner.Group.Waypoints : null;
            if (points == null || points.Length == 0 || !Owner.ReachedDestination) return;

            pauseTimer += Time.deltaTime;
            if (pauseTimer < Owner.Group.WaypointPause) return;
            pauseTimer = 0f;
            index = (index + 1) % points.Length;
            GoToWaypoint();
        }

        private void GoToWaypoint()
        {
            var points = Owner.Group != null ? Owner.Group.Waypoints : null;
            if (points == null || points.Length == 0) { Owner.Stop(); return; }
            index %= points.Length;
            Owner.MoveTo(points[index].position, Owner.PatrolSpeed);
        }
    }
}
