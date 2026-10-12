using UnityEngine;

namespace Apagao.AI.Sentinela
{
    /// <summary>
    /// Vai até a última posição conhecida e vasculha pontos aleatórios em volta.
    /// Reencontra -> Combate. Desiste -> Patrulha, mas em alerta elevado.
    /// </summary>
    public class ProcurandoState : State<SentinelaAI>
    {
        private float timer;

        public ProcurandoState(SentinelaAI owner) : base(owner) { }

        public override string Name => "Procurando";

        public override void Enter()
        {
            timer = 0f;
            Retarget();
        }

        /// <summary>Vai para o novo LastKnownPosition (ex.: ouviu um som durante a busca).</summary>
        public void Retarget() => Owner.MoveTo(Owner.LastKnownPosition, Owner.InvestigateSpeed);

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
                Owner.MoveTo(next, Owner.InvestigateSpeed);
        }
    }
}
