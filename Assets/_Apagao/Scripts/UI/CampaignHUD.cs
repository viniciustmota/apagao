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

        [Header("Orientação")]
        [SerializeField] private bool showWaypoint = true;
        [SerializeField] private bool showCompass = true;
        [SerializeField] private float compassWidth = 360f;

        private GUIStyle objectiveStyle, subtitleStyle, toastStyle, endStyle, endSmallStyle, markStyle, compassStyle;
        private Transform player;
        private static readonly Color MarkColor = new Color(1f, 0.82f, 0.25f);
        private static readonly (string label, float heading)[] Cardinals =
        {
            ("N", 0f), ("NE", 45f), ("L", 90f), ("SE", 135f), ("S", 180f), ("SO", 225f), ("O", 270f), ("NO", 315f)
        };

        private void OnGUI()
        {
            EnsureStyles();
            GUI.depth = -5;

            var campaign = Campaign.Instance;
            var radio = RadioSystem.Instance;
            var checkpoints = CheckpointManager.Instance;

            if (campaign != null && campaign.Current != null)
            {
                DrawObjective(campaign);
                Vector3? target = ObjectiveTarget(campaign);
                if (showCompass) DrawCompass(target);
                if (showWaypoint && target.HasValue) DrawWaypoint(target.Value);
            }
            DrawActTitle();
            if (radio != null && radio.HasLine) DrawSubtitle(radio);
            if (checkpoints != null && Time.time - checkpoints.LastSaveTime < checkpointToast) DrawCheckpoint();
            if (campaign != null && campaign.Finished && campaign.Count > 0 && (radio == null || !radio.Busy)) DrawEnd(campaign);
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

        // ---------- Orientação: marcador do objetivo e bússola ----------

        private Vector3? ObjectiveTarget(Campaign campaign)
        {
            var t = campaign.Current.target;
            if (t == null) return null; // alvo já destruído/coletado
            if (player == null)
            {
                var pc = FindAnyObjectByType<Apagao.Player.PlayerController>();
                if (pc != null) player = pc.transform;
            }
            return t.position + Vector3.up * 1.6f;
        }

        /// <summary>Losango amarelo sobre o objetivo, com a distância. Fora da tela, gruda na borda.</summary>
        private void DrawWaypoint(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null || player == null) return;
            float dist = Vector3.Distance(player.position, world);
            if (dist < 3f) return;

            Vector3 sp = cam.WorldToScreenPoint(world);
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var dir = new Vector2(sp.x, sp.y) - center;
            bool behind = sp.z < 0f;
            if (behind) dir = -dir;

            const float margin = 48f;
            float hx = center.x - margin, hy = center.y - margin;
            bool outside = behind || Mathf.Abs(dir.x) > hx || Mathf.Abs(dir.y) > hy;
            if (outside)
            {
                if (dir.sqrMagnitude < 1f) dir = Vector2.down; // bem atrás: aponta para baixo
                float k = Mathf.Min(hx / Mathf.Max(Mathf.Abs(dir.x), 0.001f), hy / Mathf.Max(Mathf.Abs(dir.y), 0.001f));
                dir *= k;
            }
            var pos = new Vector2(center.x + dir.x, Screen.height - (center.y + dir.y)); // GUI tem y para baixo

            // Pisca de leve para chamar atenção sem poluir.
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
            Diamond(pos, outside ? 12f : 14f, new Color(MarkColor.r, MarkColor.g, MarkColor.b, pulse));
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.Label(new Rect(pos.x - 40f, pos.y + 12f, 80f, 20f), $"{dist:0} m", markStyle);
            GUI.color = Color.white;
        }

        /// <summary>Faixa de bússola no topo: pontos cardeais pela direção da câmera e o objetivo marcado.</summary>
        private void DrawCompass(Vector3? target)
        {
            var cam = Camera.main;
            if (cam == null) return;
            float yaw = cam.transform.eulerAngles.y;
            float w = compassWidth, x0 = (Screen.width - w) * 0.5f, y = 54f;

            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(x0, y, w, 22f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - 1f, y, 2f, 22f), Texture2D.whiteTexture);

            for (int h = 0; h < 360; h += 15)
            {
                float d = Mathf.DeltaAngle(yaw, h);
                if (Mathf.Abs(d) > 90f) continue;
                float x = Screen.width * 0.5f + d / 90f * w * 0.5f;
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                GUI.DrawTexture(new Rect(x, y + 16f, 1f, 6f), Texture2D.whiteTexture);
            }
            foreach (var (label, heading) in Cardinals)
            {
                float d = Mathf.DeltaAngle(yaw, heading);
                if (Mathf.Abs(d) > 90f) continue;
                float x = Screen.width * 0.5f + d / 90f * w * 0.5f;
                GUI.color = new Color(1f, 1f, 1f, 1f - Mathf.Abs(d) / 110f);
                GUI.Label(new Rect(x - 20f, y, 40f, 18f), label, compassStyle);
            }

            if (target.HasValue && player != null)
            {
                Vector3 to = target.Value - player.position;
                float heading = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float d = Mathf.Clamp(Mathf.DeltaAngle(yaw, heading), -90f, 90f);
                float x = Screen.width * 0.5f + d / 90f * w * 0.5f;
                Diamond(new Vector2(x, y + 11f), 9f, MarkColor);
            }
            GUI.color = Color.white;
        }

        private static void Diamond(Vector2 center, float size, Color color)
        {
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, center);
            GUI.color = new Color(0f, 0f, 0f, color.a * 0.6f);
            GUI.DrawTexture(new Rect(center.x - size * 0.5f - 1.5f, center.y - size * 0.5f - 1.5f, size + 3f, size + 3f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        /// <summary>Cartela do ato ("Ato 1 — A Roça") ao entrar num ato novo.</summary>
        private void DrawActTitle()
        {
            var acts = ActManager.Instance;
            if (acts == null || acts.CurrentTitle == null) return;
            float age = Time.time - acts.LastChangeTime;
            if (age < 0f || age > 4.5f) return;
            float a = Mathf.Clamp01(age / 0.6f) * Mathf.Clamp01((4.5f - age) / 1f);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 60), acts.CurrentTitle, endStyle);
            GUI.color = Color.white;
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

        private void DrawEnd(Campaign campaign)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 60), campaign.EndTitle, endStyle);
            GUI.Label(new Rect(0, Screen.height * 0.4f + 64, Screen.width, 30), campaign.EndSubtitle, endSmallStyle);
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
            markStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
            compassStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
    }
}
