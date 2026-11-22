using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleMewLever : Interactable
{
    [SerializeField] private Transform leverPivot;
    [SerializeField] private TemplePassage[] doors;

    private Quaternion pulledRotation;

    public bool IsPulled { get; private set; }

    private void Awake()
    {
        pulledRotation = leverPivot.localRotation * Quaternion.Euler(0f, 0f, -78f);
    }

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && !IsPulled;

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt) =>
        CanInteract(player) && !string.IsNullOrEmpty(buttonPrompt)
            ? $"{buttonPrompt} · Потянуть рычаг"
            : null;

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        IsPulled = true;
        foreach (TemplePassage door in doors)
            door.SetOpen(true);
    }

    private void Update()
    {
        if (IsPulled)
        {
            leverPivot.localRotation = Quaternion.RotateTowards(
                leverPivot.localRotation, pulledRotation, 240f * Time.deltaTime);
        }
    }
}
