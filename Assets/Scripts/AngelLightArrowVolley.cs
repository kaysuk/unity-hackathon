using UnityEngine;

// Each hovering point faces the shoulder camera. Its flash locks the flight path onto Ivan.
internal sealed class AngelLightArrowVolley
{
    private const float Windup = 1f;
    private const float Interval = .32f;
    private const float LockSeconds = .095f;
    private const float TargetFlightSeconds = .10f;
    private const float MinimumSpeed = 25f;
    private const float Radius = .09f;
    private const float MaxDistance = 60f;

    private const int SpearCount = 6;
    private struct Arrow
    {
        public Vector3 origin, direction;
        public float speed, travelled, range;
        public bool locked, launched, spent;
    }

    private readonly Arrow[] arrows = new Arrow[SpearCount];
    private readonly Transform boss, skull;
    private readonly NimbEncounter encounter;
    private readonly AngelLightArrowView view;
    private readonly AngelAttackSweep sweep;
    private CharacterController player;
    private CombatHealth health;
    private Vector3 crownBase;
    private bool hitDuringVolley;

    internal AngelLightArrowVolley(Transform boss, Transform skull, Material material, NimbEncounter encounter)
    {
        this.boss = boss;
        this.encounter = encounter;
        this.skull = skull;
        sweep = new AngelAttackSweep(boss);
        view = new AngelLightArrowView(boss, material, arrows.Length);
    }

    internal void SetTarget(CharacterController body, CombatHealth targetHealth)
    {
        player = body;
        health = targetHealth;
    }

    internal void Begin()
    {
        hitDuringVolley = false;
        crownBase = skull != null ? skull.position : boss.position + Vector3.up * 1.6f;
        Vector3 aimPoint = player.bounds.center;
        Camera viewer = Camera.main;
        Vector3 forward = Vector3.ProjectOnPlane(aimPoint - crownBase, Vector3.up).normalized;
        if (forward.sqrMagnitude < .001f) forward = Vector3.ProjectOnPlane(boss.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        for (int i = 0; i < arrows.Length; i++)
        {
            // Keep a gap between the two dots at each height.
            float side = i % 2 == 0 ? -1f : 1f;
            int row = i / 2;
            Vector3 mount = right * (side * (.66f + row * .12f)) + Vector3.up * (1.3f + row * .50f);
            arrows[i] = new Arrow { origin = crownBase + mount };
            if (encounter != null && encounter.LimitToChamberEntrance(crownBase, mount.normalized, mount.magnitude) < mount.magnitude)
            {
                arrows[i].spent = true;
                view.Hide(i);
                continue;
            }
            view.Set(i, arrows[i].origin, TowardViewer(viewer, arrows[i].origin, aimPoint), 0f, false);
        }
    }

    private static Vector3 TowardViewer(Camera viewer, Vector3 tip, Vector3 aimPoint)
    {
        if (viewer == null) return aimPoint - tip;
        return viewer.orthographic ? -viewer.transform.forward : viewer.transform.position - tip;
    }

    internal bool Tick(float elapsed, float damage)
    {
        Vector3 aimPoint = player.bounds.center;
        Camera viewer = Camera.main;
        bool finished = true;
        for (int i = 0; i < arrows.Length; i++)
        {
            ref Arrow arrow = ref arrows[i];
            if (arrow.spent) continue;

            float untilLaunch = Windup + i * Interval - elapsed;
            if (!arrow.locked && untilLaunch <= LockSeconds)
            {
                // Each flash leaves the same dodge window across the arena. The spear
                // then flies at its locked speed and heading until contact with stone or Ivan.
                Vector3 offset = aimPoint - arrow.origin;
                arrow.direction = offset.normalized;
                arrow.speed = Mathf.Max(MinimumSpeed, offset.magnitude / TargetFlightSeconds);
                arrow.range = encounter != null
                    ? encounter.LimitToChamberEntrance(arrow.origin, arrow.direction, MaxDistance) : MaxDistance;
                arrow.locked = true;
            }

            if (untilLaunch > 0f)
            {
                view.Set(i, arrow.origin, TowardViewer(viewer, arrow.origin, aimPoint), arrow.locked ? 1f : 0f, false);
            }
            else
            {
                float distance = Mathf.Min(arrow.range, -untilLaunch * arrow.speed);
                if (!arrow.launched)
                {
                    // A crown can form beside a column after a dash. Stop a shot whose mount
                    // is inside or beyond that column instead of letting it emerge on the other side.
                    Vector3 mount = arrow.origin - crownBase;
                    if (arrow.range <= 0f ||
                        sweep.FirstContact(player, crownBase, Radius, mount.normalized, mount.magnitude, out _, false))
                    {
                        arrow.spent = true;
                        view.Hide(i);
                        continue;
                    }
                    arrow.launched = true;
                    GameAudio.Play(SoundCue.LightSpear, arrow.origin, .62f, 6f);
                }
                Vector3 from = arrow.origin + arrow.direction * arrow.travelled;
                if (sweep.FirstContact(player, from, Radius, arrow.direction, distance - arrow.travelled, out RaycastHit hit))
                {
                    arrow.spent = true;
                    if (hit.collider == player && !hitDuringVolley && !health.IsDead
                        && (encounter == null || !encounter.ContainsChamberPosition(player.transform.position)))
                    {
                        hitDuringVolley = true;
                        health.TakeDamage(damage, hit.point, arrow.direction, boss.gameObject);
                    }
                }
                else
                {
                    arrow.travelled = distance;
                    arrow.spent = distance >= arrow.range;
                }

                if (arrow.spent) view.Hide(i);
                else view.Set(i, arrow.origin + arrow.direction * distance, arrow.direction, 1f, true);
            }
            finished &= arrow.spent;
        }
        return finished;
    }

    internal void Stop()
    {
        for (int i = 0; i < arrows.Length; i++)
            view.Hide(i);
    }

    internal void Dispose() => view.Dispose();
}
