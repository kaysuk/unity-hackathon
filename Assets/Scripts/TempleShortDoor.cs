using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleShortDoor : Interactable, IGraphPassage
{
    [SerializeField] private Transform hinge;
    [SerializeField] private Transform oppositeHinge;
    [SerializeField] private bool permanentlyLocked;
    [SerializeField] private bool twoSidedToggle;
    [SerializeField] private float openAngle = -90f;
    private Quaternion closedRotation, openRotation;
    private Quaternion oppositeClosedRotation;
    private Quaternion TargetRotation => IsOpen ? openRotation : closedRotation;

    public bool IsOpen { get; private set; }

    public event System.Action PassabilityChanged;
    public bool IsPassable => IsOpen && hinge.localRotation == openRotation;

    private void Awake()
    {
        closedRotation = hinge.localRotation;
        if (oppositeHinge != null) oppositeClosedRotation = oppositeHinge.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    public override bool CanInteract(PlayerInteractor player)
    {
        return base.CanInteract(player) && !permanentlyLocked && player != null &&
            (twoSidedToggle ? hinge.localRotation == TargetRotation :
                !IsOpen && hinge.parent.InverseTransformPoint(player.transform.position).z > 0.08f);
    }

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        if (!isActiveAndEnabled || (!twoSidedToggle && IsOpen) || hinge.localRotation != TargetRotation)
            return null;
        if (permanentlyLocked)
            return "Дверь заперта";
        if (!CanInteract(player))
            return "Заперто изнутри";
        string action = IsOpen ? "Закрыть дверь" : "Открыть дверь";
        return string.IsNullOrEmpty(buttonPrompt) ? null : $"{buttonPrompt} · {action}";
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player)) return;
        IsOpen = !IsOpen;
        if (!IsOpen) PassabilityChanged?.Invoke();
    }

    private void Update()
    {
        if (hinge.localRotation != TargetRotation)
        {
            hinge.localRotation = Quaternion.RotateTowards(hinge.localRotation, TargetRotation, 200f * Time.deltaTime);
            if (oppositeHinge != null)
            {
                var turn = Quaternion.Inverse(closedRotation) * hinge.localRotation;
                oppositeHinge.localRotation = oppositeClosedRotation * Quaternion.Inverse(turn);
            }
            if (IsPassable) PassabilityChanged?.Invoke();
        }
    }
}
