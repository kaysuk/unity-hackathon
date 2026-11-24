using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class TransformationSpeechTests
    {
        private const string FirstLine = "OI.52.priests_fall_alyosha";
        private const string SecondLine = "OI.53.stay_away_from_the_child";
        private static readonly int[] ArchiveCounts = { 6, 7 };
        private Scene originalScene;
        private MonoBehaviour door;

        [UnityTest]
        public IEnumerator RitualDoorPlaysTheNameOnlyWithAllSevenDocuments(
            [ValueSource(nameof(ArchiveCounts))] int documentCount)
        {
            originalScene = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync("temple_v2", LoadSceneMode.Additive);
            var temple = SceneManager.GetSceneByName("temple_v2");
            SceneManager.SetActiveScene(temple);
            yield return null;

            var objects = temple.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>()).ToArray();
            var player = objects.Single(c => c.GetType().Name == "PlayerInteractor");
            var register = objects.Single(c => c.GetType().Name == "TempleArchiveDocument");
            var sheets = objects.Where(c => c.GetType().Name == "TempleSealDocument").OrderBy(c => c.name).ToArray();
            Assert.AreEqual(6, sheets.Length);
            door = objects.Single(c => c.GetType().Name == "SceneTransitionDoor");
            var barrier = (Component)door.GetType().GetField("entranceBarrier", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(door);
            Call(barrier, "SetOpen", true, true);

            // Read D07 first. Possessing the hidden register alone must not reveal the name.
            Call(register, "Interact", player);
            var journal = player.GetComponent("TempleArchiveJournal");
            Assert.That((bool)Call(journal, "HasDocument", 7), Is.True);
            Call(journal, "Close");
            foreach (var sheet in sheets.Take(documentCount - 1))
            {
                Call(sheet, "Interact", player);
                Call(journal, "Close");
            }
            Assert.That(journal.GetType().GetProperty("DocumentCount").GetValue(journal), Is.EqualTo(documentCount));

            Call(door, "Interact", player);
            Call(journal, "ConfirmEntrance");
            if (documentCount == 7)
                Call(journal, "ContinueEntrance");
            yield return StoryPlaybackTests.ReadPage(30, "30", "boss_fight");
            yield return StoryPlaybackTests.WaitFor(() => StoryPlaybackTests.Page == 31);

            var voice = StoryPlaybackTests.Comics.transform.Find("Озвучка").GetComponent<AudioSource>();
            Assert.That(voice.clip.name, Is.EqualTo(FirstLine));
            Assert.That(voice.isPlaying, Is.True);
            Assert.That(voice.outputAudioMixerGroup.name, Is.EqualTo("Dialogue"));
            var first = voice.clip;
            float lastPlayed = 0f;
            float deadline = Time.realtimeSinceStartup + first.length + 3f;
            while (voice.clip == first && Time.realtimeSinceStartup < deadline)
            {
                lastPlayed = Mathf.Max(lastPlayed, voice.time);
                yield return null;
            }
            Assert.That(StoryPlaybackTests.Page, Is.EqualTo(31));
            Assert.That(voice.clip?.name, Is.EqualTo(SecondLine));
            Assert.That(voice.isPlaying, Is.True);
            if (documentCount == 7)
                Assert.That(lastPlayed, Is.GreaterThan(7.65f), "The named take must pass the complete word.");
            else
                Assert.That(lastPlayed, Is.InRange(6.1f, 6.85f), "Keep the preceding sentence and stop before the name.");

            Call(StoryPlaybackTests.Comics, "NextPage");
            yield return StoryPlaybackTests.WaitFor(() => StoryPlaybackTests.Page == 32);
            Assert.That(voice.clip?.name, Is.EqualTo("OI.54.priest_turns_into_boss"));
            voice.timeSamples = voice.clip.samples - voice.clip.frequency / 10;
            yield return StoryPlaybackTests.WaitFor(() => voice.clip?.name == "OI.55.columns_hold_the_boss_take_1");
            yield return StoryPlaybackTests.ReadPage(32, "32", "boss_fight");
            yield return StoryPlaybackTests.WaitFor(() =>
            {
                Assert.That(StoryPlaybackTests.Page, Is.Not.EqualTo(33), "The finishing blow requires a victory in the arena.");
                return SceneManager.GetActiveScene().name == "Temple_Final_Room" && StoryPlaybackTests.Page == 0 && !temple.isLoaded;
            });
            Assert.That(voice.clip, Is.Null);
        }

        private static object Call(Component target, string method, params object[] args) =>
            target.GetType().GetMethod(method).Invoke(target, args);

        [UnityTearDown]
        public IEnumerator UnloadLevels()
        {
            if (door != null) door.StopAllCoroutines();
            var comics = StoryPlaybackTests.Comics;
            if (StoryPlaybackTests.Page != 0)
                comics.GetType().GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(comics, null);
            if (originalScene.IsValid() && originalScene.isLoaded)
                SceneManager.SetActiveScene(originalScene);
            foreach (string name in new[] { "Temple_Final_Room", "temple_v2" })
            {
                Scene level = SceneManager.GetSceneByName(name);
                if (level.IsValid() && level.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(level);
            }
        }
    }
}
