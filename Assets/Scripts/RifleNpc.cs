using UnityEngine;

[RequireComponent(typeof(Animator), typeof(CharacterController), typeof(CombatHealth))]
[DisallowMultipleComponent]
public sealed class RifleNpc : MonoBehaviour
{
    [SerializeField] private Transform rifle;
    [SerializeField] private Transform muzzle;
    [SerializeField] private GameObject muzzleFlash;
    [SerializeField] private AudioSource shotAudio;
    [SerializeField, Tooltip("Stand at the starting post while undisturbed. Return here after an alert.")] private bool holdPosition;
    [SerializeField, Min(0.1f)] private float walkSpeed = 1.55f;
    [SerializeField, Min(1f)] private float detectionRange = 16f;
    [SerializeField, Min(1f)] private float preferredDistance = 6f;
    [SerializeField, Min(0f)] private float damage = 5f;
    [SerializeField, Min(0.05f)] private float shotInterval = 0.16f;
    [SerializeField, Min(1)] private int burstSize = 3;
    [SerializeField, Min(0.1f)] private float burstPause = 1.6f;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveZ = Animator.StringToHash("MoveZ");
    private static readonly int Firing = Animator.StringToHash("Firing");
    private static readonly int Hit = Animator.StringToHash("Hit");

    private static readonly int VictimState = Animator.StringToHash("Base Layer.Rear Takedown Victim");
    private static readonly int Locomotion = Animator.StringToHash("Base Layer.Locomotion");
    private IvanStealth takedownOwner;
    public bool AvailableForTakedown => isActiveAndEnabled && !health.IsDead && takedownOwner == null;

    private Animator animator;
    private CharacterController body;
    private CombatHealth health;
    private Transform targetActor;
    private IvanStealth targetStealth;
    private CombatHealth target;
    private CharacterController targetBody;
    private Quaternion rifleRest;
    private int shotsRemaining;
    private float nextShot;
    private float flashUntil;
    private float recoil;
    private float strafeSide = 1f;
    private bool firePose;
    private bool hitPending;
    private int hitFrame;
    private Quaternion? shotFacing;

    public bool HoldsPosition => holdPosition;
    private NPCChaser chaser;
    private bool templePerception;

    public void SetTempleTarget(Transform player)
    {
        templePerception = true;
        targetActor = player;
        target = player.GetComponent<CombatHealth>();
        targetStealth = player.GetComponent<IvanStealth>();
        targetBody = player.GetComponent<CharacterController>();
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        body = GetComponent<CharacterController>();
        health = GetComponent<CombatHealth>();
        rifleRest = rifle.localRotation;
    }

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (targetActor == null && player != null)
        {
            targetActor = player.transform;
            target = player.GetComponent<CombatHealth>();
            targetStealth = player.GetComponent<IvanStealth>();
            targetBody = player.GetComponent<CharacterController>();
        }
        nextShot = Time.time + burstPause;
    }

    private AudioClip alertClip;
    public void SetNavigation(NPCChaser navigation, AudioClip alert)
    {
        chaser = navigation;
        alertClip = alert;
    }

    public void Shout()
    {
        if (alertClip != null) shotAudio.PlayOneShot(alertClip);
    }

    private void OnEnable() => health.onHit.AddListener(OnHit);

    private void OnDisable()
    {
        shotFacing = null;
        takedownOwner = null;
        health.onHit.RemoveListener(OnHit);
        EndBurst();
        muzzleFlash.SetActive(false);
        recoil = 0f;
        rifle.localRotation = rifleRest;
    }

    public bool BeginTakedown(IvanStealth owner, float blendSeconds)
    {
        if (!AvailableForTakedown || !animator.HasState(0, VictimState)) return false;
        takedownOwner = owner;
        shotFacing = null;
        EndBurst();
        muzzleFlash.SetActive(false);
        recoil = 0f;
        animator.ResetTrigger(Hit);
        hitPending = false;
        animator.SetFloat(MoveX, 0f);
        animator.SetFloat(MoveZ, 0f);
        animator.CrossFadeInFixedTime(VictimState, blendSeconds, 0, 0f);
        return true;
    }

    public bool IsPairedWith(IvanStealth owner) => takedownOwner == owner && !health.IsDead;

    public void EndTakedown(IvanStealth owner, bool completed)
    {
        if (takedownOwner != owner) return;
        takedownOwner = null;
        if (health.IsDead) return;
        if (completed)
            health.TakeDamage(health.HP, transform.position, owner.transform.forward, owner.gameObject);
        else if (isActiveAndEnabled)
            animator.CrossFadeInFixedTime(Locomotion, 0.15f, 0);
    }

    private void OnHit()
    {
        EndBurst();
        nextShot = Time.time + burstPause;
        muzzleFlash.SetActive(false);
        flashUntil = 0f;
        recoil = 0f;
        if (health.IsDead)
        {
            animator.ResetTrigger(Hit);
            body.enabled = false;
            return;
        }
        if (takedownOwner != null) return;
        hitFrame = Time.frameCount;
        hitPending = true;
        animator.SetTrigger(Hit);
    }

    public void FaceIncomingShot(Vector3 bulletDirection)
    {
        if (!isActiveAndEnabled || health.IsDead || takedownOwner != null) return;
        Vector3 towardShot = Vector3.ProjectOnPlane(-bulletDirection, Vector3.up);
        if (towardShot.sqrMagnitude > 0.0001f)
            shotFacing = Quaternion.LookRotation(towardShot);
    }

    private void EndBurst()
    {
        shotsRemaining = 0;
        firePose = false;
        animator.SetBool(Firing, false);
    }

    private Vector3 AimPoint => targetBody != null
        ? targetBody.transform.TransformPoint(targetBody.center + Vector3.up * targetBody.height * 0.16f)
        : targetActor.position + Vector3.up;

    private bool CanSeeTarget(Vector3 origin, out RaycastHit hit)
    {
        Vector3 ray = AimPoint - origin;
        return Physics.Raycast(origin, ray.normalized, out hit, ray.magnitude + 0.3f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && hit.transform.IsChildOf(targetActor);
    }

    private void Update()
    {
        if (health.IsDead || takedownOwner != null) return;
        // A hit trigger may be set after this Animator has already evaluated for the frame.
        if (hitPending && Time.frameCount > hitFrame + 1 &&
            !animator.GetCurrentAnimatorStateInfo(0).IsTag("Hit") &&
            !(animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("Hit")))
            hitPending = false;

        Vector2 move = Vector2.zero;
        if (shotFacing.HasValue)
        {
            Quaternion facing = shotFacing.Value;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 360f * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, facing) < 0.1f)
                shotFacing = null;
        }
        else if (!hitPending && targetActor != null && (target == null || !target.IsDead))
        {
            Vector3 delta = Vector3.ProjectOnPlane(targetActor.position - transform.position, Vector3.up);
            float distance = delta.magnitude;
            Vector3 eye = transform.position + Vector3.up * (body.height * 0.75f);
            bool noticed;
            if (templePerception)
            {
                float range = targetStealth != null && targetStealth.IsCrouching ? 5f : 15f;
                noticed = (AimPoint - eye).sqrMagnitude <= range * range &&
                    Vector3.Dot(transform.forward, delta.normalized) >= 0.7071068f && CanSeeTarget(eye, out _);
            }
            else
            {
                noticed = targetStealth == null ||
                    ((Vector3.Dot(transform.forward, delta.normalized) > 0.15f ||
                      (!targetStealth.IsCrouching && distance < 1.8f)) && CanSeeTarget(eye, out _));
            }
            Vector3 steering = chaser != null ? chaser.GetDirection(noticed) : Vector3.zero;
            if (noticed && distance <= detectionRange && distance > 0.001f &&
                (chaser == null || chaser.CurrentState != NPCChaser.State.RushToGrate))
            {
                var facing = Quaternion.LookRotation(delta);
                if (!holdPosition || chaser != null)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 240f * Time.deltaTime);
                bool aligned = Quaternion.Angle(transform.rotation, facing) < 10f;
                if (aligned && target != null && CanSeeTarget(eye, out _))
                    move = Engage(distance);
                else EndBurst();
                if (chaser != null)
                {
                    Vector3 local = transform.InverseTransformDirection(steering);
                    move = distance > 1.1f ? new Vector2(local.x, local.z) : Vector2.zero;
                }
            }
            else
            {
                EndBurst();
                if (steering.sqrMagnitude > 0f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(steering), 240f * Time.deltaTime);
                    Vector3 local = transform.InverseTransformDirection(steering);
                    move = new Vector2(local.x, local.z);
                }
            }
        }
        else EndBurst();

        Vector3 direction = transform.right * move.x + transform.forward * move.y;
        body.SimpleMove(direction * walkSpeed);
        animator.SetFloat(MoveX, move.x, 0.12f, Time.deltaTime);
        animator.SetFloat(MoveZ, move.y, 0.12f, Time.deltaTime);
    }

    private Vector2 Engage(float distance)
    {
        if (!firePose && Time.time >= nextShot)
        {
            firePose = true;
            shotsRemaining = burstSize;
            animator.SetBool(Firing, true);
        }
        if (firePose || holdPosition) return Vector2.zero;
        if (distance > preferredDistance + 1f) return Vector2.up;
        if (distance < preferredDistance - 1f) return Vector2.down;
        return new Vector2(strafeSide, 0f);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!firePose || target == null || target.IsDead || health.IsDead) return;
        // Turn both animated hand goals together around the grip, preserving their spacing.
        rifle.localRotation = rifleRest;
        Vector3 aim = AimPoint - muzzle.position;
        if (aim.sqrMagnitude < 0.001f) return;
        Quaternion correction = Quaternion.FromToRotation(muzzle.forward, aim.normalized);
        Vector3 grip = animator.GetIKPosition(AvatarIKGoal.RightHand);
        Vector3 support = animator.GetIKPosition(AvatarIKGoal.LeftHand);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, grip + correction * (support - grip));
        foreach (var hand in AimHands)
        {
            animator.SetIKRotationWeight(hand, 1f);
            animator.SetIKRotation(hand, correction * animator.GetIKRotation(hand));
        }
    }

    private static readonly AvatarIKGoal[] AimHands = { AvatarIKGoal.RightHand, AvatarIKGoal.LeftHand };

    private void LateUpdate()
    {
        // Cast from the current animated hand, after the Animator has evaluated.
        recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime / 0.12f);
        rifle.localRotation = rifleRest * Quaternion.Euler(-5f * recoil, 0f, 0f);
        if (muzzleFlash.activeSelf && Time.time >= flashUntil) muzzleFlash.SetActive(false);
        if (!firePose || Time.time < nextShot || target == null || target.IsDead || health.IsDead) return;
        if (shotsRemaining == 0)
        {
            EndBurst();
            nextShot = Time.time + burstPause;
            strafeSide = -strafeSide;
            return;
        }
        Vector3 origin = muzzle.position;
        if (CanSeeTarget(origin, out var hit))
        {
            Vector3 direction = (hit.point - origin).normalized;
            target.TakeDamage(damage, hit.point, direction, gameObject);
            shotAudio.PlayOneShot(shotAudio.clip);
            recoil = 1f;
            rifle.localRotation = rifleRest * Quaternion.Euler(-5f, 0f, 0f);
            muzzleFlash.transform.localRotation = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
            muzzleFlash.SetActive(true);
            flashUntil = Time.time + 0.05f;
        }
        shotsRemaining--;
        nextShot = Time.time + shotInterval;
    }
}
