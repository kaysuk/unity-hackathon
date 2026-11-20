using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneTransitionDoor : Interactable
{
    [SerializeField] private string nextScene = "Temple_Final_Room";
    [SerializeField] private Vector3 arrivalPosition = new Vector3(0f, 0.1f, 0f);

    private bool isLoading;

    public bool CanEnter => isActiveAndEnabled && !isLoading;

    public override bool CanInteract(PlayerInteractor player) => CanEnter;

    public override void Interact(PlayerInteractor player)
    {
        Enter(player.Controller, player.CameraObject);
    }

    public void Enter(CharacterController ivan, GameObject playerCamera)
    {
        if (!CanEnter)
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
        SceneManager.MoveGameObjectToScene(ivan.gameObject, newScene);
        SceneManager.MoveGameObjectToScene(playerCamera, newScene);
        SceneManager.SetActiveScene(newScene);

        ivan.enabled = false;
        ivan.transform.position = arrivalPosition;
        ivan.enabled = true;

        SceneManager.UnloadSceneAsync(oldScene);
    }
}
