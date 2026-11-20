using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerInteractor : MonoBehaviour
{
    private const float AimToleranceDegrees = 18f;

    [SerializeField] private ShoulderCamera viewCamera;
    [SerializeField, Min(0.1f)] private float reach = 2f;

    private readonly RaycastHit[] raycastHits = new RaycastHit[16];
    private readonly Collider[] nearbyColliders = new Collider[32];
    private CharacterController controller;
    private PistolCombat pistolCombat;
    private InputAction interact;
    private Canvas promptCanvas;
    private UnityEngine.UI.Text promptLabel;
    private bool bindingLabelDirty = true;
    private string buttonPrompt;

    public CharacterController Controller => controller;
    public GameObject CameraObject => viewCamera.gameObject;
    public PlayerInventory Inventory { get; private set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        pistolCombat = GetComponent<PistolCombat>();
        Inventory = GetComponent<PlayerInventory>();
        interact = InputSystem.actions?.FindAction("Player/Interact");

        if (viewCamera == null || interact == null)
        {
            Debug.LogError("Проверь поле View Camera и действие Player/Interact.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        bindingLabelDirty = true;
        InputSystem.onActionChange += OnActionChange;
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= OnActionChange;
        InputSystem.onDeviceChange -= OnDeviceChange;
        UpdatePrompt(null);
    }

    private void OnDestroy()
    {
        if (promptCanvas != null)
            Destroy(promptCanvas.gameObject);
    }

    private void LateUpdate()
    {
        Interactable target = CanTarget ? FindTargetInReach() : null;

        if (target != null && target.CanInteract(this) && interact.WasPressedThisDynamicUpdate())
            target.Interact(this);

        UpdatePrompt(target);
    }

    private bool CanTarget => interact != null && interact.enabled && viewCamera != null &&
        Time.timeScale > 0f && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || !CanTarget ||
            (pistolCombat != null && pistolCombat.isActiveAndEnabled && pistolCombat.ShowsCrosshair))
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
        Ray cameraRay = viewCamera.GetAimRay();
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
            if (candidate == null || candidate is ItemPickup || !candidate.isActiveAndEnabled)
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

    private void OnActionChange(object changed, InputActionChange change)
    {
        if (change == InputActionChange.BoundControlsChanged &&
            (ReferenceEquals(changed, interact) || ReferenceEquals(changed, interact.actionMap) ||
                ReferenceEquals(changed, interact.actionMap.asset)))
        {
            bindingLabelDirty = true;
        }
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        bindingLabelDirty = true;
    }

    private void UpdatePrompt(Interactable target)
    {
        if (target != null && bindingLabelDirty)
            RefreshPromptText();

        string text = target != null ? target.GetPrompt(this, buttonPrompt) : null;
        bool visible = !string.IsNullOrEmpty(text);
        if (visible && promptLabel == null)
            CreatePrompt();

        if (promptLabel == null)
            return;

        if (visible && promptLabel.text != text)
            promptLabel.text = text;
        if (promptLabel.enabled != visible)
            promptLabel.enabled = visible;
    }

    private void RefreshPromptText()
    {
        var names = new List<string>();
        var bindings = interact.bindings;
        foreach (InputControl control in interact.controls)
        {
            int index = interact.GetBindingIndexForControl(control);
            if (index < 0)
                continue;

            while (index > 0 && bindings[index].isPartOfComposite)
                index--;

            string name = interact.GetBindingDisplayString(
                index, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                names.Add(name);
        }

        buttonPrompt = names.Count > 0 ? $"Нажмите {string.Join(" / ", names)}" : string.Empty;
        bindingLabelDirty = false;
    }

    private void CreatePrompt()
    {
        var root = new GameObject("Interaction Prompt", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        if (root.scene != gameObject.scene)
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);

        promptCanvas = root.GetComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var labelObject = new GameObject("Binding Label", typeof(RectTransform),
            typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.Outline));
        labelObject.transform.SetParent(root.transform, false);

        promptLabel = labelObject.GetComponent<UnityEngine.UI.Text>();
        promptLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        promptLabel.fontSize = 28;
        promptLabel.fontStyle = FontStyle.Bold;
        promptLabel.alignment = TextAnchor.MiddleCenter;
        promptLabel.color = Color.white;
        promptLabel.raycastTarget = false;
        promptLabel.supportRichText = false;

        var rect = promptLabel.rectTransform;
        rect.anchorMin = new Vector2(0.1f, 0.38f);
        rect.anchorMax = new Vector2(0.9f, 0.38f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 48f);

        var outline = labelObject.GetComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }
}
