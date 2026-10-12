using Apagao.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Apagao.EditorTools
{
    /// <summary>Cena do menu principal: câmera escura com névoa, ambiente sonoro e o MainMenu (IMGUI).</summary>
    public static class MenuBuilder
    {
        public const string ScenePath = IlhaTesteBuilder.Root + "/Scenes/Menu_Principal.unity";

        [MenuItem("Apagão/Criar Menu Principal")]
        public static void BuildFromMenu() => Build(openScene: true);

        public static void Build(bool openScene)
        {
            if (openScene && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            IlhaTesteBuilder.EnsureFolders();
            var previous = EditorSceneManager.GetActiveScene().path;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, openScene ? NewSceneMode.Single : NewSceneMode.Additive);
            if (!openScene) EditorSceneManager.SetActiveScene(scene);

            var camGo = new GameObject("Camera_Menu") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.07f);
            camGo.AddComponent<AudioListener>();

            new GameObject("Menu").AddComponent<MainMenu>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!openScene)
            {
                EditorSceneManager.CloseScene(scene, true);
                if (!string.IsNullOrEmpty(previous)) EditorSceneManager.SetActiveScene(EditorSceneManager.GetSceneByPath(previous));
            }
            BuildTools.SyncBuildScenes();
            Debug.Log("[Apagão] Menu_Principal criado.");
        }
    }
}
