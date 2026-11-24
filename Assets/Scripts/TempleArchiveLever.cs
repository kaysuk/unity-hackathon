using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleArchiveLever : Interactable
{
    [SerializeField] private Transform handle;
    [SerializeField] private MeshRenderer indicator;
    private Material seal;
    private TempleLeverBank bank;
    private int index;

    public bool IsDown { get; private set; }

    internal void Initialize(TempleLeverBank owner, int number, bool lit, Shader sealShader)
    {
        bank = owner;
        index = number;
        seal = indicator.material;
        seal.shader = sealShader;
        ShowState(lit);
    }

    internal void ShowState(bool lit)
    {
        IsDown = !lit;
        handle.localRotation = Quaternion.Euler(IsDown ? 95f : 0f, 0f, 0f);
        seal.SetColor("_EmissionColor", lit ? new Color(2.8f, 1.2f, 0.28f) : Color.black);
    }

    private void OnDestroy()
    {
        if (seal != null) Destroy(seal);
    }

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        if (bank.IsSolved) return FormatBoundAction(buttonPrompt, "Прочитать расчёт печатей");
        string action = IsDown ? "Поднять" : "Опустить";
        return FormatBoundAction(buttonPrompt, $"Рычаг {index + 1} · {action}. Погасите все шесть огней.");
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player)) return;
        if (bank.IsSolved)
            TempleArchiveJournal.GetOrCreate(player.gameObject).ReadSealCalculation(bank.DescribeSolution());
        else
            bank.Pull(index);
    }
}
