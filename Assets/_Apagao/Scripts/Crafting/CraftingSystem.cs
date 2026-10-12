using System.Collections;
using Apagao.Combat;
using Apagao.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Apagao.Crafting
{
    public enum CraftResult { BombaRuido, Molotov, Kit, CargaPem, Isca, Municao, AfiarFacao }

    public class Recipe
    {
        public string Name;
        public ResourceCost[] Cost;
        public bool NeedsBench;
        public float Time;
        public CraftResult Result;
    }

    /// <summary>Receitas do spec 12. Vão para ScriptableObject quando o balanceamento estabilizar.</summary>
    public static class RecipeBook
    {
        public static readonly Recipe[] All =
        {
            new Recipe { Name = "Bomba de ruído", Result = CraftResult.BombaRuido, Time = 1.5f,
                Cost = new[] { new ResourceCost(ResourceType.Sucata, 1), new ResourceCost(ResourceType.Polvora, 1) } },
            new Recipe { Name = "Molotov", Result = CraftResult.Molotov, Time = 1.5f,
                Cost = new[] { new ResourceCost(ResourceType.Pano, 1), new ResourceCost(ResourceType.Alcool, 1) } },
            new Recipe { Name = "Kit de primeiros socorros", Result = CraftResult.Kit, Time = 1.5f,
                Cost = new[] { new ResourceCost(ResourceType.Pano, 2), new ResourceCost(ResourceType.Alcool, 1) } },
            new Recipe { Name = "Afiar facão", Result = CraftResult.AfiarFacao, Time = 2f,
                Cost = new[] { new ResourceCost(ResourceType.Sucata, 1) } },
            new Recipe { Name = "Carga de PEM", Result = CraftResult.CargaPem, Time = 1.5f, NeedsBench = true,
                Cost = new[] { new ResourceCost(ResourceType.Componente, 2), new ResourceCost(ResourceType.Sucata, 1) } },
            new Recipe { Name = "Isca elétrica", Result = CraftResult.Isca, Time = 1.5f, NeedsBench = true,
                Cost = new[] { new ResourceCost(ResourceType.Componente, 1), new ResourceCost(ResourceType.Sucata, 2) } },
            new Recipe { Name = "Munição de revólver (3)", Result = CraftResult.Municao, Time = 1.5f, NeedsBench = true,
                Cost = new[] { new ResourceCost(ResourceType.Sucata, 1), new ResourceCost(ResourceType.Polvora, 1) } },
        };
    }

    /// <summary>
    /// Crafting (spec 12 R4, R5): segurar Tab abre o menu; com ele aberto, 1–7 fabrica.
    /// O tempo não para. Receitas elétricas só perto de uma bancada.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class CraftingSystem : MonoBehaviour
    {
        [SerializeField] private float benchRange = 2.5f;
        [SerializeField] private float sharpenAmount = 50f;
        [SerializeField] private int ammoPerCraft = 3;

        public bool MenuOpen { get; private set; }
        public bool IsCrafting { get; private set; }
        public float CraftProgress { get; private set; }
        public Recipe CurrentRecipe { get; private set; }
        public bool NearBench => Workbench.AnyWithin(transform.position, benchRange);
        public string LastMessage { get; private set; } = "";
        public float LastMessageTime { get; private set; } = float.NegativeInfinity;

        /// <summary>(receita, feita perto de bancada). O checkpoint usa para salvar ao usar a bancada (spec 18 R4).</summary>
        public event System.Action<Recipe, bool> Crafted;

        private Inventory inventory;
        private PlayerController player;
        private Health health;
        private Pem pem;
        private WeaponHolder weapons;
        private Facao facao;

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            player = GetComponent<PlayerController>();
            health = GetComponent<Health>();
            pem = GetComponent<Pem>();
            weapons = GetComponent<WeaponHolder>();
            facao = GetComponent<Facao>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            bool dead = health != null && health.IsDead;
            MenuOpen = kb != null && kb.tabKey.isPressed && !dead && (player == null || !player.InputLocked);
            if (!MenuOpen || IsCrafting) return;

            for (int i = 0; i < RecipeBook.All.Length && i < 9; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame)
                    TryCraft(RecipeBook.All[i]);
        }

        public bool CanCraft(Recipe recipe, out string reason)
        {
            reason = null;
            if (recipe.NeedsBench && !NearBench) { reason = "Precisa de uma bancada"; return false; }
            if (!inventory.Has(recipe.Cost)) { reason = "Faltam recursos"; return false; }
            if (!CanReceive(recipe.Result)) { reason = "Já está no limite"; return false; }
            return true;
        }

        private bool CanReceive(CraftResult result) => result switch
        {
            CraftResult.BombaRuido => inventory.GetItem(ItemType.BombaRuido) < inventory.ItemLimit(ItemType.BombaRuido),
            CraftResult.Molotov => inventory.GetItem(ItemType.Molotov) < inventory.ItemLimit(ItemType.Molotov),
            CraftResult.Kit => inventory.GetItem(ItemType.Kit) < inventory.ItemLimit(ItemType.Kit),
            CraftResult.Isca => inventory.GetItem(ItemType.Isca) < inventory.ItemLimit(ItemType.Isca),
            CraftResult.CargaPem => pem != null && pem.Charges < pem.MaxCharges,
            CraftResult.Municao => weapons != null && weapons.ReserveRoom(weapons.Revolver) > 0,
            CraftResult.AfiarFacao => facao != null && facao.DurabilityNormalized < 1f,
            _ => false
        };

        private void TryCraft(Recipe recipe)
        {
            if (!CanCraft(recipe, out string reason))
            {
                Message(reason);
                return;
            }
            StartCoroutine(Craft(recipe));
        }

        private IEnumerator Craft(Recipe recipe)
        {
            IsCrafting = true;
            CurrentRecipe = recipe;
            inventory.Spend(recipe.Cost);

            for (float t = 0f; t < recipe.Time; t += Time.deltaTime)
            {
                CraftProgress = t / recipe.Time;
                yield return null;
            }

            Apply(recipe.Result);
            Message($"Fabricado: {recipe.Name}");
            Crafted?.Invoke(recipe, NearBench);
            CraftProgress = 0f;
            CurrentRecipe = null;
            IsCrafting = false;
        }

        private void Apply(CraftResult result)
        {
            switch (result)
            {
                case CraftResult.BombaRuido: inventory.AddItem(ItemType.BombaRuido); break;
                case CraftResult.Molotov: inventory.AddItem(ItemType.Molotov); break;
                case CraftResult.Kit: inventory.AddItem(ItemType.Kit); break;
                case CraftResult.Isca: inventory.AddItem(ItemType.Isca); break;
                case CraftResult.CargaPem: pem.AddCharge(); break;
                case CraftResult.Municao: weapons.AddReserve(weapons.Revolver, ammoPerCraft); break;
                case CraftResult.AfiarFacao: facao.Sharpen(sharpenAmount); break;
            }
        }

        private void Message(string text)
        {
            LastMessage = text;
            LastMessageTime = Time.time;
        }
    }
}
