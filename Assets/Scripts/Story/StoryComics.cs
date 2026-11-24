using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// One overlay survives the handoff between scenes so the last page stays on screen during loading.
public sealed class StoryComics : MonoBehaviour
{
    internal const int FinisherPage = 33;
    [SerializeField, Min(0.1f)] private float secondsPerPage = 7f;
    private GameObject canvas;
    private RawImage picture;
    private Text controls;
    private Text kill, spare;
    private float previousScale;
    private CursorLockMode previousLock;
    private bool previousCursor;
    private bool advance;
    private bool choiceMade;
    private Texture2D texture;

    public bool Visible => canvas != null && canvas.activeSelf;
    public int Page { get; private set; }
    public bool DmitrySpared { get; set; }

    public static string Ending(bool spared, bool fullTruth) => spared ? "bad" : fullTruth ? "best" : "good";

    internal IEnumerator PlayRitualEntrance(bool hasAllDocuments) =>
        Play(30, FinisherPage - 1, hasAllDocuments: hasAllDocuments);

    internal IEnumerator Play(int first, int last, string ending = null, bool hasAllDocuments = false)
    {
        if (canvas == null) Build();
        if (!Visible)
        {
            previousScale = Time.timeScale;
            previousLock = Cursor.lockState;
            previousCursor = Cursor.visible;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            canvas.SetActive(true);
        }

        for (int page = first; page <= last; page++)
        {
            string suffix = page == 29 ? (DmitrySpared ? "_alive" : "_dead") :
                page >= 34 ? "_" + ending : "";
            string path = $"comics/{page:00}{suffix}";
            var next = Resources.Load<Texture2D>(path);
            if (next == null) Debug.LogError($"Нет изображения Resources/{path}");
            DropTexture();
            picture.texture = texture = next;
            if (next != null) picture.GetComponent<AspectRatioFitter>().aspectRatio = (float)next.width / next.height;
            Page = page;
            advance = choiceMade = false;
            bool choice = page == 28;
            controls.text = choice ? "Судьба Дмитрия" : "Пробел / A   ·   Дальше";
            spare.gameObject.SetActive(choice);
            kill.gameObject.SetActive(choice);
            string cue = page <= 6 ? "comics/comics5-6" : page <= 14 ? "comics/comics_7-14" :
                page <= 19 ? "comics/comics15-19" : page <= 29 ? "comics/comics20-29" :
                page <= 33 ? "ambience/boss_fight" : "comics/comics34-38_" + ending;
            GameAudio.HoldScore("sounds/" + cue);
            string speechSuffix = page == 31 && hasAllDocuments ? "_archive_complete" : suffix;
            bool voiced = StorySpeech.Page(page, speechSuffix);
            float elapsed = 0f;
            do
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            } while (choice ? !choiceMade : !advance && (voiced ? StorySpeech.Busy || elapsed < 1.2f : elapsed < secondsPerPage));
        }
    }

    public void NextPage() { if (Page != 28) advance = true; }

    public void ChooseDmitry(bool spareHim)
    {
        if (!Visible || Page != 28 || choiceMade) return;
        DmitrySpared = spareHim;
        choiceMade = true;
    }

    private void Update()
    {
        if (!Visible) return;
        var keys = Keyboard.current;
        var pad = Gamepad.current;
        var mouse = Mouse.current;
        bool click = mouse?.leftButton.wasPressedThisFrame == true;
        if (Page == 28)
        {
            if (keys?.digit1Key.wasPressedThisFrame == true || pad?.buttonWest.wasPressedThisFrame == true)
                ChooseDmitry(spareHim: false);
            else if (keys?.digit2Key.wasPressedThisFrame == true || pad?.buttonEast.wasPressedThisFrame == true)
                ChooseDmitry(spareHim: true);
            else if (click)
                ChooseAt(mouse.position.ReadValue());
        }
        else if (click || keys?.spaceKey.wasPressedThisFrame == true || keys?.enterKey.wasPressedThisFrame == true ||
            pad?.buttonSouth.wasPressedThisFrame == true) NextPage();
    }

    private void ChooseAt(Vector2 point)
    {
        if (RectTransformUtility.RectangleContainsScreenPoint(kill.rectTransform, point))
            ChooseDmitry(spareHim: false);
        else if (RectTransformUtility.RectangleContainsScreenPoint(spare.rectTransform, point))
            ChooseDmitry(spareHim: true);
        else
        {
            var art = picture.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(art, point, null, out var local) ||
                !art.rect.Contains(local)) return;

            // The two printed captions occupy the bottom strip of the fitted page 28 illustration.
            Vector2 uv = Rect.PointToNormalized(art.rect, local);
            if (uv.y <= 0.12f) ChooseDmitry(spareHim: uv.x >= 0.5f);
        }
    }

    internal void Close()
    {
        if (!Visible) return;
        canvas.SetActive(false);
        Page = 0;
        DropTexture();
        Time.timeScale = previousScale;
        Cursor.lockState = previousLock;
        Cursor.visible = previousCursor;
        StorySpeech.EndComic();
        GameAudio.ReleaseScore();
    }

    private void DropTexture()
    {
        picture.texture = null;
        if (texture != null) Resources.UnloadAsset(texture);
        texture = null;
    }

    private void Build()
    {
        canvas = new GameObject("Комикс", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Image));
        DontDestroyOnLoad(canvas);
        canvas.SetActive(false);
        var overlay = canvas.GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 32000;
        canvas.GetComponent<Image>().color = Color.black;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        var frame = new GameObject("Страница", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(canvas.transform, false);
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
        frame.offsetMin = new Vector2(0f, 58f); frame.offsetMax = Vector2.zero;
        picture = new GameObject("Иллюстрация", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter)).GetComponent<RawImage>();
        picture.transform.SetParent(frame, false);
        picture.raycastTarget = false;
        picture.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        controls = Label("Управление", 0.37f, 0.63f);
        kill = Label("1 / X   Убить Дмитрия", 0f, 0.37f);
        spare = Label("2 / B   Оставить в живых", 0.63f, 1f);
    }

    private Text Label(string value, float left, float right)
    {
        var label = new GameObject(value, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(canvas.transform, false);
        label.rectTransform.anchorMin = new Vector2(left, 0f);
        label.rectTransform.anchorMax = new Vector2(right, 0f);
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = new Vector2(0f, 58f);
        label.font = GameUiStyle.Font;
        label.fontSize = 23;
        label.color = GameUiStyle.Ink;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = value;
        return label;
    }

    private void OnDestroy()
    {
        Close();
        if (canvas != null) Destroy(canvas);
    }
}
