using System;
using System.Collections.Generic;
using System.Text;
using Apagao.Player;
using Apagao.Systems;
using UnityEngine;

namespace Apagao.Game
{
    /// <summary>Uma fala (spec 19 R1). Áudio fica para o marco 12; por enquanto só legenda.</summary>
    [Serializable]
    public class DialogueLine
    {
        public string speaker;
        [TextArea] public string text;
        public float duration = 3.5f;

        public DialogueLine() { }

        public DialogueLine(string speaker, string text, float duration = 3.5f)
        {
            this.speaker = speaker;
            this.text = text;
            this.duration = duration;
        }
    }

    /// <summary>
    /// Rádio e legendas (spec 19): fila de falas, sinal que piora (cortes "…" na fala da Lia)
    /// e ruído baixo quando o Caio fala (R7).
    /// </summary>
    public class RadioSystem : MonoBehaviour
    {
        public const string Lia = "Lia";
        public const string Caio = "Caio";

        [Tooltip("Qualidade do sinal: 1 = limpo, 0 = quase só chiado.")]
        [SerializeField, Range(0f, 1f)] private float signal = 1f;
        [SerializeField] private float gapBetweenLines = 0.4f;

        public static RadioSystem Instance { get; private set; }

        public string CurrentSpeaker { get; private set; }
        public string CurrentText { get; private set; }
        public bool HasLine => CurrentText != null;
        public bool Busy => HasLine || queue.Count > 0;
        public float Signal => signal;

        private readonly Queue<DialogueLine> queue = new Queue<DialogueLine>();
        private float lineEnds;
        private float nextLineAt;
        private Transform player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            Instance = this;
            var pc = FindAnyObjectByType<PlayerController>();
            player = pc != null ? pc.transform : null;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetSignal(float value) => signal = Mathf.Clamp01(value);

        public void Play(IEnumerable<DialogueLine> lines)
        {
            if (lines == null) return;
            foreach (var l in lines)
                if (l != null && !string.IsNullOrEmpty(l.text)) queue.Enqueue(l);
        }

        private void Update()
        {
            if (HasLine && Time.time >= lineEnds)
            {
                CurrentText = null;
                CurrentSpeaker = null;
                nextLineAt = Time.time + gapBetweenLines;
            }
            if (HasLine || queue.Count == 0 || Time.time < nextLineAt) return;

            var line = queue.Dequeue();
            CurrentSpeaker = line.speaker;
            CurrentText = line.speaker == Lia ? Garble(line.text, signal) : line.text;
            lineEnds = Time.time + Mathf.Max(1f, line.duration);

            if (line.speaker == Caio && player != null)
                NoiseSystem.Emit(player.position, 2f, 0.2f, NoiseType.Distracao, player.gameObject);
        }

        /// <summary>R4: quanto pior o sinal, mais palavras viram "…".</summary>
        private static string Garble(string text, float quality)
        {
            float cut = (1f - quality) * 0.6f;
            if (cut <= 0.01f) return text;

            var sb = new StringBuilder();
            bool lastWasCut = false;
            foreach (var word in text.Split(' '))
            {
                bool drop = UnityEngine.Random.value < cut;
                if (drop)
                {
                    if (!lastWasCut) sb.Append("… ");
                }
                else sb.Append(word).Append(' ');
                lastWasCut = drop;
            }
            return sb.ToString().TrimEnd();
        }
    }
}
