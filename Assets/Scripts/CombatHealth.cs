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

    public void TakeDamage(float amount, Vector3 point, Vector3 direction, GameObject source)
    {
        if (IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        HP = Mathf.Max(0f, HP - amount);
        Changed?.Invoke(HP);
        var animator = GetComponent<Animator>();
        if (IsDead)
        {
            if (hitReaction != null) hitReaction.StopReaction();
            // Death is authored in Base Layer; upper-body layers must not override it.
            if (animator != null)
                for (int i = 1; i < animator.layerCount; i++) animator.SetLayerWeight(i, 0f);
            TriggerDeath(animator);
        }
        else if (hitReaction != null) hitReaction.Kick();
        onHit.Invoke();
        if (!IsDead) return;
        onDeath.Invoke();
    }

    private static void TriggerDeath(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        int hash = Animator.StringToHash("Die");
        foreach (var parameter in animator.parameters)
            if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(hash);
                return;
            }
    }
}
