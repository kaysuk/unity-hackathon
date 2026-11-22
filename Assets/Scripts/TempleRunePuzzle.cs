using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleRunePuzzle : MonoBehaviour
{
    public const int GlyphCount = 6;
    [SerializeField] private TempleRuneStation[] stations;
    [SerializeField] private TemplePassage passage;

    public bool AreDialsAligned { get; private set; }
    public bool IsSolved { get; private set; }

    private void Awake()
    {
        if (IsConfigured())
            return;

        Debug.LogError("Настройте шесть разных наружных знаков, шесть разных камней и проход архива.", this);
        enabled = false;
    }

    private void Start() => CheckDials();

    internal void CheckDials()
    {
        if (!isActiveAndEnabled || AreDialsAligned)
            return;

        foreach (var station in stations)
        {
            if (!station.IsAligned)
                return;
        }

        AreDialsAligned = true;
        foreach (var station in stations)
            station.RevealInnerMark();
    }

    internal void CheckStones(PlayerInventory inventory)
    {
        if (!isActiveAndEnabled || !AreDialsAligned || IsSolved)
            return;

        bool allStonesPlaced = true;
        foreach (var station in stations)
        {
            station.ClearUnavailableSelection(inventory);
            allStonesPlaced &= station.HasStone;
        }
        if (!allStonesPlaced)
            return;

        IsSolved = true;
        passage.SetOpen(true);
    }

    private bool IsConfigured()
    {
        if (passage == null || stations == null || stations.Length != GlyphCount)
            return false;

        const int allGlyphs = (1 << GlyphCount) - 1;
        int outerMarks = 0;
        int stoneMarks = 0;
        foreach (var station in stations)
        {
            if (station == null || !station.IsConfiguredFor(this))
                return false;

            outerMarks |= 1 << station.TargetIndex;
            stoneMarks |= 1 << station.StoneIndex;
        }

        return outerMarks == allGlyphs && stoneMarks == allGlyphs;
    }
}
