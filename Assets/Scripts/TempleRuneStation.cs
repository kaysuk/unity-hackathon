using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleRuneStation : Interactable
{
    private const int GlyphCount = TempleRunePuzzle.GlyphCount;
    private const float StepAngle = 360f / GlyphCount;
    private const float TurnSpeed = 320f;

    private static readonly string[] RuneIds =
    {
        "rune_room_01", "rune_room_02", "rune_room_03",
        "rune_room_04", "rune_room_05", "rune_room_06"
    };

    [SerializeField] private TempleRunePuzzle puzzle;
    [SerializeField] private string roomLabel;
    [SerializeField] private Material[] glyphMaterials;
    [SerializeField, Range(0, GlyphCount - 1)] private int initialIndex;
    [SerializeField, Range(0, GlyphCount - 1)] private int targetIndex;
    [SerializeField, Range(0, GlyphCount - 1)] private int stoneIndex;
    [SerializeField] private GameObject innerMark;
    [SerializeField] private Renderer selectionMark;
    [SerializeField] private GameObject placedStone;
    [SerializeField] private Transform dial;
    [SerializeField] private Vector3 dialAxis = Vector3.forward;
    [SerializeField] private TemplePassage socketLift;

    private int currentIndex;
    private int selectedIndex = -1;
    private bool selectionRejected;
    private Quaternion restingRotation;
    private float turnAngle;
    private float targetAngle;

    public bool HasStone { get; private set; }
    public int CurrentIndex => currentIndex;
    public int SelectedIndex => selectedIndex;
    public int TargetIndex => targetIndex;
    public int StoneIndex => stoneIndex;
    public override bool RequiresDirectAim => true;
    internal bool IsAligned => currentIndex == targetIndex;

    private bool PuzzleActive => isActiveAndEnabled && puzzle != null && puzzle.isActiveAndEnabled && !puzzle.IsSolved;
    private bool SocketReady => PuzzleActive && puzzle.AreDialsAligned && socketLift.isActiveAndEnabled &&
        socketLift.IsOpen && !socketLift.IsMoving;

    internal bool IsConfiguredFor(TempleRunePuzzle controller)
    {
        if (puzzle != controller || innerMark == null || dial == null ||
            dialAxis == Vector3.zero || socketLift == null ||
            selectionMark == null || placedStone == null || glyphMaterials == null ||
            glyphMaterials.Length != GlyphCount || !ValidGlyph(initialIndex) ||
            !ValidGlyph(targetIndex) || !ValidGlyph(stoneIndex))
        {
            return false;
        }

        foreach (var material in glyphMaterials)
        {
            if (material == null)
                return false;
        }

        return true;
    }

    private static bool ValidGlyph(int index) => index >= 0 && index < GlyphCount;

    private void Awake()
    {
        if (puzzle == null || !IsConfiguredFor(puzzle))
        {
            enabled = false;
            return;
        }

        currentIndex = initialIndex;
        innerMark.SetActive(false);
        placedStone.SetActive(false);
        SetSelection(-1);

        restingRotation = dial.localRotation;
    }

    private void Update()
    {
        if (turnAngle == targetAngle)
            return;

        turnAngle = Mathf.MoveTowards(turnAngle, targetAngle, TurnSpeed * Time.deltaTime);
        dial.localRotation = restingRotation * Quaternion.AngleAxis(turnAngle, dialAxis);
        if (turnAngle == targetAngle)
        {
            turnAngle %= 360f;
            targetAngle = turnAngle;
        }
    }

    public override bool CanInteract(PlayerInteractor player) => PuzzleActive && !puzzle.AreDialsAligned;

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        if (!CanInteract(player))
            return null;

        string turn = string.IsNullOrWhiteSpace(roomLabel) ? "Повернуть плиту" : $"Повернуть плиту · {roomLabel}";
        return WithButton(buttonPrompt, turn);
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        currentIndex = (currentIndex + 1) % GlyphCount;
        targetAngle -= StepAngle;
        puzzle.CheckDials();
    }

    internal void RevealInnerMark()
    {
        innerMark.SetActive(true);
        socketLift.SetOpen(true);
    }

    internal bool CanSelect(PlayerInventory inventory) => SocketReady && !HasStone && NextHeldRune(inventory) >= 0;

    internal string GetSelectionPrompt(PlayerInventory inventory, string buttonPrompt)
    {
        if (!SocketReady)
            return null;
        if (HasStone)
            return "Камень установлен";
        if (!CanSelect(inventory))
            return "Нужен камень из одной из комнат";

        return WithButton(buttonPrompt, selectedIndex < 0 ? "Выбрать камень" : "Выбрать следующий камень");
    }

    internal void SelectNext(PlayerInventory inventory)
    {
        if (!SocketReady || HasStone)
            return;

        int nextIndex = NextHeldRune(inventory);
        if (nextIndex >= 0)
            SetSelection(nextIndex);
    }

    internal bool CanPlace(PlayerInventory inventory)
    {
        return SocketReady && !HasStone &&
            inventory != null && selectedIndex >= 0 && inventory.HasItem(RuneIds[selectedIndex]);
    }

    internal string GetSocketPrompt(PlayerInventory inventory, string buttonPrompt)
    {
        if (!SocketReady)
            return null;
        if (HasStone)
            return "Камень установлен";
        if (!CanPlace(inventory))
            return "Выберите камень на соседней площадке";
        if (selectionRejected)
            return "Этот камень не подходит · выберите другой";

        return WithButton(buttonPrompt, "Положить выбранный камень");
    }

    internal void Place(PlayerInventory inventory)
    {
        if (!CanPlace(inventory))
            return;
        if (selectedIndex != stoneIndex)
        {
            selectionRejected = true;
            return;
        }
        if (!inventory.TryUse(RuneIds[selectedIndex]))
            return;

        HasStone = true;
        SetSelection(-1);
        placedStone.SetActive(true);
        puzzle.CheckStones(inventory);
    }

    internal void ClearUnavailableSelection(PlayerInventory inventory)
    {
        if (selectedIndex >= 0 && !inventory.HasItem(RuneIds[selectedIndex]))
            SetSelection(-1);
    }

    private int NextHeldRune(PlayerInventory inventory)
    {
        if (inventory == null)
            return -1;

        for (int step = 1; step <= GlyphCount; step++)
        {
            int index = (selectedIndex + step) % GlyphCount;
            if (inventory.HasItem(RuneIds[index]))
                return index;
        }

        return -1;
    }

    private void SetSelection(int index)
    {
        selectedIndex = index;
        selectionRejected = false;
        if (index >= 0)
            selectionMark.sharedMaterial = glyphMaterials[index];
        selectionMark.gameObject.SetActive(index >= 0);
    }

    private static string WithButton(string buttonPrompt, string action) =>
        string.IsNullOrEmpty(buttonPrompt) ? action : $"{buttonPrompt} · {action}";
}
