namespace Apagao.AI.Sentinela
{
    /// <summary>
    /// Para e encara o ponto suspeito (vulto ou som). O medidor sobe enquanto vê o Caio e desce quando perde.
    /// Cheio -> Investigando. Vazio (depois de um tempo mínimo encarando) -> Patrulha ("esquece").
    /// </summary>
    public class SuspeitoState : State<SentinelaAI>
    {
        public SuspeitoState(SentinelaAI owner) : base(owner) { }

        public override string Name => "Suspeito";

        public override void Enter() => Owner.Stop();

        public override void Tick()
        {
            if (Owner.ShouldEngageImmediately) { Owner.Fsm.ChangeState(Owner.Combate); return; }

            if (Owner.CanSeePlayer) Owner.FillSuspicion();
            else Owner.DecaySuspicion();

            Owner.FaceTowards(Owner.LastKnownPosition, 180f);

            if (Owner.Suspicion >= 1f)
                Owner.InvestigatePoint(Owner.LastKnownPosition, fast: false);
            else if (Owner.Suspicion <= 0f && Owner.Fsm.TimeInState >= Owner.MinSuspiciousTime)
                Owner.Fsm.ChangeState(Owner.Patrulha);
        }
    }
}
