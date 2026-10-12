using Apagao.Combat;
using UnityEngine;

namespace Apagao.Player
{
    /// <summary>
    /// Animação procedural do corpo do Caio (sem Animator nem clipes): ciclo de passos que acompanha a velocidade,
    /// agachar, pulo, esquiva, escalada, golpe do facão, mira, arremesso, cura, takedown, susto ao levar dano e morte.
    /// Lê o estado dos outros componentes e só mexe nos ossos do "Rig".
    /// </summary>
    [DefaultExecutionOrder(100)] // depois do Facao e do WeaponHolder decidirem o frame
    public class CaioAnimator : MonoBehaviour
    {
        [SerializeField] private float blendSpeed = 14f;
        [SerializeField] private float walkStride = 1.5f;
        [SerializeField] private float runStride = 2.3f;
        [SerializeField] private float crouchStride = 1.0f;

        private Transform rig, hips, spine, head, shoulderL, shoulderR, elbowL, elbowR, handR;
        private Transform hipL, hipR, kneeL, kneeR, ankleL, ankleR;
        private Transform blade, gunRevolver, gunShotgun, gunPem;

        private PlayerController player;
        private CharacterController controller;
        private LedgeClimber climber;
        private Facao facao;
        private WeaponHolder weapons;
        private Thrower thrower;
        private PlayerHealing healing;
        private TakedownSystem takedown;
        private Health health;
        private Camera cam;

        private float phase;
        private float flinch;
        private float deathBlend;
        private float hipsBaseY;
        private readonly Pose pose = new Pose();

        private void Awake()
        {
            rig = Find(transform, "Rig");
            if (rig == null)
            {
                enabled = false; // sem corpo montado (cena antiga)
                return;
            }
            hips = Find(rig, "Hips");
            spine = Find(rig, "Spine");
            head = Find(rig, "Head");
            shoulderL = Find(rig, "ShoulderL");
            shoulderR = Find(rig, "ShoulderR");
            elbowL = Find(rig, "ElbowL");
            elbowR = Find(rig, "ElbowR");
            handR = Find(rig, "HandR");
            hipL = Find(rig, "HipL");
            hipR = Find(rig, "HipR");
            kneeL = Find(rig, "KneeL");
            kneeR = Find(rig, "KneeR");
            ankleL = Find(rig, "AnkleL");
            ankleR = Find(rig, "AnkleR");
            gunRevolver = Find(rig, "Arma_Revolver");
            gunShotgun = Find(rig, "Arma_Espingarda");
            gunPem = Find(rig, "Arma_PEM");
            blade = Find(transform, "MaoFacao");
            hipsBaseY = hips.localPosition.y;

            player = GetComponent<PlayerController>();
            controller = GetComponent<CharacterController>();
            climber = GetComponent<LedgeClimber>();
            facao = GetComponent<Facao>();
            weapons = GetComponent<WeaponHolder>();
            thrower = GetComponent<Thrower>();
            healing = GetComponent<PlayerHealing>();
            takedown = GetComponent<TakedownSystem>();
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info, float amount) => flinch = 1f;

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            float dt = Time.deltaTime;
            flinch = Mathf.MoveTowards(flinch, 0f, dt * 4f);

            pose.Reset();
            if (health != null && health.IsDead) deathBlend = Mathf.MoveTowards(deathBlend, 1f, dt * 1.6f);

            Locomotion(pose, dt);
            UpperBody(pose);

            pose.SpineX -= flinch * 14f;
            Apply(pose, dt);
            UpdateWeapons();

            // Morte: tomba para trás.
            rig.localRotation = Quaternion.Euler(-85f * Smooth(deathBlend), 0f, 0f);
            rig.localPosition = new Vector3(0f, 0.12f * Smooth(deathBlend), -0.2f * Smooth(deathBlend));
        }

        // ---------- Pernas e tronco ----------

        private void Locomotion(Pose p, float dt)
        {
            Vector3 v = controller != null ? controller.velocity : Vector3.zero;
            v.y = 0f;
            float speed = v.magnitude;
            bool grounded = player == null || player.IsGrounded;
            bool crouch = player != null && player.IsCrouching;
            bool running = player != null && player.Mode == MoveMode.Correndo;

            float stride = crouch ? crouchStride : running ? runStride : walkStride;
            phase = (phase + speed * dt / stride) % 1f;
            float s = Mathf.Sin(phase * Mathf.PI * 2f);
            float c = Mathf.Cos(phase * Mathf.PI * 2f);
            float move = Mathf.Clamp01(speed / 0.6f);

            if (climber != null && climber.IsClimbing)
            {
                p.HipLX = -70f; p.KneeLX = 100f;
                p.HipRX = -20f; p.KneeRX = 40f;
                p.SpineX = 15f;
                p.ShoulderLX = p.ShoulderRX = -165f;
                p.ElbowLX = p.ElbowRX = -20f;
                p.HipsY = hipsBaseY;
                p.ArmsTaken = true;
                return;
            }

            if (player != null && player.IsDodging)
            {
                p.HipsY = hipsBaseY - 0.22f;
                p.SpineX = 38f;
                p.HipLX = -60f; p.KneeLX = 80f;
                p.HipRX = -20f; p.KneeRX = 60f;
                p.ShoulderLX = p.ShoulderRX = 35f;
                p.ElbowLX = p.ElbowRX = -30f;
                p.ArmsTaken = true;
                return;
            }

            if (!grounded)
            {
                p.HipLX = -40f; p.KneeLX = 55f;
                p.HipRX = -5f; p.KneeRX = 25f;
                p.ShoulderLZ = -35f; p.ShoulderRZ = 35f;
                p.ShoulderLX = p.ShoulderRX = -25f;
                p.ElbowLX = p.ElbowRX = -30f;
                p.SpineX = 5f;
                p.HipsY = hipsBaseY;
                return;
            }

            float legAmp = (crouch ? 22f : running ? 44f : 28f) * move;
            float armAmp = (crouch ? 12f : running ? 45f : 22f) * move;

            p.HipLX = -s * legAmp;
            p.HipRX = s * legAmp;
            p.KneeLX = Mathf.Max(0f, s) * legAmp * 1.4f + 4f;
            p.KneeRX = Mathf.Max(0f, -s) * legAmp * 1.4f + 4f;
            p.ShoulderLX = s * armAmp;
            p.ShoulderRX = -s * armAmp;
            p.ElbowLX = p.ElbowRX = -(running ? 75f : 15f + 10f * move);
            p.ShoulderLZ = -6f;
            p.ShoulderRZ = 6f;

            float bob = (running ? 0.05f : 0.025f) * move;
            p.HipsY = hipsBaseY - bob + Mathf.Abs(c) * bob;
            p.SpineX = running ? 12f * move : 2f;
            p.SpineX += Mathf.Sin(Time.time * 1.6f) * 1.2f * (1f - move); // respiração parado

            if (crouch)
            {
                p.HipsY = hipsBaseY - 0.33f;
                p.HipLX -= 70f; p.HipRX -= 70f;
                p.KneeLX += 100f; p.KneeRX += 100f;
                p.SpineX += 22f;
                p.ShoulderLX -= 15f; p.ShoulderRX -= 15f;
                p.ElbowLX = p.ElbowRX = -40f;
            }
        }

        // ---------- Braços (ações sobrepõem o balanço do passo) ----------

        private void UpperBody(Pose p)
        {
            if (p.ArmsTaken || (health != null && health.IsDead)) return;
            float camPitch = cam != null ? Mathf.Asin(Mathf.Clamp(cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg : 0f;
            var slot = weapons != null ? weapons.Current : WeaponSlot.Facao;
            bool hasFacao = facao != null && PlayerLoadout.Allows(facao, WeaponSlot.Facao);

            if (takedown != null && takedown.IsExecuting)
            {
                p.ShoulderRX = -115f; p.ElbowRX = -10f;
                p.ShoulderLX = -60f; p.ElbowLX = -40f;
                p.SpineX += 18f;
                return;
            }

            if (healing != null && healing.IsHealing)
            {
                p.ShoulderLX = p.ShoulderRX = -55f;
                p.ShoulderLZ = 25f; p.ShoulderRZ = -25f;
                p.ElbowLX = p.ElbowRX = -95f;
                p.HeadX = 25f;
                return;
            }

            if (thrower != null && thrower.IsAiming)
            {
                p.ShoulderRX = -150f; p.ElbowRX = -70f;
                p.ShoulderLX = -70f - camPitch; p.ElbowLX = -10f;
                return;
            }

            if (weapons != null && weapons.IsAiming && slot != WeaponSlot.Facao)
            {
                // Mira: os dois braços apontam para onde a câmera olha.
                p.ShoulderRX = -90f - camPitch; p.ElbowRX = -5f; p.ShoulderRZ = 0f;
                p.ShoulderLX = -82f - camPitch; p.ElbowLX = -25f; p.ShoulderLZ = 28f;
                p.HeadX = -camPitch * 0.6f;
                p.SpineX = Mathf.Min(p.SpineX, 6f);
                return;
            }

            if (slot != WeaponSlot.Facao)
            {
                p.ShoulderRX = -25f; p.ElbowRX = -45f; // arma baixa, pronta
                return;
            }

            if (!hasFacao || blade == null) return;
            float bladeAngle = SignedAngle(blade.localEulerAngles.x);
            if (facao.IsAttacking || Mathf.Abs(bladeAngle) > 1f)
            {
                // Segue o ângulo do facão: -70 = erguido para trás, +60 = golpe embaixo à frente.
                p.ShoulderRX = -95f + bladeAngle * 0.9f;
                p.ElbowRX = -20f;
                p.ShoulderRZ = 10f;
                p.SpineX += Mathf.Clamp(bladeAngle * 0.15f, -5f, 10f);
            }
            else
            {
                p.ShoulderRX = Mathf.Min(p.ShoulderRX, -8f);
                p.ElbowRX = -35f;
            }
        }

        private void UpdateWeapons()
        {
            var slot = weapons != null ? weapons.Current : WeaponSlot.Facao;
            bool alive = health == null || !health.IsDead;
            SetActive(gunRevolver, alive && slot == WeaponSlot.Revolver);
            SetActive(gunShotgun, alive && slot == WeaponSlot.Espingarda);
            SetActive(gunPem, alive && slot == WeaponSlot.Pem);

            // O facão vai na mão: o Facao gira, o corpo leva.
            if (blade != null && handR != null) blade.position = handR.TransformPoint(new Vector3(0f, -0.08f, 0.02f));
        }

        // ---------- Aplicação ----------

        private class Pose
        {
            public float HipsY;
            public float SpineX, HeadX;
            public float ShoulderLX, ShoulderLZ, ShoulderRX, ShoulderRZ, ElbowLX, ElbowRX;
            public float HipLX, HipRX, KneeLX, KneeRX;
            public bool ArmsTaken;

            public void Reset()
            {
                HipsY = SpineX = HeadX = 0f;
                ShoulderLX = ShoulderLZ = ShoulderRX = ShoulderRZ = ElbowLX = ElbowRX = 0f;
                HipLX = HipRX = KneeLX = KneeRX = 0f;
                ArmsTaken = false;
            }
        }

        private void Apply(Pose p, float dt)
        {
            float k = 1f - Mathf.Exp(-blendSpeed * dt);
            Vector3 hp = hips.localPosition;
            hips.localPosition = new Vector3(hp.x, Mathf.Lerp(hp.y, p.HipsY, k), hp.z);

            Rot(spine, p.SpineX, 0f, k);
            Rot(head, p.HeadX - p.SpineX * 0.5f, 0f, k);
            Rot(shoulderL, p.ShoulderLX, p.ShoulderLZ, k);
            Rot(shoulderR, p.ShoulderRX, p.ShoulderRZ, k);
            Rot(elbowL, p.ElbowLX, 0f, k);
            Rot(elbowR, p.ElbowRX, 0f, k);
            Rot(hipL, p.HipLX, 0f, k);
            Rot(hipR, p.HipRX, 0f, k);
            Rot(kneeL, p.KneeLX, 0f, k);
            Rot(kneeR, p.KneeRX, 0f, k);
            // Pés paralelos ao chão.
            Rot(ankleL, -(p.HipLX + p.KneeLX) * 0.85f, 0f, k);
            Rot(ankleR, -(p.HipRX + p.KneeRX) * 0.85f, 0f, k);
        }

        private static void Rot(Transform t, float x, float z, float k)
        {
            if (t == null) return;
            t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(x, 0f, z), k);
        }

        private static void SetActive(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        private static float SignedAngle(float a) => a > 180f ? a - 360f : a;
        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var f = Find(c, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
