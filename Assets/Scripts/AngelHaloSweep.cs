using System;
using UnityEngine;

[Serializable]
public sealed class AngelHaloSweep
{
    [SerializeField, Min(0.5f)] private float windup = .9f;
    [SerializeField, Min(1f)] private float speed = 14f;
    [SerializeField, Min(1f)] private float reach = 40f;
    [SerializeField, Min(1f)] private float height = 9f;
    [SerializeField, Min(0f)] private float damage = 35f;
    [SerializeField] private Material waveMaterial;
    [SerializeField] private Collider[] columns;

    internal Collider[] Columns => columns;

    // The shader uses the same six horizontal column silhouettes as the hit check.
    private readonly Vector4[] pillars = new Vector4[6];
    private Transform boss;
    private NimbEncounter encounter;
    private CharacterController capsule;
    private CombatHealth victim;
    private AngelHaloPose pose;
    private AngelHaloWaveView view;
    private Vector3 origin;
    private Vector3 previousPosition, previousFeet;
    private float previousRadius;
    private bool passedPlayer;
    private bool released;

    public void Initialize(Transform angel, Transform target, NimbEncounter encounter = null)
    {
        boss = angel;
        this.encounter = encounter;
        SetTarget(target);
        pose = new AngelHaloPose(angel);
        view = new AngelHaloWaveView(angel, waveMaterial);
        view.SetChambers(encounter);
    }

    public void SetTarget(Transform target)
    {
        capsule = target.GetComponent<CharacterController>();
        victim = target.GetComponent<CombatHealth>();
    }

    public void Begin()
    {
        origin = boss.position;
        if (Physics.Raycast(origin + Vector3.up * 0.2f, Vector3.down, out var floor, 8f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            origin.y = floor.point.y;

        CaptureColumnShadows();

        passedPlayer = false;
        released = false;
        previousRadius = 0f;
        previousPosition = capsule.bounds.center;
        previousFeet = capsule.transform.position;
        Vector3 haloCenter = pose.Begin();
        GameAudio.Play(SoundCue.HaloGrip, haloCenter, 0.72f, 5f);
        view.Begin(origin, reach, height, pillars);
    }

    public void RestorePose() => pose?.RestorePose();

    public bool Tick(float elapsed)
    {
        if (elapsed < windup)
        {
            float progress = Mathf.Clamp01(elapsed / windup);
            pose.Charge(progress);
            view.Charge(progress);
            previousPosition = capsule.bounds.center;
            previousFeet = capsule.transform.position;
            return false;
        }

        if (!released)
        {
            released = true;
            GameAudio.Play(SoundCue.HaloWave, origin, 0.85f, 6f);
        }
        float travelTime = elapsed - windup;
        pose.Recoil(travelTime);
        float radius = Mathf.Min(reach, travelTime * speed);
        view.Release(radius);

        Vector3 position = capsule.bounds.center;
        Vector3 feet = capsule.transform.position;
        float gap = HorizontalDistance(position) - radius;
        if (!passedPlayer && gap <= 0f)
        {
            passedPlayer = true;
            // Resolve at the crossing, even when a long frame spans the edge of a shadow.
            float lastGap = HorizontalDistance(previousPosition) - previousRadius;
            float t = lastGap > 0f ? lastGap / (lastGap - gap) : 0f;
            Vector3 contact = Vector3.Lerp(previousPosition, position, t);
            Vector3 feetAtContact = Vector3.Lerp(previousFeet, feet, t);
            if (contact.y >= origin.y && contact.y <= origin.y + height && !IsBehindColumn(origin, contact)
                && (encounter == null || !encounter.ContainsChamberPosition(feetAtContact)))
                victim.TakeDamage(damage, contact, (contact - origin).normalized, boss.gameObject);
        }

        previousPosition = position;
        previousFeet = feet;
        previousRadius = radius;
        if (radius < reach) return false;
        Stop();
        return true;
    }

    private float HorizontalDistance(Vector3 position) =>
        new Vector2(position.x - origin.x, position.z - origin.z).magnitude;

    internal void CaptureColumnShadows()
    {
        for (int i = 0; i < pillars.Length; i++)
        {
            var column = columns != null && i < columns.Length ? columns[i] : null;
            pillars[i] = Vector4.zero;
            if (column == null || !column.enabled || !column.gameObject.activeInHierarchy) continue;
            var bounds = column.bounds;
            pillars[i] = new Vector4(bounds.center.x, bounds.center.z,
                Mathf.Min(bounds.extents.x, bounds.extents.z), 0f);
        }
    }

    internal bool IsBehindColumn(Vector3 attackOrigin, Vector3 position)
    {
        var start = new Vector2(attackOrigin.x, attackOrigin.z);
        var ray = new Vector2(position.x, position.z) - start;
        float lengthSquared = ray.sqrMagnitude;
        if (lengthSquared < 0.0001f) return false;
        foreach (var pillar in pillars)
        {
            if (pillar.z <= 0f) continue;
            var offset = new Vector2(pillar.x, pillar.y) - start;
            float t = Mathf.Clamp01(Vector2.Dot(offset, ray) / lengthSquared);
            if ((offset - ray * t).sqrMagnitude <= pillar.z * pillar.z) return true;
        }
        return false;
    }

    public void Stop()
    {
        pose?.Stop();
        view?.Hide();
    }

    public void Dispose()
    {
        Stop();
        view?.Dispose();
    }
}
