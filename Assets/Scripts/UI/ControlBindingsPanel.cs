using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-500)]
public sealed class ControlBindingsPanel : MonoBehaviour
{
    private const string Instructions = "Нажмите на клавишу в списке, чтобы заменить её.";
    private static ControlBindingsPanel instance;
    private static int closedFrame = -1;

    private readonly List<(GameBindings.Entry entry, Text value)> rows = new();
    private GameObject window;
    private RectTransform content;
    private ScrollRect scroll;
    private Text status;
    private CanvasGroup buttons;
    private Font font;
    private EventSystem events;
    private bool navigationBeforeCapture;
    private bool lockedForCapture;
    private int captureEndedFrame = -1;
    private GameObject lastSelection;
    private Action returnToMenu;
    private Scene owner;
    private bool pausedGame;
    private float previousTime;
    private bool previousVisibility;
    private CursorLockMode previousLock;

    public static bool BlocksGameplay => (instance != null && instance.window != null) || Time.frameCount == closedFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (instance != null) return;
        closedFrame = -1;
        var host = new GameObject("Control settings");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<ControlBindingsPanel>();
    }

    public static void OpenFromMenu(Scene scene, Font menuFont, Action onBack)
    {
        Initialize();
        instance.Open(scene, menuFont, onBack);
    }

    private void Update()
    {
        bool escape = Keyboard.current?.escapeKey.wasPressedThisFrame == true;
        if (window != null)
        {
            if (!lockedForCapture && Time.frameCount != captureEndedFrame &&
                (escape || Gamepad.current?.buttonEast.wasPressedThisFrame == true))
                Close(true);
            return;
        }
        if (!escape || BlocksGameplay || Time.timeScale <= 0f)
            return;

        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
            if (player.scene == scene && (scene.name == NimbEncounter.ArenaScene ||
                !player.TryGetComponent<CombatHealth>(out var health) || !health.IsDead))
            {
                Open(scene, GameUiStyle.Font, null);
                break;
            }
    }

    private void Open(Scene scene, Font viewFont, Action onBack)
    {
        if (window != null) return;
        owner = scene;
        font = viewFont;
        returnToMenu = onBack;
        pausedGame = onBack == null;
        if (pausedGame)
        {
            previousTime = Time.timeScale;
            previousLock = Cursor.lockState;
            previousVisibility = Cursor.visible;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        window = new GameObject("Control bindings", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        window.transform.SetParent(transform, false);
        Canvas canvas = window.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = window.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        events = EventSystem.current;
        if (events == null)
        {
            var input = new GameObject("Controls input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            input.transform.SetParent(window.transform, false);
            events = input.GetComponent<EventSystem>();
        }
        Build();
        GameBindings.Changed += Refresh;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (scene == owner) Close(false);
    }

    private void Close(bool goBack)
    {
        if (window == null) return;
        if (GameBindings.IsCapturing) GameBindings.CancelCapture();
        RestoreNavigation();
        GameBindings.Changed -= Refresh;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        window.SetActive(false);
        Destroy(window);
        window = null;
        rows.Clear();
        lastSelection = null;
        if (pausedGame)
        {
            Time.timeScale = previousTime;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisibility;
        }
        pausedGame = false;
        closedFrame = Time.frameCount;
        Action back = returnToMenu;
        returnToMenu = null;
        if (goBack) back?.Invoke();
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        Close(false);
        instance = null;
    }

    private void Refresh()
    {
        foreach (var (entry, value) in rows)
            value.text = entry.DisplayName;
    }

    private void Capture(GameBindings.Entry entry, Text key)
    {
        if (lockedForCapture) return;
        navigationBeforeCapture = events.sendNavigationEvents;
        events.sendNavigationEvents = false;
        lockedForCapture = true;
        captureEndedFrame = -1;
        buttons.interactable = false;
        key.text = "…";
        status.text = "Нажмите новую клавишу или кнопку мыши. Esc — отмена.";
        GameBindings.StartCapture(entry, result =>
        {
            captureEndedFrame = Time.frameCount;
            Refresh();
            status.text = result ?? Instructions;
        });
    }

    private void RestoreNavigation()
    {
        if (!lockedForCapture) return;
        lockedForCapture = false;
        if (events != null) events.sendNavigationEvents = navigationBeforeCapture;
        buttons.interactable = true;
    }

    private void LateUpdate()
    {
        if (window == null) return;
        if (lockedForCapture && captureEndedFrame >= 0 && Time.frameCount >= captureEndedFrame)
            RestoreNavigation();

        GameObject selected = events.currentSelectedGameObject;
        if (selected == lastSelection) return;
        lastSelection = selected;
        if (selected == null || !selected.transform.IsChildOf(content)) return;
        Canvas.ForceUpdateCanvases();
        RectTransform viewport = scroll.viewport;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected.transform);
        float scrollableHeight = content.rect.height - viewport.rect.height;
        if (scrollableHeight <= 0f) return;
        if (bounds.min.y < viewport.rect.yMin)
            scroll.verticalNormalizedPosition -= (viewport.rect.yMin - bounds.min.y) / scrollableHeight;
        else if (bounds.max.y > viewport.rect.yMax)
            scroll.verticalNormalizedPosition += (bounds.max.y - viewport.rect.yMax) / scrollableHeight;
    }

    private void Build()
    {
        var shade = Rect("Backdrop", window.transform);
        Fill(shade);
        // Keep the fullscreen hit area transparent so the menu or paused scene stays visible.
        Paint(shade, Color.clear);
        var card = Rect("Panel", shade);
        card.anchorMin = card.anchorMax = new Vector2(.06f, .5f);
        card.pivot = new Vector2(0f, .5f);
        bool canRestart = pausedGame && owner.name == NimbEncounter.ArenaScene;
        card.sizeDelta = new Vector2(408f, canRestart ? 630f : 584f);
        buttons = card.gameObject.AddComponent<CanvasGroup>();

        var rule = Rect("Rule", card);
        Place(rule, 1f, 0f, 34f, 2f);
        Paint(rule, GameUiStyle.Brass).raycastTarget = false;
        var title = Label("Title", card, "Управление", 42, GameUiStyle.Ink, TextAnchor.MiddleLeft);
        Place(title.rectTransform, 0f, 10f, 408f, 56f);
        status = Label("Instructions", card, Instructions, 17, GameUiStyle.MutedInk, TextAnchor.MiddleLeft);
        Place(status.rectTransform, 1f, 70f, 408f, 44f);

        var list = Rect("Bindings", card);
        Place(list, 0f, 129f, 408f, 387f);
        scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 27;
        var viewport = Rect("Viewport", list);
        Fill(viewport, 0, 0, 12, 0);
        Paint(viewport, Color.clear);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0, 1);
        scroll.viewport = viewport;
        scroll.content = content;
        AddScrollbar(list);

        var navigation = new List<Button[]>();
        float y = 0;
        string section = null;
        foreach (var group in GameBindings.Entries.GroupBy(entry => (entry.Section, entry.Label)))
        {
            if (section != group.Key.Section)
            {
                section = group.Key.Section;
                if (y > 0) y += 15;
                var heading = Label(section, content, section, 18, GameUiStyle.Brass, TextAnchor.MiddleLeft);
                Row(heading.rectTransform, y, 30);
                y += 34;
            }
            var row = Rect(group.Key.Label, content);
            Row(row, y, 32);
            var caption = Label("Action", row, group.Key.Label, 19, GameUiStyle.Ink, TextAnchor.MiddleLeft);
            Fill(caption.rectTransform, 1, 0, 202, 0);
            GameBindings.Entry[] slots = group.ToArray();
            var choices = new Button[slots.Length];
            float width = slots.Length > 2 ? 66f : 98f;
            for (int i = 0; i < slots.Length; i++)
            {
                var entry = slots[i];
                var button = MakeButton(i == 0 ? "Primary" : "Alternative " + i, row);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = new Vector2(1, 0);
                rect.anchorMax = Vector2.one;
                float rightEdge = -201f + (i + 1) * width + i * 2;
                rect.offsetMin = new Vector2(rightEdge - width, 0);
                rect.offsetMax = new Vector2(rightEdge, 0);
                var key = Label("Key", button.transform, entry.DisplayName, 20, Color.white, TextAnchor.MiddleLeft);
                Fill(key.rectTransform, 4, 0, 2, 0);
                button.targetGraphic = key;
                key.resizeTextForBestFit = true;
                key.resizeTextMinSize = 13;
                key.resizeTextMaxSize = 20;
                button.onClick.AddListener(() => Capture(entry, key));
                rows.Add((entry, key));
                choices[i] = button;
            }
            navigation.Add(choices);
            y += 34;
        }
        content.sizeDelta = new Vector2(0, y);

        var reset = Footer("Defaults", "По умолчанию", card, 0);
        reset.onClick.AddListener(() =>
        {
            GameBindings.ResetDefaults();
            Refresh();
            status.text = "Раскладка по умолчанию восстановлена.";
        });
        var back = Footer("Back", pausedGame ? "Вернуться в игру" : "Назад", card, 205);
        back.onClick.AddListener(() => Close(true));
        navigation.Add(new[] { reset, back });
        if (canRestart)
        {
            var restart = Footer("Restart", "Начать бой заново", card, 0, 585f, 260f);
            restart.onClick.AddListener(() =>
            {
                if (GameCheckpoint.RestartRitualFight()) Close(false);
            });
            navigation.Add(new[] { restart });
        }
        for (int i = 0; i < navigation.Count; i++)
        {
            var line = navigation[i];
            var above = navigation[(i + navigation.Count - 1) % navigation.Count];
            var below = navigation[(i + 1) % navigation.Count];
            for (int j = 0; j < line.Length; j++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = above[Mathf.Min(j, above.Length - 1)];
                nav.selectOnDown = below[Mathf.Min(j, below.Length - 1)];
                if (j > 0) nav.selectOnLeft = line[j - 1];
                if (j + 1 < line.Length) nav.selectOnRight = line[j + 1];
                line[j].navigation = nav;
            }
        }
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;
        events.SetSelectedGameObject(navigation[0][0].gameObject);
    }

    private void AddScrollbar(RectTransform list)
    {
        var rail = Rect("Scrollbar", list);
        rail.anchorMin = new Vector2(1, 0);
        rail.anchorMax = Vector2.one;
        rail.offsetMin = new Vector2(-8, 0);
        rail.offsetMax = Vector2.zero;
        Paint(rail, Color.clear);
        var handle = Rect("Handle", rail);
        Fill(handle);
        var line = Rect("Line", handle);
        Fill(line, 3, 0, 3, 0);
        var thumb = Paint(line, new Color32(177, 147, 105, 170));
        thumb.raycastTarget = false;
        var bar = rail.gameObject.AddComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle;
        bar.targetGraphic = thumb;
        bar.navigation = new Navigation { mode = Navigation.Mode.None };
        scroll.verticalScrollbar = bar;
    }

    private Button Footer(string name, string caption, Transform parent, float x, float y = 539f, float width = 187f)
    {
        Button button = MakeButton(name, parent);
        Place((RectTransform)button.transform, x, y, width, 42);
        Text text = Label("Label", button.transform, caption, 23, Color.white, TextAnchor.MiddleLeft);
        button.targetGraphic = text;
        Fill(text.rectTransform);
        return button;
    }

    private static Button MakeButton(string name, Transform parent)
    {
        var rect = Rect(name, parent);
        Paint(rect, Color.clear);
        var button = rect.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = GameUiStyle.Ink;
        colors.highlightedColor = GameUiStyle.Brass;
        colors.selectedColor = GameUiStyle.Brass;
        colors.pressedColor = Color.white;
        colors.disabledColor = colors.normalColor;
        colors.colorMultiplier = 1;
        button.colors = colors;
        return button;
    }

    private Text Label(string name, Transform parent, string value, int size, Color color, TextAnchor alignment)
    {
        var rect = Rect(name, parent);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.text = value;
        label.raycastTarget = false;
        if (pausedGame)
        {
            var shadow = label.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(0f, 0f, 0f, .94f);
            shadow.effectDistance = new Vector2(1.1f, -1.1f);
        }
        return label;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image Paint(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static void Fill(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Row(RectTransform rect, float y, float height)
    {
        rect.anchorMin = rect.pivot = new Vector2(0, 1);
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = new Vector2(0, -y);
        rect.sizeDelta = new Vector2(0, height);
    }
}
