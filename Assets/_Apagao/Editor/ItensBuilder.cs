using Apagao.Crafting;
using UnityEngine;

namespace Apagao.EditorTools
{
    /// <summary>Espalha recursos pela vila e coloca uma bancada (spec 12). Chamado pelo "Criar Ilha de Teste".</summary>
    public static class ItensBuilder
    {
        public static void Populate(Terrain t)
        {
            var old = GameObject.Find("Itens");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Itens").transform;

            // Bancada perto do início, fora da rota das Sentinelas.
            var bancada = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bancada.name = "Bancada";
            bancada.transform.SetParent(root);
            Vector3 ground = IlhaTesteBuilder.Ground(t, -20f, -5f);
            bancada.transform.position = ground + Vector3.up * 0.45f;
            bancada.transform.localScale = new Vector3(1.6f, 0.9f, 0.8f);
            bancada.GetComponent<MeshRenderer>().sharedMaterial = IlhaTesteBuilder.Mat("Bancada", new Color(0.45f, 0.32f, 0.2f));
            bancada.isStatic = true;
            bancada.AddComponent<Workbench>();

            // Recursos: o básico perto do começo, os raros perto do perigo.
            Item(t, root, ResourceType.Sucata, 1, -18.5f, -3.5f);
            Item(t, root, ResourceType.Pano, 2, -12f, 2f);
            Item(t, root, ResourceType.Alcool, 1, -4f, -2.2f);
            Item(t, root, ResourceType.Polvora, 1, 3f, 2.4f);
            Item(t, root, ResourceType.Sucata, 2, 10f, -2.5f);
            Item(t, root, ResourceType.Pano, 1, 16f, 2.5f);
            Item(t, root, ResourceType.Alcool, 1, 22f, -4f);
            Item(t, root, ResourceType.Componente, 1, 27f, 6f);   // perto da igreja (Sentinela 2)
            Item(t, root, ResourceType.Polvora, 2, -6f, 14f);     // atrás das casas (Matilha)
            Item(t, root, ResourceType.Componente, 1, 8f, 21f);   // território da Matilha
        }

        private static void Item(Terrain t, Transform parent, ResourceType type, int amount, float x, float z)
        {
            var go = new GameObject($"Item_{type}");
            go.transform.SetParent(parent);
            go.transform.position = IlhaTesteBuilder.Ground(t, x, z);
            go.AddComponent<Apagao.Crafting.Pickup>().Configure(type, amount);
        }
    }
}
