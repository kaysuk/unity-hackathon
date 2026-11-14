using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class ShoulderCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0.75f, 1.65f, -2.8f);
    [SerializeField, Range(0f, 45f)] private float downwardAngle = 10f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-25f, 60f);
    [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;

    private float yaw;
    private float pitch;
    public float Yaw => yaw;
    public float YawDelta { get; private set; }

    private void Start()
    {
        yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
        pitch = downwardAngle;
    }

    private void Update()
    {
        YawDelta = 0f;
        if (Time.timeScale <= 0f || Cursor.lockState != CursorLockMode.Locked || Mouse.current == null)
            return;
        Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity;
        YawDelta = delta.x;
        yaw += delta.x;
        pitch = Mathf.Clamp(pitch - delta.y, pitchLimits.x, pitchLimits.y);
    }

    private void LateUpdate()
    {
        if (target == null) return;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + Vector3.up * offset.y;
        Vector3 displacement = rotation * new Vector3(offset.x, 0f, offset.z);
        float distance = displacement.magnitude;
        if (distance < 0.001f) return;
        Vector3 direction = displacement / distance;
        // Exclude the player's colliders while keeping scene geometry on its existing layers.
        foreach (var hit in Physics.SphereCastAll(pivot, collisionRadius, direction, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(target))
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.05f));
        }
        transform.SetPositionAndRotation(pivot + direction * distance, rotation);
    }
}
