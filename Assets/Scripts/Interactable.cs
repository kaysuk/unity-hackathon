using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    public virtual bool CanInteract(PlayerInteractor player) => isActiveAndEnabled;

    public virtual string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        return CanInteract(player) ? buttonPrompt : null;
    }

    public abstract void Interact(PlayerInteractor player);
}
