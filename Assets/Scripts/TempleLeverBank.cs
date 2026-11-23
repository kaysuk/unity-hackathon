using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleLeverBank : MonoBehaviour
{
    [SerializeField] private TemplePassage passage;
    [SerializeField, TextArea] private string clue;
    private TempleArchiveLever[] levers;

    public bool IsSolved => passage.IsOpen;
    public string Clue => clue;

    private void Awake() => levers = GetComponentsInChildren<TempleArchiveLever>();

    internal void CheckCombination()
    {
        foreach (var lever in levers)
            if (!lever.Matches) return;

        passage.SetOpen(true);
    }
}
