using System.Collections.Generic;
using Apagao.Combat;
using Apagao.Player;
using Apagao.Systems;
using UnityEngine;

namespace Apagao.AI.Drones
{
    /// <summary>
    /// Vespa, drone kamikaze (spec 15 R9–R12): pousada no ninho até a rede marcar o Caio por perto.
    /// Aproxima, trava por 1 s com aviso crescente e mergulha em linha reta no ponto travado.
    /// Tiro no ar = explode onde está. PEM = cai sem explodir.
    /// </summary>
    public class VespaAI : DroneAI
    {
        private enum State { Pousada, Decolando, Aproximando, Travando, Mergulhando, Voltando }

        [Header("Ninho")]
        [Tooltip("A rede lança a Vespa se o Caio for marcado a até esta distância do ninho.")]
        [SerializeField] private float launchRange = 40f;
        [SerializeField] private float takeoffTime = 0.6f;

        [Header("Ataque")]
        [SerializeField] private float approachSpeed = 10f;
        [SerializeField] private float approachHeight = 4f;
        [SerializeField] private float lockDistance = 8f;
        [SerializeField] private float lockTime = 1f;
        [SerializeField] private float diveSpeed = 18f;
        [SerializeField] private float diveMaxTime = 1.5f;
        [Tooltip("Sem ver o Caio por este tempo, volta para o ninho.")]
        [SerializeField] private float giveUpTime = 8f;
        [SerializeField] private float sightRange = 30f;

        [Header("Explosão (R11)")]
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private float explosionDamage = 60f;
        [SerializeField] private float explosionNoise = 25f;
        [SerializeField] private float contactRadius = 0.45f;

        private State state;
        private float stateTime;
        private Vector3 nest;
        private Quaternion nestRotation;
        private Vector3 lockedPoint;
        private Vector3 diveDirection;
        private float lastSeen = float.NegativeInfinity;
        private readonly Collider[] buffer = new Collider[32];
        private readonly RaycastHit[] sweepHits = new RaycastHit[8];

        public override bool IsAware => state != State.Pousada;
        public override string StateName => state.ToString();

        protected override void Awake()
        {
            base.Awake();
            nest = transform.position;
            nestRotation = transform.rotation;
            if (Player != null) Motor.IgnoreForAvoidance = Player;
        }

        private void ChangeState(State next)
        {
            state = next;
            stateTime = 0f;
        }

        protected override void Think()
        {
            stateTime += Time.deltaTime;
            if (state != State.Pousada && CanSeeAround(sightRange))
            {
                LastKnownPosition = Player.position;
                lastSeen = Time.time;
            }

            switch (state)
            {
                case State.Pousada:
                    // Também decola se o Caio já estiver marcado e chegar perto do ninho.
                    if (PlayerMarked && Player != null && Vector3.Distance(nest, Player.position) <= launchRange * 0.5f && CanSeeAround(sightRange))
                        Launch(Player.position);
                    break;

                case State.Decolando:
                    Motor.FlyTo(nest + Vector3.up * 2.5f, approachSpeed);
                    if (stateTime >= takeoffTime) ChangeState(State.Aproximando);
                    break;

                case State.Aproximando: Approach(); break;
                case State.Travando: Lock(); break;
                case State.Mergulhando: Dive(); break;

                case State.Voltando:
                    Motor.FlyTo(nest, approachSpeed * 0.6f);
                    if (Vector3.Distance(transform.position, nest) < 0.3f)
                    {
                        transform.SetPositionAndRotation(nest, nestRotation);
                        ChangeState(State.Pousada);
                    }
                    break;
            }
            UpdateFeedback();
        }

        private void Launch(Vector3 target)
        {
            LastKnownPosition = target;
            lastSeen = Time.time;
            ChangeState(State.Decolando);
        }

        private void Approach()
        {
            if (Time.time - lastSeen > giveUpTime)
            {
                ChangeState(State.Voltando);
                return;
            }
            Vector3 above = LastKnownPosition + Vector3.up * approachHeight;
            Motor.FlyTo(above, approachSpeed);

            bool seesNow = Time.time - lastSeen < 0.1f;
            if (seesNow && Vector3.Distance(transform.position, PlayerChest) <= lockDistance)
                ChangeState(State.Travando);
        }

        private void Lock()
        {
            // Fica no ar acompanhando o alvo; o ponto só congela no fim (dá tempo de esquivar).
            Motor.Hover();
            Motor.FaceTowards(LastKnownPosition);
            if (stateTime < lockTime) return;

            lockedPoint = LastKnownPosition + Vector3.up * 0.9f;
            diveDirection = (lockedPoint - transform.position).normalized;
            ChangeState(State.Mergulhando);
        }

        private void Dive()
        {
            Vector3 before = transform.position;
            Motor.MoveStraight(diveDirection, diveSpeed);

            bool passed = Vector3.Dot(lockedPoint - transform.position, diveDirection) <= 0f;
            if (passed || stateTime >= diveMaxTime || Touching() || SweptIntoSomething(before))
                Explode();
        }

        /// <summary>Em alta velocidade, confere o trecho percorrido neste frame para não atravessar nada.</summary>
        private bool SweptIntoSomething(Vector3 from)
        {
            Vector3 delta = transform.position - from;
            float dist = delta.magnitude;
            if (dist < 0.001f) return false;
            int count = Physics.RaycastNonAlloc(from, delta / dist, sweepHits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!sweepHits[i].collider.transform.IsChildOf(transform)) return true;
            return false;
        }

        private bool Touching()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, contactRadius, buffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!buffer[i].transform.IsChildOf(transform)) return true;
            return false;
        }

        // ---------- Explosão ----------

        private void Explode()
        {
            if (IsDead) return;
            Vector3 center = transform.position;
            ExplosionFlash.Spawn(center, explosionRadius);
            NoiseSystem.Emit(center, explosionNoise, 1f, NoiseType.Explosao, gameObject);

            var cam = FindAnyObjectByType<ThirdPersonCamera>();
            if (cam != null && Player != null)
            {
                float d = Vector3.Distance(center, Player.position);
                if (d < explosionRadius * 4f) cam.Shake(0.35f * (1f - d / (explosionRadius * 4f)), 0.4f);
            }

            // Sai antes de causar dano: outras Vespas no raio explodem em cadeia sem voltar aqui.
            Vanish(reportAsMissing: false);

            var hitTargets = new HashSet<IDamageable>();
            int count = Physics.OverlapSphereNonAlloc(center, explosionRadius, buffer, ~0, QueryTriggerInteraction.Ignore);
            var colliders = new Collider[count];
            System.Array.Copy(buffer, colliders, count);
            foreach (var c in colliders)
            {
                if (c == null || c.transform.IsChildOf(transform)) continue;
                var target = c.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead || !hitTargets.Add(target)) continue;

                Vector3 closest = c.ClosestPoint(center);
                float dist = Vector3.Distance(center, closest);
                float amount = explosionDamage * Mathf.Clamp01(1f - dist / explosionRadius);
                if (amount <= 0f) continue;
                Vector3 dir = closest - center;
                target.TakeDamage(new DamageInfo(amount, DamageType.Explosao, closest, dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.down, gameObject));
            }
        }

        // ---------- Rede, dano e PEM ----------

        protected override void HandleHiveSighting(Vector3 position, bool confirmed, bool converge)
        {
            if (state == State.Pousada)
            {
                if (confirmed && Vector3.Distance(nest, position) <= launchRange) Launch(position);
                return;
            }
            if (state == State.Aproximando || state == State.Voltando)
            {
                LastKnownPosition = position;
                lastSeen = Time.time; // a rede vê por ela
                if (state == State.Voltando) ChangeState(State.Aproximando);
            }
        }

        /// <summary>R12: vida 10. Tiro ou golpe = explode onde está.</summary>
        protected override void HandleKilled() => Explode();

        /// <summary>PEM derruba sem explodir (fica a carcaça e o saque).</summary>
        public override void Stun(float seconds) => Crash();

        private void UpdateFeedback()
        {
            switch (state)
            {
                case State.Pousada:
                    SetStatusColor(new Color(1f, 0.6f, 0.1f), 0.5f);
                    break;
                case State.Travando:
                    // Pisca cada vez mais rápido: o aviso para esquivar.
                    float freq = Mathf.Lerp(4f, 20f, stateTime / lockTime);
                    bool on = Mathf.Sin(stateTime * freq * Mathf.PI * 2f) > 0f;
                    SetStatusColor(on ? new Color(1f, 0.1f, 0.05f) : new Color(0.2f, 0f, 0f), on ? 6f : 0.5f);
                    break;
                case State.Mergulhando:
                    SetStatusColor(new Color(1f, 0.1f, 0.05f), 8f);
                    break;
                default:
                    SetStatusColor(new Color(1f, 0.25f, 0.05f), 2f);
                    break;
            }
        }
    }

    /// <summary>Clarão rápido da explosão: esfera que cresce e some, com luz.</summary>
    public class ExplosionFlash : MonoBehaviour
    {
        private const float Life = 0.35f;
        private float radius;
        private float age;
        private Light glow;
        private Renderer sphere;
        private MaterialPropertyBlock block;

        public static void Spawn(Vector3 point, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Explosao";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = point;
            var flash = go.AddComponent<ExplosionFlash>();
            flash.radius = radius;
        }

        private void Start()
        {
            sphere = GetComponent<Renderer>();
            sphere.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            block = new MaterialPropertyBlock();
            glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.6f, 0.2f);
            glow.range = radius * 5f;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float k = age / Life;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.3f, 1f, Mathf.Sqrt(k));
            block.SetColor("_BaseColor", Color.Lerp(new Color(1f, 0.9f, 0.5f), new Color(0.3f, 0.1f, 0.05f), k));
            sphere.SetPropertyBlock(block);
            glow.intensity = Mathf.Lerp(12f, 0f, k);
        }
    }
}
