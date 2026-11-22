using UnityEngine;

// Two-bone solve on the evaluated Humanoid pose. Bone lengths come from the
// current avatar, including any scale inherited from its hierarchy.
internal sealed class HumanoidArmIK
{
    private readonly Transform upper;
    private readonly Transform lower;
    public Transform Wrist { get; }
    public Vector3 Shoulder => upper.position;
    public float Length => Vector3.Distance(Shoulder, lower.position) + Vector3.Distance(lower.position, Wrist.position);

    public HumanoidArmIK(Animator animator, AvatarIKGoal hand)
    {
        bool left = hand == AvatarIKGoal.LeftHand;
        upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        Wrist = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
    }

    public void Reach(Vector3 target, Quaternion orientation, Vector3 elbowHint, float weight)
    {
        if (weight <= 0f) return;

        target = Vector3.Lerp(Wrist.position, target, weight);
        orientation = Quaternion.Slerp(Wrist.rotation, orientation, weight);

        Vector3 origin = Shoulder;
        float upperLength = Vector3.Distance(origin, lower.position);
        float lowerLength = Vector3.Distance(lower.position, Wrist.position);
        float distance = Vector3.Distance(origin, target);
        if (distance <= 0.0001f || upperLength <= 0.0001f || lowerLength <= 0.0001f) return;

        Vector3 axis = (target - origin) / distance;
        float span = Mathf.Clamp(distance, Mathf.Abs(upperLength - lowerLength) + 0.0001f,
            upperLength + lowerLength - 0.0001f);
        float along = (upperLength * upperLength - lowerLength * lowerLength + span * span) / (2f * span);
        Vector3 bend = Vector3.ProjectOnPlane(lower.position - origin, axis).normalized;
        Vector3 pole = Vector3.ProjectOnPlane(elbowHint, axis).normalized;
        Vector3 bendDirection = Vector3.Lerp(bend, pole, weight);
        if (bendDirection.sqrMagnitude < 0.00001f) bendDirection = pole;

        Vector3 elbow = origin + axis * along + bendDirection.normalized *
            Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        upper.rotation = Quaternion.FromToRotation(lower.position - origin, elbow - origin) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(Wrist.position - lower.position,
            origin + axis * span - lower.position) * lower.rotation;

        // Distribute axial roll along the forearm before setting the final hand orientation.
        Vector3 rollAxis = (Wrist.position - lower.position).normalized;
        Quaternion delta = orientation * Quaternion.Inverse(Wrist.rotation);
        float axial = Vector3.Dot(new Vector3(delta.x, delta.y, delta.z), rollAxis);
        float twist = Mathf.DeltaAngle(0f, 2f * Mathf.Atan2(axial, delta.w) * Mathf.Rad2Deg);
        lower.rotation = Quaternion.AngleAxis(Mathf.Clamp(twist * 0.6f, -55f, 55f), rollAxis) * lower.rotation;
        Wrist.rotation = orientation;
    }
}
