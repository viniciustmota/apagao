using System.IO;
using System.Linq;
using Apagao.Combat;
using Apagao.Crafting;
using Apagao.Player;
using Apagao.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apagao.EditorTools
{
    /// <summary>
    /// Gera a cena de teste do marco 1: ilha, mar, uma rua com casas de telhado plano,
    /// caixotes para subir no telhado, a igreja, o Caio e a câmera.
    /// Menu: Apagão > Criar Ilha de Teste.
    /// </summary>
    public static class IlhaTesteBuilder
    {
        internal const string Root = "Assets/_Apagao";
        private const string ScenePath = Root + "/Scenes/Ilha_Teste.unity";

        private const float TerrainSize = 400f;
        private const float TerrainHeight = 30f;
        private const float WaterLevel = 4f;
        private const int HeightRes = 257;

        [MenuItem("Apagão/Criar Ilha de Teste")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Ilha de Teste", "A cena Ilha_Teste já existe. Recriar do zero?", "Recriar", "Cancelar"))
                return;

            EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Materiais
            var matParede = Mat("Parede", new Color(0.93f, 0.86f, 0.70f));
            var matParede2 = Mat("Parede_Azul", new Color(0.45f, 0.65f, 0.80f));
            var matParede3 = Mat("Parede_Rosa", new Color(0.88f, 0.62f, 0.62f));
            var matTelhado = Mat("Telhado", new Color(0.62f, 0.30f, 0.20f));
            var matIgreja = Mat("Igreja", new Color(0.96f, 0.95f, 0.90f));
            var matCaixote = Mat("Caixote", new Color(0.55f, 0.40f, 0.22f));
            var matCaio = Mat("Caio", new Color(0.90f, 0.45f, 0.10f));
            var matAgua = Mat("Agua", new Color(0.10f, 0.42f, 0.55f), 0.9f);

            CreateLighting();
            var terrain = CreateTerrain();
            CreateWater(matAgua);

            var vila = new GameObject("Vila").transform;
            Material[] paredes = { matParede, matParede2, matParede3 };

            // Fileira norte (z = +7). Alturas próximas para dar para pular de telhado em telhado.
            float[] larguraA = { 6, 5, 7, 5, 6 };
            float[] alturaA = { 3.0f, 3.4f, 3.2f, 4.0f, 3.5f };
            BuildRow(terrain, vila, "Norte", -15f, 7f, larguraA, alturaA, paredes, matTelhado);

            // Fileira sul (z = -7).
            float[] larguraB = { 7, 6, 5, 6, 6 };
            float[] alturaB = { 4.0f, 3.3f, 3.8f, 3.5f, 4.2f };
            BuildRow(terrain, vila, "Sul", -15f, -7f, larguraB, alturaB, paredes, matTelhado);

            // Caixotes: chão -> 1 m -> 2 m -> telhado de 3 m.
            Box("Caixote_1m", vila, Ground(terrain, -16f, 5f), new Vector3(1f, 1f, 1f), matCaixote);
            Box("Caixote_2m", vila, Ground(terrain, -15.5f, 6.2f), new Vector3(1f, 2f, 1.2f), matCaixote);

            // Rotas pelo alto (spec 13): galpões de 2,2 m colados aos fundos das casas (chão → galpão → telhado).
            Box("Galpao_Norte", vila, Ground(terrain, 0f, 12.25f), new Vector3(3f, 2.2f, 2.5f), matCaixote);
            Box("Galpao_Sul", vila, Ground(terrain, -5f, -12.25f), new Vector3(3f, 2.2f, 2.5f), matCaixote);

            // Muretas de 1,1 m na rua: cobertura para quem anda agachado.
            Box("Mureta_1", vila, Ground(terrain, -2f, 2.2f), new Vector3(2.5f, 1.1f, 0.3f), matParede);
            Box("Mureta_2", vila, Ground(terrain, 9f, -2.2f), new Vector3(2.5f, 1.1f, 0.3f), matParede);

            // Igreja no fim da rua, com torre.
            var igreja = new GameObject("Igreja").transform;
            igreja.SetParent(vila);
            Box("Nave", igreja, Ground(terrain, 32f, 0f), new Vector3(10f, 7f, 14f), matIgreja);
            Box("Torre", igreja, Ground(terrain, 32f, -9f), new Vector3(4f, 12f, 4f), matIgreja);

            CreatePlayerAndCamera(terrain, matCaio);
            InimigosBuilder.Populate(terrain);
            ItensBuilder.Populate(terrain);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Apagão] Ilha_Teste criada. Aperte Play.");
        }

        // ---------- Iluminação e ambiente ----------

        private static void CreateLighting()
        {
            var sol = new GameObject("Sol");
            var light = sol.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            sol.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.sun = light;
            var sky = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.70f, 0.80f, 0.88f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 120f;
            RenderSettings.fogEndDistance = 450f;
        }

        // ---------- Terreno ----------

        private static Terrain CreateTerrain()
        {
            string dir = Root + "/Terrain";
            var data = new TerrainData
            {
                heightmapResolution = HeightRes,
                alphamapResolution = 256,
                size = new Vector3(TerrainSize, TerrainHeight, TerrainSize)
            };
            data.SetHeights(0, 0, GenerateHeights());

            var areia = Layer(dir, "Areia", new Color(0.86f, 0.78f, 0.58f));
            var grama = Layer(dir, "Grama", new Color(0.30f, 0.48f, 0.20f));
            data.terrainLayers = new[] { areia, grama };
            PaintAlphamap(data);

            ReplaceAsset(data, dir + "/IlhaTeste_Data.asset");

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Ilha";
            go.transform.position = new Vector3(-TerrainSize / 2f, 0f, -TerrainSize / 2f);

            var terrain = go.GetComponent<Terrain>();
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                ReplaceAsset(mat, dir + "/Terreno_URP.mat");
                terrain.materialTemplate = mat;
            }
            return terrain;
        }

        /// <summary>Ilha arredondada com costa irregular, vila plana no centro e um morro ao norte.</summary>
        private static float[,] GenerateHeights()
        {
            var h = new float[HeightRes, HeightRes];
            float seabed = 1.8f / TerrainHeight;   // fundo do mar raso
            float land = 5.5f / TerrainHeight;     // nível da vila

            for (int y = 0; y < HeightRes; y++)
            for (int x = 0; x < HeightRes; x++)
            {
                float u = x / (HeightRes - 1f), v = y / (HeightRes - 1f);
                float dx = u - 0.5f, dz = v - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                float ang = Mathf.Atan2(dz, dx);

                float coast = 0.36f + 0.04f * Mathf.Sin(ang * 3f + 1.3f) + 0.025f * Mathf.Sin(ang * 7f);
                float isLand = SmoothStep(coast, coast - 0.08f, d);

                float noise = (Mathf.PerlinNoise(u * 6f + 37.1f, v * 6f + 12.7f) - 0.5f) * 0.06f;
                float hill = 0.35f * Gauss(u, v, 0.5f, 0.72f, 0.10f);
                float village = SmoothStep(0.12f, 0.06f, d);
                float relief = Mathf.Lerp(noise + hill, 0f, village);

                h[y, x] = Mathf.Lerp(seabed, land + relief, isLand);
            }
            return h;
        }

        private static void PaintAlphamap(TerrainData data)
        {
            int res = data.alphamapResolution;
            var maps = new float[res, res, 2];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float height = data.GetInterpolatedHeight(x / (res - 1f), y / (res - 1f));
                float grass = SmoothStep(WaterLevel + 0.6f, WaterLevel + 1.3f, height);
                maps[y, x, 0] = 1f - grass;
                maps[y, x, 1] = grass;
            }
            data.SetAlphamaps(0, 0, maps);
        }

        private static TerrainLayer Layer(string dir, string name, Color color)
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++)
            {
                float n = 0.92f + 0.08f * Mathf.PerlinNoise(i % 8 * 0.7f, i / 8 * 0.7f);
                pixels[i] = color * n;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            ReplaceAsset(tex, $"{dir}/Tex_{name}.asset");

            var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(8f, 8f) };
            ReplaceAsset(layer, $"{dir}/{name}.terrainlayer");
            return layer;
        }

        private static void CreateWater(Material mat)
        {
            var agua = GameObject.CreatePrimitive(PrimitiveType.Plane);
            agua.name = "Mar";
            agua.transform.position = new Vector3(0f, WaterLevel, 0f);
            agua.transform.localScale = new Vector3(100f, 1f, 100f);
            Object.DestroyImmediate(agua.GetComponent<Collider>()); // dá para entrar no raso
            agua.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ---------- Vila ----------

        private static void BuildRow(Terrain t, Transform parent, string nome, float startX, float z,
            float[] larguras, float[] alturas, Material[] paredes, Material telhado)
        {
            const float profundidade = 8f;
            float x = startX;
            for (int i = 0; i < larguras.Length; i++)
            {
                float w = larguras[i], hgt = alturas[i];
                float cx = x + w / 2f;
                var casa = new GameObject($"Casa_{nome}_{i + 1}").transform;
                casa.SetParent(parent);

                Vector3 chao = Ground(t, cx, z);
                Box("Paredes", casa, chao, new Vector3(w, hgt, profundidade), paredes[i % paredes.Length]);
                Box("Telhado", casa, chao + Vector3.up * hgt, new Vector3(w + 0.4f, 0.3f, profundidade + 0.4f), telhado, bury: false);
                x += w;
            }
        }

        /// <summary>Cria um cubo com a base em <paramref name="baseCenter"/>. Se <paramref name="bury"/>, enterra 1 m extra para não flutuar em terreno irregular.</summary>
        private static GameObject Box(string name, Transform parent, Vector3 baseCenter, Vector3 size, Material mat, bool bury = true)
        {
            float extra = bury ? 1f : 0f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(baseCenter.x, baseCenter.y + (size.y - extra) / 2f, baseCenter.z);
            go.transform.localScale = new Vector3(size.x, size.y + extra, size.z);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        // ---------- Jogador ----------

        private static void CreatePlayerAndCamera(Terrain t, Material mat)
        {
            var caio = new GameObject("Caio");
            caio.transform.position = Ground(t, -22f, 0f) + Vector3.up * 0.05f;
            caio.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // olhando para a rua

            var cc = caio.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(caio.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            visual.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // "Nariz" para enxergar para onde o Caio está virado.
            var nariz = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nariz.name = "Frente";
            Object.DestroyImmediate(nariz.GetComponent<Collider>());
            nariz.transform.SetParent(visual.transform, false);
            nariz.transform.localPosition = new Vector3(0f, 0.55f, 0.5f);
            nariz.transform.localScale = new Vector3(0.4f, 0.15f, 0.3f);

            var controller = caio.AddComponent<PlayerController>();
            caio.AddComponent<PlayerFootsteps>();
            caio.AddComponent<PlayerScent>();
            caio.AddComponent<LedgeClimber>();
            caio.AddComponent<Health>();
            caio.AddComponent<PlayerDeath>();

            // Facão na mão direita: o pivô gira no golpe.
            var mao = new GameObject("MaoFacao").transform;
            mao.SetParent(caio.transform, false);
            mao.localPosition = new Vector3(0.38f, 1.1f, 0.15f);
            var lamina = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lamina.name = "Lamina";
            Object.DestroyImmediate(lamina.GetComponent<Collider>());
            lamina.transform.SetParent(mao, false);
            lamina.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            lamina.transform.localScale = new Vector3(0.05f, 0.04f, 0.55f);
            lamina.GetComponent<MeshRenderer>().sharedMaterial = Mat("Facao", new Color(0.6f, 0.62f, 0.64f), 0.7f);
            var facao = caio.AddComponent<Facao>();
            SetRef(facao, "blade", mao);
            caio.AddComponent<TakedownSystem>();

            // Armas (spec 10, 11): revólver, espingarda de pesca e PEM.
            var cano = new GameObject("Cano").transform;
            cano.SetParent(caio.transform, false);
            cano.localPosition = new Vector3(0.38f, 1.4f, 0.5f);

            var ondaGo = new GameObject("OndaPEM");
            ondaGo.transform.SetParent(caio.transform, false);
            var onda = ondaGo.AddComponent<LineRenderer>();
            onda.useWorldSpace = true;
            onda.startWidth = 0.08f;
            onda.endWidth = 0.08f;
            onda.sharedMaterial = UnlitMat("PEM_Onda", new Color(0.35f, 0.8f, 1f));
            onda.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            onda.enabled = false;
            var pem = caio.AddComponent<Pem>();
            SetRef(pem, "wave", onda);

            // Recursos, crafting, interação, arremessáveis e cura (spec 12).
            caio.AddComponent<Inventory>();
            caio.AddComponent<CraftingSystem>();
            caio.AddComponent<PlayerInteractor>();
            caio.AddComponent<PlayerHealing>();
            var arcoGo = new GameObject("ArcoArremesso");
            arcoGo.transform.SetParent(caio.transform, false);
            var arco = arcoGo.AddComponent<LineRenderer>();
            arco.useWorldSpace = true;
            arco.startWidth = 0.04f;
            arco.endWidth = 0.04f;
            arco.sharedMaterial = UnlitMat("Arco_Arremesso", new Color(1f, 1f, 1f));
            arco.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arco.enabled = false;
            var thrower = caio.AddComponent<Thrower>();
            var tso = new SerializedObject(thrower);
            tso.FindProperty("hand").objectReferenceValue = cano;
            tso.FindProperty("arc").objectReferenceValue = arco;
            tso.ApplyModifiedPropertiesWithoutUndo();

            var holder = caio.AddComponent<WeaponHolder>();
            var hso = new SerializedObject(holder);
            hso.FindProperty("revolver").objectReferenceValue = Weapon("Revolver", "Revólver", 50f, 1, 60f, 0.5f, 0.35f, 6, 2.5f, 12, 6, 6, 40f);
            hso.FindProperty("espingarda").objectReferenceValue = Weapon("Espingarda", "Espingarda de pesca", 12f, 8, 15f, 6f, 0.9f, 2, 3f, 6, 2, 4, 35f);
            hso.FindProperty("muzzle").objectReferenceValue = cano;
            hso.FindProperty("tracerMaterial").objectReferenceValue = UnlitMat("Tracer_Caio", new Color(1f, 0.9f, 0.5f));
            hso.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("HUD").AddComponent<GameHUD>();

            var camGo = new GameObject("Camera Principal") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 600f;
            camGo.AddComponent<AudioListener>();
            var tpc = camGo.AddComponent<ThirdPersonCamera>();

            SetRef(controller, "cameraTransform", camGo.transform);
            SetRef(controller, "visual", visual.transform);
            SetRef(tpc, "target", caio.transform);

            camGo.transform.position = caio.transform.position + new Vector3(-3.5f, 2f, 0f);
            Selection.activeGameObject = caio;
        }

        // ---------- Utilidades ----------

        internal static Vector3 Ground(Terrain t, float x, float z)
        {
            var p = new Vector3(x, 0f, z);
            p.y = t.SampleHeight(p) + t.transform.position.y;
            return p;
        }

        /// <summary>Cria ou atualiza o asset de dados de uma arma em Data/Armas (spec 10).</summary>
        private static WeaponData Weapon(string file, string displayName, float damage, int pellets, float range, float spread,
            float fireInterval, int magazine, float reloadTime, int maxReserve, int startLoaded, int startReserve, float noiseRadius)
        {
            string dir = Root + "/Data/Armas";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root + "/Data", "Armas");
            string path = $"{dir}/{file}.asset";

            var data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(data, path);
            }

            var so = new SerializedObject(data);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("damage").floatValue = damage;
            so.FindProperty("pellets").intValue = pellets;
            so.FindProperty("range").floatValue = range;
            so.FindProperty("spread").floatValue = spread;
            so.FindProperty("fireInterval").floatValue = fireInterval;
            so.FindProperty("magazine").intValue = magazine;
            so.FindProperty("reloadTime").floatValue = reloadTime;
            so.FindProperty("maxReserve").intValue = maxReserve;
            so.FindProperty("startLoaded").intValue = startLoaded;
            so.FindProperty("startReserve").intValue = startReserve;
            so.FindProperty("noiseRadius").floatValue = noiseRadius;
            so.FindProperty("noiseIntensity").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        private static Material UnlitMat(string name, Color color)
        {
            string path = $"{Root}/Materials/{name}.mat";
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

        internal static Material Mat(string name, Color color, float smoothness = 0.15f)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void ReplaceAsset(Object asset, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "_Apagao");
            foreach (var sub in new[] { "Scenes", "Materials", "Terrain", "Prefabs", "Data" })
                if (!AssetDatabase.IsValidFolder($"{Root}/{sub}")) AssetDatabase.CreateFolder(Root, sub);
        }

        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Smoothstep estilo shader: 0 em e0, 1 em e1 (aceita e0 &gt; e1).</summary>
        private static float SmoothStep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float Gauss(float u, float v, float cu, float cv, float sigma)
        {
            float du = u - cu, dv = v - cv;
            return Mathf.Exp(-(du * du + dv * dv) / (2f * sigma * sigma));
        }
    }
}
