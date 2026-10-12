using UnityEngine;

namespace Apagao.AI.Sentinela
{
    /// <summary>
    /// Vai até o ponto suspeito (último avistamento, som ou chamado da rede) e olha em volta.
    /// Ver o Caio -> Combate. Nada encontrado -> Patrulha.
    /// </summary>
    public class InvestigandoState : State<SentinelaAI>
    {
        private float lookTimer;

        public InvestigandoState(SentinelaAI owner) : base(owner) { }

        public override string Name => "Investigando";

        public override void Enter() => Restart();

        /// <summary>Recomeça indo para o novo LastKnownPosition (ex.: ouviu outro som no caminho).</summary>
        public void Restart()
        {
            lookTimer = 0f;
            Owner.MoveTo(Owner.LastKnownPosition, Owner.CurrentInvestigateSpeed);
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

        public override void Exit() => Owner.ClearInvestigateFast();
    }
}
