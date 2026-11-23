using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class Node : MonoBehaviour
{
    public string nodeName;
    [SerializeField] private int acousticRoom;
    public int AcousticRoom => acousticRoom;
    [SerializeField] private BoxCollider roomFloor;

    public bool ContainsFootstep(Vector3 feet)
    {
        if (roomFloor == null) return false;
        Bounds floor = roomFloor.bounds;
        return Mathf.Abs(feet.y - floor.max.y) < 0.7f && feet.x >= floor.min.x && feet.x <= floor.max.x &&
            feet.z >= floor.min.z && feet.z <= floor.max.z;
    }
    public List<Edge> edges = new List<Edge>();
    [SerializeField] private Node neighbour;

    [ContextMenu("AddOneWayEdge")]
    private void AddOneWayFromInspector() => ConnectNeighbour(false);

    [ContextMenu("AddTwoWayEdge")]
    private void AddTwoWayFromInspector() => ConnectNeighbour(true);

    private void ConnectNeighbour(bool twoWay)
    {
        if (neighbour == null || neighbour == this || neighbour.gameObject.scene != gameObject.scene)
        {
            Debug.LogWarning("Выберите соседний узел этой сцены.", this);
            return;
        }
#if UNITY_EDITOR
        Undo.RecordObjects(new Object[] { this, neighbour }, "Connect navigation nodes");
#endif
        float distance = Vector3.Distance(transform.position, neighbour.transform.position);
        if (twoWay) AddTwoWayEdge(neighbour, distance);
        else AddOneWayEdge(neighbour, distance);
#if UNITY_EDITOR
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(neighbour);
#endif
    }

    public void AddOneWayEdge(Node target, float cost) => Add(target, cost, true, false);

    public void AddTwoWayEdge(Node target, float cost)
    {
        AddOneWayEdge(target, cost);
        if (target != null) target.AddOneWayEdge(this, cost);
    }

    // Сохраняет семантику исходного API. Для двусторонней физической двери используйте
    // AddTwoWayEdge и InteractableDoor: сторону открывания уже проверяет сама дверь.
    public void AddDoorEdge(Node target, float cost, bool fromSideOpenable)
    {
        Add(target, cost, fromSideOpenable, true);
        if (target != null) target.Add(this, cost, !fromSideOpenable, true);
    }

    private void Add(Node target, float cost, bool allowed, bool door)
    {
        if (target == null || target == this || target.gameObject.scene != gameObject.scene ||
            edges.Exists(edge => edge != null && edge.target == target)) return;
        edges.Add(new Edge { target = target, cost = cost, canEnter = allowed, isDoor = door, isOpen = !door });
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.12f);
        foreach (Edge edge in edges)
        {
            if (edge?.target == null) continue;
            Gizmos.color = edge.GetCost() < float.MaxValue ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, edge.target.transform.position);
        }
    }
}
