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
    private static readonly Color32 Brass = new(199, 164, 103, 255);
    private static readonly Color32 DarkBrass = new(97, 79, 59, 255);
    private static readonly Color32 Ivory = new(238, 227, 208, 255);

    private readonly List<int> held = new(GlyphCount);
    private readonly Image[] tiles = new Image[GlyphCount];
    private readonly TempleRuneIcon[] faces = new TempleRuneIcon[GlyphCount];
    private readonly TempleRuneIcon[] edges = new TempleRuneIcon[GlyphCount];
    private RectTransform panel;
    private Text controls;
    private Action<int> onChoose;
    private int selection;
    private int openedFrame;

    public int ChoiceCount => held.Count;
    public int SelectedGlyph => IsOpen ? held[selection] : -1;

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
        UpdateControls(Gamepad.current != null &&
            (Keyboard.current == null || Gamepad.current.buttonNorth.wasPressedThisFrame));

        onChoose = choose;
        openedFrame = Time.frameCount;
    }

    private void LateUpdate()
    {
        if (!IsOpen || openedFrame == Time.frameCount || !Application.isFocused)
            return;

        Keyboard keys = Keyboard.current;
        Gamepad pad = Gamepad.current;
        if (keys?.escapeKey.wasPressedThisFrame == true || pad?.buttonEast.wasPressedThisFrame == true)
        {
            Cancel();
            return;
        }

        bool padLeft = pad?.dpad.left.wasPressedThisFrame == true;
        bool padRight = pad?.dpad.right.wasPressedThisFrame == true;
        bool left = keys?.leftArrowKey.wasPressedThisFrame == true || keys?.aKey.wasPressedThisFrame == true;
        bool right = keys?.rightArrowKey.wasPressedThisFrame == true || keys?.dKey.wasPressedThisFrame == true;
        if (left || right || padLeft || padRight)
        {
            UpdateControls(padLeft || padRight);
            MoveSelection(left || padLeft ? -1 : 1);
        }

        if (keys?.eKey.wasPressedThisFrame == true || keys?.enterKey.wasPressedThisFrame == true ||
            keys?.numpadEnterKey.wasPressedThisFrame == true || pad?.buttonSouth.wasPressedThisFrame == true)
        {
            Confirm();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || (mouse.delta.ReadValue() == Vector2.zero && !mouse.leftButton.wasPressedThisFrame))
            return;

        UpdateControls(false);
        Vector2 pointer = mouse.position.ReadValue();
        for (int i = 0; i < held.Count; i++)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(tiles[i].rectTransform, pointer))
                continue;

            selection = i;
            HighlightSelection();
            if (mouse.leftButton.wasPressedThisFrame)
                Confirm();
            return;
        }
    }

    public void MoveSelection(int direction)
    {
        if (!IsOpen)
            return;

        selection = (selection + Math.Sign(direction) + held.Count) % held.Count;
        HighlightSelection();
    }

    public void Confirm()
    {
        if (!IsOpen)
            return;

        int glyph = SelectedGlyph;
        Action<int> choose = onChoose;
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
        for (int i = 0; i < held.Count; i++)
        {
            bool selected = i == selection;
            tiles[i].color = selected ? Brass : DarkBrass;
        }
    }

    private void UpdateControls(bool gamepad)
    {
        controls.text = gamepad ? "← →   Выбор      A   Положить      B   Назад" :
            "← →   Выбор      ЛКМ / E   Положить      Esc   Назад";
    }

    protected override void BuildView(Transform root)
    {
        Image frame = Element<Image>(root, "Tray", Vector2.zero);
        panel = frame.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, .24f);
        frame.color = DarkBrass;
        Image lining = Element<Image>(panel, "Lining", Vector2.zero);
        RectTransform interior = lining.rectTransform;
        interior.anchorMin = Vector2.zero;
        interior.anchorMax = Vector2.one;
        interior.offsetMin = Vector2.one;
        interior.offsetMax = -Vector2.one;
        lining.color = new Color32(30, 25, 23, 249);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Text title = Label(interior, font, 60f, 23);
        title.text = "Выберите камень";
        title.color = Ivory;
        controls = Label(interior, font, -62f, 16);
        controls.color = new Color32(211, 196, 171, 255);

        for (int i = 0; i < GlyphCount; i++)
        {
            Image tile = Element<Image>(interior, $"Stone {i + 1}", new Vector2(72f, 72f));
            tiles[i] = tile;
            Image face = Element<Image>(tile.transform, "Face", new Vector2(68f, 68f));
            face.color = new Color32(54, 46, 43, 255);
            edges[i] = Element<TempleRuneIcon>(face.transform, "Beveled edge", new Vector2(60f, 60f));
            faces[i] = Element<TempleRuneIcon>(face.transform, "Glyph", new Vector2(60f, 60f));
        }
    }

    private static Text Label(Transform parent, Font font, float y, int size)
    {
        Text label = Element<Text>(parent, y > 0f ? "Title" : "Controls", new Vector2(484f, 34f));
        label.rectTransform.anchoredPosition = new Vector2(0f, y);
        label.font = font;
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
