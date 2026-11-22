using UnityEngine;

// Evaluates the hand constraints after the Animator has sampled the clips.
// The grip transforms and the holster can all move during the locomotion cycle.
internal sealed class IvanSidearmRig
{
    private const float TransferPhase = 0.40f;
    private const float ArmExtension = 0.991f;
    private static readonly Vector3 RaisedArmDirection = new Vector3(-0.02f, 0.15f, 1f).normalized;
    private readonly HumanoidArmIK right;
    private readonly HumanoidArmIK left;
    private readonly Transform root;
    private readonly Transform holster;
    private readonly Transform mount;
    private readonly Transform propDirection;
    private readonly Transform rightGrip;
    private readonly Transform leftGrip;

    public IvanSidearmRig(Animator animator, Transform holster, Transform mount,
        Transform propDirection, Transform rightGrip, Transform leftGrip)
    {
        root = animator.transform;
        right = new HumanoidArmIK(animator, AvatarIKGoal.RightHand);
        left = new HumanoidArmIK(animator, AvatarIKGoal.LeftHand);
        this.holster = holster;
        this.mount = mount;
        this.propDirection = propDirection;
        this.rightGrip = rightGrip;
        this.leftGrip = leftGrip;
    }

    public void Apply(float phase, float contact, float clearance, float raise, Quaternion aimDirection)
    {
        if (phase <= 0f && mount.parent == holster) return;

        float lead = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.15f, phase)) *
            (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.32f, phase)));
        right.Reach(right.Wrist.position + root.up * 0.024f - root.forward * 0.048f,
            right.Wrist.rotation, root.right * 0.3f - root.forward, lead);

        if (phase > 0f && contact > 0f)
        {
            Quaternion rotation = holster.rotation * Quaternion.Inverse(rightGrip.localRotation);
            Vector3 gripOffset = rotation * Vector3.Scale(rightGrip.localPosition, right.Wrist.lossyScale);
            right.Reach(holster.position + root.up * clearance - gripOffset, rotation,
                root.right * 0.45f - root.forward, contact);
        }

        if (raise > 0f)
        {
            Vector3 target = right.Shoulder + aimDirection * RaisedArmDirection * (right.Length * ArmExtension);
            Quaternion rotation = aimDirection * Quaternion.Inverse(rightGrip.localRotation * propDirection.localRotation);
            right.Reach(target, rotation, root.right * 0.45f - root.up, raise);
        }

        bool held = phase >= TransferPhase;
        if (mount.parent != (held ? right.Wrist : holster))
            Attach(held);

        // The support target belongs to the prop, so this solve must follow the transfer.
        float support = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.57f, 0.94f, phase));
        left.Reach(leftGrip.position, leftGrip.rotation, -root.right * 0.55f - root.up, support);
    }

    public void Holster() => Attach(false);

    private void Attach(bool held)
    {
        mount.SetParent(held ? right.Wrist : holster, false);
        mount.SetLocalPositionAndRotation(held ? rightGrip.localPosition : Vector3.zero,
            held ? rightGrip.localRotation : Quaternion.identity);
    }
}
