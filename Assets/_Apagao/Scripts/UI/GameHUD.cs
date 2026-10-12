using Apagao.Combat;
using Apagao.Player;
using UnityEngine;

namespace Apagao.UI
{
    /// <summary>
    /// HUD mínimo (spec 20, marco 5): vida discreta, durabilidade do facão, prompt de takedown,
    /// vinheta de dano e tela de morte. Provisório em IMGUI; vai para UI Toolkit no marco 11.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Tooltip("Por quanto tempo a barra de vida fica visível depois de um dano.")]
        [SerializeField] private float showHealthAfterDamage = 4f;

        private Health health;
        private Facao facao;
        private TakedownSystem takedown;
        private WeaponHolder weapons;
        private PlayerDeath death;
        private Camera cam;
        private float damageFlash;
        private GUIStyle promptStyle;
        private GUIStyle smallStyle;
        private GUIStyle deathStyle;
        private GUIStyle markerStyle;

        private void Start()
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                health = player.GetComponent<Health>();
                facao = player.GetComponent<Facao>();
                takedown = player.GetComponent<TakedownSystem>();
                weapons = player.GetComponent<WeaponHolder>();
                death = player.GetComponent<PlayerDeath>();
            }
            if (health != null) health.Damaged += (_, _) => damageFlash = 1f;
            cam = Camera.main;
        }

        private void Update() => damageFlash = Mathf.MoveTowards(damageFlash, 0f, Time.deltaTime * 1.5f);

        private void OnGUI()
        {
            EnsureStyles();
            if (health == null) return;

            DrawVignette();
            DrawHealthBar();
            DrawWeapon();
            DrawCrosshair();
            DrawWeaponMessage();
            DrawTakedownPrompt();
            DrawAimMarker();
            DrawDeath();
        }

        private void DrawVignette()
        {
            float low = health.Normalized < 0.3f && !health.IsDead ? 0.12f * (0.6f + 0.4f * Mathf.Sin(Time.time * 4f)) : 0f;
            float alpha = damageFlash * 0.35f + low;
            if (alpha <= 0.001f) return;
            GUI.color = new Color(0.8f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawHealthBar()
        {
            bool show = Time.time - health.LastDamageTime < showHealthAfterDamage || health.Normalized < 0.5f;
            if (!show || health.IsDead) return;

            var bg = new Rect(24, Screen.height - 48, 240, 12);
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(0.9f, 0.85f, 0.8f), health.Normalized);
            GUI.DrawTexture(new Rect(bg.x, bg.y, bg.width * health.Normalized, bg.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>Arma em mãos e munição, no canto inferior esquerdo (spec 20 R6).</summary>
        private void DrawWeapon()
        {
            if (health.IsDead) return;
            string text;
            var slot = weapons != null ? weapons.Current : WeaponSlot.Facao;

            if (slot == WeaponSlot.Facao)
            {
                if (facao == null) return;
                string state = facao.IsBroken ? "<color=#ff6060>quebrado</color>" : $"{facao.DurabilityNormalized:P0}";
                text = $"[1] Facão {state}";
            }
            else if (slot == WeaponSlot.Pem)
            {
                var pem = weapons.Pem;
                string bolts = new string('■', pem.Charges) + new string('□', pem.MaxCharges - pem.Charges);
                text = $"[4] PEM <color=#60d0ff>{bolts}</color>";
            }
            else
            {
                weapons.TryGetAmmo(out int loaded, out int reserve);
                string reload = weapons.IsReloading ? "  <color=#ffcc40>recarregando…</color>" : "";
                text = $"[{(int)slot}] {weapons.CurrentData.DisplayName}  <b>{loaded}</b> / {reserve}{reload}";
            }

            GUI.Label(new Rect(24, Screen.height - 32, 500, 22), text, smallStyle);
        }

        private void DrawCrosshair()
        {
            if (weapons == null || !weapons.IsAiming || health.IsDead) return;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            GUI.color = weapons.Current == WeaponSlot.Pem ? new Color(0.4f, 0.85f, 1f, 0.9f) : new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(cx - 8, cy - 1, 6, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2, cy - 1, 6, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy - 8, 2, 6), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy + 2, 2, 6), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawWeaponMessage()
        {
            if (weapons == null || Time.time - weapons.LastMessageTime > 1.5f || health.IsDead) return;
            GUI.Label(new Rect(Screen.width * 0.5f - 200, Screen.height * 0.5f + 40, 400, 24), weapons.LastMessage, promptStyle);
        }

        private void DrawTakedownPrompt()
        {
            if (takedown == null || takedown.IsExecuting || cam == null) return;
            var target = takedown.Candidate;
            if (target == null || (target as Object) == null) return;

            Vector3 screen = cam.WorldToScreenPoint(target.transform.position + Vector3.up * 2.3f);
            if (screen.z <= 0f) return;
            var rect = new Rect(screen.x - 90, Screen.height - screen.y - 14, 180, 28);
            GUI.Label(rect, "<b>E</b> — Finalizar", promptStyle);
        }

        /// <summary>Losango sobre o alvo que o facão vai acertar (some quando há prompt de takedown).</summary>
        private void DrawAimMarker()
        {
            if (facao == null || cam == null || facao.PreviewTarget == null) return;
            if (takedown != null && takedown.Candidate != null) return;

            Vector3 screen = cam.WorldToScreenPoint(facao.PreviewTarget.position + Vector3.up * 2.1f);
            if (screen.z <= 0f) return;
            GUI.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            GUI.Label(new Rect(screen.x - 10, Screen.height - screen.y - 12, 20, 24), "◆", markerStyle);
            GUI.color = Color.white;
        }

        private void DrawDeath()
        {
            if (!health.IsDead || death == null) return;
            float t = Mathf.Clamp01((Time.time - death.DeathTime) / (death.RestartDelay * 0.6f));
            GUI.color = new Color(0f, 0f, 0f, t);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, t);
            GUI.Label(new Rect(0, Screen.height * 0.5f - 30, Screen.width, 60), "Você morreu", deathStyle);
            GUI.color = Color.white;
        }

        private void EnsureStyles()
        {
            if (promptStyle != null) return;
            promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 16, richText = true, alignment = TextAnchor.MiddleCenter };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            deathStyle = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleCenter };
            markerStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        }
    }
}
