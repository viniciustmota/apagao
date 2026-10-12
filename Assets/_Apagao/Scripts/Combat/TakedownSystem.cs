using System.Collections;
using Apagao.Player;
using Apagao.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using Apagao.Audio;

namespace Apagao.Combat
{
    /// <summary>
    /// Takedown furtivo (spec 09): procura um alvo finalizável perto do Caio e, com E,
    /// alinha o Caio atrás dele e o destrói com o facão.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class TakedownSystem : MonoBehaviour
    {
        [SerializeField] private float range = 1.8f;
        [SerializeField] private float duration = 1.2f;
        [Tooltip("Momento do golpe final, em segundos desde o início.")]
        [SerializeField] private float killAt = 0.8f;
        [SerializeField] private float alignDistance = 1.0f;
        [SerializeField] private float alignTime = 0.3f;

        [Header("Ruído")]
        [SerializeField] private float noiseRadius = 3f;
        [SerializeField] private float noiseIntensity = 0.3f;

        public ITakedownTarget Candidate { get; private set; }
        public bool IsExecuting { get; private set; }

        private readonly Collider[] buffer = new Collider[16];
        private PlayerController player;
        private CharacterController controller;
        private Facao facao;
        private Health health;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            controller = GetComponent<CharacterController>();
            facao = GetComponent<Facao>();
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (IsExecuting) return;

            Candidate = PlayerLoadout.Allows(this, WeaponSlot.Facao) ? FindCandidate() : null; // sem facão, sem takedown
            if (Candidate == null) return;

            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked)
                StartCoroutine(Execute(Candidate));
        }

        private ITakedownTarget FindCandidate()
        {
            if (player.InputLocked) return null;
            if (health != null && health.IsDead) return null;
            if (facao != null && (facao.IsBroken || facao.IsAttacking)) return null;

            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, range, buffer, ~0, QueryTriggerInteraction.Ignore);
            ITakedownTarget best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var target = buffer[i].GetComponentInParent<ITakedownTarget>();
                if (target == null || !target.CanBeTakenDown(transform)) continue;

                float d = Vector3.Distance(transform.position, target.transform.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = target;
                }
            }
            return best;
        }

        private IEnumerator Execute(ITakedownTarget target)
        {
            IsExecuting = true;
            player.InputLocked = true;
            target.BeginTakedown();

            Transform t = target.transform;
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 endPos = t.position - t.forward * alignDistance;
            endPos.y = t.position.y;
            Quaternion endRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(t.forward, Vector3.up));

            // Desliga o CharacterController para posicionar o Caio diretamente.
            controller.enabled = false;
            for (float time = 0f; time < alignTime; time += Time.deltaTime)
            {
                float k = time / alignTime;
                transform.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, k), Quaternion.Slerp(startRot, endRot, k));
                yield return null;
            }
            transform.SetPositionAndRotation(endPos, endRot);

            yield return new WaitForSeconds(Mathf.Max(0f, killAt - alignTime));

            if (facao != null) facao.UseForTakedown();
            Sfx.Play("takedown", transform.position + transform.forward + Vector3.up, 0.9f, SoundCategory.Efeitos, 20f);
            NoiseSystem.Emit(transform.position, noiseRadius, noiseIntensity, NoiseType.Impacto, gameObject);
            if (t != null) target.CompleteTakedown();

            yield return new WaitForSeconds(Mathf.Max(0f, duration - killAt));

            controller.enabled = true;
            player.InputLocked = false;
            IsExecuting = false;
            Candidate = null;
        }
    }
}
