using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public sealed class TempleSealDocument : Interactable
{
    [SerializeField, Range(1, 6)] private int number = 1;

    internal static readonly (string Id, string Clue)[] Notes =
    {
        ("seal_document_01", "Знаки на полу читают по порядку комнат."),
        ("seal_document_02", "Вторую печать ставят в честь восходящего солнца."),
        ("seal_document_03", "Третий знак высечен одной замкнутой чертой."),
        ("seal_document_04", "Четвёртый знак переписчик принял за букву чужого письма."),
        ("seal_document_05", "У пятой луны есть спутник — точка в изгибе серпа."),
        ("seal_document_06", "Шестая луна одинока."),
    };

    private string DocumentId => Notes[number - 1].Id;

    internal static bool HasCollectedPage(PlayerInventory inventory, int page) =>
        inventory.HasCollected(Notes[page - 1].Id);

    public static int CountCollected(PlayerInventory inventory)
    {
        int count = 0;
        foreach (var note in Notes)
            if (inventory.HasCollected(note.Id)) count++;
        return count;
    }

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && player.Inventory != null && !player.Inventory.HasCollected(DocumentId);

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        var inventory = player.Inventory;
        if (inventory == null) return null;
        if (inventory.HasCollected(DocumentId))
        {
            int found = player.TryGetComponent(out TempleArchiveJournal journal) ? journal.DocumentCount : CountCollected(inventory);
            return $"Документы {found}/{TempleArchiveJournal.DocumentTotal} · {Notes[number - 1].Clue}";
        }
        return string.IsNullOrEmpty(buttonPrompt) ? null : $"{buttonPrompt} · Прочитать «{TempleArchiveText.Titles[number - 1]}»";
    }

    public override void Interact(PlayerInteractor player)
    {
        if (CanInteract(player) && player.Inventory.TryCollect(DocumentId))
        {
            TempleArchiveJournal.GetOrCreate(player.gameObject).CollectDocument(number);
            if (gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
                GameAudio.Interface(SoundCue.Paper);
        }
    }
}
