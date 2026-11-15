using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController), typeof(Animator))]
public sealed class IvanMovement : MonoBehaviour
{
    private const float TurnBlendDuration = 0.1f;
    private static readonly int MoveXId = Animator.StringToHash("MoveX");
    private static readonly int MoveZId = Animator.StringToHash("MoveZ");
    private static readonly int AimHeldId = Animator.StringToHash("AimHeld");
    private static readonly int LocomotionId = Animator.StringToHash("Base Layer.Locomotion");

    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float forwardSpeed = 2f;
    [SerializeField, Min(1f)] private float runMultiplier = 2f;
    [SerializeField, Min(0.01f)] private float acceleration = 8f;
    [SerializeField, Min(1f)] private float rotationSpeed = 540f;
    [SerializeField, Min(0f)] private float animationDampTime = 0.1f;
    [SerializeField, Range(10f, 90f)] private float idleTurnThreshold = 35f;

    [Header("Aiming")]
    [SerializeField] private bool readAimFromMouse = true;
    [SerializeField, Min(1f)] private float aimRotationSpeed = 540f;

    public bool IsAiming { get; private set; }

    private Vector3 horizontalVelocity;
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
    private bool hasAimHeldParameter;

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
        foreach (var parameter in animator.parameters)
        {
            if (parameter.nameHash == AimHeldId && parameter.type == AnimatorControllerParameterType.Bool)
                hasAimHeldParameter = true;
        }

        idleTargetYaw = transform.eulerAngles.y;
        if (moveAction == null || cameraTransform == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("IvanMovement requires Player/Move, a Main Camera and an Animator Controller.", this);
            enabled = false;
        }
    }

    private void OnEnable() => SetCursor(true);
    private void OnDisable()
    {
        ResetAim();
        horizontalVelocity = Vector3.zero;
        speed = 0f;
        verticalSpeed = 0f;
        turning = false;
        SetCursor(false);
    }
    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
        {
            ResetAim();
            SetCursor(false);
        }
    }

    private void ResetAim()
    {
        IsAiming = false;
        if (animator != null && hasAimHeldParameter)
            animator.SetBool(AimHeldId, false);
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
        animator.CrossFadeInFixedTime(name, TurnBlendDuration, 0);
        return true;
    }

    private void Update()
    {
        UpdateCursor();
        bool captured = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        bool aimHeld = UpdateAimInput(captured);
        IsAiming = aimHeld;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        Vector2 input = captured
            ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f)
            : Vector2.zero;
        float cameraYaw = orbitCamera != null ? orbitCamera.Yaw : cameraTransform.eulerAngles.y;
        Vector3 desiredDirection = Quaternion.Euler(0f, cameraYaw, 0f) * new Vector3(input.x, 0f, input.y);
        bool moving = input.sqrMagnitude > 0.01f;
        float desiredYaw = aimHeld ? cameraYaw : GetDesiredYaw(desiredDirection, moving, captured, cameraYaw);
        if (aimHeld) idleTargetYaw = cameraYaw;
        bool weaponTransition = IsWeaponTransitionActive();
        if (weaponTransition)
        {
            turning = false;
            horizontalVelocity = Vector3.zero;
            speed = 0f;
        }
        else
        {
            UpdateRotation(desiredYaw, moving, captured, aimHeld, deltaTime);
        }
        UpdateMovement(input, desiredDirection, moving && !weaponTransition, aimHeld, deltaTime);
        UpdateAnimation(aimHeld, deltaTime);
    }

    private static bool IsWeaponTransitionState(AnimatorStateInfo state)
    {
        return state.IsName("Base Layer.PistolUp") || state.IsName("Base Layer.PistolDown");
    }

    private bool IsWeaponTransitionActive()
    {
        // Keep feet planted through both the incoming and outgoing animation blends.
        if (IsWeaponTransitionState(animator.GetCurrentAnimatorStateInfo(0))) return true;
        return animator.IsInTransition(0) &&
            IsWeaponTransitionState(animator.GetNextAnimatorStateInfo(0));
    }

    private bool UpdateAimInput(bool captured)
    {
        if (!hasAimHeldParameter) return false;

        if (readAimFromMouse)
        {
            bool held = captured && Application.isFocused && Mouse.current?.rightButton.isPressed == true;
            animator.SetBool(AimHeldId, held);
        }

        return animator.GetBool(AimHeldId);
    }

    private static void UpdateCursor()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            SetCursor(false);
        else if (Mouse.current?.leftButton.wasPressedThisFrame == true && Application.isFocused)
            SetCursor(true);
    }

    private float GetDesiredYaw(Vector3 direction, bool moving, bool captured, float cameraYaw)
    {
        if (moving)
            idleTargetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        else if (captured && orbitCamera != null && Mathf.Abs(orbitCamera.YawDelta) > 0.01f)
            idleTargetYaw = cameraYaw;

        return idleTargetYaw;
    }

    private bool IsTurnStateActive()
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName("Turn Left") || state.IsName("Turn Right") || state.IsName("Turn Back");
    }

    private void UpdateRotation(float desiredYaw, bool moving, bool captured, bool aimHeld, float deltaTime)
    {
        // Only finish turns we still own. Never force a weapon state back to Locomotion.
        if (turning && aimHeld)
        {
            // Let an in-flight blend settle so a pending turn cannot strand the Animator.
            if (animator.IsInTransition(0)) return;
            turning = false;
            if (IsTurnStateActive())
                animator.CrossFadeInFixedTime(LocomotionId, TurnBlendDuration, 0);
        }
        else if (turning && !animator.IsInTransition(0) && !IsTurnStateActive())
        {
            turning = false;
        }

        bool canStartTurn = !aimHeld && !animator.IsInTransition(0) &&
            animator.GetCurrentAnimatorStateInfo(0).fullPathHash == LocomotionId;
        float angle = Mathf.DeltaAngle(transform.eulerAngles.y, desiredYaw);
        if (canStartTurn && captured && !turning && speed < 0.05f &&
            Mathf.Abs(angle) > (moving ? 70f : idleTurnThreshold))
            BeginTurn(angle);

        if (turning)
        {
            turnTime = Mathf.Min(turnTime + deltaTime, turnLength);
            float progress = Mathf.SmoothStep(0f, 1f, turnTime / turnLength);
            transform.rotation = Quaternion.Euler(0f, turnStart + turnAngle * progress, 0f);
            if (turnTime >= turnLength)
            {
                turning = false;
                if (IsTurnStateActive() && !animator.IsInTransition(0))
                    animator.CrossFadeInFixedTime(LocomotionId, TurnBlendDuration, 0);
            }
        }
        else if (moving || (aimHeld && captured))
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.Euler(0f, desiredYaw, 0f),
                (aimHeld ? aimRotationSpeed : rotationSpeed) * deltaTime);
        }
    }

    private void UpdateMovement(Vector2 input, Vector3 desiredDirection, bool moving, bool aimHeld, float deltaTime)
    {
        if (aimHeld)
        {
            // Aim movement follows the camera independently of the body's facing direction.
            // Vector acceleration also smooths reversal and keeps braking in the travel direction.
            Vector3 targetVelocity = moving ? desiredDirection * forwardSpeed : Vector3.zero;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * deltaTime);
            speed = horizontalVelocity.magnitude;
        }
        else
        {
            bool running = sprintAction?.IsPressed() == true;
            float alignment = Mathf.Max(0f, Vector3.Dot(transform.forward, desiredDirection.normalized));
            float targetSpeed = moving && !turning
                ? forwardSpeed * (running ? runMultiplier : 1f) * input.magnitude * alignment : 0f;
            // Preserve momentum when leaving aim while moving backward or sideways.
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity,
                transform.forward * targetSpeed, acceleration * deltaTime);
            speed = horizontalVelocity.magnitude;
        }

        if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = 0f;
        verticalSpeed += Physics.gravity.y * deltaTime;
        controller.Move((horizontalVelocity + Vector3.up * verticalSpeed) * deltaTime);
    }

    private void UpdateAnimation(bool aimHeld, float deltaTime)
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(controller.velocity, Vector3.up);
        Vector3 localVelocity = transform.InverseTransformDirection(planarVelocity);
        float animationScale = 2f / forwardSpeed;
        float moveX = aimHeld ? localVelocity.x * animationScale : 0f;
        float moveZ = aimHeld ? localVelocity.z * animationScale : planarVelocity.magnitude * animationScale;
        animator.SetFloat(MoveXId, moveX, animationDampTime, deltaTime);
        animator.SetFloat(MoveZId, moveZ, animationDampTime, deltaTime);
    }
}
