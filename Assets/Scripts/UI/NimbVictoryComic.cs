using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NimbVictoryComic : PausedGameOverlay
{
    // Place numbered page textures under Assets/Resources/NimbVictoryComic in reading order.
    private const string PagesResource = "NimbVictoryComic";
    private const string MainMenu = "MainMenu";
    private const float FadeSeconds = .55f;

    private OverlayControls input;
    private Texture2D[] pages;
    private CanvasGroup curtain;
    private RawImage pageImage;
    private AspectRatioFitter pageFit;
    private Text title;
    private Text controls;
    private int pageIndex;
    private float openedAt;
    private bool leaving;

    private void Awake() => input = new OverlayControls(RefreshControls);

    internal void Begin()
    {
        pages = Resources.LoadAll<Texture2D>(PagesResource);
        Array.Sort(pages, (left, right) => StringComparer.Ordinal.Compare(left.name, right.name));
        if (!TryOpenOverlay("Nimb victory comic", 200, new Vector2(1600f, 900f), usePointer: false))
            return;

        openedAt = Time.unscaledTime;
        curtain.alpha = 0f;
        ShowPage();
    }

    private void Update()
    {
        if (!IsOpen || leaving || ControlBindingsPanel.BlocksGameplay)
            return;

        float age = Time.unscaledTime - openedAt;
        curtain.alpha = Mathf.Clamp01(age / FadeSeconds);
        if (age < FadeSeconds)
            return;

        if (input.Pressed(input.Back))
            ExitToMenu();
        else if (input.Pressed(input.Accept))
            Advance();
    }

    private void Advance()
    {
        if (pageIndex + 1 >= pages.Length)
        {
            ExitToMenu();
            return;
        }

        pageIndex++;
        ShowPage();
    }

    private void ExitToMenu()
    {
        leaving = true;
        SceneManager.LoadSceneAsync(MainMenu, LoadSceneMode.Single);
    }

    private void ShowPage()
    {
        bool hasPage = pages.Length != 0;
        pageImage.enabled = hasPage;
        title.enabled = !hasPage;
        if (hasPage)
        {
            Texture2D page = pages[pageIndex];
            pageImage.texture = page;
            pageFit.aspectRatio = (float)page.width / page.height;
        }

        RefreshControls();
    }

    private void RefreshControls()
    {
        if (!IsOpen)
            return;

        string advanceKey = input.Label(input.Accept);
        controls.text = pageIndex + 1 < pages.Length
            ? $"{advanceKey}  Дальше     {input.Label(input.Back)}  Пропустить"
            : $"{advanceKey}  В меню";
    }

    protected override void BuildView(Transform root)
    {
        curtain = root.gameObject.AddComponent<CanvasGroup>();
        RectTransform backdrop = AddRect(root, "Backdrop", Vector2.zero, Vector2.one);
        backdrop.gameObject.AddComponent<Image>().color = new Color32(10, 8, 8, 255);

        RectTransform frame = AddRect(backdrop, "Page area", new Vector2(.035f, .105f), new Vector2(.965f, .965f));
        RectTransform page = AddRect(frame, "Comic page", Vector2.zero, Vector2.one);
        pageImage = page.gameObject.AddComponent<RawImage>();
        pageImage.raycastTarget = false;
        pageFit = page.gameObject.AddComponent<AspectRatioFitter>();
        pageFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        title = AddText(backdrop, "Victory", "Нимб повержен", 82, GameUiStyle.Ink, new Vector2(.1f, .38f),
            new Vector2(.9f, .62f));
        controls = AddText(backdrop, "Comic controls", string.Empty, 29, GameUiStyle.MutedInk,
            new Vector2(.06f, .016f), new Vector2(.94f, .09f));
    }

    private static RectTransform AddRect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var child = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Text AddText(Transform parent, string name, string content, int size, Color color,
        Vector2 min, Vector2 max)
    {
        Text text = AddRect(parent, name, min, max).gameObject.AddComponent<Text>();
        text.font = GameUiStyle.Font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.text = content;
        return text;
    }

    protected override void OnOverlayClosed() { }
}
