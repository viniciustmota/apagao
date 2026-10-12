using System.Collections.Generic;
using UnityEngine;

namespace Apagao.Systems
{
    /// <summary>
    /// Mato alto (spec 22 R6): dentro da área, a visibilidade do Caio para as máquinas terrestres cai para ×0,4.
    /// Não bloqueia movimento. A térmica dos drones ignora o mato (spec 15 R5).
    /// </summary>
    public class VegetationPatch : MonoBehaviour
    {
        [SerializeField] private Vector3 size = new Vector3(6f, 1.2f, 6f);

        private static readonly List<VegetationPatch> all = new List<VegetationPatch>();
        private Bounds bounds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        private void OnEnable()
        {
            bounds = new Bounds(transform.position + Vector3.up * size.y * 0.5f, size);
            all.Add(this);
        }

        private void OnDisable() => all.Remove(this);

        public static bool Contains(Vector3 point)
        {
            point.y += 0.3f; // pés no chão contam
            foreach (var v in all)
                if (v.bounds.Contains(point)) return true;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 0.3f, 0.4f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * size.y * 0.5f, size);
        }
    }
}
