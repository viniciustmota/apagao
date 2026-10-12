using System.Collections;
using Apagao.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;
using Apagao.Audio;

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

        private void OnEnable()
        {
            health.Died += OnDied;
            health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            health.Died -= OnDied;
            health.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info, float amount)
        {
            if (!health.IsDead && info.Type != DamageType.Fogo) Sfx.Play2D("dano_caio", Mathf.Clamp01(0.5f + amount / 60f));
        }

        private void OnDied()
        {
            DeathTime = Time.time;
            Sfx.Play2D("morte", 0.9f, SoundCategory.Efeitos, 0f);
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
