using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-20)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class IvanSidearm : MonoBehaviour
{
    private static readonly int Draw = Animator.StringToHash("Sidearm.Draw");
    private static readonly int Stow = Animator.StringToHash("Sidearm.Stow");
    private static readonly int GestureTime = Animator.StringToHash("SidearmTime");
    private static readonly int ContactWeight = Animator.StringToHash("HolsterContact");
    private static readonly int AimLean = Animator.StringToHash("AimLean");
    private static readonly int Clearance = Animator.StringToHash("HolsterClearance");
    private const float AimStanceYaw = 24f;

    [SerializeField] private Transform holster;
    [SerializeField] private Transform pistolMount;
    [SerializeField] private Transform propDirection;
    [SerializeField] private Transform rightGrip;
    [SerializeField] private Transform leftGrip;
    [SerializeField, Min(0.1f)] private float drawSeconds = 1.15f;
    [SerializeField, Min(0.1f)] private float stowSeconds = 1.4f;

    [SerializeField] private AudioClip shotClip;
    [SerializeField] private GameObject muzzleFlash;
    private float flashUntil;
    private InputAction attack;
    internal bool ShotRequested { get; private set; }
    private IvanStealth stealth;
    private Animator animator;
    private IvanSidearmRig rig;
    private Camera view;
    private InputAction next;
    private InputAction previous;
    private int layer;
    private bool drawing;
    private bool aimRequested;
    private float phase;

    private float aimWeight;
    private float raise;
    public bool IsAiming { get; private set; }
    public bool IsSidearmActive => drawing || phase > 0f;
    public float StanceYaw => AimStanceYaw * aimWeight;
    internal bool IsReadyToFire => isActiveAndEnabled && IsAiming && raise >= .95f;
    internal float ShotRecoil { private get; set; }

    private void Awake()
    {
        stealth = GetComponent<IvanStealth>();
        animator = GetComponent<Animator>();
        rig = new IvanSidearmRig(animator, holster, pistolMount, propDirection, rightGrip, leftGrip);
        layer = animator.GetLayerIndex("Sidearm");
        next = InputSystem.actions?.FindAction("Player/Next");
        attack = InputSystem.actions?.FindAction("Player/Attack");
        previous = InputSystem.actions?.FindAction("Player/Previous");
    }

    private void Update()
    {
        if (muzzleFlash.activeSelf && Time.time >= flashUntil) muzzleFlash.SetActive(false);
        if (stealth == null || !stealth.IsPerforming) ReadInput();
        AdvanceGesture(Time.deltaTime);
        ShotRequested = Time.timeScale > 0f && drawing && phase >= 1f &&
            (stealth == null || !stealth.IsPerforming) && attack?.WasPressedThisFrame() == true;
    }

    private void ReadInput()
    {
        bool requested = drawing;
        if (next?.WasPressedThisFrame() == true) requested = true;
        var mouse = Mouse.current;
        if (mouse?.rightButton.wasPressedThisFrame == true) aimRequested = true;
        bool stow = previous?.WasPressedThisFrame() == true;
        if (stow || mouse?.rightButton.isPressed != true) aimRequested = false;

        bool aiming = aimRequested && (stealth == null || stealth.TryStandForAim());
        if (aiming) requested = true;
        else if (stow || IsAiming) requested = false;
        IsAiming = aiming;
        if (requested != drawing)
        {
            drawing = requested;
            animator.Play(drawing ? Draw : Stow, layer, 0f);
        }
    }

    private void AdvanceGesture(float deltaTime)
    {
        aimWeight = Mathf.MoveTowards(aimWeight, IsAiming ? 1f : 0f, deltaTime * 5f);
        phase = Mathf.MoveTowards(phase, drawing ? 1f : 0f, deltaTime / (drawing ? drawSeconds : stowSeconds));
        raise = aimWeight * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, phase));
        animator.SetFloat(AimLean, raise);
        animator.SetFloat(GestureTime, drawing ? phase : 1f - phase);
        animator.SetLayerWeight(layer, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase / 0.12f)));
    }

    private void LateUpdate()
    {
        Quaternion direction = Quaternion.identity;
        float aim = raise;
        if (aim > 0f)
        {
            if (view == null || !view.isActiveAndEnabled || !view.CompareTag("MainCamera")) view = Camera.main;
            if (view == null) aim = 0f;
            else
            {
                float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, view.transform.eulerAngles.x), -45f, 55f);
                direction = Quaternion.Euler(pitch - ShotRecoil, transform.eulerAngles.y - StanceYaw, 0f);
            }
        }
        rig.Apply(phase, animator.GetFloat(ContactWeight), animator.GetFloat(Clearance), aim, direction);
    }

    internal void ShowShot()
    {
        muzzleFlash.transform.localRotation = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
        muzzleFlash.SetActive(true);
        flashUntil = Time.time + 0.05f;
        if (shotClip != null) AudioSource.PlayClipAtPoint(shotClip, propDirection.position, 0.7f);
    }

    private void OnDisable()
    {
        muzzleFlash.SetActive(false);
        ShotRequested = false;
        IsAiming = false;
        aimRequested = false;
        aimWeight = 0f;
        ShotRecoil = 0f;
        raise = 0f;
        drawing = false;
        phase = 0f;
        if (animator == null) return;
        animator.SetFloat(AimLean, 0f);
        animator.SetLayerWeight(layer, 0f);

        // A fatal hit leaves the prop on the animated hand or holster that currently carries it.
        if (!TryGetComponent<CombatHealth>(out var health) || !health.IsDead) rig.Holster();
    }
}
