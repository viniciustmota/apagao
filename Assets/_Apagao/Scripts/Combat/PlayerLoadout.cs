using System;
using UnityEngine;

namespace Apagao.Combat
{
    /// <summary>
    /// Quais armas o Caio já tem (spec 18): na campanha ele começa de mãos vazias e acha cada uma num ato.
    /// Na ilha de teste vem tudo liberado.
    /// </summary>
    public class PlayerLoadout : MonoBehaviour
    {
        [SerializeField] private bool facao = true;
        [SerializeField] private bool revolver = true;
        [SerializeField] private bool espingarda = true;
        [SerializeField] private bool pem = true;

        public event Action<WeaponSlot> Unlocked;

        public bool Has(WeaponSlot slot) => slot switch
        {
            WeaponSlot.Facao => facao,
            WeaponSlot.Revolver => revolver,
            WeaponSlot.Espingarda => espingarda,
            WeaponSlot.Pem => pem,
            _ => false
        };

        public void Unlock(WeaponSlot slot)
        {
            if (Has(slot)) return;
            switch (slot)
            {
                case WeaponSlot.Facao: facao = true; break;
                case WeaponSlot.Revolver: revolver = true; break;
                case WeaponSlot.Espingarda: espingarda = true; break;
                case WeaponSlot.Pem: pem = true; break;
            }
            Unlocked?.Invoke(slot);
        }

        /// <summary>Bits 0–3 = facão, revólver, espingarda, PEM (checkpoint).</summary>
        public int Mask
        {
            get => (facao ? 1 : 0) | (revolver ? 2 : 0) | (espingarda ? 4 : 0) | (pem ? 8 : 0);
            set
            {
                facao = (value & 1) != 0;
                revolver = (value & 2) != 0;
                espingarda = (value & 4) != 0;
                pem = (value & 8) != 0;
            }
        }

        /// <summary>Sem componente = tudo liberado.</summary>
        public static bool Allows(Component owner, WeaponSlot slot)
        {
            var l = owner.GetComponent<PlayerLoadout>();
            return l == null || l.Has(slot);
        }
    }
}
