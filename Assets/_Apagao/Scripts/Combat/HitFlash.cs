using System.Collections;
using UnityEngine;

namespace Apagao.Combat
{
    /// <summary>Pisca os renderers em branco ao levar dano: confirma o acerto para o jogador (spec 09 R5a).</summary>
    [RequireComponent(typeof(Health))]
    public class HitFlash : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float duration = 0.08f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private Health health;
        private MaterialPropertyBlock block;
        private Coroutine running;

        private void Awake()
        {
            health = GetComponent<Health>();
            block = new MaterialPropertyBlock();
        }

        private void OnEnable() => health.Damaged += OnDamaged;
        private void OnDisable() => health.Damaged -= OnDamaged;

        private void OnDamaged(DamageInfo info, float amount)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            block.SetColor(BaseColorId, flashColor);
            foreach (var r in renderers) if (r != null) r.SetPropertyBlock(block);

            yield return new WaitForSecondsRealtime(duration); // funciona mesmo durante o hitstop

            foreach (var r in renderers) if (r != null) r.SetPropertyBlock(null);
            running = null;
        }
    }
}
