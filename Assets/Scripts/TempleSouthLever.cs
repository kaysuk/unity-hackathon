using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleSouthLever : Interactable
{
    [SerializeField] private TemplePassage room5Gate;
    [SerializeField] private TemplePassage room4Gate;
    [SerializeField] private Transform leverPivot;

    public bool OpensRoom4 { get; private set; }

    private bool IsConfigured => room5Gate != null && room4Gate != null && leverPivot != null;

    private Quaternion LeverRotation => Quaternion.Euler(0f, 0f, OpensRoom4 ? 34f : -34f);

    private void Start()
    {
        if (!IsConfigured)
            return;

        UpdateGates(immediate: true);
        leverPivot.localRotation = LeverRotation;
    }

    private void Update()
    {
        if (leverPivot != null)
            leverPivot.localRotation = Quaternion.RotateTowards(
                leverPivot.localRotation, LeverRotation, 270f * Time.deltaTime);
    }

    public override bool CanInteract(PlayerInteractor player) => base.CanInteract(player) && IsConfigured;

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        if (!CanInteract(player) || string.IsNullOrEmpty(buttonPrompt))
            return null;

        string direction = OpensRoom4 ? "Б → А" : "А → Б";
        return $"{buttonPrompt} · Перевести рычаг ({direction})";
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        OpensRoom4 = !OpensRoom4;
        UpdateGates(immediate: false);
    }

    private void UpdateGates(bool immediate)
    {
        room5Gate.SetOpen(!OpensRoom4, immediate);
        room4Gate.SetOpen(OpensRoom4, immediate);
    }
}
