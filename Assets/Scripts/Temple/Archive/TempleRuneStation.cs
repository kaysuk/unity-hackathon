using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleRuneStation : Interactable
{
    private const int GlyphCount = TempleRunePuzzle.GlyphCount;
    private const float StepAngle = 360f / GlyphCount;
    private const float TurnDuration = 0.72f;
    private const float SeatingDuration = 0.42f;

    [SerializeField] private TempleRunePuzzle puzzle;
    [SerializeField, Range(0, GlyphCount - 1)] private int initialIndex;
    [SerializeField, Range(0, GlyphCount - 1)] private int targetIndex;
    [SerializeField, Range(0, GlyphCount - 1)] private int stoneIndex;
    [SerializeField] private GameObject placedStone;
    [SerializeField] private Transform dial;
    [SerializeField] private Vector3 dialAxis = Vector3.forward;
    [SerializeField] private TemplePassage socketLift;

    private Quaternion restingRotation;
    private bool isTurning;
    private float turnStartAngle;
    private float turnTime;
    private bool isSeating;
    private float seatingTime;
    private Vector3 seatPosition;
    private Quaternion seatRotation;

    public int OccupantIndex { get; private set; } = -1;
    public bool HasStone => OccupantIndex >= 0 && !isSeating;
    public int CurrentIndex { get; private set; }
    public int TargetIndex => targetIndex;
    public int StoneIndex => stoneIndex;
    public override bool RequiresDirectAim => true;
    internal bool IsAligned => !isTurning && CurrentIndex == targetIndex;

    private bool PuzzleActive => isActiveAndEnabled && puzzle != null && puzzle.isActiveAndEnabled && !puzzle.IsSolved;
    private bool DialReady => PuzzleActive && !puzzle.AreDialsAligned;
    private bool SocketReady => PuzzleActive && puzzle.AreDialsAligned && socketLift.isActiveAndEnabled &&
        socketLift.IsOpen && !socketLift.IsMoving;

    internal bool IsConfiguredFor(TempleRunePuzzle controller)
    {
        return puzzle == controller && dial != null && dialAxis != Vector3.zero && socketLift != null &&
            placedStone != null && placedStone.TryGetComponent<MeshFilter>(out _) &&
            placedStone.TryGetComponent<MeshRenderer>(out _) && ValidGlyph(initialIndex) &&
            ValidGlyph(targetIndex) && ValidGlyph(stoneIndex);
    }

    private static bool ValidGlyph(int index) => index >= 0 && index < GlyphCount;

    private void Awake()
    {
        if (puzzle == null || !IsConfiguredFor(puzzle))
        {
            enabled = false;
            return;
        }

        CurrentIndex = initialIndex;
        restingRotation = dial.localRotation;
        seatPosition = placedStone.transform.localPosition;
        seatRotation = placedStone.transform.localRotation;
        placedStone.SetActive(false);
    }

    public override bool CanInteract(PlayerInteractor player) => DialReady && !isTurning;

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt) =>
        DialReady ? WithButton(buttonPrompt, "Повернуть плиту") : null;

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        turnStartAngle = (initialIndex - CurrentIndex) * StepAngle;
        CurrentIndex = (CurrentIndex + 1) % GlyphCount;
        turnTime = 0f;
        isTurning = true;
    }

    private void AdvanceDial()
    {
        turnTime += Time.deltaTime;
        float angle = Mathf.SmoothStep(turnStartAngle, turnStartAngle - StepAngle, turnTime / TurnDuration);
        dial.localRotation = restingRotation * Quaternion.AngleAxis(angle, dialAxis);
        if (turnTime >= TurnDuration)
        {
            isTurning = false;
            puzzle.CheckDials();
        }
    }

    internal void RaiseSocket() => socketLift.SetOpen(true);

    internal bool CanUseSocket(PlayerInventory inventory)
    {
        if (!SocketReady || isSeating || inventory == null)
            return false;
        if (HasStone)
            return true;

        for (int i = 0; i < GlyphCount; i++)
        {
            if (inventory.HasItem(TempleRunePuzzle.RuneIds[i]) && puzzle.RuneModels[i] != null)
                return true;
        }

        return false;
    }

    internal string GetSocketPrompt(PlayerInventory inventory, string buttonPrompt)
    {
        if (!SocketReady || isSeating)
            return null;
        if (HasStone)
            return WithButton(buttonPrompt, "Забрать камень");

        return CanUseSocket(inventory) ? WithButton(buttonPrompt, "Выбрать камень") : "Нет рунных камней";
    }

    internal void UseSocket(PlayerInteractor player)
    {
        PlayerInventory inventory = player != null ? player.Inventory : null;
        if (!CanUseSocket(inventory))
            return;

        if (HasStone)
        {
            if (inventory.TryReturn(TempleRunePuzzle.RuneIds[OccupantIndex]))
            {
                OccupantIndex = -1;
                placedStone.SetActive(false);
            }
            return;
        }

        TempleRunePicker.Open(player, puzzle.RuneModels, TempleRunePuzzle.RuneIds, index => PlaceStone(inventory, index));
    }

    private void PlaceStone(PlayerInventory inventory, int index)
    {
        if (!SocketReady || OccupantIndex >= 0 || !ValidGlyph(index) || inventory == null ||
            puzzle.RuneModels[index] == null ||
            !inventory.TryUse(TempleRunePuzzle.RuneIds[index]))
            return;

        var model = puzzle.RuneModels[index];
        placedStone.GetComponent<MeshFilter>().sharedMesh = model.GetComponent<MeshFilter>().sharedMesh;
        placedStone.GetComponent<MeshRenderer>().sharedMaterials = model.sharedMaterials;
        OccupantIndex = index;
        isSeating = true;
        seatingTime = 0f;
        SetSeatingPose(0f);
        placedStone.SetActive(true);
    }

    private void Update()
    {
        if (isTurning)
            AdvanceDial();
        if (!isSeating)
            return;

        seatingTime += Time.deltaTime;
        SetSeatingPose(Mathf.SmoothStep(0f, 1f, seatingTime / SeatingDuration));
        if (seatingTime >= SeatingDuration)
        {
            isSeating = false;
            puzzle.CheckStones();
        }
    }

    private void SetSeatingPose(float progress)
    {
        Vector3 position = seatPosition + Vector3.up * ((1f - progress) * .30f);
        Quaternion tilt = Quaternion.Slerp(Quaternion.Euler(-14f, 9f, 0f), Quaternion.identity, progress);
        placedStone.transform.SetLocalPositionAndRotation(position, seatRotation * tilt);
    }

    private static string WithButton(string buttonPrompt, string action) =>
        string.IsNullOrEmpty(buttonPrompt) ? action : FormatBoundAction(buttonPrompt, action);
}
