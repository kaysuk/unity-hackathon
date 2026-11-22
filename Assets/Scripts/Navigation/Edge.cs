using System;

[Serializable]
public class Edge
{
    public Node target;
    public float cost;
    public bool canEnter = true, canExit = true;
    public bool isDoor, isOpen = true;

    // Направление задаёт запись в исходном узле. Для обратного хода нужна своя запись.
    public float GetCost() => canEnter && (!isDoor || isOpen) && float.IsFinite(cost) && cost >= 0f
        ? cost : float.MaxValue;
}
