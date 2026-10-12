using System;
using Apagao.Audio;
using Apagao.Combat;
using Apagao.Systems;
using UnityEngine;

namespace Apagao.Game
{
    /// <summary>
    /// Gerador do escudo do Capataz (spec 17 R3): segurar E por 3 s com o facão em mãos para cortar os cabos.
    /// Fica na cena desligado; o checkpoint lembra (ISaveGone).
    /// </summary>
    public class Generator : MonoBehaviour, IInteractable, ISaveGone
    {
        [SerializeField] private float holdTime = 3f;
        [SerializeField] private Renderer panel;
        [SerializeField] private Light glow;

        public bool Sabotaged { get; private set; }
        public event Action<Generator> Disabled;

        private LoopSound hum;
        private MaterialPropertyBlock block;

        public string Prompt => "Cortar os cabos do gerador";
        public float HoldTime => holdTime;
        public Vector3 Position => transform.position + Vector3.up * 0.8f;

        private void OnEnable() => InteractableRegistry.Register(this);
        private void OnDisable() => InteractableRegistry.Unregister(this);

        private void Start()
        {
            if (!Sabotaged) hum = LoopSound.Attach(gameObject, "antena_loop", 0.5f, 18f, SoundCategory.Efeitos, 0.7f);
        }

        private void Update()
        {
            if (Sabotaged || glow == null) return;
            glow.intensity = 2f + Mathf.PingPong(Time.time * 3f, 1.5f);
        }

        /// <summary>Precisa do facão (spec 17 R3).</summary>
        public bool CanInteract(GameObject who) => !Sabotaged && PlayerLoadout.Allows(who.transform, WeaponSlot.Facao);

        public void Interact(GameObject who) => Disable(silent: false);

        public void RestoreGone() => Disable(silent: true);

        private void Disable(bool silent)
        {
            if (Sabotaged) return;
            Sabotaged = true;
            if (hum != null) hum.Stop();
            if (glow != null) glow.enabled = false;
            if (panel != null)
            {
                block ??= new MaterialPropertyBlock();
                block.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f));
                block.SetColor("_EmissionColor", Color.black);
                panel.SetPropertyBlock(block);
            }
            if (!silent)
            {
                Sfx.Play("antena_sabotada", transform.position + Vector3.up, 1f, SoundCategory.Efeitos, 40f);
                NoiseSystem.Emit(transform.position, 15f, 0.8f, NoiseType.Impacto, gameObject);
            }
            SaveId.ReportGone(gameObject);
            Disabled?.Invoke(this);
        }
    }
}
