using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleRuneSocket : Interactable
{
    [SerializeField] private TempleRuneStation station;
    [SerializeField] private bool selectsStone;

    public override bool RequiresDirectAim => true;
    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && station != null &&
        (selectsStone ? station.CanSelect(player?.Inventory) : station.CanPlace(player?.Inventory));

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        if (!isActiveAndEnabled || station == null)
            return null;

        return selectsStone
            ? station.GetSelectionPrompt(player?.Inventory, buttonPrompt)
            : station.GetSocketPrompt(player?.Inventory, buttonPrompt);
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!isActiveAndEnabled || station == null)
            return;

        if (selectsStone)
            station.SelectNext(player?.Inventory);
        else
            station.Place(player?.Inventory);
    }
}
