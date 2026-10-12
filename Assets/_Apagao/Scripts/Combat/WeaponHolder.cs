using System.Collections;
using System.Collections.Generic;
using Apagao.Crafting;
using Apagao.Player;
using Apagao.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Apagao.Combat
{
    public enum WeaponSlot { Facao = 1, Revolver = 2, Espingarda = 3, Pem = 4 }

    /// <summary>
    /// Armas do Caio (spec 10, 11): troca com 1–4, mira com o botão direito, dispara com o esquerdo mirando, recarrega com R.
    /// O facão (slot 1) cuida do próprio ataque. Disparos usam o centro da tela como mira.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class WeaponHolder : MonoBehaviour
    {
        [SerializeField] private WeaponData revolver;
        [SerializeField] private WeaponData espingarda;
        [SerializeField] private Transform muzzle;

        [Header("Recuo e dispersão")]
        [SerializeField] private float walkingSpreadMultiplier = 1.5f;
        [SerializeField] private float recoilShake = 0.08f;

        [Header("Visual")]
        [SerializeField] private Material tracerMaterial;
        [SerializeField] private float tracerTime = 0.05f;

        public WeaponSlot Current { get; private set; } = WeaponSlot.Facao;
        public WeaponData Revolver => revolver;
        public WeaponData Espingarda => espingarda;
        public bool IsAiming { get; private set; }
        public bool IsReloading { get; private set; }
        public WeaponData CurrentData => Current == WeaponSlot.Revolver ? revolver : Current == WeaponSlot.Espingarda ? espingarda : null;
        public Pem Pem { get; private set; }
        public string LastMessage { get; private set; } = "";
        public float LastMessageTime { get; private set; } = float.NegativeInfinity;

        private class Ammo
        {
            public int Loaded;
            public int Reserve;
        }

        private readonly Dictionary<WeaponData, Ammo> ammo = new Dictionary<WeaponData, Ammo>();
        private readonly List<LineRenderer> tracers = new List<LineRenderer>();
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private PlayerController player;
        private Health health;
        private Camera cam;
        private ThirdPersonCamera cameraRig;
        private float nextShot;
        private Coroutine reloading;
        private CraftingSystem crafting;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            health = GetComponent<Health>();
            Pem = GetComponent<Pem>();
            crafting = GetComponent<CraftingSystem>();
            foreach (var data in new[] { revolver, espingarda })
                if (data != null) ammo[data] = new Ammo { Loaded = data.StartLoaded, Reserve = data.StartReserve };
        }

        private void Start()
        {
            cam = Camera.main;
            if (cam != null) cameraRig = cam.GetComponent<ThirdPersonCamera>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool blocked = kb == null || mouse == null || player.InputLocked || (health != null && health.IsDead)
                           || Cursor.lockState != CursorLockMode.Locked;

            // Com o menu de crafting aberto, os números fabricam em vez de trocar de arma.
            bool menuOpen = crafting != null && crafting.MenuOpen;
            if (!blocked && !menuOpen)
            {
                if (kb.digit1Key.wasPressedThisFrame) Select(WeaponSlot.Facao);
                if (kb.digit2Key.wasPressedThisFrame && revolver != null) Select(WeaponSlot.Revolver);
                if (kb.digit3Key.wasPressedThisFrame && espingarda != null) Select(WeaponSlot.Espingarda);
                if (kb.digit4Key.wasPressedThisFrame && Pem != null) Select(WeaponSlot.Pem);
            }

            SetAiming(!blocked && Current != WeaponSlot.Facao && mouse.rightButton.isPressed && !player.IsDodging);

            if (IsAiming) player.SnapFacing(AimRay().direction);

            // Correr ou esquivar interrompe a recarga.
            if (IsReloading && (player.Mode == MoveMode.Correndo || player.IsDodging)) CancelReload("Recarga interrompida");

            if (blocked || Current == WeaponSlot.Facao) return;

            if (kb.rKey.wasPressedThisFrame) StartReload();
            if (IsAiming && mouse.leftButton.wasPressedThisFrame) Fire();
        }

        private void Select(WeaponSlot slot)
        {
            if (slot == Current) return;
            CancelReload(null);
            Current = slot;
        }

        private void SetAiming(bool aiming)
        {
            bool changed = aiming != IsAiming;
            IsAiming = aiming;
            player.Aiming = aiming;
            // Só mexe na trava de rotação ao começar ou parar de mirar; o facão cuida da dele.
            if (changed) player.RotationLocked = aiming;
            if (cameraRig != null) cameraRig.Aiming = aiming;
        }

        // ---------- Disparo ----------

        private void Fire()
        {
            if (Time.time < nextShot) return;

            if (Current == WeaponSlot.Pem)
            {
                Ray ray = AimRay();
                Vector3 origin = MuzzlePosition();
                int result = Pem.Fire(origin, AimPoint(ray, Pem.Range) - origin);
                if (result < 0) Message(Pem.Charges <= 0 ? "PEM sem carga" : "PEM recarregando");
                else
                {
                    nextShot = Time.time + 0.3f;
                    Shake(0.06f);
                }
                return;
            }

            var data = CurrentData;
            if (data == null || IsReloading) return;
            var a = ammo[data];
            if (a.Loaded <= 0)
            {
                Message(a.Reserve > 0 ? "Sem munição no tambor — R para recarregar" : "Sem munição");
                nextShot = Time.time + 0.3f;
                return;
            }

            a.Loaded--;
            nextShot = Time.time + data.FireInterval;

            Ray aim = AimRay();
            Vector3 muzzlePos = MuzzlePosition();
            Vector3 baseDir = (AimPoint(aim, data.Range) - muzzlePos).normalized;
            float spread = data.Spread * (player.Mode == MoveMode.Parado || player.Mode == MoveMode.Agachado ? 1f : walkingSpreadMultiplier);

            for (int i = 0; i < data.Pellets; i++)
            {
                Vector3 dir = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * Vector3.forward;
                dir = Quaternion.LookRotation(baseDir) * dir;
                Vector3 end = muzzlePos + dir * data.Range;

                if (RaycastIgnoringSelf(muzzlePos, dir, data.Range, out RaycastHit hit))
                {
                    end = hit.point;
                    var target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null && !target.IsDead)
                        target.TakeDamage(new DamageInfo(data.Damage, DamageType.Bala, hit.point, dir, gameObject));
                }
                ShowTracer(i, muzzlePos, end);
            }

            NoiseSystem.Emit(muzzlePos, data.NoiseRadius, data.NoiseIntensity, NoiseType.Arma, gameObject);
            Shake(recoilShake);
            if (a.Loaded == 0 && a.Reserve > 0) Message("R para recarregar");
        }

        /// <summary>Raio do centro da tela.</summary>
        private Ray AimRay() =>
            cam != null ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : new Ray(transform.position + Vector3.up * 1.5f, transform.forward);

        /// <summary>Ponto que a mira está apontando (o primeiro obstáculo à frente da câmera, ignorando o Caio).</summary>
        private Vector3 AimPoint(Ray ray, float range)
        {
            float maxDistance = range + 5f;
            return RaycastIgnoringSelf(ray.origin, ray.direction, maxDistance, out RaycastHit hit)
                ? hit.point
                : ray.origin + ray.direction * maxDistance;
        }

        private bool RaycastIgnoringSelf(Vector3 origin, Vector3 dir, float distance, out RaycastHit closest)
        {
            closest = default;
            int count = Physics.RaycastNonAlloc(origin, dir, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform)) continue;
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    closest = hits[i];
                }
            }
            return best < float.MaxValue;
        }

        private Vector3 MuzzlePosition() =>
            muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.4f + transform.forward * 0.5f;

        // ---------- Recarga ----------

        private void StartReload()
        {
            var data = CurrentData;
            if (data == null || IsReloading) return;
            var a = ammo[data];
            if (a.Loaded >= data.Magazine) return;
            if (a.Reserve <= 0) { Message("Sem munição de reserva"); return; }
            reloading = StartCoroutine(Reload(data));
        }

        private IEnumerator Reload(WeaponData data)
        {
            IsReloading = true;
            Message("Recarregando…");
            yield return new WaitForSeconds(data.ReloadTime);
            var a = ammo[data];
            int needed = data.Magazine - a.Loaded;
            int taken = Mathf.Min(needed, a.Reserve);
            a.Loaded += taken;
            a.Reserve -= taken;
            IsReloading = false;
            reloading = null;
        }

        private void CancelReload(string message)
        {
            if (!IsReloading) return;
            if (reloading != null) StopCoroutine(reloading);
            reloading = null;
            IsReloading = false;
            if (message != null) Message(message);
        }

        /// <summary>Munição coletada (crafting, marco 8). Retorna quanto coube.</summary>
        public int AddReserve(WeaponData data, int amount)
        {
            if (data == null || !ammo.TryGetValue(data, out var a)) return 0;
            int room = data.MaxReserve - a.Reserve;
            int added = Mathf.Clamp(amount, 0, room);
            a.Reserve += added;
            return added;
        }

        public int ReserveRoom(WeaponData data) =>
            data != null && ammo.TryGetValue(data, out var a) ? data.MaxReserve - a.Reserve : 0;

        public bool TryGetAmmo(WeaponData data, out int loaded, out int reserve)
        {
            loaded = reserve = 0;
            if (data == null || !ammo.TryGetValue(data, out var a)) return false;
            loaded = a.Loaded;
            reserve = a.Reserve;
            return true;
        }

        /// <summary>Restaura a munição de uma arma (checkpoint).</summary>
        public void SetAmmo(WeaponData data, int loaded, int reserve)
        {
            if (data == null || !ammo.TryGetValue(data, out var a)) return;
            a.Loaded = Mathf.Clamp(loaded, 0, data.Magazine);
            a.Reserve = Mathf.Clamp(reserve, 0, data.MaxReserve);
        }

        public bool TryGetAmmo(out int loaded, out int reserve)
        {
            loaded = reserve = 0;
            var data = CurrentData;
            if (data == null || !ammo.TryGetValue(data, out var a)) return false;
            loaded = a.Loaded;
            reserve = a.Reserve;
            return true;
        }

        // ---------- Feedback ----------

        private void Message(string text)
        {
            LastMessage = text;
            LastMessageTime = Time.time;
        }

        private void Shake(float amount)
        {
            if (cameraRig != null) cameraRig.Shake(amount, 0.12f);
        }

        private void ShowTracer(int index, Vector3 from, Vector3 to)
        {
            while (tracers.Count <= index)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.startWidth = 0.03f;
                lr.endWidth = 0.01f;
                lr.sharedMaterial = tracerMaterial;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.enabled = false;
                tracers.Add(lr);
            }
            var line = tracers[index];
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            StartCoroutine(FlashTracer(line));
        }

        private IEnumerator FlashTracer(LineRenderer line)
        {
            line.enabled = true;
            yield return new WaitForSeconds(tracerTime);
            line.enabled = false;
        }
    }
}
