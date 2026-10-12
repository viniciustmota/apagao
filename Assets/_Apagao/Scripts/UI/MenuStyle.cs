using Apagao.Systems;
using UnityEngine;

namespace Apagao.UI
{
    /// <summary>Estilo comum dos menus (IMGUI provisório, spec 20) e o painel de opções.</summary>
    public static class MenuStyle
    {
        public static GUIStyle Title, Subtitle, Button, Label, Small, Toggle;
        private static Texture2D buttonBg, buttonHover;

        public static void Ensure()
        {
            if (Title != null) return;
            buttonBg = Solid(new Color(0.08f, 0.09f, 0.11f, 0.85f));
            buttonHover = Solid(new Color(0.75f, 0.22f, 0.12f, 0.95f));

            Title = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Title.normal.textColor = new Color(0.95f, 0.92f, 0.85f);
            Subtitle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            Subtitle.normal.textColor = new Color(0.95f, 0.55f, 0.3f);
            Label = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            Label.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            Toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 18 };
            Toggle.normal.textColor = Toggle.onNormal.textColor = new Color(0.9f, 0.9f, 0.9f);
            Small = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, richText = true };
            Small.normal.textColor = new Color(0.65f, 0.65f, 0.7f);

            Button = new GUIStyle(GUI.skin.button) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fixedHeight = 46f };
            Button.normal.background = buttonBg;
            Button.hover.background = buttonHover;
            Button.active.background = buttonHover;
            Button.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
            Button.hover.textColor = Color.white;
            Button.active.textColor = Color.white;
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        public static void Darken(float alpha)
        {
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>Área central de botões (largura fixa).</summary>
        public static Rect Column(float top, float width = 360f, float height = 420f) =>
            new Rect((Screen.width - width) * 0.5f, top, width, height);

        /// <summary>Painel de opções. Retorna true quando o jogador clica em Voltar.</summary>
        public static bool Options(float top)
        {
            GUILayout.BeginArea(Column(top, 460f, 360f));
            GUILayout.Label($"Volume geral: {GameSettings.MasterVolume:P0}", Label);
            GameSettings.MasterVolume = GUILayout.HorizontalSlider(GameSettings.MasterVolume, 0f, 1f);
            GUILayout.Space(10f);
            GUILayout.Label($"Música: {GameSettings.MusicVolume:P0}", Label);
            GameSettings.MusicVolume = GUILayout.HorizontalSlider(GameSettings.MusicVolume, 0f, 1f);
            GUILayout.Space(10f);
            GUILayout.Label($"Sensibilidade do mouse: {GameSettings.Sensitivity:0.0}x", Label);
            GameSettings.Sensitivity = GUILayout.HorizontalSlider(GameSettings.Sensitivity, 0.2f, 3f);
            GUILayout.Space(10f);
            GameSettings.InvertY = GUILayout.Toggle(GameSettings.InvertY, "  Inverter o mouse no eixo vertical", Toggle);
            GUILayout.Space(20f);
            bool back = GUILayout.Button("Voltar", Button);
            GUILayout.EndArea();
            if (back) GameSettings.Save();
            return back;
        }
    }
}
