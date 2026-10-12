using Apagao.Game;
using UnityEngine;

namespace Apagao.UI
{
    /// <summary>
    /// HUD da campanha (spec 20, IMGUI provisório): objetivo atual no topo, legendas do rádio embaixo,
    /// aviso de checkpoint e tela de fim da demo.
    /// </summary>
    public class CampaignHUD : MonoBehaviour
    {
        [SerializeField] private float objectiveHighlight = 4f;
        [SerializeField] private float checkpointToast = 2.5f;

        private GUIStyle objectiveStyle, subtitleStyle, toastStyle, endStyle, endSmallStyle;

        private void OnGUI()
        {
            EnsureStyles();
            GUI.depth = -5;

            var campaign = Campaign.Instance;
            var radio = RadioSystem.Instance;
            var checkpoints = CheckpointManager.Instance;

            if (campaign != null && campaign.Current != null) DrawObjective(campaign);
            if (radio != null && radio.HasLine) DrawSubtitle(radio);
            if (checkpoints != null && Time.time - checkpoints.LastSaveTime < checkpointToast) DrawCheckpoint();
            if (campaign != null && campaign.Finished && campaign.Count > 0 && (radio == null || !radio.Busy)) DrawEnd();
        }

        private void DrawObjective(Campaign campaign)
        {
            // Recém-começado: maior e amarelo; depois fica discreto.
            float age = Time.time - campaign.ObjectiveStartTime;
            bool fresh = age < objectiveHighlight;
            string color = fresh ? "#ffd75a" : "#e8e8e8";
            string text = $"<color={color}><b>Objetivo</b>  {campaign.Current.text}</color>";
            objectiveStyle.fontSize = fresh ? 20 : 16;
            var rect = new Rect(0, 18, Screen.width, 30);
            Shadowed(rect, text, objectiveStyle);
        }

        private void DrawSubtitle(RadioSystem radio)
        {
            string speakerColor = radio.CurrentSpeaker == RadioSystem.Caio ? "#f0a050" : "#7fd0ff";
            string tag = radio.CurrentSpeaker == RadioSystem.Lia ? $"  <color=#8a8a8a>[rádio {radio.Signal:P0}]</color>" : "";
            string text = $"<color={speakerColor}><b>{radio.CurrentSpeaker}</b></color>{tag}\n{radio.CurrentText}";

            float w = Mathf.Min(900f, Screen.width - 80f);
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.74f, w, 70);
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, text, subtitleStyle);
        }

        private void DrawCheckpoint()
        {
            var rect = new Rect(Screen.width - 260, 18, 236, 26);
            Shadowed(rect, "<color=#9fe39f>Checkpoint salvo</color>", toastStyle);
        }

        private void DrawEnd()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 60), "Fim da demo", endStyle);
            GUI.Label(new Rect(0, Screen.height * 0.4f + 64, Screen.width, 30), "F9 recomeça do zero", endSmallStyle);
        }

        private static void Shadowed(Rect rect, string text, GUIStyle style)
        {
            var shadow = new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(shadow, StripColor(text), style);
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
        }

        private static string StripColor(string text) =>
            System.Text.RegularExpressions.Regex.Replace(text, "<color=[^>]*>|</color>", "");

        private void EnsureStyles()
        {
            if (objectiveStyle != null) return;
            objectiveStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, alignment = TextAnchor.MiddleCenter };
            subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            toastStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, alignment = TextAnchor.MiddleRight };
            endStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
            endSmallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        }
    }
}
