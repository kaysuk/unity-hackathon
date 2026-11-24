using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
                    source.loop && source.clip?.name == "temple" && source.isPlaying), Is.True);
                pickup.GetType().GetMethod("Interact").Invoke(pickup, new object[] { player });
                Assert.That(pickup.gameObject.activeSelf, Is.False);
                Assert.That(audio.GetComponentsInChildren<AudioSource>().Any(source =>
                    source.clip?.name == "Stone" && source.isPlaying &&
                    source.spatialBlend == 1f && source.transform.position == pickup.transform.position), Is.True);
                yield return SceneManager.LoadSceneAsync("Temple_Final_Room", LoadSceneMode.Additive);
                SceneManager.SetActiveScene(SceneManager.GetSceneByName("Temple_Final_Room"));
                Assert.That(audio.GetComponentsInChildren<AudioSource>().Any(source =>
                    source.loop && source.clip?.name == "boss_fight" && source.isPlaying), Is.True);
            }
            finally
            {
                SceneManager.SetActiveScene(original);
            }
        }

        [UnityTest]
        public IEnumerator BossDispelCarriesIntoTheEndingWithoutOtherAttackSounds()
        {
            var original = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync("Temple_Final_Room", LoadSceneMode.Additive);
            var arena = SceneManager.GetSceneByName("Temple_Final_Room");
            SceneManager.SetActiveScene(arena);
            var audio = GameObject.Find("Game audio");
            var type = Type.GetType("GameAudio, Assembly-CSharp", true);
            var cue = Type.GetType("SoundCue, Assembly-CSharp", true);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            AudioSource StartEffect(string name)
            {
                type.GetMethod("Play", flags).Invoke(null,
                    new object[] { Enum.Parse(cue, name), Vector3.zero, 0.5f, 3f, 1f });
                return audio.GetComponentsInChildren<AudioSource>().Single(source =>
                    source.clip?.name == name && source.isPlaying);
            }

            try
            {
                var lastAttack = StartEffect("AngelStrike");
                var dispel = StartEffect("AngelDispelled");
                type.GetMethod("HoldScore", flags).Invoke(null, new object[] { "sounds/comics/comics34-38_good" });
                Assert.That(dispel.isPlaying, Is.True, "The final boss cue must be audible over page 34.");
                Assert.That(lastAttack.isPlaying, Is.False, "Attacks from the arena stop when the comic opens.");
                Assert.That(audio.GetComponentsInChildren<AudioSource>().Any(source =>
                    source.clip?.name == "comics34-38_good" && source.isPlaying), Is.True);
            }
            finally
            {
                type.GetMethod("ReleaseScore", flags).Invoke(null, null);
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
