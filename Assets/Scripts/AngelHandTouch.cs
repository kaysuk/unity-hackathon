using UnityEngine;

// One detached hand leaves the fan. Its leading fingertip follows the locked line.
internal sealed class AngelHandTouch
{
    private const float LiftSeconds = .56f;
    private const float LockSeconds = .03f;
    private const float FlightSeconds = .17f;
    private const float LaunchTime = LiftSeconds + LockSeconds;
    private const float TipRadius = .055f;
    private const float StrikeRoll = 28f;
    private const float FlightGlow = .24f;

    private readonly Transform boss, skull;
    private readonly NimbEncounter encounter;
    private readonly AngelHandTouchPose pose;
    private readonly AngelAttackSweep sweep;
    private readonly GameObject tell;
    private readonly Light glow;
    private readonly AudioSource chime;
    private CharacterController player;
    private CombatHealth victim;
    private Vector3 launch, direction;
    private float draw, reach, chamberLimit, flown, returnStart, returnSeconds;
    private bool active, hitIvan, struckStone;

    internal bool Locked { get; private set; }
    internal bool Returning { get; private set; }
    internal bool CanBegin => pose.IsReady && player != null && skull != null;
    internal Vector3 PalmPosition => pose.PalmPosition;
    internal Vector3 TipPosition => pose.TipPosition;

    internal AngelHandTouch(Transform boss, Transform skull, NimbEncounter encounter)
    {
        this.boss = boss;
        this.encounter = encounter;
        this.skull = skull;
        pose = new AngelHandTouchPose(boss);
        sweep = new AngelAttackSweep(boss);
        tell = new GameObject("Hand touch tell");
        tell.transform.SetParent(boss, false);
        glow = tell.AddComponent<Light>();
        glow.color = new Color(1f, .72f, .34f);
        glow.range = 1.35f;
        glow.enabled = false;
        chime = tell.AddComponent<AudioSource>();
        chime.playOnAwake = false;
        chime.spatialBlend = .65f;
        chime.minDistance = 5f;
        chime.maxDistance = 45f;
        chime.rolloffMode = AudioRolloffMode.Linear;
        chime.volume = .65f;
        chime.clip = Resources.Load<AudioClip>("NimbHandChime");
    }

    internal void SetTarget(CharacterController body, CombatHealth health)
    {
        if (player != body && active) Stop();
        player = body;
        victim = health;
    }

    internal bool Begin()
    {
        if (!CanBegin) return false;
        active = true;
        Locked = Returning = hitIvan = struckStone = false;
        flown = 0f;
        tell.transform.position = skull.position;
        if (chime.clip != null) chime.Play();
        return true;
    }

    internal void RestorePose() => pose.Restore();

    internal bool Tick(float elapsed, float damage)
    {
        if (!active) return true;
        if (!Locked)
        {
            Bounds body = player.bounds;
            Vector3 chest = body.center + Vector3.up * (.39f * body.extents.y);
            Vector3 heading = Vector3.ProjectOnPlane(chest - skull.position, Vector3.up).normalized;
            if (heading.sqrMagnitude < .001f) heading = boss.forward;
            float lift = Ease(elapsed / LiftSeconds);
            Vector3 offset = Vector3.Cross(heading, Vector3.up) * (1.02f + .16f * Mathf.Sin(lift * Mathf.PI))
                + heading * .48f + Vector3.up * .10f;
            Vector3 raiseDirection = offset.normalized;
            float raiseDistance = encounter != null
                ? encounter.LimitToChamberEntrance(skull.position, raiseDirection, offset.magnitude) : offset.magnitude;
            if (sweep.FirstContact(player, skull.position, TipRadius, raiseDirection, raiseDistance, out var obstruction, false))
                raiseDistance = Mathf.Max(0f, obstruction.distance - .015f);
            Vector3 ready = skull.position + raiseDirection * raiseDistance;
            direction = (chest - ready).normalized;
            Show(ready, lift, Mathf.Lerp(52f, StrikeRoll, lift), .8f * lift);
            if (elapsed < LiftSeconds) return false;

            LockLine(chest, Mathf.Min(body.extents.x, body.extents.z));
        }

        if (elapsed < LaunchTime)
        {
            float beat = Ease((elapsed - LiftSeconds) / LockSeconds);
            Show(launch + direction * (draw * (1f - beat)), 1f, StrikeRoll, Mathf.Lerp(2.8f, .9f, beat));
            return false;
        }

        if (!Returning)
        {
            float progress = Ease((elapsed - LaunchTime) / FlightSeconds);
            Advance(reach * progress, damage);
            if (!active) return true;
            if (!struckStone && progress < 1f) return false;
            Returning = true;
            returnStart = struckStone ? elapsed : LaunchTime + FlightSeconds;
            returnSeconds = .39f + flown / 24f;
        }
        return Withdraw(elapsed - returnStart);
    }

    private void LockLine(Vector3 chest, float bodyRadius)
    {
        Locked = true;
        Vector3 ready = TipPosition;
        Vector3 sight = chest - skull.position;
        Vector3 end = chest + direction * (bodyRadius + .60f);
        // Cover between the boss and Ivan catches even a hand raised to one side of the skull.
        if (sweep.FirstContact(player, skull.position, TipRadius, sight.normalized, sight.magnitude, out var cover, false))
            end = cover.point - cover.normal * .12f;

        Vector3 line = end - ready;
        if (line.sqrMagnitude > .000001f) direction = line.normalized;
        draw = .23f;
        if (sweep.FirstContact(player, ready, TipRadius, -direction, draw, out var behind, false))
            draw = Mathf.Max(0f, behind.distance - .015f);
        launch = ready - direction * draw;
        reach = line.magnitude + draw;
        chamberLimit = encounter != null ? encounter.LimitToChamberEntrance(launch, direction, reach) : reach;
    }

    private void Advance(float next, float damage)
    {
        bool reachedChamber = chamberLimit < reach && next >= chamberLimit;
        next = Mathf.Min(next, chamberLimit);
        Vector3 from = launch + direction * flown;
        float step = next - flown;
        bool hit = sweep.FirstContact(player, from, TipRadius, direction, step, out var contact, !hitIvan);
        if (hit && contact.collider == player)
        {
            if (encounter == null || !encounter.ContainsChamberPosition(player.transform.position))
            {
                hitIvan = true;
                Show(from + direction * contact.distance, 1f, StrikeRoll, FlightGlow);
                if (!victim.IsDead) victim.TakeDamage(damage, contact.point, direction, boss.gameObject);
                if (!active) return;
            }
            // Finish this frame's travel through Ivan, stopping at any stone on the same segment.
            hit = sweep.FirstContact(player, from, TipRadius, direction, step, out contact, false);
        }

        struckStone = hit || reachedChamber;
        flown = hit ? flown + contact.distance : next;
        Show(launch + direction * flown, 1f, StrikeRoll, FlightGlow);
    }

    private bool Withdraw(float elapsed)
    {
        float hold = struckStone ? .15f : hitIvan ? .17f : .06f;
        float recoil = struckStone ? Mathf.Min(.055f, flown) : 0f;
        if (elapsed < hold)
        {
            float beat = Ease(elapsed / hold);
            Show(launch + direction * (flown - recoil * beat), 1f, StrikeRoll, FlightGlow * (1f - beat));
            return false;
        }

        float t = Mathf.Clamp01((elapsed - hold) / returnSeconds);
        float remaining = (flown - recoil) * (1f - Ease(t));
        // Withdraw along the same line. Only release the bones to Float near their docking pose.
        float influence = Mathf.Max(Ease(remaining / 1.6f), 1f - Ease((t - .55f) / .45f));
        Show(launch + direction * remaining, influence, StrikeRoll + 12f * Mathf.Sin(Mathf.PI * t), 0f);
        if (t < 1f) return false;
        Stop();
        return true;
    }

    private void Show(Vector3 point, float influence, float roll, float brightness)
    {
        pose.Apply(point, direction, influence, roll);
        tell.transform.position = PalmPosition + Vector3.up * .22f;
        glow.intensity = brightness;
        glow.enabled = brightness > .01f;
    }

    private static float Ease(float t) => Mathf.SmoothStep(0f, 1f, t);

    internal void Stop()
    {
        active = Locked = Returning = false;
        pose.Restore();
        if (glow != null) glow.enabled = false;
        if (chime != null) chime.Stop();
    }

    internal void Dispose()
    {
        Stop();
        Object.Destroy(tell);
    }
}
