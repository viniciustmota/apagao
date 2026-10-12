using Apagao.Player;
using Apagao.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Apagao.AI.Hive
{
    /// <summary>
    /// Antena da área (spec 07). Segurar E perto dela por 2 s derruba a rede: os robôs passam a agir sozinhos.
    /// No marco 7 o PEM também vai sabotar a antena.
    /// </summary>
    public class HiveAntenna : MonoBehaviour
    {
        [Tooltip("Vazio = usa a rede cuja área contém a antena.")]
        [SerializeField] private HiveNetwork network;
        [SerializeField] private float sabotageTime = 2f;
        [SerializeField] private float interactRange = 2f;

        [Header("Sabotagem")]
        [SerializeField] private float noiseRadius = 8f;
        [SerializeField] private float noiseIntensity = 0.5f;

        [Header("Visual")]
        [SerializeField] private Renderer beacon;
        [SerializeField] private Light beaconLight;

        public bool Sabotaged { get; private set; }
        public bool PlayerInRange { get; private set; }
        /// <summary>Progresso da sabotagem, de 0 a 1.</summary>
        public float Progress { get; private set; }

        private Transform player;
        private float baseIntensity;

        private void Start()
        {
            if (network == null) network = HiveNetwork.FindFor(transform.position);
            var pc = FindAnyObjectByType<PlayerController>();
            player = pc != null ? pc.transform : null;
            if (beaconLight != null) baseIntensity = beaconLight.intensity;
        }

        private void Update()
        {
            if (Sabotaged) return;

            if (beaconLight != null)
                beaconLight.intensity = baseIntensity * (0.4f + 0.6f * Mathf.PingPong(Time.time * 1.5f, 1f));

            if (player == null) return;
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            PlayerInRange = d.magnitude <= interactRange;

            var kb = Keyboard.current;
            bool holding = PlayerInRange && kb != null && kb.eKey.isPressed;
            Progress = holding ? Progress + Time.deltaTime / sabotageTime : 0f;
            if (Progress >= 1f) Sabotage();
        }

        public void Sabotage()
        {
            if (Sabotaged) return;
            Sabotaged = true;
            Progress = 1f;
            PlayerInRange = false;

            if (network != null) network.SetOnline(false);
            NoiseSystem.Emit(transform.position, noiseRadius, noiseIntensity, NoiseType.Impacto, gameObject);

            if (beacon != null) beacon.enabled = false;
            if (beaconLight != null) beaconLight.enabled = false;
        }
    }
}
