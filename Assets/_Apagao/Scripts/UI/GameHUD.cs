using Apagao.Combat;
using Apagao.Crafting;
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
        private Inventory inventory;
        private CraftingSystem crafting;
        private PlayerInteractor interactor;
        private Thrower thrower;
        private PlayerHealing healing;
        private LedgeClimber climber;
        private string toast = "";
        private float toastTime = float.NegativeInfinity;
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
                inventory = player.GetComponent<Inventory>();
                crafting = player.GetComponent<CraftingSystem>();
                interactor = player.GetComponent<PlayerInteractor>();
                thrower = player.GetComponent<Thrower>();
                healing = player.GetComponent<PlayerHealing>();
                climber = player.GetComponent<LedgeClimber>();
                death = player.GetComponent<PlayerDeath>();
            }
            if (health != null) health.Damaged += (_, _) => damageFlash = 1f;
            if (inventory != null)
                inventory.ResourceAdded += (type, amount) =>
                {
                    toast = $"+{amount} {CraftingNames.Of(type)}  ({inventory.Get(type)}/{inventory.Limit(type)})";
                    toastTime = Time.time;
                };
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
            DrawItems();
            DrawInteraction();
            DrawClimbHint();
            DrawCraftingPanel();
            DrawProgress();
            DrawToasts();
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

        // ---------- Itens e crafting (spec 12) ----------

        /// <summary>Arremessável escolhido e kits, logo acima da arma.</summary>
        private void DrawItems()
        {
            if (inventory == null || health.IsDead) return;
            string throwable = thrower != null
                ? $"[G] {CraftingNames.Of(thrower.Selected)} ×{inventory.GetItem(thrower.Selected)}  <color=#909090>[T] trocar</color>"
                : "";
            GUI.Label(new Rect(24, Screen.height - 76, 600, 22),
                $"{throwable}    [H] Kit ×{inventory.GetItem(ItemType.Kit)}", smallStyle);
        }

        private void DrawInteraction()
        {
            if (interactor == null || interactor.Current == null || cam == null || health.IsDead) return;
            Vector3 screen = cam.WorldToScreenPoint(interactor.Current.Position + Vector3.up * 0.8f);
            if (screen.z <= 0f) return;
            string progress = interactor.HoldProgress > 0f ? $"  {interactor.HoldProgress:P0}" : "";
            GUI.Label(new Rect(screen.x - 120, Screen.height - screen.y - 14, 240, 28),
                $"<b>E</b> — {interactor.Current.Prompt}{progress}", promptStyle);
        }

        private void DrawClimbHint()
        {
            if (climber == null || !climber.LedgeAvailable || health.IsDead) return;
            GUI.Label(new Rect(Screen.width * 0.5f - 90, Screen.height * 0.5f + 100, 180, 26), "<b>Espaço</b> — Escalar", promptStyle);
        }

        /// <summary>Menu de crafting enquanto Tab está pressionado.</summary>
        private void DrawCraftingPanel()
        {
            if (crafting == null || inventory == null || health.IsDead) return;

            if (!crafting.MenuOpen)
            {
                if (crafting.NearBench)
                    GUI.Label(new Rect(Screen.width * 0.5f - 150, Screen.height - 110, 300, 26), "Bancada — segure <b>Tab</b>", promptStyle);
                return;
            }

            var area = new Rect(Screen.width * 0.5f - 260, Screen.height * 0.5f - 170, 520, 340);
            GUI.Box(area, "");
            GUILayout.BeginArea(new Rect(area.x + 16, area.y + 12, area.width - 32, area.height - 24));

            string res = "";
            foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType)))
                res += $"{CraftingNames.Of(t)} <b>{inventory.Get(t)}</b>   ";
            GUILayout.Label(res, smallStyle);
            GUILayout.Label(crafting.NearBench ? "<color=#80ff80>Bancada por perto</color>" : "<color=#909090>Sem bancada: receitas elétricas indisponíveis</color>", smallStyle);
            GUILayout.Space(6);

            var recipes = RecipeBook.All;
            for (int i = 0; i < recipes.Length; i++)
            {
                var r = recipes[i];
                bool ok = crafting.CanCraft(r, out string reason);
                string cost = "";
                foreach (var c in r.Cost) cost += $"{c.amount} {CraftingNames.Of(c.type)}  ";
                string color = ok ? "#ffffff" : "#808080";
                string extra = ok ? "" : $"  <i>({reason})</i>";
                GUILayout.Label($"<color={color}><b>{i + 1}</b>  {r.Name} — {cost}{(r.NeedsBench ? "[bancada]" : "")}{extra}</color>", smallStyle);
            }
            GUILayout.EndArea();
        }

        private void DrawProgress()
        {
            if (health.IsDead) return;
            string label = null;
            float value = 0f;
            if (crafting != null && crafting.IsCrafting) { label = $"Fabricando: {crafting.CurrentRecipe.Name}"; value = crafting.CraftProgress; }
            else if (healing != null && healing.IsHealing) { label = "Usando kit…"; value = healing.Progress; }
            if (label == null) return;

            var bg = new Rect(Screen.width * 0.5f - 120, Screen.height * 0.5f + 70, 240, 10);
            GUI.Label(new Rect(bg.x, bg.y - 24, 300, 22), label, smallStyle);
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.85f, 0.5f);
            GUI.DrawTexture(new Rect(bg.x, bg.y, bg.width * value, bg.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>Recursos coletados e avisos de crafting, cura e arremesso.</summary>
        private void DrawToasts()
        {
            if (health.IsDead) return;
            string message = null;
            float newest = float.NegativeInfinity;
            void Consider(string text, float time)
            {
                if (time > newest && Time.time - time < 2f) { newest = time; message = text; }
            }
            Consider(toast, toastTime);
            if (crafting != null) Consider(crafting.LastMessage, crafting.LastMessageTime);
            if (healing != null) Consider(healing.LastMessage, healing.LastMessageTime);
            if (thrower != null) Consider(thrower.LastMessage, thrower.LastMessageTime);
            if (message != null)
                GUI.Label(new Rect(24, Screen.height - 104, 600, 22), message, smallStyle);
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
