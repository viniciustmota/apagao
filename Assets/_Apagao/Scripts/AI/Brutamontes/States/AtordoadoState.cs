using UnityEngine;

namespace Apagao.AI.Brutamontes
{
    /// <summary>
    /// Bateu na parede durante a investida (spec 16 R4): 2 s parado, cambaleando. Janela para molotov e PEM.
    /// Não é a paralisia do PEM: não libera o takedown sozinho.
    /// </summary>
    public class AtordoadoState : State<BrutamontesAI>
    {
        private Quaternion baseRotation;

        public AtordoadoState(BrutamontesAI owner) : base(owner) { }

        public override string Name => "Atordoado";

        public override void Enter()
        {
            Owner.HaltPath();
            Owner.SetArmRaise(0f);
            Owner.SlamWall();
            baseRotation = Owner.transform.rotation;
        }

        public override void Tick()
        {
            // Balança a cabeça de lado a lado.
            float wobble = Mathf.Sin(Owner.Fsm.TimeInState * 9f) * 6f * (1f - Owner.Fsm.TimeInState / Owner.WallStunTime);
            Owner.transform.rotation = baseRotation * Quaternion.Euler(0f, wobble, 0f);
            if (Owner.Fsm.TimeInState >= Owner.WallStunTime) Owner.Fsm.ChangeState(Owner.Combate);
        }

        public override void Exit() => Owner.transform.rotation = baseRotation;
    }
}
