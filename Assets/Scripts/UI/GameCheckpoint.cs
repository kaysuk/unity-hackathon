using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(GameHudBinding))]
[DisallowMultipleComponent]
public sealed class GameCheckpoint : MonoBehaviour
{
    private const string EntranceScene = "temple_v2";
    private const string RitualScene = "Temple_Final_Room";
    private const string SaveKey = "Karamazovy.StoryCheckpoint";
    private const int SaveVersion = 1;

    [Serializable]
    private sealed class Snapshot
    {
        public int version = SaveVersion;
        public string scene;
        public Vector3 position;
        public Quaternion rotation;
        public GameObjectives.State objectives;
        public PlayerInventory.Entry[] inventory;
        public int archiveDocuments;
        public bool fullFamilyTruth;
        public bool hasHealth;
        public float hp;
        public bool hasPistol;
        public int magazineCapacity;
        public int magazine;
        public int reserve;
    }

    private static GameCheckpoint instance;
    private GameHudBinding hud;
    private GameObject player;
    private CombatHealth health;
    private Snapshot currentCheckpoint;
    private Scene boundScene;
    private bool storyRun;
    private bool loading;
    private bool retrying;

    public static bool HasSave => ReadSave() != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var root = new GameObject("Game UI", typeof(RectTransform));
        DontDestroyOnLoad(root);
        instance = root.AddComponent<GameCheckpoint>();
    }

    public static bool StartNewGame() => instance != null && instance.TryLoad(null);

    public static bool ContinueGame()
    {
        Snapshot save = ReadSave();
        return save != null && instance != null && instance.TryLoad(save);
    }

    private void Awake()
    {
        hud = GetComponent<GameHudBinding>();
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
            BindScene(scene);
    }

    private void OnActiveSceneChanged(Scene previous, Scene next) => BindScene(next);

    private void BindScene(Scene scene)
    {
        if (!scene.IsValid() || scene == boundScene)
            return;
        boundScene = scene;
        GameObjectives.ConfigureScene(scene.name);
        BindPlayer(FindPlayer(scene));
        if (storyRun && !loading && player != null && IsStoryScene(scene.name))
            SaveEntry(scene.name);
    }

    private void BindPlayer(GameObject actor)
    {
        if (health != null)
            health.onDeath.RemoveListener(OnPlayerDeath);

        player = actor;
        health = actor != null ? actor.GetComponent<CombatHealth>() : null;
        if (health != null)
            health.onDeath.AddListener(OnPlayerDeath);
        hud.Bind(actor);
    }

    private static GameObject FindPlayer(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.activeInHierarchy && root.CompareTag("Player"))
                return root;
        return null;
    }

    private bool TryLoad(Snapshot save)
    {
        if (loading || !Application.CanStreamedLevelBeLoaded(EntranceScene))
            return false;
        StartCoroutine(LoadStory(save));
        return true;
    }

    private IEnumerator LoadStory(Snapshot save)
    {
        loading = true;
        hud.SetLoading(true);
        // Retrying the entrance scene also resets its puzzles and patrols.
        yield return SceneManager.LoadSceneAsync(EntranceScene, LoadSceneMode.Single);
        Scene entrance = SceneManager.GetSceneByName(EntranceScene);
        GameObject actor = FindPlayer(entrance);

        if (actor != null && save?.scene == RitualScene)
        {
            var interaction = actor.GetComponent<PlayerInteractor>();
            if (interaction != null)
            {
                yield return SceneManager.LoadSceneAsync(RitualScene, LoadSceneMode.Additive);
                Scene ritual = SceneManager.GetSceneByName(RitualScene);
                SceneTransitionDoor.MovePlayer(ritual, interaction.Controller, interaction.CameraObject, save.position);
                SceneManager.SetActiveScene(ritual);
                yield return SceneManager.UnloadSceneAsync(entrance);
            }
        }

        Scene current = SceneManager.GetActiveScene();
        if (actor != null && (save == null || current.name == save.scene))
        {
            if (save != null)
                ApplySave(save, actor);
            else if (actor.TryGetComponent(out PistolCombat pistol))
                pistol.RestoreAmmo(12, 24, 12); // The playable story starts after scene 505.

            storyRun = true;
            currentCheckpoint = save;
            if (save == null)
                SaveEntry(current.name);
            BindPlayer(actor);
        }
        else
        {
            storyRun = false;
            currentCheckpoint = null;
        }

        loading = false;
        hud.SetLoading(false);
    }

    private static void ApplySave(Snapshot save, GameObject actor)
    {
        var controller = actor.GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled) controller.enabled = false;
        actor.transform.SetPositionAndRotation(save.position, save.rotation);
        if (controllerWasEnabled) controller.enabled = true;

        if (save.hasHealth && actor.TryGetComponent(out CombatHealth hitPoints))
            hitPoints.RestoreHP(save.hp);
        if (save.hasPistol && actor.TryGetComponent(out PistolCombat pistol))
            pistol.RestoreAmmo(save.magazine, save.reserve, save.magazineCapacity);
        if (actor.TryGetComponent(out PlayerInventory inventory))
            inventory.Restore(save.inventory);
        if (save.archiveDocuments != 0)
            TempleArchiveJournal.GetOrCreate(actor).RestoreProgress(save.archiveDocuments, save.fullFamilyTruth);
        GameObjectives.Restore(save.objectives);
    }

    private void SaveEntry(string scene)
    {
        var save = new Snapshot
        {
            scene = scene,
            position = player.transform.position,
            rotation = player.transform.rotation,
            objectives = GameObjectives.Capture()
        };
        if (player.TryGetComponent(out PlayerInventory inventory))
            save.inventory = inventory.Capture();
        if (player.TryGetComponent(out TempleArchiveJournal journal))
        {
            save.archiveDocuments = journal.CollectedMask;
            save.fullFamilyTruth = journal.FullFamilyTruth;
        }
        if (health != null)
        {
            save.hasHealth = true;
            save.hp = health.HP;
        }
        if (player.TryGetComponent(out PistolCombat pistol))
        {
            save.hasPistol = true;
            save.magazineCapacity = pistol.MagazineCapacity;
            save.magazine = pistol.Ammo;
            save.reserve = pistol.ReserveAmmo;
        }
        currentCheckpoint = save;
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save));
        PlayerPrefs.Save();
    }

    private static Snapshot ReadSave()
    {
        string data = PlayerPrefs.GetString(SaveKey);
        if (string.IsNullOrEmpty(data))
            return null;
        try
        {
            var save = JsonUtility.FromJson<Snapshot>(data);
            return save != null && save.version == SaveVersion && IsStoryScene(save.scene) &&
                Application.CanStreamedLevelBeLoaded(save.scene) ? save : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsStoryScene(string scene) => scene == EntranceScene || scene == RitualScene;

    private void OnPlayerDeath()
    {
        if (storyRun && currentCheckpoint != null && !loading && !retrying)
            StartCoroutine(Retry());
    }

    private IEnumerator Retry()
    {
        retrying = true;
        yield return new WaitForSecondsRealtime(2f);
        if (health != null && health.IsDead)
            TryLoad(currentCheckpoint);
        retrying = false;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (health != null)
            health.onDeath.RemoveListener(OnPlayerDeath);
        if (instance == this)
            instance = null;
    }
}
