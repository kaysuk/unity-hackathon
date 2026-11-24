using System.Collections.Generic;
using UnityEngine;
using Attack = NimbAttack;

[DisallowMultipleComponent]
public sealed class AngelSealAttack : MonoBehaviour
{
    // These three hands lead the combat gestures while the full fan follows the authored animation.
    internal static readonly string[] HandPrefixes = { "hand1:", "hand8:", "hand2:" };

    [SerializeField] private Transform player;
    [SerializeField] private Material sealMaterial;
    [SerializeField, Min(0.1f)] private float radius = 1.5f;
    [SerializeField, Min(0.1f)] private float warningSeconds = .88f;
    [SerializeField, Min(0.1f)] private float recoverySeconds = 2.8f;
    [SerializeField, Min(0f)] private float damage = 35f;
    [SerializeField, Min(0.1f)] private float activationRange = 12f;

    [SerializeField] private Material rimMaterial;
    [SerializeField] private AngelHaloSweep haloSweep = new AngelHaloSweep();
    [Header("Attack selection")]
    [Tooltip("Play the original fixed cycle to reproduce attacks during debugging.")]
    [SerializeField] private bool useFixedAttackOrder;

    private const int FinalPhaseAltars = 4;
    private NimbEncounter encounter;
    private bool haloFollowupPending;
    private NimbAttackModel attackModel;
    private NimbAttackCsv attackCsv;
    private Attack nextAttack;
    private Attack? lastAttack;
    private int consecutiveAttacks;
    private const int CoverHistoryLength = 3;
    private readonly Queue<bool> recentReactions = new Queue<bool>(CoverHistoryLength);
    internal int RecentCoverReactions { get; private set; }
    private Vector3 attackOrigin;
    private bool watchingForCover, enteredCover;
    private AngelHeadDash headDash;
    private NimbArenaClearance arenaClearance;
    private AngelLightArrowVolley lightArrows;
    private AngelHandTouch handTouch;
    private bool touching;
    private bool firingArrows;
    private bool dashing;
    private bool sweeping;
    private const float MarkInterval = .65f;
    private readonly AngelEyeMark[] marks = new AngelEyeMark[5];
    private int marksInCast;
    private int placedMarks;
    private bool hitDuringBurst;
    private static readonly int SealLayer = Shader.PropertyToID("_SealLayer");
    private static readonly int BeamProgress = Shader.PropertyToID("_BeamProgress");
    private MeshRenderer pillar;
    private Mesh pillarMesh;
    private Light pillarLight;
    private MaterialPropertyBlock pillarProperties;
    private Vector3 pillarBase;
    private float pillarStart;
    private Transform fingertip;
    private LineRenderer litFinger;
    private Light fingerLight;

    private const float PillarSeconds = .36f;
    private CombatHealth health;
    private Transform wrist;
    private Transform finger;
    private Transform knuckle;
    private float nextCast;
    private float castStart;
    private bool casting;
    private bool encounterSuspended;
    private bool encounterFinished;

    private bool engaged;
    private Transform skull;
    private CharacterController playerBody;
    private Quaternion animatedSkull;
    private bool gazeApplied;
    private float bodyTurnSpeed;
    private float lookYaw, lookPitch;

    internal Transform EncounterTarget => player;
    internal NimbArenaClearance ArenaClearance => arenaClearance ??= new NimbArenaClearance(transform, haloSweep.Columns);
    private int ConfirmedAltars => encounter != null ? encounter.ConfirmedAltars : 0;
    internal float RecoverySeconds => ConfirmedAltars switch
    {
        >= FinalPhaseAltars => Mathf.Min(recoverySeconds, 1.5f),
        >= 2 => Mathf.Min(recoverySeconds, 2f),
        _ => recoverySeconds
    };

    internal void BindEncounter(NimbEncounter owner) => encounter = owner;

    public void SetTarget(Transform target)
    {
        if (player != target)
        {
            CloseAttackLog();
            ResetAttackHistory();
        }
        player = target;
        health = target.GetComponent<CombatHealth>();
        playerBody = target.GetComponent<CharacterController>();
        haloSweep.SetTarget(target);
        headDash?.SetTarget(target);
        lightArrows?.SetTarget(playerBody, health);
        handTouch?.SetTarget(playerBody, health);
    }

    private void Start()
    {
        // An additive room can be unloaded before its first gameplay frame.
        if (!gameObject.scene.isLoaded)
        {
            enabled = false;
            return;
        }

        health = player.GetComponent<CombatHealth>();
        playerBody = player.GetComponent<CharacterController>();
        BindHandBounds();
        // The imported FBX can retain or strip the hand namespace.
        foreach (var bone in GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == "skeleton:head" || bone.name == "head") skull = bone;
            if (finger != null || !bone.name.Contains("arm_wrist")) continue;
            foreach (var joint in bone.GetComponentsInChildren<Transform>(true))
            {
                if (!joint.name.Contains("index_1") || joint.childCount == 0) continue;
                wrist = bone;
                knuckle = joint;
                finger = joint.GetChild(0);
                break;
            }
        }

        for (int i = 0; i < marks.Length; i++)
            marks[i] = new AngelEyeMark(transform, rimMaterial, i);
        CreateLightPillar();
        if (finger != null) CreateFingerHighlight();
        haloSweep.Initialize(transform, player, encounter);
        headDash = new AngelHeadDash(transform, player, sealMaterial, ArenaClearance, encounter);
        lightArrows = new AngelLightArrowVolley(transform, skull, sealMaterial, encounter);
        lightArrows.SetTarget(playerBody, health);
        handTouch = new AngelHandTouch(transform, skull, encounter);
        handTouch.SetTarget(playerBody, health);
        attackModel = NimbAttackModel.Load();
        nextCast = Time.time + recoverySeconds;
    }

    private void BindHandBounds()
    {
        // The FBX assigns every skin to hand9 for culling, even though the ten forearms fly independently.
        const string meshSuffix = ":arm_mesh";
        foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!skin.name.EndsWith(meshSuffix, System.StringComparison.Ordinal)) continue;
            string wristName = skin.name.Substring(0, skin.name.Length - meshSuffix.Length) + ":arm_wrist";
            foreach (var joint in skin.bones)
            {
                if (joint == null || joint.name != wristName) continue;
                skin.rootBone = joint;
                // The wrist is near the middle of the mesh. Allow full finger extension in any direction.
                float forearm = joint.InverseTransformPoint(joint.parent.position).magnitude;
                skin.localBounds = new Bounds(Vector3.zero, Vector3.one * (2.5f * forearm));
                break;
            }
        }
    }

    private void CreateLightPillar()
    {
        var shaft = new GameObject("Seal light pillar", typeof(MeshFilter), typeof(MeshRenderer));
        shaft.transform.SetParent(transform, false);
        pillarMesh = AngelHaloWaveView.CreateOpenCylinder(48, "Seal light pillar");
        shaft.GetComponent<MeshFilter>().sharedMesh = pillarMesh;
        pillar = shaft.GetComponent<MeshRenderer>();
        pillar.sharedMaterial = rimMaterial;
        pillar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pillar.receiveShadows = false;
        pillarProperties = new MaterialPropertyBlock();
        pillarProperties.SetFloat(SealLayer, 2f);
        var glow = new GameObject("Seal glow");
        glow.transform.SetParent(shaft.transform, false);
        glow.transform.localPosition = Vector3.up * .2f;
        pillarLight = glow.AddComponent<Light>();
        pillarLight.type = LightType.Point;
        pillarLight.color = new Color(1f, .64f, .24f);
        pillarLight.range = radius * 2.8f;
        HidePillar();
    }

    private void UpdatePillar()
    {
        if (pillar == null || !pillar.enabled) return;
        float t = Mathf.Clamp01((Time.time - pillarStart) / PillarSeconds);
        if (t >= 1f)
        {
            HidePillar();
            return;
        }
        float rise = Mathf.Clamp01(t / .34f);
        float height = Mathf.Lerp(.16f, 4.8f, 1f - (1f - rise) * (1f - rise));
        float width = radius * Mathf.Lerp(.76f, .96f, rise);
        pillar.transform.SetPositionAndRotation(pillarBase, Quaternion.identity);
        pillar.transform.localScale = new Vector3(width, height, width);
        pillarProperties.SetFloat(BeamProgress, t);
        pillar.SetPropertyBlock(pillarProperties);
        pillarLight.intensity = 3.2f * (1f - Mathf.SmoothStep(0f, 1f, t));
    }

    private void HidePillar()
    {
        if (pillar != null) pillar.enabled = false;
        if (pillarLight != null) pillarLight.enabled = false;
    }

    private void CreateFingerHighlight()
    {
        fingertip = finger;
        while (fingertip.childCount > 0) fingertip = fingertip.GetChild(0);

        var glow = new GameObject("Pointing finger glow");
        glow.transform.SetParent(transform, false);
        litFinger = glow.AddComponent<LineRenderer>();
        litFinger.sharedMaterial = sealMaterial;
        litFinger.positionCount = 3;
        litFinger.numCapVertices = 8;
        litFinger.numCornerVertices = 4;
        litFinger.widthMultiplier = 0.085f;
        var tint = new MaterialPropertyBlock();
        tint.SetColor("_BaseColor", new Color(0.12f, 2.6f, 3.6f));
        litFinger.SetPropertyBlock(tint);
        litFinger.enabled = false;

        fingerLight = glow.AddComponent<Light>();
        fingerLight.type = LightType.Point;
        fingerLight.color = new Color(0.15f, 0.86f, 1f);
        fingerLight.range = 1.3f;
        fingerLight.intensity = 4f;
        fingerLight.enabled = false;
    }

    private void RestoreGaze()
    {
        if (gazeApplied && skull != null) skull.localRotation = animatedSkull;
        gazeApplied = false;
    }

    private void FacePlayer(float gazeWeight)
    {
        Vector3 target = playerBody != null ? playerBody.bounds.center : player.position + Vector3.up;
        Vector3 flat = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
        if (flat.sqrMagnitude < .0001f) return;

        float targetYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        float bodyYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref bodyTurnSpeed, .36f, 75f);
        transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
        if (skull == null) return;

        Vector3 toPlayer = target - skull.position;
        float yaw = Mathf.Clamp(Mathf.DeltaAngle(bodyYaw, targetYaw), -52f, 52f);
        float horizontal = Vector3.ProjectOnPlane(toPlayer, Vector3.up).magnitude;
        float pitch = Mathf.Clamp(-Mathf.Atan2(toPlayer.y, horizontal) * Mathf.Rad2Deg, -20f, 30f);
        float response = 1f - Mathf.Exp(-12f * Time.deltaTime);
        lookYaw = Mathf.Lerp(lookYaw, yaw, response);
        lookPitch = Mathf.Lerp(lookPitch, pitch, response);

        // Layer the look over this frame's animated skull; the torso turns more slowly.
        animatedSkull = skull.localRotation;
        skull.rotation = Quaternion.AngleAxis(lookYaw * gazeWeight, Vector3.up) *
            Quaternion.AngleAxis(lookPitch * gazeWeight, transform.right) * skull.rotation;
        gazeApplied = true;
    }

    private void Update()
    {
        RestoreGaze();
        haloSweep.RestorePose();
        headDash?.RestorePose();
        handTouch?.RestorePose();
    }

    private void LateUpdate()
    {
        if (encounterSuspended || Time.timeScale <= 0f) return;
        TickAttack();
        // The afterglow remains at the mark as the boss turns between casts.
        UpdatePillar();
    }

    private void TickAttack()
    {
        if (health == null || health.IsDead)
        {
            enabled = false;
            return;
        }

        if (watchingForCover && haloSweep.IsBehindColumn(attackOrigin, playerBody.bounds.center))
        {
            enteredCover = true;
            watchingForCover = false;
        }

        if (touching)
        {
            if (handTouch.Tick(Time.time - castStart, damage))
            {
                touching = false;
                FinishAttack();
            }
            return;
        }
        if (firingArrows)
        {
            FacePlayer(1f);
            if (lightArrows.Tick(Time.time - castStart, damage))
            {
                firingArrows = false;
                FinishAttack();
            }
            return;
        }
        if (sweeping || dashing)
        {
            float attackTime = Time.time - castStart;
            if (sweeping ? haloSweep.Tick(attackTime) : headDash.Tick(attackTime))
            {
                sweeping = false;
                dashing = false;
                FinishAttack();
            }
            return;
        }
        if (!casting)
        {
            if (!engaged)
            {
                if (Vector3.Distance(transform.position, player.position) > activationRange)
                {
                    FacePlayer(1f);
                    return;
                }
                engaged = true;
                nextCast = Mathf.Max(nextCast, Time.time + .25f);
            }

            float timeLeft = nextCast - Time.time;
            if (timeLeft > 0f)
            {
                FacePlayer(Mathf.SmoothStep(0f, 1f, timeLeft / .25f));
                return;
            }

            BeginNextAttack();
            if (!casting) return;
        }

        float elapsed = Time.time - castStart;
        // Every placement and strike uses the original cast clock so frame delays do not accumulate.
        while (placedMarks < marksInCast && elapsed >= placedMarks * MarkInterval)
        {
            var mark = marks[placedMarks];
            mark.Place(player.position, radius);
            GameAudio.Play(SoundCue.AngelCharge, mark.Center, 0.64f, 5f);
            placedMarks++;
        }
        PointAtMark(marks[placedMarks - 1].Center, elapsed);
        for (int i = 0; i < placedMarks; i++)
        {
            var mark = marks[i];
            if (mark.Tick(elapsed - i * MarkInterval, warningSeconds)) StrikeMark(mark);
        }

        float finalStrike = (marksInCast - 1) * MarkInterval + warningSeconds;
        if (elapsed < finalStrike + AngelEyeMark.FlashSeconds) return;
        HideSeal();
        FinishAttack();
    }

    private void BeginNextAttack()
    {
        bodyTurnSpeed = lookYaw = lookPitch = 0f;
        castStart = Time.time;
        // A dash moves the boss. Observe Ivan's reaction relative to the original telegraph.
        attackOrigin = transform.position;
        Vector3 position = playerBody.bounds.center;
        haloSweep.CaptureColumnShadows();
        bool inCover = haloSweep.IsBehindColumn(attackOrigin, position);
        float distance = Vector3.ProjectOnPlane(position - attackOrigin, Vector3.up).magnitude;
        bool dashAvailable = headDash.HasClearApproach(haloSweep.Columns);
        bool handAvailable = handTouch.CanBegin;
        Attack? excludedRepeat = null;
        float? roll = null;
        string mode;
        Attack requested;
        Attack attack;
        bool haloFollowup = haloFollowupPending && !useFixedAttackOrder;
        haloFollowupPending = false;
        if (haloFollowup)
        {
            mode = "combo";
            requested = attack = Attack.EyeSeal;
        }
        else if (useFixedAttackOrder || attackModel == null)
        {
            mode = useFixedAttackOrder ? "fixed" : "model_fallback";
            requested = nextAttack;
            attack = requested == Attack.HeadDash && !dashAvailable ? Attack.EyeSeal : requested;
        }
        else
        {
            mode = "adaptive";
            if (consecutiveAttacks >= 2) excludedRepeat = lastAttack;
            roll = Random.value;
            attack = attackModel.Choose(distance, RecentCoverReactions, inCover,
                dashAvailable, handAvailable, excludedRepeat, roll.Value);
            requested = attack;
        }

        if (!roll.HasValue)
            attackModel?.ScoreFixedDecision(distance, RecentCoverReactions, inCover, attack);

        switch (attack)
        {
            case Attack.HaloWave:
                nextAttack = Attack.HandTouch;
                sweeping = true;
                haloSweep.Begin();
                break;
            case Attack.HandTouch:
                nextAttack = Attack.LightArrows;
                touching = handTouch.Begin();
                if (!touching) return;
                break;
            case Attack.LightArrows:
                nextAttack = Attack.HeadDash;
                firingArrows = true;
                lightArrows.Begin();
                break;
            case Attack.HeadDash:
                nextAttack = Attack.EyeSeal;
                dashing = true;
                headDash.Begin();
                break;
            case Attack.EyeSeal:
                if (!haloFollowup) nextAttack = Attack.HaloWave;
                marksInCast = haloFollowup ? 1 : marks.Length;
                placedMarks = 0;
                hitDuringBurst = false;
                casting = true;
                break;
        }
        // Record only an attack that actually started; a rejected hand cast never produces a label.
        if (attackCsv == null)
        {
            attackCsv = new NimbAttackCsv(NimbAttackCsv.DefaultDirectory);
            health.DamageReceived += OnTargetDamaged;
            health.onDeath.AddListener(LearnFromAttempt);
        }
        attackCsv.Begin(attack, mode, requested, distance, inCover, RecentCoverReactions,
            dashAvailable, handAvailable, excludedRepeat, roll, attackModel?.HitChances, attackModel?.SelectionChances);

        consecutiveAttacks = lastAttack == attack ? consecutiveAttacks + 1 : 1;
        lastAttack = attack;
        watchingForCover = !inCover;
        enteredCover = false;
    }

    private void FinishAttack()
    {
        attackCsv?.Finish(false);
        watchingForCover = false;
        if (lastAttack.HasValue)
        {
            if (recentReactions.Count == CoverHistoryLength && recentReactions.Dequeue()) RecentCoverReactions--;
            recentReactions.Enqueue(enteredCover);
            if (enteredCover) RecentCoverReactions++;
        }
        // Queue the seal only once the wave has crossed the whole arena.
        haloFollowupPending = lastAttack == Attack.HaloWave && !useFixedAttackOrder &&
            ConfirmedAltars >= FinalPhaseAltars;
        nextCast = Time.time + RecoverySeconds;
    }

    private void OnTargetDamaged(GameObject source)
    {
        if (source == gameObject) attackCsv?.MarkHit();
    }

    // DamageReceived is sent before onDeath, so a fatal attack already has its hit label.
    internal void LearnFromAttempt()
    {
        if (attackCsv == null) return;
        string path = attackCsv.FilePath;
        // On death the next tick disables the component and clears its attack effects.
        encounterSuspended = health == null || !health.IsDead;
        CloseAttackLog();
        if (path != null) NimbAttackLearning.ApplyEncounter(path);
    }

    private void CloseAttackLog()
    {
        if (attackCsv == null) return;
        if (health != null)
        {
            health.DamageReceived -= OnTargetDamaged;
            health.onDeath.RemoveListener(LearnFromAttempt);
        }
        attackCsv.Dispose();
        attackCsv = null;
    }

    private void ResetAttackHistory()
    {
        haloFollowupPending = false;
        recentReactions.Clear();
        RecentCoverReactions = consecutiveAttacks = 0;
        watchingForCover = enteredCover = false;
        lastAttack = null;
        nextAttack = Attack.EyeSeal;
    }

    internal void SetEncounterSuspended(bool suspended)
    {
        if (encounterFinished || encounterSuspended == suspended) return;
        encounterSuspended = suspended;
        if (suspended)
        {
            attackCsv?.Finish(true);
            ClearAttackEffects();
        }
        else
        {
            nextCast = Mathf.Max(nextCast, Time.time + .45f);
        }
    }

    internal void FinishEncounter()
    {
        if (encounterFinished) return;
        SetEncounterSuspended(true);
        encounterFinished = true;
        GameAudio.Play(SoundCue.AngelDispelled, transform.position, .45f, 5f);
        CloseAttackLog();
    }

    private void PointAtMark(Vector3 center, float elapsed)
    {
        if (wrist != null)
        {
            var turn = Quaternion.FromToRotation(finger.position - knuckle.position, center - knuckle.position);
            wrist.rotation = Quaternion.Slerp(Quaternion.identity, turn, Mathf.Clamp01(elapsed / 0.22f)) * wrist.rotation;
            litFinger.enabled = true;
            litFinger.SetPosition(0, knuckle.position);
            litFinger.SetPosition(1, finger.position);
            litFinger.SetPosition(2, fingertip.position);
            fingerLight.transform.position = fingertip.position;
            fingerLight.enabled = true;
        }
    }

    private void StrikeMark(AngelEyeMark mark)
    {
        GameAudio.Play(SoundCue.AngelStrike, mark.Center, 0.91f, 6f);
        pillarBase = mark.SurfacePosition;
        pillarStart = Time.time;
        pillar.enabled = true;
        pillarLight.enabled = true;

        Vector3 offset = player.position - mark.Center;
        if (hitDuringBurst || Mathf.Abs(offset.y) >= 1f || new Vector2(offset.x, offset.z).sqrMagnitude > radius * radius)
            return;
        hitDuringBurst = true;
        health.TakeDamage(damage, player.position, Vector3.up, gameObject);
    }

    private void HideSeal()
    {
        casting = false;
        foreach (var mark in marks) mark?.Hide();
        if (litFinger != null) litFinger.enabled = false;
        if (fingerLight != null) fingerLight.enabled = false;
    }

    private void OnDestroy()
    {
        haloSweep.Dispose();
        lightArrows?.Dispose();
        handTouch?.Dispose();
        if (pillarMesh != null) Destroy(pillarMesh);
    }

    private void OnApplicationQuit() => CloseAttackLog();

    private void OnDisable()
    {
        CloseAttackLog();
        engaged = false;
        ClearAttackEffects();
        ResetAttackHistory();
    }

    private void ClearAttackEffects()
    {
        haloFollowupPending = false;
        RestoreGaze();
        bodyTurnSpeed = lookYaw = lookPitch = 0f;
        HideSeal();
        HidePillar();
        haloSweep.Stop();
        headDash?.Stop();
        lightArrows?.Stop();
        handTouch?.Stop();
        touching = false;
        firingArrows = false;
        sweeping = false;
        dashing = false;
        watchingForCover = enteredCover = false;
    }
}
