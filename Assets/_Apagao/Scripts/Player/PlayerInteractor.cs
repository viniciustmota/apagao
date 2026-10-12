using Apagao.Combat;
using Apagao.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Apagao.Player
{
    /// <summary>
    /// Acha o interagível mais próximo e, com E, interage (segurando pelo tempo que ele pedir).
    /// O takedown tem prioridade: com um alvo de takedown por perto, o E é dele.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private float range = 2f;

        public IInteractable Current { get; private set; }
        public float HoldProgress { get; private set; }

        private PlayerController player;
        private TakedownSystem takedown;
        private Health health;
        private bool waitRelease;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            takedown = GetComponent<TakedownSystem>();
            health = GetComponent<Health>();
        }

        private void Update()
        {
            Current = FindNearest();
            var kb = Keyboard.current;
            bool holding = kb != null && kb.eKey.isPressed;

            if (!holding) waitRelease = false;
            if (Current == null || !holding || waitRelease)
            {
                HoldProgress = 0f;
                return;
            }

            HoldProgress = Current.HoldTime <= 0f ? 1f : HoldProgress + Time.deltaTime / Current.HoldTime;
            if (HoldProgress < 1f) return;

            Current.Interact(gameObject);
            HoldProgress = 0f;
            waitRelease = true; // um uso por aperto
        }

        private IInteractable FindNearest()
        {
            if (player.InputLocked || (health != null && health.IsDead)) return null;
            if (takedown != null && (takedown.Candidate != null || takedown.IsExecuting)) return null;

            IInteractable best = null;
            float bestDist = range * range;
            var all = InteractableRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var item = all[i];
                float d = (item.Position - transform.position).sqrMagnitude;
                if (d > bestDist || !item.CanInteract(gameObject)) continue;
                bestDist = d;
                best = item;
            }
            return best;
        }
    }
}
