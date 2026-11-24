using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleRiddleReminder : Interactable
{
    [SerializeField] private TempleRunePuzzle puzzle;

    protected override string ActionLabel => "Повторить загадку";

    public override bool CanInteract(PlayerInteractor player) =>
        isActiveAndEnabled && puzzle != null && puzzle.CanHearRiddle(player);

    public override void Interact(PlayerInteractor player)
    {
        if (CanInteract(player))
            StorySpeech.SayPrompt("temple.rune.remind", once: false);
    }
}
