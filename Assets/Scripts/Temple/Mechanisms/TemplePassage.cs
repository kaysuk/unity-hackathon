using UnityEngine;

[DisallowMultipleComponent]
public sealed class TemplePassage : MonoBehaviour, IGraphPassage
{
    [SerializeField] private Vector3 openOffset = Vector3.up * 3f;
    [SerializeField, Min(0.01f)] private float speed = 3.6f;

    private Vector3 closedPosition;
    private Vector3 targetPosition;

    public bool IsOpen { get; private set; }
    public bool IsMoving => transform.localPosition != targetPosition;
    internal float MovementVolume { get; set; } = 0.55f;

    public event System.Action PassabilityChanged;
    public bool IsPassable => IsOpen && !IsMoving;

    private void Awake()
    {
        closedPosition = transform.localPosition;
        targetPosition = closedPosition;
    }

    public void SetOpen(bool open, bool immediate = false)
    {
        bool wasPassable = IsPassable;
        if (open != IsOpen && !immediate) GameAudio.Play(SoundCue.Gate, transform.position, MovementVolume);
        IsOpen = open;
        targetPosition = open ? closedPosition + openOffset : closedPosition;
        if (immediate)
            transform.localPosition = targetPosition;
        if (wasPassable != IsPassable) PassabilityChanged?.Invoke();
    }

    private void Update()
    {
        if (!IsMoving) return;
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPosition, speed * Time.deltaTime);
        if (!IsMoving) GameAudio.Play(SoundCue.GateStop, transform.position, MovementVolume);
        if (IsPassable) PassabilityChanged?.Invoke();
    }
}
