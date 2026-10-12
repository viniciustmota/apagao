using UnityEngine;

namespace Apagao.AI.Matilha
{
    /// <summary>
    /// Combate em grupo (spec 14 R6, R7): o líder segura e avança devagar, os outros flanqueiam pelos lados.
    /// Perto o bastante: agacha 0,4 s (aviso) e dá o bote. Perde de vista -> volta a rastrear.
    /// </summary>
    public class CombateCaoState : State<MatilhaAI>
    {
        private enum Phase { Approach, Telegraph, Pounce }

        private Phase phase;
        private float phaseTimer;
        private float cooldown;
        private float lostTimer;
        private Vector3 pounceDir;
        private bool bitten;

        public CombateCaoState(MatilhaAI owner) : base(owner) { }

        public override string Name => "Combate";

        public override void Enter()
        {
            phase = Phase.Approach;
            lostTimer = 0f;
            Owner.Suspicion = 1f;
            Owner.HeightenedAlert = true;
            if (Owner.Group != null) Owner.Group.Rally(Owner, Owner.LastKnownPosition, combat: true);
        }

        public override void Exit() => Owner.Squat(false);

        public override void Tick()
        {
            cooldown -= Time.deltaTime;

            if (phase == Phase.Telegraph) { TickTelegraph(); return; }
            if (phase == Phase.Pounce) { TickPounce(); return; }

            if (!Owner.CanSeePlayer)
            {
                lostTimer += Time.deltaTime;
                Owner.MoveTo(Owner.LastKnownPosition, Owner.ChaseSpeed);
                if (lostTimer >= Owner.LoseSightTime) Owner.TrackTo(Owner.LastKnownPosition);
                return;
            }
            lostTimer = 0f;

            float dist = Owner.DistanceToPlayer;
            if (dist <= Owner.AttackRange && cooldown <= 0f)
            {
                phase = Phase.Telegraph;
                phaseTimer = 0f;
                Owner.Stop();
                Owner.Squat(true);
                return;
            }

            Vector3 player = Owner.Player.position;
            float side = 1f;
            PackRole role = PackRole.Segurar;
            if (Owner.Group != null) role = Owner.Group.RoleOf(Owner, out side);

            if (role == PackRole.Segurar)
            {
                Owner.MoveTo(player, dist > Owner.HoldDistance ? Owner.ChaseSpeed : Owner.HoldCreepSpeed);
                return;
            }

            // Flanco: um ponto ao lado do Caio, girado a partir da direção do cão que segura.
            var holder = Owner.Group.Leader;
            Vector3 from = holder != null && holder != Owner ? holder.transform.position : Owner.transform.position;
            Vector3 baseDir = from - player;
            baseDir.y = 0f;
            if (baseDir.sqrMagnitude < 0.01f) baseDir = -Owner.Player.forward;
            Vector3 flankPoint = player + Quaternion.Euler(0f, side * Owner.FlankAngle, 0f) * baseDir.normalized * Owner.FlankDistance;

            bool inPosition = Vector3.Distance(Owner.transform.position, flankPoint) < 1.5f || dist < Owner.FlankDistance * 0.75f;
            Owner.MoveTo(inPosition ? player : flankPoint, Owner.ChaseSpeed);
        }

        private void TickTelegraph()
        {
            phaseTimer += Time.deltaTime;
            if (Owner.Player != null) Owner.FaceTowards(Owner.Player.position, 720f);
            if (phaseTimer < Owner.TelegraphTime) return;

            phase = Phase.Pounce;
            phaseTimer = 0f;
            bitten = false;
            Owner.Squat(false);
            pounceDir = Owner.Player != null ? Owner.Player.position - Owner.transform.position : Owner.transform.forward;
            pounceDir.y = 0f;
            pounceDir.Normalize();
        }

        private void TickPounce()
        {
            phaseTimer += Time.deltaTime;
            if (Owner.Agent.isOnNavMesh) Owner.Agent.Move(pounceDir * Owner.PounceSpeed * Time.deltaTime);
            if (!bitten) bitten = Owner.TryBite();

            if (phaseTimer < Owner.PounceTime) return;
            phase = Phase.Approach;
            cooldown = Owner.AttackCooldown;
        }
    }
}
