using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Handle confirmation after the player interactor and combat have polled this frame's input.
[DefaultExecutionOrder(150)]
[DisallowMultipleComponent]
public sealed class TempleRunePicker : PausedGameOverlay
{
    private const int GlyphCount = TempleRunePuzzle.GlyphCount;
    private const float TileStep = 82f;
    private static readonly Color32 TileFace = new(50, 42, 33, 255);

    private readonly List<int> held = new(GlyphCount);
    private readonly Image[] tiles = new Image[GlyphCount];
    private readonly TempleRuneIcon[] faces = new TempleRuneIcon[GlyphCount];
    private readonly TempleRuneIcon[] edges = new TempleRuneIcon[GlyphCount];
    private RectTransform panel;
    private Text controls;
    private OverlayControls input;
    private InputAction interact;
    private InputAction point;
    private InputAction click;
    private Vector2 previousPointer;
    private Action<int> onChoose;
    private int selection;
    private int openedFrame;

    public int ChoiceCount => held.Count;
    public int SelectedGlyph => IsOpen ? held[selection] : -1;

    private void Awake()
    {
        input = new OverlayControls(RefreshControls);
        interact = OverlayControls.Find("Player/Interact");
        point = OverlayControls.Find("UI/Point");
        click = OverlayControls.Find("UI/Click");
    }

    public static void Open(PlayerInteractor player, MeshRenderer[] models, string[] ids, Action<int> choose)
    {
        if (player == null || player.Inventory == null || choose == null || Time.timeScale <= 0f ||
            models == null || models.Length != GlyphCount || ids == null || ids.Length != GlyphCount)
            return;

        if (!player.TryGetComponent(out TempleRunePicker picker))
            picker = player.gameObject.AddComponent<TempleRunePicker>();
        picker.Show(player.Inventory, models, ids, choose);
    }

    private void Show(PlayerInventory inventory, MeshRenderer[] models, string[] ids, Action<int> choose)
    {
        if (!isActiveAndEnabled || IsOpen)
            return;

        for (int i = 0; i < GlyphCount; i++)
            if (models[i] != null && inventory.HasItem(ids[i]))
                held.Add(i);
        if (held.Count == 0)
            return;

        if (!TryOpenOverlay("Rune Picker", 105, new Vector2(1280f, 720f), usePointer: true))
        {
            held.Clear();
            return;
        }

        GameAudio.Interface(SoundCue.Cloth);
        selection = 0;
        panel.sizeDelta = new Vector2(Mathf.Max(504f, held.Count * TileStep + 36f), 178f);
        for (int i = 0; i < tiles.Length; i++)
        {
            bool visible = i < held.Count;
            tiles[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            tiles[i].rectTransform.anchoredPosition = new Vector2((i - (held.Count - 1) * .5f) * TileStep, 0f);
            MeshRenderer model = models[held[i]];
            Mesh mesh = model.GetComponent<MeshFilter>().sharedMesh;
            Material[] surfaces = model.sharedMaterials;
            edges[i].SetStone(mesh, 1, surfaces[1]);
            faces[i].SetStone(mesh, 0, surfaces[0]);
        }
        HighlightSelection();
        input.UseDevice(interact.activeControl?.device);
        previousPointer = point.ReadValue<Vector2>();
        RefreshControls();

        onChoose = choose;
        openedFrame = Time.frameCount;
    }

    private void LateUpdate()
    {
        if (!IsOpen || openedFrame == Time.frameCount || !Application.isFocused || ControlBindingsPanel.BlocksGameplay)
            return;

        if (input.Pressed(input.Back))
        {
            Cancel();
            return;
        }

        if (input.Pressed(input.Left))
            MoveSelection(-1);
        else if (input.Pressed(input.Right))
            MoveSelection(1);

        bool clicked = input.Pressed(click);
        if (!clicked && (input.Pressed(input.Accept) || input.Pressed(interact)))
        {
            Confirm();
            return;
        }

        Vector2 pointer = point.ReadValue<Vector2>();
        if (!clicked && pointer == previousPointer)
            return;
        previousPointer = pointer;
        input.UseDevice(point.activeControl?.device);
        for (int i = 0; i < held.Count; i++)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(tiles[i].rectTransform, pointer))
                continue;

            selection = i;
            HighlightSelection();
            if (clicked)
                Confirm();
            return;
        }
    }

    public void MoveSelection(int direction)
    {
        if (!IsOpen)
            return;

        int next = (selection + Math.Sign(direction) + held.Count) % held.Count;
        if (selection == next) return;
        selection = next;
        GameAudio.Interface(SoundCue.MenuClick);
        HighlightSelection();
    }

    public void Confirm()
    {
        if (!IsOpen)
            return;

        int glyph = SelectedGlyph;
        Action<int> choose = onChoose;
        GameAudio.Interface(SoundCue.MenuClick);
        Cancel();
        choose(glyph);
    }

    public void Cancel() => CloseOverlay();

    protected override void OnOverlayClosed()
    {
        onChoose = null;
        held.Clear();
    }

    private void HighlightSelection()
    {
        Color brass = GameUiStyle.Brass;
        Color idle = Color.Lerp(TileFace, brass, .31f);
        for (int i = 0; i < held.Count; i++)
            tiles[i].color = i == selection ? brass : idle;
    }

    private void RefreshControls()
    {
        if (!IsOpen)
            return;

        string confirm = input.GamepadHints ? input.Label(input.Accept) : $"{input.Label(click)} / {input.Label(interact)}";
        controls.text = $"{input.Label(input.Left)} / {input.Label(input.Right)}   Выбор      {confirm}   Положить      {input.Label(input.Back)}   Назад";
    }

    protected override void BuildView(Transform root)
    {
        Image frame = Element<Image>(root, "Tray", Vector2.zero);
        panel = frame.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, .24f);
        frame.color = Color.Lerp(TileFace, GameUiStyle.Brass, .55f);
        Image lining = Element<Image>(panel, "Lining", Vector2.zero);
        RectTransform interior = lining.rectTransform;
        interior.anchorMin = Vector2.zero;
        interior.anchorMax = Vector2.one;
        interior.offsetMin = Vector2.one;
        interior.offsetMax = -Vector2.one;
        lining.color = GameUiStyle.Panel;

        Text title = Label(interior, 60f, 27);
        title.text = "Выберите камень";
        title.color = GameUiStyle.Ink;
        controls = Label(interior, -62f, 19);
        controls.color = GameUiStyle.MutedInk;
        controls.resizeTextForBestFit = true;
        controls.resizeTextMinSize = 14;
        controls.resizeTextMaxSize = 19;

        for (int i = 0; i < GlyphCount; i++)
        {
            Image tile = Element<Image>(interior, $"Stone {i + 1}", new Vector2(72f, 72f));
            tiles[i] = tile;
            Image face = Element<Image>(tile.transform, "Face", new Vector2(68f, 68f));
            face.color = TileFace;
            edges[i] = Element<TempleRuneIcon>(face.transform, "Beveled edge", new Vector2(60f, 60f));
            faces[i] = Element<TempleRuneIcon>(face.transform, "Glyph", new Vector2(60f, 60f));
        }
    }

    private static Text Label(Transform parent, float y, int size)
    {
        Text label = Element<Text>(parent, y > 0f ? "Title" : "Controls", new Vector2(484f, 40f));
        label.rectTransform.anchoredPosition = new Vector2(0f, y);
        label.font = GameUiStyle.Font;
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        return label;
    }

    private static T Element<T>(Transform parent, string name, Vector2 size) where T : Graphic
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(T));
        T graphic = go.GetComponent<T>();
        graphic.rectTransform.SetParent(parent, false);
        graphic.rectTransform.sizeDelta = size;
        graphic.raycastTarget = false;
        return graphic;
    }
}
