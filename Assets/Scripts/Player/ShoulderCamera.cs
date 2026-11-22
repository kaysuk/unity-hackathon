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
    private Ray frameAimRay;
    private int framingFrame = -1;
    private float yaw;
    private float pitch;
    public float Yaw => yaw;
    public float YawDelta { get; private set; }

    public void AddRecoil(float degrees)
    {
        if (!isActiveAndEnabled) return;
        pitch = Mathf.Clamp(pitch - Mathf.Max(0f, degrees), pitchLimits.x, pitchLimits.y);
        // Keep this frame's evaluated camera and muzzle consistent; apply recoil next frame.
    }

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
        Ray aimRay = GetAimRay();
        transform.SetPositionAndRotation(aimRay.origin, Quaternion.Euler(pitch, yaw, 0f));
    }

    // Also used before animation evaluation, so rig targets use this frame's camera angles.
    public Ray GetAimRay()
    {
        if (!isActiveAndEnabled || viewCamera == null || target == null)
            return new Ray(transform.position, transform.forward);
        if (framingFrame == Time.frameCount) return frameAimRay;
        UpdateAimFraming();
        framingFrame = Time.frameCount;

        Vector3 currentOffset = Vector3.Lerp(offset, aimOffset, aimWeight);
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + Vector3.up * currentOffset.y;
        Vector3 displacement = rotation * new Vector3(currentOffset.x, 0f, currentOffset.z);
        float distance = displacement.magnitude;
        Vector3 direction = distance > 0.001f ? displacement / distance : Vector3.zero;
        if (distance > 0.001f)
        {
            foreach (var hit in Physics.SphereCastAll(pivot, collisionRadius, direction, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(playerRoot))
                    distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.05f));
            }
        }
        frameAimRay = new Ray(pivot + direction * distance, rotation * Vector3.forward);
        return frameAimRay;
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
        framingFrame = -1;
    }

}
