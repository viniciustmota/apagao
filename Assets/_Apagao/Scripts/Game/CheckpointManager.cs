using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Apagao.AI.Hive;
using Apagao.Combat;
using Apagao.Crafting;
using Apagao.Player;
using Apagao.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Apagao.Audio;

namespace Apagao.Game
{
    /// <summary>O que um checkpoint guarda (spec 18 R5).</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int objective;
        public Vector3 position;
        public float yaw;
        public float health;
        public int[] resources;
        public int[] items;
        public int revolverLoaded, revolverReserve;
        public int espingardaLoaded, espingardaReserve;
        public int pemCharges;
        public float facaoDurability;
        public int loadout = -1;
        public List<string> gone = new List<string>();
    }

    /// <summary>
    /// Checkpoints (spec 18 R4–R7): salva em JSON num slot e, ao carregar a cena, restaura o Caio e
    /// remove o que já tinha sumido (inimigos destruídos, itens coletados, antenas sabotadas).
    /// Morrer recarrega a cena, e a cena recarregada volta ao último checkpoint. F9 apaga o save e recomeça.
    /// </summary>
    [DefaultExecutionOrder(-1000)] // antes de todos: desativa os objetos sumidos antes do Awake deles
    public class CheckpointManager : MonoBehaviour
    {
        public static CheckpointManager Instance { get; private set; }
        /// <summary>Um arquivo por cena: o save da ilha de teste não vaza para a campanha.</summary>
        public static string PathFor(string scene) => Path.Combine(Application.persistentDataPath, $"apagao_save_{scene}.json");
        public static string SavePath => PathFor(SceneManager.GetActiveScene().name);

        /// <summary>Save lido ao abrir a cena; null = jogo novo.</summary>
        public SaveData Loaded { get; private set; }
        public float LastSaveTime { get; private set; } = float.NegativeInfinity;

        private readonly HashSet<string> gone = new HashSet<string>();
        private readonly List<ISaveGone> toRestore = new List<ISaveGone>();
        private PlayerController player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public static void DeleteSave() => DeleteSave(SceneManager.GetActiveScene().name);

        public static void DeleteSave(string scene)
        {
            string path = PathFor(scene);
            if (File.Exists(path)) File.Delete(path);
        }

        private void Awake()
        {
            Instance = this;
            SaveId.Gone += OnGone;
            Loaded = Read();
            if (Loaded == null) return;

            foreach (var id in Loaded.gone) gone.Add(id);
            foreach (var s in Resources.FindObjectsOfTypeAll<SaveId>())
            {
                if (!s.gameObject.scene.IsValid() || !gone.Contains(s.Id)) continue; // ignora prefabs e assets
                var restorable = s.GetComponent<ISaveGone>();
                if (restorable != null)
                {
                    toRestore.Add(restorable); // antena, gerador: ficam na cena, desligados (depois do Start deles)
                    continue;
                }
                s.gameObject.SetActive(false);
                Destroy(s.gameObject);
            }
        }

        private void OnDestroy()
        {
            SaveId.Gone -= OnGone;
            if (player != null && player.TryGetComponent(out CraftingSystem crafting)) crafting.Crafted -= OnCrafted;
            if (Instance == this) Instance = null;
        }

        private IEnumerator Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player != null && player.TryGetComponent(out CraftingSystem crafting)) crafting.Crafted += OnCrafted;
            if (Loaded != null && player != null) ApplyPlayer(Loaded);

            yield return null; // espera o Start das antenas e geradores
            foreach (var r in toRestore)
                if (r is UnityEngine.Object o && o != null) r.RestoreGone();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame) NewGame();
        }

        private void OnGone(string id) => gone.Add(id);

        private void OnCrafted(Recipe recipe, bool atBench)
        {
            if (atBench) Save(); // R4: usar a bancada salva
        }

        public void NewGame()
        {
            DeleteSave();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ---------- Salvar ----------

        public void Save()
        {
            if (player == null) return;
            var health = player.GetComponent<Health>();
            if (health != null && health.IsDead) return;

            var data = new SaveData
            {
                objective = Campaign.Instance != null ? Campaign.Instance.CurrentIndex : 0,
                position = player.transform.position,
                yaw = player.transform.eulerAngles.y,
                health = health != null ? health.Current : 100f,
                gone = new List<string>(gone)
            };

            if (player.TryGetComponent(out Inventory inv))
            {
                data.resources = inv.SnapshotResources();
                data.items = inv.SnapshotItems();
            }
            if (player.TryGetComponent(out WeaponHolder weapons))
            {
                weapons.TryGetAmmo(weapons.Revolver, out data.revolverLoaded, out data.revolverReserve);
                weapons.TryGetAmmo(weapons.Espingarda, out data.espingardaLoaded, out data.espingardaReserve);
            }
            if (player.TryGetComponent(out Pem pem)) data.pemCharges = pem.Charges;
            if (player.TryGetComponent(out PlayerLoadout loadout)) data.loadout = loadout.Mask;
            var facao = player.GetComponentInChildren<Facao>();
            data.facaoDurability = facao != null ? facao.Durability : 0f;

            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
                LastSaveTime = Time.time;
                Sfx.Play2D("checkpoint", 0.5f, SoundCategory.Efeitos, 0f);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Apagão] Não consegui salvar o checkpoint: {e.Message}");
            }
        }

        // ---------- Carregar ----------

        private static SaveData Read()
        {
            try
            {
                if (!File.Exists(SavePath)) return null;
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Apagão] Save inválido, começando do zero: {e.Message}");
                return null;
            }
        }

        private void ApplyPlayer(SaveData data)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(data.position, Quaternion.Euler(0f, data.yaw, 0f));
            if (controller != null) controller.enabled = true;

            if (player.TryGetComponent(out Health health)) health.Restore(data.health);
            if (player.TryGetComponent(out Inventory inv)) inv.Restore(data.resources, data.items);
            if (player.TryGetComponent(out WeaponHolder weapons))
            {
                weapons.SetAmmo(weapons.Revolver, data.revolverLoaded, data.revolverReserve);
                weapons.SetAmmo(weapons.Espingarda, data.espingardaLoaded, data.espingardaReserve);
            }
            if (player.TryGetComponent(out Pem pem)) pem.SetCharges(data.pemCharges);
            if (data.loadout >= 0 && player.TryGetComponent(out PlayerLoadout loadout)) loadout.Mask = data.loadout;
            var facao = player.GetComponentInChildren<Facao>();
            if (facao != null) facao.SetDurability(data.facaoDurability);
        }
    }
}
