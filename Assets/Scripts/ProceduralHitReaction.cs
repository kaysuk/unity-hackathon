using UnityEngine;

// Apply after animation/rig evaluation, so the complete upper body recoils together.
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class ProceduralHitReaction : MonoBehaviour
{
    [SerializeField] private Transform chest;
    [SerializeField, Range(0f, 20f)] private float backwardDegrees = 7f;
    [SerializeField, Range(0f, 20f)] private float leftDegrees = 5f;
    [SerializeField, Min(0.01f)] private float duration = 0.22f;
    private float remaining;
    private Quaternion original;
    private Quaternion applied;
    private bool hasOffset;

    private void Awake()
    {
        var animator = GetComponent<Animator>();
        if (chest == null && animator != null && animator.isHuman)
            chest = animator.GetBoneTransform(HumanBodyBones.Chest) ??
                animator.GetBoneTransform(HumanBodyBones.Spine);
    }

    public void Kick() => remaining = Mathf.Max(0.01f, duration);
    public void StopReaction() { remaining = 0f; Restore(); }

    private void Restore()
    {
        // Do not overwrite a fresh pose from another system.
        if (hasOffset && chest != null && Quaternion.Angle(chest.localRotation, applied) < 0.01f)
            chest.localRotation = original;
        hasOffset = false;
    }

    private void Update() => Restore();

    private void LateUpdate()
    {
        if (chest == null || remaining <= 0f) return;
        float strength = Mathf.Clamp01(remaining / Mathf.Max(0.01f, duration));
        strength *= strength;
        original = chest.localRotation;
        Quaternion kick = Quaternion.AngleAxis(-backwardDegrees * strength, transform.right) *
            Quaternion.AngleAxis(leftDegrees * strength, transform.forward);
        chest.rotation = kick * chest.rotation;
        applied = chest.localRotation;
        hasOffset = true;
        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
    }

    private void OnDisable() => StopReaction();
}
