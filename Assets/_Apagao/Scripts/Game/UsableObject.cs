using Apagao.Systems;
using UnityEngine;

namespace Apagao.Game
{
    /// <summary>Objeto de objetivo que o Caio usa segurando E (ex.: o rádio da estação).</summary>
    public class UsableObject : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Usar";
        [SerializeField] private float holdTime = 1.5f;
        [Tooltip("Só pode ser usado quando for o alvo do objetivo atual.")]
        [SerializeField] private bool onlyWhenObjective = true;

        public bool Used { get; private set; }
        public string Prompt => prompt;
        public float HoldTime => holdTime;
        public Vector3 Position => transform.position;

        private void OnEnable() => InteractableRegistry.Register(this);
        private void OnDisable() => InteractableRegistry.Unregister(this);

        public bool CanInteract(GameObject who)
        {
            if (Used) return false;
            if (!onlyWhenObjective) return true;
            var current = Campaign.Instance != null ? Campaign.Instance.Current : null;
            return current != null && current.target == transform;
        }

        public void Interact(GameObject who) => Used = true;
    }
}
