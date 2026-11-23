using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GraphManager : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float anchorRange = 7f;
    [SerializeField, Min(0.05f)] private float floorTolerance = 0.65f;
    [SerializeField, Tooltip("Volume covering the full stair shaft, including the air between steps.")]
    private Bounds silentStairwell;
    private readonly List<Node> nodes = new List<Node>();
    private float minimumCostPerMetre;
    public event Action PassageChanged;

    public void CollectNodes()
    {
        nodes.Clear();
        soundLevels.Clear();
        minimumCostPerMetre = 1f;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            nodes.AddRange(root.GetComponentsInChildren<Node>());
        foreach (Node node in nodes)
            foreach (Edge edge in node.edges)
            {
                if (edge?.target == null || edge.cost < 0f) continue;
                float length = Vector3.Distance(node.transform.position, edge.target.transform.position);
                if (length > 0f) minimumCostPerMetre = Mathf.Min(minimumCostPerMetre, edge.cost / length);
            }
    }

    public List<Node> FindPath(Node start, Node goal) => Pathfinding.FindPath(start, goal, minimumCostPerMetre);
    public event Action<NPCChaser, Node> Alarm;
    public void RaiseAlarm(NPCChaser caller, Node room) => Alarm?.Invoke(caller, room);

    public List<Node> ApproachRoute(Node start, Node goal, out bool blocked)
    {
        var route = FindPath(start, goal);
        blocked = route == null;
        if (!blocked) return route;
        route = Pathfinding.FindPath(start, goal, minimumCostPerMetre, true);
        if (route == null) return null;
        for (int i = 1; i < route.Count; i++)
        {
            Node next = route[i];
            if (route[i - 1].edges.Exists(e => e.target == next && e.GetCost() < float.MaxValue)) continue;
            route.RemoveRange(i, route.Count - i);
            break;
        }
        return route;
    }

    private readonly Dictionary<Node, Dictionary<Node, float>> soundLevels = new();

    private int silentStairsMask;

    private void Awake() => silentStairsMask = LayerMask.GetMask("SilentStairs");

    public Node FootstepSource(Vector3 feet, CharacterController body, CharacterController player)
    {
        if (silentStairwell.size.y > 0f && silentStairwell.Contains(transform.InverseTransformPoint(feet)))
            return null;

        // The capsule can still be supported by a tread after its centre has crossed the edge.
        float radius = Mathf.Min(player.bounds.extents.x, player.bounds.extents.z);
        Vector3 sole = feet + Vector3.up * radius;
        Vector3 below = sole - Vector3.up * (player.stepOffset + player.skinWidth);
        if (Physics.CheckCapsule(below, sole, radius, silentStairsMask, QueryTriggerInteraction.Ignore))
            return null;
        return SoundAnchor(feet, body, player);
    }

    public Node SoundAnchor(Vector3 feet, CharacterController body, CharacterController player)
    {
        // Furnishings can block a capsule cast to a waypoint without changing the acoustic room.
        foreach (Node node in nodes)
            if (node.ContainsFootstep(feet)) return node;
        return FindAnchor(feet, body, player);
    }

    public float FootstepLevel(Node source, Node listener)
    {
        if (source == null || listener == null) return 0f;
        if (!soundLevels.TryGetValue(source, out var levels))
        {
            levels = new Dictionary<Node, float> { [source] = 1f };
            var pending = new Queue<Node>();
            pending.Enqueue(source);
            while (pending.Count > 0)
            {
                var node = pending.Dequeue();
                foreach (var edge in node.edges)
                {
                    if (edge?.target == null) continue;
                    bool boundary = edge.isDoor || node.AcousticRoom != edge.target.AcousticRoom;
                    float gain = boundary ? (edge.isDoor && !edge.isOpen ? 0.2f : 0.5f) : 1f;
                    float level = levels[node] * gain;
                    if (levels.TryGetValue(edge.target, out float previous) && previous >= level) continue;
                    levels[edge.target] = level;
                    pending.Enqueue(edge.target);
                }
            }
            soundLevels[source] = levels;
        }
        return levels.TryGetValue(listener, out float heard) ? heard : 0f;
    }

    public void NotifyPassageChanged()
    {
        soundLevels.Clear();
        PassageChanged?.Invoke();
    }

    // Привязка к полу и проверка свободного пути до точки исключают выбор соседней комнаты через стену.
    public Node FindAnchor(Vector3 feet, CharacterController body, CharacterController player)
    {
        Node closest = null;
        float best = anchorRange * anchorRange;
        foreach (Node node in nodes)
        {
            if (node == null || !node.isActiveAndEnabled) continue;
            Vector3 delta = node.transform.position - feet;
            float squared = delta.sqrMagnitude;
            if (squared > best || Mathf.Abs(delta.y) > floorTolerance ||
                !ClearApproach(feet, node.transform.position, body, player)) continue;
            best = squared;
            closest = node;
        }
        return closest;
    }

    public static bool ClearApproach(Vector3 feet, Vector3 end, CharacterController npc, CharacterController player)
    {
        // Капсула начинается над полом, чтобы её нижняя полусфера не цепляла стыки плит.
        Vector3 delta = end - feet;
        if (delta.sqrMagnitude < 0.0001f) return true;
        Bounds bounds = npc.bounds;
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        Vector3 bottom = feet + Vector3.up * (radius + 0.10f);
        Vector3 top = feet + Vector3.up * Mathf.Max(radius + 0.10f, bounds.size.y - radius);
        foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius, delta.normalized,
                     delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (hit.collider != npc && hit.collider != player &&
                !hit.transform.IsChildOf(npc.transform) && (player == null || !hit.transform.IsChildOf(player.transform)))
                return false;
        return true;
    }
}
