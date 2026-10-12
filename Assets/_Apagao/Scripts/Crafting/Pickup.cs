using System;
using Apagao.Systems;
using UnityEngine;

namespace Apagao.Crafting
{
    /// <summary>Entrada de saque de uma máquina destruída (spec 12 R3).</summary>
    [Serializable]
    public struct LootEntry
    {
        public ResourceType type;
        public int amount;
        [Range(0f, 1f)] public float chance;
    }

    /// <summary>Recurso no chão. Segurar E por 0,5 s coleta (spec 12 R2).</summary>
    public class Pickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ResourceType resource;
        [SerializeField] private int amount = 1;

        public string Prompt => $"Coletar {amount} {CraftingNames.Of(resource)}";
        public float HoldTime => 0.5f;
        public Vector3 Position => transform.position;

        private Transform visual;
        private float phase;

        private void OnEnable() => InteractableRegistry.Register(this);
        private void OnDisable() => InteractableRegistry.Unregister(this);

        private void Start()
        {
            phase = UnityEngine.Random.value * 10f;
            BuildVisual();
        }

        private void Update()
        {
            if (visual == null) return;
            visual.localPosition = Vector3.up * (0.25f + Mathf.Sin(Time.time * 2f + phase) * 0.05f);
            visual.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
        }

        public bool CanInteract(GameObject who)
        {
            var inv = who.GetComponent<Inventory>();
            return inv != null && inv.Get(resource) < inv.Limit(resource);
        }

        public void Interact(GameObject who)
        {
            var inv = who.GetComponent<Inventory>();
            if (inv == null) return;
            amount -= inv.Add(resource, amount);
            if (amount <= 0) Destroy(gameObject);
        }

        private void BuildVisual()
        {
            var type = resource switch
            {
                ResourceType.Alcool => PrimitiveType.Cylinder,
                ResourceType.Polvora => PrimitiveType.Cylinder,
                ResourceType.Componente => PrimitiveType.Cube,
                ResourceType.Pano => PrimitiveType.Cube,
                _ => PrimitiveType.Cube
            };
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.name = "Visual";
            visual = go.transform;
            visual.SetParent(transform, false);
            visual.localScale = resource switch
            {
                ResourceType.Alcool => new Vector3(0.15f, 0.2f, 0.15f),
                ResourceType.Polvora => new Vector3(0.2f, 0.12f, 0.2f),
                ResourceType.Pano => new Vector3(0.35f, 0.08f, 0.3f),
                ResourceType.Componente => new Vector3(0.2f, 0.06f, 0.15f),
                _ => new Vector3(0.3f, 0.2f, 0.25f)
            };

            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", CraftingNames.ColorOf(resource));
            go.GetComponent<Renderer>().SetPropertyBlock(block);
        }

        /// <summary>Cria um recurso no chão (saque de máquinas).</summary>
        public static Pickup Spawn(ResourceType type, int amount, Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                position = hit.point;

            var go = new GameObject($"Item_{type}");
            go.transform.position = position;
            var pickup = go.AddComponent<Pickup>();
            pickup.resource = type;
            pickup.amount = amount;
            return pickup;
        }

        /// <summary>Usado pelos builders de cena.</summary>
        public void Configure(ResourceType type, int count)
        {
            resource = type;
            amount = count;
        }
    }
}
