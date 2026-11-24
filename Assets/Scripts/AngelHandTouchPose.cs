using System.Collections.Generic;
using UnityEngine;

// An additive pose on the first detached hand; all positions below are world-space metres.
internal sealed class AngelHandTouchPose
{
    private readonly Transform arm, wrist;
    private readonly Transform[,] finger = new Transform[5, 4];
    private readonly Quaternion[,] animatedFinger = new Quaternion[5, 3];
    private readonly Vector3 palmInWrist, palmNormalInWrist;
    private readonly Quaternion forearmFrameInArm;
    private Vector3 animatedArmPosition;
    private Quaternion animatedArmRotation, animatedWristRotation;
    private bool posed;

    internal bool IsReady { get; }
    internal Vector3 PalmPosition => IsReady ? wrist.TransformPoint(palmInWrist) : Vector3.zero;
    internal Vector3 TipPosition => IsReady ? finger[2, 3].position : Vector3.zero;

    internal AngelHandTouchPose(Transform boss)
    {
        var bones = new Dictionary<string, Transform>();
        foreach (var bone in boss.GetComponentsInChildren<Transform>(true))
            if (bone.name.StartsWith("hand1:", System.StringComparison.Ordinal))
                bones[bone.name.Substring(6)] = bone;
        if (!bones.TryGetValue("arm_root", out arm) || !bones.TryGetValue("arm_wrist", out wrist)) return;
        string[] names = { "thumb", "index", "middle", "ring", "little" };
        for (int digit = 0; digit < names.Length; digit++)
        {
            // In this FBX index_1 through little_1 are metacarpals, and _2 are the knuckles.
            int first = digit == 0 ? 1 : 2;
            for (int link = 0; link < 4; link++)
                if (!bones.TryGetValue(names[digit] + "_" + (first + link), out finger[digit, link])) return;
        }

        Vector3 knuckles = (finger[1, 0].position + finger[4, 0].position) * .5f;
        Vector3 along = (finger[2, 0].position - wrist.position).normalized;
        Vector3 across = (finger[1, 0].position - finger[4, 0].position).normalized;
        Vector3 palmNormal = Vector3.Cross(across, along).normalized;
        // The thumb pad sits forward of the metacarpal plane, on the palm side.
        if (Vector3.Dot(palmNormal, finger[0, 1].position - wrist.position) < 0f) palmNormal = -palmNormal;
        // The FBX bones use different local axes. Calibrate both from the same palm plane.
        forearmFrameInArm = Quaternion.Inverse(arm.rotation) *
            Quaternion.LookRotation(wrist.position - arm.position, palmNormal);
        palmNormalInWrist = Quaternion.Inverse(wrist.rotation) * palmNormal;
        float palmLength = Vector3.Distance(wrist.position, knuckles);
        palmInWrist = wrist.InverseTransformPoint(Vector3.Lerp(wrist.position, knuckles, .63f) +
                                                  palmNormal * (.095f * palmLength));
        IsReady = true;
    }

    internal void Apply(Vector3 tipPoint, Vector3 toward, float influence, float wristRoll)
    {
        Restore();
        if (!IsReady || influence <= 0f) return;
        float weight = Mathf.Clamp01(influence);
        Vector3 goal = Vector3.Lerp(TipPosition, tipPoint, weight);
        animatedArmPosition = arm.localPosition;
        animatedArmRotation = arm.localRotation;
        animatedWristRotation = wrist.localRotation;
        for (int d = 0; d < 5; d++)
            for (int j = 0; j < 3; j++) animatedFinger[d, j] = finger[d, j].localRotation;

        // Float also animates the metacarpals. Take the visible palm axis from this frame.
        var idleFrame = Quaternion.LookRotation(finger[2, 0].position - wrist.position, wrist.rotation * palmNormalInWrist);
        var handFrameInWrist = Quaternion.Inverse(wrist.rotation) * idleFrame;
        Vector3 forward = toward.sqrMagnitude > .0001f ? toward.normalized : idleFrame * Vector3.forward;
        Vector3 palmNormal = Vector3.ProjectOnPlane(Vector3.down, forward);
        if (palmNormal.sqrMagnitude < .001f) palmNormal = Vector3.ProjectOnPlane(Vector3.forward, forward);
        var frame = Quaternion.AngleAxis(wristRoll, forward) * Quaternion.LookRotation(forward, palmNormal);
        arm.rotation = Quaternion.Slerp(arm.rotation, frame * Quaternion.Inverse(forearmFrameInArm), weight);
        wrist.rotation = Quaternion.Slerp(wrist.rotation, frame * Quaternion.Inverse(handFrameInWrist), weight);

        // The four long fingers extend together. The shorter thumb lies along the index finger.
        Vector3 thumbRest = Vector3.Lerp(finger[1, 0].position, finger[1, 1].position, .35f);
        Vector3 thumbDirection = (thumbRest - finger[0, 0].position).normalized;
        for (int d = 0; d < 5; d++)
        {
            for (int j = 0; j < 3; j++)
            {
                Vector3 heading = d == 0 ? Vector3.Slerp(thumbDirection, forward, .28f * j) : forward;
                Transform joint = finger[d, j];
                Vector3 segment = finger[d, j + 1].position - joint.position;
                var turn = Quaternion.FromToRotation(segment, heading);
                joint.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * joint.rotation;
            }
        }
        arm.position += goal - TipPosition;
        posed = true;
    }

    internal void Restore()
    {
        if (!posed) return;
        arm.SetLocalPositionAndRotation(animatedArmPosition, animatedArmRotation);
        wrist.localRotation = animatedWristRotation;
        for (int d = 0; d < 5; d++)
            for (int j = 0; j < 3; j++) finger[d, j].localRotation = animatedFinger[d, j];
        posed = false;
    }
}
