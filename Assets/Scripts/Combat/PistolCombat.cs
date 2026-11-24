using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Update feeds recoil before rig evaluation; LateUpdate fires from the evaluated muzzle.
[DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
public sealed class PistolCombat : MonoBehaviour
{
    [SerializeField] private Transform muzzle;
    [SerializeField] private ShoulderCamera aimCamera;
    [SerializeField, Min(0f)] private float damage = 25f;
    [SerializeField, Min(1f)] private float range = 100f;
    [SerializeField, Min(0.01f)] private float shotInterval = 0.18f;
    [SerializeField, Min(0f)] private float recoilDegrees = 1.5f;
    [SerializeField, Range(0f, 20f)] private float armKickDegrees = 5f;
    [SerializeField, Min(0.01f)] private float armReturnTime = 0.12f;
    private InputAction reload;
    private InputAction attack;
    private PistolWeaponAimConstraint weaponAim;
    private float armRecoil;
    [SerializeField, Min(1)] private int magazineCapacity = 8;
    [SerializeField, Min(0)] private int startingTotalAmmo = 24;
    [SerializeField, Min(0.1f)] private float reloadDuration = 2f;
    public int MagazineCapacity => magazineCapacity;
    public int Ammo { get; private set; }
    public int ReserveAmmo { get; private set; }
    public bool IsReloading { get; private set; }
    private float reloadEnds;
    internal float ReloadProgress => Mathf.Clamp01(1f - (reloadEnds - Time.time) / Mathf.Max(0.1f, reloadDuration));

    private void Awake()
    {
        magazineCapacity = Mathf.Max(1, magazineCapacity);
        int total = Mathf.Max(0, startingTotalAmmo);
        Ammo = Mathf.Min(magazineCapacity, total);
        ReserveAmmo = total - Ammo;
    }

    public void CancelReload()
    {
        IsReloading = false;
        CombatAnimation.SetBool(animator, "IsReload", false);
    }
    [SerializeField] private LayerMask hitLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private Material effectMaterial;
    private IvanMovement movement;
    private IvanSidearm sidearm;
    private Camera viewCamera;
    private IvanOrbitCamera orbitCamera;
    private CombatHealth health;
    private Animator animator;
    private Material runtimeMaterial;
    private float nextShot;
    private float hitMarkerUntil;
    private bool wasCaptured;
    private static readonly int Armed = Animator.StringToHash("Base Layer.PistolLocomotion");

    private void Start()
    {
        reload = InputSystem.actions?.FindAction("Player/Reload");
        attack = InputSystem.actions?.FindAction("Player/Attack");
        movement = GetComponent<IvanMovement>();
        sidearm = GetComponent<IvanSidearm>();
        health = GetComponent<CombatHealth>();
        animator = GetComponent<Animator>();
        weaponAim = GetComponentInChildren<PistolWeaponAimConstraint>();
        if (GetComponent<PistolShotAfterPose>() == null) gameObject.AddComponent<PistolShotAfterPose>();
        if (sidearm != null)
            ResolveViewCamera();
        else if (aimCamera == null && Camera.main != null)
            aimCamera = Camera.main.GetComponent<ShoulderCamera>();
        if (muzzle == null)
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (child.name == "Muzzle") { muzzle = child; break; }
        if (effectMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) effectMaterial = runtimeMaterial = new Material(shader);
        }
        if (animator == null || muzzle == null)
        {
            Debug.LogError("PistolCombat requires Animator and Muzzle.", this);
            enabled = false;
            return;
        }
        if (sidearm != null)
            return;
        bool hasReload = false;
        foreach (var parameter in animator.parameters)
            hasReload |= parameter.name == "IsReload" && parameter.type == AnimatorControllerParameterType.Bool;
        if (!hasReload || movement == null || aimCamera == null)
        {
            Debug.LogError("Legacy PistolCombat requires IvanMovement, ShoulderCamera and Animator Bool IsReload.", this);
            enabled = false;
        }
    }

    private void ResolveViewCamera()
    {
        Camera camera = Camera.main;
        if (camera == null || camera.gameObject.scene != gameObject.scene)
        {
            foreach (Camera candidate in Camera.allCameras)
            {
                if (!candidate.CompareTag("MainCamera") || candidate.gameObject.scene != gameObject.scene)
                    continue;
                camera = candidate;
                break;
            }
        }
        viewCamera = camera;
        orbitCamera = camera != null ? camera.GetComponent<IvanOrbitCamera>() : null;
    }

    private bool Ready
    {
        get
        {
            if (health != null && health.IsDead)
                return false;
            if (sidearm != null)
                return sidearm.IsReadyToFire && viewCamera != null && viewCamera.isActiveAndEnabled &&
                    !CombatAnimation.HasActiveTag(animator, "Hit");
            return movement != null && movement.isActiveAndEnabled && movement.IsAiming &&
                aimCamera != null && aimCamera.isActiveAndEnabled &&
                animator.GetCurrentAnimatorStateInfo(0).fullPathHash == Armed && !animator.IsInTransition(0);
        }
    }

    private void CompleteReload()
    {
        int transferred = Mathf.Min(Mathf.Max(0, magazineCapacity - Ammo), ReserveAmmo);
        if (transferred > 0) GameAudio.Play(SoundCue.MagazineIn, transform.position, 0.48f);
        Ammo += transferred;
        ReserveAmmo -= transferred;
        CancelReload();
    }

    private void Update()
    {
        if (sidearm != null && (viewCamera == null || !viewCamera.isActiveAndEnabled))
            ResolveViewCamera();
        if (IsReloading)
        {
            if (!Ready) CancelReload();
            else if (Time.time >= reloadEnds)
            {
                CompleteReload();
            }
        }
        // Sync the kick before RigBuilder evaluates aim, then left-hand IK.
        if (weaponAim != null)
        {
            var data = weaponAim.data;
            data.recoilAngle = armRecoil;
            weaponAim.data = data;
        }
        if (sidearm != null)
            sidearm.ShotRecoil = armRecoil;
        armRecoil = Mathf.MoveTowards(armRecoil, 0f,
            Mathf.Max(1f, armKickDegrees) / Mathf.Max(0.01f, armReturnTime) * Time.deltaTime);
    }

    public void EvaluateShotAfterPose()
    {
        if (Time.timeScale <= 0f || ControlBindingsPanel.BlocksGameplay) return;
        bool captured = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        if (captured && reload?.WasPressedThisFrame() == true)
            TryStartReload();
        bool shooting = sidearm != null ? sidearm.ShotRequested :
            captured && wasCaptured && attack?.WasPressedThisFrame() == true;
        if (shooting && Time.timeScale > 0f && Ready && !IsReloading &&
            !CombatAnimation.BlocksAim(animator) && Time.time >= nextShot)
        {
            if (Ammo > 0) Fire();
            else GameAudio.Play(SoundCue.DryFire, muzzle.position, 0.32f);
        }
        wasCaptured = captured;
    }

    internal bool TryStartReload()
    {
        if (!isActiveAndEnabled || IsReloading || Time.timeScale <= 0f || !Ready ||
            Ammo >= magazineCapacity || ReserveAmmo <= 0 || CombatAnimation.BlocksAim(animator))
            return false;

        IsReloading = true;
        reloadEnds = Time.time + Mathf.Max(0.1f, reloadDuration);
        GameAudio.Play(SoundCue.MagazineOut, transform.position, 0.41f);
        CombatAnimation.SetBool(animator, "IsReload", true);
        return true;
    }

    private bool Cast(Ray ray, float distance, out RaycastHit nearest)
    {
        nearest = default;
        bool found = false;
        foreach (var hit in Physics.RaycastAll(ray, distance, hitLayers.value | Physics.IgnoreRaycastLayer, QueryTriggerInteraction.Collide))
        {
            if (hit.transform.IsChildOf(transform)) continue;
            var zones = hit.collider.GetComponentInParent<SwatHitZones>();
            if (hit.collider.isTrigger || (hitLayers.value & (1 << hit.collider.gameObject.layer)) == 0)
            {
                if (zones == null || !zones.Contains(hit.collider)) continue;
            }
            else if (zones != null && hit.collider is CharacterController) continue;
            if (!found || hit.distance < nearest.distance) { nearest = hit; found = true; }
        }
        return found;
    }

    private bool IsMuzzleObstructed()
    {
        // An overlap catches origins inside geometry; the sweep catches a barrel
        // that has passed completely through a thin wall.
        foreach (var collider in Physics.OverlapSphere(muzzle.position, 0.025f, hitLayers,
            QueryTriggerInteraction.Ignore))
            if (!collider.transform.IsChildOf(transform)) return true;
        var controller = GetComponent<CharacterController>();
        Vector3 anchor = controller != null ? transform.TransformPoint(controller.center) :
            transform.position + transform.up;
        Vector3 segment = muzzle.position - anchor;
        return segment.sqrMagnitude > 0.000001f &&
            Cast(new Ray(anchor, segment.normalized), segment.magnitude, out _);
    }

    private void Fire()
    {
        if (IsMuzzleObstructed()) return;
        if (sidearm != null) sidearm.ShowShot();
        else GameAudio.PistolShot(muzzle.position);
        Ammo--;
        // Count the reserve as well: an empty magazine alone only calls for a reload.
        int roundsLeft = Ammo + ReserveAmmo;
        if (roundsLeft == 3)
            StoryGameplayVoice.FromPlayer(this, gameObject, "combat.low_ammo");
        else if (roundsLeft == 0)
            StoryGameplayVoice.FromPlayer(this, gameObject, "combat.empty_ammo");
        nextShot = Time.time + Mathf.Max(0.01f, shotInterval);
        Ray cameraRay = sidearm != null
            ? viewCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0f))
            : aimCamera.GetAimRay();
        Vector3 target = Cast(cameraRay, range, out var cameraHit) ? cameraHit.point : cameraRay.GetPoint(range);
        Vector3 origin = muzzle.position;
        Vector3 delta = target - origin;
        Vector3 end = target;
        if (delta.sqrMagnitude > 0.000001f && Cast(new Ray(origin, delta.normalized),
            Mathf.Min(range, delta.magnitude + 0.01f), out var hit))
        {
            end = hit.point;
            var zones = hit.collider.GetComponentInParent<SwatHitZones>();
            if (zones != null && zones.Contains(hit.collider))
            {
                zones.ApplyShot(hit, delta.normalized, gameObject);
                hitMarkerUntil = Time.unscaledTime + 0.15f;
            }
            else foreach (var component in hit.collider.GetComponentsInParent<MonoBehaviour>())
                if (component is IDamageable receiver)
                {
                    if (receiver is CombatHealth health)
                        health.TakePistolShot(damage, hit.point, delta.normalized, gameObject);
                    else
                        receiver.TakeDamage(damage, hit.point, delta.normalized, gameObject);
                    hitMarkerUntil = Time.unscaledTime + 0.15f;
                    break;
                }
            if (hit.collider.GetComponentInParent<CombatHealth>() == null)
                GameAudio.Play(SoundCue.WallHit, hit.point, 0.58f);
            ShowImpact(hit.point, hit.normal);
        }
        StartCoroutine(Tracer(origin, end));
        CombatAnimation.Trigger(animator, "Fire");
        if (sidearm == null)
            aimCamera.AddRecoil(recoilDegrees);
        else if (orbitCamera != null)
            orbitCamera.AddRecoil(recoilDegrees);
        armRecoil = Mathf.Min(20f, armRecoil + armKickDegrees);
    }

    private IEnumerator Tracer(Vector3 start, Vector3 end)
    {
        if (effectMaterial == null) yield break;
        var effect = new GameObject("Shot tracer");
        var line = effect.AddComponent<LineRenderer>();
        line.sharedMaterial = effectMaterial;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = line.endWidth = 0.012f;
        line.startColor = line.endColor = new Color(1f, 0.8f, 0.3f);
        // Fallback cleanup also works if the shooter is disabled before coroutine resumes.
        Destroy(effect, 0.1f);
        yield return null;
        // Do not render the same shot again from the recoiling camera's next pose.
        if (effect != null)
        {
            line.enabled = false;
            Destroy(effect);
        }
    }

    private void ShowImpact(Vector3 point, Vector3 normal)
    {
        if (effectMaterial == null) return;
        var effect = new GameObject("Shot impact");
        var marker = effect.AddComponent<LineRenderer>();
        marker.sharedMaterial = effectMaterial;
        marker.useWorldSpace = false;
        marker.loop = true;
        marker.positionCount = 12;
        marker.startWidth = marker.endWidth = 0.008f;
        marker.startColor = marker.endColor = Color.red;
        effect.transform.SetPositionAndRotation(point + normal * 0.005f,
            Quaternion.LookRotation(normal));
        for (int i = 0; i < marker.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / marker.positionCount;
            marker.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.02f);
        }
        Destroy(effect, 0.25f);
    }

    public bool ShowsCrosshair => Ready && Time.timeScale > 0f &&
        Application.isFocused && Cursor.lockState == CursorLockMode.Locked;

    internal bool ShowsAmmo => health == null || !health.IsDead;

    internal void RestoreAmmo(int magazine, int reserve, int capacity)
    {
        magazineCapacity = Mathf.Max(1, capacity);
        Ammo = Mathf.Clamp(magazine, 0, magazineCapacity);
        ReserveAmmo = Mathf.Max(0, reserve);
        CancelReload();
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || !ShowsCrosshair) return;
        Color old = GUI.color;
        GUI.color = Time.unscaledTime < hitMarkerUntil ? Color.red : Color.white;
        float x = Screen.width * 0.5f, y = Screen.height * 0.5f;
        GUI.DrawTexture(new Rect(x - 9, y - 1, 6, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + 3, y - 1, 6, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 1, y - 9, 2, 6), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 1, y + 3, 2, 6), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void OnDisable()
    {
        CancelReload();
        wasCaptured = false;
        armRecoil = 0f;
        if (sidearm != null)
            sidearm.ShotRecoil = 0f;
        if (weaponAim != null)
        {
            var data = weaponAim.data;
            data.recoilAngle = 0f;
            weaponAim.data = data;
        }
    }
    private void OnDestroy() { if (runtimeMaterial != null) Destroy(runtimeMaterial); }
}
