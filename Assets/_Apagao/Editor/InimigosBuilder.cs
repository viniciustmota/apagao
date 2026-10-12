using Apagao.AI;
using Apagao.AI.Hive;
using Apagao.DebugTools;
using Apagao.AI.Brutamontes;
using Apagao.AI.Matilha;
using Apagao.AI.Sentinela;
using Apagao.Combat;
using Apagao.Crafting;
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
            DronesBuilder.Populate(terrain);
            CampanhaBuilder.TagSaveIds();
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

            // Matilha: 3 cães rondando por trás da fileira norte de casas (spec 14).
            CreateMatilha(t, inimigos, "Matilha_1", new[]
            {
                new Vector2(-18f, 17f), new Vector2(14f, 17f), new Vector2(14f, 25f), new Vector2(-18f, 25f)
            }, 3, matRobo, matVisor, matIndicador);

            // Brutamontes: ronda o quintal atrás da fileira sul (spec 16). O muro serve para atraí-lo numa investida.
            var rotaBruto = Route(t, inimigos, "Rota_Brutamontes", new[] { new Vector2(-16f, -19f), new Vector2(16f, -19f) });
            CreateBrutamontes("Brutamontes", inimigos, IlhaTesteBuilder.Ground(t, -16f, -19f), 90f, rotaBruto, matRobo, matVisor, matIndicador);
            var muro = GameObject.CreatePrimitive(PrimitiveType.Cube);
            muro.name = "Muro_Quintal";
            muro.transform.SetParent(inimigos);
            muro.transform.position = IlhaTesteBuilder.Ground(t, 6f, -25f) + Vector3.up * 1f;
            muro.transform.localScale = new Vector3(7f, 3f, 0.6f);
            muro.GetComponent<MeshRenderer>().sharedMaterial = IlhaTesteBuilder.Mat("Parede", new Color(0.93f, 0.86f, 0.70f));
            muro.isStatic = true;
        }

        internal static void CreateBrutamontes(string name, Transform parent, Vector3 position, float yaw, Transform[] route,
            Material robo, Material visor, Material indicador)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var col = root.AddComponent<CapsuleCollider>();
            col.height = 2.8f;
            col.radius = 0.7f;
            col.center = new Vector3(0f, 1.4f, 0f);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.7f;
            agent.height = 2.8f;
            agent.angularSpeed = 90f; // R2: vira devagar
            agent.acceleration = 6f;
            agent.stoppingDistance = 0.5f;
            agent.enabled = false;

            var vision = root.AddComponent<VisionSensor>();
            var vso = new SerializedObject(vision);
            vso.FindProperty("viewDistance").floatValue = 18f;
            vso.FindProperty("viewAngle").floatValue = 70f;
            vso.FindProperty("eyeHeight").floatValue = 2.5f;
            vso.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<HearingSensor>();
            root.AddComponent<HiveMember>();

            // Vida (spec 16): 400; frente ×0,25, costas ×1,5; fogo nas juntas ×3; elétrico só paralisa.
            var health = root.AddComponent<Health>();
            var hso = new SerializedObject(health);
            hso.FindProperty("maxHealth").floatValue = 400f;
            hso.FindProperty("multipliers.fogo").floatValue = 3f;
            hso.FindProperty("multipliers.eletrico").floatValue = 0f;
            hso.FindProperty("backMultiplier").floatValue = 1.5f;
            hso.FindProperty("frontMultiplier").floatValue = 0.25f;
            hso.ApplyModifiedPropertiesWithoutUndo();

            var ai = root.AddComponent<BrutamontesAI>();

            // Corpo articulado (RobotBodyBuilder + RobotAnimator). O ombro direito "Ombro_D" ergue no soco.
            var body = RobotBodyBuilder.Brutamontes(root.transform, robo, visor);

            var indicator = Part(PrimitiveType.Sphere, "Indicador", root.transform, new Vector3(0f, 3.3f, 0f), Vector3.one * 0.25f, indicador);
            indicator.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var flash = root.AddComponent<HitFlash>();
            var fso = new SerializedObject(flash);
            SetArray(fso.FindProperty("renderers"), body.Flash.ToArray());
            fso.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(ai);
            SetArray(so.FindProperty("waypoints"), route);
            so.FindProperty("rightArm").objectReferenceValue = body.RightShoulder;
            so.FindProperty("alertIndicator").objectReferenceValue = indicator.GetComponent<Renderer>();
            so.FindProperty("stunDurationMultiplier").floatValue = 0.5f; // R5: PEM paralisa 1,5 s
            so.FindProperty("knockbackScale").floatValue = 0.2f;
            SetArray(so.FindProperty("wreckParts"), body.Wreck.ToArray());
            SetLoot(so.FindProperty("loot"), (ResourceType.Sucata, 3, 1f), (ResourceType.Componente, 2, 1f));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void CreateSentinela(string name, Transform parent, Vector3 position, float yaw, Transform[] route,
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

            // Corpo articulado com rifle no braço direito (RobotBodyBuilder + RobotAnimator).
            var body = RobotBodyBuilder.Sentinela(root.transform, robo, visor);

            var flash = root.AddComponent<HitFlash>();
            var fso = new SerializedObject(flash);
            SetArray(fso.FindProperty("renderers"), body.Flash.ToArray());
            fso.ApplyModifiedPropertiesWithoutUndo();
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
            SetArray(so.FindProperty("wreckParts"), body.Wreck.ToArray());
            SetLoot(so.FindProperty("loot"), (ResourceType.Sucata, 2, 1f), (ResourceType.Componente, 1, 0.5f));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void CreateAntenna(Transform parent, Vector3 ground, Material metal, Material luz)
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

        internal static void CreateMatilha(Terrain t, Transform parent, string name, Vector2[] route, int count,
            Material robo, Material visor, Material indicador)
        {
            var groupGo = new GameObject(name);
            groupGo.transform.SetParent(parent);
            groupGo.transform.position = IlhaTesteBuilder.Ground(t, route[0].x, route[0].y);
            var group = groupGo.AddComponent<MatilhaGroup>();

            var waypoints = Route(t, parent, "Rota_" + name, route);
            var gso = new SerializedObject(group);
            SetArray(gso.FindProperty("waypoints"), waypoints);
            gso.ApplyModifiedPropertiesWithoutUndo();

            Vector3 start = groupGo.transform.position;
            Vector3 dir = (waypoints[1].position - waypoints[0].position).normalized;
            float yaw = Quaternion.LookRotation(dir).eulerAngles.y;
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3((i % 2 == 1 ? -1.6f : 1.6f) * (i > 0 ? 1 : 0), 0f, -1.6f * ((i + 1) / 2));
                Vector3 pos = IlhaTesteBuilder.Ground(t, start.x + offset.x, start.z + offset.z);
                CreateCao($"Cao_{i + 1}", groupGo.transform, pos, yaw, robo, visor, indicador);
            }
        }

        private static void CreateCao(string name, Transform parent, Vector3 position, float yaw,
            Material robo, Material visor, Material indicador)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 2; // deitada no eixo Z, como o corpo do cão
            col.height = 1.2f;
            col.radius = 0.32f;
            col.center = new Vector3(0f, 0.6f, 0f);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1f;
            agent.angularSpeed = 540f;
            agent.acceleration = 20f;
            agent.stoppingDistance = 0.3f;
            agent.enabled = false;

            var vision = root.AddComponent<VisionSensor>();
            var vso = new SerializedObject(vision);
            vso.FindProperty("viewDistance").floatValue = 10f;
            vso.FindProperty("viewAngle").floatValue = 140f;
            vso.FindProperty("eyeHeight").floatValue = 0.8f;
            vso.ApplyModifiedPropertiesWithoutUndo();

            var hearing = root.AddComponent<HearingSensor>();
            var hso2 = new SerializedObject(hearing);
            hso2.FindProperty("hearingMultiplier").floatValue = 2f;
            hso2.FindProperty("earHeight").floatValue = 0.8f;
            hso2.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<HiveMember>();

            // Vida (spec 08): 60, bala ×0,8, fogo ×1,5, explosão ×1,5, elétrico paralisa; juntas (por trás) ×2.
            var health = root.AddComponent<Health>();
            var hso = new SerializedObject(health);
            hso.FindProperty("maxHealth").floatValue = 60f;
            hso.FindProperty("multipliers.bala").floatValue = 0.8f;
            hso.FindProperty("multipliers.fogo").floatValue = 1.5f;
            hso.FindProperty("multipliers.explosao").floatValue = 1.5f;
            hso.FindProperty("multipliers.eletrico").floatValue = 0f;
            hso.FindProperty("backMultiplier").floatValue = 2f;
            hso.ApplyModifiedPropertiesWithoutUndo();

            var ai = root.AddComponent<MatilhaAI>();

            // Corpo articulado de quatro patas (RobotBodyBuilder + RobotAnimator).
            var body = RobotBodyBuilder.Cao(root.transform, robo, visor);
            var indicator = Part(PrimitiveType.Sphere, "Indicador", root.transform, new Vector3(0f, 1.45f, 0f), Vector3.one * 0.18f, indicador);
            indicator.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var flash = root.AddComponent<HitFlash>();
            var fso = new SerializedObject(flash);
            SetArray(fso.FindProperty("renderers"), body.Flash.ToArray());
            fso.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(ai);
            so.FindProperty("head").objectReferenceValue = body.Head;
            so.FindProperty("body").objectReferenceValue = body.Torso;
            so.FindProperty("alertIndicator").objectReferenceValue = indicator.GetComponent<Renderer>();
            SetArray(so.FindProperty("wreckParts"), body.Wreck.ToArray());
            SetLoot(so.FindProperty("loot"), (ResourceType.Sucata, 1, 1f), (ResourceType.Componente, 1, 0.3f));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Saque da máquina destruída (spec 12 R3).</summary>
        internal static void SetLoot(SerializedProperty array, params (ResourceType type, int amount, float chance)[] entries)
        {
            array.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = array.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("type").enumValueIndex = (int)entries[i].type;
                e.FindPropertyRelative("amount").intValue = entries[i].amount;
                e.FindPropertyRelative("chance").floatValue = entries[i].chance;
            }
        }

        internal static void SetArray(SerializedProperty array, params Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        internal static Transform[] Route(Terrain t, Transform parent, string name, Vector2[] points)
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

        internal static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
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

        internal static Material Unlit(string name, Color color)
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

        internal static Material Emissive(string name, Color color, float intensity)
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
