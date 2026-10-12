using UnityEngine;

namespace Apagao.AI.Hive
{
    /// <summary>
    /// Conecta uma máquina à rede da sua área (spec 07). O comportamento fica no IHiveAware do mesmo objeto.
    /// </summary>
    public class HiveMember : MonoBehaviour
    {
        [Tooltip("Vazio = usa a rede cuja área contém a posição inicial.")]
        [SerializeField] private HiveNetwork network;

        public HiveNetwork Network => network;
        public IHiveAware Receiver { get; private set; }

        /// <summary>Fora da rede temporariamente (ex.: paralisado pelo PEM).</summary>
        public bool Offline => Time.time < offlineUntil;

        /// <summary>Conectado a uma rede ativa e sem estar offline.</summary>
        public bool Connected => network != null && network.Online && !Offline;

        private float offlineUntil = float.NegativeInfinity;

        private void Awake() => Receiver = GetComponent<IHiveAware>();

        private void Start()
        {
            if (network == null) network = HiveNetwork.FindFor(transform.position);
            if (network != null) network.Register(this);
        }

        private void OnDestroy()
        {
            if (network != null) network.Unregister(this);
        }

        public void GoOffline(float seconds) => offlineUntil = Mathf.Max(offlineUntil, Time.time + seconds);

        public void ReportSighting(Vector3 position, bool confirmed)
        {
            if (Connected) network.ReportSighting(this, position, confirmed);
        }

        /// <summary>Chamar quando a máquina for destruída, antes do Destroy.</summary>
        public void ReportDeath(bool aware)
        {
            if (network == null) return;
            network.ReportDeath(this, aware);
            network = null;
        }

        /// <summary>Sai da rede sem virar "desaparecido" (ex.: Vespa que explodiu de propósito).</summary>
        public void Leave()
        {
            if (network == null) return;
            network.Unregister(this);
            network = null;
        }
    }
}
