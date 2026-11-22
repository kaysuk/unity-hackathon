using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(MeshRenderer))]
public sealed class TempleSlidingTile : Interactable
{
    [SerializeField, Range(1, 15)] private int number = 1;
    [SerializeField, Range(0, 15)] private int slot;

    public int Number => number;
    public int Slot => slot;

    private const int Width = TempleSlidingPuzzle.Width;
    private TempleSlidingPuzzle board;
    private Vector3 destination;
    private bool sliding;

    private void OnEnable()
    {
        board = GetComponentInParent<TempleSlidingPuzzle>();
        // Every tile uses one cell of the shared picture atlas, including in Scene view.
        float cell = 1f / Width;
        int index = number - 1;
        var properties = new MaterialPropertyBlock();
        properties.SetVector("_BaseMap_ST", new Vector4(cell, cell,
            index % Width * cell, 1f - (index / Width + 1) * cell));
        GetComponent<MeshRenderer>().SetPropertyBlock(properties);
    }

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && board != null && board.CanSlide(this);

    public override string GetPrompt(PlayerInteractor player, string buttonPrompt)
    {
        if (board == null || board.IsSolved)
            return null;
        return CanInteract(player) && !string.IsNullOrEmpty(buttonPrompt)
            ? $"{buttonPrompt} · Сдвинуть плитку"
            : "Соберите знак по образцу";
    }

    public override void Interact(PlayerInteractor player)
    {
        if (CanInteract(player))
            board.TrySlide(this);
    }

    internal void MoveTo(int nextSlot)
    {
        slot = nextSlot;
        destination = new Vector3((slot % Width + .5f) / Width - .5f,
            .5f - (slot / Width + .5f) / Width, transform.localPosition.z);
        sliding = true;
    }

    private void Update()
    {
        if (!sliding)
            return;
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, destination, 2f * Time.deltaTime);
        sliding = transform.localPosition != destination;
    }
}
