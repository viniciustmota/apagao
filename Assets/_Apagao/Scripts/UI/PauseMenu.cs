using Apagao.Player;
using Apagao.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Apagao.UI
{
    /// <summary>Menu de pausa (Esc): continuar, voltar ao checkpoint, opções, menu principal e sair.</summary>
    public class PauseMenu : MonoBehaviour
    {
        public const string MainMenuScene = "Menu_Principal";

        private bool open;
        private bool options;
        private PlayerController player;
        private bool lockedBefore;

        private void Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            GamePause.Clear();
        }

        private void OnDestroy() => GamePause.Clear();

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (options) options = false;
            else SetOpen(!open);
        }

        private void SetOpen(bool value)
        {
            open = value;
            options = false;
            if (player != null)
            {
                if (value) lockedBefore = player.InputLocked;
                player.InputLocked = value || lockedBefore; // nada de golpe ou arremesso com o menu aberto
            }
            GamePause.Set(value);
        }

        private void OnGUI()
        {
            if (!open) return;
            MenuStyle.Ensure();
            GUI.depth = -20;
            MenuStyle.Darken(0.6f);
            GUI.Label(new Rect(0, Screen.height * 0.12f, Screen.width, 80f), "Pausa", MenuStyle.Title);

            if (options)
            {
                if (MenuStyle.Options(Screen.height * 0.3f)) options = false;
                return;
            }

            GUILayout.BeginArea(MenuStyle.Column(Screen.height * 0.3f));
            if (GUILayout.Button("Continuar", MenuStyle.Button)) SetOpen(false);
            GUILayout.Space(8f);
            if (GUILayout.Button("Voltar ao último checkpoint", MenuStyle.Button)) Load(SceneManager.GetActiveScene().name);
            GUILayout.Space(8f);
            if (GUILayout.Button("Opções", MenuStyle.Button)) options = true;
            GUILayout.Space(8f);
            if (Application.CanStreamedLevelBeLoaded(MainMenuScene) && GUILayout.Button("Menu principal", MenuStyle.Button))
                Load(MainMenuScene);
            GUILayout.Space(8f);
            if (GUILayout.Button("Sair do jogo", MenuStyle.Button)) Quit();
            GUILayout.EndArea();
        }

        private static void Load(string scene)
        {
            GamePause.Clear();
            SceneManager.LoadScene(scene);
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
