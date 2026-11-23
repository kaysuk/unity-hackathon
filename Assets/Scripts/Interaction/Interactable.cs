using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    public virtual bool RequiresDirectAim => false;

    protected virtual string ActionLabel => null;

    public virtual bool CanInteract(PlayerInteractor player) => isActiveAndEnabled;

    public virtual string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        if (!CanInteract(player))
            return null;

        string label = ActionLabel;
        if (label == null)
            return buttonPrompt;

        return string.IsNullOrEmpty(buttonPrompt) ? null : FormatBoundAction(buttonPrompt, label);
    }

    protected static string FormatBoundAction(string buttonPrompt, string action) =>
        $"{buttonPrompt} · {action}";

    public abstract void Interact(PlayerInteractor player);
}
