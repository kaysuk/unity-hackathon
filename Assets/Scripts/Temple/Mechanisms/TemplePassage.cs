using UnityEngine;

[DisallowMultipleComponent]
public sealed class TemplePassage : MonoBehaviour, IGraphPassage
{
    [SerializeField] private Vector3 openOffset = Vector3.up * 3f;
    [SerializeField, Min(0.01f)] private float speed = 3.6f;

    private enum MotionSound { Gate, StoneWall, StoneSocket, WoodenShutter }
    [SerializeField] private MotionSound motionSound;

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
        if (open != IsOpen && !immediate)
        {
            var cue = motionSound switch
            {
                MotionSound.StoneWall => SoundCue.StoneWall,
                MotionSound.StoneSocket => SoundCue.StoneLift,
                MotionSound.WoodenShutter => SoundCue.Slide,
                _ => open ? SoundCue.Gate : SoundCue.GateClose
            };
            GameAudio.Play(cue, transform.position, MovementVolume);
        }
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
        if (!IsMoving)
        {
            var stop = motionSound switch
            {
                MotionSound.StoneWall or MotionSound.StoneSocket => SoundCue.Stone,
                MotionSound.WoodenShutter => SoundCue.WoodImpact,
                _ => SoundCue.GateStop
            };
            GameAudio.Play(stop, transform.position, MovementVolume * 0.75f);
        }
        if (IsPassable) PassabilityChanged?.Invoke();
    }
}
