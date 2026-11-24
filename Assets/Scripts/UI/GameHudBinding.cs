using UnityEngine;

// Keep the view independent of the actor and the systems that supply its values.
[RequireComponent(typeof(GameHud))]
[DisallowMultipleComponent]
internal sealed class GameHudBinding : MonoBehaviour
{
    private GameHud view;
    private GameObject player;
    private CombatHealth health;
    private PistolCombat pistol;
    private float previousHP;
    private float damageSourceSide;
    private bool loading;

    private void Awake()
    {
        view = GetComponent<GameHud>();
        view.SetGameplayVisible(false);
        GameObjectives.Changed += RefreshObjectives;
        GameBindings.Changed += RefreshObjectives;
    }

    internal void Bind(GameObject actor)
    {
        if (health != null)
        {
            health.Changed -= RefreshHealth;
            health.DamageReceived -= RememberDamageSource;
        }

        if (player != actor) view.ClearBlood();
        damageSourceSide = 0f;
        player = actor;
        view.SetInventoryHint(actor != null && actor.TryGetComponent<InventoryMenu>(out _));
        health = actor != null ? actor.GetComponent<CombatHealth>() : null;
        pistol = actor != null ? actor.GetComponent<PistolCombat>() : null;
        previousHP = health != null ? health.HP : 1f;
        view.SetHealth(previousHP, health != null ? health.MaximumHP : 1f, false);
        if (health != null)
        {
            health.DamageReceived += RememberDamageSource;
            health.Changed += RefreshHealth;
        }

        RefreshObjectives();
        LateUpdate();
    }

    internal void SetLoading(bool value)
    {
        loading = value;
        LateUpdate();
    }

    private void RememberDamageSource(GameObject source)
    {
        var camera = Camera.main;
        damageSourceSide = source != null && camera != null
            ? Vector3.Dot(camera.transform.right, (source.transform.position - player.transform.position).normalized)
            : 0f;
    }

    private void RefreshHealth(float hp)
    {
        view.SetHealth(hp, health.MaximumHP, hp < previousHP, damageSourceSide);
        damageSourceSide = 0f;
        previousHP = hp;
    }

    private void RefreshObjectives() => view.SetObjectives(GameObjectives.Primary, GameObjectives.Secondary);

    private void LateUpdate()
    {
        view.SetGameplayVisible(player != null && !loading && Time.timeScale > 0f);
        GameAudio.Heartbeat(!loading && health != null && !health.IsDead && health.HP <= health.MaximumHP * 0.3f);
        bool hasPistol = pistol != null && pistol.isActiveAndEnabled;
        view.SetAmmo(hasPistol ? pistol.Ammo : 0, hasPistol ? pistol.ReserveAmmo : 0,
            hasPistol && pistol.IsReloading, hasPistol && pistol.ShowsAmmo);
    }

    private void OnDestroy()
    {
        GameObjectives.Changed -= RefreshObjectives;
        GameBindings.Changed -= RefreshObjectives;
        if (health != null)
        {
            health.Changed -= RefreshHealth;
            health.DamageReceived -= RememberDamageSource;
        }
    }
}
