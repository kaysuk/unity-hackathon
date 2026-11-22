using UnityEngine;

[DisallowMultipleComponent]
public sealed class ItemPickup : Interactable
{
    [SerializeField] private string itemId;
    [SerializeField] private string pickupHint = "Подобрать предмет";

    public string ItemId => itemId;
    public override bool RequiresDirectAim => true;

    public override bool CanInteract(PlayerInteractor player)
    {
        return base.CanInteract(player) && player != null && player.Inventory != null &&
            !string.IsNullOrWhiteSpace(itemId) && !player.Inventory.HasCollected(itemId);
    }

    protected override string ActionLabel => pickupHint ?? string.Empty;

    public override void Interact(PlayerInteractor player)
    {
        if (CanInteract(player) && player.Inventory.TryCollect(itemId))
            gameObject.SetActive(false);
    }
}
