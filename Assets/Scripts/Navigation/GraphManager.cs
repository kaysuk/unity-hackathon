using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GraphManager : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float anchorRange = 7f;
    [SerializeField, Min(0.05f)] private float floorTolerance = 0.65f;
    private readonly List<Node> nodes = new List<Node>();
    private float minimumCostPerMetre;
    public event Action PassageChanged;

    public void CollectNodes()
    {
        nodes.Clear();
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
    public void NotifyPassageChanged() => PassageChanged?.Invoke();

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
