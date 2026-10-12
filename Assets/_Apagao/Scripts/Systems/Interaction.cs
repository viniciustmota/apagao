using System.Collections.Generic;
using UnityEngine;

namespace Apagao.Systems
{
    /// <summary>Algo que o Caio usa com E: itens, bancada, portas (spec 12).</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        /// <summary>Segundos segurando E. 0 = instantâneo.</summary>
        float HoldTime { get; }
        Vector3 Position { get; }
        bool CanInteract(GameObject who);
        void Interact(GameObject who);
    }

    /// <summary>Lista global de interagíveis: dispensa colisores e física para achar o mais próximo.</summary>
    public static class InteractableRegistry
    {
        private static readonly List<IInteractable> all = new List<IInteractable>();

        public static IReadOnlyList<IInteractable> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        public static void Register(IInteractable i)
        {
            if (!all.Contains(i)) all.Add(i);
        }

        public static void Unregister(IInteractable i) => all.Remove(i);
    }
}
