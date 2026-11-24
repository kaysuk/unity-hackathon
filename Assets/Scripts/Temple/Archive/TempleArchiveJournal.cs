using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class TempleArchiveJournal : PausedGameOverlay
{
    internal const int DocumentTotal = 7;
    internal const int CalculationPage = 0;
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

    private bool readingSealCalculation;
    internal string SealCalculation { get; set; }
    private Action returnToInventory;
    private Action pendingEntrance;
    private Action continueAfterReading;

    public int DocumentCount { get; private set; }
    public bool FullFamilyTruth { get; private set; }
    internal int CollectedMask => collectedMask;
    public string VisibleDocumentText => IsOpen ? body.text : string.Empty;
    public bool IsConfirmingEntrance => pendingEntrance != null;

    public static TempleArchiveJournal GetOrCreate(GameObject player) =>
        player.TryGetComponent(out TempleArchiveJournal journal)
            ? journal
            : player.AddComponent<TempleArchiveJournal>();

    public bool HasDocument(int number) =>
        number >= 1 && number <= DocumentTotal && (collectedMask & (1 << (number - 1))) != 0;

    internal void RestoreProgress(int mask, bool fullTruth)
    {
        collectedMask = mask & CompleteMask;
        DocumentCount = 0;
        currentDocument = 0;
        for (int number = 1; number <= DocumentTotal; number++)
        {
            if (!HasDocument(number))
                continue;

            DocumentCount++;
            currentDocument = number;
        }

        ImportRoomNotes();
        FullFamilyTruth = fullTruth && collectedMask == CompleteMask;
    }

    public void ReadDocument(int number)
    {
        if (number < 1 || number > DocumentTotal || IsConfirmingEntrance || continueAfterReading != null ||
            !OpenScreen())
            return;

        bool firstRead = CollectDocument(number);
        ShowPage(number, firstRead);
    }

    internal bool CollectDocument(int number)
    {
        if (HasDocument(number)) return false;
        collectedMask |= 1 << (number - 1);
        DocumentCount++;
        currentDocument = number;
        if (number > 1 && number < DocumentTotal)
            StoryGameplayVoice.FromPlayer(this, gameObject, $"archive.d{number:00}", "temple_v2");
        if (gameObject.scene == SceneManager.GetActiveScene())
            GameObjectives.SetArchiveProgress(DocumentCount);
        return true;
    }

    internal void ReadSealCalculation(string calculation)
    {
        if (IsOpen || !OpenScreen()) return;
        SealCalculation = calculation;
        readingSealCalculation = true;
        GameAudio.Interface(SoundCue.Paper);
        heading.text = "АРХИВ  /  КОМНАТА 7.1";
        title.text = "Расчёт шести печатей";
        body.text = calculation;
        reaction.text = string.Empty;
        foreach (Text seal in seals) seal.gameObject.SetActive(false);
        controls.text = "↑ / ↓ Прокрутить    Esc / B Закрыть";
        FitPage();
    }

    internal void RereadFromInventory(int page, Action goBack)
    {
        if (IsOpen || goBack == null) return;
        if (page == CalculationPage && !string.IsNullOrEmpty(SealCalculation))
            ReadSealCalculation(SealCalculation);
        else if (OpenScreen())
        {
            if (!HasDocument(page))
            {
                Close();
                return;
            }
            ShowPage(page, false);
        }

        if (!IsOpen) return;
        returnToInventory = goBack;
        controls.text = "↑ / ↓ Прокрутить    Esc / B К вещам";
    }

    public void ConfirmRitualEntrance(Action enter)
    {
        if (enter == null || IsOpen || !OpenScreen())
            return;

        GameAudio.Interface(SoundCue.MenuClick);
        StoryGameplayVoice.FromPlayer(this, gameObject, "temple.ritual.door", "temple_v2");
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

    public void Close()
    {
        if (IsOpen) GameAudio.Interface(IsConfirmingEntrance ? SoundCue.MenuClick : SoundCue.Paper);
        Action goBack = returnToInventory;
        CloseOverlay();
        goBack?.Invoke();
    }

    protected override void OnOverlayClosed()
    {
        returnToInventory = null;
        readingSealCalculation = false;
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
        float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
        if (delta != 0f && overflow > 0f)
        {
            float step = .65f * scroll.viewport.rect.height / overflow;
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + step * Mathf.Sign(delta));
        }
    }

    private void FlipPage(int direction)
    {
        if (readingSealCalculation || returnToInventory != null) return;
        int next = currentDocument;
        do
        {
            next = (next - 1 + direction + DocumentTotal) % DocumentTotal + 1;
        } while (!HasDocument(next));

        ShowPage(next, false);
    }

    private void ShowPage(int number, bool firstRead)
    {
        GameAudio.Interface(SoundCue.Paper);
        currentDocument = number;
        bool sealedRegister = number == DocumentTotal && collectedMask != CompleteMask;
        bool registerFirstRead = number == DocumentTotal && !sealedRegister && !FullFamilyTruth;
        if (sealedRegister && firstRead)
            StoryGameplayVoice.FromPlayer(this, gameObject, "archive.d07.sealed", "temple_v2");
        else if (registerFirstRead)
            StoryGameplayVoice.FromPlayer(this, gameObject, "archive.d07.open", "temple_v2");

        string room = number == DocumentTotal ? "7.2" : number.ToString();
        heading.text = $"АРХИВ ПОДЗЕМНОГО ХРАМА  /  КОМНАТА {room}  ·  {DocumentCount} ИЗ {DocumentTotal}";
        title.text = $"D{number:00}  {TempleArchiveText.Titles[number - 1]}";
        body.text = sealedRegister ? TempleArchiveText.SealedRegister : TempleArchiveText.ReadPage(number);
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

    private bool OpenScreen()
    {
        if (!TryOpenOverlay("Archive Journal", 100, new Vector2(1600f, 900f), usePointer: false))
            return false;

        ImportRoomNotes();
        return true;
    }

    private void ImportRoomNotes()
    {
        // Room pickups also carry the clues used by the six-seal puzzle.
        if (TryGetComponent(out PlayerInventory inventory))
        {
            for (int page = 1; page < DocumentTotal; page++)
            {
                if (!HasDocument(page) && TempleSealDocument.HasCollectedPage(inventory, page))
                {
                    collectedMask |= 1 << (page - 1);
                    DocumentCount++;
                    if (currentDocument == 0) currentDocument = page;
                }
            }
        }
    }

    private void FitPage()
    {
        Canvas.ForceUpdateCanvases();
        scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(scroll.viewport.rect.height, body.preferredHeight + 22f));
        scroll.verticalNormalizedPosition = 1f;
    }

    protected override void BuildView(Transform root)
    {
        font = GameUiStyle.Font;
        Color ink = new Color32(46, 36, 27, 255);
        Color inkFaded = new Color32(89, 71, 53, 255);
        Color rule = Color.Lerp(GameUiStyle.Brass, ink, .42f);
        Color shade = GameUiStyle.Panel;
        shade.a = .87f;
        RectTransform dimmer = Rect(root, "Backdrop", Vector2.zero, Vector2.one);
        Fill(dimmer, shade);
        RectTransform paper = Rect(dimmer, "Paper", new Vector2(.18f, .09f), new Vector2(.82f, .91f));
        Fill(paper, new Color32(228, 215, 186, 255));
        Fill(Rect(paper, "Top rule", new Vector2(.05f, .872f), new Vector2(.112f, .875f)), rule);
        Fill(Rect(paper, "Bottom rule", new Vector2(.05f, .108f), new Vector2(.95f, .111f)), rule);

        heading = Label(paper, "Archive progress", .05f, .88f, .95f, .968f, 23, inkFaded, TextAnchor.MiddleLeft);
        title = Label(paper, "Document title", .05f, .735f, .95f, .855f, 42, ink, TextAnchor.MiddleLeft);
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 27;
        title.resizeTextMaxSize = 42;

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
        body = Label(page, "Document body", 0f, 0f, 1f, 1f, 29, ink, TextAnchor.UpperLeft);
        body.lineSpacing = 1.1f;
        body.verticalOverflow = VerticalWrapMode.Overflow;

        for (int i = 0; i < seals.Length; i++)
        {
            float step = .90f / seals.Length;
            float x = .05f + i * step;
            seals[i] = Label(paper, $"Document seal {i + 1}", x, .125f, x + step - .01f, .224f, 25, ink, TextAnchor.MiddleCenter);
            Fill(seals[i].transform, new Color32(203, 181, 143, 255));
        }

        reaction = Label(paper, "Ivan reaction", .05f, .124f, .95f, .224f, 25, inkFaded, TextAnchor.MiddleLeft);
        reaction.fontStyle = FontStyle.Italic;
        controls = Label(paper, "Page controls", .05f, .021f, .95f, .094f, 22, inkFaded, TextAnchor.MiddleLeft);
        controls.resizeTextForBestFit = true;
        controls.resizeTextMinSize = 16;
        controls.resizeTextMaxSize = 22;
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
