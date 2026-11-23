using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hackathon.Menu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string VolumePreference = "MainMenu.MasterVolume";

        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject exitPanel;
        [SerializeField] private GameObject unavailablePanel;
        [SerializeField] private Button newGame;
        [SerializeField] private Button settings;
        [SerializeField] private Button exit;
        [SerializeField] private Button settingsBack;
        [SerializeField] private Button fullscreen;
        [SerializeField] private Button exitConfirm;
        [SerializeField] private Button exitCancel;
        [SerializeField] private Button unavailableBack;
        [SerializeField] private Slider volume;
        [SerializeField] private Text volumeValue;
        [SerializeField] private Text fullscreenValue;
        [SerializeField] private CanvasGroup fade;

        private Button continueGame;
        private Vector2 newGamePosition;
        private Vector2 settingsPosition;
        private Vector2 exitPosition;
        private GameObject activePanel;
        private bool transitioning;

        private Button FirstAction => continueGame.gameObject.activeSelf ? continueGame : newGame;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            CreateContinueButton();
            foreach (var button in new[] { newGame, continueGame, settings, exit, settingsBack, exitConfirm, exitCancel,
                unavailableBack, fullscreen })
                button.onClick.AddListener(() => GameAudio.Interface(SoundCue.MenuClick));
            RefreshActions();
            newGame.onClick.AddListener(() => StartCoroutine(LoadGame(false)));
            continueGame.onClick.AddListener(() => StartCoroutine(LoadGame(true)));
            settings.onClick.AddListener(OpenSettings);
            exit.onClick.AddListener(() => Show(exitPanel, exitCancel));
            settingsBack.onClick.AddListener(() => Show(mainPanel, settings));
            exitConfirm.onClick.AddListener(Quit);
            exitCancel.onClick.AddListener(() => Show(mainPanel, exit));
            unavailableBack.onClick.AddListener(() => Show(mainPanel, FirstAction));
            fullscreen.onClick.AddListener(ToggleFullscreen);
            volume.onValueChanged.AddListener(SetVolume);

            float savedVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference, AudioListener.volume));
            volume.SetValueWithoutNotify(savedVolume);
            ApplyVolume(savedVolume);
            Show(mainPanel, null);
            StartCoroutine(ShowIntro());
        }

        private void CreateContinueButton()
        {
            newGamePosition = ((RectTransform)newGame.transform).anchoredPosition;
            settingsPosition = ((RectTransform)settings.transform).anchoredPosition;
            exitPosition = ((RectTransform)exit.transform).anchoredPosition;
            continueGame = Instantiate(newGame, newGame.transform.parent, false);
            continueGame.gameObject.name = "Continue";
            continueGame.transform.SetSiblingIndex(newGame.transform.GetSiblingIndex());
            continueGame.GetComponentInChildren<Text>().text = "Продолжить";
        }

        private void RefreshActions()
        {
            bool hasSave = GameCheckpoint.HasSave;
            continueGame.gameObject.SetActive(hasSave);
            Vector2 extraRow = hasSave ? settingsPosition - newGamePosition : Vector2.zero;
            ((RectTransform)newGame.transform).anchoredPosition = newGamePosition + extraRow;
            ((RectTransform)settings.transform).anchoredPosition = settingsPosition + extraRow;
            ((RectTransform)exit.transform).anchoredPosition = exitPosition + extraRow;

            SetVerticalNavigation(continueGame, exit, newGame);
            SetVerticalNavigation(newGame, hasSave ? continueGame : exit, settings);
            SetVerticalNavigation(exit, settings, FirstAction);
        }

        private static void SetVerticalNavigation(Button button, Selectable up, Selectable down)
        {
            Navigation navigation = button.navigation;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            button.navigation = navigation;
        }

        public void GoBack()
        {
            if (transitioning)
                return;

            if (activePanel == settingsPanel)
                Show(mainPanel, settings);
            else if (activePanel == exitPanel)
                Show(mainPanel, exit);
            else if (activePanel == unavailablePanel)
                Show(mainPanel, FirstAction);
        }

        private void OpenSettings()
        {
            RefreshFullscreenLabel();
            Show(settingsPanel, volume);
        }

        private void Show(GameObject panel, Selectable focus)
        {
            mainPanel.SetActive(panel == mainPanel);
            settingsPanel.SetActive(panel == settingsPanel);
            exitPanel.SetActive(panel == exitPanel);
            unavailablePanel.SetActive(panel == unavailablePanel);
            activePanel = panel;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(focus == null ? null : focus.gameObject);
        }

        private IEnumerator ShowIntro()
        {
            transitioning = true;
            fade.alpha = 1f;
            yield return FadeTo(0f, 0.35f);
            Show(mainPanel, FirstAction);
            transitioning = false;
        }

        private IEnumerator LoadGame(bool resume)
        {
            if (transitioning)
                yield break;

            transitioning = true;
            newGame.interactable = false;
            continueGame.interactable = false;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            yield return FadeTo(1f, 0.32f);
            bool loading = resume ? GameCheckpoint.ContinueGame() : GameCheckpoint.StartNewGame();
            if (loading)
                yield break;

            newGame.interactable = true;
            continueGame.interactable = true;
            RefreshActions();
            unavailablePanel.transform.Find("Status").GetComponent<Text>().text = resume
                ? "Не удалось загрузить\nсохранение."
                : "Не удалось запустить\nновую игру.";
            Show(unavailablePanel, null);
            yield return FadeTo(0f, 0.25f);
            Show(unavailablePanel, unavailableBack);
            transitioning = false;
        }

        private IEnumerator FadeTo(float target, float seconds)
        {
            fade.blocksRaycasts = true;
            float start = fade.alpha;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                fade.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / seconds));
                yield return null;
            }
            fade.alpha = target;
            fade.blocksRaycasts = target > 0f;
        }

        private void SetVolume(float value)
        {
            ApplyVolume(value);
            PlayerPrefs.SetFloat(VolumePreference, value);
        }

        private void ApplyVolume(float value)
        {
            AudioListener.volume = value;
            volumeValue.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        private void ToggleFullscreen()
        {
            bool fullscreenEnabled = !Screen.fullScreen;
            Screen.fullScreen = fullscreenEnabled;
            fullscreenValue.text = fullscreenEnabled ? "Полный экран" : "Оконный";
        }

        private void RefreshFullscreenLabel()
        {
            fullscreenValue.text = Screen.fullScreen ? "Полный экран" : "Оконный";
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
