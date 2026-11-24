using System.Collections.Generic;
using UnityEngine;

// Offsets the evaluated FBX pose. The torus stays in its authored position behind the skull.
internal sealed class AngelHaloPose
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    private readonly Transform boss;
    private readonly Animator animator;
    private readonly Transform head;
    private readonly Transform chest;
    private readonly Transform[] arms = new Transform[3];
    private readonly Transform[] wrists = new Transform[3];
    private readonly Quaternion[] restArms = new Quaternion[3];
    private readonly List<Ring> rings = new List<Ring>(3);
    private readonly Light glow;
    private Quaternion restHead, restChest;
    private float restSpeed;
    private bool active;

    private sealed class Ring
    {
        internal readonly Renderer renderer;
        private readonly MaterialPropertyBlock original = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
        private Color baseColor, emission;

        internal Ring(Renderer renderer) => this.renderer = renderer;

        internal void Capture()
        {
            renderer.GetPropertyBlock(original);
            renderer.GetPropertyBlock(tint);
            Material mat = renderer.sharedMaterial;
            baseColor = original.HasColor(BaseColor) ? original.GetColor(BaseColor) : mat.GetColor(BaseColor);
            emission = original.HasColor(Emission) ? original.GetColor(Emission) : mat.GetColor(Emission);
        }

        internal void SetLight(Color color, float blend)
        {
            Color surface = Color.Lerp(baseColor, color, blend);
            surface.a = baseColor.a;
            tint.SetColor(BaseColor, surface);
            tint.SetColor(Emission, Color.Lerp(emission, color * 2f, blend));
            renderer.SetPropertyBlock(tint);
        }

        internal void Restore()
        {
            if (renderer != null) renderer.SetPropertyBlock(original.isEmpty ? null : original);
        }
    }

    internal AngelHaloPose(Transform boss)
    {
        this.boss = boss;
        animator = boss.GetComponent<Animator>();
        foreach (Transform bone in boss.GetComponentsInChildren<Transform>(true))
        {
            switch (bone.name)
            {
                case "skeleton:head": head = bone; break;
                case "skeleton:chest": chest = bone; break;
                case "halo_outer_ring":
                case "inner_torus":
                case "outter_torus":
                    if (bone.TryGetComponent<Renderer>(out var renderer) && renderer.sharedMaterial != null)
                        rings.Add(new Ring(renderer));
                    break;
            }
            for (int i = 0; i < arms.Length; i++)
            {
                string prefix = AngelSealAttack.HandPrefixes[i];
                if (bone.name == prefix + "arm_root") arms[i] = bone;
                if (bone.name == prefix + "arm_wrist") wrists[i] = bone;
            }
        }
        glow = new GameObject("Halo radiance").AddComponent<Light>();
        glow.transform.SetParent(boss, false);
        glow.type = LightType.Point;
        glow.range = 12f;
        glow.enabled = false;
    }

    internal Vector3 Begin()
    {
        Stop();
        active = true;
        if (animator != null)
        {
            restSpeed = animator.speed;
            animator.speed = 0f;
        }
        if (chest != null) restChest = chest.localRotation;
        if (head != null) restHead = head.localRotation;
        for (int i = 0; i < arms.Length; i++)
            if (arms[i] != null) restArms[i] = arms[i].localRotation;
        foreach (var ring in rings) ring.Capture();
        glow.transform.position = rings.Count > 0 ? rings[0].renderer.bounds.center : boss.position + Vector3.up * 2f;
        Charge(0f);
        return glow.transform.position;
    }

    internal void Charge(float progress)
    {
        if (!active) return;
        float t = Mathf.SmoothStep(0f, 1f, progress);
        Pose(t, 0f);
        Color gold = Color.Lerp(new Color(1.3f, .48f, .045f), new Color(4.2f, 3.8f, 2.65f), t);
        foreach (var ring in rings) ring.SetLight(gold, Mathf.Clamp01(.1f + 2f * t));
        glow.color = Color.Lerp(new Color(1f, .58f, .12f), new Color(1f, .92f, .73f), t);
        glow.intensity = 7f * t;
        glow.enabled = true;
    }

    internal void Recoil(float elapsedSinceRelease)
    {
        if (!active) return;
        const float duration = .55f;
        float t = Mathf.Clamp01(elapsedSinceRelease / duration);
        if (t >= 1f) { Stop(); return; }
        float release = 1f - Mathf.SmoothStep(0f, 1f, t);
        float kick = Mathf.SmoothStep(0f, 1f, t / .18f) * release;
        Pose(release, kick);
        foreach (var ring in rings) ring.SetLight(new Color(.16f, .07f, .022f), release);
        glow.enabled = false;
    }

    private void Pose(float spread, float kick)
    {
        RestorePose();
        if (chest != null) chest.rotation = Quaternion.AngleAxis(-8f * kick, boss.right) * chest.rotation;
        if (head != null) head.rotation = Quaternion.AngleAxis(-12f * spread, boss.right) * head.rotation;
        for (int i = 0; i < arms.Length; i++)
        {
            var arm = arms[i];
            if (arm == null || wrists[i] == null) continue;
            Vector3 outward = Vector3.ProjectOnPlane(wrists[i].position - boss.position, boss.up).normalized;
            Vector3 from = (wrists[i].position - arm.position).normalized;
            Vector3 to = Vector3.RotateTowards(from, outward + boss.up * .15f, 0.25f * spread, 0f);
            if (from.sqrMagnitude > .5f) arm.rotation = Quaternion.FromToRotation(from, to) * arm.rotation;
        }
    }

    internal void RestorePose()
    {
        if (!active) return;
        if (chest != null) chest.localRotation = restChest;
        if (head != null) head.localRotation = restHead;
        for (int i = 0; i < arms.Length; i++)
            if (arms[i] != null) arms[i].localRotation = restArms[i];
    }

    internal void Stop()
    {
        if (!active) return;
        RestorePose();
        foreach (var ring in rings) ring.Restore();
        if (glow != null) glow.enabled = false;
        if (animator != null) animator.speed = restSpeed;
        active = false;
    }
}
