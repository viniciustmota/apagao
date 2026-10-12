using System.Globalization;
using Apagao.AI;
using Apagao.AI.Drones;
using Apagao.AI.Hive;
using Apagao.Game;
using Apagao.Systems;
using Apagao.UI;
using UnityEditor;
using UnityEngine;

namespace Apagao.EditorTools
{
    /// <summary>
    /// Demo da campanha na vila de teste (specs 18 e 19): bancada → antena → rádio da estação,
    /// com checkpoints, falas da Lia e tela de fim. Também grava os SaveIds dos objetos da cena.
    /// </summary>
    public static class CampanhaBuilder
    {
        public static void Populate(Terrain t)
        {
            var old = GameObject.Find("Campanha");
            if (old != null) Object.DestroyImmediate(old);
            CheckpointManager.DeleteSave("Ilha_Teste"); // cena nova, save novo

            var root = new GameObject("Campanha");
            root.AddComponent<CheckpointManager>();
            root.AddComponent<RadioSystem>();
            root.AddComponent<CampaignHUD>();
            var campaign = root.AddComponent<Campaign>();

            var radio = CreateRadio(t, root.transform, 43.5f, 0f);
            var bancada = GameObject.Find("Itens/Bancada");
            var antena = GameObject.Find("Inimigos/Antena_VIGILIA");

            var so = new SerializedObject(campaign);
            var list = so.FindProperty("objectives");
            list.arraySize = 3;

            SetObjective(list.GetArrayElementAtIndex(0), "Chegue à bancada no começo da vila", ObjectiveKind.Chegar,
                bancada != null ? bancada.transform : null, 3f, checkpoint: false, signal: 0.95f,
                new[]
                {
                    L(RadioSystem.Lia, "Caio? Caio, tá me ouvindo? Aqui é a Lia."),
                    L(RadioSystem.Caio, "Tô ouvindo. A vila tá cheia dessas coisas."),
                    L(RadioSystem.Lia, "Tem uma bancada logo na entrada. Junta o que achar antes de seguir.", 4f)
                },
                new[]
                {
                    L(RadioSystem.Lia, "Usa o que tiver. Elas não descansam, e você também não pode.", 4f)
                });

            SetObjective(list.GetArrayElementAtIndex(1), "Derrube a antena da VIGÍLIA no fim da rua", ObjectiveKind.SabotarAntena,
                antena != null ? antena.transform : null, 0f, checkpoint: true, signal: 0.7f,
                new[]
                {
                    L(RadioSystem.Lia, "Tem uma antena perto da igreja. É por ela que as máquinas conversam.", 4.5f),
                    L(RadioSystem.Lia, "Derruba ela e elas ficam cegas umas das outras.", 4f)
                },
                new[]
                {
                    L(RadioSystem.Caio, "Antena no chão."),
                    L(RadioSystem.Lia, "Ouvi o estalo daqui. Agora o rádio da estação, atrás da igreja.", 4f)
                });

            SetObjective(list.GetArrayElementAtIndex(2), "Chegue ao rádio da estação, atrás da igreja", ObjectiveKind.Usar,
                radio, 0f, checkpoint: true, signal: 0.35f,
                new[]
                {
                    L(RadioSystem.Lia, "O sinal tá caindo, Caio. Chega no rádio e chama o continente. Eu não vou conseguir te ouvir por muito tempo.", 5f)
                },
                new[]
                {
                    L(RadioSystem.Caio, "Continente, aqui é o Caio, da Ilha do Amparo. Alguém na escuta?", 4f),
                    L("Lia?", "Caio. Fica onde você está. A gente tá indo te buscar.", 4.5f),
                    L("", "O sinal está limpo demais. Aquela não é a Lia.", 4.5f)
                });

            so.ApplyModifiedPropertiesWithoutUndo();
            TagSaveIds();
        }

        /// <summary>Dá um id estável (caminho + posição) a tudo que o checkpoint precisa lembrar.</summary>
        [MenuItem("Apagão/Gravar SaveIds na Cena Aberta")]
        public static void TagSaveIds()
        {
            int count = 0;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!go.scene.IsValid() || EditorUtility.IsPersistent(go)) continue; // só objetos da cena
                bool tracked = go.GetComponent<MachineAI>() || go.GetComponent<DroneAI>() ||
                               go.GetComponent<Apagao.Crafting.Pickup>() || go.GetComponent<HiveAntenna>() ||
                               go.GetComponent<Apagao.Combat.WeaponPickup>();
                if (!tracked) continue;

                var id = go.GetComponent<SaveId>();
                if (id == null) id = go.AddComponent<SaveId>();
                var so = new SerializedObject(id);
                Vector3 p = go.transform.position;
                so.FindProperty("id").stringValue =
                    $"{Path(go.transform)}@{p.x.ToString("0.0", CultureInfo.InvariantCulture)},{p.z.ToString("0.0", CultureInfo.InvariantCulture)}";
                so.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }
            Debug.Log($"[Apagão] {count} SaveIds gravados.");
        }

        // ---------- Peças ----------

        private static Transform CreateRadio(Terrain t, Transform parent, float x, float z)
        {
            var root = new GameObject("Radio_Estacao");
            root.transform.SetParent(parent);
            root.transform.position = IlhaTesteBuilder.Ground(t, x, z);

            var mesa = IlhaTesteBuilder.Mat("Caixote", new Color(0.55f, 0.40f, 0.22f));
            var metal = IlhaTesteBuilder.Mat("Robo", new Color(0.22f, 0.24f, 0.26f), 0.6f);
            var luz = InimigosBuilder.Emissive("Radio_Luz", new Color(0.3f, 1f, 0.4f), 2f);

            var tampo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tampo.name = "Mesa";
            tampo.transform.SetParent(root.transform, false);
            tampo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            tampo.transform.localScale = new Vector3(1.4f, 0.9f, 0.7f);
            tampo.GetComponent<MeshRenderer>().sharedMaterial = mesa;
            tampo.isStatic = true;

            InimigosBuilder.Part(PrimitiveType.Cube, "Aparelho", root.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.8f, 0.4f, 0.45f), metal);
            InimigosBuilder.Part(PrimitiveType.Cube, "Painel", root.transform, new Vector3(0f, 1.12f, 0.23f), new Vector3(0.6f, 0.15f, 0.02f), luz);
            InimigosBuilder.Part(PrimitiveType.Cylinder, "Antena", root.transform, new Vector3(0.3f, 1.8f, 0f), new Vector3(0.03f, 0.6f, 0.03f), metal);

            var usable = root.AddComponent<UsableObject>();
            var so = new SerializedObject(usable);
            so.FindProperty("prompt").stringValue = "Usar o rádio";
            so.FindProperty("holdTime").floatValue = 1.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root.transform;
        }

        internal static void SetObjective(SerializedProperty o, string text, ObjectiveKind kind, Transform target, float radius,
            bool checkpoint, float signal, DialogueLine[] onStart, DialogueLine[] onComplete)
        {
            o.FindPropertyRelative("text").stringValue = text;
            o.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            o.FindPropertyRelative("target").objectReferenceValue = target;
            o.FindPropertyRelative("radius").floatValue = radius;
            o.FindPropertyRelative("checkpoint").boolValue = checkpoint;
            o.FindPropertyRelative("signal").floatValue = signal;
            SetLines(o.FindPropertyRelative("onStart"), onStart);
            SetLines(o.FindPropertyRelative("onComplete"), onComplete);
        }

        private static void SetLines(SerializedProperty array, DialogueLine[] lines)
        {
            array.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                var e = array.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("speaker").stringValue = lines[i].speaker;
                e.FindPropertyRelative("text").stringValue = lines[i].text;
                e.FindPropertyRelative("duration").floatValue = lines[i].duration;
            }
        }

        internal static DialogueLine L(string speaker, string text, float duration = 3.5f) => new DialogueLine(speaker, text, duration);

        private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
