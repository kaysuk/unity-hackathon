using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CombatHealth), typeof(Animator))]
public sealed class IvanDamageResponse : MonoBehaviour
{
    private static readonly int Hit = Animator.StringToHash("Hit");
    private Animator animator;
    private CombatHealth health;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<CombatHealth>();
    }

    private void OnEnable()
    {
        health.onHit.AddListener(OnHit);
        health.onDeath.AddListener(OnDeath);
    }

    private void OnDisable()
    {
        health.onHit.RemoveListener(OnHit);
        health.onDeath.RemoveListener(OnDeath);
    }

    private void OnHit()
    {
        if (!health.IsDead) animator.SetTrigger(Hit);
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
