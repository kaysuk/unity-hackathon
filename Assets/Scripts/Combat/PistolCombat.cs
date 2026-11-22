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
    private PistolWeaponAimConstraint weaponAim;
    private float armRecoil;
    [SerializeField, Min(1)] private int magazineCapacity = 8;
    [SerializeField, Min(0)] private int startingTotalAmmo = 24;
    [SerializeField, Min(0.1f)] private float reloadDuration = 2f;
    public int Ammo { get; private set; }
    public int ReserveAmmo { get; private set; }
    public bool IsReloading { get; private set; }
    private float reloadEnds;

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
    private CombatHealth health;
    private Animator animator;
    private Material runtimeMaterial;
    private float nextShot;
    private float hitMarkerUntil;
    private bool wasCaptured;
    private GUIStyle ammoStyle;
    private GUIStyle ammoDetailStyle;
    private static readonly int Armed = Animator.StringToHash("Base Layer.PistolLocomotion");

    private void Start()
    {
        movement = GetComponent<IvanMovement>();
        health = GetComponent<CombatHealth>();
        animator = GetComponent<Animator>();
        weaponAim = GetComponentInChildren<PistolWeaponAimConstraint>();
        if (GetComponent<PistolShotAfterPose>() == null) gameObject.AddComponent<PistolShotAfterPose>();
        bool hasReload = false;
        if (animator != null)
            foreach (var parameter in animator.parameters)
                hasReload |= parameter.name == "IsReload" && parameter.type == AnimatorControllerParameterType.Bool;
        if (!hasReload)
        {
            Debug.LogError("PistolCombat requires Animator Bool IsReload.", this);
            enabled = false;
            return;
        }
        if (aimCamera == null && Camera.main != null)
            aimCamera = Camera.main.GetComponent<ShoulderCamera>();
        if (muzzle == null)
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (child.name == "Muzzle") { muzzle = child; break; }
        if (effectMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) effectMaterial = runtimeMaterial = new Material(shader);
        }
        if (movement == null || animator == null || muzzle == null || aimCamera == null)
        {
            Debug.LogError("PistolCombat requires player movement, Animator, Muzzle and ShoulderCamera.", this);
            enabled = false;
        }
    }

    private bool Ready => movement != null && movement.isActiveAndEnabled && movement.IsAiming &&
        (health == null || !health.IsDead) && aimCamera != null && aimCamera.isActiveAndEnabled &&
        animator.GetCurrentAnimatorStateInfo(0).fullPathHash == Armed && !animator.IsInTransition(0);

    private void CompleteReload()
    {
        int transferred = Mathf.Min(Mathf.Max(0, magazineCapacity - Ammo), ReserveAmmo);
        Ammo += transferred;
        ReserveAmmo -= transferred;
        CancelReload();
    }

    private void Update()
    {
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
        armRecoil = Mathf.MoveTowards(armRecoil, 0f,
            Mathf.Max(1f, armKickDegrees) / Mathf.Max(0.01f, armReturnTime) * Time.deltaTime);
    }

    public void EvaluateShotAfterPose()
    {
        bool captured = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        if (captured && Time.timeScale > 0f && Ready && !IsReloading &&
            !CombatAnimation.BlocksAim(animator) && Ammo < magazineCapacity && ReserveAmmo > 0 &&
            Keyboard.current?.rKey.wasPressedThisFrame == true)
        {
            IsReloading = true;
            reloadEnds = Time.time + Mathf.Max(0.1f, reloadDuration);
            CombatAnimation.SetBool(animator, "IsReload", true);
        }
        if (captured && wasCaptured && Time.timeScale > 0f && Ready && !IsReloading && Ammo > 0 &&
            !CombatAnimation.BlocksAim(animator) && Time.time >= nextShot &&
            Mouse.current?.leftButton.wasPressedThisFrame == true)
            Fire();
        wasCaptured = captured;
    }

    private bool Cast(Ray ray, float distance, out RaycastHit nearest)
    {
        nearest = default;
        bool found = false;
        foreach (var hit in Physics.RaycastAll(ray, distance, hitLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform)) continue;
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
        Ammo--;
        nextShot = Time.time + Mathf.Max(0.01f, shotInterval);
        Ray cameraRay = aimCamera.GetAimRay();
        Vector3 target = Cast(cameraRay, range, out var cameraHit) ? cameraHit.point : cameraRay.GetPoint(range);
        Vector3 origin = muzzle.position;
        Vector3 delta = target - origin;
        Vector3 end = target;
        if (delta.sqrMagnitude > 0.000001f && Cast(new Ray(origin, delta.normalized),
            Mathf.Min(range, delta.magnitude + 0.01f), out var hit))
        {
            end = hit.point;
            foreach (var component in hit.collider.GetComponentsInParent<MonoBehaviour>())
                if (component is IDamageable receiver)
                {
                    receiver.TakeDamage(damage, hit.point, delta.normalized, gameObject);
                    hitMarkerUntil = Time.unscaledTime + 0.15f;
                    break;
                }
            ShowImpact(hit.point, hit.normal);
        }
        StartCoroutine(Tracer(origin, end));
        CombatAnimation.Trigger(animator, "Fire");
        aimCamera.AddRecoil(recoilDegrees);
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

    private void OnGUI()
    {
        if (health != null)
            GUI.Label(new Rect(20, Screen.height - 45, 220, 30), $"HP: {health.HP:0} / {health.MaximumHP:0}");
        if (Ready) DrawAmmoHUD();
        if (!ShowsCrosshair) return;
        Color old = GUI.color;
        GUI.color = Time.unscaledTime < hitMarkerUntil ? Color.red : Color.white;
        float x = Screen.width * 0.5f, y = Screen.height * 0.5f;
        GUI.DrawTexture(new Rect(x - 9, y - 1, 6, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + 3, y - 1, 6, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 1, y - 9, 2, 6), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 1, y + 3, 2, 6), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void DrawAmmoHUD()
    {
        if (ammoStyle == null)
        {
            ammoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            ammoDetailStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleRight
            };
        }
        float width = Mathf.Min(320f, Screen.width - 32f);
        float x = Screen.width - width - 16f;
        float y = Screen.height - 132f;
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(x, y, width, 112f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 16f, y + 6f, width - 32f, 48f),
            $"{Ammo} / {magazineCapacity}", ammoStyle);
        GUI.Label(new Rect(x + 16f, y + 54f, width - 32f, 26f),
            $"Запас: {ReserveAmmo}", ammoDetailStyle);
        GUI.Label(new Rect(x + 16f, y + 80f, width - 32f, 26f),
            IsReloading ? "Перезарядка…" : "R — перезарядить", ammoDetailStyle);
        GUI.color = old;
    }

    private void OnDisable()
    {
        CancelReload();
        wasCaptured = false;
        armRecoil = 0f;
        if (weaponAim != null)
        {
            var data = weaponAim.data;
            data.recoilAngle = 0f;
            weaponAim.data = data;
        }
    }
    private void OnDestroy() { if (runtimeMaterial != null) Destroy(runtimeMaterial); }
}
