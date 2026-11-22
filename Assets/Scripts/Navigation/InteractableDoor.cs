using System;
using System.Linq;
using UnityEngine;

// Состоянием управляет действующий механизм двери, включая замки, анимацию и ввод E.
public interface IGraphPassage
{
    bool IsPassable { get; }
    event Action PassabilityChanged;
}

[DisallowMultipleComponent]
public sealed class InteractableDoor : MonoBehaviour
{
    [Tooltip("Узел перед проёмом. Его список Edges должен содержать переход к To.")]
    [SerializeField] private Node from;
    [Tooltip("Узел по другую сторону этого же проёма.")]
    [SerializeField] private Node to;
    [SerializeField] private bool twoWay = true;
    [Tooltip("Компонент физической двери: TempleShortDoor, TemplePassage или M0GateMechanism.")]
    [SerializeField] private MonoBehaviour mechanism;
    private IGraphPassage passage;
    private GraphManager graph;
    private Edge forward, reverse;

    public void Bind(GraphManager manager)
    {
        if (passage != null) passage.PassabilityChanged -= Synchronize;
        graph = manager;
        passage = mechanism as IGraphPassage;
        forward = UniqueEdge(from, to);
        reverse = twoWay ? UniqueEdge(to, from) : null;
        if (from == null || to == null || from == to || from.gameObject.scene != manager.gameObject.scene ||
            to.gameObject.scene != manager.gameObject.scene || forward == null || (twoWay && reverse == null) || passage == null)
        {
            Debug.LogError($"Дверь {name}: проверьте механизм и уникальные связи {from?.name} → {to?.name}.", this);
            // Ошибка настройки не должна сделать закрытый проход доступным.
            BlockMatching(from, to);
            if (twoWay) BlockMatching(to, from);
            passage = null;
            graph.NotifyPassageChanged();
            return;
        }
        passage.PassabilityChanged += Synchronize;
        Synchronize();
    }

    private static Edge UniqueEdge(Node source, Node target)
    {
        if (source == null || target == null) return null;
        var matches = source.edges.Where(edge => edge?.target == target).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static void BlockMatching(Node source, Node target)
    {
        if (source == null || target == null) return;
        foreach (Edge edge in source.edges)
            if (edge?.target == target) { edge.isDoor = true; edge.isOpen = false; }
    }

    private void Synchronize()
    {
        bool open = passage.IsPassable;
        bool changed = !forward.isDoor || forward.isOpen != open ||
            (reverse != null && (!reverse.isDoor || reverse.isOpen != open));
        forward.isDoor = true;
        forward.isOpen = open;
        if (reverse != null) { reverse.isDoor = true; reverse.isOpen = open; }
        if (changed) graph.NotifyPassageChanged();
    }

    private void OnDestroy()
    {
        if (passage != null) passage.PassabilityChanged -= Synchronize;
    }
}
