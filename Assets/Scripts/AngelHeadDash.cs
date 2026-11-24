using UnityEngine;

// Per-cast movement and contact use the same trajectory as the animated skull.
internal sealed class AngelHeadDash
{
    private const float AimSeconds = .70f;
    private const float LockSeconds = .20f;
    private const float MinimumFlightSpeed = 21f;
    private const float CruiseSeconds = .18f;
    private const float SkullRadius = .42f;
    private const float ContactSkin = .025f;
    private const float LaunchSeconds = .10f;
    private const float BrakeDistance = 1.5f;
    private const float Damage = 35f;
    private const float RiseSeconds = 2.12f;
    private const float PulloutSeconds = .32f;
    private const float PulloutDegrees = 3.5f;

    private readonly Transform boss;
    private readonly NimbEncounter encounter;
    private readonly AngelDashPose pose;
    private readonly NimbArenaClearance clearance;
    private readonly AngelAttackSweep skullSweep;
    private CharacterController target;
    private CombatHealth health;
    private const int DiveSegments = 6;
    private readonly Vector3[] path = new Vector3[DiveSegments + 3];
    private readonly float[] pathLengths = new float[DiveSegments + 3];
    private int lastPoint;
    private Vector3 castOrigin, direction, launchHead, flightHead, releaseHead, hoverPosition;
    private Quaternion initialRotation, dashHeading, flightRotation, recoveryRotation;
    private float floorY, hoverClearance, hoverHeadHeight, distance, travelled, contactTime;
    private float peakSpeed, accelerationTime, brakingTime, flightDuration;
    private bool active, locked, blocked, dealtDamage, landed, settling;

    internal AngelHeadDash(Transform boss, Transform target, Material material, NimbArenaClearance clearance, NimbEncounter encounter)
    {
        this.boss = boss;
        this.encounter = encounter;
        pose = new AngelDashPose(boss, material);
        this.clearance = clearance;
        skullSweep = new AngelAttackSweep(boss);
        SetTarget(target);
    }

    internal void SetTarget(Transform player)
    {
        target = player.GetComponent<CharacterController>();
        health = player.GetComponent<CombatHealth>();
    }

    internal bool HasClearApproach(Collider[] columns)
    {
        if (columns == null || columns.Length == 0) return true;
        Vector3 from = pose.HeadPosition;
        Vector3 toIvan = target.bounds.center - from;
        float toNearSide = toIvan.magnitude - Mathf.Min(target.bounds.extents.x, target.bounds.extents.z);
        if (toNearSide <= 0f) return true;

        // Only the six arena pillars veto selection. Walls and late cover still stop a launched dash.
        foreach (var hit in Physics.SphereCastAll(from, SkullRadius, toIvan.normalized, toNearSide,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (System.Array.IndexOf(columns, hit.collider) >= 0) return false;
        }
        return true;
    }

    internal void Begin()
    {
        Stop();
        castOrigin = boss.position;
        initialRotation = boss.rotation;
        floorY = clearance.FloorBelow(castOrigin + Vector3.up * .15f, castOrigin.y - .6f, target.transform);
        hoverClearance = castOrigin.y - floorY;
        hoverHeadHeight = pose.HeadPosition.y + .2f;
        active = true;
        locked = false;
        landed = false;
        settling = false;
        dealtDamage = false;
        travelled = 0f;
        pose.Begin();
        GameAudio.Play(SoundCue.HeadDash, pose.HeadPosition, .8f, 7f);
    }

    internal void RestorePose() => pose.RestorePose();

    internal bool Tick(float elapsed)
    {
        if (!active) return true;
        Physics.SyncTransforms();
        if (!locked)
        {
            Aim(Mathf.Clamp01(elapsed / AimSeconds));
            if (elapsed < AimSeconds) return false;
            locked = true;
            launchHead = flightHead = pose.HeadPosition;
            LockPath();
            dashHeading = Quaternion.LookRotation(direction, Vector3.up);
            PrepareFlight();
            contactTime = AimSeconds + LockSeconds + flightDuration;
        }

        float flightTime = elapsed - (AimSeconds + LockSeconds);
        if (flightTime < 0f)
        {
            // Gather the body behind the skull while the warning still shows the locked bearing.
            float gather = Mathf.SmoothStep(0f, 1f, (elapsed - AimSeconds) / LockSeconds);
            boss.rotation = dashHeading * Quaternion.Euler(Mathf.Lerp(12f, 53f, gather), 0f, 0f);
            Vector3 start = new Vector3(launchHead.x, floorY, launchHead.z);
            Vector3 tip = PathPosition(distance);
            pose.Lock(start, new Vector3(tip.x, floorY, tip.z), (elapsed - AimSeconds) / LockSeconds);
            PlaceHeadAt(launchHead);
            flightHead = pose.HeadPosition;
            return false;
        }

        if (!landed)
        {
            float next = AdvanceFlight(DistanceFlown(flightTime), elapsed);
            Vector3 head = PathPosition(next);
            float flightProgress = distance > .001f ? next / distance : 1f;
            float extend = Mathf.SmoothStep(0f, 1f, flightTime / .20f);
            float brake = Mathf.SmoothStep(0f, 1f, (flightProgress - .63f) / .37f);
            float bank = 6f * extend * Mathf.Sin(Mathf.PI * flightProgress);
            // On a clear run the body is already starting to lift when the skull stops travelling.
            float pullout = blocked ? 0f : Mathf.Clamp01(1f - (flightDuration - flightTime) / PulloutSeconds);
            float pitch = 53f + 43f * extend - 5f * brake - PulloutDegrees * pullout * pullout;
            flightRotation = dashHeading * Quaternion.Euler(0f, 0f, bank) * Quaternion.Euler(pitch, 0f, 0f);
            boss.rotation = flightRotation;
            pose.Fly(Mathf.Clamp01(flightTime / .18f), flightProgress);
            PlaceHeadAt(head);
            Vector3 actualHead = pose.HeadPosition;
            StrikeFlight(next, head, actualHead);
            flightHead = actualHead;
            travelled = next;
            if (next < distance) return false;
            landed = true;
        }

        float sinceImpact = Mathf.Max(0f, elapsed - contactTime);
        float pause = blocked ? 2f : 0f;
        if (!settling)
        {
            float impactTime = Mathf.Min(sinceImpact, pause);
            float recoil = blocked ? 6f * Mathf.Sin(impactTime * 14f) * Mathf.Exp(-impactTime * 7f) : 0f;
            boss.rotation = Quaternion.AngleAxis(recoil, dashHeading * Vector3.right) * flightRotation;
            pose.Impact(impactTime, blocked);
            float pushOff = blocked ? .3f * Mathf.SmoothStep(0f, 1f, (impactTime - 1.55f) / .45f) : 0f;
            PlaceHeadAt(PathPosition(distance) - direction * pushOff);
            if (sinceImpact < pause) return false;

            settling = true;
            recoveryRotation = boss.rotation;
            releaseHead = pose.HeadPosition;
            pose.RestorePose();
            Vector3 neutralOffset = dashHeading * (Quaternion.Inverse(boss.rotation) * (pose.HeadPosition - boss.position));
            // An open finish carries forward. At a wall the skull stays outside the obstacle as the feet swing under it.
            Vector3 finalHead = releaseHead + direction * (blocked ? -.66f : .45f);
            Vector3 finalRoot = finalHead - Vector3.ProjectOnPlane(neutralOffset, Vector3.up);
            if (blocked)
            {
                Vector3 pivotTravel = Vector3.ProjectOnPlane(finalRoot - boss.position, Vector3.up);
                finalRoot = boss.position + Vector3.ClampMagnitude(pivotTravel, 1.35f);
            }
            hoverPosition = HoverAt(finalRoot);
            pose.BeginRise();
        }

        float t = Mathf.Clamp01((sinceImpact - pause) / RiseSeconds);
        pose.RestorePose();
        Vector3 idleHead = hoverPosition + dashHeading * (Quaternion.Inverse(boss.rotation) * (pose.HeadPosition - boss.position));
        float ease = Mathf.SmoothStep(0f, 1f, t);
        float arc = Mathf.Sin(Mathf.PI * t);
        arc *= arc;
        // Hermite interpolation carries the flight's pitch velocity into the rise. Against
        // stone both ends of the curve are at rest. Both paths slow all the way into hover.
        float releasePitch = Quaternion.Angle(recoveryRotation, dashHeading);
        float pitchVelocity = blocked ? 0f : 2f * PulloutDegrees / PulloutSeconds;
        float level = ease + pitchVelocity * RiseSeconds / Mathf.Max(releasePitch, 1f) * t * (1f - t) * (1f - t);
        boss.rotation = Quaternion.Slerp(recoveryRotation, dashHeading, level);
        pose.Return(t);

        // A shallow bank keeps the reaching hands in silhouette. The skull gains height
        // before the hips swing under it, so the feet can finish the arc without a kick.
        boss.rotation *= Quaternion.AngleAxis((blocked ? -11f : 18f) * arc, Vector3.up);
        Vector3 desiredHead = Vector3.Lerp(releaseHead, idleHead, ease) + direction * ((blocked ? -.14f : .20f) * arc);
        desiredHead.y = Mathf.Lerp(releaseHead.y, idleHead.y, ease + .18f * arc);
        PlaceHeadAt(desiredHead);
        if (t < 1f) return false;
        pose.Stop();
        active = false;
        return true;
    }

    // Restore the floating height relative to the floor at the new XZ, also on an interrupted cast.
    private Vector3 HoverAt(Vector3 position)
    {
        position = ClampHover(position);
        Vector3 probe = new Vector3(position.x, floorY + .3f, position.z);
        position.y = clearance.FloorBelow(probe, floorY, target.transform) + hoverClearance;
        return position;
    }

    private Vector3 ClampHover(Vector3 position) =>
        clearance.ClampPosition(boss.position, position, floorY, hoverHeadHeight, target.transform);

    private void PlaceHeadAt(Vector3 head) =>
        boss.position = ClampHover(boss.position + head - pose.HeadPosition);

    private void PrepareFlight()
    {
        // A longer lane increases dive speed rather than granting additional time after the lock.
        // On short dives acceleration and braking still meet before full speed.
        float speed = Mathf.Max(MinimumFlightSpeed, distance / CruiseSeconds);
        float stoppingDistance = blocked ? 0f : BrakeDistance;
        float distanceAtFullSpeed = .5f * speed * LaunchSeconds + stoppingDistance;
        float fraction = Mathf.Sqrt(Mathf.Min(1f, distance / distanceAtFullSpeed));
        peakSpeed = speed * fraction;
        accelerationTime = LaunchSeconds * fraction;
        brakingTime = 2f * stoppingDistance / speed * fraction;
        flightDuration = peakSpeed > 0f ? distance / peakSpeed + .5f * (accelerationTime + brakingTime) : 0f;
    }

    private float DistanceFlown(float seconds)
    {
        if (seconds >= flightDuration) return distance;
        if (seconds < accelerationTime) return .5f * peakSpeed / accelerationTime * seconds * seconds;
        float remaining = flightDuration - seconds;
        if (remaining < brakingTime)
            return distance - .5f * peakSpeed / brakingTime * remaining * remaining;
        return peakSpeed * (seconds - .5f * accelerationTime);
    }

    private void Aim(float t)
    {
        Vector3 towardPlayer = Vector3.ProjectOnPlane(target.bounds.center - castOrigin, Vector3.up);
        direction = towardPlayer.sqrMagnitude > .0001f ? towardPlayer.normalized : initialRotation * Vector3.forward;
        float windup = Mathf.SmoothStep(0f, 1f, t);
        float bodyTurn = Mathf.SmoothStep(0f, 1f, (t - .10f) / .90f);
        Quaternion yaw = Quaternion.LookRotation(direction, Vector3.up);
        boss.SetPositionAndRotation(ClampHover(castOrigin - direction * (.27f * windup)),
            Quaternion.Slerp(initialRotation, yaw * Quaternion.Euler(12f, 0f, 0f), bodyTurn));
        pose.Aim(t, direction);
    }

    private void LockPath()
    {
        Bounds playerBounds = target.bounds;
        Vector3 toPlayer = Vector3.ProjectOnPlane(playerBounds.center - launchHead, Vector3.up);
        float toLock = Mathf.Max(0f, Vector3.Dot(toPlayer, direction));
        if (toLock > SkullRadius)
        {
            // The animated skull can be offset sideways from the root used for aiming.
            direction = toPlayer.normalized;
            toLock = toPlayer.magnitude;
        }
        float playerRadius = Mathf.Min(playerBounds.extents.x, playerBounds.extents.z);
        float end = toLock + playerRadius + SkullRadius + BrakeDistance;
        float dive = Mathf.Min(toLock, Mathf.Clamp(toLock - .85f, .35f, 3.5f));
        float targetLaneY = playerBounds.min.y + 1.2f;
        path[0] = launchHead;
        pathLengths[0] = 0f;
        lastPoint = 0;
        blocked = false;
        for (int i = 1; i < path.Length; i++)
        {
            float t = Mathf.Clamp01((float)i / DiveSegments);
            float along = i <= DiveSegments ? dive * t : (i == DiveSegments + 1 ? toLock : end);
            Vector3 point = launchHead + direction * along;
            float laneY = Mathf.Lerp(floorY + 1.2f, targetLaneY, Mathf.InverseLerp(0f, toLock, along));
            point.y = Mathf.SmoothStep(launchHead.y, laneY, t);
            Vector3 span = point - path[lastPoint];
            float length = span.magnitude;
            if (length < .0001f) continue;
            Vector3 forward = span / length;
            float clear = FreePath(path[lastPoint], forward, length);
            path[lastPoint + 1] = path[lastPoint] + forward * clear;
            pathLengths[lastPoint + 1] = pathLengths[lastPoint] + clear;
            lastPoint++;
            blocked = clear < length - .001f;
            if (blocked) break;
        }
        distance = pathLengths[lastPoint];
    }

    private Vector3 PathPosition(float metres)
    {
        for (int i = 1; i <= lastPoint; i++)
        {
            if (metres < pathLengths[i])
                return Vector3.Lerp(path[i - 1], path[i], Mathf.InverseLerp(pathLengths[i - 1], pathLengths[i], metres));
        }
        return path[lastPoint];
    }

    private float AdvanceFlight(float next, float elapsed)
    {
        Vector3 from = PathPosition(travelled);
        float start = travelled;
        for (int i = 1; i <= lastPoint && start < next; i++)
        {
            float end = Mathf.Min(next, pathLengths[i]);
            if (end <= start) continue;
            Vector3 to = PathPosition(end);
            Vector3 span = to - from;
            float length = span.magnitude;
            if (length > .0001f)
            {
                Vector3 forward = span / length;
                float clear = FreePath(from, forward, length);
                if (clear < length - .001f)
                {
                    distance = start + clear;
                    blocked = true;
                    contactTime = elapsed;
                    return distance;
                }
            }
            from = to;
            start = end;
        }
        return next;
    }

    private void StrikeFlight(float next, Vector3 plannedHead, Vector3 actualHead)
    {
        if (dealtDamage || next <= travelled) return;
        Vector3 previous = PathPosition(travelled);
        if ((flightHead - previous).sqrMagnitude > .0001f || (actualHead - plannedHead).sqrMagnitude > .0001f)
        {
            // Clearance can move the posed skull off the planned line when the trailing body hits cover.
            Vector3 span = actualHead - flightHead;
            float length = span.magnitude;
            if (length <= .0001f) return;
            Vector3 forward = span / length;
            length = FreeSkullPath(flightHead, forward, length);
            if (length > 0f) StrikePlayer(flightHead, flightHead + forward * length);
            return;
        }

        for (int i = 1; i < lastPoint && !dealtDamage; i++)
        {
            if (pathLengths[i] <= travelled || pathLengths[i] >= next) continue;
            StrikePlayer(previous, path[i]);
            previous = path[i];
        }
        if (!dealtDamage) StrikePlayer(previous, plannedHead);
    }

    private float FreePath(Vector3 start, Vector3 forward, float length)
    {
        float limit = FreeSkullPath(start, forward, length);

        Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up);
        float horizontal = flat.magnitude;
        if (horizontal > .0001f)
        {
            float horizontalLimit = limit * horizontal;
            float clear = clearance.FreeDistance(start, flat / horizontal, horizontalLimit, floorY,
                hoverHeadHeight, SkullRadius, target.transform);
            if (clear < horizontalLimit) limit = clear / horizontal;
        }
        return limit;
    }

    private float FreeSkullPath(Vector3 from, Vector3 forward, float length) =>
        skullSweep.FirstContact(target, from, SkullRadius, forward, length, out var hit, includePlayer: false)
            ? Mathf.Max(0f, hit.distance - ContactSkin) : length;

    private void StrikePlayer(Vector3 from, Vector3 to)
    {
        if (!target.enabled || health.IsDead) return;
        // Closest points between this step of the skull and Ivan's upright capsule axis.
        Bounds body = target.bounds;
        float playerRadius = Mathf.Min(body.extents.x, body.extents.z);
        Vector3 low = body.center;
        Vector3 high = body.center;
        low.y = body.min.y + playerRadius;
        high.y = body.max.y - playerRadius;
        Vector3 step = to - from;
        Vector3 flatStep = Vector3.ProjectOnPlane(step, Vector3.up);
        float t = Mathf.Clamp01(Vector3.Dot(body.center - from, flatStep) / Mathf.Max(.000001f, flatStep.sqrMagnitude));
        Vector3 nearest = from + step * t;
        Vector3 axis = body.center;
        axis.y = Mathf.Clamp(nearest.y, low.y, high.y);
        if (nearest.y != axis.y)
        {
            t = Mathf.Clamp01(Vector3.Dot(axis - from, step) / Mathf.Max(.000001f, step.sqrMagnitude));
            nearest = from + step * t;
        }
        float reach = SkullRadius + playerRadius;
        if ((nearest - axis).sqrMagnitude > reach * reach) return;
        if (encounter != null && encounter.ContainsChamberPosition(target.transform.position)) return;
        Vector3 contact = axis + (nearest - axis).normalized * playerRadius;
        dealtDamage = true;
        health.TakeDamage(Damage, contact, direction, boss.gameObject);
    }

    internal void Stop()
    {
        if (!active) return;
        active = false;
        Vector3 position = HoverAt(boss.position);
        Vector3 facing = locked ? direction : Vector3.ProjectOnPlane(boss.forward, Vector3.up);
        pose.Stop();
        boss.SetPositionAndRotation(position, facing.sqrMagnitude > .0001f
            ? Quaternion.LookRotation(facing, Vector3.up) : initialRotation);
    }
}
