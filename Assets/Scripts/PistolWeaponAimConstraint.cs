using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

[Serializable]
public struct PistolWeaponAimData : IAnimationJobData
{
    public Transform rightUpperArm;
    public Transform muzzle;
    [SyncSceneToStream] public Transform target;
    [SyncSceneToStream, Range(0f, 90f)] public float maxCorrectionAngle;
    [SyncSceneToStream, Range(0f, 20f)] public float recoilAngle;

    public bool IsValid()
    {
        return rightUpperArm != null && muzzle != null && target != null &&
            muzzle != rightUpperArm && muzzle.IsChildOf(rightUpperArm) &&
            !target.IsChildOf(rightUpperArm);
    }

    public void SetDefaultValues()
    {
        rightUpperArm = null;
        muzzle = null;
        target = null;
        maxCorrectionAngle = 60f;
        recoilAngle = 0f;
    }
}

public struct PistolWeaponAimJob : IWeightedAnimationJob
{
    public ReadWriteTransformHandle arm;
    public ReadOnlyTransformHandle muzzle;
    public ReadOnlyTransformHandle target;
    public FloatProperty maxCorrectionAngle;
    public FloatProperty recoilAngle;
    public FloatProperty jobWeight { get; set; }

    public void ProcessRootMotion(AnimationStream stream) { }

    public void ProcessAnimation(AnimationStream stream)
    {
        float weight = Mathf.Clamp01(jobWeight.Get(stream));
        if (weight <= 0f)
        {
            AnimationRuntimeUtils.PassThrough(stream, arm);
            return;
        }

        Vector3 pivot = arm.GetPosition(stream);
        Quaternion initialRotation = arm.GetRotation(stream);
        Vector3 muzzleOffset = muzzle.GetPosition(stream) - pivot;
        Vector3 initialForward = muzzle.GetRotation(stream) * Vector3.forward;
        Vector3 targetPosition = target.GetPosition(stream);
        float limit = Mathf.Clamp(maxCorrectionAngle.Get(stream), 0f, 90f);

        // Rotate the arm as a unit: the existing elbow bend, wrist pose and grip
        // remain intact. Recompute muzzle position because it moves around the shoulder.
        Quaternion correction = Quaternion.identity;
        for (int i = 0; i < 16; i++)
        {
            Vector3 origin = pivot + correction * muzzleOffset;
            Vector3 desired = targetPosition - origin;
            if (desired.sqrMagnitude < 0.0001f) break;

            Quaternion step = Quaternion.FromToRotation(correction * initialForward, desired.normalized);
            correction = Quaternion.RotateTowards(Quaternion.identity, step * correction, limit);
        }

        // Blend once, not once per iteration; rig weight must retain its meaning.
        Quaternion aimRotation = Quaternion.Slerp(Quaternion.identity, correction, weight);
        Vector3 recoilAxis = aimRotation * (muzzle.GetRotation(stream) * Vector3.right);
        Quaternion recoil = Quaternion.AngleAxis(-Mathf.Clamp(recoilAngle.Get(stream), 0f, 20f) * weight,
            recoilAxis);
        arm.SetRotation(stream, recoil * aimRotation * initialRotation);
    }
}

public sealed class PistolWeaponAimBinder : AnimationJobBinder<PistolWeaponAimJob, PistolWeaponAimData>
{
    public override PistolWeaponAimJob Create(Animator animator, ref PistolWeaponAimData data, Component component)
    {
        return new PistolWeaponAimJob
        {
            arm = ReadWriteTransformHandle.Bind(animator, data.rightUpperArm),
            muzzle = ReadOnlyTransformHandle.Bind(animator, data.muzzle),
            target = ReadOnlyTransformHandle.Bind(animator, data.target),
            maxCorrectionAngle = FloatProperty.Bind(animator, component,
                ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(PistolWeaponAimData.maxCorrectionAngle))),
            recoilAngle = FloatProperty.Bind(animator, component,
                ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(PistolWeaponAimData.recoilAngle)))
        };
    }

    public override void Destroy(PistolWeaponAimJob job) { }
}

// Place after BodyAim and before LeftHandIK in the same Rig hierarchy.
[DisallowMultipleComponent]
[AddComponentMenu("Animation Rigging/Pistol Weapon Aim Constraint")]
public sealed class PistolWeaponAimConstraint : RigConstraint<
    PistolWeaponAimJob, PistolWeaponAimData, PistolWeaponAimBinder>
{
}
