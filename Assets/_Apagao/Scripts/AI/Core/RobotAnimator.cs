using UnityEngine;

namespace Apagao.AI
{
    /// <summary>
    /// Animação procedural dos robôs terrestres (sem Animator): passos que acompanham a velocidade do NavMeshAgent,
    /// cabeça que procura ou encara o alvo, braço do rifle mirando no Caio, tremedeira paralisado e corpo largado
    /// no takedown. Bípede (Sentinela, Brutamontes) ou quadrúpede (cão).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class RobotAnimator : MonoBehaviour
    {
        public enum BodyKind { Bipede, Quadrupede }

        [SerializeField] private BodyKind kind = BodyKind.Bipede;
        [Tooltip("Metros por ciclo completo de passos.")]
        [SerializeField] private float stride = 1.4f;
        [SerializeField] private float legAmplitude = 28f;
        [SerializeField] private float armAmplitude = 14f;
        [Tooltip("Velocidade em que o passo atinge a amplitude total.")]
        [SerializeField] private float fullSpeed = 2f;
        [SerializeField] private float blendSpeed = 12f;

        [Header("O que este animador controla")]
        [SerializeField] private bool animateHead = true;
        [SerializeField] private bool animateRightArm = true;
        [Tooltip("Em combate, o braço direito aponta para o Caio (rifle da Sentinela).")]
        [SerializeField] private bool aimRightArm;

        private MachineAI ai;
        private UnityEngine.AI.NavMeshAgent agent;
        private Transform hips, spine, head, shoulderL, shoulderR, elbowL, elbowR, hipL, hipR, kneeL, kneeR;
        private Transform legFL, legFR, legBL, legBR, kneeFL, kneeFR, kneeBL, kneeBR;
        private float phase, hipsBaseY;
        private float lookOffset; // cada robô varre num ritmo próprio
        private Vector3 lastPosition;

        private void Awake()
        {
            ai = GetComponent<MachineAI>();
            agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            hips = Find("Hips");
            spine = Find("Spine");
            head = Find("Head");
            shoulderL = Find("ShoulderL");
            shoulderR = Find("ShoulderR");
            elbowL = Find("ElbowL");
            elbowR = Find("ElbowR");
            hipL = Find("HipL");
            hipR = Find("HipR");
            kneeL = Find("KneeL");
            kneeR = Find("KneeR");
            legFL = Find("LegFL"); legFR = Find("LegFR"); legBL = Find("LegBL"); legBR = Find("LegBR");
            kneeFL = Find("KneeFL"); kneeFR = Find("KneeFR"); kneeBL = Find("KneeBL"); kneeBR = Find("KneeBR");
            if (hips != null) hipsBaseY = hips.localPosition.y;
            lastPosition = transform.position;
            lookOffset = Random.Range(0f, 100f);
        }

        private void LateUpdate()
        {
            if (ai == null || ai.IsDead) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Velocidade real (o agente pode estar desligado no começo, e a investida usa Move).
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            float speed = Mathf.Min(delta.magnitude / dt, 15f);
            float k = 1f - Mathf.Exp(-blendSpeed * dt);

            if (ai.IsFrozen) { Slump(k, 1f); return; }
            if (ai.IsStunned) { Stunned(k); return; }

            phase = (phase + speed * dt / stride) % 1f;
            float s = Mathf.Sin(phase * Mathf.PI * 2f);
            float c = Mathf.Cos(phase * Mathf.PI * 2f);
            float move = Mathf.Clamp01(speed / fullSpeed);
            bool alert = ai.Alert != AlertLevel.Nenhum;

            if (kind == BodyKind.Bipede) Biped(s, c, move, speed, alert, k);
            else Quadruped(s, move, k);

            if (animateHead && head != null) Look(alert, k);
        }

        // ---------- Bípede ----------

        private void Biped(float s, float c, float move, float speed, bool alert, float k)
        {
            float legAmp = legAmplitude * Mathf.Clamp(move, 0f, 1.3f);
            float armAmp = armAmplitude * move;

            Rot(hipL, -s * legAmp, 0f, k);
            Rot(hipR, s * legAmp, 0f, k);
            Rot(kneeL, Mathf.Max(0f, s) * legAmp * 1.3f + 3f, 0f, k);
            Rot(kneeR, Mathf.Max(0f, -s) * legAmp * 1.3f + 3f, 0f, k);

            if (hips != null)
            {
                float bob = 0.03f * move;
                Vector3 hp = hips.localPosition;
                hips.localPosition = new Vector3(hp.x, Mathf.Lerp(hp.y, hipsBaseY - bob + Mathf.Abs(c) * bob, k), hp.z);
            }
            // Respira parado (servo em espera), inclina andando rápido.
            Rot(spine, 6f * Mathf.Clamp01(speed / 3f) + Mathf.Sin(Time.time * 1.1f) * 1f * (1f - move), 0f, k);

            Rot(shoulderL, s * armAmp, -5f, k);
            Rot(elbowL, -12f - (alert ? 15f : 0f), 0f, k);

            if (!animateRightArm) return;
            if (aimRightArm && ai.Alert == AlertLevel.Combate && ai.CanSeePlayer && ai.Player != null)
            {
                // Rifle no ombro: o braço aponta para o peito do Caio, o esquerdo apoia.
                Vector3 to = ai.Player.position + Vector3.up * 1.2f - shoulderR.position;
                float pitch = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                Rot(shoulderR, -90f - pitch, 0f, k * 1.5f);
                Rot(elbowR, -5f, 0f, k);
                Rot(shoulderL, -75f - pitch, 30f, k);
                Rot(elbowL, -35f, 0f, k);
            }
            else if (alert)
            {
                Rot(shoulderR, -30f, 0f, k); // rifle em prontidão
                Rot(elbowR, -50f, 0f, k);
            }
            else
            {
                Rot(shoulderR, -s * armAmp, 5f, k);
                Rot(elbowR, -12f, 0f, k);
            }
        }

        // ---------- Quadrúpede (trote em diagonal) ----------

        private void Quadruped(float s, float move, float k)
        {
            float amp = legAmplitude * Mathf.Clamp(move, 0f, 1.4f);
            Rot(legFL, -s * amp, 0f, k);
            Rot(legBR, -s * amp, 0f, k);
            Rot(legFR, s * amp, 0f, k);
            Rot(legBL, s * amp, 0f, k);
            // Joelho da frente dobra para trás, o de trás para frente (como um cão).
            Rot(kneeFL, Mathf.Max(0f, s) * amp * 1.2f + 5f, 0f, k);
            Rot(kneeBR, -(Mathf.Max(0f, s) * amp * 1.2f + 5f), 0f, k);
            Rot(kneeFR, Mathf.Max(0f, -s) * amp * 1.2f + 5f, 0f, k);
            Rot(kneeBL, -(Mathf.Max(0f, -s) * amp * 1.2f + 5f), 0f, k);
        }

        // ---------- Cabeça ----------

        private void Look(bool alert, float k)
        {
            float yaw, pitch = 0f;
            if (alert)
            {
                // Encara o último lugar onde o Caio esteve (ou está).
                Vector3 target = ai.CanSeePlayer && ai.Player != null ? ai.Player.position + Vector3.up : ai.LastKnownPosition + Vector3.up;
                Vector3 local = transform.InverseTransformDirection(target - head.position);
                yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -70f, 70f);
                pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -30f, 30f);
            }
            else
            {
                yaw = Mathf.Sin(Time.time * 0.6f + lookOffset) * 35f; // varre o caminho
            }
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(pitch, yaw, 0f), k * 0.6f);
        }

        // ---------- Paralisado e finalizado ----------

        private void Stunned(float k)
        {
            Slump(k, 0.6f);
            // Tremedeira elétrica.
            if (spine != null) spine.localRotation *= Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-3f, 3f), 0f);
            if (head != null) head.localRotation *= Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(-8f, 8f), 0f);
        }

        private void Slump(float k, float amount)
        {
            if (kind == BodyKind.Bipede)
            {
                Rot(spine, 30f * amount, 0f, k);
                if (animateHead) Rot(head, 35f * amount, 0f, k);
                Rot(shoulderL, 5f, -3f, k);
                if (animateRightArm) Rot(shoulderR, 5f, 3f, k);
                Rot(elbowL, -5f, 0f, k);
                if (animateRightArm) Rot(elbowR, -5f, 0f, k);
                Rot(kneeL, 25f * amount, 0f, k);
                Rot(kneeR, 25f * amount, 0f, k);
                Rot(hipL, -15f * amount, 0f, k);
                Rot(hipR, -15f * amount, 0f, k);
                if (hips != null)
                {
                    Vector3 hp = hips.localPosition;
                    hips.localPosition = new Vector3(hp.x, Mathf.Lerp(hp.y, hipsBaseY - 0.12f * amount, k), hp.z);
                }
            }
            else
            {
                // Cão: as patas cedem.
                foreach (var l in new[] { legFL, legFR }) Rot(l, -20f * amount, 0f, k);
                foreach (var l in new[] { legBL, legBR }) Rot(l, 25f * amount, 0f, k);
                foreach (var kn in new[] { kneeFL, kneeFR }) Rot(kn, 50f * amount, 0f, k);
                foreach (var kn in new[] { kneeBL, kneeBR }) Rot(kn, -50f * amount, 0f, k);
            }
        }

        // ---------- Utilidades ----------

        private static void Rot(Transform t, float x, float z, float k)
        {
            if (t == null) return;
            t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(x, 0f, z), Mathf.Clamp01(k));
        }

        private Transform Find(string name) => Find(transform, name);

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
