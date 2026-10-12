using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Apagao.UI;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Apagao.EditorTools
{
    /// <summary>
    /// Build para outras pessoas testarem (marco 13): menu Apagão → Build. Gera a pasta do jogo,
    /// copia o guia de teste e compacta num .zip pronto para mandar.
    /// </summary>
    public static class BuildTools
    {
        private const string Scenes = IlhaTesteBuilder.Root + "/Scenes/";
        private static readonly string[] Order = { "Menu_Principal", "Ilha_Amparo", "Ilha_Teste" };

        /// <summary>Ordem das cenas no build: menu primeiro, depois campanha e laboratório (as que existirem).</summary>
        public static void SyncBuildScenes()
        {
            var list = Order.Select(n => Scenes + n + ".unity").Where(File.Exists)
                .Select(p => new EditorBuildSettingsScene(p, true)).ToList();
            // Mantém cenas extras que alguém tenha colocado à mão, depois das nossas.
            foreach (var s in EditorBuildSettings.scenes)
                if (!list.Any(l => l.path == s.path) && File.Exists(s.path)) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }

        [MenuItem("Apagão/Build/Linux", priority = 100)]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Linux", "Apagao.x86_64");

        [MenuItem("Apagão/Build/Windows", priority = 101)]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Windows", "Apagao.exe");

        [MenuItem("Apagão/Build/Linux e Windows", priority = 102)]
        public static void BuildBoth()
        {
            if (Build(BuildTarget.StandaloneLinux64, "Linux", "Apagao.x86_64")) Build(BuildTarget.StandaloneWindows64, "Windows", "Apagao.exe");
        }

        private static bool Build(BuildTarget target, string folder, string exe)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            if (!File.Exists(Scenes + "Menu_Principal.unity")) MenuBuilder.Build(openScene: false);
            if (!File.Exists(Scenes + "Ilha_Amparo.unity"))
            {
                EditorUtility.DisplayDialog("Build", "Falta a cena da campanha. Crie antes pelo menu Apagão → Criar Ilha do Amparo.", "Ok");
                return false;
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                EditorUtility.DisplayDialog("Build",
                    $"O módulo de build para {folder} não está instalado.\n\nNo Unity Hub: Installs → engrenagem da versão 6000.6 → Add modules → " +
                    $"{(target == BuildTarget.StandaloneWindows64 ? "Windows Build Support (Mono)" : "Linux Build Support (Mono)")}.", "Ok");
                return false;
            }

            SyncBuildScenes();
            PlayerSettings.productName = "Apagão - Ilha do Amparo";
            PlayerSettings.companyName = "Vinicius Mota";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));
            string dir = Path.Combine(root, folder);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(dir, exe),
                target = target,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Build", $"O build para {folder} falhou ({summary.result}). Veja os erros no Console.", "Ok");
                return false;
            }

            // Guia de teste junto do jogo e um .zip para mandar.
            string guide = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TESTE.md"));
            if (File.Exists(guide)) File.Copy(guide, Path.Combine(dir, "LEIA-ME - TESTE.md"), true);
            string zip = Path.Combine(root, $"Apagao_{folder}.zip");
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(dir, zip, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: true);

            double mb = new FileInfo(zip).Length / (1024.0 * 1024.0);
            Debug.Log($"[Apagão] Build {folder} pronto em {summary.totalTime.TotalSeconds:0}s: {zip} ({mb:0} MB)");
            EditorUtility.RevealInFinder(zip);
            return true;
        }
    }
}
