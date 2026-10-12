using System;
using UnityEngine;

namespace Apagao.Combat
{
    public enum DamageType { Corte, Bala, Fogo, Explosao, Eletrico, Queda }

    /// <summary>Um golpe ou disparo. Direction aponta do atacante para o alvo.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly DamageType Type;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly GameObject Source;

        public DamageInfo(float amount, DamageType type, Vector3 point, Vector3 direction, GameObject source)
        {
            Amount = amount;
            Type = type;
            Point = point;
            Direction = direction;
            Source = source;
        }
    }

    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(in DamageInfo info);
    }

    /// <summary>Afetado pelo PEM (spec 11). A duração é sugerida; cada alvo aplica seu próprio multiplicador.</summary>
    public interface IStunnable
    {
        bool IsStunned { get; }
        void Stun(float seconds);
    }

    /// <summary>Permite ao dono da vida ignorar um golpe (ex.: esquiva contra explosões).</summary>
    public interface IDamageFilter
    {
        bool Ignores(in DamageInfo info);
    }

    /// <summary>Inimigo que pode ser finalizado pelo facão (spec 09).</summary>
    public interface ITakedownTarget
    {
        Transform transform { get; }
        bool CanBeTakenDown(Transform attacker);
        /// <summary>Congela o alvo no início da animação.</summary>
        void BeginTakedown();
        /// <summary>Golpe final: o alvo é destruído.</summary>
        void CompleteTakedown();
    }

    /// <summary>Multiplicador de dano por tipo (spec 08). 0 = imune.</summary>
    [Serializable]
    public class DamageMultipliers
    {
        public float corte = 1f;
        public float bala = 1f;
        public float fogo = 1f;
        public float explosao = 1f;
        public float eletrico = 1f;

        public float For(DamageType type) => type switch
        {
            DamageType.Corte => corte,
            DamageType.Bala => bala,
            DamageType.Fogo => fogo,
            DamageType.Explosao => explosao,
            DamageType.Eletrico => eletrico,
            _ => 1f
        };
    }
}
