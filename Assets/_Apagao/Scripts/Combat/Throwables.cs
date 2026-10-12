using System.Collections;
using System.Collections.Generic;
using Apagao.Crafting;
using Apagao.Player;
using Apagao.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using Apagao.Audio;

namespace Apagao.Combat
{
    /// <summary>Alvo da isca elétrica (cães robô).</summary>
    public interface ILureable
    {
        void Lure(float stunSeconds);
    }

    /// <summary>
    /// Arremessáveis (spec 12 R6): T escolhe, segurar G mostra o arco, soltar G arremessa.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class Thrower : MonoBehaviour
    {
        private static readonly ItemType[] Throwables = { ItemType.BombaRuido, ItemType.Molotov, ItemType.Isca };

        [SerializeField] private float throwSpeed = 14f;
        [SerializeField] private float upward = 0.35f;
        [SerializeField] private Transform hand;
        [SerializeField] private LineRenderer arc;

        public ItemType Selected { get; private set; } = ItemType.BombaRuido;
        public bool IsAiming { get; private set; }
        public string LastMessage { get; private set; } = "";
        public float LastMessageTime { get; private set; } = float.NegativeInfinity;

        private const int ArcPoints = 40;
        private readonly Vector3[] arcBuffer = new Vector3[ArcPoints];
        private Inventory inventory;
        private PlayerController player;
        private Health health;
        private Collider playerCollider;
        private Camera cam;
        private int selectedIndex;

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            player = GetComponent<PlayerController>();
            health = GetComponent<Health>();
            playerCollider = GetComponent<CharacterController>();
        }

        private void Start() => cam = Camera.main;

        private void Update()
        {
            var kb = Keyboard.current;
            bool blocked = kb == null || cam == null || (player != null && player.InputLocked)
                           || (health != null && health.IsDead) || Cursor.lockState != CursorLockMode.Locked;
            if (blocked)
            {
                SetArc(false);
                IsAiming = false;
                return;
            }

            if (kb.tKey.wasPressedThisFrame)
            {
                selectedIndex = (selectedIndex + 1) % Throwables.Length;
                Selected = Throwables[selectedIndex];
            }

            if (kb.gKey.wasPressedThisFrame && inventory.GetItem(Selected) <= 0)
                Message($"Sem {CraftingNames.Of(Selected)} — fabrique com Tab");

            IsAiming = kb.gKey.isPressed && inventory.GetItem(Selected) > 0;
            if (IsAiming) DrawArc();
            else SetArc(false);

            if (kb.gKey.wasReleasedThisFrame && inventory.GetItem(Selected) > 0) Throw();
        }

        private Vector3 Origin => hand != null ? hand.position : transform.position + Vector3.up * 1.5f;

        private Vector3 LaunchVelocity()
        {
            Vector3 dir = cam.transform.forward + Vector3.up * upward;
            return dir.normalized * throwSpeed;
        }

        private void DrawArc()
        {
            if (arc == null) return;
            Vector3 p = Origin;
            Vector3 v = LaunchVelocity();
            const float dt = 0.05f;
            int count = 0;
            while (count < ArcPoints - 1)
            {
                arcBuffer[count++] = p;
                Vector3 next = p + v * dt;
                v += Physics.gravity * dt;
                if (count > 2 && Physics.Linecast(p, next, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
                    && !hit.collider.transform.IsChildOf(transform))
                {
                    arcBuffer[count++] = hit.point;
                    break;
                }
                p = next;
            }
            arc.positionCount = count;
            for (int i = 0; i < count; i++) arc.SetPosition(i, arcBuffer[i]);
            SetArc(true);
        }

        private void SetArc(bool on)
        {
            if (arc != null) arc.enabled = on;
        }

        private void Throw()
        {
            if (!inventory.UseItem(Selected)) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Arremesso_{Selected}";
            go.transform.position = Origin;
            go.transform.localScale = Vector3.one * 0.18f;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", Selected switch
            {
                ItemType.Molotov => new Color(0.9f, 0.5f, 0.1f),
                ItemType.Isca => new Color(0.3f, 0.8f, 1f),
                _ => new Color(0.3f, 0.3f, 0.3f)
            });
            go.GetComponent<Renderer>().SetPropertyBlock(block);

            var col = go.GetComponent<Collider>();
            if (playerCollider != null) Physics.IgnoreCollision(col, playerCollider);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = LaunchVelocity();

            go.AddComponent<ThrownItem>().Init(Selected);
            Sfx.Play2D("arremesso", 0.5f);
            SetArc(false);
        }

        private void Message(string text)
        {
            LastMessage = text;
            LastMessageTime = Time.time;
        }
    }

    /// <summary>O objeto em voo: ao bater em algo, dispara o efeito.</summary>
    public class ThrownItem : MonoBehaviour
    {
        private ItemType type;
        private bool triggered;

        public void Init(ItemType itemType)
        {
            type = itemType;
            Destroy(gameObject, 8f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (triggered) return;
            triggered = true;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;

            switch (type)
            {
                case ItemType.BombaRuido:
                    NoiseSystem.Emit(point, 20f, 0.8f, NoiseType.Distracao, gameObject);
                    Sfx.Play("bomba_ruido", point, 1f, SoundCategory.Efeitos, 35f, 0f);
                    break;
                case ItemType.Molotov:
                    FireArea.Spawn(point);
                    break;
                case ItemType.Isca:
                    ElectricLure.Spawn(point);
                    break;
            }
            Destroy(gameObject);
        }
    }

    /// <summary>Área de fogo do molotov (spec 12): 4 m, 8 s, 10 de dano/s. Queima o Caio também.</summary>
    public class FireArea : MonoBehaviour
    {
        private const float Tick = 0.5f;

        // Molotov: 4 m, 8 s. Brasa (spec 15): 6 m, 15 s.
        private float radius = 4f;
        private float duration = 8f;
        private float damagePerSecond = 10f;

        private readonly Collider[] buffer = new Collider[32];
        private readonly HashSet<IDamageable> hitThisTick = new HashSet<IDamageable>();
        private Light glow;

        public static FireArea Spawn(Vector3 point, float radius = 4f, float duration = 8f, float damagePerSecond = 10f)
        {
            if (Physics.Raycast(point + Vector3.up, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                point = hit.point;

            var go = new GameObject("Fogo");
            go.transform.position = point;
            var fire = go.AddComponent<FireArea>();
            fire.radius = radius;
            fire.duration = duration;
            fire.damagePerSecond = damagePerSecond;
            return fire;
        }

        private void Start()
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(transform, false);
            disc.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(1f, 0.45f, 0.05f));
            disc.GetComponent<Renderer>().SetPropertyBlock(block);

            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.up * 1.2f;
            glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.5f, 0.1f);
            glow.range = radius * 2f;

            NoiseSystem.Emit(transform.position, 12f, 0.7f, NoiseType.Impacto, gameObject); // vidro quebrando
            Sfx.Play("vidro", transform.position, 0.8f, SoundCategory.Efeitos, 25f);
            LoopSound.Attach(gameObject, "fogo_loop", Mathf.Clamp(radius / 6f, 0.4f, 0.8f), 20f, SoundCategory.Efeitos);
            StartCoroutine(Burn());
        }

        private void Update()
        {
            if (glow != null) glow.intensity = 3f + Mathf.PerlinNoise(Time.time * 8f, 0f) * 2f;
        }

        private IEnumerator Burn()
        {
            for (float t = 0f; t < duration; t += Tick)
            {
                hitThisTick.Clear();
                int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, radius, buffer, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var target = buffer[i].GetComponentInParent<IDamageable>();
                    if (target == null || target.IsDead || !hitThisTick.Add(target)) continue;
                    target.TakeDamage(new DamageInfo(damagePerSecond * Tick, DamageType.Fogo, buffer[i].bounds.center, Vector3.zero, gameObject));
                }
                yield return new WaitForSeconds(Tick);
            }
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Isca elétrica (spec 12): pulsa um sinal que atrai os cães (a audição deles dobra o raio: ~25 m)
    /// e prende por 5 s os que chegam perto.
    /// </summary>
    public class ElectricLure : MonoBehaviour
    {
        private const float Duration = 6f;
        private const float PulseInterval = 1f;
        private const float PulseRadius = 12.5f;
        private const float CaptureRadius = 2f;
        private const float StunSeconds = 5f;

        private readonly Collider[] buffer = new Collider[16];
        private readonly HashSet<ILureable> captured = new HashSet<ILureable>();
        private Light blink;
        private float nextPulse;
        private float endTime;

        public static void Spawn(Vector3 point)
        {
            if (Physics.Raycast(point + Vector3.up, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                point = hit.point;

            var go = new GameObject("Isca");
            go.transform.position = point;
            go.AddComponent<ElectricLure>();
        }

        private void Start()
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(transform, false);
            box.transform.localPosition = Vector3.up * 0.1f;
            box.transform.localScale = new Vector3(0.25f, 0.15f, 0.25f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.3f, 0.8f, 1f));
            box.GetComponent<Renderer>().SetPropertyBlock(block);

            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.5f;
            blink = lightGo.AddComponent<Light>();
            blink.type = LightType.Point;
            blink.color = new Color(0.3f, 0.8f, 1f);
            blink.range = 5f;

            endTime = Time.time + Duration;
            LoopSound.Attach(gameObject, "isca_loop", 0.5f, 20f, SoundCategory.Efeitos);
        }

        private void Update()
        {
            if (blink != null) blink.intensity = Mathf.PingPong(Time.time * 8f, 3f);

            if (Time.time >= nextPulse)
            {
                nextPulse = Time.time + PulseInterval;
                NoiseSystem.Emit(transform.position, PulseRadius, 0.5f, NoiseType.Eletrico, gameObject);
            }

            int count = Physics.OverlapSphereNonAlloc(transform.position, CaptureRadius, buffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var target = buffer[i].GetComponentInParent<ILureable>();
                if (target != null && captured.Add(target)) target.Lure(StunSeconds);
            }

            if (Time.time >= endTime) Destroy(gameObject);
        }
    }
}
