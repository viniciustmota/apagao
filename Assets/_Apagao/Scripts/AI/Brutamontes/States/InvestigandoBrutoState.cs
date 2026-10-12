using UnityEngine;

namespace Apagao.AI.Brutamontes
{
    /// <summary>Vai até o ponto suspeito e olha em volta. Ver o Caio -> Combate.</summary>
    public class InvestigandoBrutoState : State<BrutamontesAI>
    {
        private float lookTimer;

        public InvestigandoBrutoState(BrutamontesAI owner) : base(owner) { }

        public override string Name => "Investigando";

        public override void Enter() => Restart();

        public void Restart()
        {
            lookTimer = 0f;
            Owner.MoveTo(Owner.LastKnownPosition, Owner.MoveSpeed);
        }

        public override void Tick()
        {
            if (Owner.CanSeePlayer) { Owner.Fsm.ChangeState(Owner.Combate); return; }
            if (!Owner.ReachedDestination) return;

            lookTimer += Time.deltaTime;
            Owner.transform.Rotate(0f, Owner.TurnSpeed * 0.5f * Time.deltaTime, 0f);
            if (lookTimer >= Owner.LookAroundTime)
            {
                Owner.Suspicion = 0f;
                Owner.Fsm.ChangeState(Owner.Patrulha);
            }
        }
    }
}
