using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(NPCMover))]
public sealed class NPCChaser : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.35f;
    private GraphManager graph;
    private Transform player;
    private CharacterController playerBody, body;
    private NPCMover mover;
    private Node goal;
    private float nextCheck;
    public bool Ready => isActiveAndEnabled && graph != null && player != null;

    public void Initialize(GraphManager manager, Transform target)
    {
        Unsubscribe();
        graph = manager;
        player = target;
        body = GetComponent<CharacterController>();
        playerBody = player.GetComponent<CharacterController>();
        mover = GetComponent<NPCMover>();
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable() { if (graph != null) Subscribe(); }
    private void OnDisable() => Unsubscribe();
    private void Subscribe()
    {
        graph.PassageChanged += Replan;
        Replan();
    }
    private void Unsubscribe() { if (graph != null) graph.PassageChanged -= Replan; }

    private Vector3 PlayerFeet => playerBody != null ? NPCMover.FootPosition(playerBody) : player.position;

    private void Update()
    {
        if (!Ready || !body.enabled || Time.time < nextCheck) return;
        Node current = graph.FindAnchor(PlayerFeet, body, playerBody);
        if (current != goal || !mover.HasRoute) Plan(current);
        nextCheck = Time.time + refreshInterval;
    }

    private void Replan()
    {
        if (Ready && body.enabled) Plan(graph.FindAnchor(PlayerFeet, body, playerBody));
        nextCheck = Time.time + refreshInterval;
    }

    private void Plan(Node destination)
    {
        goal = destination;
        Node last = mover.LastReached;
        Node next = mover.Next;
        if (last != null && next != null && PlanFromSegment(last, next)) return;

        // Закрытие прохода может застать NPC по любую сторону створки.
        // Новый начальный узел должен быть физически доступен с текущей позиции.
        Node start = graph.FindAnchor(mover.Feet, body, playerBody);
        mover.SetRoute(graph.FindPath(start, destination));
    }

    private bool PlanFromSegment(Node last, Node next)
    {
        Edge forward = last.edges.Find(edge => edge?.target == next);
        if (forward == null || forward.GetCost() == float.MaxValue) return false;
        Edge reverse = next.edges.Find(edge => edge?.target == last);
        var onward = graph.FindPath(next, goal);
        var back = reverse != null && reverse.GetCost() < float.MaxValue ? graph.FindPath(last, goal) : null;
        Vector3 segment = next.transform.position - last.transform.position;
        float progress = segment.sqrMagnitude > 0f
            ? Mathf.Clamp01(Vector3.Dot(mover.Feet - last.transform.position, segment) / segment.sqrMagnitude) : 1f;

        // Сравниваем оставшуюся стоимость с обоих концов ребра. На одностороннем ребре
        // продолжение вперёд — единственный вариант. При равенстве сохраняем направление.
        if (onward != null && (back == null || RouteCost(onward) + forward.cost * (1f - progress)
                                      <= RouteCost(back) + reverse.cost * progress))
        {
            onward.Insert(0, last);
            mover.SetRoute(onward, true);
        }
        else mover.SetRoute(back);
        return true;
    }

    private static float RouteCost(List<Node> route)
    {
        float cost = 0f;
        for (int i = 1; i < route.Count; i++)
        {
            Node targetNode = route[i];
            float least = float.MaxValue;
            foreach (Edge edge in route[i - 1].edges)
                if (edge?.target == targetNode) least = Mathf.Min(least, edge.GetCost());
            cost += least;
        }
        return cost;
    }

    public Vector3 GetDirection()
    {
        Vector3 direction = mover.Direction();
        if (!mover.AtGoal || !GraphManager.ClearApproach(mover.Feet, PlayerFeet, body, playerBody))
            return direction;
        Vector3 delta = Vector3.ProjectOnPlane(PlayerFeet - mover.Feet, Vector3.up);
        return delta.sqrMagnitude > 0.25f ? delta.normalized : Vector3.zero;
    }
    public bool AtGoal => mover.AtGoal;
}
