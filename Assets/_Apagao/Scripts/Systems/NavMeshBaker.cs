using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Apagao.Systems
{
    /// <summary>
    /// Gera o NavMesh da área ao iniciar a cena, a partir dos colliders (terreno, casas, caixotes).
    /// Ignora o jogador e os inimigos. Não depende do pacote AI Navigation.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class NavMeshBaker : MonoBehaviour
    {
        [SerializeField] private Vector3 size = new Vector3(140f, 40f, 140f);
        [SerializeField] private int agentTypeId = 0;

        private NavMeshDataInstance instance;

        private void Awake() => Bake();

        public void Bake()
        {
            if (instance.valid) instance.Remove();

            var sources = new List<NavMeshBuildSource>();
            var worldBounds = new Bounds(transform.position, size);
            NavMeshBuilder.CollectSources(worldBounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => s.component != null && IsDynamic(s.component.gameObject));

            var settings = NavMesh.GetSettingsByID(agentTypeId);
            var localBounds = new Bounds(Vector3.zero, size);
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, localBounds, transform.position, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
        }

        private static bool IsDynamic(GameObject go) =>
            go.GetComponentInParent<CharacterController>() != null ||
            go.GetComponentInParent<NavMeshAgent>() != null ||
            go.GetComponentInParent<Rigidbody>() != null;

        private void OnDestroy()
        {
            if (instance.valid) instance.Remove();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
