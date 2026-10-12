using System;
using System.Collections.Generic;
using UnityEngine;

namespace Apagao.AI.Hive
{
    /// <summary>Quem participa da rede e reage ao que ela transmite.</summary>
    public interface IHiveAware
    {
        /// <summary>Fora da patrulha normal (desconfiado, investigando, em combate ou procurando).</summary>
        bool IsAware { get; }

        /// <summary>Outro membro viu o Caio. <paramref name="converge"/> = deve ir até lá.</summary>
        void OnHiveSighting(Vector3 position, bool confirmed, bool converge);

        /// <summary>Um membro sumiu. <paramref name="investigate"/> = você é um dos escolhidos para ir ver.</summary>
        void OnHiveMemberMissing(Vector3 lastPosition, bool investigate);
    }

    /// <summary>
    /// Rede da VIGÍLIA de uma área (spec 07): visão compartilhada, "uma vez visto, marcado",
    /// check-in periódico e descoberta de membros desaparecidos. A antena da área pode derrubá-la.
    /// </summary>
    public class HiveNetwork : MonoBehaviour
    {
        [SerializeField] private Vector3 areaSize = new Vector3(140f, 60f, 140f);

        [Header("Regras")]
        [SerializeField] private float checkInInterval = 15f;
        [Tooltip("Com o Caio marcado, membros a até esta distância de um avistamento vão até lá.")]
        [SerializeField] private float convergeRadius = 40f;
        [Tooltip("Quantos membros vão investigar um desaparecido.")]
        [SerializeField] private int missingResponders = 2;
        [Tooltip("Intervalo mínimo entre transmissões de avistamento.")]
        [SerializeField] private float broadcastInterval = 0.25f;

        public bool Online { get; private set; } = true;
        public bool PlayerSpotted { get; private set; }
        public Vector3 LastReportedPosition { get; private set; }
        public float CheckInInterval => checkInInterval;
        public IReadOnlyList<HiveMember> Members => members;
        public Bounds Bounds => new Bounds(transform.position, areaSize);

        public event Action<Vector3, bool> SightingBroadcast;
        public event Action<Vector3> MemberMissing;
        public event Action<bool> OnlineChanged;

        private struct PendingMissing
        {
            public Vector3 Position;
            public float DueTime;
        }

        private readonly List<HiveMember> members = new List<HiveMember>();
        private readonly Dictionary<HiveMember, float> nextCheckIn = new Dictionary<HiveMember, float>();
        private readonly List<PendingMissing> pending = new List<PendingMissing>();
        private readonly List<HiveMember> sortBuffer = new List<HiveMember>();
        private float lastBroadcast = float.NegativeInfinity;

        // ---------- Registro global ----------

        private static readonly List<HiveNetwork> all = new List<HiveNetwork>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        /// <summary>Rede cuja área contém o ponto, ou null.</summary>
        public static HiveNetwork FindFor(Vector3 point)
        {
            foreach (var n in all)
                if (n.Bounds.Contains(point)) return n;
            return null;
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        // ---------- Membros ----------

        public void Register(HiveMember m)
        {
            if (members.Contains(m)) return;
            members.Add(m);
            nextCheckIn[m] = Time.time + UnityEngine.Random.Range(0f, checkInInterval); // fase aleatória
        }

        public void Unregister(HiveMember m)
        {
            members.Remove(m);
            nextCheckIn.Remove(m);
        }

        // ---------- Relatos ----------

        /// <summary>Um membro viu o Caio. Confirmado = em combate (liga "uma vez visto, marcado").</summary>
        public void ReportSighting(HiveMember from, Vector3 position, bool confirmed)
        {
            if (!Online || from.Offline) return;

            LastReportedPosition = position;
            if (confirmed) PlayerSpotted = true;

            if (Time.time - lastBroadcast < broadcastInterval) return;
            lastBroadcast = Time.time;

            foreach (var m in members)
            {
                if (m == from || m.Offline || m.Receiver == null) continue;
                bool converge = PlayerSpotted && Vector3.Distance(m.transform.position, position) <= convergeRadius;
                m.Receiver.OnHiveSighting(position, confirmed, converge);
            }
            SightingBroadcast?.Invoke(position, confirmed);
        }

        /// <summary>
        /// Um membro foi destruído. Em alerta e conectado: sinal de morte imediato.
        /// Distraído ou paralisado: a rede só percebe quando ele faltar ao check-in.
        /// </summary>
        public void ReportDeath(HiveMember m, bool aware)
        {
            float due = nextCheckIn.TryGetValue(m, out float t) ? t : Time.time + checkInInterval;
            Vector3 position = m.transform.position;
            bool silent = !aware || m.Offline;
            Unregister(m);

            if (!Online) return; // rede caída nunca descobre
            if (silent) pending.Add(new PendingMissing { Position = position, DueTime = due });
            else RaiseMissing(position);
        }

        public void SetOnline(bool online)
        {
            if (Online == online) return;
            Online = online;
            if (!online) pending.Clear();
            OnlineChanged?.Invoke(online);
        }

        /// <summary>Segundos até a rede perceber o próximo desaparecido (para o HUD).</summary>
        public bool TryGetNextMissing(out float secondsLeft)
        {
            secondsLeft = float.MaxValue;
            foreach (var p in pending) secondsLeft = Mathf.Min(secondsLeft, p.DueTime - Time.time);
            secondsLeft = Mathf.Max(0f, secondsLeft);
            return pending.Count > 0 && Online;
        }

        // ---------- Check-in ----------

        private void Update()
        {
            if (!Online) return;
            float now = Time.time;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (now < pending[i].DueTime) continue;
                Vector3 p = pending[i].Position;
                pending.RemoveAt(i);
                RaiseMissing(p);
            }

            // Membro paralisado fica "devendo" o check-in até voltar.
            foreach (var m in members)
                if (now >= nextCheckIn[m] && !m.Offline)
                    nextCheckIn[m] = now + checkInInterval;
        }

        private void RaiseMissing(Vector3 position)
        {
            sortBuffer.Clear();
            foreach (var m in members)
                if (!m.Offline && m.Receiver != null) sortBuffer.Add(m);
            sortBuffer.Sort((a, b) =>
                (a.transform.position - position).sqrMagnitude.CompareTo((b.transform.position - position).sqrMagnitude));

            for (int i = 0; i < sortBuffer.Count; i++)
                sortBuffer[i].Receiver.OnHiveMemberMissing(position, i < missingResponders);

            MemberMissing?.Invoke(position);
        }

        // ---------- Debug ----------

        private void OnDrawGizmos()
        {
            Gizmos.color = Online ? new Color(1f, 0.2f, 0.2f, 0.25f) : new Color(0.5f, 0.5f, 0.5f, 0.25f);
            Gizmos.DrawWireCube(transform.position, areaSize);
        }

        private void OnDrawGizmosSelected()
        {
            foreach (var m in members)
            {
                if (m == null) continue;
                Gizmos.color = m.Offline || !Online ? Color.gray : new Color(1f, 0.3f, 0.3f);
                Gizmos.DrawLine(transform.position + Vector3.up * 6f, m.transform.position + Vector3.up * 2f);
            }
        }
    }
}
