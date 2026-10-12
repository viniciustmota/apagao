using System.Collections.Generic;
using UnityEngine;

namespace Apagao.Crafting
{
    /// <summary>Bancada (spec 12 R5): perto dela, as receitas elétricas ficam disponíveis no menu de crafting.</summary>
    public class Workbench : MonoBehaviour
    {
        private static readonly List<Workbench> all = new List<Workbench>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        public static bool AnyWithin(Vector3 point, float range)
        {
            float r2 = range * range;
            foreach (var b in all)
                if ((b.transform.position - point).sqrMagnitude <= r2) return true;
            return false;
        }
    }
}
