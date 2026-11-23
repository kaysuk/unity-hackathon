using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public abstract class PausedGameOverlay : MonoBehaviour
{
    private static PausedGameOverlay active;
    private GameObject canvas;
    private Scene openScene;
    private float previousTimeScale;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisibility;
    private bool usesPointer;

    public bool IsOpen => ReferenceEquals(active, this);

    protected bool TryOpenOverlay(string canvasName, int sortingOrder, Vector2 referenceResolution, bool usePointer)
    {
        if (!isActiveAndEnabled || (active != null && active != this))
            return false;
        if (IsOpen)
            return true;

        openScene = gameObject.scene;
        if (canvas == null)
        {
            canvas = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas.SetActive(false);
            Canvas overlay = canvas.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = sortingOrder;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = .35f;
            BuildView(canvas.transform);
        }
        if (canvas.scene != openScene)
            SceneManager.MoveGameObjectToScene(canvas, openScene);

        previousTimeScale = Time.timeScale;
        previousCursorLock = Cursor.lockState;
        previousCursorVisibility = Cursor.visible;
        usesPointer = usePointer;
        active = this;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        Time.timeScale = 0f;
        if (usesPointer)
            ShowPointer();
        canvas.SetActive(true);
        return true;
    }

    protected void CloseOverlay()
    {
        if (!IsOpen)
            return;

        active = null;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        if (canvas != null)
            canvas.SetActive(false);
        Time.timeScale = previousTimeScale;
        if (Application.isFocused)
        {
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisibility;
        }
        OnOverlayClosed();
    }

    protected abstract void BuildView(Transform root);
    protected abstract void OnOverlayClosed();

    private static void ShowPointer()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (scene == openScene)
            CloseOverlay();
    }

    protected void OnApplicationFocus(bool focused)
    {
        if (focused && IsOpen && usesPointer)
            ShowPointer();
    }

    protected void OnDisable() => CloseOverlay();

    protected void OnDestroy()
    {
        if (canvas != null)
            Destroy(canvas);
    }
}
