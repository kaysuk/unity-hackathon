using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleKeyLock : Interactable
{
    [SerializeField] private string keyId;
    [SerializeField] private TemplePassage shutter;

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && !shutter.IsOpen && player != null &&
        player.Inventory != null && player.Inventory.HasItem(keyId);

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        if (shutter.IsOpen)
            return null;
        return CanInteract(player) ? FormatBoundAction(buttonPrompt, "Открыть М3 ключом") : "Для М3 нужен ключ";
    }

    public override void Interact(PlayerInteractor player)
    {
        if (CanInteract(player) && player.Inventory.TryUse(keyId))
            shutter.SetOpen(true);
    }
}
