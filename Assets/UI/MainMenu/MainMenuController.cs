using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hackathon.Menu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string VolumePreference = "MainMenu.MasterVolume";

        [SerializeField, TextArea(1, 3)] private string gameTitle = "Тихий\nдом";
        [SerializeField] private Text titleLabel;

        [SerializeField, Tooltip("Project path or build name of the first gameplay scene.")]
        private string firstScene;

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

        private GameObject activePanel;
        private bool transitioning;

        private void Awake()
        {
            titleLabel.text = gameTitle;
        }

        private void Start()
        {
            newGame.onClick.AddListener(() => StartCoroutine(StartGame()));
            settings.onClick.AddListener(OpenSettings);
            exit.onClick.AddListener(() => Show(exitPanel, exitCancel));
            settingsBack.onClick.AddListener(() => Show(mainPanel, settings));
            exitConfirm.onClick.AddListener(Quit);
            exitCancel.onClick.AddListener(() => Show(mainPanel, exit));
            unavailableBack.onClick.AddListener(() => Show(mainPanel, newGame));
            fullscreen.onClick.AddListener(ToggleFullscreen);
            volume.onValueChanged.AddListener(SetVolume);

            float savedVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference, AudioListener.volume));
            volume.SetValueWithoutNotify(savedVolume);
            ApplyVolume(savedVolume);
            Show(mainPanel, null);
            StartCoroutine(ShowIntro());
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
                Show(mainPanel, newGame);
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
            Show(mainPanel, newGame);
            transitioning = false;
        }

        private IEnumerator StartGame()
        {
            if (transitioning)
                yield break;

            transitioning = true;
            newGame.interactable = false;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            yield return FadeTo(1f, 0.32f);
            if (!string.IsNullOrWhiteSpace(firstScene) && Application.CanStreamedLevelBeLoaded(firstScene))
            {
                MainMenuStoryState.Reset();
                SceneManager.LoadScene(firstScene);
                yield break;
            }

            newGame.interactable = true;
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
