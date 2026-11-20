using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleM3Insert : Interactable
{
    private const int StepsPerTurn = 4;
    private const float TurnSpeed = 540f;

    [SerializeField] private TempleM3Mechanism mechanism;
    [SerializeField, Range(0, StepsPerTurn - 1)] private int quarterTurn;
    [SerializeField] private string turnHint = "Повернуть вставку";

    private Quaternion targetRotation;
    private bool turning;

    public bool IsAligned => quarterTurn == 0 && !turning;

    private void Awake()
    {
        transform.localRotation = RotationAtStep();
    }

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && mechanism != null && !mechanism.IsSolved && !turning;

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt) =>
        CanInteract(player) && !string.IsNullOrEmpty(buttonPrompt) ? $"{buttonPrompt} · {turnHint}" : null;

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        quarterTurn = (quarterTurn + 1) % StepsPerTurn;
        targetRotation = RotationAtStep();
        turning = true;
    }

    private void Update()
    {
        if (!turning)
            return;

        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation, targetRotation, TurnSpeed * Time.deltaTime);
        if (Quaternion.Angle(transform.localRotation, targetRotation) > 0.05f)
            return;

        transform.localRotation = targetRotation;
        turning = false;
        mechanism.CheckAlignment();
    }

    private Quaternion RotationAtStep() => Quaternion.Euler(0f, 0f, 90f * quarterTurn);
}
