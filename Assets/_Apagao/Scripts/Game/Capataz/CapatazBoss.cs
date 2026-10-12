using System.Collections;
using System.Collections.Generic;
using Apagao.AI;
using Apagao.AI.Drones;
using Apagao.AI.Hive;
using Apagao.Audio;
using Apagao.Combat;
using Apagao.Player;
using UnityEngine;

namespace Apagao.Game
{
    /// <summary>
    /// A luta final contra o Capataz na sala do Núcleo (spec 17). As fases são ligadas pelos objetivos da campanha
    /// (tags "capataz1", "capataz2", "capataz3"), o que também faz o checkpoint voltar na fase certa (R7).
    /// 1) Escudo: cortar os 3 geradores enquanto chegam reforços.
    /// 2) Núcleo exposto: lasers girando (pular o baixo, agachar no alto); PEM derruba o escudo por 4 s.
    /// 3) Desespero: fogo na sala, Vespas soltas e 90 s para destruir o Núcleo.
    /// </summary>
    public class CapatazBoss : MonoBehaviour
    {
        public const string Speaker = "Capataz";

        [Header("Peças")]
        [SerializeField] private NucleoCore core;
        [SerializeField] private Generator[] generators;
        [SerializeField] private ObjectiveFlag phase1Done;
        [SerializeField] private ObjectiveFlag phase2Done;
        [SerializeField] private ObjectiveFlag phase3Done;
        [Tooltip("Sentinelas desligadas que entram aos pares como reforço.")]
        [SerializeField] private GameObject[] reinforcements;
        [Tooltip("Vespas desligadas que entram na fase 3.")]
        [SerializeField] private GameObject[] vespas;
        [SerializeField] private Transform[] fireSpots;

        [Header("Lasers (fase 2)")]
        [SerializeField] private LineRenderer[] lasers;
        [SerializeField] private float laserLength = 14f;
        [SerializeField] private float laserSpeed = 45f;
        [SerializeField] private float patternTime = 7f;
        [SerializeField] private float warningTime = 1.4f;
        [SerializeField] private float laserDamage = 25f;
        [SerializeField, Range(0f, 1f)] private float phase3At = 0.6f;

        [Header("Fase 3")]
        [SerializeField] private float burnTime = 90f;
        [SerializeField] private float fireEvery = 7f;
        [SerializeField] private float overtimeDps = 15f;

        public static CapatazBoss Instance { get; private set; }
        public int Phase { get; private set; }
        public bool Defeated { get; private set; }
        public float BurnLeft => Phase == 3 ? Mathf.Max(0f, burnEnd - Time.time) : 0f;
        public NucleoCore Core => core;
        public int GeneratorsLeft
        {
            get
            {
                int n = 0;
                if (generators != null) foreach (var g in generators) if (g != null && !g.Sabotaged) n++;
                return n;
            }
        }

        private Transform player;
        private PlayerController playerMove;
        private Health playerHealth;
        private int nextReinforcement;
        private float laserAngle;
        private float patternStart;
        private bool laserHigh;
        private float nextLaserHit;
        private float burnEnd;
        private float nextFire;
        private int fireIndex;
        private Coroutine wave;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            Instance = this;
            playerMove = FindAnyObjectByType<PlayerController>();
            player = playerMove != null ? playerMove.transform : null;
            playerHealth = player != null ? player.GetComponent<Health>() : null;
        }

        private void OnEnable()
        {
            Campaign.ObjectiveStarted += OnObjective;
            if (generators != null) foreach (var g in generators) if (g != null) g.Disabled += OnGeneratorDisabled;
            var h = core != null ? core.GetComponent<Health>() : null; // o Awake do Núcleo pode não ter rodado
            if (h != null) h.Died += OnCoreDestroyed;
        }

        private void OnDisable()
        {
            Campaign.ObjectiveStarted -= OnObjective;
            if (generators != null) foreach (var g in generators) if (g != null) g.Disabled -= OnGeneratorDisabled;
            var h = core != null ? core.GetComponent<Health>() : null;
            if (h != null) h.Died -= OnCoreDestroyed;
            if (Instance == this) Instance = null;
        }

        // ---------- Fases (ligadas pelos objetivos) ----------

        private void OnObjective(Objective o, bool fromCheckpoint)
        {
            switch (o.tag)
            {
                case "capataz1": StartPhase1(fromCheckpoint); break;
                case "capataz2": StartPhase2(); break;
                case "capataz3": StartPhase3(); break;
            }
        }

        private void StartPhase1(bool fromCheckpoint)
        {
            Phase = 1;
            core.ShieldActive = true;
            core.ShieldBreakable = false;
            SetLasers(false);
            Say(fromCheckpoint ? "De volta, Caio? A estação tem paciência." : null);
            CallReinforcements(2);
        }

        private void StartPhase2()
        {
            Phase = 2;
            core.ShieldActive = true;
            core.ShieldBreakable = true;
            patternStart = Time.time;
            laserHigh = false;
            CallReinforcements(1);
        }

        private void StartPhase3()
        {
            Phase = 3;
            core.ShieldActive = false;
            SetLasers(false);
            // Carregando o checkpoint da fase 3, o Núcleo volta já ferido.
            if (core.Health.Normalized > phase3At) core.Health.Restore(core.Health.Max * phase3At);
            burnEnd = Time.time + burnTime;
            nextFire = Time.time + 2f;
            foreach (var v in vespas)
            {
                if (v == null) continue;
                v.SetActive(true);
                StartCoroutine(LaunchNextFrame(v));
            }
        }

        private IEnumerator LaunchNextFrame(GameObject v)
        {
            yield return null; // espera o Awake/Start da Vespa
            var vespa = v != null ? v.GetComponent<VespaAI>() : null;
            if (vespa != null && player != null) vespa.ForceLaunch(player.position);
        }

        // ---------- Atualização ----------

        private void Update()
        {
            if (Defeated || player == null) return;

            if (Phase == 1 && GeneratorsLeft == 0 && phase1Done != null && !phase1Done.Done)
            {
                phase1Done.Done = true;
                Say("Sem os geradores eu fico nu. Que deselegante.");
            }

            if (Phase == 2)
            {
                UpdateLasers();
                if (core.Health.Normalized <= phase3At && phase2Done != null && !phase2Done.Done)
                {
                    phase2Done.Done = true;
                    SetLasers(false);
                }
            }

            if (Phase == 3) UpdateBurning();
        }

        private void OnGeneratorDisabled(Generator g)
        {
            if (Phase != 1) return;
            int left = GeneratorsLeft;
            if (left == 2) Say("Isso era patrimônio público, Caio.");
            else if (left == 1) Say("Guardas! O eletricista está mexendo na fiação.");
            CallReinforcements(2);
        }

        private void CallReinforcements(int count)
        {
            if (reinforcements == null) return;
            for (int i = 0; i < count && nextReinforcement < reinforcements.Length; i++, nextReinforcement++)
            {
                var r = reinforcements[nextReinforcement];
                if (r == null || r.activeSelf) { count++; continue; } // já destruída/ativa: pula para a próxima
                r.SetActive(true);
                StartCoroutine(AlertNextFrame(r));
            }
        }

        private IEnumerator AlertNextFrame(GameObject r)
        {
            yield return null;
            yield return null; // Start da máquina e do NavMesh
            var ai = r != null ? r.GetComponent<MachineAI>() : null;
            if (ai != null && player != null) ai.OnHiveSighting(player.position, confirmed: true, converge: true);
        }

        // ---------- Lasers (fase 2) ----------

        private void UpdateLasers()
        {
            if (lasers == null || lasers.Length == 0) return;
            bool shieldDown = !core.ShieldUp;
            if (shieldDown)
            {
                SetLasers(false); // o PEM apaga tudo junto com o escudo
                return;
            }

            float t = Time.time - patternStart;
            if (t >= patternTime)
            {
                patternStart = Time.time;
                laserHigh = !laserHigh; // alterna: baixo (pular) e alto (agachar)
                t = 0f;
                Sfx.Play("sentinela_mira", core.transform.position + Vector3.up * 2f, 1f, SoundCategory.Inimigos, 40f, 0f, laserHigh ? 1.2f : 0.8f);
            }
            bool warning = t < warningTime;
            laserAngle = (laserAngle + laserSpeed * Time.deltaTime) % 360f;

            float floorY = core.transform.position.y;
            float h = laserHigh ? 1.45f : 0.35f;
            for (int i = 0; i < lasers.Length; i++)
            {
                var lr = lasers[i];
                if (lr == null) continue;
                float a = (laserAngle + i * 360f / lasers.Length) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 start = core.transform.position + Vector3.up * h + dir * 1.2f;
                lr.enabled = true;
                lr.SetPosition(0, start);
                lr.SetPosition(1, start + dir * laserLength);
                lr.widthMultiplier = warning ? 0.25f : 1f;
                lr.startColor = lr.endColor = warning ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.1f, 0.05f);

                if (!warning) TryLaserHit(dir, floorY, laserHigh);
            }
        }

        private void TryLaserHit(Vector3 dir, float floorY, bool high)
        {
            if (Time.time < nextLaserHit || playerHealth == null || playerHealth.IsDead) return;
            Vector3 to = player.position - core.transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 1f || dist > laserLength + 1.2f) return;
            float halfWidth = Mathf.Atan2(0.45f, dist) * Mathf.Rad2Deg;
            if (Vector3.Angle(dir, to) > halfWidth) return;

            // Baixo: pega quem está no chão (pule). Alto: pega quem está em pé (agache).
            bool hit = high ? !playerMove.IsCrouching : player.position.y - floorY < 0.3f;
            if (!hit) return;
            nextLaserHit = Time.time + 0.8f;
            playerHealth.TakeDamage(new DamageInfo(laserDamage, DamageType.Eletrico, player.position + Vector3.up, dir, gameObject));
        }

        private void SetLasers(bool on)
        {
            if (lasers == null) return;
            foreach (var l in lasers) if (l != null) l.enabled = on;
        }

        // ---------- Fase 3 ----------

        private void UpdateBurning()
        {
            if (fireSpots != null && fireSpots.Length > 0 && Time.time >= nextFire)
            {
                nextFire = Time.time + fireEvery;
                var spot = fireSpots[fireIndex % fireSpots.Length];
                fireIndex++;
                if (spot != null) FireArea.Spawn(spot.position, 3.5f, burnTime + 30f, 10f);
            }

            // Tempo esgotado: a sala fica inabitável.
            if (BurnLeft <= 0f && playerHealth != null && !playerHealth.IsDead)
                playerHealth.TakeDamage(new DamageInfo(overtimeDps * Time.deltaTime, DamageType.Fogo, player.position, Vector3.zero, gameObject));
        }

        // ---------- Vitória (R6) ----------

        private void OnCoreDestroyed()
        {
            if (Defeated) return;
            Defeated = true;
            SetLasers(false);
            if (phase3Done != null) phase3Done.Done = true;
            Say("Não… a ilha… era min—", 2.5f);
            Sfx.Play("explosao", core.transform.position + Vector3.up, 1f, SoundCategory.Efeitos, 120f, 0f, 0.7f);

            // O link da VIGÍLIA cai: redes desligadas, máquinas apagam.
            HiveNetwork.SetAllOnline(false);
            foreach (var m in new List<MachineAI>(MachineAI.Active)) if (m != null && !m.IsDead) m.Destroyed();
            foreach (var d in new List<DroneAI>(DroneAI.Active)) if (d != null && !d.IsDead) d.Stun(999f);
            foreach (var r in core.GetComponentsInChildren<Renderer>()) r.enabled = false;
            if (core.TryGetComponent(out Collider c)) c.enabled = false;
        }

        private static void Say(string line, float duration = 4f)
        {
            if (string.IsNullOrEmpty(line) || RadioSystem.Instance == null) return;
            RadioSystem.Instance.Play(new[] { new DialogueLine(Speaker, line, duration) });
        }
    }
}
