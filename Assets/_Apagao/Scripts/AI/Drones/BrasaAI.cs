using Apagao.Audio;
using Apagao.Combat;
using UnityEngine;

namespace Apagao.AI.Drones
{
    /// <summary>
    /// Brasa, drone incendiário (spec 15 R13–R15): patrulha a 8 m e, quando alertado, despeja fogo
    /// num círculo de 6 m na última posição conhecida. Imune a fogo. O PEM derruba por 6 s.
    /// </summary>
    public class BrasaAI : DroneAI
    {
        private enum State { Patrulha, Indo, Despejando, Recarga, Caida, Subindo }

        [Header("Rota")]
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float altitude = 8f;
        [SerializeField] private float patrolSpeed = 4f;
        [SerializeField] private float attackSpeed = 7f;

        [Header("Sensores")]
        [SerializeField] private float viewRange = 15f;
        [SerializeField] private float viewHalfAngle = 40f;
        [SerializeField] private float lockTime = 0.8f;
        [Tooltip("Avistamentos confirmados da rede até esta distância disparam o ataque.")]
        [SerializeField] private float respondRadius = 40f;

        [Header("Fogo (R13/R14)")]
        [SerializeField] private float telegraphTime = 0.6f;
        [SerializeField] private float fireRadius = 6f;
        [SerializeField] private float fireDuration = 15f;
        [SerializeField] private float fireDamagePerSecond = 10f;
        [SerializeField] private float cooldown = 6f;
        [Tooltip("Visto há menos que isto ao fim da recarga = ataca de novo.")]
        [SerializeField] private float memory = 4f;

        [Header("PEM (R15)")]
        [SerializeField] private float groundedTime = 6f;
        [SerializeField] private float stunImmunity = 5f;

        private State state;
        private float stateTime;
        private int waypointIndex;
        private float lockProgress;
        private float lastSeen = float.NegativeInfinity;
        private float nextReport;
        private float fallSpeed;
        private float immuneUntil = float.NegativeInfinity;

        public override bool IsAware => state != State.Patrulha;
        protected override float RotorPitch => 0.65f;
        protected override float RotorVolume => 0.55f;
        protected override bool RotorActive => state != State.Caida;
        private LoopSound flame;

        protected override void HandleKilled()
        {
            if (flame != null) flame.Stop();
            Crash();
        }

        protected override void Awake()
        {
            base.Awake();
            flame = LoopSound.Attach(gameObject, "brasa_chama_loop", 0.35f, 30f, SoundCategory.Inimigos);
        }
        public override bool IsStunned => state == State.Caida;
        public override string StateName => state == State.Patrulha && lockProgress > 0f ? $"Patrulha {lockProgress / lockTime:P0}" : state.ToString();

        private void ChangeState(State next)
        {
            state = next;
            stateTime = 0f;
        }

        protected override void Think()
        {
            stateTime += Time.deltaTime;
            bool sees = state != State.Caida && CanSeeFromAbove(viewRange, viewHalfAngle);
            if (sees)
            {
                LastKnownPosition = Player.position;
                lastSeen = Time.time;
                if (state != State.Patrulha && Time.time >= nextReport)
                {
                    nextReport = Time.time + 1f;
                    ReportSighting(LastKnownPosition, confirmed: true);
                }
            }

            switch (state)
            {
                case State.Patrulha: Patrol(sees); break;

                case State.Indo:
                    Vector3 above = DroneMotor.AtAltitude(LastKnownPosition, altitude);
                    Motor.FlyTo(above, attackSpeed);
                    if (Motor.Arrived(above, 1.5f)) ChangeState(State.Despejando);
                    break;

                case State.Despejando:
                    Motor.Hover();
                    if (stateTime >= telegraphTime)
                    {
                        Drop();
                        ChangeState(State.Recarga);
                    }
                    break;

                case State.Recarga:
                    // Circula por perto enquanto recarrega, para não ficar parado em cima do fogo.
                    float angle = stateTime * 0.8f;
                    Vector3 around = LastKnownPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 8f;
                    Motor.FlyTo(DroneMotor.AtAltitude(around, altitude), patrolSpeed);
                    if (stateTime >= cooldown)
                        ChangeState(Time.time - lastSeen <= memory ? State.Indo : State.Patrulha);
                    break;

                case State.Caida: Fallen(); break;

                case State.Subindo:
                    Vector3 up = DroneMotor.AtAltitude(transform.position, altitude);
                    Motor.FlyTo(up, patrolSpeed);
                    if (Mathf.Abs(transform.position.y - up.y) < 0.5f)
                        ChangeState(Time.time - lastSeen <= memory ? State.Indo : State.Patrulha);
                    break;
            }
            UpdateFeedback();
        }

        private void Patrol(bool sees)
        {
            lockProgress = Mathf.Clamp(lockProgress + (sees ? Time.deltaTime : -Time.deltaTime), 0f, lockTime);
            if (lockProgress >= lockTime)
            {
                lockProgress = 0f;
                ReportSighting(LastKnownPosition, confirmed: true);
                ChangeState(State.Indo);
                return;
            }
            if (waypoints == null || waypoints.Length == 0)
            {
                Motor.Hover();
                return;
            }
            Vector3 target = DroneMotor.AtAltitude(waypoints[waypointIndex].position, altitude);
            Motor.FlyTo(target, sees ? patrolSpeed * 0.4f : patrolSpeed);
            if (Motor.Arrived(target, 1.5f)) waypointIndex = (waypointIndex + 1) % waypoints.Length;
        }

        private void Drop()
        {
            Vector3 ground = transform.position;
            ground.y = DroneMotor.GroundHeight(ground);
            var hits = Physics.RaycastAll(transform.position, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform) || h.distance >= best) continue;
                best = h.distance;
                ground = h.point; // cai no telhado, se houver um
            }
            FireDrop.Spawn(transform.position + Vector3.down * 0.5f, ground, fireRadius, fireDuration, fireDamagePerSecond);
            Sfx.Play("brasa_despejo", transform.position, 1f, SoundCategory.Inimigos, 45f);
        }

        // ---------- PEM: cai e fica 6 s no chão ----------

        public override void Stun(float seconds)
        {
            if (IsDead || state == State.Caida || Time.time < immuneUntil) return;
            if (flame != null) flame.Volume = 0f; // chama apaga enquanto está no chão
            fallSpeed = 0f;
            if (hive != null) hive.GoOffline(groundedTime + 1f);
            ChangeState(State.Caida);
        }

        private void Fallen()
        {
            float floor = FloorBelow();
            if (transform.position.y > floor + 0.01f)
            {
                fallSpeed += 20f * Time.deltaTime;
                Vector3 p = transform.position;
                p.y = Mathf.Max(floor, p.y - fallSpeed * Time.deltaTime);
                transform.position = p;
            }
            if (stateTime < groundedTime) return;
            immuneUntil = Time.time + stunImmunity;
            if (flame != null) flame.Volume = 1f;
            ChangeState(State.Subindo);
        }

        /// <summary>Altura em que o drone fica apoiado (chão ou telhado abaixo dele).</summary>
        private float FloorBelow()
        {
            float floor = DroneMotor.GroundHeight(transform.position);
            foreach (var h in Physics.RaycastAll(transform.position + Vector3.up * 0.5f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.collider.transform.IsChildOf(transform)) floor = Mathf.Max(floor, h.point.y);
            return floor + 0.5f; // metade da altura do corpo
        }

        // ---------- Rede e dano ----------

        protected override void HandleHiveSighting(Vector3 position, bool confirmed, bool converge)
        {
            if (!confirmed || state == State.Caida || state == State.Subindo) return;
            if (Vector3.Distance(transform.position, position) > respondRadius) return;
            LastKnownPosition = position;
            lastSeen = Time.time;
            if (state == State.Patrulha) ChangeState(State.Indo);
        }

        protected override void HandleDamaged(DamageInfo info)
        {
            lastSeen = Time.time;
            if (state == State.Patrulha) ChangeState(State.Indo);
        }

        private void UpdateFeedback()
        {
            switch (state)
            {
                case State.Caida:
                    SetStatusColor(new Color(0.3f, 0.8f, 1f), Mathf.PingPong(Time.time * 8f, 2f)); // paralisado
                    break;
                case State.Despejando:
                    SetStatusColor(new Color(1f, 0.4f, 0f), Mathf.PingPong(Time.time * 40f, 8f));
                    break;
                case State.Patrulha:
                    SetStatusColor(Color.Lerp(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.3f, 0f), lockProgress / lockTime), 1.5f);
                    break;
                default:
                    SetStatusColor(new Color(1f, 0.3f, 0f), 4f);
                    break;
            }
        }
    }

    /// <summary>Carga incendiária caindo da Brasa: acende o fogo ao chegar no ponto de impacto.</summary>
    public class FireDrop : MonoBehaviour
    {
        private Vector3 target;
        private float radius, duration, dps;
        private float speed;

        public static void Spawn(Vector3 from, Vector3 target, float radius, float duration, float dps)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Carga_Incendiaria";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.35f;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(1f, 0.45f, 0.05f));
            go.GetComponent<Renderer>().SetPropertyBlock(block);

            var drop = go.AddComponent<FireDrop>();
            drop.target = target;
            drop.radius = radius;
            drop.duration = duration;
            drop.dps = dps;
        }

        private void Update()
        {
            speed += 20f * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude > 0.0001f) return;
            FireArea.Spawn(target, radius, duration, dps);
            Destroy(gameObject);
        }
    }
}
