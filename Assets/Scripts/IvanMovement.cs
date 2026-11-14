using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController), typeof(Animator))]
public sealed class IvanMovement : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float forwardSpeed = 2f;
    [SerializeField, Min(1f)] private float runMultiplier = 2f;
    [SerializeField, Min(0.01f)] private float acceleration = 8f;
    [SerializeField, Min(1f)] private float rotationSpeed = 540f;
    [SerializeField, Min(0f)] private float animationDampTime = 0.1f;
    [SerializeField, Range(10f, 90f)] private float idleTurnThreshold = 35f;

    private CharacterController controller;
    private Animator animator;
    private ShoulderCamera orbitCamera;
    private Transform cameraTransform;
    private InputAction moveAction;
    private InputAction sprintAction;
    private readonly Dictionary<string, float> turnLengths = new Dictionary<string, float>();
    private float speed;
    private float verticalSpeed;
    private float turnTime;
    private float turnLength;
    private float turnStart;
    private float turnAngle;
    private float idleTargetYaw;
    private bool turning;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        moveAction = InputSystem.actions?.FindAction("Player/Move");
        sprintAction = InputSystem.actions?.FindAction("Player/Sprint");
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            orbitCamera = Camera.main.GetComponent<ShoulderCamera>();
        }
        if (animator.runtimeAnimatorController != null)
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                turnLengths[clip.name] = clip.length;
        idleTargetYaw = transform.eulerAngles.y;
        if (moveAction == null || cameraTransform == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("IvanMovement requires Player/Move, a Main Camera and an Animator Controller.", this);
            enabled = false;
        }
    }

    private void OnEnable() => SetCursor(true);
    private void OnDisable() => SetCursor(false);
    private void OnApplicationFocus(bool focused)
    {
        if (!focused) SetCursor(false);
    }

    private static void SetCursor(bool captured)
    {
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    private bool BeginTurn(float angle)
    {
        string name = Mathf.Abs(angle) > 150f ? "Turn Back" : angle < 0f ? "Turn Left" : "Turn Right";
        if (!turnLengths.TryGetValue(name, out turnLength)) return false;
        if (!animator.HasState(0, Animator.StringToHash(name))) return false;
        turnLength = Mathf.Max(0.01f, turnLength);
        turnStart = transform.eulerAngles.y;
        turnAngle = angle;
        turnTime = 0f;
        turning = true;
        speed = 0f;
        animator.CrossFadeInFixedTime(name, 0.1f);
        return true;
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            SetCursor(false);
        else if (Mouse.current?.leftButton.wasPressedThisFrame == true && Application.isFocused)
            SetCursor(true);
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        bool captured = Cursor.lockState == CursorLockMode.Locked;
        Vector2 input = captured ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
        float cameraYaw = orbitCamera != null ? orbitCamera.Yaw : cameraTransform.eulerAngles.y;
        Vector3 desiredDirection = Quaternion.Euler(0f, cameraYaw, 0f) * new Vector3(input.x, 0f, input.y);
        bool moving = input.sqrMagnitude > 0.01f;
        float desiredYaw = moving
            ? Mathf.Atan2(desiredDirection.x, desiredDirection.z) * Mathf.Rad2Deg
            : idleTargetYaw;

        if (moving)
            idleTargetYaw = desiredYaw;
        else if (captured && orbitCamera != null && Mathf.Abs(orbitCamera.YawDelta) > 0.01f)
        {
            idleTargetYaw = cameraYaw;
            desiredYaw = idleTargetYaw;
        }

        float angle = Mathf.DeltaAngle(transform.eulerAngles.y, desiredYaw);
        if (captured && !turning && speed < 0.05f &&
            Mathf.Abs(angle) > (moving ? 70f : idleTurnThreshold))
            BeginTurn(angle);

        if (turning)
        {
            turnTime = Mathf.Min(turnTime + dt, turnLength);
            float progress = Mathf.SmoothStep(0f, 1f, turnTime / turnLength);
            transform.rotation = Quaternion.Euler(0f, turnStart + turnAngle * progress, 0f);
            if (turnTime >= turnLength)
            {
                turning = false;
                animator.CrossFadeInFixedTime("Locomotion", 0.1f);
            }
        }
        else if (moving)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.Euler(0f, desiredYaw, 0f), rotationSpeed * dt);
        }

        bool running = sprintAction?.IsPressed() == true;
        float alignment = Mathf.Max(0f, Vector3.Dot(transform.forward, desiredDirection.normalized));
        float targetSpeed = moving && !turning
            ? forwardSpeed * (running ? runMultiplier : 1f) * input.magnitude * alignment : 0f;
        speed = Mathf.MoveTowards(speed, targetSpeed, acceleration * dt);
        // Move along the body's facing direction while it catches up with the camera-relative input.
        Vector3 direction = transform.forward;
        if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = 0f;
        verticalSpeed += Physics.gravity.y * dt;
        controller.Move((direction * speed + Vector3.up * verticalSpeed) * dt);
        float actualSpeed = Vector3.ProjectOnPlane(controller.velocity, Vector3.up).magnitude;
        animator.SetFloat("MoveZ", 2f * actualSpeed / forwardSpeed, animationDampTime, dt);
        animator.SetFloat("MoveX", 0f, animationDampTime, dt);
    }
}
