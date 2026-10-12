using Apagao.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apagao.EditorTools
{
    /// <summary>
    /// Corpo low-poly do Caio montado com primitivas: eletricista com camisa de trabalho laranja,
    /// calça jeans, bota, boné verde-oliva e mochila de reservista. Cada articulação é um pivô vazio
    /// (o mesh fica pendurado nele), para o <see cref="CaioAnimator"/> girar.
    /// </summary>
    public static class CaioModelBuilder
    {
        private static Material pele, camisa, calca, bota, cabelo, oliva, couro, metal, olho;

        public static Transform Build(Transform caio)
        {
            pele = IlhaTesteBuilder.Mat("Caio_Pele", new Color(0.55f, 0.37f, 0.26f));
            camisa = IlhaTesteBuilder.Mat("Caio", new Color(0.90f, 0.45f, 0.10f));
            calca = IlhaTesteBuilder.Mat("Caio_Calca", new Color(0.18f, 0.25f, 0.38f));
            bota = IlhaTesteBuilder.Mat("Caio_Bota", new Color(0.16f, 0.12f, 0.09f));
            cabelo = IlhaTesteBuilder.Mat("Caio_Cabelo", new Color(0.07f, 0.05f, 0.04f));
            oliva = IlhaTesteBuilder.Mat("Caio_Oliva", new Color(0.30f, 0.33f, 0.19f));
            couro = IlhaTesteBuilder.Mat("Caio_Couro", new Color(0.36f, 0.22f, 0.12f));
            metal = IlhaTesteBuilder.Mat("Caio_Metal", new Color(0.25f, 0.26f, 0.28f), 0.7f);
            olho = IlhaTesteBuilder.Mat("Caio_Olho", new Color(0.05f, 0.04f, 0.04f));

            var rig = Joint("Rig", caio, Vector3.zero);

            // Quadril e tronco.
            var hips = Joint("Hips", rig, new Vector3(0f, 0.95f, 0f));
            Mesh("Pelve", hips, new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.18f, 0.21f), calca);
            Mesh("Cinto", hips, new Vector3(0f, 0.08f, 0f), new Vector3(0.36f, 0.05f, 0.23f), couro);
            Mesh("Bolsa_Ferramentas", hips, new Vector3(0.2f, 0.0f, 0.02f), new Vector3(0.07f, 0.16f, 0.14f), couro);
            Mesh("Alicate", hips, new Vector3(0.2f, 0.1f, 0.05f), new Vector3(0.03f, 0.08f, 0.02f), metal);

            var spine = Joint("Spine", hips, new Vector3(0f, 0.08f, 0f));
            Mesh("Tronco", spine, new Vector3(0f, 0.27f, 0f), new Vector3(0.4f, 0.5f, 0.23f), camisa);
            Mesh("Gola", spine, new Vector3(0f, 0.51f, 0.02f), new Vector3(0.22f, 0.05f, 0.18f), camisa);
            Mesh("Bolso_Peito", spine, new Vector3(-0.1f, 0.38f, 0.12f), new Vector3(0.1f, 0.09f, 0.02f), IlhaTesteBuilder.Mat("Caio_Bolso", new Color(0.78f, 0.38f, 0.08f)));
            Mesh("Faixa_Refletiva", spine, new Vector3(0f, 0.2f, 0f), new Vector3(0.41f, 0.035f, 0.235f), IlhaTesteBuilder.Mat("Caio_Faixa", new Color(0.85f, 0.85f, 0.8f), 0.6f));
            Mesh("Mochila", spine, new Vector3(0f, 0.3f, -0.19f), new Vector3(0.3f, 0.38f, 0.14f), oliva);
            Mesh("Mochila_Bolso", spine, new Vector3(0f, 0.18f, -0.27f), new Vector3(0.22f, 0.14f, 0.04f), oliva);
            Mesh("Alca_E", spine, new Vector3(-0.12f, 0.33f, 0.115f), new Vector3(0.05f, 0.34f, 0.02f), oliva);
            Mesh("Alca_D", spine, new Vector3(0.12f, 0.33f, 0.115f), new Vector3(0.05f, 0.34f, 0.02f), oliva);

            // Cabeça.
            var head = Joint("Head", spine, new Vector3(0f, 0.54f, 0.01f));
            Mesh("Pescoco", head, new Vector3(0f, 0.03f, 0f), new Vector3(0.1f, 0.08f, 0.1f), pele);
            Mesh("Cabeca", head, new Vector3(0f, 0.17f, 0.01f), new Vector3(0.21f, 0.24f, 0.23f), pele);
            Mesh("Cabelo", head, new Vector3(0f, 0.26f, -0.01f), new Vector3(0.22f, 0.07f, 0.24f), cabelo);
            Mesh("Bone", head, new Vector3(0f, 0.3f, 0f), new Vector3(0.23f, 0.06f, 0.25f), oliva);
            Mesh("Aba", head, new Vector3(0f, 0.28f, 0.16f), new Vector3(0.2f, 0.02f, 0.11f), oliva);
            Mesh("Nariz", head, new Vector3(0f, 0.15f, 0.13f), new Vector3(0.04f, 0.06f, 0.04f), pele);
            Mesh("Olho_E", head, new Vector3(-0.05f, 0.19f, 0.12f), new Vector3(0.03f, 0.025f, 0.01f), olho);
            Mesh("Olho_D", head, new Vector3(0.05f, 0.19f, 0.12f), new Vector3(0.03f, 0.025f, 0.01f), olho);
            Mesh("Barba", head, new Vector3(0f, 0.07f, 0.09f), new Vector3(0.17f, 0.06f, 0.06f), cabelo);
            Mesh("Orelha_E", head, new Vector3(-0.11f, 0.17f, 0f), new Vector3(0.03f, 0.06f, 0.05f), pele);
            Mesh("Orelha_D", head, new Vector3(0.11f, 0.17f, 0f), new Vector3(0.03f, 0.06f, 0.05f), pele);

            // Braços: ombro → cotovelo → mão. Manga curta, antebraço de pele.
            foreach (var side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "L" : "R";
                var shoulder = Joint("Shoulder" + s, spine, new Vector3(0.25f * side, 0.47f, 0f));
                Mesh("Manga", shoulder, new Vector3(0f, -0.08f, 0f), new Vector3(0.14f, 0.17f, 0.14f), camisa);
                Mesh("Braco", shoulder, new Vector3(0f, -0.2f, 0f), new Vector3(0.1f, 0.22f, 0.1f), pele);
                var elbow = Joint("Elbow" + s, shoulder, new Vector3(0f, -0.3f, 0f));
                Mesh("Antebraco", elbow, new Vector3(0f, -0.13f, 0f), new Vector3(0.09f, 0.26f, 0.09f), pele);
                var hand = Joint("Hand" + s, elbow, new Vector3(0f, -0.28f, 0f));
                Mesh("Mao", hand, new Vector3(0f, -0.04f, 0.01f), new Vector3(0.08f, 0.1f, 0.06f), pele);
                if (side < 0) Mesh("Relogio", elbow, new Vector3(0f, -0.24f, 0f), new Vector3(0.095f, 0.03f, 0.095f), metal);
            }

            // Armas na mão direita (o CaioAnimator liga a da vez).
            var handR = Find(rig, "HandR");
            var revolver = Joint("Arma_Revolver", handR, new Vector3(0f, -0.06f, 0.04f));
            Mesh("Corpo", revolver, new Vector3(0f, 0f, 0.08f), new Vector3(0.04f, 0.07f, 0.2f), metal);
            Mesh("Cabo", revolver, new Vector3(0f, -0.06f, -0.01f), new Vector3(0.035f, 0.09f, 0.05f), couro);
            var espingarda = Joint("Arma_Espingarda", handR, new Vector3(0f, -0.06f, 0.04f));
            Mesh("Cano", espingarda, new Vector3(0f, 0.01f, 0.35f), new Vector3(0.05f, 0.05f, 0.75f), metal);
            Mesh("Coronha", espingarda, new Vector3(0f, -0.02f, -0.12f), new Vector3(0.05f, 0.09f, 0.28f), couro);
            var pem = Joint("Arma_PEM", handR, new Vector3(0f, -0.06f, 0.04f));
            Mesh("Caixa", pem, new Vector3(0f, 0f, 0.07f), new Vector3(0.1f, 0.08f, 0.16f), oliva);
            Mesh("Bobina", pem, new Vector3(0f, 0f, 0.17f), new Vector3(0.07f, 0.07f, 0.05f), InimigosBuilder.Emissive("Caio_PEM", new Color(0.3f, 0.8f, 1f), 2f));

            // Cano alinhado com o antebraço (o +Z da arma aponta para onde a mão aponta).
            foreach (var arma in new[] { revolver, espingarda, pem }) arma.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Pernas: quadril → joelho → tornozelo.
            foreach (var side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "L" : "R";
                var hip = Joint("Hip" + s, hips, new Vector3(0.1f * side, -0.05f, 0f));
                Mesh("Coxa", hip, new Vector3(0f, -0.22f, 0f), new Vector3(0.15f, 0.45f, 0.16f), calca);
                var knee = Joint("Knee" + s, hip, new Vector3(0f, -0.45f, 0f));
                Mesh("Canela", knee, new Vector3(0f, -0.2f, 0f), new Vector3(0.13f, 0.4f, 0.14f), calca);
                var ankle = Joint("Ankle" + s, knee, new Vector3(0f, -0.43f, 0f));
                Mesh("Bota", ankle, new Vector3(0f, 0.04f, 0.04f), new Vector3(0.13f, 0.12f, 0.26f), bota);
            }

            caio.gameObject.AddComponent<CaioAnimator>();
            return rig;
        }

        private static Transform Joint(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        private static void Mesh(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // a colisão é o CharacterController
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var f = Find(c, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
