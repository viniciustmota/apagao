using UnityEngine;

namespace Apagao.Game
{
    /// <summary>Marca de objetivo ligada por código (ex.: o Capataz conclui uma fase). Objetivo "Concluir" espera Done.</summary>
    public class ObjectiveFlag : MonoBehaviour
    {
        public bool Done { get; set; }
    }
}
