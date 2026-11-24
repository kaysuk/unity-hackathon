using System;
using UnityEngine;

// Both the light spears and the detached hand stop at the first solid on their swept path.
internal sealed class AngelAttackSweep
{
    private readonly Transform boss;
    private RaycastHit[] contacts = new RaycastHit[8];

    internal AngelAttackSweep(Transform boss) => this.boss = boss;

    // Sweep the whole distance since the last rendered frame, selecting cover and Ivan
    // in contact order even when a frame spans the full distance to both.
    internal bool FirstContact(CharacterController player, Vector3 origin, float radius, Vector3 direction,
        float distance, out RaycastHit nearest, bool includePlayer = true)
    {
        nearest = default;
        if (distance <= 0f) return false;
        int count;
        while ((count = Physics.SphereCastNonAlloc(origin, radius, direction, contacts, distance,
                   Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) == contacts.Length)
            Array.Resize(ref contacts, contacts.Length * 2);

        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = contacts[i];
            if (hit.collider.gameObject.scene != boss.gameObject.scene || hit.transform.IsChildOf(boss) ||
                (hit.transform.IsChildOf(player.transform) && !(includePlayer && hit.collider == player)) ||
                hit.distance >= nearestDistance) continue;
            nearest = hit;
            nearestDistance = hit.distance;
        }
        return nearest.collider != null;
    }
}
