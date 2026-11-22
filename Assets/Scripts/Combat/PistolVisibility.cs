using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class PistolVisibility : MonoBehaviour
{
    [SerializeField] private Transform weaponRoot;
    [Tooltip("Fraction of the draw animation after which the gun becomes visible.")]
    [SerializeField, Range(0f, 1f)] private float showOnDraw = 0.25f;
    [Tooltip("Fraction of the holster animation after which the gun is hidden.")]
    [SerializeField, Range(0f, 1f)] private float hideOnHolster = 0.75f;

    private static readonly int DrawId = Animator.StringToHash("Base Layer.PistolUp");
    private static readonly int HolsterId = Animator.StringToHash("Base Layer.PistolDown");
    private static readonly int ArmedId = Animator.StringToHash("Base Layer.PistolLocomotion");

    private Animator animator;
    private Renderer[] weaponRenderers;
    private bool[] initialEnabled;
    private int trackedState;
    private float previousNormalizedTime;
    private float actionProgress;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (weaponRoot == null || weaponRoot == transform || !weaponRoot.IsChildOf(transform))
        {
            Debug.LogError("PistolVisibility: assign the Glock child object to Weapon Root.", this);
            enabled = false;
            return;
        }

        weaponRenderers = weaponRoot.GetComponentsInChildren<Renderer>(true);
        initialEnabled = new bool[weaponRenderers.Length];
        for (int i = 0; i < weaponRenderers.Length; i++)
            initialEnabled[i] = weaponRenderers[i].enabled;

        SetVisible(false);
    }

    private void LateUpdate()
    {
        // Read after Animator evaluation, including the first frame of an incoming state.
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (IsWeaponAction(next) || !IsWeaponAction(state))
                state = next;
        }

        if (trackedState != state.fullPathHash)
        {
            trackedState = state.fullPathHash;
            previousNormalizedTime = state.normalizedTime;
            actionProgress = 0f;
        }
        else
        {
            // Count actual playback travel instead of inferring the entry point from
            // the sign of normalizedTime. Reverse states can start at different offsets.
            actionProgress = Mathf.Clamp01(actionProgress +
                Mathf.Abs(state.normalizedTime - previousNormalizedTime));
            previousNormalizedTime = state.normalizedTime;
        }

        bool visible;
        if (state.fullPathHash == DrawId)
            visible = actionProgress >= showOnDraw;
        else if (state.fullPathHash == HolsterId)
            visible = actionProgress < hideOnHolster;
        else
            visible = state.fullPathHash == ArmedId;

        SetVisible(visible);
    }

    private static bool IsWeaponAction(AnimatorStateInfo state)
    {
        return state.fullPathHash == DrawId || state.fullPathHash == HolsterId;
    }

    private void SetVisible(bool visible)
    {
        for (int i = 0; i < weaponRenderers.Length; i++)
        {
            if (weaponRenderers[i] != null)
                weaponRenderers[i].enabled = visible && initialEnabled[i];
        }
    }

    private void OnDisable()
    {
        // Only renderers are affected; Muzzle, grip targets and rig bindings stay active.
        trackedState = 0;
        actionProgress = 0f;
        previousNormalizedTime = 0f;
        if (weaponRenderers == null) return;
        for (int i = 0; i < weaponRenderers.Length; i++)
        {
            if (weaponRenderers[i] != null)
                weaponRenderers[i].enabled = initialEnabled[i];
        }
    }
}
