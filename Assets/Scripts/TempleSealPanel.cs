using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleSealPanel : Interactable
{
    public enum Mark { Gate, Sun, Circle, Glyph, DottedCrescent, Crescent }

    private static readonly Mark[] Code =
        { Mark.Gate, Mark.Sun, Mark.Circle, Mark.Glyph, Mark.DottedCrescent, Mark.Crescent };
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    [SerializeField] private Renderer[] indicators;
    private MaterialPropertyBlock lamp;
    private bool wrongMark;

    public int Progress { get; private set; }
    public bool IsSolved => Progress == Code.Length;

    private void Awake()
    {
        lamp = new MaterialPropertyBlock();
        RefreshIndicators();
    }

    internal bool AcceptsInput(PlayerInteractor player) => isActiveAndEnabled && !IsSolved &&
        TempleSealDocument.CountCollected(player.Inventory) == Code.Length;

    // The stone backing displays the panel state. Only individual seals accept a press.
    public override bool CanInteract(PlayerInteractor player) => false;
    public override void Interact(PlayerInteractor player) { }

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        if (IsSolved)
        {
            if (player.TryGetComponent(out TempleArchiveJournal journal) && journal.FullFamilyTruth)
                return "Тайная запись: «Алексей Карамазов. Мать — Софья Карамазова».";
            return "Тайная запись · " + TempleArchiveText.SealedRegister;
        }
        int count = TempleSealDocument.CountCollected(player.Inventory);
        if (count < Code.Length) return $"Найдено документов {count}/{Code.Length}";
        return wrongMark ? "Неверная печать. Начните сначала" : $"Введено печатей {Progress}/{Code.Length}";
    }

    internal void Press(PlayerInteractor player, Mark mark)
    {
        if (!AcceptsInput(player)) return;
        wrongMark = mark != Code[Progress];
        Progress = wrongMark ? 0 : Progress + 1;
        GameAudio.Play(wrongMark ? SoundCue.SealFail : IsSolved ? SoundCue.Seal : SoundCue.Switch, transform.position);
        RefreshIndicators();
    }

    private void RefreshIndicators()
    {
        Color lit = IsSolved ? new Color(.28f, .9f, .48f) : new Color(1f, .62f, .16f);
        for (int i = 0; i < indicators.Length; i++)
        {
            lamp.SetColor(BaseColor, i < Progress ? lit : new Color(.09f, .07f, .055f));
            lamp.SetColor(Emission, i < Progress ? lit * 1.6f : Color.black);
            indicators[i].SetPropertyBlock(lamp);
        }
    }
}
