using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class TempleSupplyCrate : Interactable
{
    [SerializeField] private Transform platform;
    [SerializeField] private TemplePassage documentNiche;
    [SerializeField] private TemplePassage northGate;
    [SerializeField, Min(0.01f)] private float slideSpeed = 1.25f;

    private Collider crateCollider;
    private CharacterController pusher;
    private bool moving;

    public bool IsOnPlatform { get; private set; }

    private void Awake() => crateCollider = GetComponent<Collider>();

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && !moving && !IsOnPlatform;

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt) =>
        CanInteract(player) && !string.IsNullOrEmpty(buttonPrompt)
            ? $"{buttonPrompt} · Сдвинуть ящик на площадку"
            : null;

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player))
            return;
        pusher = player.Controller;
        moving = true;
    }

    private void Update()
    {
        if (!moving)
            return;

        Vector3 next = Vector3.MoveTowards(transform.position, platform.position, slideSpeed * Time.deltaTime);
        Bounds nextBounds = crateCollider.bounds;
        nextBounds.center += next - transform.position;
        if (pusher != null && nextBounds.Intersects(pusher.bounds))
            return;

        transform.position = next;
        if (next != platform.position)
            return;

        moving = false;
        IsOnPlatform = true;
        documentNiche.SetOpen(true);
        northGate.SetOpen(true);
    }
}
