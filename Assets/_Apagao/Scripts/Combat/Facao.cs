using System.Collections;
using System.Collections.Generic;
using Apagao.Player;
using Apagao.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Apagao.Combat
{
    /// <summary>
    /// Facão do Caio (spec 09). Clique = ataque leve. Segurar e soltar = pesado.
    /// O golpe vai para onde a câmera aponta, com assistência de mira e um pequeno avanço até o alvo.
    /// Gasta com o uso; quebrado, causa metade do dano e não permite takedown.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(CharacterController))]
    public class Facao : MonoBehaviour
    {
        [Header("Dano")]
        [SerializeField] private float lightDamage = 25f;
        [SerializeField] private float heavyDamage = 50f;
        [Tooltip("Segurar o botão por este tempo vira ataque pesado.")]
        [SerializeField] private float heavyHoldTime = 0.3f;

        [Header("Tempos (s)")]
        [SerializeField] private float lightWindup = 0.08f;
        [SerializeField] private float lightActive = 0.10f;
        [SerializeField] private float lightRecovery = 0.30f;
        [SerializeField] private float heavyWindup = 0.25f;
        [SerializeField] private float heavyActive = 0.15f;
        [SerializeField] private float heavyRecovery = 0.60f;

        [Header("Assistência de mira")]
        [SerializeField] private float aimAssistRange = 2.8f;
        [SerializeField] private float aimAssistAngle = 60f;

        [Header("Avanço até o alvo")]
        [SerializeField] private float lightLunge = 0.4f;
        [SerializeField] private float heavyLunge = 0.8f;
        [Tooltip("O avanço para a esta distância do alvo.")]
        [SerializeField] private float lungeStopDistance = 0.9f;

        [Header("Hitbox")]
        [SerializeField] private Vector3 hitboxHalfExtents = new Vector3(0.7f, 0.9f, 0.9f);
        [SerializeField] private float hitboxForward = 1.0f;
        [SerializeField] private float hitboxHeight = 1.0f;

        [Header("Durabilidade")]
        [SerializeField] private float maxDurability = 100f;
        [SerializeField] private float lightCost = 5f;
        [SerializeField] private float heavyCost = 10f;
        [SerializeField] private float takedownCost = 15f;
        [SerializeField, Range(0f, 1f)] private float brokenDamageMultiplier = 0.5f;

        [Header("Retorno do acerto")]
        [Tooltip("Congelamento no impacto (s, tempo real).")]
        [SerializeField] private float lightHitStop = 0.05f;
        [SerializeField] private float heavyHitStop = 0.09f;
        [SerializeField] private float lightShake = 0.05f;
        [SerializeField] private float heavyShake = 0.12f;

        [Header("Ruído ao acertar metal")]
        [SerializeField] private float hitNoiseRadius = 8f;
        [SerializeField] private float hitNoiseIntensity = 0.5f;

        [Header("Visual")]
        [Tooltip("Pivô do facão na mão; gira durante o golpe.")]
        [SerializeField] private Transform blade;

        public float Durability { get; private set; }
        public float DurabilityNormalized => Durability / maxDurability;
        public bool IsBroken => Durability <= 0f;
        public bool IsAttacking { get; private set; }
        /// <summary>Alvo que a assistência de mira vai acertar agora (para o HUD marcar).</summary>
        public Transform PreviewTarget { get; private set; }

        private readonly Collider[] buffer = new Collider[16];
        private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
        private PlayerController player;
        private CharacterController controller;
        private Transform cameraTransform;
        private Quaternion bladeRest;
        private bool charging;
        private float pressTime;
        private bool hitSomething;
        private ThirdPersonCamera cameraRig;
        private WeaponHolder holder;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            controller = GetComponent<CharacterController>();
            holder = GetComponent<WeaponHolder>();
            Durability = maxDurability;
            if (blade != null) bladeRest = blade.localRotation;
        }

        private void Start()
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
                cameraRig = Camera.main.GetComponent<ThirdPersonCamera>();
            }
        }

        private void Update()
        {
            // Com outra arma na mão, o facão fica guardado.
            if (holder != null && holder.Current != WeaponSlot.Facao && !IsAttacking)
            {
                PreviewTarget = null;
                if (charging) EndCharge();
                if (blade != null) blade.gameObject.SetActive(false);
                return;
            }
            if (blade != null && !blade.gameObject.activeSelf) blade.gameObject.SetActive(true);

            PreviewTarget = player.InputLocked || IsAttacking ? null : FindAimTarget(out _);

            var mouse = Mouse.current;
            if (mouse == null || player.InputLocked || IsAttacking || Cursor.lockState != CursorLockMode.Locked)
            {
                if (charging) EndCharge();
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                charging = true;
                pressTime = Time.time;
                player.RotationLocked = true;
            }

            if (!charging) return;

            // Carregando: o Caio fica virado para a câmera e ergue o facão.
            player.SnapFacing(AimDirection());
            float held = Time.time - pressTime;
            SetBladeAngle(-Mathf.Lerp(20f, 70f, Mathf.Clamp01(held / heavyHoldTime)));

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                charging = false;
                StartCoroutine(Swing(held >= heavyHoldTime));
            }
        }

        private void EndCharge()
        {
            charging = false;
            player.RotationLocked = false;
            ResetBlade();
        }

        private IEnumerator Swing(bool heavy)
        {
            IsAttacking = true;
            player.RotationLocked = true;
            hitThisSwing.Clear();
            hitSomething = false;

            Transform target = FindAimTarget(out Vector3 aimDir);
            player.SnapFacing(aimDir);

            // Preparação: acompanha o alvo e avança até ele.
            float windup = heavy ? heavyWindup : lightWindup;
            float lunge = heavy ? heavyLunge : lightLunge;
            float moved = 0f;
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                if (target != null)
                {
                    Vector3 to = target.position - transform.position;
                    to.y = 0f;
                    player.SnapFacing(to);
                    float room = Mathf.Max(0f, to.magnitude - lungeStopDistance);
                    float step = Mathf.Min(lunge - moved, room, lunge / windup * Time.deltaTime);
                    if (step > 0f && controller.enabled)
                    {
                        controller.Move(to.normalized * step);
                        moved += step;
                    }
                }
                yield return null;
            }

            // Golpe: janela ativa conferida a cada frame.
            float damage = (heavy ? heavyDamage : lightDamage) * (IsBroken ? brokenDamageMultiplier : 1f);
            Wear(heavy ? heavyCost : lightCost);
            StartCoroutine(StrikeVisual());

            float active = heavy ? heavyActive : lightActive;
            for (float t = 0f; t <= active; t += Time.deltaTime)
            {
                bool hadHit = hitSomething;
                HitCheck(damage);
                if (!hadHit && hitSomething)
                {
                    if (cameraRig != null) cameraRig.Shake(heavy ? heavyShake : lightShake, 0.15f);
                    yield return HitStop(heavy ? heavyHitStop : lightHitStop);
                }
                yield return null;
            }

            if (hitSomething)
                NoiseSystem.Emit(transform.position + transform.forward, hitNoiseRadius, hitNoiseIntensity, NoiseType.Impacto, gameObject);

            yield return new WaitForSeconds(heavy ? heavyRecovery : lightRecovery);

            ResetBlade();
            player.RotationLocked = false;
            IsAttacking = false;
        }

        /// <summary>Congela o tempo por um instante no impacto: dá peso ao golpe.</summary>
        private static IEnumerator HitStop(float seconds)
        {
            if (seconds <= 0f) yield break;
            Time.timeScale = 0.02f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
        }

        /// <summary>Direção da câmera no plano horizontal (ou a frente do Caio, sem câmera).</summary>
        private Vector3 AimDirection()
        {
            Vector3 dir = cameraTransform != null ? cameraTransform.forward : transform.forward;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        }

        /// <summary>Alvo vivo mais alinhado com a câmera, dentro do alcance e do ângulo da assistência.</summary>
        private Transform FindAimTarget(out Vector3 aimDir)
        {
            aimDir = AimDirection();
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, aimAssistRange, buffer, ~0, QueryTriggerInteraction.Ignore);

            Transform best = null;
            float bestAngle = aimAssistAngle;
            for (int i = 0; i < count; i++)
            {
                Collider c = buffer[i];
                if (c.transform.IsChildOf(transform)) continue;
                var damageable = c.GetComponentInParent<IDamageable>();
                if (damageable == null || damageable.IsDead || !(damageable is Component comp)) continue;

                Vector3 to = comp.transform.position - transform.position;
                to.y = 0f;
                float angle = Vector3.Angle(aimDir, to);
                if (angle <= bestAngle)
                {
                    bestAngle = angle;
                    best = comp.transform;
                }
            }

            if (best != null)
            {
                Vector3 to = best.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f) aimDir = to.normalized;
            }
            return best;
        }

        private void HitCheck(float damage)
        {
            Vector3 center = transform.position + Vector3.up * hitboxHeight + transform.forward * hitboxForward;
            int count = Physics.OverlapBoxNonAlloc(center, hitboxHalfExtents, buffer, transform.rotation, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider c = buffer[i];
                if (c.transform.IsChildOf(transform)) continue;
                var target = c.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead || !hitThisSwing.Add(target)) continue;

                target.TakeDamage(new DamageInfo(damage, DamageType.Corte, c.ClosestPoint(center), transform.forward, gameObject));
                hitSomething = true;
            }
        }

        /// <summary>Chamado pelo TakedownSystem no golpe final.</summary>
        public void UseForTakedown()
        {
            Wear(takedownCost);
            StartCoroutine(TakedownVisual());
        }

        /// <summary>Afiar (crafting, marco 8).</summary>
        public void Sharpen(float amount) => Durability = Mathf.Min(maxDurability, Durability + amount);

        private void Wear(float amount) => Durability = Mathf.Max(0f, Durability - amount);

        // ---------- Visual provisório (sem animação de personagem) ----------

        private IEnumerator TakedownVisual()
        {
            SetBladeAngle(-70f);
            yield return StrikeVisual();
            yield return new WaitForSeconds(0.3f);
            ResetBlade();
        }

        private IEnumerator StrikeVisual()
        {
            const float duration = 0.08f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                SetBladeAngle(Mathf.Lerp(-70f, 60f, t / duration));
                yield return null;
            }
            SetBladeAngle(60f);
        }

        private void SetBladeAngle(float x)
        {
            if (blade != null) blade.localRotation = bladeRest * Quaternion.Euler(x, 0f, 0f);
        }

        private void ResetBlade()
        {
            if (blade != null) blade.localRotation = bladeRest;
        }

        private void OnDisable()
        {
            if (player != null) player.RotationLocked = false;
            Time.timeScale = 1f;
            IsAttacking = false;
            charging = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
            Gizmos.matrix = Matrix4x4.TRS(
                transform.position + Vector3.up * hitboxHeight + transform.forward * hitboxForward,
                transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, hitboxHalfExtents * 2f);
        }
    }
}
