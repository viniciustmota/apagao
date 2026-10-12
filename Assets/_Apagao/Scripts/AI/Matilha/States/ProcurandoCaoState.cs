using UnityEngine;

namespace Apagao.AI.Matilha
{
    /// <summary>Vasculha pontos em volta da última posição conhecida. Desiste -> Patrulha em alerta elevado.</summary>
    public class ProcurandoCaoState : State<MatilhaAI>
    {
        private float timer;

        public ProcurandoCaoState(MatilhaAI owner) : base(owner) { }

        public override string Name => "Procurando";

        public override void Enter()
        {
            timer = 0f;
            Owner.MoveTo(Owner.LastKnownPosition, Owner.TrackSpeed);
        }

        public override void Tick()
        {
            if (Owner.CanSeePlayer) { Owner.Fsm.ChangeState(Owner.Combate); return; }

            timer += Time.deltaTime;
            if (timer >= Owner.SearchDuration)
            {
                Owner.Suspicion = 0f;
                Owner.Fsm.ChangeState(Owner.Patrulha);
                return;
            }

            if (Owner.ReachedDestination &&
                Owner.RandomPointNear(Owner.LastKnownPosition, Owner.SearchRadius, out Vector3 next))
                Owner.MoveTo(next, Owner.TrackSpeed);
        }
    }
}
