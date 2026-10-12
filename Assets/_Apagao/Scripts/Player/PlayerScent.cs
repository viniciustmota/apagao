using UnityEngine;

namespace Apagao.Player
{
    /// <summary>
    /// Rastro que a Matilha fareja (spec 14 R3): um ponto por segundo, que dura 20 s.
    /// Agachado o Caio não deixa rastro.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerScent : MonoBehaviour
    {
        [SerializeField] private float interval = 1f;
        [SerializeField] private float lifetime = 20f;
        [Tooltip("Distância mínima até o ponto anterior para deixar um novo.")]
        [SerializeField] private float minSpacing = 0.5f;

        private struct ScentPoint
        {
            public Vector3 Position;
            public float Time;
        }

        private ScentPoint[] points;
        private int head;
        private float timer;
        private PlayerController player;

        public float Lifetime => lifetime;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            points = new ScentPoint[Mathf.CeilToInt(lifetime / interval) + 2];
            for (int i = 0; i < points.Length; i++) points[i].Time = float.NegativeInfinity;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < interval) return;
            timer = 0f;

            if (player.Mode == MoveMode.Agachado || !player.IsGrounded) return;

            int last = (head - 1 + points.Length) % points.Length;
            if (IsAlive(points[last]) && (points[last].Position - transform.position).sqrMagnitude < minSpacing * minSpacing)
            {
                points[last].Time = Time.time; // parado em pé: o cheiro "renova" no mesmo lugar
                return;
            }

            points[head] = new ScentPoint { Position = transform.position, Time = Time.time };
            head = (head + 1) % points.Length;
        }

        private bool IsAlive(ScentPoint p) => Time.time - p.Time <= lifetime;

        /// <summary>
        /// O ponto de rastro mais recente perto de <paramref name="from"/>, mais novo que <paramref name="newerThan"/>.
        /// Seguir sempre o mais novo leva o cão na direção do Caio.
        /// </summary>
        public bool TryGetFreshestNear(Vector3 from, float radius, float newerThan, out Vector3 position, out float time)
        {
            position = default;
            time = float.NegativeInfinity;
            float r2 = radius * radius;
            bool found = false;

            foreach (var p in points)
            {
                if (!IsAlive(p) || p.Time <= newerThan || p.Time <= time) continue;
                if ((p.Position - from).sqrMagnitude > r2) continue;
                position = p.Position;
                time = p.Time;
                found = true;
            }
            return found;
        }

        private void OnDrawGizmosSelected()
        {
            if (points == null) return;
            foreach (var p in points)
            {
                if (!IsAlive(p)) continue;
                float age = (Time.time - p.Time) / lifetime;
                Gizmos.color = Color.Lerp(new Color(0.6f, 1f, 0.2f), new Color(0.3f, 0.3f, 0.3f), age);
                Gizmos.DrawSphere(p.Position + Vector3.up * 0.1f, 0.15f);
            }
        }
    }
}
