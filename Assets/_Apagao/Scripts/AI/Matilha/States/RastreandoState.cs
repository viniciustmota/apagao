using UnityEngine;

namespace Apagao.AI.Matilha
{
    /// <summary>
    /// Vai até o ponto suspeito e segue o rastro do Caio, sempre pelo ponto mais recente (spec 14 R3, R4).
    /// Rastro fresco perto do Caio -> Combate. Sem rastro por um tempo -> Procurando.
    /// </summary>
    public class RastreandoState : State<MatilhaAI>
    {
        private const float FreshScent = 2.5f;
        private const float FreshScentDistance = 5f;
        private const float SniffInterval = 0.4f;

        private float lastScentTime;
        private float noScentTimer;
        private float sniffTimer;

        public RastreandoState(MatilhaAI owner) : base(owner) { }

        public override string Name => "Rastreando";

        public override void Enter()
        {
            lastScentTime = float.NegativeInfinity;
            Owner.Listening = true;
            Retarget();
            if (Owner.Group != null) Owner.Group.Rally(Owner, Owner.LastKnownPosition, combat: false);
        }

        public override void Exit() => Owner.Listening = false;

        /// <summary>Vai para o novo LastKnownPosition (som novo, chamado da rede ou do grupo).</summary>
        public void Retarget()
        {
            noScentTimer = 0f;
            sniffTimer = 0f;
            Owner.MoveTo(Owner.LastKnownPosition, Owner.TrackSpeed);
        }

        public override void Tick()
        {
            if (Owner.CanSeePlayer) { Owner.Fsm.ChangeState(Owner.Combate); return; }

            sniffTimer -= Time.deltaTime;
            if (sniffTimer <= 0f && Owner.Scent != null)
            {
                sniffTimer = SniffInterval;
                if (Owner.Scent.TryGetFreshestNear(Owner.transform.position, Owner.SniffRadius, lastScentTime,
                        out Vector3 point, out float time))
                {
                    lastScentTime = time;
                    noScentTimer = 0f;
                    Owner.MoveTo(point, Owner.TrackSpeed);

                    // Rastro quentinho e o Caio logo ali, mesmo escondido: achou.
                    if (Time.time - time < FreshScent && Owner.DistanceToPlayer < FreshScentDistance)
                    {
                        Owner.LastKnownPosition = Owner.Player.position;
                        Owner.Fsm.ChangeState(Owner.Combate);
                        return;
                    }
                }
            }

            if (!Owner.ReachedDestination) return;

            noScentTimer += Time.deltaTime;
            Owner.transform.Rotate(0f, 120f * Time.deltaTime, 0f); // fareja em volta
            if (noScentTimer >= Owner.TrackGiveUp) Owner.Fsm.ChangeState(Owner.Procurando);
        }
    }
}
