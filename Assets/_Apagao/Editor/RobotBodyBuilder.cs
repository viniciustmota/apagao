using System.Collections.Generic;
using Apagao.AI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apagao.EditorTools
{
    /// <summary>
    /// Corpos articulados low-poly dos robôs (Sentinela, cão da Matilha, Brutamontes), montados com primitivas.
    /// Cada articulação é um pivô vazio; o <see cref="RobotAnimator"/> gira os pivôs pelo nome.
    /// </summary>
    public static class RobotBodyBuilder
    {
        public class Body
        {
            public Transform Root, Hips, Spine, Head, RightShoulder;
            /// <summary>Partes que viram carcaça (spec 05): soltas do corpo quando a máquina morre.</summary>
            public readonly List<Transform> Wreck = new List<Transform>();
            /// <summary>Renderers que piscam ao levar dano.</summary>
            public readonly List<Renderer> Flash = new List<Renderer>();
            public Transform Torso;
        }

        private static Material Armor(string name, Color c) => IlhaTesteBuilder.Mat(name, c, 0.55f);

        // ---------- Sentinela (2 m) ----------

        public static Body Sentinela(Transform root, Material robo, Material visor)
        {
            var b = new Body { Root = Joint("Rig", root, Vector3.zero) };
            var escuro = Armor("Robo_Escuro", new Color(0.12f, 0.13f, 0.15f));
            var placa = Armor("Sentinela_Placa", new Color(0.36f, 0.38f, 0.40f));

            b.Hips = Joint("Hips", b.Root, new Vector3(0f, 1.0f, 0f));
            M(b, "Pelve", b.Hips, Vector3.zero, new Vector3(0.36f, 0.2f, 0.24f), escuro);

            b.Spine = Joint("Spine", b.Hips, new Vector3(0f, 0.1f, 0f));
            b.Torso = M(b, "Tronco", b.Spine, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.55f, 0.3f), robo).transform;
            M(b, "Peitoral", b.Spine, new Vector3(0f, 0.38f, 0.16f), new Vector3(0.44f, 0.3f, 0.06f), placa);
            M(b, "Abdomen", b.Spine, new Vector3(0f, 0.06f, 0.02f), new Vector3(0.3f, 0.14f, 0.22f), escuro);
            M(b, "Bateria", b.Spine, new Vector3(0f, 0.32f, -0.21f), new Vector3(0.3f, 0.36f, 0.14f), escuro);
            M(b, "Bateria_Luz", b.Spine, new Vector3(0f, 0.32f, -0.285f), new Vector3(0.05f, 0.26f, 0.01f), visor, flash: false);

            b.Head = Joint("Head", b.Spine, new Vector3(0f, 0.6f, 0f));
            M(b, "Pescoco", b.Head, new Vector3(0f, 0.04f, 0f), new Vector3(0.12f, 0.1f, 0.12f), escuro);
            M(b, "Cabeca", b.Head, new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.26f, 0.32f), robo);
            M(b, "Visor", b.Head, new Vector3(0f, 0.21f, 0.165f), new Vector3(0.26f, 0.06f, 0.03f), visor, flash: false);
            M(b, "Antena", b.Head, new Vector3(0.1f, 0.38f, -0.06f), new Vector3(0.015f, 0.14f, 0.015f), escuro);

            foreach (var side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "L" : "R";
                var shoulder = Joint("Shoulder" + s, b.Spine, new Vector3(0.33f * side, 0.52f, 0f));
                M(b, "Ombreira", shoulder, new Vector3(0f, 0.02f, 0f), new Vector3(0.2f, 0.12f, 0.22f), placa);
                M(b, "Braco", shoulder, new Vector3(0f, -0.16f, 0f), new Vector3(0.11f, 0.3f, 0.11f), escuro);
                var elbow = Joint("Elbow" + s, shoulder, new Vector3(0f, -0.32f, 0f));
                M(b, "Antebraco", elbow, new Vector3(0f, -0.15f, 0f), new Vector3(0.13f, 0.3f, 0.13f), robo);
                M(b, "Mao", elbow, new Vector3(0f, -0.34f, 0f), new Vector3(0.1f, 0.1f, 0.1f), escuro);
                if (side > 0)
                {
                    b.RightShoulder = shoulder;
                    // Rifle preso no antebraço, cano na direção da mão.
                    var rifle = Joint("Rifle", elbow, new Vector3(0.08f, -0.2f, 0f));
                    rifle.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    M(b, "Rifle_Corpo", rifle, new Vector3(0f, 0f, 0.1f), new Vector3(0.07f, 0.11f, 0.5f), escuro);
                    M(b, "Rifle_Cano", rifle, new Vector3(0f, 0.02f, 0.42f), new Vector3(0.035f, 0.035f, 0.3f), escuro);
                }
            }

            foreach (var side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "L" : "R";
                var hip = Joint("Hip" + s, b.Hips, new Vector3(0.12f * side, -0.06f, 0f));
                M(b, "Coxa", hip, new Vector3(0f, -0.23f, 0f), new Vector3(0.16f, 0.44f, 0.18f), escuro);
                var knee = Joint("Knee" + s, hip, new Vector3(0f, -0.46f, 0f));
                M(b, "Joelheira", knee, new Vector3(0f, 0f, 0.08f), new Vector3(0.14f, 0.12f, 0.06f), placa);
                M(b, "Canela", knee, new Vector3(0f, -0.22f, 0f), new Vector3(0.14f, 0.42f, 0.16f), robo);
                M(b, "Pe", knee, new Vector3(0f, -0.46f, 0.05f), new Vector3(0.16f, 0.06f, 0.3f), escuro);
            }

            b.Wreck.Add(b.Spine);
            b.Wreck.Add(b.Hips);
            Animator(root, RobotAnimator.BodyKind.Bipede, stride: 1.3f, leg: 26f, arm: 14f, fullSpeed: 2f, head: true, rightArm: true, aim: true);
            return b;
        }

        // ---------- Cão da Matilha (quadrúpede, ~0,8 m) ----------

        public static Body Cao(Transform root, Material robo, Material visor)
        {
            var b = new Body { Root = Joint("Rig", root, Vector3.zero) };
            var escuro = Armor("Robo_Escuro", new Color(0.12f, 0.13f, 0.15f));
            var placa = Armor("Cao_Placa", new Color(0.4f, 0.36f, 0.3f));

            b.Spine = Joint("Corpo_Pivo", b.Root, new Vector3(0f, 0.66f, 0f));
            b.Torso = M(b, "Corpo", b.Spine, Vector3.zero, new Vector3(0.4f, 0.3f, 0.95f), robo).transform;
            M(b, "Costas", b.Spine, new Vector3(0f, 0.17f, -0.05f), new Vector3(0.34f, 0.06f, 0.7f), placa);
            M(b, "Cauda", b.Spine, new Vector3(0f, 0.12f, -0.55f), new Vector3(0.03f, 0.03f, 0.25f), escuro);

            b.Head = Joint("Cabeca", b.Spine, new Vector3(0f, 0.12f, 0.5f));
            M(b, "Pescoco", b.Head, new Vector3(0f, -0.02f, -0.02f), new Vector3(0.16f, 0.16f, 0.16f), escuro);
            M(b, "Cranio", b.Head, new Vector3(0f, 0.04f, 0.13f), new Vector3(0.26f, 0.22f, 0.32f), robo);
            M(b, "Mandibula", b.Head, new Vector3(0f, -0.08f, 0.2f), new Vector3(0.2f, 0.07f, 0.26f), escuro);
            M(b, "Visor", b.Head, new Vector3(0f, 0.08f, 0.29f), new Vector3(0.22f, 0.05f, 0.03f), visor, flash: false);
            M(b, "Orelha_E", b.Head, new Vector3(-0.09f, 0.18f, 0.06f), new Vector3(0.04f, 0.1f, 0.06f), escuro);
            M(b, "Orelha_D", b.Head, new Vector3(0.09f, 0.18f, 0.06f), new Vector3(0.04f, 0.1f, 0.06f), escuro);

            foreach (var (name, pos) in new[]
            {
                ("FL", new Vector3(-0.16f, 0.6f, 0.34f)), ("FR", new Vector3(0.16f, 0.6f, 0.34f)),
                ("BL", new Vector3(-0.16f, 0.6f, -0.34f)), ("BR", new Vector3(0.16f, 0.6f, -0.34f))
            })
            {
                var leg = Joint("Leg" + name, b.Root, pos);
                M(b, "Coxa", leg, new Vector3(0f, -0.14f, 0f), new Vector3(0.1f, 0.3f, 0.12f), escuro);
                var knee = Joint("Knee" + name, leg, new Vector3(0f, -0.28f, 0f));
                M(b, "Canela", knee, new Vector3(0f, -0.14f, 0f), new Vector3(0.07f, 0.3f, 0.08f), robo);
                M(b, "Pata", knee, new Vector3(0f, -0.3f, 0.02f), new Vector3(0.1f, 0.04f, 0.14f), escuro);
            }

            b.Wreck.Add(b.Spine);
            Animator(root, RobotAnimator.BodyKind.Quadrupede, stride: 1.1f, leg: 30f, arm: 0f, fullSpeed: 3f, head: false, rightArm: false, aim: false);
            return b;
        }

        // ---------- Brutamontes (2,8 m) ----------

        public static Body Brutamontes(Transform root, Material robo, Material visor)
        {
            var b = new Body { Root = Joint("Rig", root, Vector3.zero) };
            var escuro = Armor("Robo_Escuro", new Color(0.12f, 0.13f, 0.15f));
            var blindagem = IlhaTesteBuilder.Mat("Brutamontes_Blindagem", new Color(0.55f, 0.48f, 0.2f), 0.5f);
            var juntas = InimigosBuilder.Emissive("Brutamontes_Juntas", new Color(1f, 0.45f, 0.1f), 1.2f);

            b.Hips = Joint("Hips", b.Root, new Vector3(0f, 1.25f, 0f));
            M(b, "Pelve", b.Hips, Vector3.zero, new Vector3(0.8f, 0.35f, 0.55f), escuro);

            b.Spine = Joint("Spine", b.Hips, new Vector3(0f, 0.15f, 0f));
            b.Torso = M(b, "Tronco", b.Spine, new Vector3(0f, 0.55f, 0f), new Vector3(1.4f, 1.05f, 0.85f), robo).transform;
            M(b, "Blindagem", b.Spine, new Vector3(0f, 0.6f, 0.47f), new Vector3(1.3f, 0.95f, 0.14f), blindagem);
            M(b, "Listra_1", b.Spine, new Vector3(-0.35f, 0.6f, 0.545f), new Vector3(0.12f, 0.9f, 0.01f), escuro);
            M(b, "Listra_2", b.Spine, new Vector3(0.35f, 0.6f, 0.545f), new Vector3(0.12f, 0.9f, 0.01f), escuro);
            foreach (var x in new[] { -0.35f, 0.35f })
            {
                M(b, "Escape", b.Spine, new Vector3(x, 0.95f, -0.5f), new Vector3(0.14f, 0.5f, 0.14f), escuro);
                M(b, "Escape_Brasa", b.Spine, new Vector3(x, 1.21f, -0.5f), new Vector3(0.1f, 0.03f, 0.1f), juntas, flash: false);
            }

            b.Head = Joint("Head", b.Spine, new Vector3(0f, 1.08f, 0.2f));
            M(b, "Cabeca", b.Head, new Vector3(0f, 0.17f, 0f), new Vector3(0.55f, 0.38f, 0.55f), robo);
            M(b, "Visor", b.Head, new Vector3(0f, 0.2f, 0.28f), new Vector3(0.45f, 0.08f, 0.03f), visor, flash: false);
            M(b, "Grade", b.Head, new Vector3(0f, 0.06f, 0.28f), new Vector3(0.3f, 0.1f, 0.03f), escuro);

            foreach (var side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "L" : "R";
                // O ombro direito mantém o nome antigo: o BrutamontesAI ergue ele no soco.
                var shoulder = Joint(side < 0 ? "ShoulderL" : "Ombro_D", b.Spine, new Vector3(0.95f * side, 0.95f, 0f));
                M(b, "Ombreira", shoulder, new Vector3(0.05f * side, 0.05f, 0f), new Vector3(0.55f, 0.35f, 0.65f), blindagem);
                M(b, "Braco", shoulder, new Vector3(0f, -0.42f, 0f), new Vector3(0.42f, 0.75f, 0.45f), escuro);
                var elbow = Joint("Elbow" + s, shoulder, new Vector3(0f, -0.82f, 0f));
                M(b, "Cotovelo", elbow, Vector3.zero, Vector3.one * 0.32f, juntas, flash: false);
                M(b, "Antebraco", elbow, new Vector3(0f, -0.35f, 0f), new Vector3(0.55f, 0.7f, 0.58f), robo);
                M(b, "Punho", elbow, new Vector3(0f, -0.8f, 0.02f), new Vector3(0.6f, 0.38f, 0.6f), escuro);
                if (side > 0) b.RightShoulder = shoulder;
            }

            foreach (var side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "L" : "R";
                var hip = Joint("Hip" + s, b.Hips, new Vector3(0.38f * side, -0.1f, 0f));
                M(b, "Coxa", hip, new Vector3(0f, -0.3f, 0f), new Vector3(0.5f, 0.6f, 0.55f), escuro);
                var knee = Joint("Knee" + s, hip, new Vector3(0f, -0.6f, 0f));
                M(b, "Junta", knee, new Vector3(0f, 0f, -0.2f), Vector3.one * 0.3f, juntas, flash: false); // ponto fraco (molotov)
                M(b, "Canela", knee, new Vector3(0f, -0.27f, 0.02f), new Vector3(0.55f, 0.55f, 0.6f), robo);
                M(b, "Pe", knee, new Vector3(0f, -0.5f, 0.12f), new Vector3(0.6f, 0.12f, 0.85f), escuro);
            }

            b.Wreck.Add(b.Spine);
            b.Wreck.Add(b.Hips);
            Animator(root, RobotAnimator.BodyKind.Bipede, stride: 2.2f, leg: 22f, arm: 10f, fullSpeed: 2f, head: true, rightArm: false, aim: false);
            return b;
        }

        // ---------- Peças ----------

        private static void Animator(Transform root, RobotAnimator.BodyKind kind, float stride, float leg, float arm, float fullSpeed,
            bool head, bool rightArm, bool aim)
        {
            var anim = root.gameObject.AddComponent<RobotAnimator>();
            var so = new SerializedObject(anim);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("stride").floatValue = stride;
            so.FindProperty("legAmplitude").floatValue = leg;
            so.FindProperty("armAmplitude").floatValue = arm;
            so.FindProperty("fullSpeed").floatValue = fullSpeed;
            so.FindProperty("animateHead").boolValue = head;
            so.FindProperty("animateRightArm").boolValue = rightArm;
            so.FindProperty("aimRightArm").boolValue = aim;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Joint(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        private static GameObject M(Body b, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool flash = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // a colisão fica na raiz
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            if (flash) b.Flash.Add(r);
            return go;
        }
    }
}
