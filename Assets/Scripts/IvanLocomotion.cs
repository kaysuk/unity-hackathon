using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController), typeof(IvanStanding))]
public sealed class IvanLocomotion : MonoBehaviour
{
    private static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int RunTurn = Animator.StringToHash("RunTurn");
    private const float ArmedTurnSpeed = 720f;
    private const float TurnTapDuration = 0.16f;

    [SerializeField, Min(0.1f)] private float walkSpeed = 1.5f;
    [SerializeField, Min(0.1f)] private float runSpeed = 3.0f;
    [SerializeField, Min(0.1f)] private float acceleration = 5f;
    [SerializeField, Min(0.1f)] private float braking = 8f;
    [SerializeField, Min(1f)] private float steeringDegreesPerSecond = 300f;

    private CharacterController capsule;
    private IvanStanding standing;
    private static readonly int SneakX = Animator.StringToHash("SneakX");
    private static readonly int SneakZ = Animator.StringToHash("SneakZ");
    private IvanStealth stealth;
    private IvanSidearm sidearm;
    private Vector3 travelDirection;
    private Animator animator;
    private InputAction move;
    private InputAction sprint;
    private float forwardSpeed;
    private float verticalSpeed;
    private float heldDirectionTime;
    private float? previousCameraYaw;
    private bool followLook;
    private bool wasAiming;

    private void Awake()
    {
        capsule = GetComponent<CharacterController>();
        standing = GetComponent<IvanStanding>();
        sidearm = GetComponent<IvanSidearm>();
        stealth = GetComponent<IvanStealth>();
        travelDirection = transform.forward;
        animator = GetComponent<Animator>();
        move = InputSystem.actions?.FindAction("Player/Move");
        sprint = InputSystem.actions?.FindAction("Player/Sprint");
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        var camera = Camera.main;
        float cameraYaw = camera != null ? camera.transform.eulerAngles.y : 0f;
        float yawDelta = camera != null && previousCameraYaw.HasValue
            ? Mathf.DeltaAngle(previousCameraYaw.Value, cameraYaw) : 0f;
        bool lookingAround = Mathf.Abs(yawDelta) > 0.001f;
        previousCameraYaw = camera != null ? cameraYaw : (float?)null;

        Vector2 input = camera != null && move != null
            ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f)
            : Vector2.zero;
        bool hasDirection = input.sqrMagnitude >= 0.12f;
        bool running = sprint != null && sprint.IsPressed();
        float requestedSpeed = hasDirection ? (running ? runSpeed : walkSpeed) * input.magnitude : 0f;
        float targetSpeed = 0f;
        float runTurn = 0f;

        bool armed = sidearm != null && sidearm.IsSidearmActive;
        bool aiming = camera != null && armed && sidearm.IsAiming;
        if (armed) standing.ContinueIntoLocomotion();

        bool crouched = stealth != null && stealth.IsCrouching;
        if (crouched)
        {
            heldDirectionTime = 0f;
            followLook = false;
            if (stealth.IsPerforming) forwardSpeed = 0f;
            else
            {
                TurnArmedTowards(cameraYaw + (aiming ? sidearm.StanceYaw : 0f), dt,
                    aiming && wasAiming ? yawDelta : 0f);
                if (hasDirection)
                {
                    travelDirection = Quaternion.Euler(0f, cameraYaw, 0f) * new Vector3(input.x, 0f, input.y).normalized;
                    targetSpeed = stealth.Speed * input.magnitude;
                }
            }
        }
        else if (aiming)
        {
            UpdateAimFacing(cameraYaw, wasAiming ? yawDelta : 0f, input, hasDirection, dt);
            targetSpeed = requestedSpeed;
        }
        else
        {
            if (hasDirection)
            {
                float yaw = cameraYaw + Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
                targetSpeed = UpdateTravelFacing(yaw, running, armed, dt, out runTurn) * requestedSpeed;
            }
            else
            {
                heldDirectionTime = 0f;
                followLook = camera != null && forwardSpeed <= 0.01f && (lookingAround || followLook) &&
                    (armed ? TurnArmedTowards(cameraYaw, dt) : standing.FollowCamera(cameraYaw));
            }
            travelDirection = transform.forward;
        }

        wasAiming = aiming;
        float actualSpeed = MoveCapsule(targetSpeed, dt);
        if (crouched)
        {
            Vector3 local = transform.InverseTransformDirection(travelDirection) * (actualSpeed / stealth.Speed);
            animator.SetFloat(SneakX, local.x, 0.10f, dt);
            animator.SetFloat(SneakZ, local.z, 0.10f, dt);
        }
        animator.SetFloat(MoveSpeed, actualSpeed);
        animator.SetFloat(RunTurn, runTurn * Mathf.Clamp01(actualSpeed / walkSpeed), 0.06f, dt);
    }

    private bool TurnArmedTowards(float yaw, float dt, float viewDelta = 0f)
    {
        // Once aiming, inherit the mouse turn immediately; only the initial stance offset needs catching up.
        float facing = Mathf.MoveTowardsAngle(transform.eulerAngles.y + viewDelta, yaw, ArmedTurnSpeed * dt);
        transform.rotation = Quaternion.Euler(0f, facing, 0f);
        return Mathf.Abs(Mathf.DeltaAngle(facing, yaw)) > 0.01f;
    }

    private void UpdateAimFacing(float cameraYaw, float viewDelta, Vector2 input, bool hasDirection, float dt)
    {
        followLook = false;
        heldDirectionTime = 0f;
        TurnArmedTowards(cameraYaw + sidearm.StanceYaw, dt, viewDelta);
        if (hasDirection)
            travelDirection = Quaternion.Euler(0f, cameraYaw, 0f) * new Vector3(input.x, 0f, input.y).normalized;
    }

    // Returns the fraction of requested travel speed that the current facing permits.
    private float UpdateTravelFacing(float yaw, bool running, bool armed, float dt, out float runTurn)
    {
        followLook = false;
        heldDirectionTime += dt;
        if (armed || running || heldDirectionTime >= TurnTapDuration)
            standing.ContinueIntoLocomotion();
        else if (forwardSpeed <= 0.01f)
            standing.FaceDirection(yaw);

        runTurn = 0f;
        if (standing.IsTurning) return 0f;
        float previousYaw = transform.eulerAngles.y;
        // A first step can pivot more tightly than a full-speed stride.
        float steering = running
            ? Mathf.Lerp(540f, 360f, Mathf.Clamp01(forwardSpeed / runSpeed))
            : steeringDegreesPerSecond;
        float facing = Mathf.MoveTowardsAngle(previousYaw, yaw, steering * dt);
        transform.rotation = Quaternion.Euler(0f, facing, 0f);
        float alignment = Mathf.Clamp01(Mathf.Cos(Mathf.DeltaAngle(facing, yaw) * Mathf.Deg2Rad));
        if (!running) return alignment;

        float yawRate = Mathf.DeltaAngle(previousYaw, facing) / dt;
        runTurn = Mathf.Clamp(yawRate / 220f, -1f, 1f);
        return Mathf.Lerp(0.65f, 1f, alignment);
    }

    private float MoveCapsule(float targetSpeed, float dt)
    {
        forwardSpeed = Mathf.MoveTowards(forwardSpeed, targetSpeed,
            (targetSpeed < forwardSpeed ? braking : acceleration) * dt);
        if (capsule.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        verticalSpeed += Physics.gravity.y * dt;

        Vector3 start = transform.position;
        var flags = capsule.Move((travelDirection * forwardSpeed + Vector3.up * verticalSpeed) * dt);
        if ((flags & CollisionFlags.Below) != 0) verticalSpeed = -2f;
        float travelled = Mathf.Max(0f, Vector3.Dot(transform.position - start, travelDirection) / dt);
        if ((flags & CollisionFlags.Sides) != 0) forwardSpeed = Mathf.Min(forwardSpeed, travelled);
        return Mathf.Min(forwardSpeed, travelled);
    }

    private void OnDisable()
    {
        forwardSpeed = 0f;
        verticalSpeed = 0f;
        heldDirectionTime = 0f;
        previousCameraYaw = null;
        wasAiming = false;
        followLook = false;
        if (animator != null && animator.isActiveAndEnabled)
        {
            animator.SetFloat(MoveSpeed, 0f);
            animator.SetFloat(RunTurn, 0f);
        }
    }
}
