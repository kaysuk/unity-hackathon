using UnityEngine;

[DisallowMultipleComponent]
public sealed class ItemPickup : Interactable
{
    [SerializeField] private string itemId;
    [SerializeField] private string pickupHint = "Подобрать предмет";

    public override bool CanInteract(PlayerInteractor player)
    {
        return base.CanInteract(player) && player.Inventory != null &&
            !string.IsNullOrWhiteSpace(itemId) && !player.Inventory.HasCollected(itemId);
    }

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        return CanInteract(player) && !string.IsNullOrEmpty(buttonPrompt)
            ? $"{buttonPrompt} · {pickupHint}"
            : null;
    }

    public override void Interact(PlayerInteractor player)
    {
        if (CanInteract(player) && player.Inventory.TryCollect(itemId))
            gameObject.SetActive(false);
    }
}
