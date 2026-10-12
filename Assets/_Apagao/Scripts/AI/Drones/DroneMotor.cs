using UnityEngine;

namespace Apagao.AI.Drones
{
    /// <summary>
    /// Voo dos drones (spec 15 R1): sem NavMesh. Vai até um ponto com aceleração limitada, desacelera ao chegar
    /// e sobe quando há obstáculo à frente. Também inclina o corpo e gira as hélices.
    /// </summary>
    public class DroneMotor : MonoBehaviour
    {
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float turnSpeed = 270f;
        [Tooltip("Distância em que começa a frear antes do destino.")]
        [SerializeField] private float slowRadius = 3f;

        [Header("Desvio")]
        [SerializeField] private float avoidDistance = 4f;
        [SerializeField] private float avoidRadius = 0.6f;
        [Tooltip("Nunca desce abaixo desta altura sobre o terreno.")]
        [SerializeField] private float minClearance = 1.5f;

        [Header("Visual")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform[] rotors;
        [SerializeField] private float rotorSpeed = 1800f;
        [SerializeField] private float maxTilt = 20f;

        public Vector3 Velocity { get; private set; }
        /// <summary>Ignorar este objeto no desvio (o alvo da Vespa, por exemplo).</summary>
        public Transform IgnoreForAvoidance { get; set; }

        private readonly RaycastHit[] hits = new RaycastHit[8];

        /// <summary>Altura do terreno no ponto (0 se não houver terreno).</summary>
        public static float GroundHeight(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            return t != null ? t.SampleHeight(p) + t.transform.position.y : 0f;
        }

        /// <summary>O ponto (x, z) a <paramref name="altitude"/> metros acima do terreno.</summary>
        public static Vector3 AtAltitude(Vector3 p, float altitude) =>
            new Vector3(p.x, GroundHeight(p) + altitude, p.z);

        /// <summary>Chamar todo frame: voa até o ponto, freando ao chegar.</summary>
        public void FlyTo(Vector3 point, float speed)
        {
            Vector3 to = point - transform.position;
            float dist = to.magnitude;
            Vector3 desired = dist > 0.05f ? to / dist * speed * Mathf.Clamp01(dist / slowRadius) : Vector3.zero;
            Steer(desired, avoid: true);
        }

        /// <summary>Parado no ar.</summary>
        public void Hover() => Steer(Vector3.zero, avoid: false);

        /// <summary>Velocidade fixa numa direção, sem desvio (mergulho da Vespa).</summary>
        public void MoveStraight(Vector3 direction, float speed)
        {
            Velocity = direction.normalized * speed;
            transform.position += Velocity * Time.deltaTime;
            Animate();
        }

        public bool Arrived(Vector3 point, float tolerance = 1f)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude <= tolerance;
        }

        /// <summary>Gira só no eixo Y para olhar o ponto (o padrão é olhar para onde voa).</summary>
        public void FaceTowards(Vector3 point)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
        }

        private void Steer(Vector3 desired, bool avoid)
        {
            if (avoid && desired.sqrMagnitude > 0.01f && Blocked(desired.normalized))
                desired = new Vector3(desired.x * 0.3f, Mathf.Max(desired.magnitude, 3f), desired.z * 0.3f);

            Velocity = Vector3.MoveTowards(Velocity, desired, acceleration * Time.deltaTime);
            Vector3 next = transform.position + Velocity * Time.deltaTime;

            float floor = GroundHeight(next) + minClearance;
            if (next.y < floor)
            {
                next.y = floor;
                if (Velocity.y < 0f) Velocity = new Vector3(Velocity.x, 0f, Velocity.z);
            }
            transform.position = next;

            Vector3 flat = new Vector3(Velocity.x, 0f, Velocity.z);
            if (flat.sqrMagnitude > 0.25f) FaceTowards(transform.position + flat);
            Animate();
        }

        private bool Blocked(Vector3 direction)
        {
            int count = Physics.SphereCastNonAlloc(transform.position, avoidRadius, direction, hits, avoidDistance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = hits[i].collider.transform;
                if (hit.IsChildOf(transform)) continue;
                if (IgnoreForAvoidance != null && hit.IsChildOf(IgnoreForAvoidance)) continue;
                if (hits[i].collider is TerrainCollider) continue; // o terreno é tratado pela altura mínima
                return true;
            }
            return false;
        }

        private void Animate()
        {
            if (rotors != null)
                foreach (var r in rotors)
                    if (r != null) r.Rotate(0f, rotorSpeed * Time.deltaTime, 0f, Space.Self);

            if (body == null) return;
            // Inclina na direção do movimento, como um quadricóptero.
            Vector3 local = transform.InverseTransformDirection(Velocity);
            float pitch = Mathf.Clamp(local.z * 2f, -maxTilt, maxTilt);
            float roll = Mathf.Clamp(-local.x * 2f, -maxTilt, maxTilt);
            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(pitch, 0f, roll), 1f - Mathf.Exp(-6f * Time.deltaTime));
        }
    }
}
