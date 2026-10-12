using UnityEngine;

namespace Apagao.AI.Sentinela
{
    /// <summary>
    /// Persegue o Caio até a distância de tiro, mira (laser) e atira em rajadas.
    /// Perde de vista por alguns segundos -> Procurando.
    /// </summary>
    public class CombateState : State<SentinelaAI>
    {
        private float lostTimer;

        public CombateState(SentinelaAI owner) : base(owner) { }

        public override string Name => "Combate";

        public override void Enter()
        {
            lostTimer = 0f;
            Owner.Suspicion = 1f;
            Owner.HeightenedAlert = true; // uma vez visto, marcado
        }

        public override void Exit() => Owner.ResetGun();

        public override void Tick()
        {
            if (Owner.CanSeePlayer)
            {
                lostTimer = 0f;
                if (Owner.DistanceToPlayer > Owner.EngageDistance)
                    Owner.MoveTo(Owner.Player.position, Owner.ChaseSpeed);
                else
                    Owner.Stop();

                Owner.FaceTowards(Owner.Player.position, 540f);
                Owner.TickGun(true);
                return;
            }

            Owner.TickGun(false);
            lostTimer += Time.deltaTime;
            Owner.MoveTo(Owner.LastKnownPosition, Owner.ChaseSpeed);
            if (lostTimer >= Owner.LoseSightTime) Owner.Fsm.ChangeState(Owner.Procurando);
        }
    }
}
