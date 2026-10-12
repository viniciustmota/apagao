using System.Collections.Generic;
using UnityEngine;

namespace Apagao.Audio
{
    /// <summary>Categorias de volume (spec 21 R1). Sem AudioMixer por enquanto: cada uma é um multiplicador.</summary>
    public enum SoundCategory { Ambiente, Efeitos, Inimigos, Vozes, Musica, Radio }

    /// <summary>
    /// Atalho para tocar sons de qualquer lugar. Os clipes ficam em Resources/Audio e são achados pelo nome.
    /// Sem AudioManager na cena, as chamadas não fazem nada.
    /// </summary>
    public static class Sfx
    {
        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        private static readonly HashSet<string> missing = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cache.Clear();
            missing.Clear();
        }

        public static AudioClip Clip(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (cache.TryGetValue(name, out var clip)) return clip;
            clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip == null && missing.Add(name)) Debug.LogWarning($"[Apagão] Som não encontrado: Resources/Audio/{name}");
            cache[name] = clip;
            return clip;
        }

        /// <summary>Som 3D no mundo.</summary>
        public static void Play(string name, Vector3 position, float volume = 1f, SoundCategory category = SoundCategory.Efeitos,
            float maxDistance = 40f, float pitchVariance = 0.06f, float pitch = 1f)
        {
            var m = AudioManager.Ensure();
            if (m != null) m.PlayAt(Clip(name), position, volume, category, maxDistance, pitch * (1f + Random.Range(-pitchVariance, pitchVariance)));
        }

        /// <summary>Som sem posição (interface, o próprio Caio).</summary>
        public static void Play2D(string name, float volume = 1f, SoundCategory category = SoundCategory.Efeitos, float pitchVariance = 0.04f)
        {
            var m = AudioManager.Ensure();
            if (m != null) m.Play2D(Clip(name), volume, category, 1f + Random.Range(-pitchVariance, pitchVariance));
        }
    }
}
