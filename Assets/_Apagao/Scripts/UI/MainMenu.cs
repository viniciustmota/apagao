using System.IO;
using Apagao.Game;
using Apagao.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Apagao.UI
{
    /// <summary>Tela de título: continuar a campanha, jogo novo, laboratório, opções e sair.</summary>
    public class MainMenu : MonoBehaviour
    {
        public const string CampaignScene = "Ilha_Amparo";
        public const string LabScene = "Ilha_Teste";

        private bool options;
        private bool confirmNew;
        private bool hasSave;

        private void Start()
        {
            GamePause.Clear();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            hasSave = File.Exists(CheckpointManager.PathFor(CampaignScene));
        }

        private void OnGUI()
        {
            MenuStyle.Ensure();
            // Faixa vermelha piscando ao fundo: a VIGÍLIA está online.
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f);
            GUI.color = new Color(0.9f, 0.1f, 0.05f, 0.08f + 0.06f * blink);
            GUI.DrawTexture(new Rect(0, Screen.height * 0.08f, Screen.width, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(0, Screen.height * 0.12f, Screen.width, 90f), "APAGÃO", MenuStyle.Title);
            GUI.Label(new Rect(0, Screen.height * 0.12f + 84f, Screen.width, 32f), "Ilha do Amparo", MenuStyle.Subtitle);

            float top = Screen.height * 0.36f;
            if (options)
            {
                if (MenuStyle.Options(top)) options = false;
                return;
            }

            GUILayout.BeginArea(MenuStyle.Column(top));
            if (confirmNew)
            {
                GUILayout.Label("Começar de novo apaga o progresso salvo. Tem certeza?", MenuStyle.Small);
                GUILayout.Space(8f);
                if (GUILayout.Button("Sim, jogo novo", MenuStyle.Button)) NewGame();
                GUILayout.Space(8f);
                if (GUILayout.Button("Cancelar", MenuStyle.Button)) confirmNew = false;
                GUILayout.EndArea();
                return;
            }

            if (hasSave && GUILayout.Button("Continuar", MenuStyle.Button)) Load(CampaignScene);
            GUILayout.Space(8f);
            if (GUILayout.Button("Jogo novo", MenuStyle.Button))
            {
                if (hasSave) confirmNew = true;
                else NewGame();
            }
            GUILayout.Space(8f);
            if (Application.CanStreamedLevelBeLoaded(LabScene) && GUILayout.Button("Laboratório (tudo liberado)", MenuStyle.Button))
                Load(LabScene);
            GUILayout.Space(8f);
            if (GUILayout.Button("Opções", MenuStyle.Button)) options = true;
            GUILayout.Space(8f);
            if (GUILayout.Button("Sair", MenuStyle.Button)) PauseMenu.Quit();
            GUILayout.EndArea();

            GUI.Label(new Rect(0, Screen.height - 40f, Screen.width, 24f),
                "WASD andar · Shift correr · C agachar · Espaço pular/escalar · E interagir · Esc pausa", MenuStyle.Small);
        }

        private static void NewGame()
        {
            CheckpointManager.DeleteSave(CampaignScene);
            Load(CampaignScene);
        }

        private static void Load(string scene)
        {
            GamePause.Clear();
            SceneManager.LoadScene(scene);
        }
    }
}
