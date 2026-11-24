using System.Collections.Generic;
using UnityEngine;

// Additive offsets on the authored floating skeleton, evaluated after the normal animation.
internal sealed class AngelDashPose
{
    private readonly Transform boss;
    private readonly Animator animator;
    private readonly Transform head, chest;
    private readonly Transform handRig;
    private Vector3 handRigPosition;
    private readonly Transform haloPivot;
    private Vector3 haloPosition;
    private Quaternion haloRotation;
    private Bend pelvis, neck;
    private readonly Bend[] thighs = new Bend[2], knees = new Bend[2], ankles = new Bend[2];
    private readonly Transform[] fingertips = new Transform[3];
    private readonly Transform[] arms = new Transform[3];
    private readonly Transform[] wrists = new Transform[3];
    private readonly Vector3[] armPositions = new Vector3[3];
    private readonly Quaternion[] armRotations = new Quaternion[3];
    private readonly Quaternion[] wristRotations = new Quaternion[3];
    private readonly LineRenderer stripe;
    private readonly List<Renderer> halo = new List<Renderer>(3);
    private readonly List<Color> haloBase = new List<Color>(3);
    private readonly List<Color> haloEmission = new List<Color>(3);
    private static readonly Color DashGold = new Color(1.25f, .78f, .22f);
    private static readonly Color DashEmission = new Color(2.5f, 1.4f, .22f);
    private readonly List<MaterialPropertyBlock> oldHalo = new List<MaterialPropertyBlock>(3);
    private readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    private static readonly int DashRise = Animator.StringToHash("Base Layer.Dash Rise");
    private static readonly int Hover = Animator.StringToHash("Base Layer.New State");
    // Wrist positions relative to the skull, in metres across, up and along the flight path.
    private static readonly Vector3[] FlyingWristOffsets =
    {
        new Vector3(-.69f, .28f, -.91f),
        new Vector3(.78f, .56f, -1.65f),
        new Vector3(-.45f, -.15f, -2.35f)
    };
    private Vector3 chestPosition, headPosition;
    private Quaternion chestRotation, headRotation;
    private float animationSpeed;
    private bool active, posed, playingRise;
    private Posture launch, returnFrom;
    private float aimYaw;

    // Each delta layers on the Animator's current pose and is removed before the next evaluation.
    private struct Bend
    {
        public Transform bone;
        private Quaternion animated;

        public void Apply(Quaternion rotation)
        {
            if (bone == null) return;
            animated = bone.localRotation;
            bone.rotation = rotation * bone.rotation;
        }

        public void Restore()
        {
            if (bone != null) bone.localRotation = animated;
        }

        public void GuideTowards(Transform child, Vector3 direction, float weight)
        {
            if (bone == null || child == null || weight <= 0f) return;
            Vector3 alongLimb = child.position - bone.position;
            if (alongLimb.sqrMagnitude < .0001f) return;
            var turn = Quaternion.FromToRotation(alongLimb, direction);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * bone.rotation;
        }
    }

    // Reach is signed for each of the three detached arms: trail the skull or brace ahead of it.
    private struct Posture
    {
        public float chestPitch, chestYaw, headPitch, headYaw, chestLead;
        public float chestRoll, headRoll, headLead;
        public float hipPitch, hipRoll;
        public Vector2 thighPitch, kneeFlex;
        public Vector3 reach, spread, wristPitch;

        public Posture Scale(float weight) => new Posture
        {
            chestPitch = chestPitch * weight, chestYaw = chestYaw * weight,
            headPitch = headPitch * weight, headYaw = headYaw * weight,
            chestLead = chestLead * weight, chestRoll = chestRoll * weight,
            headRoll = headRoll * weight, headLead = headLead * weight,
            hipPitch = hipPitch * weight, hipRoll = hipRoll * weight,
            thighPitch = thighPitch * weight, kneeFlex = kneeFlex * weight,
            reach = reach * weight, spread = spread * weight, wristPitch = wristPitch * weight
        };
    }

    private static float Ease(float start, float end, float time) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));

    internal Vector3 HeadPosition => head != null ? head.position : boss.position + boss.up;

    internal AngelDashPose(Transform boss, Material material)
    {
        this.boss = boss;
        animator = boss.GetComponent<Animator>();
        foreach (var bone in boss.GetComponentsInChildren<Transform>(true))
        {
            switch (bone.name)
            {
                case "Hands_Rig": handRig = bone; break;
                case "halo": haloPivot = bone; break;
                case "skeleton:root_pelvis": pelvis.bone = bone; break;
                case "skeleton:neck_base": neck.bone = bone; break;
                case "skeleton:leg_l": thighs[0].bone = bone; break;
                case "skeleton:leg_r": thighs[1].bone = bone; break;
                case "skeleton:knee_l": knees[0].bone = bone; break;
                case "skeleton:knee_r": knees[1].bone = bone; break;
                case "skeleton:foot_l": ankles[0].bone = bone; break;
                case "skeleton:foot_r": ankles[1].bone = bone; break;
                case "skeleton:head": head = bone; break;
                case "skeleton:chest": chest = bone; break;
                case "halo_outer_ring":
                case "inner_torus":
                case "outter_torus":
                    if (bone.TryGetComponent<Renderer>(out var renderer))
                    {
                        halo.Add(renderer);
                        oldHalo.Add(new MaterialPropertyBlock());
                        haloBase.Add(default);
                        haloEmission.Add(default);
                    }
                    break;
            }
            for (int i = 0; i < arms.Length; i++)
            {
                string prefix = AngelSealAttack.HandPrefixes[i];
                if (bone.name == prefix + "arm_root") arms[i] = bone;
                if (bone.name == prefix + "arm_wrist") wrists[i] = bone;
                if (bone.name == prefix + "middle_5") fingertips[i] = bone;
            }
        }

        stripe = new GameObject("Locked dash path").AddComponent<LineRenderer>();
        stripe.transform.SetParent(boss, false);
        stripe.sharedMaterial = material;
        stripe.useWorldSpace = true;
        stripe.alignment = LineAlignment.TransformZ;
        stripe.startWidth = .10f;
        stripe.endWidth = .035f;
        stripe.numCapVertices = 3;
        stripe.positionCount = 2;
        stripe.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        stripe.receiveShadows = false;
        tint.SetColor(BaseColor, new Color(3.3f, 2.8f, 1.6f));
        stripe.SetPropertyBlock(tint);
        stripe.enabled = false;
    }

    internal void Begin()
    {
        Stop();
        active = true;
        if (animator != null)
        {
            animationSpeed = animator.speed;
            animator.speed = animationSpeed * .42f;
        }
        for (int i = 0; i < halo.Count; i++)
        {
            halo[i].GetPropertyBlock(oldHalo[i]);
            var mat = halo[i].sharedMaterial;
            haloBase[i] = oldHalo[i].HasColor(BaseColor) ? oldHalo[i].GetColor(BaseColor) : mat.GetColor(BaseColor);
            haloEmission[i] = oldHalo[i].HasColor(Emission) ? oldHalo[i].GetColor(Emission) : mat.GetColor(Emission);
            halo[i].GetPropertyBlock(tint);
            tint.SetColor(BaseColor, DashGold);
            tint.SetColor(Emission, DashEmission);
            halo[i].SetPropertyBlock(tint);
        }
        launch = returnFrom = default;
    }

    internal void Aim(float progress, Vector3 direction)
    {
        Vector3 facing = Vector3.ProjectOnPlane(boss.forward, Vector3.up);
        aimYaw = Mathf.Clamp(Vector3.SignedAngle(facing, direction, Vector3.up), -48f, 48f);
        Windup(progress);
    }

    private void Windup(float time)
    {
        float skull = Ease(0f, .60f, time);
        float ribs = Ease(.16f, .93f, time);
        Apply(new Posture
        {
            hipPitch = -4f * ribs, hipRoll = 3f * ribs,
            thighPitch = new Vector2(-10f, -6f) * ribs,
            kneeFlex = new Vector2(17f, 11f) * ribs,
            chestPitch = 14f * ribs, chestYaw = aimYaw * .32f * ribs, chestLead = -.18f * ribs,
            headYaw = aimYaw * skull - aimYaw * .32f * ribs, headPitch = -8f * skull,
            reach = new Vector3(-Ease(.36f, 1.10f, time), -Ease(.45f, 1.22f, time),
                -Ease(.52f, 1.34f, time)) * .74f
        });
    }

    internal void Lock(Vector3 floorStart, Vector3 floorEnd, float progress)
    {
        // Tracking has stopped; the weight of each hand still carries it into the tuck.
        Windup(1f + .4f * progress);
        Vector3 lift = Vector3.up * .045f;
        stripe.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        stripe.SetPosition(0, floorStart + lift);
        stripe.SetPosition(1, Vector3.Lerp(floorStart, floorEnd, Mathf.SmoothStep(.06f, 1f, progress * 2f)) + lift);
        stripe.enabled = true;
    }

    internal void Fly(float launchProgress, float travelProgress)
    {
        stripe.enabled = false;
        float thrust = Ease(0f, .80f, launchProgress);
        float ribs = Ease(.20f, 1f, launchProgress);
        float glide = Ease(.10f, .65f, travelProgress) * thrust;
        float brace = Ease(.52f, .98f, travelProgress) * thrust;

        float kick = Ease(.05f, .55f, travelProgress);
        float release = Ease(.55f, .98f, travelProgress);
        // The skull stretches first, the ribs bank under it, then one hand sweeps
        // past the shoulder while the other two are still trailing the body.
        launch = new Posture
        {
            headLead = .22f * thrust - .13f * glide - .06f * brace,
            chestLead = -.18f - .17f * thrust + .46f * glide - .10f * brace,
            hipPitch = -4f + 10f * ribs - 5f * brace,
            hipRoll = 3f - 11f * kick + 12f * release,
            thighPitch = new Vector2(-10f + 24f * ribs - 11f * kick,
                                     -6f + 22f * kick - 10f * release),
            kneeFlex = new Vector2(17f + 13f * thrust - 23f * kick + 4f * release,
                                   11f + 12f * kick - 17f * release),
            chestPitch = 14f - 17f * ribs + 8f * glide - 5f * brace,
            headPitch = -8f - 6f * thrust + 9f * glide - 4f * brace,
            chestRoll = 11f * glide - 17f * brace,
            headRoll = -6f * glide + 10f * brace,
            headYaw = aimYaw * (1f - thrust) * .68f,
            chestYaw = aimYaw * (1f - ribs) * .32f + 14f * glide - 21f * brace,
            reach = new Vector3(-.74f - .30f * thrust + 1.85f * brace,
                                -.74f - .38f * ribs + .54f * brace,
                                -.74f - .47f * glide + .18f * brace),
            spread = new Vector3(.25f * glide + .18f * brace, .40f * glide - .24f * brace, -.14f * glide),
            wristPitch = new Vector3(-22f * thrust + 57f * brace, -32f * glide + 15f * brace, -24f * glide)
        };
        Apply(launch);
    }

    internal void Impact(float elapsed, bool blocked)
    {
        stripe.enabled = false;
        if (!blocked)
        {
            returnFrom = launch;
            Apply(returnFrom);
            return;
        }

        // Chest compresses behind the planted skull. The three hands catch, settle, then push off.
        float compress = Ease(0f, .15f, elapsed);
        float settle = Ease(.15f, .48f, elapsed);
        float push = Ease(1.5f, 2f, elapsed);
        var hit = launch;
        // The pelvis carries on behind the stopped ribs; the bent knees follow a beat later.
        float tuck = Ease(.04f, .22f, elapsed);
        float unfold = Ease(.22f, .62f, elapsed);
        hit.hipPitch += 9f * compress - 7f * settle;
        hit.hipRoll -= 5f * compress - 4f * settle;
        hit.thighPitch += new Vector2(10f, 7f) * tuck - new Vector2(8f, 6f) * unfold;
        hit.kneeFlex += new Vector2(19f, 25f) * tuck - new Vector2(15f, 19f) * unfold;
        hit.chestPitch += 9f * compress - 16f * settle - 14f * push;
        hit.chestLead += .17f * compress - .12f * settle - .21f * push;
        hit.headPitch += 13f * compress - 7f * settle - 4f * push;
        for (int i = 0; i < 3; i++)
        {
            float delay = .035f + i * .035f;
            float catchUp = Ease(delay, delay + .23f, elapsed);
            hit.reach[i] = Mathf.Lerp(launch.reach[i], .72f, catchUp) + .22f * push;
            hit.wristPitch[i] = Mathf.Lerp(launch.wristPitch[i], 25f, catchUp);
        }
        Apply(hit);
        returnFrom = hit;
    }

    internal void BeginRise()
    {
        if (animator == null) return;
        animator.speed = animationSpeed;
        if (!animator.isActiveAndEnabled || !animator.HasState(0, DashRise)) return;
        // The start of Summon's opening gesture blends into the braced flight pose.
        animator.CrossFadeInFixedTime(DashRise, .42f, 0, .70f);
        playingRise = true;
    }

    private void ResumeHover()
    {
        if (!playingRise) return;
        playingRise = false;
        if (animator != null && animator.isActiveAndEnabled && animator.HasState(0, Hover))
        {
            // The first open pose in Float matches the spread of Summon's wrists.
            animator.CrossFadeInFixedTime(Hover, .54f, 0, .45f);
        }
    }

    internal void Return(float progress)
    {
        if (progress >= .65f) ResumeHover();
        var returning = returnFrom.Scale(1f - Ease(.02f, .66f, progress));
        // The head finds its upright bearing first. The hips and knees reverse the swing
        // at different times; the detached wrists carry through before opening around the ribs.
        float headWeight = 1f - Ease(0f, .56f, progress);
        returning.headPitch = returnFrom.headPitch * headWeight;
        returning.headRoll = returnFrom.headRoll * headWeight;
        float gather = Mathf.Sin(Mathf.PI * Ease(0f, .75f, progress));
        // The ribs open first. The pelvis follows the ribs and the knees uncurl last.
        returning.chestPitch -= 11f * gather;
        returning.headPitch -= 4f * gather;
        float trail = Mathf.Sin(Mathf.PI * Ease(.10f, 1f, progress));
        returning.hipPitch += 6f * trail;
        returning.kneeFlex += new Vector2(14f, 21f) * trail;
        returning.thighPitch += new Vector2(-9f, -5f) * gather;
        float hipSwing = Mathf.Sin(Mathf.PI * Ease(.15f, .95f, progress));
        returning.hipRoll += 7f * hipSwing;
        for (int i = 0; i < arms.Length; i++)
        {
            float release = Ease(.08f + .08f * i, .73f + .12f * i, progress);
            float fan = Mathf.Sin(Mathf.PI * release);
            returning.reach[i] = returnFrom.reach[i] * (1f - release);
            returning.spread[i] = returnFrom.spread[i] * (1f - release) + .22f * fan;
            returning.wristPitch[i] = returnFrom.wristPitch[i] * (1f - release) - (10f + 4f * i) * fan;
        }
        float rest = Ease(.30f, .96f, progress);
        for (int i = 0; i < halo.Count; i++)
        {
            halo[i].GetPropertyBlock(tint);
            tint.SetColor(BaseColor, Color.Lerp(DashGold, haloBase[i], rest));
            tint.SetColor(Emission, Color.Lerp(DashEmission, haloEmission[i], rest));
            halo[i].SetPropertyBlock(tint);
        }
        Apply(returning);
    }

    private void Apply(Posture frame)
    {
        if (!active) return;
        RestorePose();
        Vector3 heading = Vector3.Cross(boss.right, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, heading);
        float bodyPitch = Vector3.SignedAngle(Vector3.up, Vector3.ProjectOnPlane(boss.up, right), right);
        float stream = Ease(35f, 84f, bodyPitch);
        // The fan trails the leading skull so its front fingertips also clear the pillar.
        if (handRig != null)
        {
            handRigPosition = handRig.localPosition;
            handRig.position -= heading * (.26f * stream);
        }
        float hipPitch = frame.hipPitch;
        if (head != null && pelvis.bone != null)
        {
            // The idle clip can already be leaning into the dive when the attack starts.
            Vector3 spine = Vector3.ProjectOnPlane(head.position - pelvis.bone.position, right);
            float lean = Vector3.SignedAngle(boss.up, spine, right);
            hipPitch -= Mathf.Clamp(lean, -35f, 35f) * stream;
        }
        // Bending the hips and knees at different times makes the feet follow the launch.
        pelvis.Apply(Quaternion.AngleAxis(frame.hipRoll, heading) * Quaternion.AngleAxis(hipPitch, right));
        for (int i = 0; i < thighs.Length; i++)
        {
            float side = i == 0 ? -.17f : .17f;
            Vector3 trail = -heading + right * side;
            thighs[i].Apply(Quaternion.AngleAxis(frame.thighPitch[i], right));
            thighs[i].GuideTowards(knees[i].bone, trail, stream * .48f);
            knees[i].Apply(Quaternion.AngleAxis(frame.kneeFlex[i], right));
            Vector3 toeLine = trail + Vector3.up * (.10f + frame.kneeFlex[i] * .014f);
            knees[i].GuideTowards(ankles[i].bone, toeLine, stream * .46f);
            ankles[i].Apply(Quaternion.AngleAxis(-frame.kneeFlex[i] * .38f, right));
        }

        // Share the counter-rotation over the neck and skull so the gaze stays out in front.
        if (chest != null)
        {
            chestPosition = chest.localPosition;
            chestRotation = chest.localRotation;
            chest.position += heading * frame.chestLead;
            chest.rotation = Quaternion.AngleAxis(frame.chestRoll, heading) *
                Quaternion.AngleAxis(frame.chestYaw, Vector3.up) *
                Quaternion.AngleAxis(frame.chestPitch, boss.right) * chest.rotation;
        }
        neck.Apply(Quaternion.AngleAxis(-bodyPitch * .31f, right));
        if (head != null)
        {
            headPosition = head.localPosition;
            headRotation = head.localRotation;
            head.position += heading * frame.headLead;
            head.rotation = Quaternion.AngleAxis(frame.headRoll, heading) *
                Quaternion.AngleAxis(frame.headYaw, Vector3.up) *
                Quaternion.AngleAxis(frame.headPitch - bodyPitch * .37f, right) * head.rotation;
        }
        PoseHalo(heading, right, bodyPitch);
        for (int i = 0; i < arms.Length; i++)
        {
            var arm = arms[i];
            if (arm == null) continue;
            armPositions[i] = arm.localPosition;
            armRotations[i] = arm.localRotation;
            Transform wrist = wrists[i];
            if (wrist == null) continue;
            wristRotations[i] = wrist.localRotation;
            float reach = frame.reach[i];
            Vector3 along = reach < 0f ? -heading : heading;
            Vector3 from = (wrists[i].position - arm.position).normalized;
            if (from.sqrMagnitude < .5f) continue;
            Vector3 to = Vector3.RotateTowards(from, along, .75f * Mathf.Abs(reach), 0f);
            arm.rotation = Quaternion.FromToRotation(from, to) * arm.rotation;
            Vector3 radial = Vector3.ProjectOnPlane(arm.position - boss.position, heading).normalized;
            arm.position += heading * (.48f * reach) + radial * frame.spread[i];
            wrist.rotation = Quaternion.AngleAxis(frame.wristPitch[i], right) * wrist.rotation;
            if (fingertips[i] != null && head != null)
            {
                // The detached arms fan out around the ribs as the body levels.
                // The leading wrist travels from the hip to the cheek before contact.
                float align = Ease(26f, 86f, bodyPitch);
                var offset = FlyingWristOffsets[i];
                offset.z += (i == 0 ? .82f : .30f) * reach;
                Vector3 wristTarget = head.position + heading * offset.z + right * offset.x + Vector3.up * offset.y;
                arm.position += (wristTarget - wrist.position) * align;
                // Keep a trailing hand inside the skull's low flight lane as it drops.
                float sag = head.position.y - 1.00f - Mathf.Min(wrist.position.y, fingertips[i].position.y);
                arm.position += Vector3.up * (Mathf.Max(0f, sag) * Ease(25f, 65f, bodyPitch));
            }
        }
        posed = true;
    }

    private void PoseHalo(Vector3 heading, Vector3 right, float pitch)
    {
        if (haloPivot == null) return;
        haloPosition = haloPivot.localPosition;
        haloRotation = haloPivot.localRotation;
        if (halo.Count == 0 || head == null) return;
        // Its FBX pivot is at the feet. Swing the spinning rings about their visible centre.
        var ring = halo[0];
        Vector3 center = ring.transform.TransformPoint(ring.localBounds.center);
        float tuck = Ease(12f, 72f, pitch);
        Vector3 overSkull = head.position + Vector3.up * .65f - heading * .23f;
        haloPivot.rotation = Quaternion.AngleAxis(-.84f * pitch * tuck, right) * haloPivot.rotation;
        haloPivot.position += Vector3.Lerp(center, overSkull, tuck) -
            ring.transform.TransformPoint(ring.localBounds.center);
    }

    internal void RestorePose()
    {
        if (!posed) return;
        if (handRig != null) handRig.localPosition = handRigPosition;
        if (haloPivot != null) haloPivot.SetLocalPositionAndRotation(haloPosition, haloRotation);
        pelvis.Restore();
        neck.Restore();
        for (int i = 0; i < thighs.Length; i++)
        {
            thighs[i].Restore();
            knees[i].Restore();
            ankles[i].Restore();
        }
        if (chest != null) chest.SetLocalPositionAndRotation(chestPosition, chestRotation);
        if (head != null) head.SetLocalPositionAndRotation(headPosition, headRotation);
        for (int i = 0; i < arms.Length; i++)
        {
            if (arms[i] == null) continue;
            arms[i].SetLocalPositionAndRotation(armPositions[i], armRotations[i]);
            if (wrists[i] != null) wrists[i].localRotation = wristRotations[i];
        }
        posed = false;
    }

    internal void Stop()
    {
        if (stripe != null) stripe.enabled = false;
        if (!active) return;
        RestorePose();
        ResumeHover();
        if (animator != null) animator.speed = animationSpeed;
        for (int i = 0; i < halo.Count; i++)
            if (halo[i] != null) halo[i].SetPropertyBlock(oldHalo[i].isEmpty ? null : oldHalo[i]);
        active = false;
    }
}
