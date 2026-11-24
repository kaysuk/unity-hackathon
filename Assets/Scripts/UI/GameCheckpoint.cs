using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(GameHudBinding))]
[DisallowMultipleComponent]
public sealed class GameCheckpoint : MonoBehaviour
{
    private const string Street = "Church_Exterior";
    private const string Church = "Church_Interior";
    private const string FirstTemple = "temple_firts_v2";
    private const string EntranceScene = "temple_v2";
    private const string RitualScene = "Temple_Final_Room";
    private const string SaveKey = "Karamazovy.StoryCheckpoint";
    private const int SaveVersion = 2;

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
        public string sealCalculation;
        public bool fullFamilyTruth;
        public bool returnedToChurch;
        public bool dmitrySpared;
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

    private ChurchBookshelf bookshelf;
    private bool returnedToChurch;
    internal static StoryComics Comics => instance != null ? instance.comics : null;
    private StoryComics comics;

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
        gameObject.AddComponent<StorySpeech>();
        comics = gameObject.AddComponent<StoryComics>();
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
        if (scene.name == "MainMenu" && !loading) storyRun = false;
        GameObjectives.ConfigureScene(scene.name);
        BindPlayer(FindPlayer(scene));
        if (scene.name == RitualScene)
            GameObjectives.EnterStage(GameObjectives.Stage.DestroyAltars);
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
        storyRun = true;
        returnedToChurch = save?.returnedToChurch ?? false;
        comics.DmitrySpared = save?.dmitrySpared ?? false;
        if (save == null) yield return comics.Play(5, 14);
        string destination = save?.scene ?? Church;
        // The church interior and first temple use the street actor and camera.
        // A ritual checkpoint uses the archive actor with the complete interaction rig.
        string carrier = destination == RitualScene ? EntranceScene :
            destination == Church || destination == FirstTemple ? Street : destination;
        // Restoring the ritual actor briefly loads the archive. Keep the boss score through that handoff.
        if (destination == RitualScene) GameAudio.HoldScore(GameAudio.BossTrack);
        yield return SceneManager.LoadSceneAsync(carrier, LoadSceneMode.Single);
        if (carrier != destination)
            yield return MoveTo(destination, save?.position ?? new Vector3(0f, 0.1f, -13f));
        FinishEntry(save);
        if (destination == RitualScene) GameAudio.ReleaseScore();
    }

    private IEnumerator MoveTo(string destination, Vector3 arrival)
    {
        Scene previous = SceneManager.GetActiveScene();
        var body = player.GetComponent<CharacterController>();
        Camera camera = null;
        foreach (var root in previous.GetRootGameObjects())
            if (root.CompareTag("MainCamera") && root.TryGetComponent(out camera)) break;
        yield return SceneManager.LoadSceneAsync(destination, LoadSceneMode.Additive);
        var next = SceneManager.GetSceneByName(destination);
        SceneTransitionDoor.MovePlayer(next, body, camera.gameObject, arrival);
        SceneManager.SetActiveScene(next);
        yield return SceneManager.UnloadSceneAsync(previous);
    }

    private void FinishEntry(Snapshot save)
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene == Street)
        {
            GameObjectives.EnterStage(returnedToChurch ? GameObjectives.Stage.FindAnotherEntrance : GameObjectives.Stage.InspectChurch);
            if (!returnedToChurch)
            {
                var cordon = FindAnyObjectByType<ChurchCordon>();
                if (cordon != null) cordon.OpenForFirstVisit();
            }
        }
        else if (scene == Church)
        {
            GameObjectives.EnterStage(returnedToChurch ? GameObjectives.Stage.ReachBasementUndetected : GameObjectives.Stage.CheckMarks);
            bookshelf = FindAnyObjectByType<ChurchBookshelf>();
            if (bookshelf != null && player != null)
            {
                if (!player.TryGetComponent(out PlayerInteractor _))
                    player.AddComponent<PlayerInteractor>();
                bookshelf.Prepare(player.transform, returnedToChurch);
            }
        }
        else if (scene == FirstTemple)
            GameObjectives.EnterStage(GameObjectives.Stage.InvestigateVoices);

        if (player != null)
        {
            if (save != null) ApplySave(save, player);
            else if (scene == EntranceScene && player.TryGetComponent(out PistolCombat pistol))
                pistol.RestoreAmmo(12, 24, 12);
            if (save != null && scene == RitualScene)
                GameObjectives.EnterStage(GameObjectives.Stage.DestroyAltars);
            if (save == null) SaveEntry(scene);
            else currentCheckpoint = save;
        }
        comics.Close();
        loading = false;
        hud.SetLoading(false);
        if (returnedToChurch && scene == Church) StorySpeech.Say("lights_out_stealth");
    }

    internal static void Approach(StoryWaypoint.Passage passage, Vector3 target, float radius, Scene scene)
    {
        var run = instance;
        if (run == null || !run.storyRun || run.loading || run.player == null ||
            scene != run.boundScene || Time.timeScale <= 0f || run.health != null && run.health.IsDead) return;
        if ((run.player.transform.position - target).sqrMagnitude > radius * radius) return;
        if (passage == StoryWaypoint.Passage.Basement && run.bookshelf != null && !run.bookshelf.Open) return;
        bool rear = passage == StoryWaypoint.Passage.RearDoor;
        if ((rear || passage == StoryWaypoint.Passage.FrontDoor) && rear != run.returnedToChurch) return;
        run.StartCoroutine(run.Travel(passage));
    }

    private IEnumerator Travel(StoryWaypoint.Passage passage)
    {
        loading = true;
        hud.SetLoading(true);
        switch (passage)
        {
            case StoryWaypoint.Passage.FrontDoor:
            case StoryWaypoint.Passage.RearDoor:
                GameAudio.ThroughDoor();
                yield return MoveTo(Church, returnedToChurch ? new Vector3(5f, 0.1f, 13f) : new Vector3(0f, 0.1f, -13f));
                break;
            case StoryWaypoint.Passage.Basement when returnedToChurch:
                GameAudio.Play(SoundCue.Unlock, player.transform.position, 0.46f);
                StorySpeech.Say("key_fits");
                StorySpeech.Say("lower_door_opened");
                while (StorySpeech.Busy) yield return null;
                yield return SceneManager.LoadSceneAsync(EntranceScene);
                break;
            case StoryWaypoint.Passage.Basement:
            case StoryWaypoint.Passage.FirstAltar: // Also supports checkpoints from the earlier first-temple route.
                StorySpeech.Say("first_descent");
                while (StorySpeech.Busy) yield return null;
                yield return comics.Play(15, 29);
                returnedToChurch = true;
                yield return SceneManager.LoadSceneAsync(Street);
                break;
        }
        FinishEntry(null);
    }

    internal static void FinishRitual()
    {
        if (instance == null || instance.loading || instance.player == null) return;
        instance.StartCoroutine(instance.ShowEnding());
    }

    private IEnumerator ShowEnding()
    {
        loading = true;
        hud.SetLoading(true);
        bool truth = player.TryGetComponent(out TempleArchiveJournal journal) && journal.FullFamilyTruth;
        yield return comics.Play(34, 38, StoryComics.Ending(comics.DmitrySpared, truth));
        storyRun = false;
        currentCheckpoint = null;
        yield return SceneManager.LoadSceneAsync("MainMenu");
        comics.Close();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        int documents = save.archiveDocuments;
        var objectives = save.objectives;
        if (save.version == 1)
        {
            // The workshop register occupied D06 in version 1. Room 6 kept a separate pickup.
            const int oldRegister = 1 << 5;
            bool alreadyReadEnding = save.fullFamilyTruth && documents == (oldRegister << 1) - 1;
            documents = (documents & (oldRegister - 1)) | ((documents & oldRegister) << 1);
            if (alreadyReadEnding)
                documents |= oldRegister;
        }
        if (documents != 0 || !string.IsNullOrEmpty(save.sealCalculation) ||
            (inventory != null && TempleSealDocument.CountCollected(inventory) > 0))
        {
            var journal = TempleArchiveJournal.GetOrCreate(actor);
            journal.RestoreProgress(documents, save.fullFamilyTruth);
            journal.SealCalculation = save.sealCalculation;
            objectives.DocumentsCollected = journal.DocumentCount == TempleArchiveJournal.DocumentTotal;
        }
        GameObjectives.Restore(objectives);
    }

    private void SaveEntry(string scene)
    {
        var save = new Snapshot
        {
            scene = scene,
            returnedToChurch = returnedToChurch,
            dmitrySpared = comics.DmitrySpared,
            position = player.transform.position,
            rotation = player.transform.rotation,
            objectives = GameObjectives.Capture()
        };
        if (player.TryGetComponent(out PlayerInventory inventory))
            save.inventory = inventory.Capture();
        if (player.TryGetComponent(out TempleArchiveJournal journal))
        {
            save.archiveDocuments = journal.CollectedMask;
            save.sealCalculation = journal.SealCalculation;
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
            return save != null && save.version >= 1 && save.version <= SaveVersion && IsStoryScene(save.scene) &&
                Application.CanStreamedLevelBeLoaded(save.scene) ? save : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsStoryScene(string scene) => scene is Street or Church or FirstTemple or EntranceScene or RitualScene;

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
