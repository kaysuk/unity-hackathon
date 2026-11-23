using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class GameAudioTests
    {
        [Test]
        public void EverySoundCueHasAMonoRecording()
        {
            var cues = Type.GetType("SoundCue, Assembly-CSharp", true);
            foreach (string name in Enum.GetNames(cues))
            {
                var clip = Resources.Load<AudioClip>("GameAudio/" + name);
                Assert.That(clip, Is.Not.Null, name);
                Assert.That(clip.channels, Is.EqualTo(1), name);
                Assert.That(clip.samples, Is.GreaterThan(100), name);
            }
        }

        [UnityTest]
        public IEnumerator TemplePickupAndRitualSceneUseTheirAssignedAudio()
        {
            var original = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync("temple_v2", LoadSceneMode.Additive);
            var temple = SceneManager.GetSceneByName("temple_v2");
            SceneManager.SetActiveScene(temple);
            var audio = GameObject.Find("Game audio");
            var objects = temple.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>()).ToArray();
            var player = objects.Single(component => component.GetType().Name == "PlayerInteractor");
            var pickup = objects.First(component => component.GetType().Name == "ItemPickup" &&
                ((string)component.GetType().GetProperty("ItemId").GetValue(component)).StartsWith("rune_"));
            try
            {
                Assert.That(audio.GetComponentsInChildren<AudioSource>().Any(source =>
                    source.loop && source.clip?.name == "Undercroft" && source.isPlaying), Is.True);
                pickup.GetType().GetMethod("Interact").Invoke(pickup, new object[] { player });
                Assert.That(pickup.gameObject.activeSelf, Is.False);
                Assert.That(audio.GetComponentsInChildren<AudioSource>().Any(source =>
                    source.clip?.name == "Stone" && source.isPlaying &&
                    source.spatialBlend == 1f && source.transform.position == pickup.transform.position), Is.True);
                yield return SceneManager.LoadSceneAsync("Temple_Final_Room", LoadSceneMode.Additive);
                SceneManager.SetActiveScene(SceneManager.GetSceneByName("Temple_Final_Room"));
                Assert.That(audio.GetComponentsInChildren<AudioSource>().Any(source =>
                    source.loop && source.clip?.name == "Ritual" && source.isPlaying), Is.True);
            }
            finally
            {
                SceneManager.SetActiveScene(original);
            }
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            foreach (string name in new[] { "Temple_Final_Room", "temple_v2" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
