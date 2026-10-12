using System.Collections.Generic;
using System.IO;
using System.Linq;
using Apagao.AI.Hive;
using Apagao.Combat;
using Apagao.Crafting;
using Apagao.DebugTools;
using Apagao.Game;
using Apagao.Player;
using Apagao.Systems;
using Apagao.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apagao.EditorTools
{
    /// <summary>
    /// Greybox da Ilha do Amparo (spec 22) com a campanha em 5 atos (spec 18).
    /// Vila (sul) → mata → sítio (oeste) → almoxarifado (nordeste) e manguezal (leste) → rua principal → estação (norte).
    /// Menu: Apagão > Criar Ilha do Amparo.
    /// </summary>
    public static class IlhaAmparoBuilder
    {
        private const string SceneName = "Ilha_Amparo";
        private const string ScenePath = IlhaTesteBuilder.Root + "/Scenes/" + SceneName + ".unity";
        private const float Size = 600f;
        private const float MaxHeight = 60f;
        private const float WaterLevel = 4f;
        private const int HeightRes = 513;

        /// <summary>Área plana de uma zona: centro, raio, altura e largura da transição.</summary>
        private struct Pad
        {
            public Vector2 Center;
            public float Radius, Height, Blend;
            public Pad(float x, float z, float r, float h, float blend = 40f)
            {
                Center = new Vector2(x, z);
                Radius = r;
                Height = h;
                Blend = blend;
            }
        }

        private static readonly Pad[] Pads =
        {
            new Pad(0f, -150f, 75f, 6f),        // Vila dos pescadores
            new Pad(-110f, -50f, 50f, 9f),      // Sítio do pai
            new Pad(150f, -80f, 62f, 2.9f, 40f),// Manguezal (água rasa: agachado, a cabeça fica abaixo d'água)
            new Pad(80f, 40f, 52f, 8f),         // Almoxarifado
            new Pad(-50f, 110f, 40f, 9f),       // Rua principal (oeste)
            new Pad(0f, 110f, 40f, 9f),         // Rua principal (centro)
            new Pad(50f, 110f, 40f, 9f),        // Rua principal (leste)
            new Pad(0f, 190f, 48f, 14f, 50f),   // Estação de cabos
        };

        /// <summary>Trilhas de terra entre as zonas (pintadas no terreno, sem árvores em cima).</summary>
        private static readonly Vector2[][] Trails =
        {
            new[] { new Vector2(0f, -112f), new Vector2(-40f, -100f), new Vector2(-85f, -68f), new Vector2(-110f, -52f) },          // vila → sítio
            new[] { new Vector2(-105f, -40f), new Vector2(-70f, -10f), new Vector2(-10f, 10f), new Vector2(40f, 25f), new Vector2(72f, 36f) }, // sítio → almoxarifado
            new[] { new Vector2(80f, 60f), new Vector2(40f, 76f), new Vector2(-40f, 88f), new Vector2(-82f, 100f) },               // almoxarifado → rua
            new[] { new Vector2(76f, 115f), new Vector2(55f, 140f), new Vector2(20f, 158f), new Vector2(0f, 166f) },               // rua → estação
            new[] { new Vector2(90f, 20f), new Vector2(120f, -10f), new Vector2(145f, -55f) },                                     // ramal do manguezal
        };

        private static Material paredeA, paredeB, paredeC, telhado, madeira, metal, concreto, caule, copa, mato, luzMat;
        private static Material robo, visor, indicador, laser, tracer;
        private static Terrain terrain;
        private static System.Random rng;

        [MenuItem("Apagão/Criar Ilha do Amparo")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Ilha do Amparo", "A cena Ilha_Amparo já existe. Recriar do zero?", "Recriar", "Cancelar"))
                return;

            IlhaTesteBuilder.EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            rng = new System.Random(42);
            CreateMaterials();

            var sun = IlhaTesteBuilder.CreateLighting();
            terrain = CreateTerrain();
            IlhaTesteBuilder.CreateWater(IlhaTesteBuilder.Mat("Agua", new Color(0.10f, 0.42f, 0.55f), 0.9f), 130f);

            // Cenário fixo (sempre ligado) e um grupo de inimigos/itens por ato.
            var cenario = new GameObject("Cenario").transform;
            var atos = new Transform[5];
            for (int i = 0; i < atos.Length; i++) atos[i] = new GameObject($"Ato_{i}").transform;

            BuildVila(cenario, atos[0]);
            BuildMata(cenario, atos[1]);
            var facao = BuildSitio(cenario, atos[1], out var antenaSitio);
            BuildMangue(cenario, atos[2]);
            var pem = BuildAlmoxarifado(cenario, atos[2], out var antenaAlmox);
            BuildRua(cenario, atos[3]);
            BuildEstacao(cenario, atos[4], out var radio, out var flags);

            var caio = IlhaTesteBuilder.CreatePlayerAndCamera(terrain, IlhaTesteBuilder.Mat("Caio", new Color(0.90f, 0.45f, 0.10f)), new Vector2(-52f, -150f), 90f);
            var loadout = caio.AddComponent<PlayerLoadout>();
            var lso = new SerializedObject(loadout);
            foreach (var f in new[] { "facao", "revolver", "espingarda", "pem" }) lso.FindProperty(f).boolValue = false;
            lso.ApplyModifiedPropertiesWithoutUndo();
            caio.AddComponent<SeaCurrent>();
            var cpem = caio.GetComponent<Pem>();
            if (cpem != null)
            {
                var pso = new SerializedObject(cpem);
                var start = pso.FindProperty("startCharges");
                if (start != null) start.intValue = 0;
                pso.ApplyModifiedPropertiesWithoutUndo();
            }

            var sistemas = new GameObject("Sistemas");
            sistemas.AddComponent<DebugHUD>();

            BuildCampaign(sun, atos, facao, antenaSitio, pem, antenaAlmox, radio, flags);

            // Atos futuros começam desligados (o ActManager liga na hora certa).
            for (int i = 1; i < atos.Length; i++) atos[i].gameObject.SetActive(false);

            CampanhaBuilder.TagSaveIds();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Apagão] Ilha_Amparo criada. Aperte Play.");
        }

        // ---------- Terreno ----------

        private static Terrain CreateTerrain()
        {
            string dir = IlhaTesteBuilder.Root + "/Terrain";
            var data = new TerrainData
            {
                heightmapResolution = HeightRes,
                alphamapResolution = 512,
                size = new Vector3(Size, MaxHeight, Size)
            };

            var h = new float[HeightRes, HeightRes];
            for (int y = 0; y < HeightRes; y++)
            for (int x = 0; x < HeightRes; x++)
            {
                float wx = x / (HeightRes - 1f) * Size - Size / 2f;
                float wz = y / (HeightRes - 1f) * Size - Size / 2f;
                h[y, x] = HeightAt(wx, wz) / MaxHeight;
            }
            data.SetHeights(0, 0, h);

            var areia = IlhaTesteBuilder.Layer(dir, "Amparo_Areia", new Color(0.86f, 0.78f, 0.58f));
            var grama = IlhaTesteBuilder.Layer(dir, "Amparo_Grama", new Color(0.27f, 0.45f, 0.18f));
            var lama = IlhaTesteBuilder.Layer(dir, "Amparo_Lama", new Color(0.33f, 0.28f, 0.2f));
            var trilha = IlhaTesteBuilder.Layer(dir, "Amparo_Trilha", new Color(0.55f, 0.42f, 0.27f));
            data.terrainLayers = new[] { areia, grama, lama, trilha };

            int res = data.alphamapResolution;
            var maps = new float[res, res, 4];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float height = data.GetInterpolatedHeight(x / (res - 1f), y / (res - 1f));
                float wx = x / (res - 1f) * Size - Size / 2f, wz = y / (res - 1f) * Size - Size / 2f;
                float mud = PadWeight(Pads[2], wx, wz);
                float grass = IlhaTesteBuilder.SmoothStep(WaterLevel + 0.6f, WaterLevel + 1.4f, height) * (1f - mud);
                float path = IlhaTesteBuilder.SmoothStep(3.5f, 1.8f, TrailDistance(wx, wz));
                maps[y, x, 0] = Mathf.Max(0f, 1f - grass - mud) * (1f - path);
                maps[y, x, 1] = grass * (1f - path);
                maps[y, x, 2] = mud * (1f - path);
                maps[y, x, 3] = path;
            }
            data.SetAlphamaps(0, 0, maps);

            IlhaTesteBuilder.ReplaceAsset(data, dir + "/Amparo_Data.asset");
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Ilha_do_Amparo";
            go.transform.position = new Vector3(-Size / 2f, 0f, -Size / 2f);
            var t = go.GetComponent<Terrain>();
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                IlhaTesteBuilder.ReplaceAsset(mat, dir + "/Amparo_Terreno_URP.mat");
                t.materialTemplate = mat;
            }
            return t;
        }

        /// <summary>Altura do mundo em (x, z): ilha irregular, mata ondulada, zonas planas e morro da estação.</summary>
        private static float HeightAt(float x, float z)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            float ang = Mathf.Atan2(z, x);
            float coast = 255f + 18f * Mathf.Sin(ang * 3f + 1.3f) + 10f * Mathf.Sin(ang * 7f);
            float land = IlhaTesteBuilder.SmoothStep(coast, coast - 45f, d);

            float relief = 7f + (Mathf.PerlinNoise(x * 0.012f + 31f, z * 0.012f + 7f) - 0.5f) * 8f
                           + 10f * IlhaTesteBuilder.Gauss(x / Size, z / Size, -0.05f, 0.05f, 0.08f)   // morro da mata
                           + 6f * IlhaTesteBuilder.Gauss(x / Size, z / Size, 0f, 0.33f, 0.06f);      // morro da estação
            float h = relief;
            foreach (var p in Pads) h = Mathf.Lerp(h, p.Height, PadWeight(p, x, z));

            float seabed = 0f; // mar fundo: a corrente empurra de volta (R8)
            return Mathf.Lerp(seabed, h, land);
        }

        private static float PadWeight(Pad p, float x, float z)
        {
            float dist = Vector2.Distance(p.Center, new Vector2(x, z));
            return IlhaTesteBuilder.SmoothStep(p.Radius + p.Blend, p.Radius, dist);
        }

        // Cada zona foi desenhada nas coordenadas da primeira versão (ilha de 1 km); o deslocamento
        // da zona a aproxima das outras na ilha compacta. G, Item, rotas e marcadores aplicam o deslocamento.
        private static readonly Vector2 VilaOff = new Vector2(0f, 100f);
        private static readonly Vector2 SitioOff = new Vector2(120f, 0f);
        private static readonly Vector2 MangueOff = new Vector2(-95f, -20f);
        private static readonly Vector2 AlmoxOff = new Vector2(-50f, -70f);
        private static readonly Vector2 RuaOff = new Vector2(0f, -90f);
        private static readonly Vector2 EstacaoOff = new Vector2(0f, -130f);
        private static Vector2 off;

        private static Vector3 G(float x, float z) => IlhaTesteBuilder.Ground(terrain, x + off.x, z + off.y);

        /// <summary>Rota da zona atual (aplica o deslocamento).</summary>
        private static Vector2[] S(params Vector2[] pts)
        {
            var r = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) r[i] = pts[i] + off;
            return r;
        }

        private static Transform[] Rt(Transform parent, string name, params Vector2[] pts) =>
            InimigosBuilder.Route(terrain, parent, name, S(pts));

        // ---------- Zonas ----------

        /// <summary>Prólogo — noite. Rua leste-oeste com beco no meio da fileira norte (a saída).</summary>
        private static void BuildVila(Transform cenario, Transform ato)
        {
            off = VilaOff;
            var vila = Group(cenario, "Vila");
            Material[] paredes = { paredeA, paredeB, paredeC };
            IlhaTesteBuilder.BuildRow(terrain, vila, "Vila_N_Oeste", -62f, -238f, new float[] { 7, 6, 7, 6, 8, 6, 7 }, new[] { 3.2f, 3.6f, 3.0f, 3.8f, 3.4f, 3.1f, 3.5f }, paredes, telhado);
            IlhaTesteBuilder.BuildRow(terrain, vila, "Vila_N_Leste", 8f, -238f, new float[] { 6, 7, 6, 8, 7, 6 }, new[] { 3.3f, 3.0f, 3.7f, 3.2f, 3.6f, 3.4f }, paredes, telhado);
            IlhaTesteBuilder.BuildRow(terrain, vila, "Vila_S", -62f, -262f, new float[] { 8, 6, 7, 6, 7, 8, 6, 7, 6, 7, 6, 7, 8, 6, 7, 7, 6, 7 }, new[] { 3.5f, 3.2f, 3.8f, 3.1f, 3.4f, 3.6f, 3.0f, 3.3f, 3.7f, 3.2f, 3.5f, 3.1f, 3.6f, 3.4f, 3.2f, 3.8f, 3.3f, 3.5f }, paredes, telhado);

            // Rotas pelo alto: caixotes e galpões encostados nas casas.
            IlhaTesteBuilder.Box("Caixote_1m", vila, G(-30f, -244f), new Vector3(1f, 1f, 1f), madeira);
            IlhaTesteBuilder.Box("Caixote_2m", vila, G(-30f, -242.8f), new Vector3(1.2f, 2f, 1f), madeira);
            IlhaTesteBuilder.Box("Galpao_Vila_1", vila, G(20f, -256.5f), new Vector3(3f, 2.2f, 2.5f), madeira);
            IlhaTesteBuilder.Box("Mureta_V1", vila, G(-15f, -247.5f), new Vector3(2.5f, 1.1f, 0.3f), paredeA);
            IlhaTesteBuilder.Box("Mureta_V2", vila, G(12f, -252.5f), new Vector3(2.5f, 1.1f, 0.3f), paredeA);
            IlhaTesteBuilder.Box("Barco", vila, G(-20f, -330f), new Vector3(2f, 1f, 6f), madeira);

            foreach (var x in new[] { -45f, -15f, 15f, 45f }) Lamp(vila, x, -246f);

            // Mato alto entre a vila e a mata: a primeira rota escondida.
            for (int i = 0; i < 8; i++) Grass(vila, -35f + i * 10f, -212f + (i % 2) * 6f, 8f, 6f);

            var rede = Network(ato, "Rede_Vila", G(0f, -250f), 170f);
            InimigosBuilder.CreateAntenna(ato, G(68f, -245f), robo, visor);
            Sentinela(ato, "Sentinela_Vila_1", new[] { new Vector2(-45f, -250f), new Vector2(45f, -250f) });
            Sentinela(ato, "Sentinela_Vila_2", new[] { new Vector2(-50f, -228f), new Vector2(-8f, -228f), new Vector2(-8f, -222f), new Vector2(50f, -222f) });
            DronesBuilder.CreateOlho(ato, G(-50f, -280f) + Vector3.up * 14f, Rt(ato, "Rota_Olho_Vila", new Vector2(-50f, -280f), new Vector2(50f, -280f), new Vector2(50f, -220f), new Vector2(-50f, -220f)));

            Item(ato, ResourceType.Pano, 1, -40f, -244f);
            Item(ato, ResourceType.Alcool, 1, 5f, -256f);
            Item(ato, ResourceType.Sucata, 2, 30f, -244f);
            Baker(ato, "NavMesh_Vila", G(0f, -250f), 190f);
        }

        /// <summary>Mata e coqueiral entre a vila e o resto da ilha: árvores, mato alto e uma matilha solta.</summary>
        private static void BuildMata(Transform cenario, Transform ato)
        {
            off = Vector2.zero; // a mata usa coordenadas da ilha compacta
            var mata = Group(cenario, "Mata");
            int placed = 0;
            for (int i = 0; i < 3000 && placed < 230; i++)
            {
                float x = Range(-220f, 220f), z = Range(-130f, 160f);
                if (InAnyPad(x, z, 10f) || NearTrail(x, z, 4f) || G(x, z).y < WaterLevel + 1f) continue;
                Tree(mata, x, z);
                placed++;
            }
            for (int i = 0, n = 0; i < 400 && n < 45; i++)
            {
                float x = Range(-200f, 200f), z = Range(-125f, 150f);
                if (InAnyPad(x, z, 5f) || G(x, z).y < WaterLevel + 1f) continue;
                Grass(mata, x, z, Range(5f, 10f), Range(5f, 10f));
                n++;
            }

            // Placas nas bifurcações: quem está perdido segue a trilha de terra.
            Sign(mata, 7f, -116f, 0f, "<- Sitio");
            Sign(mata, -98f, -32f, 0f, "Almoxarifado ->");
            Sign(mata, 72f, 66f, 0f, "<- Rua principal");
            Sign(mata, 98f, 16f, 0f, "Manguezal ->");
            Sign(mata, 82f, 122f, 0f, "Estacao ^");

            // Pontos de interesse no caminho: caminhonete largada, barraco de pescador e caixas para saquear.
            IlhaTesteBuilder.Box("Caminhonete", mata, G(-52f, -2f), new Vector3(2.2f, 1.8f, 5f), metal);
            IlhaTesteBuilder.Box("Cacamba", mata, G(-52f, 1f), new Vector3(2.2f, 0.6f, 2.2f), madeira);
            Shed(mata, "Barraco", G(18f, 38f), new Vector3(5f, 2.6f, 4f), madeira, openSouth: true);
            IlhaTesteBuilder.Box("Caixote_Barraco", mata, G(18f, 39f), new Vector3(1f, 0.8f, 1f), madeira);
            Item(ato, ResourceType.Componente, 1, 18f, 39f);
            Item(ato, ResourceType.Sucata, 1, -49f, -2f);
            // Uma Sentinela sozinha na trilha do sítio ao almoxarifado: treino de takedown sem rede por perto.
            Sentinela(ato, "Sentinela_Trilha", new[] { new Vector2(-60f, -8f), new Vector2(-5f, 10f), new Vector2(35f, 22f) });
            Baker(ato, "NavMesh_Trilha", G(-10f, 10f), 120f);

            // A matilha ronda a trilha entre a vila e o sítio: o primeiro encontro do ato 1.
            InimigosBuilder.CreateMatilha(terrain, ato, "Matilha_Mata", new[]
            {
                new Vector2(-75f, -105f), new Vector2(-35f, -105f), new Vector2(-35f, -75f), new Vector2(-75f, -75f)
            }, 3, robo, visor, indicador);
            Item(ato, ResourceType.Pano, 2, -20f, -95f);
            Item(ato, ResourceType.Polvora, 1, -85f, -80f);
            Item(ato, ResourceType.Alcool, 1, 10f, 15f);      // no meio do caminho para o almoxarifado
            Item(ato, ResourceType.Sucata, 2, -40f, 60f);
            Baker(ato, "NavMesh_Mata", G(-55f, -90f), 90f);
        }

        /// <summary>Ato 1 — sítio do pai: casa, galpão aberto com o facão, curral e a antena.</summary>
        private static Transform BuildSitio(Transform cenario, Transform ato, out Transform antena)
        {
            off = SitioOff;
            var sitio = Group(cenario, "Sitio");
            IlhaTesteBuilder.Box("Casa_do_Pai", sitio, G(-240f, -55f), new Vector3(12f, 4f, 9f), paredeA);
            IlhaTesteBuilder.Box("Telhado_Casa", sitio, G(-240f, -55f) + Vector3.up * 4f, new Vector3(12.6f, 0.3f, 9.6f), telhado, bury: false);
            IlhaTesteBuilder.Box("Varanda", sitio, G(-240f, -49f), new Vector3(1.6f, 0.9f, 0.8f), madeira);
            Shed(sitio, "Galpao_Sitio", G(-215f, -35f), new Vector3(8f, 3.2f, 6f), madeira, openSouth: true);
            IlhaTesteBuilder.Box("Mesa_Facao", sitio, G(-215f, -36f), new Vector3(1.6f, 0.9f, 0.8f), madeira);
            for (int i = 0; i < 6; i++) IlhaTesteBuilder.Box($"Cerca_{i}", sitio, G(-260f + i * 7f, -75f), new Vector3(6f, 1.1f, 0.2f), madeira);
            IlhaTesteBuilder.Box("Caixa_Dagua", sitio, G(-252f, -38f), new Vector3(2f, 2.4f, 2f), concreto);

            var facao = WeaponPickupAt(ato, "Facao_do_Pai", WeaponSlot.Facao, "Pegar o facão do seu pai", G(-215f, -36f) + Vector3.up * 1.05f, 0);
            WeaponPickupAt(ato, "Revolver_do_Pai", WeaponSlot.Revolver, "Pegar o revólver do seu pai", G(-240f, -49f) + Vector3.up * 1.05f, 6);
            Bench(ato, "Bancada_Sitio", G(-228f, -45f));

            var rede = Network(ato, "Rede_Sitio", G(-230f, -50f), 140f);
            InimigosBuilder.CreateAntenna(ato, G(-205f, -72f), robo, visor);
            antena = ato.GetChild(ato.childCount - 1); // a antena acabou de ser criada
            Sentinela(ato, "Sentinela_Sitio_1", new[] { new Vector2(-255f, -30f), new Vector2(-200f, -30f), new Vector2(-200f, -70f), new Vector2(-255f, -70f) });
            Sentinela(ato, "Sentinela_Sitio_2", new[] { new Vector2(-212f, -42f), new Vector2(-222f, -42f) }); // guarda o galpão

            Item(ato, ResourceType.Sucata, 2, -250f, -60f);
            Item(ato, ResourceType.Alcool, 1, -226f, -30f);
            Item(ato, ResourceType.Componente, 1, -205f, -60f);
            Baker(ato, "NavMesh_Sitio", G(-230f, -50f), 130f);
            return facao;
        }

        /// <summary>Manguezal (opcional): água rasa, raízes, barco com a espingarda e a Brasa por cima.</summary>
        private static void BuildMangue(Transform cenario, Transform ato)
        {
            off = MangueOff;
            var mangue = Group(cenario, "Manguezal");
            for (int i = 0; i < 70; i++)
            {
                float x = 245f + Range(-60f, 60f), z = -60f + Range(-60f, 60f);
                var raiz = IlhaTesteBuilder.Box("Raiz", mangue, G(x, z), new Vector3(0.4f, Range(1.5f, 3f), 0.4f), caule);
                raiz.transform.Rotate(Range(-15f, 15f), Range(0f, 360f), Range(-15f, 15f));
                if (i % 3 == 0)
                {
                    var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(c.GetComponent<Collider>());
                    c.name = "Copa_Mangue";
                    c.transform.SetParent(mangue);
                    c.transform.position = G(x, z) + Vector3.up * 3.5f;
                    c.transform.localScale = new Vector3(4f, 2.5f, 4f);
                    c.GetComponent<MeshRenderer>().sharedMaterial = copa;
                }
            }
            for (int i = 0; i < 10; i++) Grass(mangue, 245f + Range(-50f, 50f), -60f + Range(-50f, 50f), 8f, 8f);
            IlhaTesteBuilder.Box("Barco_Mangue", mangue, G(262f, -78f), new Vector3(2f, 0.9f, 5f), madeira);
            WeaponPickupAt(ato, "Espingarda_de_Pesca", WeaponSlot.Espingarda, "Pegar a espingarda de pesca", G(262f, -78f) + Vector3.up * 1.1f, 4);
            Item(ato, ResourceType.Componente, 1, 230f, -40f);

            DronesBuilder.CreateBrasa(ato, G(200f, -100f) + Vector3.up * 8f, Rt(ato, "Rota_Brasa_Mangue", new Vector2(200f, -100f), new Vector2(290f, -100f), new Vector2(290f, -20f), new Vector2(200f, -20f)));
        }

        /// <summary>Ato 2 — galpão oco com prateleiras, bancada e as peças do PEM. Brutamontes no pátio.</summary>
        private static Transform BuildAlmoxarifado(Transform cenario, Transform ato, out Transform antena)
        {
            off = AlmoxOff;
            var almox = Group(cenario, "Almoxarifado");
            Vector3 c = G(130f, 110f);
            Shed(almox, "Galpao_Almoxarifado", c, new Vector3(32f, 6f, 20f), concreto, openSouth: true, openNorth: true);
            for (int i = 0; i < 3; i++)
                IlhaTesteBuilder.Box($"Prateleira_{i}", almox, G(120f + i * 10f, 114f), new Vector3(6f, 2.6f, 1f), metal);
            IlhaTesteBuilder.Box("Mesa_PEM", almox, G(130f, 104f), new Vector3(2f, 0.9f, 1f), madeira);

            // Rota vertical: chão → caixote 1 m → caixote 2 m → anexo 3,5 m → telhado 6 m.
            IlhaTesteBuilder.Box("Caixote_A1", almox, G(110f, 96f), new Vector3(1f, 1f, 1f), madeira);
            IlhaTesteBuilder.Box("Caixote_A2", almox, G(111.2f, 97.5f), new Vector3(1.2f, 2f, 1.2f), madeira);
            IlhaTesteBuilder.Box("Anexo", almox, G(112f, 100.8f), new Vector3(4f, 3.9f, 2.6f), concreto);
            IlhaTesteBuilder.Box("Container_1", almox, G(155f, 88f), new Vector3(6f, 2.5f, 2.4f), metal);
            IlhaTesteBuilder.Box("Container_2", almox, G(100f, 125f), new Vector3(2.4f, 2.5f, 6f), metal);

            var pem = WeaponPickupAt(ato, "Pecas_do_PEM", WeaponSlot.Pem, "Montar o PEM", G(130f, 104f) + Vector3.up * 1.05f, 2, holdTime: 3f);
            Bench(ato, "Bancada_Almoxarifado", G(124f, 104f));

            var rede = Network(ato, "Rede_Almoxarifado", c, 150f);
            InimigosBuilder.CreateAntenna(ato, G(165f, 135f), robo, visor);
            antena = ato.GetChild(ato.childCount - 1);
            InimigosBuilder.CreateBrutamontes("Brutamontes_Patio", ato, G(100f, 86f), 90f,
                Rt(ato, "Rota_Bruto_Patio", new Vector2(100f, 86f), new Vector2(160f, 86f)), robo, visor, indicador);
            Sentinela(ato, "Sentinela_Almox_1", new[] { new Vector2(118f, 108f), new Vector2(142f, 108f) });
            Sentinela(ato, "Sentinela_Almox_2", new[] { new Vector2(108f, 130f), new Vector2(152f, 130f), new Vector2(152f, 92f), new Vector2(108f, 92f) });
            InimigosBuilder.CreateMatilha(terrain, ato, "Matilha_Almox", S(
                new Vector2(165f, 95f), new Vector2(180f, 95f), new Vector2(180f, 130f), new Vector2(165f, 130f)
            ), 2, robo, visor, indicador);

            Item(ato, ResourceType.Componente, 1, 140f, 116f);
            Item(ato, ResourceType.Sucata, 2, 105f, 110f);
            Item(ato, ResourceType.Polvora, 1, 150f, 104f);
            Item(ato, ResourceType.Alcool, 1, 92f, 100f);
            Baker(ato, "NavMesh_Almox", c, 120f);
            return pem;
        }

        /// <summary>Ato 3 — rua principal: duas fileiras de casas com vãos de 1,2 m para pular de telhado em telhado.</summary>
        private static void BuildRua(Transform cenario, Transform ato)
        {
            off = RuaOff;
            var rua = Group(cenario, "Rua_Principal");
            Material[] paredes = { paredeA, paredeB, paredeC };
            foreach (var row in new[] { (z: 188f, name: "Sul", seed: 0), (z: 212f, name: "Norte", seed: 3) })
            {
                float x = -70f;
                for (int i = 0; x < 66f; i++)
                {
                    float w = 7f + (i + row.seed) % 3;
                    float h = 4.6f + ((i * 7 + row.seed) % 5) * 0.15f;
                    var casa = new GameObject($"Casa_Rua_{row.name}_{i + 1}").transform;
                    casa.SetParent(rua);
                    Vector3 chao = G(x + w / 2f, row.z);
                    IlhaTesteBuilder.Box("Paredes", casa, chao, new Vector3(w, h, 9f), paredes[i % 3]);
                    IlhaTesteBuilder.Box("Telhado", casa, chao + Vector3.up * h, new Vector3(w + 0.3f, 0.3f, 9.3f), telhado, bury: false);
                    x += w + 1.2f; // vão para pular
                }
            }
            // Subida: caixotes e anexo no começo da fileira sul.
            IlhaTesteBuilder.Box("Caixote_R1", rua, G(-73f, 184f), new Vector3(1f, 1f, 1f), madeira);
            IlhaTesteBuilder.Box("Caixote_R2", rua, G(-73f, 185.6f), new Vector3(1.2f, 2f, 1.2f), madeira);
            IlhaTesteBuilder.Box("Anexo_R", rua, G(-73f, 188f), new Vector3(2.4f, 3.4f, 2.6f), concreto);
            IlhaTesteBuilder.Box("Carro_1", rua, G(-20f, 197f), new Vector3(2f, 1.5f, 4.5f), metal);
            IlhaTesteBuilder.Box("Carro_2", rua, G(30f, 203f), new Vector3(2f, 1.5f, 4.5f), metal);
            foreach (var x in new[] { -50f, -10f, 30f }) Lamp(rua, x, 200f);

            // Caixa d'água: ninho das Vespas.
            IlhaTesteBuilder.Box("Torre_Caixa_Dagua", rua, G(0f, 228f), new Vector3(1.2f, 9f, 1.2f), metal);
            IlhaTesteBuilder.Box("Caixa_Dagua_Rua", rua, G(0f, 228f) + Vector3.up * 9f, new Vector3(5f, 3f, 5f), concreto, bury: false);
            Vector3 top = G(0f, 228f) + Vector3.up * 12.4f;
            DronesBuilder.CreateVespa(ato, "Vespa_Rua_1", top + Vector3.left * 1.2f, 180f);
            DronesBuilder.CreateVespa(ato, "Vespa_Rua_2", top + Vector3.right * 1.2f, 180f);

            var rede = Network(ato, "Rede_Rua", G(0f, 200f), 150f);
            InimigosBuilder.CreateAntenna(ato, G(76f, 216f), robo, visor);
            Sentinela(ato, "Sentinela_Rua_1", new[] { new Vector2(-60f, 200f), new Vector2(60f, 200f) });
            Sentinela(ato, "Sentinela_Rua_2", new[] { new Vector2(60f, 196f), new Vector2(-60f, 196f) });
            Sentinela(ato, "Sentinela_Rua_3", new[] { new Vector2(-55f, 224f), new Vector2(55f, 224f) });
            DronesBuilder.CreateOlho(ato, G(-60f, 200f) + Vector3.up * 14f, Rt(ato, "Rota_Olho_Rua", new Vector2(-60f, 200f), new Vector2(60f, 200f)));
            Bench(ato, "Bancada_Rua", G(-78f, 196f));
            Item(ato, ResourceType.Componente, 1, -40f, 205f);
            Item(ato, ResourceType.Alcool, 1, 10f, 194f);
            Item(ato, ResourceType.Pano, 1, 45f, 206f);
            Baker(ato, "NavMesh_Rua", G(0f, 200f), 170f);
        }

        /// <summary>Ato 4 — estação de cabos no morro: muro com portão, prédio oco e o rádio.</summary>
        /// <summary>Ato 4 — estação de cabos no morro: muro com portão e a Sala do Núcleo, onde está o Capataz (spec 17).</summary>
        private static void BuildEstacao(Transform cenario, Transform ato, out Transform radio, out ObjectiveFlag[] flags)
        {
            off = EstacaoOff;
            var est = Group(cenario, "Estacao");
            Vector3 c = G(0f, 320f);
            // Muro de 2,5 m (escalável) com portão ao sul.
            IlhaTesteBuilder.Box("Muro_N", est, G(0f, 345f), new Vector3(60f, 2.5f, 0.5f), concreto);
            IlhaTesteBuilder.Box("Muro_L", est, G(30f, 320f), new Vector3(0.5f, 2.5f, 50f), concreto);
            IlhaTesteBuilder.Box("Muro_O", est, G(-30f, 320f), new Vector3(0.5f, 2.5f, 50f), concreto);
            IlhaTesteBuilder.Box("Muro_S_O", est, G(-17.5f, 295f), new Vector3(25f, 2.5f, 0.5f), concreto);
            IlhaTesteBuilder.Box("Muro_S_L", est, G(17.5f, 295f), new Vector3(25f, 2.5f, 0.5f), concreto);
            IlhaTesteBuilder.Box("Caixote_E1", est, G(-12f, 293f), new Vector3(1f, 1.4f, 1f), madeira);
            foreach (var x in new[] { -20f, 20f }) Lamp(est, x, 300f);

            var rede = Network(ato, "Rede_Estacao", c, 140f);
            InimigosBuilder.CreateAntenna(ato, G(24f, 304f), robo, visor);
            InimigosBuilder.CreateBrutamontes("Brutamontes_Portao", ato, G(-12f, 290f), 90f,
                Rt(ato, "Rota_Bruto_Portao", new Vector2(-12f, 290f), new Vector2(12f, 290f)), robo, visor, indicador);
            Sentinela(ato, "Sentinela_Estacao_1", new[] { new Vector2(-25f, 300f), new Vector2(-25f, 340f), new Vector2(25f, 340f), new Vector2(25f, 300f) });
            Sentinela(ato, "Sentinela_Estacao_2", new[] { new Vector2(-8f, 302f), new Vector2(8f, 302f) });
            DronesBuilder.CreateOlho(ato, G(-30f, 280f) + Vector3.up * 14f, Rt(ato, "Rota_Olho_Estacao", new Vector2(-30f, 280f), new Vector2(30f, 280f), new Vector2(30f, 350f), new Vector2(-30f, 350f)));
            Item(ato, ResourceType.Componente, 1, -20f, 335f);
            Item(ato, ResourceType.Sucata, 2, 20f, 285f);

            BuildArena(est, ato, out radio, out flags);
            Baker(ato, "NavMesh_Estacao", c, 120f);
        }

        /// <summary>Sala do Núcleo (spec 17 R1): 3 geradores, cobertura baixa, passarelas, o Núcleo com escudo e os reforços.</summary>
        private static void BuildArena(Transform est, Transform ato, out Transform radio, out ObjectiveFlag[] flags)
        {
            Vector3 floor = G(0f, 322f);
            Shed(est, "Sala_do_Nucleo", floor, new Vector3(34f, 9f, 26f), concreto, openSouth: true);

            // Cobertura baixa e passarelas a 3,6 m (subida por caixotes nos cantos do sul).
            foreach (var p in new[] { new Vector2(-6f, 316f), new Vector2(6f, 316f), new Vector2(-9f, 328f), new Vector2(9f, 328f), new Vector2(0f, 313f) })
                IlhaTesteBuilder.Box("Cobertura", est, G(p.x, p.y), new Vector3(2.2f, 1.1f, 0.9f), madeira);
            foreach (var side in new[] { -1f, 1f })
            {
                IlhaTesteBuilder.Box("Passarela", est, G(15.5f * side, 322f) + Vector3.up * 3.6f, new Vector3(2f, 0.3f, 20f), metal, bury: false);
                IlhaTesteBuilder.Box("Caixote_1m", est, G(12.2f * side, 311f), new Vector3(1f, 1f, 1f), madeira);
                IlhaTesteBuilder.Box("Caixote_2m", est, G(13.6f * side, 311.6f), new Vector3(1.2f, 2f, 1.2f), madeira);
            }
            foreach (var p in new[] { new Vector2(-9f, 316f), new Vector2(9f, 316f), new Vector2(-9f, 330f), new Vector2(9f, 330f) })
            {
                var l = new GameObject("Luz_Sala").AddComponent<Light>();
                l.transform.SetParent(est);
                l.transform.position = G(p.x, p.y) + Vector3.up * 7.5f;
                l.type = LightType.Point;
                l.range = 13f;
                l.intensity = 2.5f;
                l.color = new Color(0.8f, 0.85f, 1f);
            }

            // Bancada, rádio e recursos para a luta (critério: dá para vencer com o que tem na estação).
            Bench(ato, "Bancada_Estacao", G(-11f, 333f));
            IlhaTesteBuilder.Box("Mesa_Radio", est, G(11f, 333.5f), new Vector3(1.6f, 0.9f, 0.8f), madeira);
            radio = Radio(ato, G(11f, 333.5f) + Vector3.up * 0.9f);
            Item(ato, ResourceType.Componente, 1, -14f, 326f);
            Item(ato, ResourceType.Componente, 1, 14f, 326f);
            Item(ato, ResourceType.Alcool, 1, 12f, 312f);
            Item(ato, ResourceType.Pano, 1, -12f, 314f);

            // O Núcleo: pedestal, cilindro brilhante, anéis e o escudo.
            var nucleo = new GameObject("Nucleo");
            nucleo.transform.SetParent(ato);
            nucleo.transform.position = G(0f, 324f);
            var col = nucleo.AddComponent<CapsuleCollider>();
            col.radius = 1f;
            col.height = 4f;
            col.center = new Vector3(0f, 2f, 0f);
            InimigosBuilder.Part(PrimitiveType.Cube, "Pedestal", nucleo.transform, new Vector3(0f, 0.4f, 0f), new Vector3(3f, 0.8f, 3f), metal);
            var coreMat = InimigosBuilder.Emissive("Nucleo", new Color(1f, 0.1f, 0.05f), 3f);
            var corpo = InimigosBuilder.Part(PrimitiveType.Cylinder, "Corpo", nucleo.transform, new Vector3(0f, 2.1f, 0f), new Vector3(1.5f, 1.3f, 1.5f), coreMat);
            foreach (var hgt in new[] { 1.3f, 2.9f })
                InimigosBuilder.Part(PrimitiveType.Cylinder, "Anel", nucleo.transform, new Vector3(0f, hgt, 0f), new Vector3(2f, 0.08f, 2f), metal);
            var escudo = InimigosBuilder.Part(PrimitiveType.Sphere, "Escudo", nucleo.transform, new Vector3(0f, 2f, 0f), Vector3.one * 5f, ShieldMaterial());
            escudo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var luz = new GameObject("Luz_Nucleo").AddComponent<Light>();
            luz.transform.SetParent(nucleo.transform, false);
            luz.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            luz.type = LightType.Point;
            luz.range = 16f;
            luz.color = new Color(1f, 0.25f, 0.15f);

            var health = nucleo.AddComponent<Health>();
            var hso = new SerializedObject(health);
            hso.FindProperty("maxHealth").floatValue = 600f;
            hso.FindProperty("multipliers.eletrico").floatValue = 0f;
            hso.ApplyModifiedPropertiesWithoutUndo();
            var core = nucleo.AddComponent<NucleoCore>();
            var cso = new SerializedObject(core);
            cso.FindProperty("shield").objectReferenceValue = escudo.GetComponent<Renderer>();
            cso.FindProperty("core").objectReferenceValue = corpo.GetComponent<Renderer>();
            cso.FindProperty("coreLight").objectReferenceValue = luz;
            cso.ApplyModifiedPropertiesWithoutUndo();
            var flash = nucleo.AddComponent<HitFlash>();
            var fso = new SerializedObject(flash);
            InimigosBuilder.SetArray(fso.FindProperty("renderers"), corpo.GetComponent<Renderer>());
            fso.ApplyModifiedPropertiesWithoutUndo();

            // Geradores (fase 1).
            var gens = new List<Generator>();
            foreach (var p in new[] { new Vector2(-14f, 316f), new Vector2(14f, 316f), new Vector2(0f, 333.3f) })
            {
                var g = new GameObject("Gerador");
                g.transform.SetParent(ato);
                g.transform.position = G(p.x, p.y);
                IlhaTesteBuilder.Box("Corpo", g.transform, g.transform.position, new Vector3(1.6f, 2.2f, 1.2f), metal);
                var painel = InimigosBuilder.Part(PrimitiveType.Cube, "Painel", g.transform, new Vector3(0f, 1.4f, 0f), new Vector3(1.65f, 0.4f, 1.25f),
                    InimigosBuilder.Emissive("Gerador_Painel", new Color(0.3f, 0.8f, 1f), 2.5f));
                var gl = new GameObject("Luz").AddComponent<Light>();
                gl.transform.SetParent(g.transform, false);
                gl.transform.localPosition = new Vector3(0f, 2.6f, 0f);
                gl.type = LightType.Point;
                gl.range = 6f;
                gl.color = new Color(0.3f, 0.8f, 1f);
                var gen = g.AddComponent<Generator>();
                var gso = new SerializedObject(gen);
                gso.FindProperty("panel").objectReferenceValue = painel.GetComponent<Renderer>();
                gso.FindProperty("glow").objectReferenceValue = gl;
                gso.ApplyModifiedPropertiesWithoutUndo();
                gens.Add(gen);
            }

            // Reforços (desligados até o Capataz chamar) e Vespas nas passarelas (fase 3).
            var reforcos = new List<GameObject>();
            var spots = new[] { new Vector2(-4f, 304f), new Vector2(4f, 304f), new Vector2(-10f, 306f), new Vector2(10f, 306f), new Vector2(-6f, 299f), new Vector2(6f, 299f) };
            for (int i = 0; i < spots.Length; i++)
            {
                string name = $"Reforco_{i + 1}";
                Sentinela(ato, name, new[] { spots[i], new Vector2(spots[i].x * 0.5f, 318f) });
                var r = ato.Find(name).gameObject;
                r.SetActive(false);
                reforcos.Add(r);
            }
            var vespas = new List<GameObject>();
            float catwalkTop = floor.y + 3.9f + 0.4f;
            foreach (var (name, x, z) in new[] { ("Vespa_Nucleo_1", -15.5f, 330f), ("Vespa_Nucleo_2", 15.5f, 330f), ("Vespa_Nucleo_3", -15.5f, 314f) })
            {
                Vector3 g = G(x, z);
                DronesBuilder.CreateVespa(ato, name, new Vector3(g.x, catwalkTop, g.z), 180f);
                var v = ato.Find(name).gameObject;
                v.SetActive(false);
                vespas.Add(v);
            }

            // Capataz: orquestra as fases.
            var bossGo = new GameObject("Capataz");
            bossGo.transform.SetParent(ato);
            bossGo.transform.position = floor;
            flags = new ObjectiveFlag[3];
            for (int i = 0; i < 3; i++)
            {
                var f = new GameObject($"Fase_{i + 1}_Concluida");
                f.transform.SetParent(bossGo.transform, false);
                flags[i] = f.AddComponent<ObjectiveFlag>();
            }
            var fogo = new List<Transform>();
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                var spot = new GameObject("Fogo_" + i).transform;
                spot.SetParent(bossGo.transform);
                spot.position = G(Mathf.Sin(a) * 10f, 322f + Mathf.Cos(a) * 8f);
                fogo.Add(spot);
            }
            var laserMat = InimigosBuilder.Unlit("Laser_Capataz", Color.white);
            var lasers = new List<LineRenderer>();
            for (int i = 0; i < 2; i++)
            {
                var lgo = new GameObject("Laser_" + i);
                lgo.transform.SetParent(bossGo.transform, false);
                var lr = lgo.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.startWidth = lr.endWidth = 0.12f;
                lr.sharedMaterial = laserMat;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.enabled = false;
                lasers.Add(lr);
            }
            var boss = bossGo.AddComponent<CapatazBoss>();
            var bso = new SerializedObject(boss);
            bso.FindProperty("core").objectReferenceValue = core;
            InimigosBuilder.SetArray(bso.FindProperty("generators"), gens.ToArray());
            bso.FindProperty("phase1Done").objectReferenceValue = flags[0];
            bso.FindProperty("phase2Done").objectReferenceValue = flags[1];
            bso.FindProperty("phase3Done").objectReferenceValue = flags[2];
            InimigosBuilder.SetArray(bso.FindProperty("reinforcements"), reforcos.ToArray());
            InimigosBuilder.SetArray(bso.FindProperty("vespas"), vespas.ToArray());
            InimigosBuilder.SetArray(bso.FindProperty("fireSpots"), fogo.ToArray());
            InimigosBuilder.SetArray(bso.FindProperty("lasers"), lasers.ToArray());
            bso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Radio(Transform parent, Vector3 position)
        {
            var go = new GameObject("Radio_Estacao");
            go.transform.SetParent(parent);
            go.transform.position = position;
            InimigosBuilder.Part(PrimitiveType.Cube, "Aparelho", go.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.8f, 0.4f, 0.45f), metal);
            InimigosBuilder.Part(PrimitiveType.Cube, "Painel", go.transform, new Vector3(0f, 0.22f, 0.23f), new Vector3(0.6f, 0.15f, 0.02f), InimigosBuilder.Emissive("Radio_Luz", new Color(0.3f, 1f, 0.4f), 2f));
            var usable = go.AddComponent<UsableObject>();
            var so = new SerializedObject(usable);
            so.FindProperty("prompt").stringValue = "Usar o rádio";
            so.FindProperty("holdTime").floatValue = 1.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go.transform;
        }

        /// <summary>Material translúcido azul do escudo (URP Unlit transparente).</summary>
        private static Material ShieldMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
            var m = new Material(shader);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(0.35f, 0.75f, 1f, 0.22f));
            IlhaTesteBuilder.ReplaceAsset(m, IlhaTesteBuilder.Root + "/Materials/Escudo_Nucleo.mat");
            return m;
        }

        // ---------- Campanha ----------

        private static void BuildCampaign(Light sun, Transform[] atos, Transform facao, Transform antenaSitio, Transform pem, Transform antenaAlmox,
            Transform radio, ObjectiveFlag[] flags)
        {
            CheckpointManager.DeleteSave(SceneName);
            var root = new GameObject("Campanha");
            root.AddComponent<CheckpointManager>();
            root.AddComponent<RadioSystem>();
            root.AddComponent<CampaignHUD>();
            var acts = root.AddComponent<ActManager>();
            var campaign = root.AddComponent<Campaign>();

            // Atos e luz (spec 22 R7).
            var aso = new SerializedObject(acts);
            aso.FindProperty("sun").objectReferenceValue = sun;
            var list = aso.FindProperty("acts");
            list.arraySize = 5;
            Act(list.GetArrayElementAtIndex(0), "Prólogo — Escuro", atos[0], new Vector3(25f, 150f, 0f), new Color(0.55f, 0.65f, 0.95f), 0.3f, new Color(0.07f, 0.08f, 0.12f), new Color(0.04f, 0.05f, 0.08f), 160f);
            Act(list.GetArrayElementAtIndex(1), "Ato 1 — A Roça", atos[1], new Vector3(12f, -60f, 0f), new Color(1f, 0.72f, 0.5f), 0.9f, new Color(0.35f, 0.3f, 0.3f), new Color(0.75f, 0.62f, 0.55f), 320f);
            Act(list.GetArrayElementAtIndex(2), "Ato 2 — O Almoxarifado", atos[2], new Vector3(50f, -30f, 0f), new Color(1f, 0.95f, 0.85f), 1.3f, new Color(0.55f, 0.6f, 0.65f), new Color(0.7f, 0.8f, 0.88f), 500f);
            Act(list.GetArrayElementAtIndex(3), "Ato 3 — Pelos Telhados", atos[3], new Vector3(10f, 120f, 0f), new Color(1f, 0.55f, 0.3f), 1f, new Color(0.4f, 0.3f, 0.3f), new Color(0.8f, 0.5f, 0.35f), 360f);
            Act(list.GetArrayElementAtIndex(4), "Ato 4 — A Estação", atos[4], new Vector3(25f, 150f, 0f), new Color(0.55f, 0.65f, 0.95f), 0.3f, new Color(0.07f, 0.08f, 0.12f), new Color(0.04f, 0.05f, 0.08f), 160f);
            aso.ApplyModifiedPropertiesWithoutUndo();

            Transform Marker(string name, float x, float z, Vector2 zone)
            {
                off = zone;
                var m = new GameObject("Marcador_" + name).transform;
                m.SetParent(root.transform);
                m.position = G(x, z);
                return m;
            }

            const string Lia = RadioSystem.Lia, Caio = RadioSystem.Caio;
            var cso = new SerializedObject(campaign);
            cso.FindProperty("endTitle").stringValue = "Fim";
            cso.FindProperty("endSubtitle").stringValue = "Obrigado por jogar Apagão: Ilha do Amparo · F9 recomeça";
            var obj = cso.FindProperty("objectives");
            obj.arraySize = 13;
            int k = 0;

            void O(int act, string text, ObjectiveKind kind, Transform target, float radius, bool checkpoint, float signal,
                DialogueLine[] start, DialogueLine[] done)
            {
                var o = obj.GetArrayElementAtIndex(k++);
                CampanhaBuilder.SetObjective(o, text, kind, target, radius, checkpoint, signal, start, done);
                o.FindPropertyRelative("act").intValue = act;
            }
            DialogueLine L(string who, string text, float d = 3.8f) => CampanhaBuilder.L(who, text, d);

            O(0, "Saia da vila pelo beco ao norte sem ser visto", ObjectiveKind.Chegar, Marker("Saida_Vila", 0f, -205f, VilaOff), 10f, false, 0.95f,
                new[] { L(Lia, "Caio? É a Lia. Se você tá ouvindo, não acende luz nenhuma.", 4.5f), L(Caio, "Tô ouvindo. Elas tão na rua."), L(Lia, "Sai pelo beco do meio, atrás das casas. Agachado. Vai pro sítio do teu pai.", 4.5f) },
                new[] { L(Lia, "Isso. Agora some no mato.") });
            O(1, "Atravesse a mata até o sítio do seu pai, a oeste", ObjectiveKind.Chegar, Marker("Sitio", -230f, -50f, SitioOff), 18f, true, 0.85f,
                new[] { L(Lia, "Tem cão solto na mata. Fica no mato alto e longe da trilha.", 4.5f) },
                new[] { L(Caio, "Cheguei no sítio. Tá tudo revirado.") });
            O(1, "Pegue o facão no galpão do sítio", ObjectiveKind.Destruir, facao, 0f, false, 0.85f,
                new[] { L(Lia, "O facão do teu pai ainda deve estar no galpão. Sem ele você não derruba nenhuma dessas coisas.", 5f) },
                new[] { L(Lia, "Por trás e em silêncio. Segura E quando estiver colado nelas.", 4.5f) });
            O(1, "Derrube a antena do sítio", ObjectiveKind.SabotarAntena, antenaSitio, 0f, false, 0.85f,
                new[] { L(Lia, "A antena perto do curral liga as máquinas daqui. Derruba e elas param de se avisar.", 5f) },
                new[] { L(Lia, "Boa. O almoxarifado da estação fica a nordeste. Lá tem peça pra montar um PEM.", 5f) });
            O(2, "Chegue ao almoxarifado da estação, a nordeste", ObjectiveKind.Chegar, Marker("Almoxarifado", 130f, 110f, AlmoxOff), 28f, true, 0.7f,
                new[] { L(Caio, "PEM?"), L(Lia, "Um pulso elétrico. Apaga elas por uns segundos. É a tua chance de chegar perto.", 4.5f) },
                new[] { L(Lia, "Cuidado com o grandão. Bala de frente nem arranha ele.", 4f) });
            O(2, "Monte o PEM na mesa do almoxarifado", ObjectiveKind.Destruir, pem, 0f, false, 0.7f,
                new DialogueLine[0],
                new[] { L(Lia, "Tecla 4. Mira e solta. Funciona até na antena.") });
            O(2, "Derrube a antena do almoxarifado", ObjectiveKind.SabotarAntena, antenaAlmox, 0f, false, 0.7f,
                new DialogueLine[0],
                new[] { L(Lia, "A rua principal leva até a estação. Pelo chão você não passa. Vai por cima.", 4.5f) });
            O(3, "Atravesse a rua principal pelos telhados", ObjectiveKind.Chegar, Marker("Fim_da_Rua", 72f, 200f, RuaOff), 10f, true, 0.5f,
                new[] { L(Lia, "Tem drone nessa rua. Telhado esconde da câmera de calor, mas não do olho.", 4.5f) },
                new[] { L(Caio, "Tô vendo a estação.") });
            const string Capataz = CapatazBoss.Speaker;
            O(4, "Entre na estação de cabos", ObjectiveKind.Chegar, Marker("Estacao", 0f, 312f, EstacaoOff), 5f, true, 0.3f,
                new[] { L(Lia, "Caio… o sinal tá… quase indo. Chega no rádio da estação. Por favor.", 4.5f) },
                new[] { L(Caio, "O que é aquilo no meio da sala?") });
            O(4, "Corte os cabos dos 3 geradores (segure E com o facão)", ObjectiveKind.Concluir, flags[0].transform, 0f, true, 0.25f,
                new[] { L(Capataz, "Bem-vindo à estação, Caio. Quem fala é o prefeito. Ou o que sobrou da voz dele.", 5f),
                        L(Capataz, "Os geradores me protegem. E os meus guardas protegem os geradores.", 4.5f) },
                new DialogueLine[0]);
            O(4, "Derrube o escudo com o PEM e golpeie o Núcleo", ObjectiveKind.Concluir, flags[1].transform, 0f, true, 0.25f,
                new[] { L(Capataz, "Paciência. Eu aprendi com vocês."), L(Lia, "Caio… laser baixo, pula… laser alto, agacha…", 4.5f) },
                new DialogueLine[0]);
            O(4, "Destrua o Núcleo antes que a sala queime", ObjectiveKind.Concluir, flags[2].transform, 0f, true, 0.25f,
                new[] { L(Capataz, "Se a ilha não é minha, não é de ninguém.", 4f) },
                new[] { L(Caio, "Acabou… o zumbido parou.") });
            O(4, "Use o rádio da estação", ObjectiveKind.Usar, radio, 0f, false, 0.6f,
                new[] { L(Caio, "O rádio ainda tem bateria.") },
                new[] { L(Caio, "Continente, aqui é o Caio, da Ilha do Amparo. Alguém na escuta?", 4.5f),
                        L(Lia, "Caio? Caio, é você? Tô ouvindo! Tô ouvindo você!", 4.5f),
                        L(Lia, "Fica onde você está. O barco sai ao amanhecer.", 4.5f) });
            // As fases do Capataz são ligadas por estas marcas (e o checkpoint volta na fase certa).
            obj.GetArrayElementAtIndex(9).FindPropertyRelative("tag").stringValue = "capataz1";
            obj.GetArrayElementAtIndex(10).FindPropertyRelative("tag").stringValue = "capataz2";
            obj.GetArrayElementAtIndex(11).FindPropertyRelative("tag").stringValue = "capataz3";
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Act(SerializedProperty a, string title, Transform group, Vector3 sunEuler, Color sunColor, float intensity,
            Color ambient, Color fog, float fogEnd)
        {
            a.FindPropertyRelative("title").stringValue = title;
            var groups = a.FindPropertyRelative("groups");
            groups.arraySize = 1;
            groups.GetArrayElementAtIndex(0).objectReferenceValue = group.gameObject;
            var l = a.FindPropertyRelative("lighting");
            l.FindPropertyRelative("sunEuler").vector3Value = sunEuler;
            l.FindPropertyRelative("sunColor").colorValue = sunColor;
            l.FindPropertyRelative("sunIntensity").floatValue = intensity;
            l.FindPropertyRelative("ambient").colorValue = ambient;
            l.FindPropertyRelative("fog").colorValue = fog;
            l.FindPropertyRelative("fogEnd").floatValue = fogEnd;
        }

        // ---------- Peças ----------

        private static void CreateMaterials()
        {
            paredeA = IlhaTesteBuilder.Mat("Parede", new Color(0.93f, 0.86f, 0.70f));
            paredeB = IlhaTesteBuilder.Mat("Parede_Azul", new Color(0.45f, 0.65f, 0.80f));
            paredeC = IlhaTesteBuilder.Mat("Parede_Rosa", new Color(0.88f, 0.62f, 0.62f));
            telhado = IlhaTesteBuilder.Mat("Telhado", new Color(0.62f, 0.30f, 0.20f));
            madeira = IlhaTesteBuilder.Mat("Caixote", new Color(0.55f, 0.40f, 0.22f));
            metal = IlhaTesteBuilder.Mat("Metal", new Color(0.4f, 0.42f, 0.45f), 0.5f);
            concreto = IlhaTesteBuilder.Mat("Concreto", new Color(0.62f, 0.62f, 0.6f));
            caule = IlhaTesteBuilder.Mat("Caule", new Color(0.4f, 0.3f, 0.2f));
            copa = IlhaTesteBuilder.Mat("Copa", new Color(0.2f, 0.42f, 0.18f));
            mato = IlhaTesteBuilder.Mat("Mato_Alto", new Color(0.35f, 0.55f, 0.2f));
            luzMat = InimigosBuilder.Emissive("Lampada", new Color(1f, 0.85f, 0.55f), 3f);

            robo = IlhaTesteBuilder.Mat("Robo", new Color(0.22f, 0.24f, 0.26f), 0.6f);
            visor = InimigosBuilder.Emissive("Robo_Visor", new Color(1f, 0.05f, 0.05f), 3f);
            indicador = InimigosBuilder.Unlit("Indicador_Alerta", Color.white);
            laser = InimigosBuilder.Unlit("Laser", new Color(1f, 0.1f, 0.1f));
            tracer = InimigosBuilder.Unlit("Tracer", new Color(1f, 0.85f, 0.4f));
            DronesBuilder.InitMaterials();
        }

        private static Transform Group(Transform parent, string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent);
            return g;
        }

        private static void Sentinela(Transform parent, string name, Vector2[] route)
        {
            var points = InimigosBuilder.Route(terrain, parent, "Rota_" + name, S(route));
            Vector3 dir = route.Length > 1 ? new Vector3(route[1].x - route[0].x, 0f, route[1].y - route[0].y) : Vector3.forward;
            float yaw = Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir : Vector3.forward).eulerAngles.y;
            InimigosBuilder.CreateSentinela(name, parent, G(route[0].x, route[0].y), yaw, points, robo, visor, indicador, laser, tracer);
        }

        private static Transform Network(Transform parent, string name, Vector3 center, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = center;
            var net = go.AddComponent<HiveNetwork>();
            var so = new SerializedObject(net);
            so.FindProperty("areaSize").vector3Value = new Vector3(size, 80f, size);
            so.ApplyModifiedPropertiesWithoutUndo();
            return go.transform;
        }

        /// <summary>NavMesh só da zona, gerado quando o ato liga (ilha inteira de uma vez seria pesado demais).</summary>
        private static void Baker(Transform parent, string name, Vector3 center, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = center;
            var baker = go.AddComponent<NavMeshBaker>();
            var so = new SerializedObject(baker);
            so.FindProperty("size").vector3Value = new Vector3(size, 50f, size);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Item(Transform parent, ResourceType type, int amount, float x, float z) =>
            ItensBuilder.Item(terrain, parent, type, amount, x + off.x, z + off.y);

        private static void Bench(Transform parent, string name, Vector3 ground)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(parent);
            b.transform.position = ground + Vector3.up * 0.45f;
            b.transform.localScale = new Vector3(1.6f, 0.9f, 0.8f);
            b.GetComponent<MeshRenderer>().sharedMaterial = IlhaTesteBuilder.Mat("Bancada", new Color(0.45f, 0.32f, 0.2f));
            b.isStatic = true;
            b.AddComponent<Workbench>();
        }

        private static Transform WeaponPickupAt(Transform parent, string name, WeaponSlot slot, string prompt, Vector3 position, int bonus, float holdTime = 0.8f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = position;
            Vector3 scale = slot switch
            {
                WeaponSlot.Facao => new Vector3(0.06f, 0.04f, 0.6f),
                WeaponSlot.Revolver => new Vector3(0.08f, 0.15f, 0.3f),
                WeaponSlot.Espingarda => new Vector3(0.08f, 0.08f, 1f),
                _ => new Vector3(0.3f, 0.2f, 0.3f)
            };
            var mat = slot == WeaponSlot.Pem ? InimigosBuilder.Emissive("Pecas_PEM", new Color(0.3f, 0.8f, 1f), 2f) : metal;
            InimigosBuilder.Part(PrimitiveType.Cube, "Visual", go.transform, Vector3.zero, scale, mat);

            var light = new GameObject("Brilho").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = Vector3.up * 0.4f;
            light.type = LightType.Point;
            light.range = 2.5f;
            light.intensity = 1.5f;
            light.color = new Color(1f, 0.9f, 0.6f);

            var pickup = go.AddComponent<WeaponPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("slot").enumValueIndex = System.Array.IndexOf(System.Enum.GetValues(typeof(WeaponSlot)), slot);
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("holdTime").floatValue = holdTime;
            so.FindProperty("bonus").intValue = bonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go.transform;
        }

        /// <summary>Galpão oco: paredes, telhado e vãos de porta de 4 m nas faces abertas.</summary>
        private static void Shed(Transform parent, string name, Vector3 ground, Vector3 size, Material mat, bool openSouth = false, bool openNorth = false)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent);
            const float t = 0.5f, door = 4f;
            float hx = size.x / 2f, hz = size.z / 2f;
            IlhaTesteBuilder.Box("Parede_L", root, ground + new Vector3(hx, 0f, 0f), new Vector3(t, size.y, size.z), mat);
            IlhaTesteBuilder.Box("Parede_O", root, ground + new Vector3(-hx, 0f, 0f), new Vector3(t, size.y, size.z), mat);
            Wall(root, "Parede_S", ground + new Vector3(0f, 0f, -hz), size.x, size.y, t, openSouth ? door : 0f, mat);
            Wall(root, "Parede_N", ground + new Vector3(0f, 0f, hz), size.x, size.y, t, openNorth ? door : 0f, mat);
            IlhaTesteBuilder.Box("Telhado", root, ground + Vector3.up * size.y, new Vector3(size.x + 0.6f, 0.35f, size.z + 0.6f), telhado, bury: false);
        }

        private static void Wall(Transform root, string name, Vector3 center, float width, float height, float t, float door, Material mat)
        {
            if (door <= 0f)
            {
                IlhaTesteBuilder.Box(name, root, center, new Vector3(width, height, t), mat);
                return;
            }
            float side = (width - door) / 2f;
            IlhaTesteBuilder.Box(name + "_A", root, center + new Vector3(-(door + side) / 2f, 0f, 0f), new Vector3(side, height, t), mat);
            IlhaTesteBuilder.Box(name + "_B", root, center + new Vector3((door + side) / 2f, 0f, 0f), new Vector3(side, height, t), mat);
            IlhaTesteBuilder.Box(name + "_Verga", root, center + new Vector3(0f, 3f, 0f), new Vector3(door, height - 3f, t), mat, bury: false);
        }

        private static void Tree(Transform parent, float x, float z)
        {
            float h = Range(5f, 9f);
            var tronco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tronco.name = "Tronco";
            tronco.transform.SetParent(parent);
            tronco.transform.position = G(x, z) + Vector3.up * (h / 2f - 0.3f);
            tronco.transform.localScale = new Vector3(0.5f, h / 2f, 0.5f);
            tronco.transform.Rotate(Range(-6f, 6f), 0f, Range(-6f, 6f));
            tronco.GetComponent<MeshRenderer>().sharedMaterial = caule;
            tronco.isStatic = true;

            var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(c.GetComponent<Collider>()); // a copa não para tiro nem visão
            c.name = "Copa";
            c.transform.SetParent(parent);
            c.transform.position = G(x, z) + Vector3.up * h;
            float s = Range(3f, 5f);
            c.transform.localScale = new Vector3(s, s * 0.6f, s);
            c.GetComponent<MeshRenderer>().sharedMaterial = copa;
            c.isStatic = true;
        }

        /// <summary>Mato alto (spec 22 R6): visual sem colisão + área que esconde o Caio.</summary>
        private static void Grass(Transform parent, float x, float z, float w, float d)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Mato_Alto";
            go.transform.SetParent(parent);
            Vector3 g = G(x, z);
            go.transform.position = g + Vector3.up * 0.45f;
            go.transform.localScale = new Vector3(w, 0.9f, d);
            go.GetComponent<MeshRenderer>().sharedMaterial = mato;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = true;

            var patch = new GameObject("Area_Mato");
            patch.transform.SetParent(go.transform.parent);
            patch.transform.position = g;
            var vp = patch.AddComponent<VegetationPatch>();
            var so = new SerializedObject(vp);
            so.FindProperty("size").vector3Value = new Vector3(w, 1.6f, d);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Lamp(Transform parent, float x, float z)
        {
            var poste = IlhaTesteBuilder.Box("Poste", parent, G(x, z), new Vector3(0.15f, 4.5f, 0.15f), metal);
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.name = "Lampada";
            bulb.transform.SetParent(poste.transform.parent);
            bulb.transform.position = G(x, z) + Vector3.up * 4.4f;
            bulb.transform.localScale = Vector3.one * 0.35f;
            bulb.GetComponent<MeshRenderer>().sharedMaterial = luzMat;
            var l = new GameObject("Luz_Poste").AddComponent<Light>();
            l.transform.SetParent(bulb.transform, false);
            l.type = LightType.Point;
            l.range = 14f;
            l.intensity = 6f;
            l.color = new Color(1f, 0.82f, 0.55f);
        }

        // ---------- Utilidades ----------

        private static float TrailDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            foreach (var trail in Trails)
                for (int i = 0; i < trail.Length - 1; i++)
                {
                    Vector2 a = trail[i], b = trail[i + 1], ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
                }
            return best;
        }

        private static bool NearTrail(float x, float z, float margin) => TrailDistance(x, z) < margin;

        /// <summary>Placa de madeira com texto dos dois lados.</summary>
        private static void Sign(Transform parent, float x, float z, float yaw, string text)
        {
            var root = new GameObject("Placa_" + text).transform;
            root.SetParent(parent);
            root.position = G(x, z);
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            IlhaTesteBuilder.Box("Poste", root, root.position, new Vector3(0.12f, 2f, 0.12f), madeira);
            var tabua = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tabua.name = "Tabua";
            tabua.transform.SetParent(root, false);
            tabua.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            tabua.transform.localScale = new Vector3(2.2f, 0.45f, 0.06f);
            tabua.GetComponent<MeshRenderer>().sharedMaterial = madeira;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var back in new[] { false, true })
            {
                var tgo = new GameObject(back ? "Texto_Verso" : "Texto");
                tgo.transform.SetParent(root, false);
                tgo.transform.localPosition = new Vector3(0f, 1.75f, back ? 0.04f : -0.04f);
                tgo.transform.localRotation = Quaternion.Euler(0f, back ? 180f : 0f, 0f);
                var tm = tgo.AddComponent<TextMesh>();
                // Do outro lado a seta vira: "->" de frente aponta para o mesmo lugar que "<-" de costas.
                tm.text = back ? text.Replace("<-", "\u0001").Replace("->", "<-").Replace("\u0001", "->") : text;
                tm.font = font;
                tm.fontSize = 48;
                tm.characterSize = 0.045f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color(0.95f, 0.92f, 0.8f);
                tgo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        private static bool InAnyPad(float x, float z, float margin)
        {
            foreach (var p in Pads)
                if (Vector2.Distance(p.Center, new Vector2(x, z)) < p.Radius + margin) return true;
            return false;
        }

        private static float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); // a campanha abre primeiro no build
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
