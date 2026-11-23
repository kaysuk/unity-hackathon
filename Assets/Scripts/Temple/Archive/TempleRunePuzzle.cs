using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleRunePuzzle : MonoBehaviour
{
    public const int GlyphCount = 6;
    [SerializeField] private TempleRuneStation[] stations;
    [SerializeField] private TemplePassage passage;
    internal static readonly string[] RuneIds =
    {
        "rune_room_01", "rune_room_02", "rune_room_03",
        "rune_room_04", "rune_room_05", "rune_room_06"
    };

    private enum Stage { Dials, Stones, Open }
    private Stage stage;
    private MeshRenderer[] runeModels;

    internal MeshRenderer[] RuneModels => runeModels ??= FindRuneModels();

    public bool AreDialsAligned => stage != Stage.Dials;
    public bool IsSolved => stage == Stage.Open;

    private void Awake()
    {
        if (IsConfigured())
            return;

        Debug.LogError("Проверьте шесть плит и решётку в комнате 7.2.", this);
        enabled = false;
    }

    private MeshRenderer[] FindRuneModels()
    {
        // Collected pickups are inactive, but remain the source of the stone's appearance.
        var models = new MeshRenderer[GlyphCount];
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var pickup in root.GetComponentsInChildren<ItemPickup>(true))
            {
                int index = System.Array.IndexOf(RuneIds, pickup.ItemId);
                if (index < 0 || !pickup.TryGetComponent(out MeshRenderer model) ||
                    !pickup.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                    continue;

                var surfaces = model.sharedMaterials;
                if (surfaces.Length == 2 && surfaces[0] != null && surfaces[0].mainTexture != null && surfaces[1] != null)
                    models[index] = model;
            }
        }

        for (int i = 0; i < models.Length; i++)
        {
            if (models[i] == null)
                Debug.LogError($"У предмета {RuneIds[i]} отсутствует модель камня с лицом и оправой.", this);
        }
        return models;
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

        stage = Stage.Stones;
        foreach (var station in stations)
            station.RaiseSocket();
    }

    internal void CheckStones()
    {
        if (!isActiveAndEnabled || !AreDialsAligned || IsSolved)
            return;

        foreach (var station in stations)
        {
            if (!station.HasStone || station.OccupantIndex != station.StoneIndex)
                return;
        }

        stage = Stage.Open;
        GameAudio.Play(SoundCue.Seal, passage.transform.position);
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
