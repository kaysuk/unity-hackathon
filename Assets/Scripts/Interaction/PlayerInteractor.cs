using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerInteractor : MonoBehaviour
{
    private const float AimToleranceDegrees = 18f;

    [SerializeField] private Camera viewCamera;
    [SerializeField, Min(0.1f)] private float reach = 2f;

    private readonly RaycastHit[] raycastHits = new RaycastHit[16];
    private readonly Collider[] nearbyColliders = new Collider[32];
    private CharacterController controller;
    private InputAction interact;
    private InteractionPrompt prompt;
    private float nextLatchRattle;

    public CharacterController Controller => controller;
    public GameObject CameraObject => viewCamera.gameObject;
    public PlayerInventory Inventory { get; private set; }

    private void OnEnable()
    {
        controller = GetComponent<CharacterController>();
        if (viewCamera == null) viewCamera = Camera.main;
        Inventory = GetComponent<PlayerInventory>();
        if (viewCamera == null)
        {
            Debug.LogError("Проверь поле View Camera.", this);
            enabled = false;
            return;
        }

        interact = InputSystem.actions?.FindAction("Player/Interact");
        prompt = new InteractionPrompt();
    }

    private void OnDisable()
    {
        prompt?.Dispose();
        prompt = null;
        interact = null;
    }

    private void LateUpdate()
    {
        // The Input System can restore project actions after this component is enabled during a reload.
        var actions = InputSystem.actions;
        if (interact == null || !ReferenceEquals(interact.actionMap?.asset, actions))
            interact = actions?.FindAction("Player/Interact");

        if (CanTarget)
            UpdateTargetInReach();
        else
            prompt.Hide();
    }

    private void UpdateTargetInReach()
    {
        Interactable target = FindTargetInReach();

        if (target != null && interact.WasPressedThisDynamicUpdate())
        {
            if (target.CanInteract(this))
                target.Interact(this);
            else if (target.RattlesWhenBlocked && Time.unscaledTime >= nextLatchRattle)
            {
                GameAudio.Play(SoundCue.Locked, target.transform.position, 0.44f);
                nextLatchRattle = Time.unscaledTime + 0.8f;
            }
        }

        prompt?.Show(this, target, interact);
    }

    private bool CanTarget => interact != null && interact.enabled && viewCamera != null &&
        Time.timeScale > 0f && !ControlBindingsPanel.BlocksGameplay && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || !CanTarget)
            return;
        if (TryGetComponent<PistolCombat>(out var pistolCombat) &&
            pistolCombat.isActiveAndEnabled && pistolCombat.ShowsCrosshair)
            return;

        float x = Screen.width * 0.5f;
        float y = Screen.height * 0.5f;
        GUI.DrawTexture(new Rect(x - 3f, y - 3f, 6f, 6f), Texture2D.whiteTexture,
            ScaleMode.StretchToFill, true, 0f, Color.black, 0f, 3f);
        GUI.DrawTexture(new Rect(x - 2f, y - 2f, 4f, 4f), Texture2D.whiteTexture,
            ScaleMode.StretchToFill, true, 0f, Color.white, 0f, 2f);
    }

    private Interactable FindTargetInReach()
    {
        Vector3 ivanPosition = transform.TransformPoint(controller.center);
        Ray cameraRay = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float cameraDistance = Vector3.Distance(cameraRay.origin, ivanPosition) + reach;

        Interactable target = FindVisibleTarget(cameraRay, cameraDistance, ivanPosition);
        return target != null ? target : FindNearbyTarget(cameraRay, ivanPosition);
    }

    private Interactable FindNearbyTarget(Ray cameraRay, Vector3 ivanPosition)
    {
        Collider[] colliders = nearbyColliders;
        int count = Physics.OverlapSphereNonAlloc(ivanPosition, reach, colliders,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (count == colliders.Length)
        {
            colliders = Physics.OverlapSphere(ivanPosition, reach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            count = colliders.Length;
        }

        float bestAlignment = Mathf.Cos(AimToleranceDegrees * Mathf.Deg2Rad);
        Interactable bestTarget = null;
        for (int i = 0; i < count; i++)
        {
            Collider collider = colliders[i];
            Interactable candidate = collider.GetComponentInParent<Interactable>();
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.RequiresDirectAim)
                continue;

            Vector3 toCenter = collider.bounds.center - cameraRay.origin;
            float distance = toCenter.magnitude;
            if (distance < 0.001f)
                continue;

            Vector3 direction = toCenter / distance;
            float alignment = Vector3.Dot(cameraRay.direction, direction);
            if (alignment < bestAlignment)
                continue;

            if (FindVisibleTarget(new Ray(cameraRay.origin, direction), distance, ivanPosition) == candidate)
            {
                bestTarget = candidate;
                bestAlignment = alignment;
            }
        }

        return bestTarget;
    }

    private Interactable FindVisibleTarget(Ray cameraRay, float cameraDistance, Vector3 ivanPosition)
    {
        if (!TryGetFirstHit(cameraRay, cameraDistance, out RaycastHit cameraHit))
            return null;

        Interactable target = cameraHit.collider.GetComponentInParent<Interactable>();
        if (target == null || !target.isActiveAndEnabled)
            return null;

        Vector3 toTarget = cameraHit.point - ivanPosition;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget > reach || distanceToTarget < 0.001f)
            return null;

        Ray ivanRay = new Ray(ivanPosition, toTarget / distanceToTarget);
        if (TryGetFirstHit(ivanRay, distanceToTarget + 0.01f, out RaycastHit ivanHit)
            && ivanHit.collider.GetComponentInParent<Interactable>() == target)
        {
            return target;
        }

        return null;
    }

    private bool TryGetFirstHit(Ray ray, float distance, out RaycastHit firstHit)
    {
        RaycastHit[] hits = raycastHits;
        int count = Physics.RaycastNonAlloc(
            ray, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        // A full buffer can leave out the nearest hit.
        if (count == hits.Length)
        {
            hits = Physics.RaycastAll(
                ray, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        firstHit = default;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                firstHit = hit;
            }
        }

        return firstHit.collider != null;
    }
}
