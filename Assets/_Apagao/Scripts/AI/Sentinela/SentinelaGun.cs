using Apagao.Combat;
using Apagao.Player;
using UnityEngine;
using Apagao.Audio;

namespace Apagao.AI.Sentinela
{
    /// <summary>
    /// Rifle da Sentinela (spec 05 R9): mira por 0,5 s (laser engrossa), depois rajada de 3.
    /// A precisão cai com a distância e quando o Caio está se movendo.
    /// </summary>
    public class SentinelaGun : MonoBehaviour
    {
        [SerializeField] private float range = 14f;
        [SerializeField] private float damage = 15f;

        [Header("Ritmo")]
        [SerializeField] private float aimTime = 0.5f;
        [SerializeField] private int burstSize = 3;
        [SerializeField] private float burstInterval = 0.12f;
        [SerializeField] private float cooldown = 2f;

        [Header("Precisão")]
        [SerializeField, Range(0f, 1f)] private float closeAccuracy = 0.8f;
        [SerializeField, Range(0f, 1f)] private float farAccuracy = 0.35f;
        [SerializeField, Range(0f, 1f)] private float walkingMultiplier = 0.8f;
        [SerializeField, Range(0f, 1f)] private float runningMultiplier = 0.5f;

        [Header("Visual")]
        [SerializeField] private LineRenderer tracer;
        [SerializeField] private float tracerTime = 0.05f;

        public float Range => range;
        public bool IsAiming => aimTimer > 0f;

        private float aimTimer;
        private float cooldownTimer;
        private int shotsLeft;
        private float shotTimer;
        private float tracerOffTime;

        /// <summary>Chamado a cada frame pelo estado de combate.</summary>
        public void Tick(bool canShoot, Vector3 muzzle, Transform target, IDamageable targetHealth, PlayerController targetMove)
        {
            cooldownTimer -= Time.deltaTime;
            if (tracer != null && tracer.enabled && Time.time >= tracerOffTime) tracer.enabled = false;

            if (!canShoot || target == null)
            {
                ResetAim();
                return;
            }

            if (shotsLeft > 0)
            {
                shotTimer -= Time.deltaTime;
                if (shotTimer > 0f) return;

                Fire(muzzle, target, targetHealth, targetMove);
                shotsLeft--;
                shotTimer = burstInterval;
                if (shotsLeft == 0) cooldownTimer = cooldown;
                return;
            }

            if (cooldownTimer > 0f) return;

            if (aimTimer == 0f) Sfx.Play("sentinela_mira", muzzle, 0.6f, SoundCategory.Inimigos, 30f, 0f);
            aimTimer += Time.deltaTime;
            if (aimTimer >= aimTime)
            {
                aimTimer = 0f;
                shotsLeft = burstSize;
                shotTimer = 0f;
            }
        }

        public void ResetAim()
        {
            aimTimer = 0f;
            shotsLeft = 0;
        }

        private void Fire(Vector3 muzzle, Transform target, IDamageable targetHealth, PlayerController targetMove)
        {
            Sfx.Play("sentinela_tiro", muzzle, 0.9f, SoundCategory.Inimigos, 60f);
            Vector3 aimPoint = target.position + Vector3.up * 1.2f;
            float distance = Vector3.Distance(muzzle, aimPoint);

            float accuracy = Mathf.Lerp(closeAccuracy, farAccuracy, Mathf.Clamp01(distance / range));
            if (targetMove != null)
            {
                if (targetMove.Mode == MoveMode.Correndo) accuracy *= runningMultiplier;
                else if (targetMove.Mode == MoveMode.Andando) accuracy *= walkingMultiplier;
            }

            bool hitRoll = Random.value < accuracy;
            Vector3 end = hitRoll ? aimPoint : aimPoint + Random.insideUnitSphere * 1.2f;
            Vector3 dir = (end - muzzle).normalized;

            // O tiro para no primeiro obstáculo: atrás de cobertura, o Caio está seguro.
            if (Physics.Raycast(muzzle, dir, out RaycastHit hit, distance + 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                if (hitRoll && hit.collider.transform.IsChildOf(target) && targetHealth != null)
                    targetHealth.TakeDamage(new DamageInfo(damage, DamageType.Bala, hit.point, dir, gameObject));
            }

            if (tracer != null)
            {
                tracer.SetPosition(0, muzzle);
                tracer.SetPosition(1, end);
                tracer.enabled = true;
                tracerOffTime = Time.time + tracerTime;
            }
        }
    }
}
