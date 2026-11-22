using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleArchiveDocument : Interactable
{
    [SerializeField, Range(1, 6)] private int documentNumber = 1;

    public int DocumentNumber => documentNumber;
    public override bool RequiresDirectAim => true;

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && player != null && documentNumber >= 1 && documentNumber <= 6;

    protected override string ActionLabel => "Прочитать документ";

    public override void Interact(PlayerInteractor player)
    {
        if (CanInteract(player))
            TempleArchiveJournal.GetOrCreate(player.gameObject).ReadDocument(documentNumber);
    }
}
