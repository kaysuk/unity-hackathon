using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleSlidingPuzzle : MonoBehaviour
{
    public const int Width = 4;
    [SerializeField] private TemplePassage northGate;
    [SerializeField] private TemplePassage documentShutter;

    private readonly TempleSlidingTile[] slots = new TempleSlidingTile[Width * Width];
    private int emptySlot;

    public bool IsSolved { get; private set; }

    private void Awake()
    {
        foreach (TempleSlidingTile tile in GetComponentsInChildren<TempleSlidingTile>())
            slots[tile.Slot] = tile;

        emptySlot = System.Array.IndexOf(slots, null);
    }

    public bool CanSlide(TempleSlidingTile tile)
    {
        if (IsSolved || tile == null || slots[tile.Slot] != tile)
            return false;

        int dx = Mathf.Abs(tile.Slot % Width - emptySlot % Width);
        int dy = Mathf.Abs(tile.Slot / Width - emptySlot / Width);
        return dx + dy == 1;
    }

    public bool TrySlide(TempleSlidingTile tile)
    {
        if (!CanSlide(tile))
            return false;

        int oldSlot = tile.Slot;
        slots[oldSlot] = null;
        slots[emptySlot] = tile;
        GameAudio.Play(SoundCue.Slide, tile.transform.position);
        tile.MoveTo(emptySlot);
        emptySlot = oldSlot;

        for (int slot = 0; slot < slots.Length - 1; slot++)
            if (slots[slot] == null || slots[slot].Number != slot + 1)
                return true;

        GameAudio.Play(SoundCue.Seal, tile.transform.position);
        IsSolved = true;
        northGate.SetOpen(true);
        if (documentShutter != null) documentShutter.SetOpen(true);
        return true;
    }
}
