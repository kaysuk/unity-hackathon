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
    internal const int BundlePage = -1;
    internal const string BundleItemId = "archive_documents";
    private const int CompleteMask = (1 << DocumentTotal) - 1;

    private int collectedMask;
    private int currentDocument;
    private OverlayControls input;
    private InputAction journal;
    private InputAction interact;
    private InputAction scrollWheel;
    private int openedFrame;
    private Font font;
    private Text title;
    private Text body;
    private Text controls;
    private ScrollRect scroll;

    private int? supplementPage;
    internal string SealCalculation { get; set; }
    private Action returnToInventory;
    private Action pendingEntrance;
    private Action continueAfterReading;

    public int DocumentCount { get; private set; }
    public bool FullFamilyTruth { get; private set; }
    internal int CollectedMask => collectedMask;
    public string VisibleDocumentText => IsOpen ? body.text : string.Empty;
    public bool IsConfirmingEntrance => pendingEntrance != null;

    private void Awake()
    {
        input = new OverlayControls(RefreshControls);
        journal = OverlayControls.Find("Player/Journal");
        interact = OverlayControls.Find("Player/Interact");
        scrollWheel = OverlayControls.Find("UI/ScrollWheel");
    }

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

    public bool ReadDocument(int number)
    {
        bool wasOpen = IsOpen;
        if (number < 1 || number > DocumentTotal || IsConfirmingEntrance || continueAfterReading != null ||
            !OpenScreen())
            return false;

        bool firstRead = CollectDocument(number);
        ShowPage(number, firstRead, wasOpen ? SoundCue.Paper : SoundCue.BookOpen);
        return true;
    }

    private bool CollectDocument(int number)
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
        if (ReadSupplement(CalculationPage, "Расчёт шести печатей", calculation))
            SealCalculation = calculation;
    }

    internal bool ReadBundle() =>
        ReadSupplement(BundlePage, "Порядок служения", Resources.Load<TextAsset>("TempleArchive/ServiceOrder").text);

    private bool ReadSupplement(int page, string documentTitle, string text)
    {
        if (IsOpen || !OpenScreen()) return false;
        supplementPage = page;
        GameAudio.Interface(page == CalculationPage ? SoundCue.Paper : SoundCue.BookOpen);
        title.text = documentTitle;
        body.text = text;
        RefreshControls();
        FitPage();
        return true;
    }

    internal void RereadFromInventory(int page, Action goBack)
    {
        if (IsOpen || goBack == null) return;
        if (page == CalculationPage && !string.IsNullOrEmpty(SealCalculation))
            ReadSealCalculation(SealCalculation);
        else if (page == BundlePage && TryGetComponent(out PlayerInventory bag) && bag.HasItem(BundleItemId))
            ReadBundle();
        else if (OpenScreen())
        {
            if (!HasDocument(page))
            {
                CloseOverlay();
                goBack();
                return;
            }
            ShowPage(page, false);
        }

        if (!IsOpen) return;
        returnToInventory = goBack;
        RefreshControls();
    }

    public void ConfirmRitualEntrance(Action enter)
    {
        if (enter == null || IsOpen || !OpenScreen())
            return;

        GameAudio.Interface(SoundCue.MenuClick);
        StoryGameplayVoice.FromPlayer(this, gameObject, "temple.ritual.door", "temple_v2");
        pendingEntrance = enter;
        title.text = "Вход в главный зал";
        body.text = "Войти в главный зал? Вернуться в архив после начала ритуала не получится.";
        RefreshControls();
        FitPage();
    }

    public void ConfirmEntrance()
    {
        if (pendingEntrance == null)
            return;

        Action enter = pendingEntrance;
        if (collectedMask == CompleteMask && !FullFamilyTruth)
        {
            pendingEntrance = null;
            ShowPage(DocumentTotal, false);
            continueAfterReading = enter;
            RefreshControls();
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
        if (!IsOpen) return;
        SoundCue closeSound = supplementPage == CalculationPage ? SoundCue.Paper : SoundCue.BookClose;
        GameAudio.Interface(IsConfirmingEntrance ? SoundCue.MenuClick : closeSound);
        Action goBack = returnToInventory;
        CloseOverlay();
        goBack?.Invoke();
    }

    protected override void OnOverlayClosed()
    {
        returnToInventory = null;
        supplementPage = null;
        pendingEntrance = null;
        continueAfterReading = null;
    }

    private void Update()
    {
        if (!Application.isFocused || ControlBindingsPanel.BlocksGameplay || (IsOpen && openedFrame == Time.frameCount))
            return;

        bool journalPressed = input.Pressed(journal);

        if (!IsOpen)
        {
            if (journalPressed && currentDocument != 0 && Time.timeScale > 0f &&
                Cursor.lockState == CursorLockMode.Locked && OpenScreen())
                ShowPage(currentDocument, false);
            return;
        }

        UpdateOpenPage(journalPressed);
    }

    private void UpdateOpenPage(bool journalPressed)
    {
        bool accept = input.Pressed(input.Accept);
        bool cancel = journalPressed || input.Pressed(input.Back);

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

            if (input.Pressed(input.Left))
                FlipPage(-1);
            if (input.Pressed(input.Right))
                FlipPage(1);
        }

        float delta = scrollWheel.ReadValue<Vector2>().y;
        if (delta != 0f)
            input.UseDevice(scrollWheel.activeControl?.device);
        if (input.Pressed(input.Up))
            delta += 1f;
        if (input.Pressed(input.Down))
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
        if (supplementPage.HasValue || returnToInventory != null) return;
        int next = currentDocument;
        do
        {
            next = (next - 1 + direction + DocumentTotal) % DocumentTotal + 1;
        } while (!HasDocument(next));

        if (next != currentDocument) ShowPage(next, false, SoundCue.Paper);
    }

    private void ShowPage(int number, bool firstRead, SoundCue sound = SoundCue.BookOpen)
    {
        GameAudio.Interface(sound);
        currentDocument = number;
        bool sealedRegister = number == DocumentTotal && collectedMask != CompleteMask;
        bool registerFirstRead = number == DocumentTotal && !sealedRegister && !FullFamilyTruth;
        if (sealedRegister && firstRead)
            StoryGameplayVoice.FromPlayer(this, gameObject, "archive.d07.sealed", "temple_v2");
        else if (registerFirstRead)
            StoryGameplayVoice.FromPlayer(this, gameObject, "archive.d07.open", "temple_v2");

        title.text = TempleArchiveText.Titles[number - 1];
        body.text = sealedRegister ? TempleArchiveText.SealedRegister : TempleArchiveText.ReadPage(number);

        RefreshControls();
        FitPage();

        if (registerFirstRead)
            FullFamilyTruth = true;
    }

    private void RefreshControls()
    {
        if (!IsOpen)
            return;

        string accept = input.Label(input.Accept);
        string back = input.Label(input.Back);
        string scrollHint = $"{input.Label(input.Up)} / {input.Label(input.Down)} Прокрутить";
        if (IsConfirmingEntrance)
            controls.text = $"{accept} Войти    {back} Назад";
        else if (continueAfterReading != null)
            controls.text = $"{scrollHint}    {accept} Продолжить";
        else if (returnToInventory != null)
            controls.text = $"{scrollHint}    {back} К вещам";
        else if (supplementPage.HasValue)
            controls.text = $"{scrollHint}    {back} Закрыть";
        else
            controls.text = $"{input.Label(input.Left)} / {input.Label(input.Right)} Листать    {scrollHint}    {back} Закрыть    {input.Label(journal)} Журнал";
    }

    private bool OpenScreen()
    {
        if (!TryOpenOverlay("Archive Journal", 100, new Vector2(1600f, 900f), usePointer: false))
            return false;

        openedFrame = Time.frameCount;
        if (!journal.WasPressedThisFrame())
            input.UseDevice(interact.activeControl?.device);

        ImportRoomNotes();
        return true;
    }

    private void ImportRoomNotes()
    {
        // Older checkpoints can hold room notes only in the inventory.
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

        title = Label(paper, "Document title", .05f, .878f, .95f, .97f, 42, ink, TextAnchor.MiddleLeft);
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 27;
        title.resizeTextMaxSize = 42;

        RectTransform viewport = Rect(paper, "Page viewport", new Vector2(.05f,.135f), new Vector2(.95f,.846f));
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
