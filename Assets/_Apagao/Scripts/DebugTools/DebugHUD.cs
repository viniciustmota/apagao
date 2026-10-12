using Apagao.AI.Hive;
using Apagao.Player;
using UnityEngine;

namespace Apagao.DebugTools
{
    /// <summary>
    /// Painel de teste: estado da rede, relógio de check-in e prompt da antena.
    /// Some quando o HUD final (spec 20) estiver pronto.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        private PlayerController player;
        private HiveAntenna antenna;
        private string lastEvent = "";
        private float lastEventTime;
        private GUIStyle style;

        private void Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            antenna = FindAnyObjectByType<HiveAntenna>();

            var network = Network();
            if (network != null)
            {
                network.MemberMissing += _ => Log("A rede percebeu um robô desaparecido!");
                network.OnlineChanged += online => Log(online ? "Rede online." : "Antena sabotada: rede OFFLINE.");
            }
        }

        private HiveNetwork Network() =>
            player != null ? HiveNetwork.FindFor(player.transform.position) : FindAnyObjectByType<HiveNetwork>();

        private void Log(string message)
        {
            lastEvent = message;
            lastEventTime = Time.time;
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            var network = Network();

            GUILayout.BeginArea(new Rect(16, 16, 520, 200), GUI.skin.box);
            if (network != null)
            {
                GUILayout.Label($"Rede: {(network.Online ? "<color=#ff6060>ONLINE</color>" : "<color=#a0a0a0>OFFLINE</color>")}" +
                                $"   Caio marcado: {(network.PlayerSpotted ? "<color=#ff6060>SIM</color>" : "não")}", style);
                if (network.TryGetNextMissing(out float seconds))
                    GUILayout.Label($"<color=#ffcc40>Check-in em {seconds:0}s: vão notar o desaparecido</color>", style);
            }
            else
            {
                GUILayout.Label("Fora da área de uma rede", style);
            }

            if (player != null) GUILayout.Label($"Movimento: {player.Mode}", style);


            if (Time.time - lastEventTime < 5f && lastEvent.Length > 0)
                GUILayout.Label($"<color=#ffffff>{lastEvent}</color>", style);
            GUILayout.EndArea();
        }
    }
}
