namespace Apagao.AI.Matilha
{
    /// <summary>
    /// Para, inclina a cabeça e encara o ponto. Medidor cheio: vendo o Caio -> Combate; sem ver -> Rastreando.
    /// </summary>
    public class SuspeitoCaoState : State<MatilhaAI>
    {
        public SuspeitoCaoState(MatilhaAI owner) : base(owner) { }

        public override string Name => "Suspeito";

        public override void Enter()
        {
            Owner.Stop();
            Owner.Listening = true;
        }

        public override void Exit() => Owner.Listening = false;

        public override void Tick()
        {
            if (Owner.ShouldEngageImmediately) { Owner.Fsm.ChangeState(Owner.Combate); return; }

            if (Owner.CanSeePlayer) Owner.FillSuspicion(1.5f); // cães desconfiam mais rápido
            else Owner.DecaySuspicion();

            Owner.FaceTowards(Owner.LastKnownPosition, 240f);

            if (Owner.Suspicion >= 1f)
            {
                if (Owner.CanSeePlayer) Owner.Fsm.ChangeState(Owner.Combate);
                else Owner.TrackTo(Owner.LastKnownPosition);
            }
            else if (Owner.Suspicion <= 0f && Owner.Fsm.TimeInState >= Owner.MinSuspiciousTime)
            {
                Owner.Fsm.ChangeState(Owner.Patrulha);
            }
        }
    }
}
