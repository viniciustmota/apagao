using System;
using UnityEngine;

namespace Apagao.Crafting
{
    public enum ResourceType { Sucata, Componente, Pano, Alcool, Polvora }
    public enum ItemType { BombaRuido, Molotov, Isca, Kit }

    [Serializable]
    public struct ResourceCost
    {
        public ResourceType type;
        public int amount;

        public ResourceCost(ResourceType type, int amount)
        {
            this.type = type;
            this.amount = amount;
        }
    }

    public static class CraftingNames
    {
        public static string Of(ResourceType t) => t switch
        {
            ResourceType.Sucata => "Sucata",
            ResourceType.Componente => "Componente",
            ResourceType.Pano => "Pano",
            ResourceType.Alcool => "Álcool",
            ResourceType.Polvora => "Pólvora",
            _ => t.ToString()
        };

        public static string Of(ItemType t) => t switch
        {
            ItemType.BombaRuido => "Bomba de ruído",
            ItemType.Molotov => "Molotov",
            ItemType.Isca => "Isca elétrica",
            ItemType.Kit => "Kit de primeiros socorros",
            _ => t.ToString()
        };

        public static Color ColorOf(ResourceType t) => t switch
        {
            ResourceType.Sucata => new Color(0.55f, 0.55f, 0.58f),
            ResourceType.Componente => new Color(0.2f, 0.8f, 0.4f),
            ResourceType.Pano => new Color(0.9f, 0.88f, 0.8f),
            ResourceType.Alcool => new Color(0.4f, 0.6f, 0.95f),
            ResourceType.Polvora => new Color(0.25f, 0.2f, 0.2f),
            _ => Color.white
        };
    }

    /// <summary>
    /// Recursos e itens do Caio (spec 12 R1). Limites fazem o jogador usar os itens em vez de acumular.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [Header("Limites")]
        [SerializeField] private int resourceLimit = 10;
        [SerializeField] private int componentLimit = 5;
        [SerializeField] private int throwableLimit = 3;
        [SerializeField] private int lureLimit = 2;
        [SerializeField] private int kitLimit = 2;

        [Header("Começa com (teste)")]
        [SerializeField] private int startSucata = 2;
        [SerializeField] private int startComponente = 1;
        [SerializeField] private int startPano = 1;
        [SerializeField] private int startAlcool = 1;
        [SerializeField] private int startPolvora = 1;
        [SerializeField] private int startBombas = 1;

        public event Action Changed;
        /// <summary>(recurso, quantidade) a cada coleta, para o HUD.</summary>
        public event Action<ResourceType, int> ResourceAdded;

        private readonly int[] resources = new int[5];
        private readonly int[] items = new int[4];

        private void Awake()
        {
            resources[(int)ResourceType.Sucata] = startSucata;
            resources[(int)ResourceType.Componente] = startComponente;
            resources[(int)ResourceType.Pano] = startPano;
            resources[(int)ResourceType.Alcool] = startAlcool;
            resources[(int)ResourceType.Polvora] = startPolvora;
            items[(int)ItemType.BombaRuido] = startBombas;
        }

        // ---------- Checkpoint (spec 18 R5) ----------

        public int[] SnapshotResources() => (int[])resources.Clone();
        public int[] SnapshotItems() => (int[])items.Clone();

        public void Restore(int[] savedResources, int[] savedItems)
        {
            if (savedResources != null)
                for (int i = 0; i < resources.Length && i < savedResources.Length; i++) resources[i] = savedResources[i];
            if (savedItems != null)
                for (int i = 0; i < items.Length && i < savedItems.Length; i++) items[i] = savedItems[i];
            Changed?.Invoke();
        }

        // ---------- Recursos ----------

        public int Get(ResourceType t) => resources[(int)t];
        public int Limit(ResourceType t) => t == ResourceType.Componente ? componentLimit : resourceLimit;

        /// <summary>Adiciona até o limite. Retorna quanto coube.</summary>
        public int Add(ResourceType t, int amount)
        {
            int added = Mathf.Clamp(amount, 0, Limit(t) - Get(t));
            if (added <= 0) return 0;
            resources[(int)t] += added;
            ResourceAdded?.Invoke(t, added);
            Changed?.Invoke();
            return added;
        }

        public bool Has(ResourceCost[] cost)
        {
            foreach (var c in cost)
                if (Get(c.type) < c.amount) return false;
            return true;
        }

        public bool Spend(ResourceCost[] cost)
        {
            if (!Has(cost)) return false;
            foreach (var c in cost) resources[(int)c.type] -= c.amount;
            Changed?.Invoke();
            return true;
        }

        // ---------- Itens ----------

        public int GetItem(ItemType t) => items[(int)t];

        public int ItemLimit(ItemType t) => t switch
        {
            ItemType.Isca => lureLimit,
            ItemType.Kit => kitLimit,
            _ => throwableLimit
        };

        public bool AddItem(ItemType t)
        {
            if (GetItem(t) >= ItemLimit(t)) return false;
            items[(int)t]++;
            Changed?.Invoke();
            return true;
        }

        public bool UseItem(ItemType t)
        {
            if (GetItem(t) <= 0) return false;
            items[(int)t]--;
            Changed?.Invoke();
            return true;
        }
    }
}
