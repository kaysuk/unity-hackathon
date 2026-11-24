using System;
using UnityEngine;

internal sealed class NimbArenaClearance
{
    private const float BodyRadius = .78f;
    private const float Skin = .025f;
    // The six inner walls of Temple_Final_Room are this far from the hall center.
    private const float HallApothem = 22.5f;
    private readonly Transform boss;
    private readonly Vector3 hallCenter;
    private readonly Vector3[] wallNormals = Array.Empty<Vector3>();
    private RaycastHit[] hits = new RaycastHit[8];

    internal NimbArenaClearance(Transform boss, Collider[] columns)
    {
        this.boss = boss;
        if (columns == null || columns.Length != 6 || Array.Exists(columns, column => column == null))
            return;

        foreach (Collider column in columns)
            hallCenter += Vector3.ProjectOnPlane(column.bounds.center, Vector3.up) / columns.Length;
        wallNormals = new Vector3[columns.Length];
        for (int i = 0; i < columns.Length; i++)
            wallNormals[i] = (Vector3.ProjectOnPlane(columns[i].bounds.center, Vector3.up) - hallCenter).normalized;
    }

    internal Vector3 ClampPosition(Vector3 from, Vector3 to, float floor, float headHeight, Transform target)
    {
        Vector3 flat = Vector3.ProjectOnPlane(to - from, Vector3.up);
        float length = flat.magnitude;
        if (length < .0001f) return to;
        Vector3 direction = flat / length;
        float clear = FreeDistance(from, direction, length, floor, headHeight, BodyRadius, target);
        return to - direction * (length - clear);
    }

    internal float FloorBelow(Vector3 origin, float fallback, Transform target)
    {
        float nearest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 10f,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!IsGeometry(hit.collider, target) || hit.normal.y < .65f || hit.distance >= nearest)
                continue;
            nearest = hit.distance;
            fallback = hit.point.y;
        }
        return fallback;
    }

    // Sweep the column of space above either the skull's route or the body's root.
    internal float FreeDistance(Vector3 from, Vector3 direction, float distance, float floor,
        float headHeight, float radius, Transform target)
    {
        float limit = distance;
        foreach (Vector3 normal in wallNormals)
        {
            float outward = Vector3.Dot(normal, direction);
            if (outward <= .0001f) continue;
            float remaining = HallApothem - BodyRadius - Skin - Vector3.Dot(from - hallCenter, normal);
            limit = Mathf.Min(limit, Mathf.Max(0f, remaining / outward));
        }
        if (limit <= 0f) return 0f;

        Vector3 bottom = new Vector3(from.x, floor + radius + .06f, from.z);
        Vector3 top = new Vector3(from.x, Mathf.Max(bottom.y, headHeight), from.z);
        int count;
        while ((count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction, hits,
                   limit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) == hits.Length)
            Array.Resize(ref hits, hits.Length * 2);

        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (!IsGeometry(hit.collider, target)) continue;
            limit = Mathf.Min(limit, Mathf.Max(0f, hit.distance - Skin));
        }
        return limit;
    }

    private bool IsGeometry(Collider collider, Transform target) =>
        collider.gameObject.scene == boss.gameObject.scene && !collider.transform.IsChildOf(boss) &&
        (target == null || !collider.transform.IsChildOf(target));
}
