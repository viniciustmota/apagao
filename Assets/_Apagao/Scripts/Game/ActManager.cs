using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apagao.Game
{
    /// <summary>Luz fixa de um ato (spec 22 R7: dia e noite por ato, não em tempo real).</summary>
    [Serializable]
    public class LightingPreset
    {
        public Vector3 sunEuler = new Vector3(50f, -30f, 0f);
        public Color sunColor = new Color(1f, 0.95f, 0.85f);
        public float sunIntensity = 1.3f;
        public Color ambient = new Color(0.55f, 0.6f, 0.65f);
        public Color fog = new Color(0.70f, 0.80f, 0.88f);
        public float fogEnd = 450f;
    }

    /// <summary>Um ato (spec 18 R2): nome, grupos de inimigos e objetos que entram em cena, e a luz.</summary>
    [Serializable]
    public class ActDefinition
    {
        public string title;
        [Tooltip("Ligados quando o jogo chega neste ato (e continuam ligados nos atos seguintes).")]
        public GameObject[] groups;
        public LightingPreset lighting = new LightingPreset();
    }

    /// <summary>
    /// Troca de ato: liga os grupos do ato (e dos anteriores), desliga os futuros e aplica a luz.
    /// Quem chama é o Campaign, ao começar um objetivo que pertence a outro ato.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class ActManager : MonoBehaviour
    {
        [SerializeField] private Light sun;
        [SerializeField] private ActDefinition[] acts;

        public static ActManager Instance { get; private set; }

        public int Current { get; private set; } = -1;
        public string CurrentTitle => acts != null && Current >= 0 && Current < acts.Length ? acts[Current].title : null;
        public float LastChangeTime { get; private set; } = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            Instance = this;
            SetGroups(0); // até o Campaign decidir, só o primeiro ato existe
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Enter(int act, bool announce = true)
        {
            if (acts == null || acts.Length == 0) return;
            act = Mathf.Clamp(act, 0, acts.Length - 1);
            if (act == Current) return;
            Current = act;
            SetGroups(act);
            Apply(acts[act].lighting);
            if (announce) LastChangeTime = Time.time;
        }

        private void SetGroups(int act)
        {
            if (acts == null) return;
            for (int i = 0; i < acts.Length; i++)
            {
                if (acts[i].groups == null) continue;
                foreach (var g in acts[i].groups)
                    if (g != null && g.activeSelf != (i <= act)) g.SetActive(i <= act);
            }
        }

        private void Apply(LightingPreset p)
        {
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(p.sunEuler);
                sun.color = p.sunColor;
                sun.intensity = p.sunIntensity;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = p.ambient;
            RenderSettings.fogColor = p.fog;
            RenderSettings.fogEndDistance = p.fogEnd;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor; // céu na cor da névoa: noite fica escura de verdade
                cam.backgroundColor = p.fog;
            }
        }
    }
}
