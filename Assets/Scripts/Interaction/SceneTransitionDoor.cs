using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneTransitionDoor : Interactable
{
    [SerializeField] private string nextScene = "Temple_Final_Room";
    [SerializeField] private Vector3 arrivalPosition = new Vector3(0f, 0.1f, 0f);

    [SerializeField] private bool confirmRitualEntrance;
    [SerializeField] private TemplePassage entranceBarrier;

    private bool isLoading;

    private bool IsBarred => entranceBarrier != null && !entranceBarrier.IsPassable;
    public bool CanEnter => isActiveAndEnabled && !isLoading && !IsBarred;
    protected override string ActionLabel => confirmRitualEntrance ? "Войти в главный зал" : base.ActionLabel;
    internal override bool RattlesWhenBlocked => IsBarred && !entranceBarrier.IsMoving;

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt) =>
        IsBarred ? "Заперто" : base.GetPrompt(player, buttonPrompt);

    public override bool CanInteract(PlayerInteractor player) => CanEnter;

    public override void Interact(PlayerInteractor player)
    {
        if (!CanEnter || player == null)
            return;

        if (confirmRitualEntrance)
        {
            if (gameObject.scene == SceneManager.GetActiveScene())
                GameObjectives.ReachRitualEntrance();
            TempleArchiveJournal.GetOrCreate(player.gameObject)
                .ConfirmRitualEntrance(() => Enter(player.Controller, player.CameraObject));
            return;
        }

        Enter(player.Controller, player.CameraObject);
    }

    public void Enter(CharacterController ivan, GameObject playerCamera)
    {
        if (!CanEnter || ivan == null || playerCamera == null)
            return;

        if (string.IsNullOrWhiteSpace(nextScene) ||
            !Application.CanStreamedLevelBeLoaded(nextScene))
        {
            Debug.LogError($"Сцена «{nextScene}» должна быть включена в Scene List.", this);
            return;
        }

        if (SceneManager.GetSceneByName(nextScene).isLoaded)
        {
            Debug.LogError($"Сцена «{nextScene}» уже загружена.", this);
            return;
        }

        isLoading = true;
        GameAudio.ThroughDoor();
        StartCoroutine(LoadLocation(ivan, playerCamera));
    }

    private IEnumerator LoadLocation(CharacterController ivan, GameObject playerCamera)
    {
        Scene oldScene = gameObject.scene;
        StoryComics comics = GameCheckpoint.Comics;
        bool ritual = nextScene == "Temple_Final_Room" && comics != null;
        if (ritual)
        {
            GameCheckpoint.SaveRitualEntrance(ivan.gameObject, arrivalPosition);
            bool hasAllDocuments = ivan.TryGetComponent(out TempleArchiveJournal journal) &&
                journal.DocumentCount == TempleArchiveJournal.DocumentTotal;
            yield return comics.PlayRitualEntrance(hasAllDocuments);
        }
        yield return SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Additive);

        Scene newScene = SceneManager.GetSceneByName(nextScene);
        MovePlayer(newScene, ivan, playerCamera, arrivalPosition);
        SceneManager.SetActiveScene(newScene);
        if (ritual) comics.Close();
        SceneManager.UnloadSceneAsync(oldScene);
    }

    internal static void MovePlayer(Scene scene, CharacterController player, GameObject playerCamera, Vector3 arrival)
    {
        Pose? arenaEntry = DiscardStandaloneRig(scene, player.gameObject, playerCamera);
        SceneManager.MoveGameObjectToScene(player.gameObject, scene);
        if (playerCamera.scene != scene)
            SceneManager.MoveGameObjectToScene(playerCamera, scene);

        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.TryGetComponent(out AngelSealAttack angel)) angel.SetTarget(player.transform);

        bool wasEnabled = player.enabled;
        player.enabled = false;
        if (arenaEntry.HasValue)
        {
            Pose pose = arenaEntry.Value;
            player.transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (playerCamera.TryGetComponent(out IvanOrbitCamera orbit))
                orbit.AlignWithTarget();
        }
        else player.transform.position = arrival;
        player.enabled = wasEnabled;
    }

    private static Pose? DiscardStandaloneRig(Scene scene, GameObject player, GameObject playerCamera)
    {
        Pose? arenaEntry = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == player || root == playerCamera)
                continue;

            bool standalonePlayer = root.CompareTag("Player") && root.TryGetComponent(out CharacterController _);
            bool standaloneCamera = root.CompareTag("MainCamera") && root.TryGetComponent(out Camera _);
            if (standalonePlayer || standaloneCamera)
            {
                // Use the same authored entrance for the standalone fight and the story player.
                if (standalonePlayer && scene.name == NimbEncounter.ArenaScene)
                    arenaEntry = new Pose(root.transform.position, root.transform.rotation);
                root.SetActive(false);
                Destroy(root);
                continue;
            }

            foreach (Camera camera in root.GetComponentsInChildren<Camera>())
            {
                if (!camera.CompareTag("MainCamera"))
                    continue;
                camera.enabled = false;
                if (camera.TryGetComponent(out AudioListener listener))
                    listener.enabled = false;
            }
        }
        return arenaEntry;
    }
}
