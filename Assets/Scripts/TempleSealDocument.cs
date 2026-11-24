using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public sealed class TempleSealDocument : Interactable
{
    internal const int RoomNoteCount = 6;
    [SerializeField, Range(1, RoomNoteCount)] private int number = 1;

    private static readonly string[] ItemIds = {
        "seal_document_01", "seal_document_02", "seal_document_03",
        "seal_document_04", "seal_document_05", "seal_document_06"
    };

    internal static bool HasCollectedPage(PlayerInventory inventory, int page) =>
        inventory.HasCollected(ItemIds[page - 1]);

    public static int CountCollected(PlayerInventory inventory)
    {
        int count = 0;
        foreach (string id in ItemIds)
            if (inventory.HasCollected(id)) count++;
        return count;
    }

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && player.Inventory != null && !HasCollectedPage(player.Inventory, number);

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        if (player.Inventory == null) return null;
        if (HasCollectedPage(player.Inventory, number))
            return TempleArchiveText.Titles[number - 1];
        return string.IsNullOrEmpty(buttonPrompt) ? null : FormatBoundAction(buttonPrompt, $"Прочитать «{TempleArchiveText.Titles[number - 1]}»");
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player)) return;
        if (TempleArchiveJournal.GetOrCreate(player.gameObject).ReadDocument(number))
            player.Inventory.TryCollect(ItemIds[number - 1]);
    }
}
