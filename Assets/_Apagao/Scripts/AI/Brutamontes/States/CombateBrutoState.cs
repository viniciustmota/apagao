using UnityEngine;

namespace Apagao.AI.Brutamontes
{
    /// <summary>
    /// Combate do Brutamontes (spec 16 R4). Anda devagar até o Caio e escolhe:
    /// perto = soco (0,8 s de aviso com o braço erguido); de 4,5 a 12 m = investida (1,2 s de aviso, linha reta de 8 m).
    /// Investida que bate em parede -> Atordoado. Perde o Caio de vista -> Procurando.
    /// </summary>
    public class CombateBrutoState : State<BrutamontesAI>
    {
        public enum CombatPhase { Perseguindo, Soco, PreparandoInvestida, Investida }

        public CombatPhase Phase { get; private set; }

        private float phaseTime;
        private float lostTimer;
        private bool punched;
        private bool chargeHit;
        private float charged;
        private float stepProgress;
        private Vector3 chargeDirection;

        public CombateBrutoState(BrutamontesAI owner) : base(owner) { }

        public override string Name => "Combate";

        public override void Enter()
        {
            Owner.Suspicion = 1f;
            Owner.HeightenedAlert = true;
            lostTimer = 0f;
            SetPhase(CombatPhase.Perseguindo);
        }

        public override void Exit() => Owner.SetArmRaise(0f);

        private void SetPhase(CombatPhase next)
        {
            Phase = next;
            phaseTime = 0f;
        }

        public override void Tick()
        {
            phaseTime += Time.deltaTime;
            switch (Phase)
            {
                case CombatPhase.Perseguindo: Chase(); break;
                case CombatPhase.Soco: Punch(); break;
                case CombatPhase.PreparandoInvestida: PrepareCharge(); break;
                case CombatPhase.Investida: Charge(); break;
            }
        }

        private void Chase()
        {
            if (!Owner.CanSeePlayer)
            {
                lostTimer += Time.deltaTime;
                Owner.MoveTo(Owner.LastKnownPosition, Owner.MoveSpeed);
                if (lostTimer >= Owner.LoseSightTime) Owner.Fsm.ChangeState(Owner.Procurando);
                return;
            }
            lostTimer = 0f;

            if (Owner.PunchReady)
            {
                Owner.HaltPath();
                punched = false;
                SetPhase(CombatPhase.Soco);
                return;
            }
            if (Owner.ChargeReady)
            {
                Owner.HaltPath();
                Owner.StartChargeCooldown();
                SetPhase(CombatPhase.PreparandoInvestida);
                return;
            }

            Owner.MoveTo(Owner.Player.position, Owner.MoveSpeed);
            Owner.FaceTowards(Owner.Player.position, Owner.TurnSpeed);
        }

        private void Punch()
        {
            if (!punched)
            {
                // Aviso: o braço sobe e ele ainda vira devagar (dá para sair pelo lado).
                Owner.SetArmRaise(Mathf.Clamp01(phaseTime / Owner.PunchWindup));
                Owner.FaceTowards(Owner.Player.position, Owner.TurnSpeed);
                if (phaseTime < Owner.PunchWindup) return;
                punched = true;
                phaseTime = 0f;
                Owner.SetArmRaise(0f);
                Owner.Punch();
                return;
            }
            if (phaseTime >= Owner.PunchRecovery) SetPhase(CombatPhase.Perseguindo);
        }

        private void PrepareCharge()
        {
            // Abaixa e mira; no fim do aviso a direção trava.
            Owner.SetArmRaise(0.4f);
            if (phaseTime < Owner.ChargeWindup - Owner.ChargeLockBeforeEnd)
            {
                Owner.FaceTowards(Owner.LastKnownPosition, Owner.TurnSpeed * 1.5f);
                chargeDirection = Owner.transform.forward;
            }
            if (phaseTime < Owner.ChargeWindup) return;

            charged = 0f;
            chargeHit = false;
            stepProgress = 0f;
            Owner.SetArmRaise(0f);
            SetPhase(CombatPhase.Investida);
        }

        private void Charge()
        {
            if (Owner.WallAhead(0.9f))
            {
                Owner.Fsm.ChangeState(Owner.Atordoado);
                return;
            }

            float step = Owner.ChargeSpeed * Time.deltaTime;
            Vector3 moved = Owner.Shove(chargeDirection * step);
            moved.y = 0f;
            charged += step;

            stepProgress += moved.magnitude;
            if (stepProgress >= 1.5f)
            {
                stepProgress = 0f;
                Owner.Tremor(0.18f, 0.12f);
            }

            if (!chargeHit) chargeHit = Owner.TryChargeHit();

            // Travou na borda do NavMesh: se há algo sólido logo à frente, é parede; senão só para.
            bool stuck = moved.magnitude < step * 0.3f;
            if (stuck && Owner.WallAhead(2f))
            {
                Owner.Fsm.ChangeState(Owner.Atordoado);
                return;
            }
            if (stuck || charged >= Owner.ChargeDistance) SetPhase(CombatPhase.Perseguindo);
        }
    }
}
