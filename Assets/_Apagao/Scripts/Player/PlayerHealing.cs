using Apagao.Combat;
using Apagao.Crafting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Apagao.Player
{
    /// <summary>Kit de primeiros socorros (spec 08 R5): H usa, leva 2 s, cura 50. Levar dano interrompe.</summary>
    [RequireComponent(typeof(Health), typeof(Inventory))]
    public class PlayerHealing : MonoBehaviour
    {
        [SerializeField] private float healAmount = 50f;
        [SerializeField] private float useTime = 2f;

        public bool IsHealing { get; private set; }
        public float Progress { get; private set; }
        public string LastMessage { get; private set; } = "";
        public float LastMessageTime { get; private set; } = float.NegativeInfinity;

        private Health health;
        private Inventory inventory;
        private PlayerController player;

        private void Awake()
        {
            health = GetComponent<Health>();
            inventory = GetComponent<Inventory>();
            player = GetComponent<PlayerController>();
        }

        private void OnEnable() => health.Damaged += OnDamaged;
        private void OnDisable() => health.Damaged -= OnDamaged;

        private void OnDamaged(DamageInfo info, float amount)
        {
            if (!IsHealing) return;
            IsHealing = false;
            Progress = 0f;
            Message("Cura interrompida");
        }

        private void Update()
        {
            if (health.IsDead) { IsHealing = false; return; }

            var kb = Keyboard.current;
            if (!IsHealing && kb != null && kb.hKey.wasPressedThisFrame && (player == null || !player.InputLocked))
            {
                if (inventory.GetItem(ItemType.Kit) <= 0) Message("Sem kit — fabrique com Tab");
                else if (health.Current >= health.Max) Message("Vida cheia");
                else
                {
                    IsHealing = true;
                    Progress = 0f;
                }
            }

            if (!IsHealing) return;
            Progress += Time.deltaTime / useTime;
            if (Progress < 1f) return;

            inventory.UseItem(ItemType.Kit);
            health.Heal(healAmount);
            IsHealing = false;
            Progress = 0f;
        }

        private void Message(string text)
        {
            LastMessage = text;
            LastMessageTime = Time.time;
        }
    }
}
