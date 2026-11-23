using UnityEngine;

[RequireComponent(typeof(CombatHealth), typeof(Animator))]
[DisallowMultipleComponent]
public sealed class SwatHitZones : MonoBehaviour
{
    private CombatHealth health;
    private SphereCollider head;
    private CapsuleCollider torso;

    private void Awake()
    {
        health = GetComponent<CombatHealth>();
        var animator = GetComponent<Animator>();
        var skull = animator.GetBoneTransform(HumanBodyBones.Head);
        var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Vector3 up = (neck.position - hips.position).normalized;
        var headZone = BoneZone("Head hit zone", skull, skull.position + up * 0.075f, Quaternion.identity);
        head = headZone.AddComponent<SphereCollider>();
        head.radius = 0.135f;
        head.isTrigger = true;

        Vector3 upper = neck.position - up * 0.04f;
        Vector3 lower = hips.position - up * 0.09f;
        var bodyZone = BoneZone("Torso hit zone", chest, (upper + lower) * 0.5f,
            Quaternion.FromToRotation(Vector3.up, up));
        torso = bodyZone.AddComponent<CapsuleCollider>();
        torso.radius = 0.195f;
        torso.height = Vector3.Distance(lower, upper);
        torso.isTrigger = true;
        health.onDeath.AddListener(DisableZones);
    }

    private static GameObject BoneZone(string zoneName, Transform bone, Vector3 position, Quaternion orientation)
    {
        var zone = new GameObject(zoneName);
        zone.layer = LayerMask.NameToLayer("Ignore Raycast");
        zone.transform.SetPositionAndRotation(position, orientation);
        zone.transform.SetParent(bone, true);
        return zone;
    }

    public bool Contains(Collider part) => part == torso || part == head;

    public void ApplyShot(RaycastHit hit, Vector3 direction, GameObject shooter)
    {
        if (!Contains(hit.collider) || health.IsDead) return;
        float third = health.MaximumHP / 3f;
        float damage = hit.collider == head || Mathf.Approximately(health.HP, third) ? health.HP : third;
        health.TakePistolShot(damage, hit.point, direction, shooter);
    }

    private void DisableZones()
    {
        head.enabled = false;
        torso.enabled = false;
    }

    private void OnDestroy() => health.onDeath.RemoveListener(DisableZones);
}
