using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hackathon.Tests
{
    public sealed class SoundSettingsTests
    {
        private readonly string[] channels = { "Music", "Dialogue", "Effects" };
        private readonly float[] previousValues = new float[3];
        private readonly bool[] hadKeys = new bool[3];
        private float previousMaster;
        private Scene menu;
        private AudioMixer mixer;

        [UnitySetUp]
        public IEnumerator OpenSettings()
        {
            previousMaster = AudioListener.volume;
            for (int i = 0; i < channels.Length; i++)
            {
                string key = "Audio." + channels[i];
                hadKeys[i] = PlayerPrefs.HasKey(key);
                previousValues[i] = PlayerPrefs.GetFloat(key, 1f);
                PlayerPrefs.SetFloat(key, 1f);
            }
            yield return LoadMenu();
            mixer = Resources.Load<AudioMixer>("GameAudio/GameMix");
            yield return null;
        }

        private IEnumerator LoadMenu()
        {
            void OnLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name == "MainMenu") menu = scene;
            }
            SceneManager.sceneLoaded += OnLoaded;
            try { yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Additive); }
            finally { SceneManager.sceneLoaded -= OnLoaded; }
        }

        [UnityTest]
        public IEnumerator SlidersControlIndependentMixerGroupsAndRestorePreferences()
        {
            Transform root = menu.GetRootGameObjects().Single(go => go.name == "Main Menu").transform;
            root.Find("Home/Settings").GetComponent<Button>().onClick.Invoke();
            Transform settings = root.Find("Settings");
            var values = new[] { 0.5f, 0f, 0.25f };
            var master = settings.Find("Volume").GetComponent<Slider>();
            float originalMaster = master.value;
            Selectable previous = master;
            foreach (string channel in channels)
            {
                int i = Array.IndexOf(channels, channel);
                var slider = settings.Find(channel).GetComponent<Slider>();
                Assert.That(previous.navigation.selectOnDown, Is.SameAs(slider));
                Assert.That(slider.navigation.selectOnUp, Is.SameAs(previous));
                previous = slider;
                slider.value = values[i];
                Assert.That(settings.Find(channel + " value").GetComponent<Text>().text,
                    Is.EqualTo($"{Mathf.RoundToInt(values[i] * 100f)}%"));
                Assert.That(PlayerPrefs.GetFloat("Audio." + channel), Is.EqualTo(values[i]));
                for (int j = 0; j < channels.Length; j++)
                    AssertGain(channels[j], j > i ? 1f : values[j]);
            }
            Assert.That(master.value, Is.EqualTo(originalMaster));
            Assert.That(AudioListener.volume, Is.EqualTo(originalMaster));

            var source = root.gameObject.AddComponent<AudioSource>();
            var route = Type.GetType("GameSoundLevels, Assembly-CSharp", true).GetMethod("Route");
            foreach (string channel in channels)
            {
                route.Invoke(null, new object[]{source, Enum.Parse(route.GetParameters()[1].ParameterType, channel)});
                Assert.That(source.outputAudioMixerGroup.name, Is.EqualTo(channel));
            }
            yield return SceneManager.UnloadSceneAsync(menu);
            yield return LoadMenu();
            yield return null;
            root = menu.GetRootGameObjects().Single(go => go.name == "Main Menu").transform;
            for (int i = 0; i < channels.Length; i++)
            {
                Assert.That(root.Find("Settings/" + channels[i]).GetComponent<Slider>().value, Is.EqualTo(values[i]));
                AssertGain(channels[i], values[i]);
            }
        }

        private void AssertGain(string channel, float gain)
        {
            Assert.That(mixer.GetFloat(channel, out float dB), Is.True, channel);
            Assert.That(dB, Is.EqualTo(gain == 0f ? -80f : 20f * Mathf.Log10(gain)).Within(.001f), channel);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            for (int i = 0; i < channels.Length; i++)
            {
                string key = "Audio." + channels[i];
                if (hadKeys[i]) PlayerPrefs.SetFloat(key, previousValues[i]);
                else PlayerPrefs.DeleteKey(key);
                if (mixer != null)
                    mixer.SetFloat(channels[i], previousValues[i] > 0f ? 20f * Mathf.Log10(previousValues[i]) : -80f);
            }
            PlayerPrefs.Save();
            AudioListener.volume = previousMaster;
            if (menu.IsValid() && menu.isLoaded) yield return SceneManager.UnloadSceneAsync(menu);
        }
    }
}
