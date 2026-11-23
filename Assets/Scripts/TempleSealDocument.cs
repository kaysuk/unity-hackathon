using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public sealed class TempleSealDocument : Interactable
{
    private static readonly string[] DocumentIds =
    {
        "seal_document_01", "seal_document_02", "seal_document_03",
        "seal_document_04", "seal_document_05", "seal_document_06"
    };

    [SerializeField, Range(1, 6)] private int number = 1;
    [SerializeField] private string readPrompt;
    [SerializeField, TextArea] private string clue;

    private string DocumentId => DocumentIds[number - 1];

    public static int CountCollected(PlayerInventory inventory)
    {
        int count = 0;
        foreach (string id in DocumentIds)
            if (inventory.HasCollected(id)) count++;
        return count;
    }

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && player.Inventory != null && !player.Inventory.HasCollected(DocumentId);

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        var inventory = player.Inventory;
        if (inventory == null) return null;
        if (inventory.HasCollected(DocumentId))
            return $"Документы {CountCollected(inventory)}/{DocumentIds.Length} · {clue}";
        return string.IsNullOrEmpty(buttonPrompt) ? null : $"{buttonPrompt} · {readPrompt}";
    }

    public override void Interact(PlayerInteractor player)
    {
        if (CanInteract(player)) player.Inventory.TryCollect(DocumentId);
    }
}
