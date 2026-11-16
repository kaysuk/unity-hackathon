using UnityEngine;
using UnityEngine.UI;

namespace Hackathon.Menu
{
    public sealed class MainMenuBackdrop : MonoBehaviour
    {
        [SerializeField] private RawImage rainyHallway;
        [SerializeField] private MainMenuBackdropEffects effects;
        [SerializeField, Min(0.1f)] private float duskDuration = 3f;
        [SerializeField, Min(0.1f)] private float breakDuration = 0.65f;

        private float weather;
        private float damage;
        private float targetWeather;
        private float targetDamage;

        private void OnEnable()
        {
            ReadStory();
            weather = targetWeather;
            damage = targetDamage;
            Draw();
            MainMenuStoryState.Changed += ReadStory;
        }

        private void OnDisable()
        {
            MainMenuStoryState.Changed -= ReadStory;
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            weather = Mathf.MoveTowards(weather, targetWeather, delta / duskDuration);
            damage = Mathf.MoveTowards(damage, targetDamage, delta / breakDuration);
            Draw();
        }

        private void ReadStory()
        {
            targetWeather = MainMenuStoryState.RainyNight ? 1f : 0f;
            targetDamage = MainMenuStoryState.PhotoFrameBroken ? 1f : 0f;
        }

        private void Draw()
        {
            float evening = Mathf.SmoothStep(0f, 1f, weather);
            rainyHallway.color = new Color(1f, 1f, 1f, evening);
            effects.SetFrame(evening, damage, Time.unscaledTime);
        }

        [ContextMenu("Preview/Rainy night (Play Mode)")]
        private void PreviewRain()
        {
            if (Application.isPlaying)
                MainMenuStoryState.StartRainyNight();
        }

        [ContextMenu("Preview/Break photo frame (Play Mode)")]
        private void PreviewBreak()
        {
            if (Application.isPlaying)
                MainMenuStoryState.BreakPhotoFrame();
        }

        [ContextMenu("Preview/Clear day (Play Mode)")]
        private void PreviewReset()
        {
            if (Application.isPlaying)
                MainMenuStoryState.Reset();
        }
    }
}
