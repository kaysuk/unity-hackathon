using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(CharacterController), typeof(Animator), typeof(IvanStanding))]
[DisallowMultipleComponent]
public sealed class IvanStealth : MonoBehaviour
{
    private static readonly int Crouch = Animator.StringToHash("Base Layer.Stealth");
    private static readonly int Takedown = Animator.StringToHash("Base Layer.Rear Takedown");
    private const float PairBlendSeconds = 0.18f;
    private const float AlignmentPhase = 0.35f;
    private static readonly int Travel = Animator.StringToHash("Base Layer.Locomotion");

    [SerializeField, Min(0.1f)] private float crouchedHeight = 1.12f;
    [SerializeField, Min(0.1f)] private float sneakSpeed = 0.95f;
    [SerializeField, Min(0.1f)] private float targetRange = 1.05f;

    private readonly Collider[] headroomHits = new Collider[16];
    private CharacterController capsule;
    private Animator animator;
    private IvanStanding standing;
    private IvanSidearm sidearm;
    private InputAction crouch;
    private InputAction takedown;
    private RifleNpc victim;
    private CharacterController victimCapsule;
    private bool restorePairCollision;
    private bool resumeCrouchAfterAim;
    private CombatHealth health;
    private float fullHeight;
    private Vector3 fullCenter;

    public bool IsCrouching { get; private set; }
    public bool IsPerforming { get; private set; }
    public float Speed => sneakSpeed;
    public float CameraDrop => (fullHeight - capsule.height) * 0.8f;

    private void Awake()
    {
        capsule = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        standing = GetComponent<IvanStanding>();
        health = GetComponent<CombatHealth>();
        sidearm = GetComponent<IvanSidearm>();
        fullHeight = capsule.height;
        fullCenter = capsule.center;
        crouch = InputSystem.actions?.FindAction("Player/Crouch");
        takedown = InputSystem.actions?.FindAction("Player/Takedown");
    }

    private void Update()
    {
        if (!IsPerforming && !sidearm.IsAiming && crouch?.WasPressedThisFrame() == true &&
            (!IsCrouching || CanStand()))
        {
            resumeCrouchAfterAim = false;
            SetCrouching(!IsCrouching);
        }
        else if (resumeCrouchAfterAim && !sidearm.IsSidearmActive)
        {
            resumeCrouchAfterAim = false;
            if (!IsCrouching) SetCrouching(true);
        }

        float height = IsCrouching && !IsPerforming ? Mathf.Clamp(crouchedHeight, 2f * capsule.radius, fullHeight) : fullHeight;
        if (height > capsule.height && !CanStand())
        {
            IsCrouching = true;
            ReturnToCrouch();
            height = Mathf.Clamp(crouchedHeight, 2f * capsule.radius, fullHeight);
        }
        if (!Mathf.Approximately(capsule.height, height))
            Resize(Mathf.MoveTowards(capsule.height, height, Time.deltaTime * 3f));

        if (IsPerforming) UpdateGesture();
        else if (IsCrouching && !sidearm.IsSidearmActive && takedown?.WasPressedThisFrame() == true)
        {
            if (!CanStand()) return;
            var candidate = FindTarget();
            if (candidate == null || !candidate.BeginTakedown(this, PairBlendSeconds)) return;
            victim = candidate;
            GameAudio.Play(SoundCue.Grapple, victim.transform.position, 0.54f);
            victimCapsule = victim.GetComponent<CharacterController>();
            restorePairCollision = !Physics.GetIgnoreCollision(capsule, victimCapsule);
            if (restorePairCollision) Physics.IgnoreCollision(capsule, victimCapsule);
            transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(victim.transform.forward, Vector3.up));
            IsPerforming = true;
            animator.CrossFadeInFixedTime(Takedown, PairBlendSeconds, 0, 0f);
        }
    }

    public bool TryStandForAim()
    {
        if (IsPerforming || ((IsCrouching || capsule.height < fullHeight) && !CanStand())) return false;
        if (IsCrouching)
        {
            resumeCrouchAfterAim = true;
            SetCrouching(false);
        }
        return true;
    }

    private void SetCrouching(bool crouched)
    {
        IsCrouching = crouched;
        GameAudio.Play(SoundCue.Cloth, transform.position, 0.32f);
        standing.ContinueIntoLocomotion();
        animator.CrossFadeInFixedTime(crouched ? Crouch : Travel, 0.24f);
    }

    private void Resize(float height)
    {
        capsule.height = height;
        capsule.center = fullCenter - Vector3.up * ((fullHeight - height) * 0.5f);
    }

    private bool CanStand()
    {
        Vector3 bottom = transform.TransformPoint(fullCenter - Vector3.up * (fullHeight * 0.5f));
        float radius = capsule.radius;
        int count = Physics.OverlapCapsuleNonAlloc(bottom + Vector3.up * (capsule.height - radius),
            bottom + Vector3.up * (fullHeight - radius), radius - 0.005f, headroomHits, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        if (count == headroomHits.Length) return false;
        for (int i = 0; i < count; i++)
            if (headroomHits[i] != victimCapsule && !headroomHits[i].transform.IsChildOf(transform)) return false;
        return true;
    }

    private RifleNpc FindTarget()
    {
        RifleNpc nearest = null;
        float best = targetRange * targetRange;
        foreach (var hit in Physics.OverlapSphere(transform.position + Vector3.up * 0.7f, targetRange,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var npc = hit.GetComponentInParent<RifleNpc>();
            if (npc == null || !npc.AvailableForTakedown) continue;
            float distance = (npc.transform.position - transform.position).sqrMagnitude;
            if (distance >= best || !CanReach(npc)) continue;
            nearest = npc;
            best = distance;
        }
        return nearest;
    }

    private bool CanReach(RifleNpc target)
    {
        Vector3 offset = target.transform.position - transform.position;
        if (offset.sqrMagnitude > targetRange * targetRange || Mathf.Abs(offset.y) > 0.4f) return false;
        Vector3 direction = Vector3.ProjectOnPlane(offset, Vector3.up).normalized;
        if (Vector3.Dot(direction, target.transform.forward) < 0.6f ||
            Vector3.Dot(direction, transform.forward) < 0.5f) return false;
        var origin = transform.position + Vector3.up * 0.8f;
        return Physics.Raycast(origin, offset.normalized, out var hit, offset.magnitude + 0.05f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<RifleNpc>() == target;
    }

    private void UpdateGesture()
    {
        var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != Takedown || victim == null || !victim.IsPairedWith(this) || !CanReach(victim))
        {
            ReturnToCrouch();
            return;
        }
        if (state.normalizedTime < AlignmentPhase)
        {
            // Blend from standing separation into the contact spacing of the paired clips.
            float spacing = Mathf.Lerp(0.61f, 0.435f, Mathf.SmoothStep(0f, 1f, state.normalizedTime / AlignmentPhase));
            Vector3 anchor = victim.transform.position - victim.transform.forward * spacing;
            Vector3 correction = Vector3.ProjectOnPlane(anchor - transform.position, Vector3.up);
            capsule.Move(Vector3.ClampMagnitude(correction, Time.deltaTime * 2.3f));
        }
        if (state.normalizedTime >= 1f) ReturnToCrouch(true);
    }

    private void ReleaseVictim(bool completed)
    {
        if (restorePairCollision && capsule.enabled && victimCapsule != null && victimCapsule.enabled &&
            capsule.gameObject.activeInHierarchy && victimCapsule.gameObject.activeInHierarchy)
        {
            // On interruption both characters can still be standing. Separate before restoring collision.
            Vector3 away = Vector3.ProjectOnPlane(transform.position - victimCapsule.transform.position, Vector3.up);
            float clearance = capsule.radius + victimCapsule.radius + capsule.skinWidth;
            if (!completed && away.sqrMagnitude > 0f && away.magnitude < clearance)
                capsule.Move(away.normalized * (clearance - away.magnitude));
            Physics.IgnoreCollision(capsule, victimCapsule, false);
        }
        if (victim != null) victim.EndTakedown(this, completed);
        restorePairCollision = false;
        victimCapsule = null;
        victim = null;
    }

    private void ReturnToCrouch(bool completed = false)
    {
        ReleaseVictim(completed);
        IsPerforming = false;
        animator.CrossFadeInFixedTime(Crouch, 0.16f);
    }

    private void OnDisable()
    {
        resumeCrouchAfterAim = false;
        ReleaseVictim(false);
        IsPerforming = false;
        if (IsCrouching && (health == null || !health.IsDead) && animator.isActiveAndEnabled) animator.CrossFadeInFixedTime(Travel, 0.12f);
        IsCrouching = false;
        if (capsule != null) Resize(fullHeight);
    }
}
