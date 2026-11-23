using System;
using UnityEngine;
using UnityEngine.Events;

public interface IDamageable
{
    void TakeDamage(float amount, Vector3 point, Vector3 direction, GameObject source);
}

[DisallowMultipleComponent]
public sealed class CombatHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)] private float maximumHP = 100f;
    [SerializeField] private bool useProceduralHitReaction = true;
    public UnityEvent onHit = new UnityEvent();
    public UnityEvent onDeath = new UnityEvent();
    public float HP { get; private set; }
    public float MaximumHP => maximumHP;
    public bool IsDead => HP <= 0f;
    public event Action<float> Changed;

    private ProceduralHitReaction hitReaction;

    private void Awake()
    {
        HP = Mathf.Max(1f, maximumHP);
        if (useProceduralHitReaction && !TryGetComponent(out hitReaction))
            hitReaction = gameObject.AddComponent<ProceduralHitReaction>();
    }

    internal void RestoreHP(float hp)
    {
        HP = Mathf.Clamp(hp, float.Epsilon, Mathf.Max(1f, maximumHP));
        Changed?.Invoke(HP);
    }

    internal void TakePistolShot(float damage, Vector3 point, Vector3 direction, GameObject shooter)
    {
        float previousHP = HP;
        TakeDamage(damage, point, direction, shooter);
        if (HP < previousHP && !IsDead && TryGetComponent(out RifleNpc guard))
            guard.OnPistolHit(shooter, direction);
    }

    public void TakeDamage(float amount, Vector3 point, Vector3 direction, GameObject source)
    {
        if (IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        HP = Mathf.Max(0f, HP - amount);
        GameAudio.Play(IsDead ? SoundCue.BodyFall : SoundCue.BodyHit, point, IsDead ? 0.63f : 0.46f);
        Changed?.Invoke(HP);
        var movement = GetComponent<IvanMovement>();
        var animator = GetComponent<Animator>();
        var combat = GetComponent<PistolCombat>();
        if (combat != null) combat.CancelReload();
        if (IsDead)
        {
            if (hitReaction != null) hitReaction.StopReaction();
            var aimRig = GetComponent<PistolAimRig>();
            if (aimRig != null) aimRig.enabled = false;
            // Death is authored in Base Layer; upper-body layers must not override it.
            if (animator != null)
                for (int i = 1; i < animator.layerCount; i++) animator.SetLayerWeight(i, 0f);
            CombatAnimation.Trigger(animator, "Die");
        }
        else if (hitReaction != null) hitReaction.Kick();
        onHit.Invoke();
        if (!IsDead) return;
        if (movement != null) movement.StopForDeath();
        onDeath.Invoke();
    }
}

public static class CombatAnimation
{
    public static void SetBool(Animator animator, string name, bool value)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var parameter in animator.parameters)
            if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Bool)
                animator.SetBool(parameter.nameHash, value);
    }

    public static bool BlocksAim(Animator animator)
    {
        return HasActiveTag(animator, "CombatBlock") || HasActiveTag(animator, "Reload");
    }

    public static bool HasActiveTag(Animator animator, string tag)
    {
        if (animator == null) return false;
        for (int i = 0; i < animator.layerCount; i++)
            if (animator.GetLayerWeight(i) > 0f &&
                (animator.GetCurrentAnimatorStateInfo(i).IsTag(tag) ||
                (animator.IsInTransition(i) && animator.GetNextAnimatorStateInfo(i).IsTag(tag))))
                return true;
        return false;
    }

    public static void Trigger(Animator animator, string name)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        int hash = Animator.StringToHash(name);
        foreach (var parameter in animator.parameters)
            if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(hash);
                return;
            }
    }
}
