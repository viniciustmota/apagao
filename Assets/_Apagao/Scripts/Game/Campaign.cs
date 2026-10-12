using System;
using Apagao.AI.Hive;
using Apagao.Player;
using UnityEngine;

namespace Apagao.Game
{
    public enum ObjectiveKind { Chegar, Usar, SabotarAntena, Destruir }

    /// <summary>Um objetivo (spec 18 R3): texto, condição, falas e se abre com checkpoint.</summary>
    [Serializable]
    public class Objective
    {
        public string text;
        public ObjectiveKind kind;
        public Transform target;
        [Tooltip("Chegar: distância horizontal até o alvo.")]
        public float radius = 3f;
        [Tooltip("Salva um checkpoint quando este objetivo começa.")]
        public bool checkpoint;
        [Range(0f, 1f)] public float signal = 1f;
        [Tooltip("Ato a que pertence (spec 18). -1 = continua no ato atual.")]
        public int act = -1;
        public DialogueLine[] onStart;
        public DialogueLine[] onComplete;
    }

    /// <summary>
    /// Sequência de objetivos da campanha (spec 18). No marco 11 roda a demo da vila de teste;
    /// os atos completos (ActDefinition) chegam com a ilha inteira.
    /// </summary>
    public class Campaign : MonoBehaviour
    {
        [SerializeField] private Objective[] objectives;
        [SerializeField] private string endTitle = "Fim da demo";
        [SerializeField] private string endSubtitle = "F9 recomeça do zero";

        public string EndTitle => endTitle;
        public string EndSubtitle => endSubtitle;

        public static Campaign Instance { get; private set; }

        public int CurrentIndex { get; private set; }
        public int Count => objectives != null ? objectives.Length : 0;
        public Objective Current => objectives != null && CurrentIndex < objectives.Length ? objectives[CurrentIndex] : null;
        public bool Finished => CurrentIndex >= Count;
        public float ObjectiveStartTime { get; private set; }

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

        private void Start()
        {
            var save = CheckpointManager.Instance != null ? CheckpointManager.Instance.Loaded : null;
            Begin(save != null ? save.objective : 0, fromCheckpoint: save != null);
        }

        private void Begin(int index, bool fromCheckpoint)
        {
            CurrentIndex = index;
            ObjectiveStartTime = Time.time;
            var o = Current;
            if (o == null) return;

            if (ActManager.Instance != null)
            {
                int act = ActFor(index);
                if (act >= 0) ActManager.Instance.Enter(act, announce: true);
            }

            if (RadioSystem.Instance != null)
            {
                RadioSystem.Instance.SetSignal(o.signal);
                if (!fromCheckpoint) RadioSystem.Instance.Play(o.onStart);
            }
            if (o.checkpoint && !fromCheckpoint && CheckpointManager.Instance != null)
                CheckpointManager.Instance.Save();
        }

        /// <summary>O ato do objetivo, ou do último objetivo anterior que tinha ato.</summary>
        private int ActFor(int index)
        {
            for (int i = Mathf.Min(index, Count - 1); i >= 0; i--)
                if (objectives[i].act >= 0) return objectives[i].act;
            return -1;
        }

        private void Update()
        {
            var o = Current;
            if (o == null || !IsDone(o)) return;

            if (RadioSystem.Instance != null) RadioSystem.Instance.Play(o.onComplete);
            Begin(CurrentIndex + 1, fromCheckpoint: false);
        }

        private bool IsDone(Objective o)
        {
            switch (o.kind)
            {
                case ObjectiveKind.Chegar:
                    if (o.target == null || player == null) return false;
                    Vector3 d = o.target.position - player.position;
                    d.y = 0f;
                    return d.magnitude <= o.radius;

                case ObjectiveKind.Usar:
                    var usable = o.target != null ? o.target.GetComponent<UsableObject>() : null;
                    return usable != null && usable.Used;

                case ObjectiveKind.SabotarAntena:
                    var antenna = o.target != null ? o.target.GetComponent<HiveAntenna>() : null;
                    return antenna == null || antenna.Sabotaged; // destruída ou já sabotada no checkpoint

                case ObjectiveKind.Destruir:
                    return o.target == null; // Unity: objeto destruído compara com null

                default:
                    return false;
            }
        }
    }
}
