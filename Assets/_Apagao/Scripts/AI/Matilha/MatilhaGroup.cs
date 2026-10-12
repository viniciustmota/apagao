using System.Collections.Generic;
using UnityEngine;

namespace Apagao.AI.Matilha
{
    public enum PackRole { Segurar, Flanquear }

    /// <summary>
    /// Coordena um grupo de 2 a 3 cães (spec 14 R1, R6): patrulha em formação atrás do líder,
    /// alerta conjunto e papéis em combate (um segura, os outros flanqueiam).
    /// </summary>
    public class MatilhaGroup : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float waypointPause = 2f;

        [Header("Formação")]
        [SerializeField] private float formationSpacing = 1.6f;

        public Transform[] Waypoints => waypoints;
        public float WaypointPause => waypointPause;

        private readonly List<MatilhaAI> members = new List<MatilhaAI>();

        private void Awake() => GetComponentsInChildren(members);

        private void CleanUp() => members.RemoveAll(m => m == null || m.IsDead);

        /// <summary>O primeiro cão vivo lidera a patrulha e segura em combate.</summary>
        public MatilhaAI Leader
        {
            get
            {
                CleanUp();
                return members.Count > 0 ? members[0] : null;
            }
        }

        /// <summary>Posição na formação: atrás do líder, alternando os lados.</summary>
        public Vector3 FormationPoint(MatilhaAI dog)
        {
            var leader = Leader;
            if (leader == null || leader == dog) return dog.transform.position;
            int index = members.IndexOf(dog);
            float side = index % 2 == 1 ? -1f : 1f;
            Vector3 local = new Vector3(side * formationSpacing, 0f, -formationSpacing * ((index + 1) / 2));
            return leader.transform.TransformPoint(local);
        }

        /// <summary>Papel em combate e lado do flanco (-1 ou +1).</summary>
        public PackRole RoleOf(MatilhaAI dog, out float side)
        {
            CleanUp();
            int index = members.IndexOf(dog);
            side = index % 2 == 1 ? -1f : 1f;
            return index <= 0 ? PackRole.Segurar : PackRole.Flanquear;
        }

        /// <summary>Um cão entrou em combate ou começou a rastrear: o grupo inteiro vai junto.</summary>
        public void Rally(MatilhaAI caller, Vector3 position, bool combat)
        {
            CleanUp();
            foreach (var m in members)
                if (m != caller) m.JoinPack(position, combat);
        }
    }
}
