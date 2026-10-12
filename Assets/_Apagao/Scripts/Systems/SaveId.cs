using System;
using UnityEngine;

namespace Apagao.Systems
{
    /// <summary>
    /// Identidade estável de um objeto da cena para o checkpoint (spec 18 R5): inimigos, itens e antenas.
    /// O id é gravado pelo builder. Quem some de vez (destruído, coletado, sabotado) chama <see cref="ReportGone"/>.
    /// </summary>
    /// <summary>
    /// Objeto que, ao "sumir", continua na cena em outro estado (antena sabotada, gerador desligado).
    /// O checkpoint chama RestoreGone em vez de destruí-lo.
    /// </summary>
    public interface ISaveGone
    {
        void RestoreGone();
    }

    [DisallowMultipleComponent]
    public class SaveId : MonoBehaviour
    {
        [SerializeField] private string id;

        public string Id => id;

        /// <summary>Um objeto com SaveId saiu do jogo. Quem ouve é o CheckpointManager.</summary>
        public static event Action<string> Gone;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Gone = null;

        public static void ReportGone(GameObject go)
        {
            if (go == null) return;
            var s = go.GetComponent<SaveId>();
            if (s != null && !string.IsNullOrEmpty(s.id)) Gone?.Invoke(s.id);
        }
    }
}
