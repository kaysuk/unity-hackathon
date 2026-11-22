using UnityEngine;

[RequireComponent(typeof(Animator), typeof(CharacterController), typeof(CombatHealth))]
[DisallowMultipleComponent]
public sealed class RifleNpc : MonoBehaviour
{
    [SerializeField] private Transform rifle;
    [SerializeField] private Transform muzzle;
    [SerializeField] private GameObject muzzleFlash;
    [SerializeField] private AudioSource shotAudio;
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

    private Animator animator;
    private CharacterController body;
    private CombatHealth health;
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

    private NPCChaser chaser;

    public void SetChaser(NPCChaser value, Transform player)
    {
        chaser = value;
        target = player.GetComponent<CombatHealth>();
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
        var player = target == null ? FindAnyObjectByType<IvanMovement>() : null;
        if (player != null)
        {
            target = player.GetComponent<CombatHealth>();
            targetBody = player.GetComponent<CharacterController>();
        }
        nextShot = Time.time + burstPause;
    }

    private void OnEnable() => health.onHit.AddListener(OnHit);

    private void OnDisable()
    {
        health.onHit.RemoveListener(OnHit);
        EndBurst();
        muzzleFlash.SetActive(false);
        recoil = 0f;
        rifle.localRotation = rifleRest;
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
        hitFrame = Time.frameCount;
        hitPending = true;
        animator.SetTrigger(Hit);
    }

    private void EndBurst()
    {
        shotsRemaining = 0;
        firePose = false;
        animator.SetBool(Firing, false);
    }

    private Vector3 AimPoint => targetBody != null
        ? targetBody.transform.TransformPoint(targetBody.center + Vector3.up * targetBody.height * 0.16f)
        : target.transform.position + Vector3.up;

    private bool CanSeeTarget(Vector3 origin, out RaycastHit hit)
    {
        Vector3 ray = AimPoint - origin;
        return Physics.Raycast(origin, ray.normalized, out hit, ray.magnitude + 0.3f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<CombatHealth>() == target;
    }

    private void Update()
    {
        if (health.IsDead) return;
        // A hit trigger may be set after this Animator has already evaluated for the frame.
        if (hitPending && Time.frameCount > hitFrame + 1 &&
            !animator.GetCurrentAnimatorStateInfo(0).IsTag("Hit") &&
            !(animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("Hit")))
            hitPending = false;

        Vector2 move = Vector2.zero;
        if (!hitPending && target != null && !target.IsDead)
        {
            Vector3 delta = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
            float distance = delta.magnitude;
            bool navigate = chaser != null && chaser.Ready;
            bool inRange = distance <= detectionRange && distance > 0.001f;
            bool visible = inRange && CanSeeTarget(transform.position + Vector3.up * (body.height * 0.75f), out _);
            if (visible || (inRange && !navigate))
            {
                var facing = Quaternion.LookRotation(delta);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 240f * Time.deltaTime);
                if (visible && Quaternion.Angle(transform.rotation, facing) < 10f) move = Engage(distance);
                else EndBurst();
            }
            else EndBurst();

            if (navigate && (!chaser.AtGoal || !visible))
            {
                // Граф управляет только ходьбой. Стрелок может целиться и стрелять на маршруте,
                // а оглушение, смерть и неподвижная стойка очереди по-прежнему имеют приоритет.
                Vector3 steering = firePose ? Vector3.zero : chaser.GetDirection();
                if (!visible && steering.sqrMagnitude > 0f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(steering), 240f * Time.deltaTime);
                Vector3 local = transform.InverseTransformDirection(steering);
                move = new Vector2(local.x, local.z);
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
        if (firePose) return Vector2.zero;
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
