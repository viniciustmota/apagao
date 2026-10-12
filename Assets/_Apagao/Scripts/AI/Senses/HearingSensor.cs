using System;
using Apagao.Systems;
using UnityEngine;

namespace Apagao.AI
{
    /// <summary>O que um robô ouviu. A posição tem erro: o som não entrega o ponto exato.</summary>
    public readonly struct HeardNoise
    {
        public readonly Vector3 EstimatedPosition;
        public readonly float Intensity;
        public readonly NoiseType Type;
        public readonly float Distance;

        public HeardNoise(Vector3 estimatedPosition, float intensity, NoiseType type, float distance)
        {
            EstimatedPosition = estimatedPosition;
            Intensity = intensity;
            Type = type;
            Distance = distance;
        }
    }

    /// <summary>
    /// Audição (spec 06): ouve eventos do NoiseSystem dentro do raio.
    /// Cada parede no caminho reduz o raio efetivo.
    /// </summary>
    public class HearingSensor : MonoBehaviour, INoiseListener
    {
        [Tooltip("Multiplica o raio de todo som. Matilha usa 2.")]
        [SerializeField] private float hearingMultiplier = 1f;
        [SerializeField] private float earHeight = 1.6f;

        [Header("Paredes")]
        [Tooltip("Quanto cada parede reduz o raio (0,4 = 40%).")]
        [SerializeField, Range(0f, 1f)] private float wallAttenuation = 0.4f;
        [SerializeField] private int maxWalls = 2;

        [Header("Precisão")]
        [Tooltip("Erro máximo da posição estimada, em fração da distância.")]
        [SerializeField, Range(0f, 0.5f)] private float positionError = 0.15f;

        public event Action<HeardNoise> Heard;

        private readonly RaycastHit[] hits = new RaycastHit[8];

        private void OnEnable() => NoiseSystem.Register(this);
        private void OnDisable() => NoiseSystem.Unregister(this);

        public void OnNoise(in NoiseEvent e)
        {
            if (e.Emitter != null && e.Emitter.transform.IsChildOf(transform)) return;

            Vector3 ear = transform.position + Vector3.up * earHeight;
            float distance = Vector3.Distance(ear, e.Position);
            float radius = e.Radius * hearingMultiplier;
            if (distance > radius) return;

            int walls = CountWalls(e.Position, ear, e.Emitter);
            radius *= Mathf.Pow(1f - wallAttenuation, Mathf.Min(walls, maxWalls));
            if (distance > radius) return;

            Vector2 offset = UnityEngine.Random.insideUnitCircle * (positionError * distance);
            Vector3 estimated = e.Position + new Vector3(offset.x, 0f, offset.y);
            Heard?.Invoke(new HeardNoise(estimated, e.Intensity, e.Type, distance));
        }

        private int CountWalls(Vector3 source, Vector3 ear, GameObject emitter)
        {
            Vector3 from = source + Vector3.up * 0.5f;
            Vector3 dir = ear - from;
            float length = dir.magnitude;
            if (length < 0.01f) return 0;

            int count = Physics.RaycastNonAlloc(from, dir / length, hits, length, ~0, QueryTriggerInteraction.Ignore);
            int walls = 0;
            for (int i = 0; i < count; i++)
            {
                Transform t = hits[i].collider.transform;
                if (t.IsChildOf(transform)) continue;
                if (emitter != null && t.IsChildOf(emitter.transform)) continue;
                walls++;
            }
            return walls;
        }
    }
}
