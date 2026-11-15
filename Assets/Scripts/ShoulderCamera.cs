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

    [Header("Aim framing")]
    [SerializeField] private Vector3 aimOffset = new Vector3(0.55f, 1.55f, -1.25f);
    [SerializeField, Range(20f, 90f)] private float aimFieldOfView = 45f;
    [SerializeField, Min(0.01f)] private float aimBlendTime = 0.15f;

    private Camera viewCamera;
    private IvanMovement playerMovement;
    private Transform playerRoot;
    private float normalFieldOfView;
    private float aimWeight;
    private float aimBlendVelocity;
    private float yaw;
    private float pitch;
    public float Yaw => yaw;
    public float YawDelta { get; private set; }

    private void Start()
    {
        viewCamera = GetComponent<Camera>();
        normalFieldOfView = viewCamera.fieldOfView;
        playerMovement = target != null ? target.GetComponentInParent<IvanMovement>() : null;
        playerRoot = playerMovement != null ? playerMovement.transform : target;
        yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
        pitch = downwardAngle;
    }

    private void Update()
    {
        YawDelta = 0f;
        if (!Application.isFocused || Time.timeScale <= 0f || Cursor.lockState != CursorLockMode.Locked || Mouse.current == null)
            return;
        Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity;
        YawDelta = delta.x;
        yaw += delta.x;
        pitch = Mathf.Clamp(pitch - delta.y, pitchLimits.x, pitchLimits.y);
    }

    private void LateUpdate()
    {
        if (target == null) return;
        UpdateAimFraming();
        Vector3 currentOffset = Vector3.Lerp(offset, aimOffset, aimWeight);
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + Vector3.up * currentOffset.y;
        Vector3 displacement = rotation * new Vector3(currentOffset.x, 0f, currentOffset.z);
        float distance = displacement.magnitude;
        if (distance < 0.001f) return;
        Vector3 direction = displacement / distance;
        // Exclude the player's colliders while keeping scene geometry on its existing layers.
        foreach (var hit in Physics.SphereCastAll(pivot, collisionRadius, direction, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(playerRoot))
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.05f));
        }
        transform.SetPositionAndRotation(pivot + direction * distance, rotation);
    }

    private void UpdateAimFraming()
    {
        // LateUpdate reads the same aim state used by movement earlier this frame.
        bool aiming = playerMovement != null && playerMovement.isActiveAndEnabled &&
            playerMovement.IsAiming && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        // SmoothDamp must not advance with a zero time step while paused.
        if (Time.deltaTime <= 0f) return;
        aimWeight = Mathf.SmoothDamp(aimWeight, aiming ? 1f : 0f,
            ref aimBlendVelocity, aimBlendTime, Mathf.Infinity, Time.deltaTime);
        viewCamera.fieldOfView = Mathf.Lerp(normalFieldOfView, aimFieldOfView, aimWeight);
    }

    private void OnDisable()
    {
        if (viewCamera != null) viewCamera.fieldOfView = normalFieldOfView;
        aimWeight = 0f;
        aimBlendVelocity = 0f;
    }

}
