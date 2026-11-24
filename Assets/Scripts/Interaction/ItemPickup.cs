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
        if (!CanInteract(player)) return;
        bool bundle = itemId == TempleArchiveJournal.BundleItemId;
        if (bundle && !TempleArchiveJournal.GetOrCreate(player.gameObject).ReadBundle()) return;
        if (!player.Inventory.TryCollect(itemId)) return;

        if (!bundle)
        {
            bool rune = itemId.StartsWith("rune_");
            GameAudio.Play(rune ? SoundCue.Stone : SoundCue.Key, transform.position);
            if (rune)
                StoryGameplayVoice.FromPlayer(this, player.gameObject, "temple.rune.found", "temple_v2");
        }
        gameObject.SetActive(false);
    }
}
