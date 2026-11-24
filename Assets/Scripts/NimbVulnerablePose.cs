using System;
using System.Collections.Generic;
using UnityEngine;

// Pose the existing skeleton and the two overlapping groups from Summon's first frame.
internal sealed class NimbVulnerablePose
{
    private readonly Transform actor;
    private readonly Transform chest;
    private readonly Transform leftArmStub, rightArmStub;
    private readonly Joint neck, midNeck, head;
    private readonly Leg leftLeg, rightLeg;
    private readonly Joint[] skeleton;
    private readonly Arm[] arms;
    private readonly Arm firstArm, mirroredArm;
    private readonly bool hasSummonPose;

    internal NimbVulnerablePose(Transform actor)
    {
        this.actor = actor;
        var nodes = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (Transform node in actor.GetComponentsInChildren<Transform>(true))
            nodes[node.name] = node;

        Transform Find(string name) => nodes.TryGetValue(name, out Transform node) ? node : null;
        chest = Find("skeleton:chest");
        leftArmStub = Find("skeleton:arm_l");
        rightArmStub = Find("skeleton:arm_r");
        neck = new Joint(Find("skeleton:neck_base"));
        midNeck = new Joint(Find("skeleton:nack_mid"));
        head = new Joint(Find("skeleton:head"));
        leftLeg = new Leg(Find("skeleton:leg_l"), Find("skeleton:knee_l"), Find("skeleton:foot_l"), -1f);
        rightLeg = new Leg(Find("skeleton:leg_r"), Find("skeleton:knee_r"), Find("skeleton:foot_r"), 1f);
        skeleton = new[] { neck, midNeck, head, leftLeg.Thigh, rightLeg.Thigh, leftLeg.Knee,
            rightLeg.Knee, leftLeg.Foot, rightLeg.Foot };

        var detached = new List<Arm>();
        const string meshSuffix = "arm_mesh";
        foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!renderer.name.EndsWith(":arm_mesh", StringComparison.Ordinal)) continue;

            string prefix = renderer.name.Substring(0, renderer.name.Length - meshSuffix.Length);
            Transform root = Find(prefix + "arm_root");
            if (root == null) continue;

            int index = 0;
            if (prefix != "hand:" && (!prefix.StartsWith("hand", StringComparison.Ordinal) ||
                !int.TryParse(prefix.Substring(4, prefix.Length - 5), out index))) continue;
            var arm = new Arm(renderer, root, index);
            detached.Add(arm);
            if (index == 0) firstArm = arm;
            if (index == 5) mirroredArm = arm;
        }
        arms = detached.ToArray();
        hasSummonPose = SampleSummonPose();
        if (hasSummonPose)
            foreach (var arm in arms)
                if (arm != firstArm && arm != mirroredArm)
                    arm.SetMergeLeader(arm.Index < 5 ? firstArm : mirroredArm);
    }

    internal void CaptureInterrupted()
    {
        foreach (Joint joint in skeleton) joint.CaptureInterrupted();
        foreach (Arm arm in arms) arm.CaptureInterrupted();
    }

    internal void Begin()
    {
        foreach (Joint joint in skeleton) joint.CaptureHover();
        foreach (Arm arm in arms) arm.CaptureHover();
    }

    internal void End()
    {
        foreach (Joint joint in skeleton) joint.Reset(0f);
        foreach (Arm arm in arms) arm.Restore();
    }

    internal void Apply(float vulnerability, float interruptedWeight)
    {
        foreach (Joint joint in skeleton) joint.Reset(interruptedWeight);
        foreach (Arm arm in arms) arm.Reset(interruptedWeight);

        float droop = Ease(.06f, .86f, vulnerability);
        float nod = Ease(.18f, .92f, vulnerability);
        float neutralHeadHeight = head.Node != null ? head.Node.position.y : 0f;
        neck.Turn(Quaternion.AngleAxis(29f * droop, actor.right));
        midNeck.Turn(Quaternion.AngleAxis(15f * nod, actor.right));
        head.Turn(Quaternion.AngleAxis(12f * nod, actor.right) *
            Quaternion.AngleAxis(4f * nod, actor.forward));
        if (head.Node != null)
        {
            Vector3 lowered = head.Node.position;
            lowered.y = Mathf.Min(lowered.y, neutralHeadHeight - .19f * nod);
            head.Node.position = lowered;
        }

        if (chest != null)
        {
            leftLeg.Fold(chest.position, actor, Ease(.08f, .95f, vulnerability));
            rightLeg.Fold(chest.position, actor, Ease(.15f, 1f, vulnerability));
        }

        if (!hasSummonPose) return;
        float lead = Ease(.05f, .78f, vulnerability);
        firstArm.ApplySummon(chest, actor, lead);
        mirroredArm.ApplySummon(chest, actor, lead);
        foreach (Arm arm in arms)
        {
            if (arm == firstArm || arm == mirroredArm) continue;
            float trail = .025f * (arm.Index % 5);
            float merge = Ease(.11f + trail, .89f + trail, vulnerability);
            arm.Merge(merge, actor.up);
        }
    }

    internal float GroundLift(float floor)
    {
        return Mathf.Max(0f, Mathf.Max(leftLeg.GroundLift(floor), rightLeg.GroundLift(floor)));
    }

    private bool SampleSummonPose()
    {
        if (firstArm == null || mirroredArm == null || chest == null ||
            !actor.TryGetComponent(out Animator animator) || animator.runtimeAnimatorController == null)
            return false;

        AnimationClip summon = Array.Find(animator.runtimeAnimatorController.animationClips,
            clip => clip != null && clip.name == "Summon");
        if (summon == null) return false;

        // Only clone transform paths needed by the two representatives and their
        // chest anchor. Sampling never alters the live Animator or attack pose.
        var sample = new GameObject("Nimb Summon pose sample") { hideFlags = HideFlags.HideAndDontSave };
        sample.transform.SetParent(actor.parent, false);
        LocalPose.Read(actor).Apply(sample.transform);
        var copies = new Dictionary<Transform, Transform> { [actor] = sample.transform };
        Transform Copy(Transform source)
        {
            if (copies.TryGetValue(source, out Transform known)) return known;
            var copy = new GameObject(source.name).transform;
            copy.SetParent(Copy(source.parent), false);
            LocalPose.Read(source).Apply(copy);
            copies.Add(source, copy);
            return copy;
        }

        Transform sampleChest = Copy(chest);
        firstArm.CopyHierarchy(Copy);
        mirroredArm.CopyHierarchy(Copy);
        Transform sampleLeft = leftArmStub != null ? Copy(leftArmStub) : null;
        Transform sampleRight = rightArmStub != null ? Copy(rightArmStub) : null;
        summon.SampleAnimation(sample, 0f);
        firstArm.ReadSummon(sampleChest, copies);
        mirroredArm.ReadSummon(sampleChest, copies);
        if (sampleLeft != null && sampleRight != null)
        {
            Vector3 handSpan = mirroredArm.SamplePosition(copies) - firstArm.SamplePosition(copies);
            Vector3 stubSpan = sampleRight.position - sampleLeft.position;
            bool firstIsLeft = Vector3.Dot(Vector3.ProjectOnPlane(handSpan, actor.up),
                Vector3.ProjectOnPlane(stubSpan, actor.up)) >= 0f;
            firstArm.SetAttachment(firstIsLeft ? leftArmStub : rightArmStub);
            mirroredArm.SetAttachment(firstIsLeft ? rightArmStub : leftArmStub);
        }
        UnityEngine.Object.Destroy(sample);
        return true;
    }

    internal static float Ease(float start, float end, float value) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));

    private sealed class Arm
    {
        private readonly Renderer skin;
        private readonly Joint root;
        private readonly Joint[] fingers;
        private readonly Joint[] mergeFingers;
        private Arm leader;
        private Transform attachment;
        private bool originallyHidden;
        private Vector3 summonPosition, summonScale;
        private Quaternion summonRotation;

        internal int Index { get; }

        internal Arm(Renderer skin, Transform armRoot, int index)
        {
            this.skin = skin;
            Index = index;
            root = new Joint(armRoot);
            Transform[] nodes = armRoot.GetComponentsInChildren<Transform>(true);
            fingers = new Joint[nodes.Length - 1];
            mergeFingers = new Joint[fingers.Length];
            for (int i = 1; i < nodes.Length; i++)
                fingers[i - 1] = new Joint(nodes[i]);
        }

        internal void CopyHierarchy(Func<Transform, Transform> copy)
        {
            copy(root.Node);
            foreach (Joint finger in fingers) copy(finger.Node);
        }

        internal void ReadSummon(Transform sampleChest, Dictionary<Transform, Transform> copies)
        {
            Transform sampleRoot = copies[root.Node];
            summonPosition = sampleChest.InverseTransformPoint(sampleRoot.position);
            summonRotation = Quaternion.Inverse(sampleChest.rotation) * sampleRoot.rotation;
            summonScale = sampleRoot.localScale;
            foreach (Joint finger in fingers) finger.ReadSummon(copies[finger.Node]);
        }

        internal Vector3 SamplePosition(Dictionary<Transform, Transform> copies) => copies[root.Node].position;

        internal void SetAttachment(Transform stub) => attachment = stub;

        internal void SetMergeLeader(Arm representative)
        {
            leader = representative;
            for (int i = 0; i < fingers.Length; i++)
            {
                string boneName = BoneName(fingers[i].Node);
                foreach (Joint finger in representative.fingers)
                {
                    if (BoneName(finger.Node) != boneName) continue;
                    mergeFingers[i] = finger;
                    break;
                }
            }
        }

        internal void CaptureInterrupted()
        {
            root.CaptureInterrupted();
            foreach (Joint finger in fingers) finger.CaptureInterrupted();
        }

        internal void CaptureHover()
        {
            originallyHidden = skin.forceRenderingOff;
            root.CaptureHover();
            foreach (Joint finger in fingers) finger.CaptureHover();
        }

        internal void Reset(float interruptedWeight)
        {
            root.Reset(interruptedWeight);
            foreach (Joint finger in fingers) finger.Reset(interruptedWeight);
        }

        internal void Restore()
        {
            Reset(0f);
            skin.forceRenderingOff = originallyHidden;
        }

        internal void ApplySummon(Transform chest, Transform actor, float weight)
        {
            skin.forceRenderingOff = originallyHidden;
            Transform node = root.Node;
            Vector3 target = chest.TransformPoint(summonPosition);
            if (attachment != null)
            {
                // Summon's hands trail from the hips. The low pose places the
                // same wrists and fingers beside the skeleton's upper-arm stubs.
                Vector3 outward = actor.right * Mathf.Sign(Vector3.Dot(attachment.position - chest.position, actor.right));
                target = attachment.position + outward * .075f + actor.forward * .09f - actor.up * .075f;
            }
            node.SetPositionAndRotation(Vector3.Lerp(node.position, target, weight),
                Quaternion.Slerp(node.rotation, chest.rotation * summonRotation, weight));
            node.localScale = Vector3.Lerp(node.localScale, summonScale, weight);
            foreach (Joint finger in fingers) finger.ApplySummon(weight);
        }

        internal void Merge(float weight, Vector3 up)
        {
            Transform node = root.Node;
            Transform target = leader.root.Node;
            // A small arc separates the trajectories until all of a side's joints coincide.
            float arc = Mathf.Sin(Mathf.PI * weight) * (.07f + .025f * (Index % 5));
            Vector3 merged = Vector3.Lerp(node.position, target.position, weight);
            node.SetPositionAndRotation(merged + up * arc,
                Quaternion.Slerp(node.rotation, target.rotation, weight));
            node.localScale = Vector3.Lerp(node.localScale, target.localScale, weight);
            for (int i = 0; i < fingers.Length; i++)
                if (mergeFingers[i] != null) fingers[i].Merge(mergeFingers[i], weight);
            skin.forceRenderingOff = originallyHidden || weight >= 1f;
        }

        private static string BoneName(Transform node)
        {
            int separator = node.name.IndexOf(':');
            return node.name.Substring(separator + 1);
        }
    }

    private sealed class Leg
    {
        private readonly float side;

        internal Joint Thigh { get; }
        internal Joint Knee { get; }
        internal Joint Foot { get; }

        internal Leg(Transform thigh, Transform knee, Transform foot, float side)
        {
            Thigh = new Joint(thigh);
            Knee = new Joint(knee);
            Foot = new Joint(foot);
            this.side = side;
        }

        internal void Fold(Vector3 chest, Transform actor, float weight)
        {
            Transform thigh = Thigh.Node;
            Transform knee = Knee.Node;
            Transform foot = Foot.Node;
            if (thigh == null || knee == null || foot == null || weight <= 0f) return;

            Vector3 hip = thigh.position;
            float upper = Vector3.Distance(hip, knee.position);
            float lower = Vector3.Distance(knee.position, foot.position);
            if (upper < .01f || lower < .01f) return;

            Vector3 footAtRest = chest - Vector3.up * (side < 0f ? 1.55f : 1.58f) +
                actor.right * (.22f * side) - actor.forward * (side < 0f ? .43f : .32f);
            Vector3 ankle = footAtRest - hip;
            if (ankle.sqrMagnitude < .0001f) return;

            Vector3 along = ankle.normalized;
            float length = Mathf.Clamp(ankle.magnitude, Mathf.Abs(upper - lower) + .005f, upper + lower - .005f);
            float reach = (upper * upper - lower * lower + length * length) / (2f * length);
            float height = Mathf.Sqrt(Mathf.Max(0f, upper * upper - reach * reach));
            Vector3 bend = Vector3.ProjectOnPlane(actor.forward + actor.right * (.15f * side), along).normalized;
            Vector3 kneeAtRest = hip + along * reach + bend * height;
            Quaternion footRotation = foot.rotation;
            Thigh.Guide(Knee, kneeAtRest - hip, weight);
            Knee.Guide(Foot, hip + along * length - knee.position, weight);

            // Keep the feet hanging under the folded shins in the authored boot orientation.
            Quaternion settled = Quaternion.AngleAxis((side < 0f ? 9f : 5f) * weight, actor.right) * footRotation;
            foot.rotation = Quaternion.Slerp(foot.rotation, settled, weight);
        }

        internal float GroundLift(float floor) =>
            Mathf.Max(Knee.Clearance(floor, .13f), Foot.Clearance(floor, .05f));
    }

    private sealed class Joint
    {
        private LocalPose hover, interrupted, summon;

        internal Transform Node { get; }

        internal Joint(Transform node) => Node = node;

        internal void CaptureInterrupted()
        {
            if (Node != null) interrupted = LocalPose.Read(Node);
        }

        internal void CaptureHover()
        {
            if (Node != null) hover = LocalPose.Read(Node);
        }

        internal void Reset(float blend)
        {
            if (Node != null) LocalPose.Blend(hover, interrupted, blend).Apply(Node);
        }

        internal void ReadSummon(Transform sample) => summon = LocalPose.Read(sample);

        internal void ApplySummon(float weight) =>
            LocalPose.Blend(LocalPose.Read(Node), summon, weight).Apply(Node);

        internal void Merge(Joint target, float weight) =>
            LocalPose.Blend(LocalPose.Read(Node), LocalPose.Read(target.Node), weight).Apply(Node);

        internal void Turn(Quaternion rotation)
        {
            if (Node != null) Node.rotation = rotation * Node.rotation;
        }

        internal void Guide(Joint child, Vector3 direction, float weight)
        {
            Vector3 along = child.Node.position - Node.position;
            if (along.sqrMagnitude < .0001f || direction.sqrMagnitude < .0001f) return;
            Turn(Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(along, direction), weight));
        }

        internal float Clearance(float floor, float margin) =>
            Node != null ? floor + margin - Node.position.y : 0f;
    }

    private readonly struct LocalPose
    {
        private readonly Vector3 position, scale;
        private readonly Quaternion rotation;

        private LocalPose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }

        internal static LocalPose Read(Transform node) =>
            new LocalPose(node.localPosition, node.localRotation, node.localScale);

        internal static LocalPose Blend(LocalPose from, LocalPose to, float weight) =>
            new LocalPose(Vector3.Lerp(from.position, to.position, weight),
                Quaternion.Slerp(from.rotation, to.rotation, weight), Vector3.Lerp(from.scale, to.scale, weight));

        internal void Apply(Transform node)
        {
            node.SetLocalPositionAndRotation(position, rotation);
            node.localScale = scale;
        }
    }
}
