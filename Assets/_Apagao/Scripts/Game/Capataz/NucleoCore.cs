using Apagao.Audio;
using Apagao.Combat;
using UnityEngine;

namespace Apagao.Game
{
    /// <summary>
    /// O Núcleo da VIGÍLIA (spec 17): vida própria, escudo que bloqueia todo dano e PEM que derruba o escudo por 4 s
    /// (só quando o Capataz deixa o escudo vulnerável, na fase 2).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class NucleoCore : MonoBehaviour, IDamageFilter, IStunnable
    {
        [SerializeField] private Renderer shield;
        [SerializeField] private Renderer core;
        [SerializeField] private Light coreLight;
        [SerializeField] private float pemWindow = 4f;

        public Health Health { get; private set; }
        /// <summary>O escudo existe (fase 1 e 2).</summary>
        public bool ShieldActive { get; set; } = true;
        /// <summary>O PEM consegue derrubar o escudo (fase 2).</summary>
        public bool ShieldBreakable { get; set; }
        public bool ShieldUp => ShieldActive && Time.time >= shieldDownUntil;
        public float ShieldDownLeft => Mathf.Max(0f, shieldDownUntil - Time.time);
        public bool IsStunned => !ShieldUp;

        private float shieldDownUntil = float.NegativeInfinity;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            Health = GetComponent<Health>();
            block = new MaterialPropertyBlock();
        }

        public bool Ignores(in DamageInfo info) => ShieldUp;

        /// <summary>PEM (spec 17 R4): abre a janela de golpes.</summary>
        public void Stun(float seconds)
        {
            if (!ShieldActive || !ShieldBreakable || !ShieldUp) return;
            shieldDownUntil = Time.time + pemWindow;
            Sfx.Play("antena_sabotada", transform.position, 1f, SoundCategory.Efeitos, 40f, 0f, 1.3f);
        }

        private void Update()
        {
            if (shield != null) shield.enabled = ShieldUp;
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * (ShieldUp ? 2f : 9f));
            if (coreLight != null) coreLight.intensity = (ShieldUp ? 4f : 9f) * pulse;
            if (core != null)
            {
                block.SetColor("_EmissionColor", (ShieldUp ? new Color(1f, 0.1f, 0.05f) : new Color(1f, 0.6f, 0.1f)) * (2f + 3f * pulse));
                core.SetPropertyBlock(block);
            }
        }
    }
}
