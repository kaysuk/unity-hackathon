using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
internal sealed class PlayerInvestigationSense : MonoBehaviour
{
    [SerializeField, Min(1f)] private float radius = 12f;

    private readonly List<Interactable> nearby = new();
    private readonly InvestigationHighlight highlight = new();
    private CharacterController body;
    private CombatHealth health;
    private InputAction investigate;
    private bool showing;

    private void Awake()
    {
        body = GetComponent<CharacterController>();
        TryGetComponent(out health);
    }

    // Both locomotion rigs read the current input before moving, even on the first held frame.
    internal bool IsHeld
    {
        get
        {
            var actions = InputSystem.actions;
            if (investigate == null || !ReferenceEquals(investigate.actionMap?.asset, actions))
                investigate = actions?.FindAction("Player/Investigate");

            return isActiveAndEnabled && investigate != null && investigate.IsPressed() &&
                Time.timeScale > 0f && !GameCheckpoint.IsLoading &&
                gameObject.scene == SceneManager.GetActiveScene() && (health == null || !health.IsDead);
        }
    }

    private void LateUpdate()
    {
        if (Application.isFocused && IsHeld && TryGetComponent(out PlayerInteractor player) && player.isActiveAndEnabled)
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                CollectNearby(player);
                highlight.Show(camera, nearby);
                showing = true;
                return;
            }
        }
        Clear();
    }

    private void CollectNearby(PlayerInteractor player)
    {
        nearby.Clear();
        Vector3 origin = transform.TransformPoint(body.center);
        float radiusSquared = radius * radius;
        Scene scene = gameObject.scene;
        var objects = Interactable.Active;
        for (int i = 0; i < objects.Count; i++)
        {
            Interactable item = objects[i];
            if (item.gameObject.scene == scene && (item.SensePosition - origin).sqrMagnitude <= radiusSquared &&
                item.CanInteract(player))
                nearby.Add(item);
        }
    }

    private void Clear()
    {
        if (!showing) return;
        highlight.Clear();
        nearby.Clear();
        showing = false;
    }

    private void OnDisable() => Clear();
    private void OnDestroy() => highlight.Dispose();
}
