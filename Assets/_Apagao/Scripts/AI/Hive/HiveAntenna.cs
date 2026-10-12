using Apagao.Combat;
using Apagao.Player;
using Apagao.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using Apagao.Audio;

namespace Apagao.AI.Hive
{
    /// <summary>
    /// Antena da área (spec 07). Segurar E perto dela por 2 s (interação comum, com aviso e barra no HUD)
    /// ou acertar com o PEM derruba a rede: os robôs passam a agir sozinhos.
    /// </summary>
    public class HiveAntenna : MonoBehaviour, IStunnable, IInteractable
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
            if (!Sabotaged) hum = LoopSound.Attach(gameObject, "antena_loop", 0.5f, 18f, SoundCategory.Inimigos);
        }

        private LoopSound hum;

        private void OnEnable() => InteractableRegistry.Register(this);
        private void OnDisable() => InteractableRegistry.Unregister(this);

        // ---------- Interação (segurar E, pelo PlayerInteractor) ----------

        public string Prompt => "Sabotar a antena";
        public float HoldTime => sabotageTime;
        public Vector3 Position => transform.position + Vector3.up * 0.6f;
        public bool CanInteract(GameObject who) => !Sabotaged;
        public void Interact(GameObject who) => Sabotage();

        private void Update()
        {
            if (Sabotaged) return;

            if (beaconLight != null)
                beaconLight.intensity = baseIntensity * (0.4f + 0.6f * Mathf.PingPong(Time.time * 1.5f, 1f));

            if (player == null) return;
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            PlayerInRange = d.magnitude <= interactRange;
        }

        public bool IsStunned => Sabotaged;

        /// <summary>Uma descarga de PEM derruba a antena (spec 07 R9).</summary>
        public void Stun(float seconds) => Sabotage();

        /// <summary>Derruba a antena. <paramref name="silent"/> = sem barulho (restaurando um checkpoint).</summary>
        public void Sabotage(bool silent = false)
        {
            if (Sabotaged) return;
            Sabotaged = true;
            if (network == null) network = HiveNetwork.FindFor(transform.position); // sabotada antes do próprio Start (checkpoint)
            Progress = 1f;
            PlayerInRange = false;

            if (network != null) network.SetOnline(false);
            if (!silent)
            {
                NoiseSystem.Emit(transform.position, noiseRadius, noiseIntensity, NoiseType.Impacto, gameObject);
                Sfx.Play("antena_sabotada", transform.position + Vector3.up * 2f, 1f, SoundCategory.Efeitos, 40f);
            }
            if (hum != null) hum.Stop();
            SaveId.ReportGone(gameObject);

            if (beacon != null) beacon.enabled = false;
            if (beaconLight != null) beaconLight.enabled = false;
        }
    }
}
