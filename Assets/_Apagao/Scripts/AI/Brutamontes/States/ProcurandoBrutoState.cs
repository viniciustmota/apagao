using UnityEngine;

namespace Apagao.AI.Brutamontes
{
    /// <summary>Vasculha em volta da última posição conhecida. Reencontra -> Combate; desiste -> Patrulha.</summary>
    public class ProcurandoBrutoState : State<BrutamontesAI>
    {
        private float timer;

        public ProcurandoBrutoState(BrutamontesAI owner) : base(owner) { }

        public override string Name => "Procurando";

        public override void Enter()
        {
            timer = 0f;
            Retarget();
        }

        public void Retarget() => Owner.MoveTo(Owner.LastKnownPosition, Owner.MoveSpeed);

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
            if (Owner.ReachedDestination && Owner.RandomPointNear(Owner.LastKnownPosition, Owner.SearchRadius, out Vector3 next))
                Owner.MoveTo(next, Owner.MoveSpeed);
        }
    }
}
