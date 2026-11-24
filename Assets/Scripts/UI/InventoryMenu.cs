using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent, RequireComponent(typeof(PlayerInventory)), DefaultExecutionOrder(150)]
public sealed class InventoryMenu : PausedGameOverlay
{
    private enum Section { Items, Notes }
    private const int VisibleRows = 6;
    private static readonly string[] SectionNames = { "Вещи", "Записки" };
    [SerializeField] private Mesh stoneMesh;
    [SerializeField] private Material stoneEdge;
    [SerializeField] private Material[] stoneFaces;
    private TempleRuneIcon faceIcon, edgeIcon;
    private readonly List<(string Title, string Description, int? Page, int Stone)> entries = new();
    private readonly Text[] tabs = new Text[SectionNames.Length];
    private RectTransform tabUnderline;
    private readonly Text[] rows = new Text[VisibleRows];
    private readonly Image[] rowBackgrounds = new Image[VisibleRows];
    private Text position, itemName, description, read, close;
    private PlayerInventory inventory;
    private Section section;
    private int selected;
    private int openedFrame;
    private int FirstRow => selected / VisibleRows * VisibleRows;

    private void Awake() => inventory = GetComponent<PlayerInventory>();

    public void Open() => ShowInventory(true);

    private void ShowInventory(bool playSound)
    {
        if (IsOpen || inventory == null || Time.timeScale <= 0f ||
            !TryOpenOverlay("Inventory", 100, new Vector2(1600, 900), usePointer: true))
            return;
        openedFrame = Time.frameCount;
        if (playSound) GameAudio.Interface(SoundCue.InventoryBag);
        RefreshSection();
    }

    private void LateUpdate()
    {
        if (Application.isFocused) HandleInput();
    }

    private void HandleInput()
    {
        Keyboard keys = Keyboard.current;
        Gamepad pad = Gamepad.current;
        bool toggle = keys?.tabKey.wasPressedThisFrame == true || pad?.startButton.wasPressedThisFrame == true;
        if (!IsOpen)
        {
            if (toggle) Open();
            return;
        }
        if (Time.frameCount == openedFrame) return;
        if (toggle || keys?.escapeKey.wasPressedThisFrame == true || pad?.buttonEast.wasPressedThisFrame == true)
        {
            Close();
            return;
        }
        if (keys?.leftArrowKey.wasPressedThisFrame == true || pad?.dpad.left.wasPressedThisFrame == true)
            ChangeSection(-1);
        else if (keys?.rightArrowKey.wasPressedThisFrame == true || pad?.dpad.right.wasPressedThisFrame == true)
            ChangeSection(1);
        else if (keys?.upArrowKey.wasPressedThisFrame == true || pad?.dpad.up.wasPressedThisFrame == true)
            MoveSelection(-1);
        else if (keys?.downArrowKey.wasPressedThisFrame == true || pad?.dpad.down.wasPressedThisFrame == true)
            MoveSelection(1);
        else if (keys?.enterKey.wasPressedThisFrame == true || pad?.buttonSouth.wasPressedThisFrame == true)
            ReadSelected();
        else if (Mouse.current != null)
            HandlePointer(Mouse.current);
    }

    private void HandlePointer(Mouse mouse)
    {
        float wheel = mouse.scroll.ReadValue().y;
        if (wheel != 0) MoveSelection(wheel > 0 ? -1 : 1);
        if (!mouse.leftButton.wasPressedThisFrame) return;
        Vector2 pointer = mouse.position.ReadValue();
        bool Hit(Text label) => label.isActiveAndEnabled &&
            RectTransformUtility.RectangleContainsScreenPoint(label.rectTransform, pointer);
        if (Hit(close))
        {
            Close();
            return;
        }
        if (Hit(read))
        {
            ReadSelected();
            return;
        }
        for (int i = 0; i < tabs.Length; i++)
        {
            if (!Hit(tabs[i])) continue;
            ChangeSection(i - (int)section);
            return;
        }
        for (int i = 0; i < rows.Length; i++)
        {
            if (!Hit(rows[i])) continue;
            MoveSelection(FirstRow + i - selected);
            return;
        }
    }

    public void Close()
    {
        if (!IsOpen) return;
        GameAudio.Interface(SoundCue.InventoryBag);
        CloseOverlay();
    }

    public void ChangeSection(int offset)
    {
        if (!IsOpen) return;
        Section next = (Section)(((int)section + offset % tabs.Length + tabs.Length) % tabs.Length);
        if (next != section) GameAudio.Interface(SoundCue.MenuClick);
        section = next;
        selected = 0;
        RefreshSection();
    }

    private void RefreshSection()
    {
        entries.Clear();
        switch (section)
        {
            case Section.Items:
                for (int i = 0; i < TempleRunePuzzle.RuneIds.Length; i++)
                    if (inventory.HasItem(TempleRunePuzzle.RuneIds[i]))
                        entries.Add(($"Рунный камень {i + 1}", $"Камень из комнаты {i + 1}. На лицевой стороне вырезан знак. Камень подходит к гнёздам на плитах в мастерской.", null, i));
                AddHeld("m0_key_room3", "Левый ключ М0", "Ключ из третьей комнаты. Вставляется в левое гнездо главной решётки. Механизму нужны обе рукояти.");
                AddHeld("m0_key_room4", "Правый ключ М0", "Ключ из четвёртой комнаты. Вставляется в правое гнездо главной решётки и работает в паре с левым.");
                AddHeld("temple_m3_key", "Латунный ключ М3", "Небольшой латунный ключ. Открывает замок на ставне М3.");
                break;
            case Section.Notes:
                AddNotes();
                break;
        }
        selected = Mathf.Clamp(selected, 0, Mathf.Max(0, entries.Count - 1));
        Render();
    }

    private void AddHeld(string id, string title, string text)
    {
        if (inventory.HasItem(id)) entries.Add((title, text, null, -1));
    }

    private void AddNotes()
    {
        TryGetComponent(out TempleArchiveJournal journal);
        for (int page = 1; page <= TempleArchiveJournal.DocumentTotal; page++)
        {
            bool hasClue = page <= TempleSealDocument.Notes.Length && TempleSealDocument.HasCollectedPage(inventory, page);
            if (!hasClue && (journal == null || !journal.HasDocument(page))) continue;
            string preview = hasClue ? TempleSealDocument.Notes[page - 1].Clue : "Документ из храмового архива.";
            entries.Add((TempleArchiveText.Titles[page - 1], preview, page, -1));
        }
        AddHeld("archive_documents", "Свёрток документов", "Сложенные архивные листы, перевязанные льняной лентой.");
        if (journal != null && !string.IsNullOrEmpty(journal.SealCalculation))
            entries.Add(("Расчёт шести печатей", "Запись о том, как рычаги в архиве связаны с печатями.", TempleArchiveJournal.CalculationPage, -1));
    }

    public void MoveSelection(int offset)
    {
        if (!IsOpen || entries.Count == 0) return;
        int next = (selected + offset % entries.Count + entries.Count) % entries.Count;
        if (selected == next) return;
        selected = next;
        GameAudio.Interface(SoundCue.MenuClick);
        Render();
    }

    public void ReadSelected()
    {
        if (!IsOpen || entries.Count == 0 || entries[selected].Page is not int page) return;
        CloseOverlay();
        TempleArchiveJournal.GetOrCreate(gameObject).RereadFromInventory(page, () => ShowInventory(false));
    }

    private void Render()
    {
        for (int i = 0; i < tabs.Length; i++)
            tabs[i].color = i == (int)section ? GameUiStyle.Brass : GameUiStyle.MutedInk;
        tabUnderline.SetParent(tabs[(int)section].transform, false);
        for (int i = 0; i < rows.Length; i++)
        {
            int index = FirstRow + i;
            rowBackgrounds[i].gameObject.SetActive(index < entries.Count);
            if (index >= entries.Count) continue;
            rows[i].text = entries[index].Title;
            rowBackgrounds[i].enabled = selected == index;
        }
        bool any = entries.Count > 0;
        int stone = any ? entries[selected].Stone : -1;
        bool showStone = stone >= 0 && stoneMesh != null && stoneEdge != null &&
            stoneFaces != null && stone < stoneFaces.Length && stoneFaces[stone] != null;
        edgeIcon.gameObject.SetActive(showStone);
        faceIcon.gameObject.SetActive(showStone);
        if (showStone)
        {
            edgeIcon.SetStone(stoneMesh, 1, stoneEdge);
            faceIcon.SetStone(stoneMesh, 0, stoneFaces[stone]);
        }
        itemName.rectTransform.anchorMin = new Vector2(showStone ? .60f : .485f, .445f);
        position.text = any ? $"{selected + 1} из {entries.Count}" : string.Empty;
        itemName.text = any ? entries[selected].Title : "Пока пусто";
        description.text = any ? entries[selected].Description : section switch
        {
            Section.Items => "Здесь будут камни и ключи, которые Иван носит с собой.",
            _ => "Здесь можно перечитать найденные записки."
        };
        read.gameObject.SetActive(any && entries[selected].Page.HasValue);
    }

    protected override void OnOverlayClosed() { }

    protected override void BuildView(Transform root)
    {
        RectTransform shade = Area(root, "Shade", 0, 0, 1, 1);
        Surface(shade, new Color32(13, 11, 10, 212));
        RectTransform panel = Area(shade, "Inventory panel", .10f, .10f, .90f, .90f);
        Surface(panel, GameUiStyle.Panel);
        Label(panel, "Heading", .045f, .84f, .67f, .96f, 49).text = "Инвентарь";
        close = Label(panel, "Close", .70f, .84f, .955f, .96f, 24, TextAnchor.MiddleRight);
        close.text = "Закрыть   Esc / B";
        for (int i = 0; i < tabs.Length; i++)
        {
            float x = .045f + i * .20f;
            tabs[i] = Label(panel, SectionNames[i], x, .72f,x + .185f, .82f, 31);
            tabs[i].text = SectionNames[i];
        }
        tabUnderline = Area(tabs[0].transform, "Selected section", 0, 0, .72f, .055f);
        Surface(tabUnderline, GameUiStyle.Brass);
        Surface(Area(panel, "Rule", .045f, .703f, .955f, .706f), GameUiStyle.Brass);
        Surface(Area(panel, "Divider", .446f, .155f, .448f, .662f), new Color32(99, 79, 57, 255));
        for (int i = 0; i < rows.Length; i++)
        {
            float top = .66f - i * .085f;
            var row = Area(panel, $"Row {i + 1}", .045f,top - .077f, .425f,top);
            rowBackgrounds[i] = Surface(row, new Color32(83, 62, 40, 255));
            rows[i] = Label(row, "Item", .04f, 0, .99f, 1, 26);
        }
        edgeIcon = Area(panel, "Stone edge", .485f, .448f, .585f, .585f).gameObject.AddComponent<TempleRuneIcon>();
        faceIcon = Area(panel, "Stone face", .485f, .448f, .585f, .585f).gameObject.AddComponent<TempleRuneIcon>();
        position = Label(panel, "Position", .485f, .58f, .95f, .66f, 25);
        position.color = GameUiStyle.Brass;
        itemName = Label(panel, "Name", .485f, .445f, .955f, .58f, 38);
        description = Label(panel, "Description", .485f, .245f, .955f, .432f, 28, TextAnchor.UpperLeft);
        read = Label(panel, "Read", .485f, .14f, .955f, .23f, 27);
        read.text = "Читать полностью   Enter / A";
        read.color = GameUiStyle.Brass;
        Label(panel, "Navigation", .045f, .028f, .955f, .115f, 26).text =
            "← →   Разделы      ↑ ↓   Предметы";
    }

    private static RectTransform Area(Transform parent, string name, float left, float bottom, float right, float top)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Image Surface(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text Label(Transform parent, string name, float left, float bottom, float right, float top,
        int size, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var label = Area(parent, name, left, bottom, right, top).gameObject.AddComponent<Text>();
        label.font = GameUiStyle.Font;
        label.fontSize = label.resizeTextMaxSize = size;
        label.resizeTextMinSize = size * 2 / 3;
        label.resizeTextForBestFit = true;
        label.alignment = alignment;
        label.color = GameUiStyle.Ink;
        label.supportRichText = label.raycastTarget = false;
        return label;
    }
}
