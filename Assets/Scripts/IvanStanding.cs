using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class IvanStanding : MonoBehaviour
{
    private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
    private static readonly int Locomotion = Animator.StringToHash("Base Layer.Locomotion");
    private static readonly int TurnFraction = Animator.StringToHash("TurnFraction");
    private static readonly int TurnLeft = Animator.StringToHash("Base Layer.Turn Left");
    private static readonly int TurnRight = Animator.StringToHash("Base Layer.Turn Right");
    private static readonly int TurnBackLeft = Animator.StringToHash("Base Layer.Turn Back Left");
    private static readonly int TurnBackRight = Animator.StringToHash("Base Layer.Turn Back Right");
    private const float MinTurnAngle = 32f;

    private Animator animator;
    private float startYaw;
    private float requestedAngle;
    private bool followsCamera;
    private float cameraYaw;
    private float previousFraction;

    public bool IsTurning { get; private set; }

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void FaceDirection(float yaw)
    {
        if (!isActiveAndEnabled) return;
        if (followsCamera && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, yaw)) < MinTurnAngle)
            ContinueIntoLocomotion();
        else if (followsCamera || CanStartTurn())
            StartTurn(yaw, false);
    }

    public bool FollowCamera(float yaw)
    {
        cameraYaw = yaw;
        if (Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, yaw)) < MinTurnAngle)
            return followsCamera;
        if (CanStartTurn()) StartTurn(yaw, true);
        return true;
    }

    private bool CanStartTurn() => isActiveAndEnabled && !IsTurning && !animator.IsInTransition(0) &&
        animator.GetCurrentAnimatorStateInfo(0).fullPathHash == Idle;

    private void StartTurn(float yaw, bool fromCamera)
    {
        float angle = Mathf.DeltaAngle(transform.eulerAngles.y, yaw);
        if (Mathf.Abs(angle) < MinTurnAngle) return;
        followsCamera = fromCamera;
        previousFraction = 0f;
        bool reverse = Mathf.Abs(angle) > 135f;
        int turnState = angle < 0f
            ? (reverse ? TurnBackLeft : TurnLeft)
            : (reverse ? TurnBackRight : TurnRight);
        IsTurning = true;
        GameAudio.Play(SoundCue.Scuff, transform.position, 0.22f);
        startYaw = transform.eulerAngles.y;
        requestedAngle = angle;
        animator.CrossFadeInFixedTime(turnState, 0.10f, 0, 0f);
    }

    public void ContinueIntoLocomotion()
    {
        if (!IsTurning) return;
        EndTurn();
        animator.CrossFadeInFixedTime(Locomotion, 0.10f, 0, 0f);
    }

    private void OnAnimatorMove()
    {
        // Keep the planted character at its scene position. The baked hip yaw
        // curve times world rotation to the footwork, including mirrored turns.
        if (!IsTurning) return;
        bool exiting = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == Idle;
        bool idle = !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == Idle;
        float fraction = exiting || idle ? 1f : Mathf.Clamp01(animator.GetFloat(TurnFraction));
        float facing = startYaw + requestedAngle * fraction;
        if (followsCamera)
        {
            // Retarget the remaining part of the planted step without reversing its footwork.
            float progress = Mathf.Max(0f, fraction - previousFraction);
            float direction = Mathf.Sign(requestedAngle);
            float remaining = direction * Mathf.DeltaAngle(transform.eulerAngles.y, cameraYaw);
            float anglePerCycle = Mathf.Clamp(remaining / Mathf.Max(0.0001f, 1f - previousFraction), 0f, 180f);
            facing = transform.eulerAngles.y + direction * anglePerCycle * progress;
            previousFraction = Mathf.Max(previousFraction, fraction);
        }
        transform.rotation = Quaternion.Euler(0f, facing, 0f);
        if (exiting || idle) EndTurn();
    }

    private void EndTurn()
    {
        IsTurning = false;
        followsCamera = false;
    }

    private void OnDisable() => EndTurn();
}
