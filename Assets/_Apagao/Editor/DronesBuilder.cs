using Apagao.AI.Drones;
using Apagao.AI.Hive;
using Apagao.Combat;
using Apagao.Crafting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apagao.EditorTools
{
    /// <summary>Coloca os drones da spec 15 na vila de teste: um Olho, duas Vespas na igreja e uma Brasa.</summary>
    public static class DronesBuilder
    {
        private static Material robo, lente, laranja;

        internal static void InitMaterials()
        {
            robo = IlhaTesteBuilder.Mat("Drone", new Color(0.18f, 0.19f, 0.21f), 0.6f);
            lente = InimigosBuilder.Unlit("Drone_Lente", Color.white);
            laranja = InimigosBuilder.Emissive("Drone_Tanque", new Color(0.9f, 0.35f, 0.05f), 1.5f);
        }

        public static void Populate(Terrain t)
        {
            var old = GameObject.Find("Drones");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Drones").transform;
            InitMaterials();

            // Olho: retângulo sobre a vila, a 12 m.
            var rotaOlho = InimigosBuilder.Route(t, root, "Rota_Olho", new[]
            {
                new Vector2(-20f, -10f), new Vector2(20f, -10f), new Vector2(20f, 15f), new Vector2(-20f, 15f)
            });
            CreateOlho(root, IlhaTesteBuilder.Ground(t, -20f, -10f) + Vector3.up * 12f, rotaOlho);

            // Vespas pousadas no telhado da nave da igreja (topo a 7 m do chão).
            float roof = IlhaTesteBuilder.Ground(t, 32f, 0f).y + 7f + 0.4f;
            CreateVespa(root, "Vespa_1", new Vector3(30f, roof, -2f), 180f);
            CreateVespa(root, "Vespa_2", new Vector3(34f, roof, 2f), 270f);

            // Brasa: atrás da fileira norte, por cima do território da Matilha.
            var rotaBrasa = InimigosBuilder.Route(t, root, "Rota_Brasa", new[]
            {
                new Vector2(-15f, 30f), new Vector2(20f, 30f)
            });
            CreateBrasa(root, IlhaTesteBuilder.Ground(t, -15f, 30f) + Vector3.up * 8f, rotaBrasa);
        }

        // ---------- Tipos ----------

        internal static void CreateOlho(Transform parent, Vector3 position, Transform[] route)
        {
            var go = Base("Olho", parent, position, 0.5f, out var body, out var motor);
            Frame(body, 0.7f, 0.18f, 0.32f);
            var lens = InimigosBuilder.Part(PrimitiveType.Sphere, "Lente", body, new Vector3(0f, -0.15f, 0f), Vector3.one * 0.22f, lente);
            lens.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Holofote: o cone de luz é o cone de visão (R7). Sombra ligada para o telhado "cortar" a luz.
            var spotGo = new GameObject("Holofote");
            spotGo.transform.SetParent(go.transform, false);
            spotGo.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            spotGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 28f;
            spot.spotAngle = 80f;
            spot.intensity = 60f;
            spot.shadows = LightShadows.Soft;

            Health(go, 20f, fogo: 1f, explosao: 1f);
            Flash(go, body);
            var ai = go.AddComponent<OlhoAI>();
            var so = new SerializedObject(ai);
            InimigosBuilder.SetArray(so.FindProperty("waypoints"), route);
            so.FindProperty("spotlight").objectReferenceValue = spot;
            so.FindProperty("alertIndicator").objectReferenceValue = lens.GetComponent<Renderer>();
            InimigosBuilder.SetLoot(so.FindProperty("loot"), (ResourceType.Componente, 1, 1f), (ResourceType.Sucata, 1, 1f));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void CreateVespa(Transform parent, string name, Vector3 position, float yaw)
        {
            var go = Base(name, parent, position, 0.35f, out var body, out var motor);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            InimigosBuilder.Part(PrimitiveType.Cube, "Carga", body, new Vector3(0f, -0.05f, 0.1f), new Vector3(0.22f, 0.16f, 0.4f), laranja);
            Frame(body, 0.4f, 0.12f, 0.2f);
            var eye = InimigosBuilder.Part(PrimitiveType.Sphere, "Olho", body, new Vector3(0f, 0f, 0.3f), Vector3.one * 0.1f, lente);
            eye.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var light = StatusLight(go, 4f);

            Health(go, 10f, fogo: 1f, explosao: 1f);
            Flash(go, body);
            var ai = go.AddComponent<VespaAI>();
            var so = new SerializedObject(ai);
            so.FindProperty("alertIndicator").objectReferenceValue = eye.GetComponent<Renderer>();
            so.FindProperty("statusLight").objectReferenceValue = light;
            InimigosBuilder.SetLoot(so.FindProperty("loot"), (ResourceType.Componente, 1, 1f));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void CreateBrasa(Transform parent, Vector3 position, Transform[] route)
        {
            var go = Base("Brasa", parent, position, 0.6f, out var body, out var motor);
            Frame(body, 1.0f, 0.3f, 0.45f);
            InimigosBuilder.Part(PrimitiveType.Cylinder, "Tanque", body, new Vector3(0f, -0.3f, 0f), new Vector3(0.5f, 0.2f, 0.5f), laranja);
            var eye = InimigosBuilder.Part(PrimitiveType.Sphere, "Olho", body, new Vector3(0f, 0f, 0.5f), Vector3.one * 0.15f, lente);
            eye.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var light = StatusLight(go, 6f);

            // R15: imune a fogo, vida 60.
            Health(go, 60f, fogo: 0f, explosao: 1.5f);
            Flash(go, body);
            var ai = go.AddComponent<BrasaAI>();
            var so = new SerializedObject(ai);
            InimigosBuilder.SetArray(so.FindProperty("waypoints"), route);
            so.FindProperty("alertIndicator").objectReferenceValue = eye.GetComponent<Renderer>();
            so.FindProperty("statusLight").objectReferenceValue = light;
            InimigosBuilder.SetLoot(so.FindProperty("loot"), (ResourceType.Componente, 1, 1f), (ResourceType.Alcool, 1, 1f));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Peças comuns ----------

        /// <summary>Raiz com collider, motor e rede. O "Corpo" é filho para poder inclinar sem mexer na raiz.</summary>
        private static GameObject Base(string name, Transform parent, Vector3 position, float radius,
            out Transform body, out DroneMotor motor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = position;

            var col = go.AddComponent<SphereCollider>();
            col.radius = radius;

            motor = go.AddComponent<DroneMotor>();
            go.AddComponent<HiveMember>();

            body = new GameObject("Corpo").transform;
            body.SetParent(go.transform, false);
            return go;
        }

        /// <summary>Chassi em X com quatro hélices. Liga corpo e hélices no motor.</summary>
        private static void Frame(Transform body, float size, float height, float rotorSize)
        {
            InimigosBuilder.Part(PrimitiveType.Cube, "Chassi", body, Vector3.zero, new Vector3(size * 0.5f, height, size * 0.5f), robo);
            var rotors = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + 90f * i) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * size * 0.6f;
                var arm = InimigosBuilder.Part(PrimitiveType.Cube, "Braco", body, p * 0.5f, new Vector3(0.06f, 0.05f, size * 0.6f), robo);
                arm.transform.localRotation = Quaternion.LookRotation(p);
                var rotor = InimigosBuilder.Part(PrimitiveType.Cylinder, "Helice", body, p + Vector3.up * height * 0.5f,
                    new Vector3(rotorSize, 0.01f, rotorSize * 0.25f), robo);
                rotor.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                rotors[i] = rotor.transform;
            }

            var motor = body.GetComponentInParent<DroneMotor>();
            var so = new SerializedObject(motor);
            so.FindProperty("body").objectReferenceValue = body;
            InimigosBuilder.SetArray(so.FindProperty("rotors"), rotors);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Light StatusLight(GameObject go, float range)
        {
            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = 2f;
            return light;
        }

        private static void Health(GameObject go, float max, float fogo, float explosao)
        {
            var health = go.AddComponent<Health>();
            var so = new SerializedObject(health);
            so.FindProperty("maxHealth").floatValue = max;
            so.FindProperty("multipliers.fogo").floatValue = fogo;
            so.FindProperty("multipliers.explosao").floatValue = explosao;
            so.FindProperty("multipliers.eletrico").floatValue = 0f; // elétrico = PEM, não dano
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Flash(GameObject go, Transform body)
        {
            var flash = go.AddComponent<HitFlash>();
            var so = new SerializedObject(flash);
            var renderers = body.GetComponentsInChildren<Renderer>();
            InimigosBuilder.SetArray(so.FindProperty("renderers"), renderers);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
