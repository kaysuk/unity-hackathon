using UnityEngine;

[DisallowMultipleComponent]
public sealed class M0GateMechanism : Interactable
{
    [SerializeField] private string leftHandleId;
    [SerializeField] private string rightHandleId;
    [SerializeField] private Transform leftHandle;
    [SerializeField] private Transform rightHandle;
    [SerializeField] private Transform gate;
    [SerializeField] private Vector3 raisedLocalPosition;
    [SerializeField, Min(0.01f)] private float liftSpeed = 2.6f;

    private Stage stage;

    public bool IsOpen => stage == Stage.Open;

    private enum Stage
    {
        WaitingForHandles,
        HandlesInserted,
        LeftHandleTurned,
        Raising,
        Open
    }

    private void Awake()
    {
        if (leftHandle == null || rightHandle == null || gate == null ||
            string.IsNullOrWhiteSpace(leftHandleId) || string.IsNullOrWhiteSpace(rightHandleId) ||
            leftHandleId == rightHandleId)
        {
            Debug.LogError("М0 требует две разные рукояти, два гнезда и решётку.", this);
            enabled = false;
        }
    }

    public override bool CanInteract(PlayerInteractor player)
    {
        if (!base.CanInteract(player))
            return false;

        return stage switch
        {
            Stage.WaitingForHandles => CountHandles(player.Inventory) == 2,
            Stage.HandlesInserted or Stage.LeftHandleTurned => true,
            _ => false
        };
    }

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        if (!isActiveAndEnabled)
            return null;

        if (stage == Stage.WaitingForHandles)
        {
            int count = CountHandles(player.Inventory);
            if (count == 0)
                return "Две рукояти. Одной не открыть. (0/2)";
            if (count == 1)
                return "Две рукояти. Одной не открыть. (1/2)";
        }

        if (string.IsNullOrEmpty(buttonPrompt))
            return null;

        return stage switch
        {
            Stage.WaitingForHandles => $"{buttonPrompt} · Вставить обе рукояти",
            Stage.HandlesInserted => $"{buttonPrompt} · Повернуть левую рукоять",
            Stage.LeftHandleTurned => $"{buttonPrompt} · Повернуть правую рукоять",
            _ => null
        };
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        switch (stage)
        {
            case Stage.WaitingForHandles:
                if (!player.Inventory.TryUsePair(leftHandleId, rightHandleId))
                    return;

                leftHandle.gameObject.SetActive(true);
                rightHandle.gameObject.SetActive(true);
                stage = Stage.HandlesInserted;
                break;

            case Stage.HandlesInserted:
                leftHandle.Rotate(0f, 0f, 90f, Space.Self);
                stage = Stage.LeftHandleTurned;
                break;

            case Stage.LeftHandleTurned:
                rightHandle.Rotate(0f, 0f, 90f, Space.Self);
                stage = Stage.Raising;
                break;
        }
    }

    private int CountHandles(PlayerInventory inventory)
    {
        if (inventory == null)
            return 0;

        int left = inventory.HasItem(leftHandleId) ? 1 : 0;
        int right = inventory.HasItem(rightHandleId) ? 1 : 0;
        return left + right;
    }

    private void Update()
    {
        if (stage != Stage.Raising)
            return;

        gate.localPosition = Vector3.MoveTowards(gate.localPosition, raisedLocalPosition, liftSpeed * Time.deltaTime);
        if (gate.localPosition == raisedLocalPosition)
            stage = Stage.Open;
    }
}
