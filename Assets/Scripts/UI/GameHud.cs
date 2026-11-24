using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
public sealed class GameHud : MonoBehaviour
{
    private static readonly Color EmptyInk = new Color32(240, 180, 157, 255);

    private Canvas canvas;
    private RawImage bloodRim;
    private BloodSplash leftSplash;
    private BloodSplash rightSplash;
    private bool lastHitWasRight = true;
    private GameObject objectivesPanel;
    private Text primaryObjective;
    private Text secondaryObjective;
    private GameObject ammoPanel;
    private Text ammoCaption;
    private Text ammoCount;
    private Text inventoryHint;
    private Text senseHint;

    private float lostHealth;
    private int displayedMagazine = -1;
    private int displayedReserve = -1;
    private bool displayedReload;
    private Font font;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        font = GameUiStyle.Font;

        bloodRim = CreateBloodLayer("Blood rim", "KaramazovyHud/BloodRim");
        leftSplash = new BloodSplash(CreateBloodLayer("Blood spatter", "KaramazovyHud/BloodSpatter"), false);
        rightSplash = new BloodSplash(CreateBloodLayer("Blood spatter right", "KaramazovyHud/BloodSprayRight"), true);
        OnRectTransformDimensionsChange();
        BuildObjectives();
        BuildAmmo();
        inventoryHint = CreateShortcut("Inventory shortcut", "Tab / Start    Инвентарь");
        senseHint = CreateShortcut("Sense shortcut", string.Empty);
        RefreshSenseHint();
        GameBindings.Changed += RefreshSenseHint;
        UpdateBlood();
    }

    private void RefreshSenseHint()
    {
        string keyboard = GameBindings.Label("Player/Investigate");
        string gamepad = GameBindings.Label("Player/Investigate", true);
        senseHint.text = $"{keyboard} / {gamepad}    Чутьё детектива (удерживать)";
    }

    private void OnDestroy() => GameBindings.Changed -= RefreshSenseHint;

    private Text CreateShortcut(string name, string text)
    {
        Text label = AddLabel(transform, name, 25, GameUiStyle.Ink, TextAnchor.MiddleLeft);
        label.text = text;
        label.rectTransform.pivot = Vector2.zero;
        label.rectTransform.anchoredPosition = new Vector2(38, 38);
        label.rectTransform.sizeDelta = new Vector2(700, 46);
        label.gameObject.SetActive(false);
        return label;
    }

    internal void SetShortcutHints(bool inventory, bool sense)
    {
        inventoryHint.gameObject.SetActive(inventory);
        senseHint.rectTransform.anchoredPosition = new Vector2(38, inventory ? 77 : 38);
        senseHint.gameObject.SetActive(sense);
    }

    public void SetGameplayVisible(bool visible)
    {
        if (canvas.enabled != visible)
            canvas.enabled = visible;
    }

    public void SetObjectives(string primary, string secondary)
    {
        bool hasPrimary = !string.IsNullOrWhiteSpace(primary);
        bool hasSecondary = !string.IsNullOrWhiteSpace(secondary);
        primaryObjective.text = primary ?? string.Empty;
        secondaryObjective.text = secondary ?? string.Empty;
        primaryObjective.gameObject.SetActive(hasPrimary);
        secondaryObjective.gameObject.SetActive(hasSecondary);
        objectivesPanel.SetActive(hasPrimary || hasSecondary);
    }

    public void SetAmmo(int inMagazine, int reserve, bool reloading, bool available)
    {
        if (ammoPanel.activeSelf != available)
            ammoPanel.SetActive(available);
        if (!available)
            return;

        if (displayedMagazine != inMagazine || displayedReserve != reserve)
        {
            displayedMagazine = inMagazine;
            displayedReserve = reserve;
            string magazineColor = ColorUtility.ToHtmlStringRGB(inMagazine == 0 ? EmptyInk : GameUiStyle.Ink);
            ammoCount.text = $"<color=#{magazineColor}>{inMagazine}</color> <size=32>/ {reserve}</size>";
        }

        if (displayedReload != reloading)
        {
            displayedReload = reloading;
            ammoCaption.text = reloading ? "Перезарядка…" : "Пистолет";
            ammoCaption.color = reloading ? GameUiStyle.Brass : GameUiStyle.MutedInk;
        }
    }

    public void SetHealth(float hp, float maxHp, bool tookHit) => SetHealth(hp, maxHp, tookHit, 0f);

    internal void SetHealth(float hp, float maxHp, bool tookHit, float sourceSide)
    {
        float damage = maxHp > 0f ? 1f - Mathf.Clamp01(hp / maxHp) : 0f;
        if (tookHit && damage > 0f)
        {
            // Centered or unattributed hits alternate between the two margins.
            lastHitWasRight = Mathf.Abs(sourceSide) < .15f ? !lastHitWasRight : sourceSide > 0f;
            var splash = lastHitWasRight ? rightSplash : leftSplash;
            splash.Hit(Mathf.Lerp(.60f, 1f, Mathf.Clamp01((damage - lostHealth) * 2.5f)));
        }
        else
        {
            leftSplash.Settle();
            rightSplash.Settle();
        }

        if (damage == 0f) ClearBlood();
        lostHealth = damage;
        UpdateBlood();
    }

    internal void ClearBlood()
    {
        lastHitWasRight = true;
        leftSplash.Clear();
        rightSplash.Clear();
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f || (leftSplash.Wetness <= 0f && rightSplash.Wetness <= 0f)) return;
        leftSplash.Advance(Time.deltaTime);
        rightSplash.Advance(Time.deltaTime);
        UpdateBlood();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (leftSplash == null || rightSplash == null) return;
        Vector2 screen = ((RectTransform)transform).rect.size;
        leftSplash.Resize(screen);
        rightSplash.Resize(screen);
    }

    private void UpdateBlood()
    {
        float rim = Mathf.SmoothStep(0f, .23f, lostHealth);
        SetBloodOpacity(bloodRim, rim + .12f * Mathf.Max(leftSplash.Wetness, rightSplash.Wetness));
        leftSplash.Draw(lostHealth);
        rightSplash.Draw(lostHealth);
    }

    private sealed class BloodSplash
    {
        private readonly RawImage image;
        private readonly bool right;
        private float strength;
        public float Wetness { get; private set; }

        public BloodSplash(RawImage image, bool right)
        {
            this.image = image;
            this.right = right;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(right ? 1f : 0f, right ? .64f : .49f);
            rect.pivot = new Vector2(right ? 1f : 0f, .5f);
        }

        public void Resize(Vector2 screen)
        {
            if (image.texture == null) return;
            float aspect = (float)image.texture.width / image.texture.height;
            float height = Mathf.Min(screen.y * (right ? .85f : 1.02f), screen.x * .32f / aspect);
            image.rectTransform.sizeDelta = new Vector2(height * aspect, height);
        }

        public void Hit(float amount)
        {
            strength = amount;
            Wetness = 1f;
        }

        public void Advance(float deltaTime) => Wetness = Mathf.MoveTowards(Wetness, 0f, deltaTime / 1.15f);
        public void Settle() => Wetness = 0f;
        public void Clear() { strength = 0f; Wetness = 0f; }

        public void Draw(float injury)
        {
            float fresh = Wetness * Wetness;
            float residual = Mathf.SmoothStep(0f, .82f, injury);
            float alpha = strength * Mathf.Lerp(residual, 1f, fresh);
            float slide = (1f - fresh) * image.rectTransform.rect.height * .016f;
            image.rectTransform.anchoredPosition = Vector2.down * slide;
            image.enabled = alpha > 0f && image.texture != null;
            image.color = new Color(Mathf.Lerp(.76f, 1f, fresh), Mathf.Lerp(.69f, 1f, fresh),
                Mathf.Lerp(.74f, 1f, fresh), alpha);
        }
    }

    private static void SetBloodOpacity(RawImage layer, float opacity)
    {
        layer.enabled = opacity > 0f && layer.texture != null;
        layer.color = new Color(1f, 1f, 1f, opacity);
    }

    private RawImage CreateBloodLayer(string name, string resource)
    {
        RectTransform rect = CreateRect(transform, name);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var layer = rect.gameObject.AddComponent<RawImage>();
        layer.texture = Resources.Load<Texture2D>(resource);
        layer.raycastTarget = false;
        return layer;
    }

    private void BuildObjectives()
    {
        RectTransform rect = CreateRect(transform, "Objectives");
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(38f, -38f);
        rect.sizeDelta = new Vector2(515f, 0f);
        AddFill(rect, GameUiStyle.Panel);
        objectivesPanel = rect.gameObject;

        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 21, 14);
        layout.spacing = 5f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fit = rect.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform marker = CreateRect(rect, "Objective rule");
        marker.anchorMin = marker.anchorMax = marker.pivot = Vector2.up;
        marker.anchoredPosition = new Vector2(22f, -13f);
        marker.sizeDelta = new Vector2(44f, 2f);
        AddFill(marker, GameUiStyle.Brass);
        marker.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        primaryObjective = AddLabel(rect, "Primary objective", 34, GameUiStyle.Ink, TextAnchor.MiddleLeft);
        secondaryObjective = AddLabel(rect, "Additional objective", 28, GameUiStyle.MutedInk, TextAnchor.MiddleLeft);
        objectivesPanel.SetActive(false);
    }

    private void BuildAmmo()
    {
        RectTransform rect = CreateRect(transform, "Pistol ammunition");
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-38f, 38f);
        rect.sizeDelta = new Vector2(212f, 118f);
        AddFill(rect, GameUiStyle.Panel);
        ammoPanel = rect.gameObject;

        RectTransform rule = CreateRect(rect, "Ammo rule");
        rule.anchorMin = rule.anchorMax = rule.pivot = Vector2.one;
        rule.anchoredPosition = new Vector2(-18f, -13f);
        rule.sizeDelta = new Vector2(44f, 2f);
        AddFill(rule, GameUiStyle.Brass);

        ammoCaption = AddLabel(rect, "Weapon or reload status", 25, GameUiStyle.MutedInk, TextAnchor.MiddleRight);
        InsetRow(ammoCaption.rectTransform, 69f, 36f);
        ammoCaption.text = "Пистолет";
        ammoCount = AddLabel(rect, "Magazine and reserve", 48, GameUiStyle.MutedInk, TextAnchor.MiddleRight);
        InsetRow(ammoCount.rectTransform, 5f, 69f);
        ammoCount.supportRichText = true;
        ammoPanel.SetActive(false);
    }

    private static void InsetRow(RectTransform rect, float bottom, float height)
    {
        rect.anchorMax = Vector2.right;
        rect.pivot = new Vector2(.5f, 0f);
        rect.sizeDelta = new Vector2(-36f, height);
        rect.anchoredPosition = new Vector2(0f, bottom);
    }

    private Text AddLabel(Transform parent, string name, int size, Color color, TextAnchor alignment)
    {
        var label = CreateRect(parent, name).gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.supportRichText = false;
        return label;
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static void AddFill(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }
}
