using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleArchiveDocument : Interactable
{
    [SerializeField, Range(1, 6)] private int documentNumber = 1;

    public int DocumentNumber => documentNumber;
    public override bool RequiresDirectAim => true;

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && player != null && documentNumber >= 1 && documentNumber <= 6;

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt) =>
        CanInteract(player) && !string.IsNullOrEmpty(buttonPrompt)
            ? $"{buttonPrompt} · Прочитать документ"
            : null;

    public override void Interact(PlayerInteractor player)
    {
        if (CanInteract(player))
            TempleArchiveJournal.GetOrCreate(player.gameObject).ReadDocument(documentNumber);
    }
}
