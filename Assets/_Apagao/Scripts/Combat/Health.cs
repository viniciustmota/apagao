using System;
using UnityEngine;

namespace Apagao.Combat
{
    /// <summary>
    /// Vida de qualquer coisa que leva dano: o Caio, robôs, objetos (spec 08).
    /// Aplica multiplicador por tipo de dano e bônus de golpe pelas costas (ponto fraco).
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private DamageMultipliers multipliers = new DamageMultipliers();

        [Header("Ponto fraco nas costas")]
        [Tooltip("Multiplicador para golpes vindos de trás. 1 = sem ponto fraco.")]
        [SerializeField] private float backMultiplier = 1f;
        [Tooltip("Abertura do cone traseiro, em graus.")]
        [SerializeField] private float backAngle = 120f;

        [Header("Blindagem frontal")]
        [Tooltip("Multiplicador para golpes vindos da frente (Brutamontes: 0,25). 1 = sem blindagem.")]
        [SerializeField] private float frontMultiplier = 1f;
        [SerializeField] private float frontAngle = 100f;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
        public bool IsDead { get; private set; }
        public float LastDamageTime { get; private set; } = float.NegativeInfinity;

        /// <summary>(golpe, dano aplicado depois dos multiplicadores)</summary>
        public event Action<DamageInfo, float> Damaged;
        public event Action Died;
        public event Action<float> Healed;

        private IDamageFilter filter;

        private void Awake()
        {
            Current = maxHealth;
            filter = GetComponent<IDamageFilter>();
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (IsDead) return;
            if (filter != null && filter.Ignores(info)) return;

            float amount = info.Amount * multipliers.For(info.Type);
            if (backMultiplier != 1f && IsFromBehind(info.Direction)) amount *= backMultiplier;
            if (frontMultiplier != 1f && IsFromFront(info.Direction)) amount *= frontMultiplier;
            if (amount <= 0f) return;

            Current = Mathf.Max(0f, Current - amount);
            LastDamageTime = Time.time;
            Damaged?.Invoke(info, amount);

            if (Current <= 0f)
            {
                IsDead = true;
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            Healed?.Invoke(amount);
        }

        /// <summary>Atacante atrás = o golpe viaja no mesmo sentido para onde o alvo olha.</summary>
        public bool IsFromBehind(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return false;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return Vector3.Angle(forward, direction) <= backAngle * 0.5f;
        }

        /// <summary>Atacante na frente = o golpe viaja contra a direção para onde o alvo olha.</summary>
        public bool IsFromFront(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return false; // fogo e explosão sem direção ignoram a blindagem
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return Vector3.Angle(forward, -direction) <= frontAngle * 0.5f;
        }
    }
}
