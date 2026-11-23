using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public sealed class TempleSealButton : Interactable
{
    [SerializeField] private TempleSealPanel.Mark mark;
    [SerializeField] private string markName;
    private TempleSealPanel panel;

    private void Awake() => panel = GetComponentInParent<TempleSealPanel>();

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && panel.AcceptsInput(player);

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        string status = panel.GetPrompt(player, buttonPrompt);
        return CanInteract(player) ? $"{buttonPrompt} · {markName} · {status}" : status;
    }

    public override void Interact(PlayerInteractor player) => panel.Press(player, mark);
}
