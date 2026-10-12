using UnityEngine;
using UnityEngine.InputSystem;

namespace Apagao.Player
{
    /// <summary>
    /// Câmera em terceira pessoa por cima do ombro, controlada pelo mouse.
    /// Desce junto quando o Caio agacha e se aproxima quando há parede atrás dele.
    /// Esc solta o cursor; clique esquerdo prende de novo.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Posição")]
        [SerializeField] private float distance = 3.5f;
        [SerializeField] private float shoulderOffset = 0.6f;
        [SerializeField] private float pivotStanding = 1.6f;
        [SerializeField] private float pivotCrouched = 1.0f;
        [SerializeField] private float pivotSmooth = 10f;

        [Header("Mira")]
        [SerializeField] private float aimDistance = 1.8f;
        [SerializeField] private float aimShoulderOffset = 0.75f;
        [SerializeField] private float normalFov = 60f;
        [SerializeField] private float aimFov = 50f;
        [SerializeField] private float aimSmooth = 12f;

        [Header("Mouse")]
        [SerializeField] private float sensitivity = 0.12f;
        [SerializeField] private float minPitch = -60f; // olhar para cima o bastante para mirar em drones
        [SerializeField] private float maxPitch = 70f;

        [Header("Colisão")]
        [SerializeField] private float collisionRadius = 0.25f;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private float shakeAmplitude;
        private float shakeDuration;
        private float shakeTimeLeft;
        private PlayerController player;
        private float yaw;
        private float pitch = 15f;
        private float pivotHeight;
        private float aimBlend;
        private Camera cam;

        /// <summary>Ligado pelo WeaponHolder enquanto o botão direito está segurando a mira.</summary>
        public bool Aiming { get; set; }

        private void Start()
        {
            if (target != null)
            {
                player = target.GetComponent<PlayerController>();
                yaw = target.eulerAngles.y;
            }
            pivotHeight = pivotStanding;
            cam = GetComponent<Camera>();
            LockCursor(true);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            var mouse = Mouse.current;
            var kb = Keyboard.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame) LockCursor(false);
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                LockCursor(true);

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * sensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, minPitch, maxPitch);
            }

            float targetPivot = player != null && player.IsCrouching ? pivotCrouched : pivotStanding;
            pivotHeight = Mathf.Lerp(pivotHeight, targetPivot, 1f - Mathf.Exp(-pivotSmooth * Time.deltaTime));

            aimBlend = Mathf.MoveTowards(aimBlend, Aiming ? 1f : 0f, Time.deltaTime * aimSmooth * 0.5f);
            float currentDistance = Mathf.Lerp(distance, aimDistance, aimBlend);
            float currentShoulder = Mathf.Lerp(shoulderOffset, aimShoulderOffset, aimBlend);
            if (cam != null) cam.fieldOfView = Mathf.Lerp(normalFov, aimFov, aimBlend);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Vector3 shoulder = pivot + rot * Vector3.right * currentShoulder;
            Vector3 back = rot * Vector3.back;

            // Aproxima a câmera se houver algo entre o ombro e a posição ideal (ignora o próprio Caio).
            float dist = currentDistance;
            int count = Physics.SphereCastNonAlloc(shoulder, collisionRadius, back, hits, currentDistance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.distance <= 0f || hit.collider.transform.IsChildOf(target)) continue;
                if (hit.distance < dist) dist = hit.distance;
            }

            Vector3 shake = Vector3.zero;
            if (shakeTimeLeft > 0f)
            {
                shakeTimeLeft -= Time.unscaledDeltaTime;
                shake = Random.insideUnitSphere * shakeAmplitude * Mathf.Clamp01(shakeTimeLeft / shakeDuration);
            }

            transform.SetPositionAndRotation(shoulder + back * dist + shake, rot);
        }

        /// <summary>Tremor curto da câmera (impactos, tiros recebidos).</summary>
        public void Shake(float amplitude, float duration)
        {
            if (amplitude < shakeAmplitude * Mathf.Clamp01(shakeTimeLeft / Mathf.Max(shakeDuration, 0.0001f))) return;
            shakeAmplitude = amplitude;
            shakeDuration = duration;
            shakeTimeLeft = duration;
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
