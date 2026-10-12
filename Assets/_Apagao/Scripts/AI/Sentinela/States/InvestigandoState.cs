using UnityEngine;

namespace Apagao.AI.Sentinela
{
    /// <summary>
    /// Vai até a última posição em que viu o Caio e olha em volta.
    /// Ver o Caio -> Combate. Nada encontrado -> Patrulha.
    /// </summary>
    public class InvestigandoState : State<SentinelaAI>
    {
        private float lookTimer;

        public InvestigandoState(SentinelaAI owner) : base(owner) { }

        public override string Name => "Investigando";

        public override void Enter()
        {
            lookTimer = 0f;
            Owner.MoveTo(Owner.LastKnownPosition, Owner.InvestigateSpeed);
        }

        public override void Tick()
        {
            if (Owner.CanSeePlayer) { Owner.Fsm.ChangeState(Owner.Combate); return; }
            if (!Owner.ReachedDestination) return;

            lookTimer += Time.deltaTime;
            Owner.transform.Rotate(0f, 90f * Time.deltaTime, 0f); // olha em volta

            if (lookTimer >= Owner.LookAroundTime)
            {
                Owner.Suspicion = 0f;
                Owner.Fsm.ChangeState(Owner.Patrulha);
            }
        }
    }
}
