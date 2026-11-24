using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hackathon.Menu
{
    public sealed class MainMenuController : MonoBehaviour
    {
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
        private Button controls;
        private Button resetMemory;
        private GameObject memoryPanel;
        private Text memoryStatus;
        private Button memoryBack;
        private Button memoryConfirm;
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
            CreateControlsButton();
            CreateMemoryMenu();
            foreach (var button in new[] { newGame, continueGame, settings, exit, settingsBack, exitConfirm, exitCancel,
                unavailableBack, fullscreen, controls, resetMemory, memoryBack, memoryConfirm })
                button.onClick.AddListener(() => GameAudio.Interface(SoundCue.MenuClick));
            RefreshActions();
            newGame.onClick.AddListener(() => StartCoroutine(LoadGame(false)));
            continueGame.onClick.AddListener(() => StartCoroutine(LoadGame(true)));
            settings.onClick.AddListener(OpenSettings);
            controls.onClick.AddListener(OpenControls);
            resetMemory.onClick.AddListener(OpenMemory);
            memoryConfirm.onClick.AddListener(ResetMemory);
            memoryBack.onClick.AddListener(() => Show(mainPanel, resetMemory));
            exit.onClick.AddListener(() => Show(exitPanel, exitCancel));
            settingsBack.onClick.AddListener(() => Show(mainPanel, settings));
            exitConfirm.onClick.AddListener(Quit);
            exitCancel.onClick.AddListener(() => Show(mainPanel, exit));
            unavailableBack.onClick.AddListener(() => Show(mainPanel, FirstAction));
            fullscreen.onClick.AddListener(ToggleFullscreen);
            ConfigureAudioSliders();
            GameSoundLevels.ApplySaved();
            volume.SetValueWithoutNotify(AudioListener.volume);
            ApplyVolume(volume.value);
            volume.onValueChanged.AddListener(SetVolume);
            Show(mainPanel, null);
            StartCoroutine(ShowIntro());
        }

        private void ConfigureAudioSliders()
        {
            Text label = settingsPanel.transform.Find("Volume label").GetComponent<Text>();
            label.text = "Общая громкость";
            label.fontSize = 31;
            Position(label.rectTransform, 4f, -222f, 232f);
            Position((RectTransform)volume.transform, 240f, -223f, 245f);
            volume.handleRect.sizeDelta = new Vector2(volume.handleRect.sizeDelta.x, -5f);
            Position(volumeValue.rectTransform, 493f, -222f, 90f);

            var music = AddVolumeSlider(GameSoundChannel.Music, "Музыка", label, -278f);
            var dialogue = AddVolumeSlider(GameSoundChannel.Dialogue, "Диалоги", label, -334f);
            var effects = AddVolumeSlider(GameSoundChannel.Effects, "Звуки", label, -390f);
            SetVerticalNavigation(volume, settingsBack, music);
            SetVerticalNavigation(music, volume, dialogue);
            SetVerticalNavigation(dialogue, music, effects);
            SetVerticalNavigation(effects, dialogue, fullscreen);
            SetVerticalNavigation(fullscreen, effects, controls);
            SetVerticalNavigation(controls, fullscreen, settingsBack);
            SetVerticalNavigation(settingsBack, controls, volume);

            var screenLabel = (RectTransform)settingsPanel.transform.Find("Screen label");
            screenLabel.anchoredPosition = new Vector2(4f, -463f);
            ((RectTransform)fullscreen.transform).anchoredPosition = new Vector2(4f, -497f);
            ((RectTransform)settingsPanel.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 740f);
            ((RectTransform)controls.transform).anchoredPosition = new Vector2(4f, -575f);
            ((RectTransform)settingsBack.transform).anchoredPosition = new Vector2(4f, -653f);
        }

        private Slider AddVolumeSlider(GameSoundChannel channel, string caption, Text labelTemplate, float y)
        {
            Transform panel = settingsPanel.transform;
            var label = Instantiate(labelTemplate, panel, false);
            var slider = Instantiate(volume, panel, false);
            var valueLabel = Instantiate(volumeValue, panel, false);
            label.name = channel + " label";
            label.text = caption;
            slider.name = channel.ToString();
            valueLabel.name = channel + " value";
            label.rectTransform.anchoredPosition = new Vector2(4f, y);
            ((RectTransform)slider.transform).anchoredPosition = new Vector2(240f, y - 1f);
            valueLabel.rectTransform.anchoredPosition = new Vector2(493f, y);
            slider.SetValueWithoutNotify(GameSoundLevels.Get(channel));
            valueLabel.text = FormatPercent(slider.value);
            slider.onValueChanged.AddListener(value =>
            {
                GameSoundLevels.Set(channel, value);
                valueLabel.text = FormatPercent(value);
            });
            return slider;
        }

        private static void Position(RectTransform rect, float x, float y, float width)
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        private static string FormatPercent(float value) => $"{Mathf.RoundToInt(value * 100)}%";

        private void CreateContinueButton()
        {
            newGamePosition = ((RectTransform)newGame.transform).anchoredPosition;
            settingsPosition = ((RectTransform)settings.transform).anchoredPosition;
            exitPosition = ((RectTransform)exit.transform).anchoredPosition;
            continueGame = Instantiate(newGame, newGame.transform.parent, false);
            continueGame.gameObject.name = "Continue";
            continueGame.transform.SetSiblingIndex(newGame.transform.GetSiblingIndex());
            continueGame.GetComponentInChildren<Text>().text = "Продолжить игру";
        }

        private void CreateControlsButton()
        {
            controls = Instantiate(settingsBack, settingsPanel.transform, false);
            controls.name = "Controls";
            controls.GetComponentInChildren<Text>().text = "Управление";
        }

        private void OpenControls()
        {
            Show(null, null);
            ControlBindingsPanel.OpenFromMenu(gameObject.scene, controls.GetComponentInChildren<Text>(true).font,
                () => Show(settingsPanel, controls));
        }

        private void CreateMemoryMenu()
        {
            resetMemory = Instantiate(settings, mainPanel.transform, false);
            resetMemory.name = "Reset Nimb memory";
            resetMemory.transform.SetSiblingIndex(exit.transform.GetSiblingIndex());
            var label = resetMemory.GetComponentInChildren<Text>();
            label.text = "Стереть память Нимба";
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500f);
            ((RectTransform)resetMemory.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 552f);

            memoryPanel = Instantiate(unavailablePanel, unavailablePanel.transform.parent, false);
            memoryPanel.name = "Nimb memory";
            memoryPanel.transform.SetSiblingIndex(unavailablePanel.transform.GetSiblingIndex());
            memoryPanel.transform.Find("Heading").GetComponent<Text>().text = "Память Нимба";
            memoryStatus = memoryPanel.transform.Find("Status").GetComponent<Text>();
            memoryBack = memoryPanel.transform.Find(unavailableBack.name).GetComponent<Button>();
            memoryConfirm = Instantiate(exitConfirm, memoryPanel.transform, false);
            memoryConfirm.name = "Confirm";
            memoryConfirm.GetComponentInChildren<Text>(true).text = "Стереть память";
            var backRect = (RectTransform)memoryBack.transform;
            ((RectTransform)memoryConfirm.transform).anchoredPosition = backRect.anchoredPosition
                + exitPosition - settingsPosition;
            SetVerticalNavigation(memoryConfirm, memoryBack, memoryBack);
        }

        private void OpenMemory()
        {
            memoryStatus.text = "Нимб забудет, как вы сражались.\nВаше прохождение сохранится.";
            memoryBack.GetComponentInChildren<Text>(true).text = "Отмена";
            memoryConfirm.gameObject.SetActive(true);
            SetVerticalNavigation(memoryBack, memoryConfirm, memoryConfirm);
            Show(memoryPanel, memoryBack);
        }

        private void ResetMemory()
        {
            if (!NimbAttackLearning.TryResetToPackaged())
            {
                memoryStatus.text = "Не удалось стереть память Нимба.\nПопробуйте ещё раз.";
                return;
            }

            memoryStatus.text = "Нимб забыл ваши прошлые бои.";
            memoryBack.GetComponentInChildren<Text>(true).text = "В главное меню";
            memoryConfirm.gameObject.SetActive(false);
            SetVerticalNavigation(memoryBack, null, null);
            Show(memoryPanel, memoryBack);
        }

        private void RefreshActions()
        {
            bool hasSave = GameCheckpoint.HasSave;
            bool hasMemory = GameCheckpoint.HasReachedRitual;
            continueGame.gameObject.SetActive(hasSave);
            resetMemory.gameObject.SetActive(hasMemory);
            Vector2 row = settingsPosition - newGamePosition;
            Vector2 extraRow = hasSave ? row : Vector2.zero;
            ((RectTransform)newGame.transform).anchoredPosition = newGamePosition + extraRow;
            ((RectTransform)settings.transform).anchoredPosition = settingsPosition + extraRow;
            ((RectTransform)resetMemory.transform).anchoredPosition = exitPosition + extraRow;
            ((RectTransform)exit.transform).anchoredPosition = exitPosition + extraRow + (hasMemory ? row : Vector2.zero);

            SetVerticalNavigation(continueGame, exit, newGame);
            SetVerticalNavigation(newGame, hasSave ? continueGame : exit, settings);
            SetVerticalNavigation(settings, newGame, hasMemory ? resetMemory : exit);
            SetVerticalNavigation(resetMemory, settings, exit);
            SetVerticalNavigation(exit, hasMemory ? resetMemory : settings, FirstAction);
        }

        private static void SetVerticalNavigation(Selectable button, Selectable up, Selectable down)
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
            else if (activePanel == memoryPanel)
                Show(mainPanel, resetMemory);
        }

        private void OpenSettings()
        {
            RefreshFullscreenLabel();
            Show(settingsPanel, volume);
        }

        private void Show(GameObject panel, Selectable focus)
        {
            if (activePanel == settingsPanel && panel != settingsPanel) PlayerPrefs.Save();
            mainPanel.SetActive(panel == mainPanel);
            settingsPanel.SetActive(panel == settingsPanel);
            exitPanel.SetActive(panel == exitPanel);
            unavailablePanel.SetActive(panel == unavailablePanel);
            memoryPanel.SetActive(panel == memoryPanel);
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
            PlayerPrefs.SetFloat(GameSoundLevels.MasterPreference, value);
        }

        private void ApplyVolume(float value)
        {
            AudioListener.volume = value;
            volumeValue.text = FormatPercent(value);
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
