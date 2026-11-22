using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneTransitionDoor : Interactable
{
    [SerializeField] private string nextScene = "Temple_Final_Room";
    [SerializeField] private Vector3 arrivalPosition = new Vector3(0f, 0.1f, 0f);

    [SerializeField] private bool confirmRitualEntrance;

    private bool isLoading;

    public bool CanEnter => isActiveAndEnabled && !isLoading;

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
        StartCoroutine(LoadLocation(ivan, playerCamera));
    }

    private IEnumerator LoadLocation(CharacterController ivan, GameObject playerCamera)
    {
        Scene oldScene = gameObject.scene;
        yield return SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Additive);

        Scene newScene = SceneManager.GetSceneByName(nextScene);
        MovePlayer(newScene, ivan, playerCamera, arrivalPosition);
        SceneManager.SetActiveScene(newScene);
        SceneManager.UnloadSceneAsync(oldScene);
    }

    internal static void MovePlayer(Scene scene, CharacterController player, GameObject playerCamera, Vector3 arrival)
    {
        DiscardStandaloneRig(scene, player.gameObject, playerCamera);
        SceneManager.MoveGameObjectToScene(player.gameObject, scene);
        if (playerCamera.scene != scene)
            SceneManager.MoveGameObjectToScene(playerCamera, scene);

        bool wasEnabled = player.enabled;
        player.enabled = false;
        player.transform.position = arrival;
        player.enabled = wasEnabled;
    }

    private static void DiscardStandaloneRig(Scene scene, GameObject player, GameObject playerCamera)
    {
        // The ritual room also supports being opened directly with its own player and camera.
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == player || root == playerCamera)
                continue;

            bool standalonePlayer = root.CompareTag("Player") && root.TryGetComponent(out CharacterController _);
            bool standaloneCamera = root.CompareTag("MainCamera") && root.TryGetComponent(out Camera _);
            if (standalonePlayer || standaloneCamera)
            {
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
    }
}
