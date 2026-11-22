using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleSecretButton : Interactable
{
    [SerializeField] private TemplePassage passage;
    [SerializeField] private Transform plunger;

    public override bool RequiresDirectAim => true;

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && passage != null && !passage.IsOpen;

    protected override string ActionLabel => "Нажать кнопку";

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        if (plunger != null)
            plunger.localPosition += Vector3.back * 0.022f;

        passage.SetOpen(true);
    }
}
