using System.Collections;
using Apagao.Combat;
using Apagao.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using Apagao.Audio;

namespace Apagao.Player
{
    /// <summary>
    /// Escalada de bordas (spec 13): de frente para uma borda entre 1,2 m e 2,6 m acima dos pés,
    /// Espaço sobe em vez de pular. No ar, empurrar contra a borda também agarra.
    /// </summary>
    [DefaultExecutionOrder(-50)] // antes do PlayerController, para "roubar" o Espaço do pulo
    [RequireComponent(typeof(PlayerController), typeof(CharacterController))]
    public class LedgeClimber : MonoBehaviour
    {
        [Header("Alturas (acima dos pés)")]
        [SerializeField] private float minHeight = 1.2f;
        [SerializeField] private float maxHeight = 2.6f;

        [Header("Detecção")]
        [Tooltip("Distância máxima até a parede à frente.")]
        [SerializeField] private float reach = 0.8f;
        [SerializeField] private float chestHeight = 1.0f;
        [Tooltip("Quanto além da parede procurar o topo.")]
        [SerializeField] private float topInset = 0.35f;

        [Header("Movimento")]
        [SerializeField] private float climbTime = 0.6f;
        [Tooltip("Fração do tempo subindo; o resto é avançar sobre a borda.")]
        [SerializeField, Range(0.2f, 0.8f)] private float upPortion = 0.6f;

        [Header("Ruído")]
        [SerializeField] private float noiseRadius = 2f;
        [SerializeField] private float noiseIntensity = 0.2f;

        public bool IsClimbing { get; private set; }
        /// <summary>Há uma borda escalável à frente agora (para o HUD).</summary>
        public bool LedgeAvailable { get; private set; }

        private readonly Collider[] overlap = new Collider[8];
        private readonly RaycastHit[] hits = new RaycastHit[8];
        private PlayerController player;
        private CharacterController controller;
        private Health health;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (IsClimbing || player.InputLocked || player.IsDodging || (health != null && health.IsDead) || !controller.enabled)
            {
                LedgeAvailable = false;
                return;
            }

            LedgeAvailable = FindLedge(out Vector3 top);
            if (!LedgeAvailable) return;

            var kb = Keyboard.current;
            bool jumpPressed = kb != null && kb.spaceKey.wasPressedThisFrame;
            bool pushingInAir = !player.IsGrounded && Vector3.Dot(player.WishDirection, Flat(transform.forward)) > 0.5f;

            if (jumpPressed || pushingInAir)
            {
                if (jumpPressed) player.JumpSuppressed = true;
                StartCoroutine(Climb(top));
            }
        }

        private bool FindLedge(out Vector3 top)
        {
            top = default;
            Vector3 forward = Flat(transform.forward);
            if (forward.sqrMagnitude < 0.01f) return false;
            Vector3 feet = transform.position;

            // 1) Uma parede à frente, na altura do peito.
            if (!RaycastIgnoringSelf(feet + Vector3.up * chestHeight, forward, reach, out RaycastHit wall)) return false;

            // 2) O topo dela, olhando de cima para baixo logo depois da face da parede.
            Vector3 probe = new Vector3(wall.point.x, feet.y + maxHeight + 0.3f, wall.point.z) + forward * topInset;
            float probeLength = maxHeight + 0.3f - (minHeight - 0.1f);
            if (!RaycastIgnoringSelf(probe, Vector3.down, probeLength, out RaycastHit surface)) return false;
            if (surface.normal.y < 0.7f) return false; // precisa ser um topo plano

            float height = surface.point.y - feet.y;
            if (height < minHeight || height > maxHeight) return false;

            // 3) Espaço para ficar em pé lá em cima (spec 13 R2).
            float r = controller.radius * 0.95f;
            Vector3 bottom = surface.point + Vector3.up * (r + 0.05f);
            Vector3 head = surface.point + Vector3.up * (controller.height - r);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, head, r, overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!overlap[i].transform.IsChildOf(transform)) return false;

            top = surface.point;
            return true;
        }

        private IEnumerator Climb(Vector3 top)
        {
            IsClimbing = true;
            player.InputLocked = true;
            controller.enabled = false;

            Vector3 forward = Flat(transform.forward);
            Vector3 start = transform.position;
            Vector3 up = new Vector3(start.x, top.y + 0.05f, start.z);
            Vector3 end = top + Vector3.up * 0.05f + forward * 0.15f;
            float upTime = climbTime * upPortion;
            float overTime = climbTime - upTime;

            NoiseSystem.Emit(transform.position, noiseRadius, noiseIntensity, NoiseType.Passo, gameObject);
            Sfx.Play2D("escalada", 0.6f);

            for (float t = 0f; t < upTime; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(start, up, Mathf.SmoothStep(0f, 1f, t / upTime));
                yield return null;
            }
            for (float t = 0f; t < overTime; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(up, end, Mathf.SmoothStep(0f, 1f, t / overTime));
                yield return null;
            }
            transform.position = end;

            controller.enabled = true;
            player.InputLocked = false;
            IsClimbing = false;
        }

        private bool RaycastIgnoringSelf(Vector3 origin, Vector3 dir, float distance, out RaycastHit closest)
        {
            closest = default;
            int count = Physics.RaycastNonAlloc(origin, dir, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform)) continue;
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    closest = hits[i];
                }
            }
            return best < float.MaxValue;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
        }

        private void OnDisable()
        {
            if (!IsClimbing) return;
            if (controller != null) controller.enabled = true;
            if (player != null) player.InputLocked = false;
            IsClimbing = false;
        }
    }
}
