using System.Collections.Generic;
using UnityEngine;

public static class Pathfinding
{
    public static List<Node> FindPath(Node start, Node goal) => FindPath(start, goal, 0f);

    // lowerBound — минимальная цена одного метра в этом графе. Даже при ручных ценах
    // ниже физического расстояния эвристика не завышает оставшуюся стоимость.
    public static List<Node> FindPath(Node start, Node goal, float lowerBound)
    {
        if (start == null || goal == null || start.gameObject.scene != goal.gameObject.scene) return null;
        var open = new List<Node> { start };
        var parents = new Dictionary<Node, Node>();
        var cost = new Dictionary<Node, float> { [start] = 0f };
        var estimate = new Dictionary<Node, float> { [start] = 0f };
        while (open.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < open.Count; i++)
                if (estimate[open[i]] < estimate[open[best]]) best = i;
            Node current = open[best];
            open.RemoveAt(best);
            if (current == goal)
            {
                var result = new List<Node> { current };
                while (parents.TryGetValue(current, out current)) result.Add(current);
                result.Reverse();
                return result;
            }
            foreach (Edge edge in current.edges)
            {
                if (edge?.target == null || edge.target.gameObject.scene != start.gameObject.scene ||
                    !edge.target.isActiveAndEnabled || edge.GetCost() == float.MaxValue) continue;
                Node next = edge.target;
                float total = cost[current] + edge.cost;
                if (cost.TryGetValue(next, out float known) && total >= known) continue;
                parents[next] = current;
                cost[next] = total;
                estimate[next] = total + lowerBound * Vector3.Distance(next.transform.position, goal.transform.position);
                if (!open.Contains(next)) open.Add(next);
            }
        }
        return null;
    }
}
