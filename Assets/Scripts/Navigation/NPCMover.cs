using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
public sealed class NPCMover : MonoBehaviour
{
    [SerializeField, Min(0.02f)] private float arrivalRadius = 0.16f;
    [SerializeField, Min(0.02f)] private float verticalTolerance = 0.38f;
    private List<Node> route;
    private int index;
    private CharacterController body;

    public Node LastReached { get; private set; }
    public Node Next => route != null && index < route.Count ? route[index] : null;
    public bool AtGoal => route != null && index == route.Count;
    public bool HasRoute => route != null;
    public Vector3 Feet => FootPosition(body);

    public static Vector3 FootPosition(CharacterController capsule)
    {
        Bounds bounds = capsule.bounds;
        return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }

    private void Awake() => body = GetComponent<CharacterController>();

    public void SetRoute(List<Node> path, bool firstAlreadyReached = false)
    {
        route = path;
        index = 0;
        if (path == null) { LastReached = null; return; }
        if (firstAlreadyReached && path.Count > 0 && path[0] == LastReached) index = 1;
        else LastReached = null;
    }

    // Только выдаём направление существующему RifleNpc. Он один вызывает SimpleMove,
    // применяет гравитацию, поворачивает модель и передаёт скорость в Animator.
    public Vector3 Direction()
    {
        while (Next != null)
        {
            Vector3 delta = Next.transform.position - Feet;
            Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (flat.sqrMagnitude > arrivalRadius * arrivalRadius || Mathf.Abs(delta.y) > verticalTolerance)
                return flat.normalized;
            LastReached = Next;
            index++;
        }
        return Vector3.zero;
    }
}
