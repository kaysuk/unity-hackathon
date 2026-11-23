using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleMewLever : Interactable
{
    [SerializeField] private Transform leverPivot;
    [SerializeField] private TemplePassage[] doors;

    private Quaternion restingRotation;
    private Quaternion pulledRotation;
    private bool isSwitchingDoors;

    public bool IsPulled { get; private set; }

    private void Awake()
    {
        restingRotation = leverPivot.localRotation;
        pulledRotation = restingRotation * Quaternion.Euler(0f, 0f, -78f);
    }

    private void Start() => doors[1].SetOpen(true, true);

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && !isSwitchingDoors;

    protected override string ActionLabel => "Потянуть рычаг";

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        IsPulled = !IsPulled;
        GameAudio.Play(SoundCue.Lever, leverPivot.position);
        StartCoroutine(IsPulled ? SwitchDoors(doors[1], doors[0]) : SwitchDoors(doors[0], doors[1]));
    }

    private IEnumerator SwitchDoors(TemplePassage closingDoor, TemplePassage openingDoor)
    {
        isSwitchingDoors = true;
        closingDoor.SetOpen(false);
        while (closingDoor.IsMoving)
            yield return null;

        openingDoor.SetOpen(true);
        while (openingDoor.IsMoving)
            yield return null;

        isSwitchingDoors = false;
    }

    private void Update()
    {
        Quaternion targetRotation = IsPulled ? pulledRotation : restingRotation;
        leverPivot.localRotation = Quaternion.RotateTowards(
            leverPivot.localRotation, targetRotation, 240f * Time.deltaTime);
    }
}
