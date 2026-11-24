using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CombatHealth), typeof(Animator))]
public sealed class IvanDamageResponse : MonoBehaviour
{
    private static readonly int Hit = Animator.StringToHash("Hit");
    [SerializeField] private Material bloodFrameMaterial;
    private Animator animator;
    private CombatHealth health;
    private Canvas damageCanvas;
    private RawImage bloodFrame;
    private bool didStart;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<CombatHealth>();
    }

    private void OnEnable()
    {
        health.Changed += ShowWounds;
        if (didStart) ShowWounds(health.HP);
        health.onHit.AddListener(OnHit);
        health.onDeath.AddListener(OnDeath);
    }

    private void Start()
    {
        didStart = true;
        ShowWounds(health.HP);
    }

    private void OnDisable()
    {
        health.Changed -= ShowWounds;
        if (damageCanvas != null) damageCanvas.enabled = false;
        health.onHit.RemoveListener(OnHit);
        health.onDeath.RemoveListener(OnDeath);
    }

    private void OnDestroy()
    {
        if (damageCanvas != null) Destroy(damageCanvas.gameObject);
    }

    private void ShowWounds(float hp)
    {
        float severity = Mathf.Clamp01(1f - hp / health.MaximumHP);
        if (severity <= 0f || bloodFrameMaterial == null)
        {
            if (damageCanvas != null) damageCanvas.enabled = false;
            return;
        }

        if (damageCanvas == null)
        {
            var root = new GameObject("Ivan Damage Overlay", typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            damageCanvas = root.GetComponent<Canvas>();
            damageCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            damageCanvas.sortingOrder = -10;

            bloodFrame = new GameObject("Blood Frame", typeof(RawImage)).GetComponent<RawImage>();
            var rect = bloodFrame.rectTransform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            bloodFrame.raycastTarget = false;
            bloodFrame.material = bloodFrameMaterial;
        }
        bloodFrame.color = new Color(1f, 1f, 1f, severity);
        damageCanvas.enabled = true;
    }

    private void OnHit()
    {
        if (health.IsDead) return;
        animator.SetTrigger(Hit);
        if (gameObject.CompareTag("Player") && gameObject.scene == SceneManager.GetActiveScene())
            StorySpeech.Say(health.HP <= health.MaximumHP * .3f ? "ivan.critical" : "ivan.hurt", false);
    }

    private void OnDeath()
    {
        animator.ResetTrigger(Hit);
        DisableControl<IvanLocomotion>();
        DisableControl<IvanStanding>();
        DisableControl<IvanStealth>();
        DisableControl<IvanSidearm>();
        DisableControl<PlayerInteractor>();
        if (TryGetComponent<CharacterController>(out var capsule)) capsule.enabled = false;
    }

    private void DisableControl<T>() where T : Behaviour
    {
        if (TryGetComponent<T>(out var control)) control.enabled = false;
    }
}
