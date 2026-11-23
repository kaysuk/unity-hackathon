using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleArchiveLever : Interactable
{
    [SerializeField] private Transform handle;
    [SerializeField] private bool requiredDown;
    private TempleLeverBank bank;

    public bool IsDown { get; private set; }
    public bool Matches => IsDown == requiredDown;

    private void Awake() => bank = GetComponentInParent<TempleLeverBank>();

    public override bool CanInteract(PlayerInteractor player) => base.CanInteract(player) && !bank.IsSolved;

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        if (bank.IsSolved) return "Проход в комнату 7.2 открыт";
        string action = IsDown ? "Поднять рычаг" : "Опустить рычаг";
        return $"{buttonPrompt} · {action} · {bank.Clue}";
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player)) return;
        IsDown = !IsDown;
        handle.localRotation = Quaternion.Euler(IsDown ? 95f : 0f, 0f, 0f);
        bank.CheckCombination();
    }
}
