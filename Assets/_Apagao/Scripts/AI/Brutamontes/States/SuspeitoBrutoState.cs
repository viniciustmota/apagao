namespace Apagao.AI.Brutamontes
{
    /// <summary>Para e encara o ponto suspeito. Medidor cheio -> Investigando; vazio -> Patrulha.</summary>
    public class SuspeitoBrutoState : State<BrutamontesAI>
    {
        public SuspeitoBrutoState(BrutamontesAI owner) : base(owner) { }

        public override string Name => "Suspeito";

        public override void Enter() => Owner.Stop();

        public override void Tick()
        {
            if (Owner.ShouldEngageImmediately) { Owner.Fsm.ChangeState(Owner.Combate); return; }

            if (Owner.CanSeePlayer) Owner.FillSuspicion();
            else Owner.DecaySuspicion();

            Owner.FaceTowards(Owner.LastKnownPosition, Owner.TurnSpeed);

            if (Owner.Suspicion >= 1f)
                Owner.InvestigatePoint(Owner.LastKnownPosition);
            else if (Owner.Suspicion <= 0f && Owner.Fsm.TimeInState >= Owner.MinSuspiciousTime)
                Owner.Fsm.ChangeState(Owner.Patrulha);
        }
    }
}
