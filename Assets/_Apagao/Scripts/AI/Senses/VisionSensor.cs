using UnityEngine;

namespace Apagao.AI
{
    /// <summary>
    /// Visão em cone: distância + ângulo (Vector3.Dot) + raycast de oclusão.
    /// Retorna também uma "visibilidade" de 0 a 1: perto e no centro do cone enxerga melhor.
    /// </summary>
    public class VisionSensor : MonoBehaviour
    {
        [SerializeField] private float viewDistance = 16f;
        [SerializeField, Range(10f, 180f)] private float viewAngle = 110f;
        [SerializeField] private float eyeHeight = 1.7f;

        [Header("Visibilidade")]
        [Tooltip("Visibilidade no limite do alcance (1 = colado no robô).")]
        [SerializeField, Range(0f, 1f)] private float farVisibility = 0.25f;
        [Tooltip("Visibilidade na borda do cone (1 = bem no centro).")]
        [SerializeField, Range(0f, 1f)] private float peripheralVisibility = 0.4f;

        /// <summary>Multiplicador de alcance (ex.: alerta elevado).</summary>
        public float RangeMultiplier { get; set; } = 1f;

        public float ViewDistance => viewDistance * RangeMultiplier;
        public float ViewAngle => viewAngle;
        public Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;

        public bool CanSee(Transform target, out float visibility)
        {
            visibility = 0f;
            if (target == null) return false;

            Vector3 targetPoint = GetTargetPoint(target);
            Vector3 toTarget = targetPoint - EyePosition;
            float distance = toTarget.magnitude;
            if (distance > ViewDistance || distance < 0.001f) return false;

            Vector3 dir = toTarget / distance;
            float cosHalf = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
            float dot = Vector3.Dot(transform.forward, dir);
            if (dot < cosHalf) return false;

            // Oclusão: o primeiro objeto no caminho precisa ser o próprio alvo.
            if (Physics.Raycast(EyePosition, dir, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(target))
                return false;

            float distanceFactor = Mathf.Lerp(1f, farVisibility, distance / ViewDistance);
            float angleFactor = Mathf.Lerp(peripheralVisibility, 1f, Mathf.InverseLerp(cosHalf, 1f, dot));
            visibility = distanceFactor * angleFactor;
            return true;
        }

        /// <summary>Mira no peito do alvo. Agachado, o peito fica mais baixo e se esconde atrás de muros baixos.</summary>
        private static Vector3 GetTargetPoint(Transform target)
        {
            var cc = target.GetComponent<CharacterController>();
            return cc != null ? target.TransformPoint(cc.center) : target.position + Vector3.up;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
            Vector3 eye = EyePosition;
            float half = viewAngle * 0.5f;
            const int segments = 20;

            Vector3 prev = eye + Quaternion.Euler(0f, -half, 0f) * transform.forward * ViewDistance;
            Gizmos.DrawLine(eye, prev);
            for (int i = 1; i <= segments; i++)
            {
                float a = Mathf.Lerp(-half, half, i / (float)segments);
                Vector3 next = eye + Quaternion.Euler(0f, a, 0f) * transform.forward * ViewDistance;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
            Gizmos.DrawLine(eye, prev);
        }
    }
}
