using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleShortDoor : Interactable, IGraphPassage
{
    [SerializeField] private Transform hinge;
    [SerializeField] private bool permanentlyLocked;
    private Quaternion openRotation;

    public bool IsOpen { get; private set; }

    public event System.Action PassabilityChanged;
    public bool IsPassable => IsOpen && hinge.localRotation == openRotation;

    private void Awake()
    {
        openRotation = hinge.localRotation * Quaternion.Euler(0f, -90f, 0f);
    }

    public override bool CanInteract(PlayerInteractor player)
    {
        return base.CanInteract(player) && !permanentlyLocked && !IsOpen && player != null &&
            hinge.parent.InverseTransformPoint(player.transform.position).z > 0.08f;
    }

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        if (!isActiveAndEnabled || IsOpen)
            return null;
        if (permanentlyLocked)
            return "Дверь заперта";
        if (!CanInteract(player))
            return "Заперто изнутри";
        return string.IsNullOrEmpty(buttonPrompt) ? null : FormatBoundAction(buttonPrompt, "Открыть дверь");
    }

    public override void Interact(PlayerInteractor player)
    {
        if (CanInteract(player))
            IsOpen = true;
    }

    private void Update()
    {
        if (IsOpen && hinge.localRotation != openRotation)
        {
            hinge.localRotation = Quaternion.RotateTowards(hinge.localRotation, openRotation, 200f * Time.deltaTime);
            if (IsPassable) PassabilityChanged?.Invoke();
        }
    }
}
