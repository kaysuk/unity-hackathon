using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class RitualAltar : Interactable, IDamageable
{
    private int remaining = 6;
    protected override string ActionLabel => "Ударить печать";
    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && GameObjectives.Capture().Stage == GameObjectives.Stage.DestroyAltars;

    public override void Interact(PlayerInteractor player)
    {
        if (CanInteract(player)) Strike(2, player != null ? player.gameObject : null);
    }

    public void TakeDamage(float amount, Vector3 point, Vector3 direction, GameObject source)
    {
        if (amount > 0f && GameObjectives.Capture().Stage == GameObjectives.Stage.DestroyAltars) Strike(3, source);
    }

    private void Strike(int amount, GameObject actor)
    {
        if (!gameObject.activeInHierarchy) return;
        remaining -= amount;
        GameAudio.Play(SoundCue.Seal, transform.position);
        if (remaining > 0) return;
        gameObject.SetActive(false);
        int destroyed = GameObjectives.Capture().AltarsDestroyed + 1;
        GameObjectives.SetAltarsDestroyed(destroyed);
        if (destroyed == 1)
            StoryGameplayVoice.FromPlayer(this, actor, "boss.first_altar", "Temple_Final_Room");
        else if (destroyed == 6)
            StoryGameplayVoice.FromPlayer(this, actor, "boss.last_altar", "Temple_Final_Room");
    }
}
