using System.Collections;
using Apagao.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Apagao.Player
{
    /// <summary>
    /// Morte do Caio (spec 08 R6): trava o controle, espera a tela escurecer e recarrega a cena.
    /// A cena recarregada volta ao último checkpoint (CheckpointManager, spec 18).
    /// </summary>
    [RequireComponent(typeof(Health), typeof(PlayerController))]
    public class PlayerDeath : MonoBehaviour
    {
        [SerializeField] private float restartDelay = 2.5f;

        public float DeathTime { get; private set; } = float.NegativeInfinity;
        public float RestartDelay => restartDelay;

        private Health health;
        private PlayerController player;

        private void Awake()
        {
            health = GetComponent<Health>();
            player = GetComponent<PlayerController>();
        }

        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;

        private void OnDied()
        {
            DeathTime = Time.time;
            player.InputLocked = true;
            StartCoroutine(Restart());
        }

        private IEnumerator Restart()
        {
            yield return new WaitForSeconds(restartDelay);
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }
    }
}
