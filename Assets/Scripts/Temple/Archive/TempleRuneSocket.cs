using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleRuneSocket : Interactable
{
    [SerializeField] private TempleRuneStation station;

    public override bool RequiresDirectAim => true;
    internal override Transform HighlightRoot => station != null ? station.SocketHighlightRoot : null;
    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && station != null && station.CanUseSocket(player?.Inventory);

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt) =>
        isActiveAndEnabled && station != null ? station.GetSocketPrompt(player?.Inventory, buttonPrompt) : null;

    public override void Interact(PlayerInteractor player)
    {
        if (isActiveAndEnabled && station != null)
            station.UseSocket(player);
    }
}
