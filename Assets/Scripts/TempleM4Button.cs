using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class TempleM4Button : Interactable
{
    [SerializeField] private TempleM4Puzzle puzzle;
    [SerializeField] private TempleM4Puzzle.Symbol symbol;
    [SerializeField] private string pressHint;
    [SerializeField] private Transform face;

    private Vector3 restingPosition;
    private float releaseAt;

    private void Awake()
    {
        restingPosition = face.localPosition;
    }

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && puzzle != null && puzzle.isActiveAndEnabled && !puzzle.IsSolved;

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt) =>
        CanInteract(player) && !string.IsNullOrEmpty(buttonPrompt) ? $"{buttonPrompt} · {pressHint}" : null;

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        face.localPosition = restingPosition + Vector3.back * .025f;
        releaseAt = Time.time + .12f;
        puzzle.Press(symbol);
    }

    private void Update()
    {
        if (releaseAt <= 0f || Time.time < releaseAt)
            return;

        face.localPosition = restingPosition;
        releaseAt = 0f;
    }
}
