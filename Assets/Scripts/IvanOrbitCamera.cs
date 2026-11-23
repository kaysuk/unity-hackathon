using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-30)]
[RequireComponent(typeof(Camera))]
public sealed class IvanOrbitCamera : MonoBehaviour
{
    private const float MinimumPitch = -25f;
    private const float MaximumPitch = 55f;

    [SerializeField] private Transform target;
    [SerializeField] private float sensitivity = 0.14f;
    [SerializeField, Min(0f)] private float stickDegreesPerSecond = 180f;

    [System.Serializable]
    private struct Framing
    {
        [Min(0.2f)] public float distance;
        [Min(0f)] public float shoulderOffset;
        public float pivotHeight;
        [Range(20f, 90f)] public float fieldOfView;

        public Framing(float distance, float shoulderOffset, float pivotHeight, float fieldOfView)
        {
            this.distance = distance;
            this.shoulderOffset = shoulderOffset;
            this.pivotHeight = pivotHeight;
            this.fieldOfView = fieldOfView;
        }
    }

    [SerializeField] private Framing orbit = new Framing(1.8f, 0.49f, 1.25f, 52f);
    [SerializeField] private Framing aim = new Framing(0.63f, 0.34f, 1.64f, 40f);
    [SerializeField, Min(0.01f)] private float blendSeconds = 0.22f;

    private IvanStealth stealth;
    private IvanSidearm sidearm;
    private Camera view;
    private float baseFieldOfView;
    private float zoom;
    private InputAction look;
    private bool mouseCaptured;
    private float yaw;
    private float pitch = 6f;

    internal void AddRecoil(float degrees)
    {
        if (isActiveAndEnabled)
            pitch = Mathf.Clamp(pitch - Mathf.Max(0f, degrees), MinimumPitch, MaximumPitch);
    }

    private void Start()
    {
        view = GetComponent<Camera>();
        baseFieldOfView = view.fieldOfView;
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            sidearm = target.GetComponent<IvanSidearm>();
            stealth = target.GetComponent<IvanStealth>();
        }
        look = InputSystem.actions?.FindAction("Player/Look");
    }

    private void Update()
    {
        if (target == null) return;
        var mouse = Mouse.current;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            ReleaseMouse();
        else if (!mouseCaptured && mouse != null &&
                 (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
        {
            Cursor.lockState = CursorLockMode.Locked;
            mouseCaptured = true;
        }

        Vector2 delta = look?.ReadValue<Vector2>() ?? Vector2.zero;
        if (look?.activeControl?.device is Pointer)
            delta = mouseCaptured || look.activeControl.device is Touchscreen ? delta * sensitivity : Vector2.zero;
        else
            delta *= stickDegreesPerSecond * Time.deltaTime;

        yaw = Mathf.Repeat(yaw + delta.x, 360f);
        pitch = Mathf.Clamp(pitch - delta.y, MinimumPitch, MaximumPitch);

        // Locomotion and the hand rig read this orientation later in the same frame.
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void LateUpdate()
    {
        if (target == null) return;
        zoom = Mathf.MoveTowards(zoom, sidearm != null && sidearm.IsAiming ? 1f : 0f,
            Time.deltaTime / blendSeconds);
        float blend = Mathf.SmoothStep(0f, 1f, zoom);
        view.fieldOfView = Mathf.Lerp(orbit.fieldOfView, aim.fieldOfView, blend);

        Vector3 pivot = target.position + Vector3.up * (Mathf.Lerp(orbit.pivotHeight, aim.pivotHeight, blend) -
            (stealth != null ? stealth.CameraDrop : 0f));
        // Reduce lateral displacement in narrow windows to keep the shoulder in frame.
        float aspectScale = Mathf.Min(1f, view.aspect / (16f / 9f));
        float offset = Mathf.Lerp(orbit.shoulderOffset, aim.shoulderOffset * aspectScale, blend);
        float reach = Mathf.Lerp(orbit.distance, aim.distance, blend);
        Vector3 right = transform.right;
        Vector3 back = -transform.forward;
        // Sweep both segments so the shoulder offset cannot cross a nearby wall.
        Vector3 shoulder = pivot + right * ClearDistance(pivot, right, offset);
        transform.position = shoulder + back * ClearDistance(shoulder, back, reach);
    }

    private static float ClearDistance(Vector3 origin, Vector3 direction, float desired)
    {
        if (Physics.SphereCast(origin, 0.14f, direction, out var hit, desired,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return Mathf.Max(0f, hit.distance - 0.05f);
        return desired;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) ReleaseMouse();
    }

    private void OnDisable()
    {
        ReleaseMouse();
        zoom = 0f;
        if (view != null) view.fieldOfView = baseFieldOfView;
    }

    private void ReleaseMouse()
    {
        if (!mouseCaptured) return;
        Cursor.lockState = CursorLockMode.None;
        mouseCaptured = false;
    }
}
