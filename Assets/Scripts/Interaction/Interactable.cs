using System.Collections.Generic;
using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    private static readonly List<Interactable> active = new();
    private Collider interactionCollider;

    internal static IReadOnlyList<Interactable> Active => active;
    internal virtual Transform HighlightRoot => transform;
    internal Vector3 SensePosition => interactionCollider != null && interactionCollider.enabled &&
        interactionCollider.gameObject.activeInHierarchy ? interactionCollider.bounds.center : transform.position;

    protected virtual void OnEnable()
    {
        interactionCollider = GetComponentInChildren<Collider>();
        active.Add(this);
    }

    protected virtual void OnDisable() => active.Remove(this);

    public virtual bool RequiresDirectAim => false;

    // Checked only when the player presses Interact on a target that refused the action.
    internal virtual bool RattlesWhenBlocked => false;

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
