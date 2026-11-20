using UnityEngine;

[DisallowMultipleComponent]
public sealed class TemplePassage : MonoBehaviour
{
    [SerializeField] private Vector3 openOffset = Vector3.up * 3f;
    [SerializeField, Min(0.01f)] private float speed = 3.6f;

    private Vector3 closedPosition;
    private Vector3 targetPosition;

    public bool IsOpen { get; private set; }
    public bool IsMoving => transform.localPosition != targetPosition;

    private void Awake()
    {
        closedPosition = transform.localPosition;
        targetPosition = closedPosition;
    }

    public void SetOpen(bool open, bool immediate = false)
    {
        IsOpen = open;
        targetPosition = open ? closedPosition + openOffset : closedPosition;
        if (immediate)
            transform.localPosition = targetPosition;
    }

    private void Update()
    {
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPosition, speed * Time.deltaTime);
    }
}
