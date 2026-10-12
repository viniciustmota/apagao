using System.Collections.Generic;
using Apagao.AI;
using Apagao.AI.Drones;
using Apagao.Game;
using UnityEngine;

namespace Apagao.Audio
{
    /// <summary>
    /// Áudio do jogo (spec 21): pool de fontes 3D com abafamento atrás de paredes, volumes por categoria,
    /// ambiente (mar, vento, cigarras que calam perto das máquinas), música por nível de alerta e chiado do rádio.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class AudioManager : MonoBehaviour
    {
        [Header("Volumes (R1)")]
        [SerializeField, Range(0f, 1f)] private float master = 1f;
        [SerializeField, Range(0f, 1f)] private float ambiente = 0.7f;
        [SerializeField, Range(0f, 1f)] private float efeitos = 1f;
        [SerializeField, Range(0f, 1f)] private float inimigos = 1f;
        [SerializeField, Range(0f, 1f)] private float vozes = 1f;
        [SerializeField, Range(0f, 1f)] private float musica = 0.55f;
        [SerializeField, Range(0f, 1f)] private float radio = 0.6f;

        [Header("Fontes")]
        [SerializeField] private int poolSize = 32;

        [Header("Cigarras (R3)")]
        [Tooltip("Abaixo desta distância de uma máquina, as cigarras calam.")]
        [SerializeField] private float cicadaSilenceRadius = 15f;
        [SerializeField] private float cicadaFadeDistance = 10f;

        [Header("Música (R4)")]
        [SerializeField] private float musicRadius = 50f;
        [Tooltip("Segundos que o combate continua tocando depois do último robô em combate.")]
        [SerializeField] private float combatLinger = 4f;

        public static AudioManager Instance { get; private set; }

        private readonly List<AudioSource> pool = new List<AudioSource>();
        private readonly List<AudioLowPassFilter> filters = new List<AudioLowPassFilter>();
        private readonly RaycastHit[] hits = new RaycastHit[8];
        private int nextSource;
        private Transform listener;

        private AudioSource sea, wind, cicadas, tension, combat, staticNoise;
        private float cicadaLevel = 1f, tensionLevel, combatLevel;
        private float lastCombatTime = float.NegativeInfinity;
        private string lastRadioText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        /// <summary>Cria o gerenciador ao abrir o jogo, em qualquer cena: não depende do builder.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        /// <summary>Garante um AudioManager vivo (persiste entre recargas de cena).</summary>
        public static AudioManager Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            return go.AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this); // cena antiga com outro AudioManager: fica o persistente
                return;
            }
            Instance = this;
            Debug.Log("[Apagão] Áudio iniciado.");
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("Fonte_" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.dopplerLevel = 0f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                pool.Add(s);
                filters.Add(go.AddComponent<AudioLowPassFilter>());
            }

            sea = Bed("mar_loop");
            wind = Bed("vento_loop");
            cicadas = Bed("cigarras_loop");
            tension = Bed("musica_tensao_loop");
            combat = Bed("musica_combate_loop");
            staticNoise = Bed("radio_estatica_loop");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>O ouvinte muda quando a cena recarrega (morte, checkpoint): procura de novo se sumiu.</summary>
        private void FindListener()
        {
            if (listener != null) return;
            var l = FindAnyObjectByType<AudioListener>();
            listener = l != null ? l.transform : null;
        }

        public float VolumeOf(SoundCategory c) => master * c switch
        {
            SoundCategory.Ambiente => ambiente,
            SoundCategory.Inimigos => inimigos,
            SoundCategory.Vozes => vozes,
            SoundCategory.Musica => musica,
            SoundCategory.Radio => radio,
            _ => efeitos
        };

        // ---------- Efeitos ----------

        public void PlayAt(AudioClip clip, Vector3 position, float volume, SoundCategory category, float maxDistance, float pitch)
        {
            if (clip == null) return;
            int i = NextFree();
            var s = pool[i];
            bool occluded = IsOccluded(position, null);
            s.transform.position = position;
            s.clip = clip;
            s.spatialBlend = 1f;
            s.minDistance = Mathf.Max(1f, maxDistance * 0.08f);
            s.maxDistance = maxDistance;
            s.volume = volume * VolumeOf(category) * (occluded ? 0.6f : 1f);
            s.pitch = pitch;
            filters[i].cutoffFrequency = occluded ? 900f : 22000f; // R6: atrás de parede soa abafado
            s.Play();
        }

        public void Play2D(AudioClip clip, float volume, SoundCategory category, float pitch)
        {
            if (clip == null) return;
            int i = NextFree();
            var s = pool[i];
            s.clip = clip;
            s.spatialBlend = 0f;
            s.volume = volume * VolumeOf(category);
            s.pitch = pitch;
            filters[i].cutoffFrequency = 22000f;
            s.Play();
        }

        private int NextFree()
        {
            for (int k = 0; k < pool.Count; k++)
            {
                int i = (nextSource + k) % pool.Count;
                if (!pool[i].isPlaying)
                {
                    nextSource = (i + 1) % pool.Count;
                    return i;
                }
            }
            int oldest = nextSource; // tudo ocupado: rouba a próxima da fila
            nextSource = (nextSource + 1) % pool.Count;
            return oldest;
        }

        /// <summary>Há algo sólido entre o ouvinte e o ponto (ignora o próprio dono do som e o Caio).</summary>
        public bool IsOccluded(Vector3 point, Transform owner)
        {
            if (listener == null) return false;
            Vector3 from = listener.position;
            Vector3 dir = point - from;
            float dist = dir.magnitude;
            if (dist < 1f) return false;
            int count = Physics.RaycastNonAlloc(from, dir / dist, hits, dist - 0.5f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var t = hits[i].collider.transform;
                if (owner != null && t.IsChildOf(owner)) continue;
                if (hits[i].collider is CharacterController) continue; // o Caio
                if (hits[i].collider is TerrainCollider) continue;   // morros não abafam no greybox
                return true;
            }
            return false;
        }

        // ---------- Ambiente, música e rádio ----------

        private AudioSource Bed(string clip)
        {
            var go = new GameObject("Leito_" + clip);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = Sfx.Clip(clip);
            s.loop = true;
            s.spatialBlend = 0f;
            s.playOnAwake = false;
            s.volume = 0f;
            if (s.clip != null)
            {
                s.time = Random.Range(0f, s.clip.length);
                s.Play();
            }
            return s;
        }

        private void Update()
        {
            FindListener();
            Vector3 center = listener != null ? listener.position : Vector3.zero;
            ScanMachines(center, musicRadius, out float nearest, out bool tense, out bool fighting);
            float dt = Time.deltaTime;

            // R3: as cigarras calam perto das máquinas — o silêncio avisa.
            float cicadaTarget = Mathf.Clamp01((nearest - cicadaSilenceRadius) / cicadaFadeDistance);
            cicadaLevel = Mathf.MoveTowards(cicadaLevel, cicadaTarget, dt * 0.8f);

            // R4: exploração → tensão → combate.
            if (fighting) lastCombatTime = Time.time;
            bool combatOn = Time.time - lastCombatTime < combatLinger;
            combatLevel = Mathf.MoveTowards(combatLevel, combatOn ? 1f : 0f, dt * (combatOn ? 1.5f : 0.3f));
            tensionLevel = Mathf.MoveTowards(tensionLevel, tense && !combatOn ? 1f : 0f, dt * 0.4f);

            float amb = VolumeOf(SoundCategory.Ambiente);
            float mus = VolumeOf(SoundCategory.Musica);
            sea.volume = 0.35f * amb;
            wind.volume = 0.25f * amb;
            cicadas.volume = 0.3f * cicadaLevel * amb;
            tension.volume = 0.6f * tensionLevel * mus;
            combat.volume = 0.7f * combatLevel * mus;

            UpdateRadio(dt);
        }

        private static void ScanMachines(Vector3 center, float radius, out float nearest, out bool tense, out bool fighting)
        {
            nearest = float.MaxValue;
            tense = fighting = false;
            foreach (var m in MachineAI.Active)
            {
                if (m == null || m.IsDead) continue;
                float d = Vector3.Distance(center, m.transform.position);
                nearest = Mathf.Min(nearest, d);
                if (d > radius) continue;
                if (m.Alert == AlertLevel.Combate) fighting = true;
                else if (m.Alert != AlertLevel.Nenhum) tense = true;
            }
            foreach (var dr in DroneAI.Active)
            {
                if (dr == null || dr.IsDead) continue;
                float d = Vector3.Distance(center, dr.transform.position);
                nearest = Mathf.Min(nearest, d);
                if (d <= radius && dr.IsAware) tense = true;
            }
        }

        private void UpdateRadio(float dt)
        {
            var r = RadioSystem.Instance;
            bool liaTalking = r != null && r.HasLine && r.CurrentSpeaker != null && r.CurrentSpeaker.StartsWith(RadioSystem.Lia);
            if (r != null && r.HasLine && r.CurrentText != lastRadioText && liaTalking)
                Sfx.Play2D("radio_bip", 0.8f, SoundCategory.Radio, 0f);
            lastRadioText = r != null ? r.CurrentText : null;

            // R5: chiado cresce com o sinal ruim. A "Lia" do final vem limpa demais.
            float target = 0f;
            if (liaTalking) target = r.CurrentSpeaker == RadioSystem.Lia ? 0.15f + (1f - r.Signal) * 0.5f : 0.03f;
            staticNoise.volume = Mathf.MoveTowards(staticNoise.volume, target * VolumeOf(SoundCategory.Radio), dt * 2f);
        }
    }
}
