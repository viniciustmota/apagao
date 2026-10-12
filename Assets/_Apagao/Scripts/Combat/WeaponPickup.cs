using Apagao.Audio;
using Apagao.Systems;
using UnityEngine;

namespace Apagao.Combat
{
    /// <summary>Arma achada no mundo (facão do pai, revólver, espingarda, peças do PEM). Segurar E libera e equipa.</summary>
    public class WeaponPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private WeaponSlot slot = WeaponSlot.Facao;
        [SerializeField] private string prompt = "Pegar";
        [SerializeField] private float holdTime = 0.8f;
        [Tooltip("Balas extras de reserva (armas de fogo) ou cargas (PEM).")]
        [SerializeField] private int bonus = 0;

        public string Prompt => prompt;
        public float HoldTime => holdTime;
        public Vector3 Position => transform.position;

        private void OnEnable() => InteractableRegistry.Register(this);
        private void OnDisable() => InteractableRegistry.Unregister(this);

        private void Update() => transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);

        public bool CanInteract(GameObject who)
        {
            var loadout = who.GetComponent<PlayerLoadout>();
            return loadout == null || !loadout.Has(slot);
        }

        public void Interact(GameObject who)
        {
            var loadout = who.GetComponent<PlayerLoadout>();
            if (loadout != null) loadout.Unlock(slot);

            var holder = who.GetComponent<WeaponHolder>();
            if (holder != null)
            {
                if (slot == WeaponSlot.Revolver) holder.AddReserve(holder.Revolver, bonus);
                if (slot == WeaponSlot.Espingarda) holder.AddReserve(holder.Espingarda, bonus);
                holder.Equip(slot);
            }
            if (slot == WeaponSlot.Pem && who.TryGetComponent(out Pem pem)) pem.SetCharges(pem.Charges + Mathf.Max(1, bonus));

            Sfx.Play2D("recarga", 0.8f);
            SaveId.ReportGone(gameObject);
            Destroy(gameObject);
        }
    }
}
