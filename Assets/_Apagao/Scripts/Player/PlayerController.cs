using UnityEngine;
using UnityEngine.InputSystem;

namespace Apagao.Player
{
    /// <summary>Modo de movimento atual. No marco 4 cada modo vira um nível de ruído no NoiseSystem.</summary>
    public enum MoveMode { Parado, Andando, Correndo, Agachado }

    /// <summary>
    /// Movimento do Caio: andar (WASD), correr (Shift), agachar (C ou Ctrl) e pular (Espaço).
    /// A direção do movimento é relativa à câmera.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Velocidades (m/s)")]
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float runSpeed = 6f;
        [SerializeField] private float crouchSpeed = 1.5f;
        [SerializeField] private float acceleration = 20f;
        [SerializeField] private float turnSpeed = 720f;

        [Header("Pulo e gravidade")]
        [SerializeField] private float jumpHeight = 1.25f;
        [SerializeField] private float gravity = -20f;

        [Header("Agachar")]
        [SerializeField] private float standHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.1f;

        [Header("Referências")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Modelo visual, achatado ao agachar. Opcional.")]
        [SerializeField] private Transform visual;

        public MoveMode Mode { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsGrounded => controller.isGrounded;
        public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
            ApplyHeight(standHeight);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            Vector2 input = new Vector2(
                (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
            input = Vector2.ClampMagnitude(input, 1f);
            bool moving = input.sqrMagnitude > 0.01f;

            if (kb.cKey.wasPressedThisFrame || kb.leftCtrlKey.wasPressedThisFrame)
                ToggleCrouch();

            // Correr levanta o Caio, se houver espaço acima dele.
            bool running = moving && kb.leftShiftKey.isPressed;
            if (running && IsCrouching && !TryStand())
                running = false;

            float targetSpeed = IsCrouching ? crouchSpeed : running ? runSpeed : walkSpeed;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }

            Vector3 desired = (forward * input.y + right * input.x) * targetSpeed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desired, acceleration * Time.deltaTime);

            if (moving && desired.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(desired, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            }

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // mantém o Caio colado no chão em descidas

            if (kb.spaceKey.wasPressedThisFrame && controller.isGrounded)
            {
                if (IsCrouching) TryStand();
                else verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;
            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

            Mode = IsCrouching ? MoveMode.Agachado
                 : !moving ? MoveMode.Parado
                 : running ? MoveMode.Correndo
                 : MoveMode.Andando;
        }

        private void ToggleCrouch()
        {
            if (IsCrouching)
            {
                TryStand();
                return;
            }
            IsCrouching = true;
            ApplyHeight(crouchHeight);
        }

        private bool TryStand()
        {
            if (!CanStand()) return false;
            IsCrouching = false;
            ApplyHeight(standHeight);
            return true;
        }

        /// <summary>Verifica se há espaço acima da cabeça para levantar (ex.: embaixo de uma mesa).</summary>
        private bool CanStand()
        {
            float r = controller.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (crouchHeight - r);
            float distance = standHeight - crouchHeight;
            return !Physics.SphereCast(origin, r, Vector3.up, out _, distance, ~0, QueryTriggerInteraction.Ignore);
        }

        private void ApplyHeight(float h)
        {
            controller.height = h;
            controller.center = new Vector3(0f, h / 2f, 0f);
            if (visual != null)
            {
                // Cápsula primitiva tem 2 m de altura na escala 1.
                visual.localScale = new Vector3(visual.localScale.x, h / 2f, visual.localScale.z);
                visual.localPosition = new Vector3(0f, h / 2f, 0f);
            }
        }
    }
}
