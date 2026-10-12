using UnityEngine;

namespace Apagao.Player
{
    /// <summary>Limite da ilha (spec 22 R8): no mar fundo, a corrente empurra o Caio de volta para a costa.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class SeaCurrent : MonoBehaviour
    {
        [SerializeField] private Vector3 islandCenter = Vector3.zero;
        [SerializeField] private float waterLevel = 4f;
        [Tooltip("Profundidade a partir da qual a corrente puxa.")]
        [SerializeField] private float deepWater = 1.3f;
        [SerializeField] private float pushSpeed = 4f;

        public bool Pushing { get; private set; }

        private CharacterController controller;

        private void Awake() => controller = GetComponent<CharacterController>();

        private void Update()
        {
            Pushing = controller.enabled && transform.position.y < waterLevel - deepWater;
            if (!Pushing) return;
            Vector3 toCenter = islandCenter - transform.position;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude < 1f) return;
            controller.Move(toCenter.normalized * pushSpeed * Time.deltaTime);
        }
    }
}
