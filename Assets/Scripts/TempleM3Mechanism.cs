using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleM3Mechanism : MonoBehaviour
{
    [SerializeField] private TempleM3Insert leftInsert;
    [SerializeField] private TempleM3Insert rightInsert;
    [SerializeField] private TemplePassage shutter;

    public bool IsSolved { get; private set; }

    internal void CheckAlignment()
    {
        if (IsSolved || !leftInsert.IsAligned || !rightInsert.IsAligned)
            return;

        IsSolved = true;
        shutter.SetOpen(true);
    }
}
