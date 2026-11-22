using UnityEngine;
using UnityEngine.Animations.Rigging;

// Camera input -> movement -> rig controls -> RigBuilder sync -> animation evaluation.
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(IvanMovement))]
public sealed class PistolAimRig : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private Rig weaponRig;
    [Tooltip("Pitch-only target used by BodyAim.")]
    [SerializeField] private Transform aimTarget;
    [Tooltip("Separate target for PistolWeaponAimConstraint; never assign the BodyAim target here.")]
    [SerializeField] private Transform weaponAimTarget;
    [SerializeField] private ShoulderCamera aimCamera;

    [Header("Aim point")]
    [SerializeField, Min(1f)] private float aimDistance = 100f;
    [SerializeField, Min(0.1f)] private float minimumAimDistance = 2f;
    [SerializeField] private LayerMask aimLayers = Physics.DefaultRaycastLayers;

    [Header("Vertical body aim")]
    [SerializeField, Min(0f)] private float bodyAimHeight = 1.5f;
    [SerializeField] private Vector2 bodyPitchLimits = new Vector2(-45f, 45f);
    [SerializeField, Min(1f)] private float bodyPitchSpeed = 180f;

    // The true camera hit point is kept separately for subsequent weapon alignment.
    public Vector3 AimPoint { get; private set; }
    private float bodyPitch;
    private bool hasBodyPitch;

    [Header("Rig blending")]
    [SerializeField, Min(0.01f)] private float blendInTime = 0.15f;
    [SerializeField, Min(0.01f)] private float blendOutTime = 0.08f;

    private static readonly int PistolLocomotionId = Animator.StringToHash("Base Layer.PistolLocomotion");
    private Animator animator;
    private IvanMovement movement;
    private PistolCombat combat;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<IvanMovement>();
        combat = GetComponent<PistolCombat>();
        if (aimCamera == null && Camera.main != null)
            aimCamera = Camera.main.GetComponent<ShoulderCamera>();

        if (weaponRig == null || aimTarget == null || aimCamera == null)
        {
            Debug.LogError("PistolAimRig requires Weapon Rig, Aim Target and a ShoulderCamera.", this);
            enabled = false;
            return;
        }

        if (weaponAimTarget != null && weaponAimTarget == aimTarget)
        {
            Debug.LogError("BodyAim and weapon aim require different target objects.", this);
            enabled = false;
            return;
        }
        weaponRig.weight = 0f;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f) return;

        UpdateAimTarget();
        bool inStance = animator.GetCurrentAnimatorStateInfo(0).fullPathHash == PistolLocomotionId;
        bool leavingStance = animator.IsInTransition(0) &&
            animator.GetNextAnimatorStateInfo(0).fullPathHash != PistolLocomotionId;
        bool aiming = movement.isActiveAndEnabled && movement.IsAiming && inStance && !leavingStance &&
            (combat == null || !combat.IsReloading) && !CombatAnimation.BlocksAim(animator);
        float blendTime = aiming ? blendInTime : blendOutTime;
        weaponRig.weight = Mathf.MoveTowards(weaponRig.weight, aiming ? 1f : 0f,
            Time.deltaTime / Mathf.Max(0.01f, blendTime));
    }

    private void UpdateAimTarget()
    {
        Ray ray = aimCamera.GetAimRay();
        float distance = Mathf.Max(aimDistance, minimumAimDistance);
        foreach (var hit in Physics.RaycastAll(ray, distance, aimLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform))
                distance = Mathf.Min(distance, hit.distance);
        }

        AimPoint = ray.GetPoint(distance);
        if (weaponAimTarget != null)
        {
            // The visible hit point may be behind the hands when the camera is close
            // to a wall. Do not force the gun backward to reach such a point.
            Vector3 weaponPoint = ray.GetPoint(Mathf.Max(minimumAimDistance, distance));
            Vector3 pivot = transform.position + Vector3.up * bodyAimHeight;
            Vector3 localDirection = transform.InverseTransformDirection(weaponPoint - pivot);
            float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(localDirection.y,
                new Vector2(localDirection.x, localDirection.z).magnitude) * Mathf.Rad2Deg;
            // Keep the target in front while the body catches up with a rapid camera turn.
            yaw = Mathf.Clamp(yaw, -60f, 60f);
            pitch = Mathf.Clamp(pitch, -75f, 75f);
            Vector3 weaponDirection = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
            weaponAimTarget.position = pivot + transform.TransformDirection(weaponDirection) *
                Mathf.Max(1f, localDirection.magnitude);
        }

        // BodyAim controls pitch only. A camera target behind a slowly turning body
        // makes Multi-Aim's shortest rotation flip; keep this target ahead of the body.
        float requestedPitch = Mathf.Asin(Mathf.Clamp(ray.direction.y, -1f, 1f)) * Mathf.Rad2Deg;
        requestedPitch = Mathf.Clamp(requestedPitch,
            Mathf.Clamp(bodyPitchLimits.x, -80f, 80f),
            Mathf.Clamp(Mathf.Max(bodyPitchLimits.x, bodyPitchLimits.y), -80f, 80f));
        bodyPitch = hasBodyPitch
            ? Mathf.MoveTowards(bodyPitch, requestedPitch, bodyPitchSpeed * Time.deltaTime)
            : requestedPitch;
        hasBodyPitch = true;

        float radians = bodyPitch * Mathf.Deg2Rad;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 direction = forward * Mathf.Cos(radians) + Vector3.up * Mathf.Sin(radians);
        Vector3 origin = transform.position + Vector3.up * bodyAimHeight;
        aimTarget.position = origin + direction * Mathf.Max(10f, minimumAimDistance);
    }

    private void OnDisable()
    {
        if (weaponRig != null) weaponRig.weight = 0f;
        hasBodyPitch = false;
    }
}
