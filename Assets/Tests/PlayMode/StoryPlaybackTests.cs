using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hackathon.Tests
{
    public sealed class StoryPlaybackTests
    {
        private const string Key = "Karamazovy.StoryCheckpoint";
        private string saved;
        private bool hadSave;
        private static Type Checkpoint => Type.GetType("GameCheckpoint, Assembly-CSharp", true);
        internal static MonoBehaviour Comics => GameObject.Find("Game UI").GetComponent("StoryComics") as MonoBehaviour;
        internal static int Page => (int)Comics.GetType().GetProperty("Page").GetValue(Comics);
        internal static AudioSource Score(string clip) => GameObject.Find("Game audio").GetComponentsInChildren<AudioSource>()
            .Single(s => s.clip?.name == clip && s.loop && s.isPlaying);
        private static GameObject Actor => SceneManager.GetActiveScene().GetRootGameObjects().Single(go => go.CompareTag("Player") && go.activeSelf);

        public static IEnumerator WaitFor(Func<bool> predicate)
        {
            float limit = Time.realtimeSinceStartup + 45f;
            while (!predicate() && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(predicate(), Is.True, $"Timeout in {SceneManager.GetActiveScene().name}, comic {Page}");
        }

        internal static IEnumerator ReadPage(int page, string picture, string cue, bool spare = false)
        {
            yield return WaitFor(() => Page == page);
            var overlay = GameObject.Find("Комикс").GetComponent<Canvas>();
            Assert.That(overlay.isRootCanvas && overlay.isActiveAndEnabled, Is.True);
            Assert.That(overlay.GetComponentInChildren<RawImage>().texture.name, Is.EqualTo(picture));
            Assert.That(Score(cue).loop, Is.True);
            if (page >= 7)
            {
                var voice = GameObject.Find("Озвучка").GetComponent<AudioSource>();
                Assert.That(voice.isPlaying && !voice.loop, Is.True, $"Dialogue on page {page}");
                Assert.That(voice.outputAudioMixerGroup.name, Is.EqualTo("Dialogue"));
                if (page == 8)
                {
                    Assert.That(voice.clip.name, Does.StartWith("OI.01."));
                    Assert.That(voice.time, Is.InRange(9.5f, 11.5f), "Opening must continue at its next phrase.");
                }
            }
            if (page == 28) Comics.GetType().GetMethod("ChooseDmitry").Invoke(Comics, new object[] {spare});
            else Comics.GetType().GetMethod("NextPage").Invoke(Comics, null);
            yield return null;
        }

        [SetUp]
        public void SavePreferences()
        {
            hadSave = PlayerPrefs.HasKey(Key);
            saved = PlayerPrefs.GetString(Key);
        }

        [UnityTearDown]
        public IEnumerator LeaveStory()
        {
            if (hadSave) PlayerPrefs.SetString(Key, saved);
            else PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            var scene = SceneManager.GetActiveScene();
            if (scene.name == "MainMenu" || scene.name == "temple_v2")
            {
                var empty = SceneManager.CreateScene("Story playback test complete");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        private static IEnumerator At(string scene, string track)
        {
            yield return WaitFor(() => SceneManager.GetActiveScene().name == scene && Time.timeScale > 0f &&
                !(bool)Checkpoint.GetField("loading", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(GameObject.Find("Game UI").GetComponent("GameCheckpoint")));
            Assert.That(Score(track).isPlaying, Is.True);
            Assert.That(Actor.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(go =>
                go.GetComponentsInChildren<AudioListener>()).Count(l => l.isActiveAndEnabled), Is.EqualTo(1));
        }

        private static void Place(Vector3 position)
        {
            var body = Actor.GetComponent<CharacterController>();
            body.enabled = false;
            body.transform.position = position;
            body.enabled = true;
            Physics.SyncTransforms();
        }

        private static readonly bool[] OfficeChoices = { true, false };

        [UnityTest]
        public IEnumerator NewGamePlaysOpeningAndOfficeAndReachesArchive([ValueSource(nameof(OfficeChoices))] bool spare)
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            var menu = GameObject.Find("Main Menu").transform;
            var fade = menu.Find("Fade").GetComponent<CanvasGroup>();
            yield return WaitFor(() => EventSystem.current?.currentSelectedGameObject != null && !fade.blocksRaycasts);
            yield return null;
            menu.Find("Home/New Game").GetComponent<Button>().onClick.Invoke();
            for (int n = 5; n <= 14; n++)
                yield return ReadPage(n, n.ToString("00"), n <= 6 ? "comics5-6" : "comics_7-14");
            yield return At("Church_Interior", "church_interior");
            var health = Actor.GetComponent("CombatHealth");
            health.GetType().GetMethod("TakeDamage").Invoke(health, new object[]{75f, Actor.transform.position, Vector3.zero, null});
            yield return null;
            yield return WaitFor(() => Score("church_interior").transform.parent.GetComponentsInChildren<AudioSource>()
                .Any(source => source.clip?.name == "player_heartb" && source.isPlaying && source.loop));
            health.GetType().GetMethod("RestoreHP", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(health, new object[]{100f});
            yield return null;
            var shelf = GameObject.Find("Bookshelf Blockout - Parked").GetComponent("ChurchBookshelf");
            Assert.That((bool)shelf.GetType().GetProperty("Open").GetValue(shelf), Is.False);
            var interactor = Actor.GetComponent("PlayerInteractor");
            shelf.GetType().GetMethod("Interact").Invoke(shelf, new object[] { interactor });
            yield return WaitFor(() => (bool)shelf.GetType().GetProperty("Open").GetValue(shelf));
            Place(GameObject.Find("Temple Transition Landing").transform.position + Vector3.up * 0.3f);
            for (int n = 15; n <= 29; n++)
                yield return ReadPage(n, n == 29 ? (spare ? "29_alive" : "29_dead") : n.ToString(), n <= 19 ? "comics15-19" : "comics20-29", spare);
            yield return At("Church_Exterior", "church_exterior");
            StringAssert.Contains("\"dmitrySpared\":" + (spare ? "true" : "false"), PlayerPrefs.GetString(Key));
            Place(GameObject.Find("Exit - Rear Church Door").transform.position + Vector3.up * 0.1f);
            yield return At("Church_Interior", "church_interior");
            Place(GameObject.Find("Temple Transition Landing").transform.position + Vector3.up * 0.35f);
            yield return At("temple_v2", "temple");
        }

        private static readonly string[] Endings = { "bad", "good", "best" };

        [UnityTest]
        public IEnumerator RitualCheckpointPlaysItsEnding([ValueSource(nameof(Endings))] string ending)
        {
            bool spared = ending == "bad";
            bool truth = ending != "good";
            PlayerPrefs.SetString(Key, "{\"version\":1,\"scene\":\"Temple_Final_Room\",\"position\":{\"z\":-12}," +
                "\"rotation\":{\"w\":1},\"archiveDocuments\":" + (truth ? 63 : 0) +
                ",\"fullFamilyTruth\":" + (truth ? "true" : "false") + ",\"dmitrySpared\":" + (spared ? "true" : "false") + "}");
            Assert.That(Checkpoint.GetMethod("ContinueGame").Invoke(null, null), Is.EqualTo(true));
            yield return At("Temple_Final_Room", "boss_fight");
            if (spared)
            {
                var originalActor = Actor;
                var originalScore = Score("boss_fight");
                originalScore.time = 20f;
                var hp = Actor.GetComponent("CombatHealth");
                hp.GetType().GetMethod("TakeDamage").Invoke(hp, new object[]{200f, Actor.transform.position, Vector3.zero, null});
                yield return WaitFor(() => SceneManager.GetActiveScene().name == "Temple_Final_Room" &&
                    originalActor == null && GameObject.FindWithTag("Player") != null);
                yield return At("Temple_Final_Room", "boss_fight");
                Assert.That(Score("boss_fight"), Is.SameAs(originalScore));
                Assert.That(originalScore.time, Is.GreaterThan(18f), "A retry restarted the score.");
            }
            var room = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>()).ToArray();
            var altars = room.Where(m => m.GetType().Name == "RitualAltar").ToArray();
            Assert.That(altars.Length, Is.EqualTo(6));
            foreach (var altar in altars)
            {
                var hit = altar.GetType().GetMethod("TakeDamage");
                for (int i = 0; i < 2; i++) hit.Invoke(altar, new object[] {25f, altar.transform.position, Vector3.forward, Actor});
                Assert.That(altar.gameObject.activeSelf, Is.False);
            }
            Place(room.Single(m => m.GetType().Name == "AngelSealAttack").transform.position + Vector3.forward * 1.8f);
            for (int n = 34; n <= 38; n++)
                yield return ReadPage(n, n + "_" + ending, "comics34-38_" + ending);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "MainMenu" && Page == 0);
        }
    }
}
