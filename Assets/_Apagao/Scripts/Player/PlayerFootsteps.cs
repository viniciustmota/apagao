using Apagao.Systems;
using UnityEngine;

namespace Apagao.Player
{
    /// <summary>
    /// Passos e quedas do Caio viram ruído (spec 06). Um evento por passada, conforme o modo de movimento.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerFootsteps : MonoBehaviour
    {
        [Header("Passada (m)")]
        [SerializeField] private float walkStride = 0.75f;
        [SerializeField] private float runStride = 1.1f;
        [SerializeField] private float crouchStride = 0.6f;

        [Header("Andando")]
        [SerializeField] private float walkRadius = 4f;
        [SerializeField] private float walkIntensity = 0.3f;

        [Header("Correndo")]
        [SerializeField] private float runRadius = 10f;
        [SerializeField] private float runIntensity = 0.6f;

        [Header("Agachado")]
        [SerializeField] private float crouchRadius = 1.5f;
        [SerializeField] private float crouchIntensity = 0.1f;

        [Header("Queda")]
        [SerializeField] private float minFallHeight = 2f;
        [SerializeField] private float fallRadius = 6f;
        [SerializeField] private float fallIntensity = 0.5f;

        private PlayerController player;
        private Vector3 lastPosition;
        private float distanceSinceStep;
        private bool wasGrounded = true;
        private float peakY;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 pos = transform.position;
            Vector3 delta = pos - lastPosition;
            delta.y = 0f;
            lastPosition = pos;

            bool grounded = player.IsGrounded;

            if (!grounded)
            {
                if (wasGrounded) peakY = pos.y;
                peakY = Mathf.Max(peakY, pos.y);
            }
            else
            {
                if (!wasGrounded)
                {
                    if (peakY - pos.y >= minFallHeight)
                        NoiseSystem.Emit(pos, fallRadius, fallIntensity, NoiseType.Queda, gameObject);
                    distanceSinceStep = 0f;
                }

                if (player.Mode == MoveMode.Parado)
                {
                    distanceSinceStep = 0f;
                }
                else
                {
                    distanceSinceStep += delta.magnitude;
                    float stride = Stride();
                    if (distanceSinceStep >= stride)
                    {
                        distanceSinceStep -= stride;
                        EmitStep(pos);
                    }
                }
            }

            wasGrounded = grounded;
        }

        private float Stride() => player.Mode switch
        {
            MoveMode.Correndo => runStride,
            MoveMode.Agachado => crouchStride,
            _ => walkStride
        };

        private void EmitStep(Vector3 pos)
        {
            switch (player.Mode)
            {
                case MoveMode.Correndo: NoiseSystem.Emit(pos, runRadius, runIntensity, NoiseType.Passo, gameObject); break;
                case MoveMode.Agachado: NoiseSystem.Emit(pos, crouchRadius, crouchIntensity, NoiseType.Passo, gameObject); break;
                default: NoiseSystem.Emit(pos, walkRadius, walkIntensity, NoiseType.Passo, gameObject); break;
            }
        }
    }
}
