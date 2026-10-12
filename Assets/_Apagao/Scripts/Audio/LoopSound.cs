using UnityEngine;

namespace Apagao.Audio
{
    /// <summary>
    /// Som contínuo preso a um objeto (motor, hélice, fogo, antena). Abafa atrás de paredes (spec 21 R6).
    /// Criado por código com <see cref="Attach"/>; Volume e Pitch podem ser mudados a cada frame.
    /// </summary>
    public class LoopSound : MonoBehaviour
    {
        private string clipName;
        private float baseVolume = 0.5f;
        private SoundCategory category = SoundCategory.Inimigos;

        /// <summary>Multiplicador de volume em tempo real (0 = mudo).</summary>
        public float Volume { get; set; } = 1f;
        public float Pitch { get; set; } = 1f;

        private AudioSource source;
        private AudioLowPassFilter filter;
        private float nextOcclusionCheck;
        private float occlusion = 1f;

        public static LoopSound Attach(GameObject host, string clip, float volume, float maxDistance = 30f,
            SoundCategory category = SoundCategory.Inimigos, float pitch = 1f)
        {
            var go = new GameObject("Som_" + clip);
            go.transform.SetParent(host.transform, false);
            var loop = go.AddComponent<LoopSound>();
            loop.clipName = clip;
            loop.baseVolume = volume;
            loop.category = category;
            loop.Pitch = pitch;
            loop.Setup(maxDistance);
            return loop;
        }

        private void Setup(float maxDistance)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = Sfx.Clip(clipName);
            source.loop = true;
            source.playOnAwake = false;
            Sfx.Configure3D(source, maxDistance);
            filter = gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 22000f;
            if (source.clip == null) return;
            source.time = Random.Range(0f, source.clip.length); // vários iguais não ficam em fase
            source.volume = 0f;
            source.Play();
        }

        private void Update()
        {
            if (source == null || source.clip == null) return;
            var m = AudioManager.Instance;
            float cat = m != null ? m.VolumeOf(category) : 1f;

            if (Time.time >= nextOcclusionCheck && m != null)
            {
                nextOcclusionCheck = Time.time + 0.25f + Random.value * 0.05f;
                occlusion = m.IsOccluded(transform.position, transform.root) ? 0f : 1f;
            }
            filter.cutoffFrequency = Mathf.Lerp(filter.cutoffFrequency, occlusion > 0.5f ? 22000f : 900f, Time.deltaTime * 6f);
            source.volume = baseVolume * Volume * cat * (occlusion > 0.5f ? 1f : 0.6f);
            source.pitch = Pitch;
        }

        public void Stop()
        {
            if (source != null) source.Stop();
        }

        /// <summary>Solta o som do dono (ex.: carcaça caindo) e some depois de um tempo.</summary>
        public void Detach(float fadeTime)
        {
            transform.SetParent(null, true);
            Destroy(gameObject, fadeTime);
        }
    }
}
