using System.Collections;
using System.Collections.Generic;
using Apagao.Systems;
using UnityEngine;

namespace Apagao.Combat
{
    /// <summary>
    /// PEM: bloqueador de drones adaptado (spec 11). Descarga em cone de 30° e 10 m que paralisa
    /// tudo que for IStunnable (robôs, antenas). Gasta 1 carga; máximo de 2.
    /// </summary>
    public class Pem : MonoBehaviour
    {
        [SerializeField] private float range = 10f;
        [SerializeField] private float coneAngle = 30f;
        [SerializeField] private float stunSeconds = 3f;
        [SerializeField] private float cooldown = 1f;

        [Header("Cargas")]
        [SerializeField] private int maxCharges = 2;
        [SerializeField] private int startCharges = 2;

        [Header("Ruído")]
        [SerializeField] private float noiseRadius = 12f;
        [SerializeField] private float noiseIntensity = 0.6f;

        [Header("Visual")]
        [SerializeField] private LineRenderer wave;
        [SerializeField] private float waveTime = 0.25f;

        public int Charges { get; private set; }
        public int MaxCharges => maxCharges;
        public float Range => range;
        public bool Ready => Charges > 0 && Time.time >= nextShot;

        private readonly Collider[] buffer = new Collider[32];
        private readonly HashSet<IStunnable> hitThisShot = new HashSet<IStunnable>();
        private float nextShot;

        private void Awake() => Charges = Mathf.Clamp(startCharges, 0, maxCharges);

        public bool AddCharge()
        {
            if (Charges >= maxCharges) return false;
            Charges++;
            return true;
        }

        /// <summary>Dispara de <paramref name="origin"/> na direção <paramref name="direction"/>. Retorna quantos alvos paralisou.</summary>
        public int Fire(Vector3 origin, Vector3 direction)
        {
            if (!Ready) return -1;
            Charges--;
            nextShot = Time.time + cooldown;
            direction.Normalize();

            float cosHalf = Mathf.Cos(coneAngle * 0.5f * Mathf.Deg2Rad);
            int count = Physics.OverlapSphereNonAlloc(origin, range, buffer, ~0, QueryTriggerInteraction.Ignore);
            hitThisShot.Clear();
            int stunned = 0;

            for (int i = 0; i < count; i++)
            {
                Collider c = buffer[i];
                if (c.transform.IsChildOf(transform)) continue;
                var target = c.GetComponentInParent<IStunnable>();
                if (target == null || hitThisShot.Contains(target) || !(target is Component comp)) continue;

                Vector3 point = c.bounds.center;
                Vector3 to = point - origin;
                float dist = to.magnitude;
                if (dist > range || dist < 0.01f) continue;
                if (Vector3.Dot(direction, to / dist) < cosHalf) continue;
                if (!HasLineOfSight(origin, point, comp.transform)) continue;

                hitThisShot.Add(target);
                target.Stun(stunSeconds);
                stunned++;
            }

            NoiseSystem.Emit(origin, noiseRadius, noiseIntensity, NoiseType.Eletrico, gameObject);
            if (wave != null) StartCoroutine(ShowWave(origin, direction));
            return stunned;
        }

        /// <summary>Nada entre o PEM e o alvo (ignora o próprio Caio).</summary>
        private bool HasLineOfSight(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            var hits = Physics.RaycastAll(from, dir / length, length, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                Transform t = hit.collider.transform;
                if (t.IsChildOf(transform) || t.IsChildOf(target)) continue;
                return false;
            }
            return true;
        }

        /// <summary>Leque azul mostrando o cone da descarga.</summary>
        private IEnumerator ShowWave(Vector3 origin, Vector3 direction)
        {
            const int segments = 10;
            wave.positionCount = segments + 3;
            wave.SetPosition(0, origin);
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(-coneAngle * 0.5f, coneAngle * 0.5f, i / (float)segments);
                wave.SetPosition(i + 1, origin + Quaternion.AngleAxis(a, Vector3.up) * direction * range);
            }
            wave.SetPosition(segments + 2, origin);
            wave.enabled = true;

            for (float t = 0f; t < waveTime; t += Time.deltaTime)
            {
                wave.widthMultiplier = Mathf.Lerp(1f, 0.1f, t / waveTime);
                yield return null;
            }
            wave.enabled = false;
        }
    }
}
