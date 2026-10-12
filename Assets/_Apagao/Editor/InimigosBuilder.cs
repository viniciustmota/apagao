using Apagao.AI;
using Apagao.AI.Hive;
using Apagao.DebugTools;
using Apagao.AI.Sentinela;
using Apagao.Combat;
using Apagao.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Apagao.EditorTools
{
    /// <summary>
    /// Coloca o NavMeshBaker e as Sentinelas com suas rotas na cena.
    /// Chamado pelo "Criar Ilha de Teste" e também disponível sozinho no menu.
    /// </summary>
    public static class InimigosBuilder
    {
        [MenuItem("Apagão/Recriar Inimigos na Cena Aberta")]
        public static void RebuildInOpenScene()
        {
            var terrain = Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Inimigos", "Nenhum Terrain na cena aberta. Use Apagão > Criar Ilha de Teste.", "Ok");
                return;
            }
            Populate(terrain);
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Apagão] Inimigos recriados.");
        }

        public static void Populate(Terrain t)
        {
            DestroyIfExists("Sistemas");
            DestroyIfExists("Inimigos");

            var sistemas = new GameObject("Sistemas");
            sistemas.AddComponent<NavMeshBaker>();
            sistemas.AddComponent<DebugHUD>();

            // Rede da VIGÍLIA cobrindo a vila (spec 07).
            var rede = new GameObject("Rede_VIGILIA_Vila");
            rede.transform.SetParent(sistemas.transform);
            rede.AddComponent<HiveNetwork>();

            var matRobo = IlhaTesteBuilder.Mat("Robo", new Color(0.22f, 0.24f, 0.26f), 0.6f);
            var matVisor = Emissive("Robo_Visor", new Color(1f, 0.05f, 0.05f), 3f);
            var matIndicador = Unlit("Indicador_Alerta", Color.white);
            var matLaser = Unlit("Laser", new Color(1f, 0.1f, 0.1f));
            var matTracer = Unlit("Tracer", new Color(1f, 0.85f, 0.4f));

            var inimigos = new GameObject("Inimigos").transform;

            // Antena no fim da rua, perto da igreja: alcançável pela rua e por trás das casas.
            CreateAntenna(inimigos, IlhaTesteBuilder.Ground(t, 19f, -1.5f), matRobo, matVisor);

            // Sentinela 1: vai e volta pela rua principal.
            var rota1 = Route(t, inimigos, "Rota_Sentinela_1", new[] { new Vector2(-6f, 0f), new Vector2(14f, 0f) });
            CreateSentinela("Sentinela_1", inimigos, IlhaTesteBuilder.Ground(t, -6f, 0f), 90f, rota1,
                matRobo, matVisor, matIndicador, matLaser, matTracer);

            // Sentinela 2: ronda em volta da igreja.
            var rota2 = Route(t, inimigos, "Rota_Sentinela_2", new[]
            {
                new Vector2(24f, 9f), new Vector2(41f, 9f), new Vector2(41f, -14f), new Vector2(24f, -14f)
            });
            CreateSentinela("Sentinela_2", inimigos, IlhaTesteBuilder.Ground(t, 24f, 9f), 90f, rota2,
                matRobo, matVisor, matIndicador, matLaser, matTracer);
        }

        private static void CreateSentinela(string name, Transform parent, Vector3 position, float yaw, Transform[] route,
            Material robo, Material visor, Material indicador, Material laserMat, Material tracerMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var col = root.AddComponent<CapsuleCollider>();
            col.height = 2f;
            col.radius = 0.4f;
            col.center = new Vector3(0f, 1f, 0f);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2f;
            agent.angularSpeed = 360f;
            agent.acceleration = 10f;
            agent.stoppingDistance = 0.3f;
            agent.enabled = false; // o SentinelaAI liga depois que o NavMesh existir

            root.AddComponent<VisionSensor>();
            root.AddComponent<HearingSensor>();
            root.AddComponent<HiveMember>();

            // Vida (spec 08): corte ×0,6, fogo ×0,5, explosão ×1,5, elétrico paralisa (sem dano); costas ×3.
            var health = root.AddComponent<Health>();
            var hso = new SerializedObject(health);
            hso.FindProperty("maxHealth").floatValue = 100f;
            hso.FindProperty("multipliers.corte").floatValue = 0.6f;
            hso.FindProperty("multipliers.fogo").floatValue = 0.5f;
            hso.FindProperty("multipliers.explosao").floatValue = 1.5f;
            hso.FindProperty("multipliers.eletrico").floatValue = 0f;
            hso.FindProperty("backMultiplier").floatValue = 3f;
            hso.ApplyModifiedPropertiesWithoutUndo();

            var gun = root.AddComponent<SentinelaGun>();
            var ai = root.AddComponent<SentinelaAI>();

            var corpo = Part(PrimitiveType.Capsule, "Corpo", root.transform, new Vector3(0f, 1f, 0f), new Vector3(0.8f, 1f, 0.8f), robo);
            var cabeca = Part(PrimitiveType.Cube, "Cabeca", root.transform, new Vector3(0f, 1.75f, 0f), new Vector3(0.45f, 0.35f, 0.45f), robo);

            var flash = root.AddComponent<HitFlash>();
            var fso = new SerializedObject(flash);
            var rs = fso.FindProperty("renderers");
            rs.arraySize = 2;
            rs.GetArrayElementAtIndex(0).objectReferenceValue = corpo.GetComponent<Renderer>();
            rs.GetArrayElementAtIndex(1).objectReferenceValue = cabeca.GetComponent<Renderer>();
            fso.ApplyModifiedPropertiesWithoutUndo();
            Part(PrimitiveType.Cube, "Visor", root.transform, new Vector3(0f, 1.78f, 0.23f), new Vector3(0.38f, 0.07f, 0.04f), visor);
            var indicator = Part(PrimitiveType.Sphere, "Indicador", root.transform, new Vector3(0f, 2.4f, 0f), Vector3.one * 0.2f, indicador);
            indicator.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var laserGo = new GameObject("Laser");
            laserGo.transform.SetParent(root.transform, false);
            var laser = laserGo.AddComponent<LineRenderer>();
            laser.positionCount = 2;
            laser.useWorldSpace = true;
            laser.startWidth = 0.03f;
            laser.endWidth = 0.015f;
            laser.sharedMaterial = laserMat;
            laser.shadowCastingMode = ShadowCastingMode.Off;
            laser.enabled = false;

            var tracerGo = new GameObject("Tracer");
            tracerGo.transform.SetParent(root.transform, false);
            var tracer = tracerGo.AddComponent<LineRenderer>();
            tracer.positionCount = 2;
            tracer.useWorldSpace = true;
            tracer.startWidth = 0.04f;
            tracer.endWidth = 0.02f;
            tracer.sharedMaterial = tracerMat;
            tracer.shadowCastingMode = ShadowCastingMode.Off;
            tracer.enabled = false;
            var gso = new SerializedObject(gun);
            gso.FindProperty("tracer").objectReferenceValue = tracer;
            gso.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(ai);
            var wp = so.FindProperty("waypoints");
            wp.arraySize = route.Length;
            for (int i = 0; i < route.Length; i++) wp.GetArrayElementAtIndex(i).objectReferenceValue = route[i];
            so.FindProperty("alertIndicator").objectReferenceValue = indicator.GetComponent<Renderer>();
            so.FindProperty("laser").objectReferenceValue = laser;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateAntenna(Transform parent, Vector3 ground, Material metal, Material luz)
        {
            var root = new GameObject("Antena_VIGILIA");
            root.transform.SetParent(parent);
            root.transform.position = ground;

            var baseBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseBox.name = "Base";
            baseBox.transform.SetParent(root.transform, false);
            baseBox.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            baseBox.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
            baseBox.GetComponent<MeshRenderer>().sharedMaterial = metal;

            var mastro = Part(PrimitiveType.Cylinder, "Mastro", root.transform, new Vector3(0f, 4.2f, 0f), new Vector3(0.15f, 3f, 0.15f), metal);
            mastro.isStatic = true;
            var beacon = Part(PrimitiveType.Sphere, "Farol", root.transform, new Vector3(0f, 7.4f, 0f), Vector3.one * 0.35f, luz);

            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 7.4f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.1f, 0.1f);
            light.range = 10f;
            light.intensity = 4f;

            var antenna = root.AddComponent<HiveAntenna>();
            var so = new SerializedObject(antenna);
            so.FindProperty("beacon").objectReferenceValue = beacon.GetComponent<Renderer>();
            so.FindProperty("beaconLight").objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform[] Route(Terrain t, Transform parent, string name, Vector2[] points)
        {
            var rota = new GameObject(name).transform;
            rota.SetParent(parent);
            var result = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var p = new GameObject($"P{i + 1}").transform;
                p.SetParent(rota);
                p.position = IlhaTesteBuilder.Ground(t, points[i].x, points[i].y);
                result[i] = p;
            }
            return result;
        }

        private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // o collider fica só na raiz
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static Material Unlit(string name, Color color)
        {
            string path = $"{IlhaTesteBuilder.Root}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Emissive(string name, Color color, float intensity)
        {
            var m = IlhaTesteBuilder.Mat(name, color);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * intensity);
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        private static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
