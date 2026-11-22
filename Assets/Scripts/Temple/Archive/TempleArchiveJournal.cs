using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class TempleArchiveJournal : PausedGameOverlay
{
    private const int DocumentTotal = 6;
    private const int CompleteMask = (1 << DocumentTotal) - 1;

    private int collectedMask;
    private int currentDocument;
    private Font font;
    private Text heading;
    private Text title;
    private Text body;
    private Text reaction;
    private Text controls;
    private ScrollRect scroll;
    private readonly Text[] seals = new Text[DocumentTotal];

    private Action pendingEntrance;
    private Action continueAfterReading;

    public int DocumentCount { get; private set; }
    public bool FullFamilyTruth { get; private set; }
    public string VisibleDocumentText => IsOpen ? body.text : string.Empty;
    public bool IsConfirmingEntrance => pendingEntrance != null;

    public static TempleArchiveJournal GetOrCreate(GameObject player) =>
        player.TryGetComponent(out TempleArchiveJournal journal)
            ? journal
            : player.AddComponent<TempleArchiveJournal>();

    public bool HasDocument(int number) =>
        number >= 1 && number <= DocumentTotal && (collectedMask & (1 << (number - 1))) != 0;

    public void ReadDocument(int number)
    {
        if (number < 1 || number > DocumentTotal || IsConfirmingEntrance || continueAfterReading != null ||
            !OpenScreen())
            return;

        bool firstRead = !HasDocument(number);
        if (firstRead)
        {
            collectedMask |= 1 << (number - 1);
            DocumentCount++;
        }

        ShowPage(number, firstRead);
    }

    public void ConfirmRitualEntrance(Action enter)
    {
        if (enter == null || IsOpen || !OpenScreen())
            return;

        pendingEntrance = enter;
        heading.text = string.Empty;
        title.text = "Вход в главный зал";
        body.text = "Войти в главный зал? Вернуться в архив после начала ритуала не получится.";
        reaction.text = string.Empty;
        foreach (Text seal in seals)
            seal.gameObject.SetActive(false);
        controls.text = "Enter / A Войти    Esc / B Назад";
        FitPage();
    }

    public void ConfirmEntrance()
    {
        if (pendingEntrance == null)
            return;

        Action enter = pendingEntrance;
        pendingEntrance = null;
        if (collectedMask == CompleteMask && !FullFamilyTruth)
        {
            ShowPage(DocumentTotal, false);
            continueAfterReading = enter;
            controls.text = "↑ / ↓ Прокрутить    Enter / A Продолжить";
            return;
        }

        Close();
        enter();
    }

    public void ContinueEntrance()
    {
        if (continueAfterReading == null)
            return;

        Action enter = continueAfterReading;
        Close();
        enter();
    }

    public void Close() => CloseOverlay();

    protected override void OnOverlayClosed()
    {
        pendingEntrance = null;
        continueAfterReading = null;
    }

    private void Update()
    {
        if (!Application.isFocused)
            return;

        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        bool journalPressed = keyboard?.jKey.wasPressedThisFrame == true ||
            gamepad?.selectButton.wasPressedThisFrame == true;

        if (!IsOpen)
        {
            if (journalPressed && currentDocument != 0 && Time.timeScale > 0f &&
                Cursor.lockState == CursorLockMode.Locked && OpenScreen())
                ShowPage(currentDocument, false);
            return;
        }

        bool accept = keyboard?.enterKey.wasPressedThisFrame == true ||
            gamepad?.buttonSouth.wasPressedThisFrame == true;
        bool cancel = journalPressed || keyboard?.escapeKey.wasPressedThisFrame == true ||
            gamepad?.buttonEast.wasPressedThisFrame == true;

        if (IsConfirmingEntrance)
        {
            if (cancel)
                Close();
            else if (accept)
                ConfirmEntrance();
            return;
        }

        if (continueAfterReading != null)
        {
            if (accept)
            {
                ContinueEntrance();
                return;
            }
        }
        else
        {
            if (cancel || accept)
            {
                Close();
                return;
            }

            if (keyboard?.leftArrowKey.wasPressedThisFrame == true ||
                gamepad?.dpad.left.wasPressedThisFrame == true)
                FlipPage(-1);
            if (keyboard?.rightArrowKey.wasPressedThisFrame == true ||
                gamepad?.dpad.right.wasPressedThisFrame == true)
                FlipPage(1);
        }

        float delta = Mouse.current?.scroll.ReadValue().y ?? 0f;
        if (keyboard?.upArrowKey.wasPressedThisFrame == true || gamepad?.dpad.up.wasPressedThisFrame == true)
            delta += 1f;
        if (keyboard?.downArrowKey.wasPressedThisFrame == true || gamepad?.dpad.down.wasPressedThisFrame == true)
            delta -= 1f;
        if (delta != 0f)
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + .16f * Mathf.Sign(delta));
    }

    private void FlipPage(int direction)
    {
        int next = currentDocument;
        do
        {
            next = (next - 1 + direction + DocumentTotal) % DocumentTotal + 1;
        } while (!HasDocument(next));

        ShowPage(next, false);
    }

    private void ShowPage(int number, bool firstRead)
    {
        currentDocument = number;
        bool sealedRegister = number == DocumentTotal && collectedMask != CompleteMask;
        bool registerFirstRead = number == DocumentTotal && !sealedRegister && !FullFamilyTruth;

        heading.text = $"ЖУРНАЛ  /  {DocumentCount} ИЗ {DocumentTotal}";
        title.text = $"D{number:00}  {TempleArchiveText.Titles[number - 1]}";
        body.text = sealedRegister ? TempleArchiveText.SealedRegister : TempleArchiveText.Pages[number - 1];
        reaction.text = (number < DocumentTotal && firstRead) || registerFirstRead
            ? TempleArchiveText.Reactions[number - 1]
            : string.Empty;

        for (int i = 0; i < seals.Length; i++)
        {
            bool found = HasDocument(i + 1);
            seals[i].text = found ? $"•  D{i + 1:00}" : $"○  D{i + 1:00}";
            seals[i].color = found ? new Color32(111, 65, 36, 255) : new Color32(105, 94, 81, 255);
            seals[i].gameObject.SetActive(sealedRegister);
        }

        controls.text = Gamepad.current != null && Keyboard.current == null
            ? "← / → Листать    ↑ / ↓ Прокрутить    B Закрыть"
            : "← / → Листать    ↑ / ↓ Прокрутить    Esc Закрыть    J Журнал";

        FitPage();

        if (registerFirstRead)
            FullFamilyTruth = true;
    }

    private bool OpenScreen() =>
        TryOpenOverlay("Archive Journal", 100, new Vector2(1600f, 900f), usePointer: false);

    private void FitPage()
    {
        Canvas.ForceUpdateCanvases();
        scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(scroll.viewport.rect.height, body.preferredHeight + 22f));
        scroll.verticalNormalizedPosition = 1f;
    }

    protected override void BuildView(Transform root)
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Color ink = new Color32(46, 36, 27, 255);
        Color inkFaded = new Color32(84, 62, 43, 255);
        RectTransform dimmer = Rect(root, "Backdrop", Vector2.zero, Vector2.one);
        Fill(dimmer, new Color(0f, 0f, 0f, .83f));
        RectTransform paper = Rect(dimmer, "Paper", new Vector2(.18f, .09f), new Vector2(.82f, .91f));
        Fill(paper, new Color32(222, 207, 174, 255));
        Fill(Rect(paper, "Top rule", new Vector2(.05f, .872f), new Vector2(.95f, .875f)), inkFaded);
        Fill(Rect(paper, "Bottom rule", new Vector2(.05f, .108f), new Vector2(.95f, .111f)), inkFaded);

        heading = Label(paper, "Archive progress", .05f, .88f, .95f, .968f, 18, inkFaded, TextAnchor.MiddleLeft);
        heading.fontStyle = FontStyle.Bold;
        title = Label(paper, "Document title", .05f, .735f, .95f, .855f, 34, ink, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 22;
        title.resizeTextMaxSize = 34;

        RectTransform viewport = Rect(paper, "Page viewport", new Vector2(.05f,.245f), new Vector2(.95f,.724f));
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.inertia = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        RectTransform page = Rect(viewport, "Page", new Vector2(0f, 1f), Vector2.one);
        page.pivot = new Vector2(.5f, 1f);
        scroll.content = page;
        body = Label(page, "Document body", 0f, 0f, 1f, 1f, 24, ink, TextAnchor.UpperLeft);
        body.lineSpacing = 1.18f;
        body.verticalOverflow = VerticalWrapMode.Overflow;

        for (int i = 0; i < seals.Length; i++)
        {
            float x = .05f + i * .15f;
            seals[i] = Label(paper, $"Document seal {i + 1}", x, .125f, x + .14f, .224f, 22, ink, TextAnchor.MiddleCenter);
            Fill(seals[i].transform, new Color32(191, 172, 135, 255));
        }

        reaction = Label(paper, "Ivan reaction", .05f, .124f, .95f, .224f, 21, inkFaded, TextAnchor.MiddleLeft);
        reaction.fontStyle = FontStyle.Italic;
        controls = Label(paper, "Page controls", .05f, .021f, .95f, .094f, 18, inkFaded, TextAnchor.MiddleLeft);
    }

    private Text Label(Transform parent, string name, float left, float bottom, float right, float top,
        int size, Color color, TextAnchor alignment)
    {
        RectTransform rect = Rect(parent, name, new Vector2(left, bottom), new Vector2(right, top));
        Text label = rect.gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color;
        label.supportRichText = false;
        label.raycastTarget = false;
        return label;
    }

    private static void Fill(Transform parent, Color color)
    {
        RectTransform rect = Rect(parent, "Surface", Vector2.zero, Vector2.one);
        rect.SetAsFirstSibling();
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
}
