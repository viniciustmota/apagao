using UnityEngine;

namespace Apagao.Systems
{
    /// <summary>Opções do jogador, salvas no PlayerPrefs: volume, música, sensibilidade e inversão do mouse.</summary>
    public static class GameSettings
    {
        private const string MasterKey = "apagao.volume";
        private const string MusicKey = "apagao.musica";
        private const string SensitivityKey = "apagao.sensibilidade";
        private const string InvertKey = "apagao.inverterY";

        // Lidos uma vez e guardados: o áudio consulta o volume em todo frame.
        private static bool loaded;
        private static float master = 1f, music = 1f, sensitivity = 1f;
        private static bool invertY;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => loaded = false;

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            master = PlayerPrefs.GetFloat(MasterKey, 1f);
            music = PlayerPrefs.GetFloat(MusicKey, 1f);
            sensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
            invertY = PlayerPrefs.GetInt(InvertKey, 0) == 1;
        }

        public static float MasterVolume
        {
            get { Load(); return master; }
            set { Load(); master = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MasterKey, master); }
        }

        public static float MusicVolume
        {
            get { Load(); return music; }
            set { Load(); music = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicKey, music); }
        }

        /// <summary>Multiplicador da sensibilidade do mouse (1 = padrão).</summary>
        public static float Sensitivity
        {
            get { Load(); return sensitivity; }
            set { Load(); sensitivity = Mathf.Clamp(value, 0.2f, 3f); PlayerPrefs.SetFloat(SensitivityKey, sensitivity); }
        }

        public static bool InvertY
        {
            get { Load(); return invertY; }
            set { Load(); invertY = value; PlayerPrefs.SetInt(InvertKey, value ? 1 : 0); }
        }

        public static void Save() => PlayerPrefs.Save();
    }

    /// <summary>Estado de pausa global: a câmera e outros sistemas consultam para ignorar o mouse.</summary>
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => IsPaused = false;

        public static void Set(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }

        /// <summary>Antes de trocar de cena: tudo volta ao normal (sem prender o mouse).</summary>
        public static void Clear()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
