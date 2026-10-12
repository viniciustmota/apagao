using Apagao.Combat;
using Apagao.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Apagao.Player
{
    /// <summary>Modo de movimento atual. No marco 4 cada modo vira um nível de ruído no NoiseSystem.</summary>
    public enum MoveMode { Parado, Andando, Correndo, Agachado }

    /// <summary>
    /// Movimento do Caio: andar (WASD), correr (Shift), agachar (C ou Ctrl), pular (Espaço) e esquivar (Alt).
    /// A direção do movimento é relativa à câmera.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour, IDamageFilter
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

        [Header("Esquiva (spec 01 R8)")]
        [SerializeField] private float dodgeDistance = 3f;
        [SerializeField] private float dodgeDuration = 0.4f;
        [Tooltip("Primeiros segundos da esquiva em que explosões não causam dano.")]
        [SerializeField] private float dodgeInvulnerable = 0.25f;
        [SerializeField] private float dodgeCooldown = 0.8f;
        [SerializeField] private float dodgeNoiseRadius = 5f;
        [SerializeField] private float dodgeNoiseIntensity = 0.4f;

        [Header("Mirando")]
        [Tooltip("Velocidade máxima enquanto mira uma arma.")]
        [SerializeField] private float aimSpeed = 2.2f;

        [Header("Referências")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Modelo visual, achatado ao agachar. Opcional.")]
        [SerializeField] private Transform visual;

        /// <summary>Trava o input (takedown, morte, cutscene). A gravidade continua.</summary>
        public bool InputLocked { get; set; }

        /// <summary>Impede que o movimento gire o Caio (ex.: durante um golpe, quem decide a direção é o ataque).</summary>
        public bool RotationLocked { get; set; }

        /// <summary>Mirando uma arma: anda devagar e não corre.</summary>
        public bool Aiming { get; set; }
        public bool IsDodging { get; private set; }
        /// <summary>O LedgeClimber usou o Espaço para escalar: não pular neste frame.</summary>
        public bool JumpSuppressed { get; set; }
        /// <summary>Direção desejada no mundo (input relativo à câmera), normalizada ou zero.</summary>
        public Vector3 WishDirection { get; private set; }

        [Header("Borda (spec 13 R6)")]
        [Tooltip("Agachado, o Caio não anda para fora de bordas mais altas que isto.")]
        [SerializeField] private float edgeDropLimit = 0.6f;

        public MoveMode Mode { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsGrounded => controller.isGrounded;
        public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private Vector3 dodgeDir;
        private float dodgeEnd;
        private float dodgeInvulnerableUntil = float.NegativeInfinity;
        private float nextDodge;

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
            if (kb == null || !controller.enabled) return;
            bool locked = InputLocked;

            Vector2 input = locked ? Vector2.zero : new Vector2(
                (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
            input = Vector2.ClampMagnitude(input, 1f);
            bool moving = input.sqrMagnitude > 0.01f;

            if (!locked && (kb.cKey.wasPressedThisFrame || kb.leftCtrlKey.wasPressedThisFrame))
                ToggleCrouch();

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }

            if (!locked && !IsDodging && kb.leftAltKey.wasPressedThisFrame && controller.isGrounded && Time.time >= nextDodge)
            {
                Vector3 dir = moving ? forward * input.y + right * input.x : -transform.forward;
                StartDodge(dir);
            }
            if (IsDodging)
            {
                TickDodge();
                return;
            }

            // Correr levanta o Caio, se houver espaço acima dele. Mirando não dá para correr.
            bool running = moving && kb.leftShiftKey.isPressed && !Aiming;
            if (running && IsCrouching && !TryStand())
                running = false;

            float targetSpeed = IsCrouching ? crouchSpeed : running ? runSpeed : walkSpeed;
            if (Aiming) targetSpeed = Mathf.Min(targetSpeed, aimSpeed);

            WishDirection = moving ? (forward * input.y + right * input.x).normalized : Vector3.zero;
            Vector3 desired = (forward * input.y + right * input.x) * targetSpeed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desired, acceleration * Time.deltaTime);

            if (moving && !RotationLocked && desired.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(desired, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            }

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // mantém o Caio colado no chão em descidas

            if (!locked && !JumpSuppressed && kb.spaceKey.wasPressedThisFrame && controller.isGrounded)
            {
                if (IsCrouching) TryStand();
                else verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;
            if (IsCrouching && controller.isGrounded) KeepOffEdges();
            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            JumpSuppressed = false;

            Mode = IsCrouching ? MoveMode.Agachado
                 : !moving ? MoveMode.Parado
                 : running ? MoveMode.Correndo
                 : MoveMode.Andando;
        }

        /// <summary>Agachado na beira de um telhado, o Caio para em vez de cair.</summary>
        private void KeepOffEdges()
        {
            Vector3 flat = horizontalVelocity;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return;

            Vector3 probe = transform.position + flat.normalized * (controller.radius + 0.15f) + Vector3.up * 0.3f;
            if (!Physics.Raycast(probe, Vector3.down, 0.3f + edgeDropLimit, ~0, QueryTriggerInteraction.Ignore))
                horizontalVelocity = Vector3.zero;
        }

        // ---------- Esquiva ----------

        private void StartDodge(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = -transform.forward;
            if (IsCrouching && !TryStand()) return;

            dodgeDir = direction.normalized;
            IsDodging = true;
            dodgeEnd = Time.time + dodgeDuration;
            dodgeInvulnerableUntil = Time.time + dodgeInvulnerable;
            nextDodge = dodgeEnd + dodgeCooldown;
            horizontalVelocity = Vector3.zero;
            NoiseSystem.Emit(transform.position, dodgeNoiseRadius, dodgeNoiseIntensity, NoiseType.Passo, gameObject);
        }

        private void TickDodge()
        {
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;
            float speed = dodgeDistance / dodgeDuration;
            controller.Move((dodgeDir * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
            Mode = MoveMode.Correndo;
            if (Time.time >= dodgeEnd) IsDodging = false;
        }

        /// <summary>Esquiva no tempo certo anula o dano de explosão (Vespa).</summary>
        public bool Ignores(in DamageInfo info) =>
            info.Type == DamageType.Explosao && Time.time < dodgeInvulnerableUntil;

        /// <summary>Vira o Caio na hora para uma direção no plano horizontal.</summary>
        public void SnapFacing(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
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
