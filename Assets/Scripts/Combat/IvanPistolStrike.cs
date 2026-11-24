using UnityEngine;

// A short upper-body gesture sampled after the sidearm's ordinary pose.
internal sealed class IvanPistolStrike
{
    private const float Contact = 0.23f;
    private const float ContactEnd = .30f;
    private const float ContactRadius = .15f;
    // Pursuing guards stop at 1.1 m. Allow a small lateral offset at that distance.
    private const float AssistRange = 1.18f;
    private const float StandingReach = .78f;
    // Wrist arc in facing space, in units of the animated arm length.
    private static readonly Vector3 WindupOffset = new Vector3(.44f, .22f, .42f);
    private static readonly Vector3 ArcControl = new Vector3(.15f, .445f, .855f);
    private static readonly Vector3 ContactOffset = new Vector3(-.19f, .445f, .855f);
    private static readonly Quaternion RaisedPistol = Quaternion.Euler(-28f, -26f, -6f);
    // In Sidearm Direction space the grip slopes slightly toward the rear.
    // In Ivan's facing frame the magazine points left and the slide sits on the right.
    private static readonly Vector3 MagazineAxis = new Vector3(-.039f, -1f, -.161f).normalized;
    private static readonly Quaternion ContactPistol = Quaternion.LookRotation(Vector3.left, Vector3.down)
        * Quaternion.Inverse(Quaternion.LookRotation(MagazineAxis, Vector3.right));
    // The contact key lies on the moving part of the curve. A miss carries through it.
    private static readonly AnimationCurve Swing = new AnimationCurve(
        new Keyframe(.10f, 0f, 0f, 0f),
        new Keyframe(.17f, .30f, 9f, 9f),
        new Keyframe(Contact, 1f, 6f, 6f),
        new Keyframe(.34f, 1.28f, 0f, 0f));
    private readonly Transform actor;
    private readonly Transform spine;
    private readonly CharacterController controller;
    private readonly Transform grip;
    private Quaternion spineRest;
    private Quaternion spineApplied;
    private bool spinePosed;
    private float elapsed;
    private CombatHealth target;
    private SwatHitZones targetZones;
    private float stepLength;
    private Vector3 wristCorrection;
    private Vector3 previousGrip;
    private bool hasPreviousGrip;
    private bool sampledContact;
    private float landedAt;
    private bool landed;
    public bool Active { get; private set; }

    public IvanPistolStrike(Animator animator, Transform grip)
    {
        actor = animator.transform;
        controller = animator.GetComponent<CharacterController>();
        this.grip = grip;
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
    }

    public void Begin(Quaternion facing)
    {
        Active = true;
        elapsed = 0f;
        sampledContact = false;
        hasPreviousGrip = false;
        wristCorrection = Vector3.zero;
        target = FindTarget(facing * Vector3.forward);
        targetZones = target != null ? target.GetComponent<SwatHitZones>() : null;
        stepLength = target != null ? Mathf.Clamp(PlanarOffset(target).magnitude - StandingReach, 0f, .33f) : 0f;
        landed = false;
    }

    public void Cancel()
    {
        RestoreSpine();
        Active = false;
    }

    public void Advance(float dt)
    {
        RestoreSpine();
        if (!Active) return;
        float previous = elapsed;
        elapsed += dt;
        // Even at a low frame rate, evaluate the contact pose once before recovery.
        if (!sampledContact) elapsed = Mathf.Min(elapsed, Contact);
        else if (previous < ContactEnd) elapsed = Mathf.Min(elapsed, ContactEnd);
        if (stepLength > 0f && target != null && !target.IsDead && controller != null && controller.enabled && previous < Contact)
        {
            Vector3 offset = PlanarOffset(target);
            float stride = stepLength * (Ease(.045f, Contact, elapsed) - Ease(.045f, Contact, previous));
            stride = Mathf.Min(stride, Mathf.Max(0f, offset.magnitude - StandingReach));
            if (stride > 0f && offset.sqrMagnitude <= AssistRange * AssistRange)
                controller.Move(offset.normalized * stride);
        }
        if (elapsed >= Duration) Active = false;
    }

    private float Duration => landed ? 0.50f : 0.60f;

    private void RestoreSpine()
    {
        if (spinePosed && Quaternion.Angle(spine.localRotation, spineApplied) < .01f)
            spine.localRotation = spineRest;
        spinePosed = false;
    }

    private static float Ease(float start, float end, float time) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));

    public void PoseTorso()
    {
        if (!Active || spine == null) return;
        float windup = Ease(0f, .10f, elapsed);
        float turn = Ease(.08f, Contact, elapsed);
        float settle = 1f - Ease(Contact, Duration - .04f, elapsed);
        float yaw = (12f * windup - 36f * turn) * settle;
        spineRest = spine.localRotation;
        Quaternion lean = Quaternion.AngleAxis(16f * turn * settle, actor.right);
        spine.rotation = lean * Quaternion.AngleAxis(yaw, actor.up) * spine.rotation;
        spineApplied = spine.localRotation;
        spinePosed = true;
    }

    public float PoseArms(HumanoidArmIK right, HumanoidArmIK left, Quaternion facing, Quaternion gripRotation)
    {
        if (!Active) return 0f;
        float travel = Swing.Evaluate(elapsed);
        if (landed)
        {
            // Brief compression on impact, then let the arm fold back.
            float stop = Mathf.Clamp01((elapsed - landedAt) / .035f);
            travel = Swing.Evaluate(landedAt) + .10f * stop * (2f - stop);
        }
        float returnStart = landed ? landedAt + .02f : .31f;
        float weight = Ease(0f, .12f, elapsed) * (1f - Ease(returnStart, Duration, elapsed));
        Vector3 approach = Vector3.LerpUnclamped(WindupOffset, ArcControl, travel);
        Vector3 finish = Vector3.LerpUnclamped(ArcControl, ContactOffset, travel);
        Vector3 arc = Vector3.LerpUnclamped(approach, finish, travel);
        float recovery = Ease(returnStart, Duration, elapsed);
        arc += new Vector3(.08f, -.14f, -.22f) * recovery;

        // The shoulder starts the gesture. The forearm rolls during the approach
        // and the grip settles into line with the arc just before contact.
        float roll = Ease(.09f, Contact - .012f, elapsed);
        Quaternion rotation = facing * Quaternion.Slerp(RaisedPistol, ContactPistol, roll) * gripRotation;
        Vector3 elbowHint = Vector3.Lerp(new Vector3(.45f, -1f, -.15f), new Vector3(.72f, -.82f, .08f), roll);
        // Correct the wrist for the actual grip position on this hand pose.
        if (!sampledContact && target != null && !target.IsDead)
        {
            Vector3 wristToGrip = Quaternion.Inverse(right.Wrist.rotation) * (grip.position - right.Wrist.position);
            Vector3 nominalGrip = right.Shoulder + facing * ContactOffset * right.Length
                + facing * ContactPistol * gripRotation * wristToGrip;
            Vector3 nearSide = AimPoint(target, targetZones) - PlanarOffset(target).normalized * .10f;
            Vector3 error = nearSide - nominalGrip;
            wristCorrection = Vector3.ClampMagnitude(error - Vector3.ClampMagnitude(error, .08f), .34f);
        }
        Vector3 wristTarget = right.Shoulder + facing * arc * right.Length + wristCorrection * Ease(.06f, .20f, elapsed);
        right.Reach(wristTarget, rotation, facing * elbowHint, weight);

        // The free hand stays near the chest until the pistol is on its way back.
        float released = Ease(0f, .09f, elapsed) * (1f - Ease(returnStart + .04f, Duration, elapsed));
        Vector3 tuck = new Vector3(.14f, -.32f, .37f);
        left.Reach(left.Shoulder + facing * tuck * left.Length, left.Wrist.rotation,
            facing * new Vector3(-.6f, -1f, 0f), released);
        return released;
    }

    private Vector3 PlanarOffset(CombatHealth health) =>
        Vector3.ProjectOnPlane(health.transform.position - actor.position, Vector3.up);

    private static Vector3 AimPoint(CombatHealth health, SwatHitZones zones) =>
        zones != null ? zones.HeadPosition : health.transform.position + Vector3.up * 1.55f;

    private CombatHealth FindTarget(Vector3 forward)
    {
        Physics.SyncTransforms();
        CombatHealth best = null;
        float bestScore = float.PositiveInfinity;
        foreach (var part in Physics.OverlapSphere(actor.position + Vector3.up, AssistRange,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var health = part.GetComponentInParent<CombatHealth>();
            if (health == null || health.transform == actor || health.IsDead) continue;
            Vector3 offset = PlanarOffset(health);
            float distance = offset.magnitude;
            float alignment = Vector3.Dot(offset.normalized, forward);
            if (distance > AssistRange || alignment < .74f) continue;
            float score = distance + (1f - alignment) * .65f;
            if (score >= bestScore) continue;
            Vector3 point = AimPoint(health, health.GetComponent<SwatHitZones>());
            if (Mathf.Abs(point.y - actor.position.y - 1.6f) > .48f || !CanReach(health, point)) continue;
            best = health;
            bestScore = score;
        }
        return best;
    }

    private bool CanReach(CombatHealth health, Vector3 point)
    {
        Vector3 origin = actor.position;
        origin.y = point.y;
        Vector3 reach = point - origin;
        foreach (var hit in Physics.SphereCastAll(origin, .035f, reach.normalized, reach.magnitude,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(actor) && !hit.transform.IsChildOf(health.transform)) return false;
        }
        return true;
    }

    public void CheckContact(Vector3 forward)
    {
        if (!Active || landed || elapsed < .20f || elapsed > ContactEnd) return;
        Vector3 end = grip.position;
        Vector3 start = hasPreviousGrip ? previousGrip : end;
        // Keep the approach sample until the contact pose, then sweep frame to frame.
        if (elapsed < Contact)
        {
            if (!hasPreviousGrip) previousGrip = end;
            hasPreviousGrip = true;
            return;
        }
        previousGrip = end;
        hasPreviousGrip = true;
        sampledContact = true;

        Physics.SyncTransforms();
        CombatHealth victim = null;
        Vector3 impact = end;
        float bestDistance = float.PositiveInfinity;
        foreach (var part in Physics.OverlapCapsule(start, end, ContactRadius,
                     Physics.AllLayers, QueryTriggerInteraction.Collide))
        {
            var health = part.GetComponentInParent<CombatHealth>();
            if (health == null || health.transform == actor || health.IsDead || Vector3.Dot(PlanarOffset(health), forward) <= 0f)
                continue;
            if (part.isTrigger && (!health.TryGetComponent<SwatHitZones>(out var zones) || !zones.Contains(part)))
                continue;
            Vector3 point = part.ClosestPoint(end);
            float distance = (point - end).sqrMagnitude;
            if (distance >= bestDistance || !CanReach(health, point)) continue;
            victim = health;
            impact = point;
            bestDistance = distance;
        }
        if (victim == null) return;
        landed = true;
        landedAt = elapsed;
        Vector3 direction = PlanarOffset(victim).normalized;
        victim.TakeDamage(20f, impact, direction, actor.gameObject);
        if (!victim.IsDead && victim.TryGetComponent<RifleNpc>(out var guard))
            guard.OnMeleeHit(actor.gameObject, direction);
    }
}
