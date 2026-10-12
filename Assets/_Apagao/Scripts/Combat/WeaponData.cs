using UnityEngine;

namespace Apagao.Combat
{
    /// <summary>Dados de balanceamento de uma arma de fogo (spec 10). Ajuste no Inspector sem recompilar.</summary>
    [CreateAssetMenu(menuName = "Apagão/Arma de fogo", fileName = "NovaArma")]
    public class WeaponData : ScriptableObject
    {
        [SerializeField] private string displayName = "Arma";

        [Header("Dano")]
        [SerializeField] private float damage = 50f;
        [Tooltip("Projéteis por disparo (espingarda > 1).")]
        [SerializeField] private int pellets = 1;
        [SerializeField] private float range = 60f;
        [Tooltip("Dispersão em graus, parado.")]
        [SerializeField] private float spread = 0.5f;

        [Header("Ritmo")]
        [SerializeField] private float fireInterval = 0.35f;
        [SerializeField] private int magazine = 6;
        [SerializeField] private float reloadTime = 2.5f;

        [Header("Munição")]
        [SerializeField] private int maxReserve = 12;
        [SerializeField] private int startLoaded = 6;
        [SerializeField] private int startReserve = 6;

        [Header("Ruído (spec 06)")]
        [SerializeField] private float noiseRadius = 40f;
        [SerializeField] private float noiseIntensity = 1f;

        public string DisplayName => displayName;
        public float Damage => damage;
        public int Pellets => Mathf.Max(1, pellets);
        public float Range => range;
        public float Spread => spread;
        public float FireInterval => fireInterval;
        public int Magazine => magazine;
        public float ReloadTime => reloadTime;
        public int MaxReserve => maxReserve;
        public int StartLoaded => startLoaded;
        public int StartReserve => startReserve;
        public float NoiseRadius => noiseRadius;
        public float NoiseIntensity => noiseIntensity;
    }
}
