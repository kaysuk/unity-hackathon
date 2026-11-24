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
            if (page == 33)
                Assert.That(GameObject.Find("Озвучка").GetComponent<AudioSource>().isPlaying, Is.False,
                    "The altar instruction belongs to the transformation, before the fight.");
            else if (page >= 7)
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

        [Serializable]
        private sealed class Entry
        {
            public string scene;
            public int comicPage;
            public Vector3 position;
            public int magazine, reserve;
            public float hp;
            public bool dmitrySpared;
            public bool returnedToChurch;
        }

        private static Entry SavedEntry(string scene, int page = 0)
        {
            var entry = JsonUtility.FromJson<Entry>(PlayerPrefs.GetString(Key));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.scene, Is.EqualTo(scene));
            Assert.That(entry.comicPage, Is.EqualTo(page));
            return entry;
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
            yield return WaitFor(() => Page == 5);
            SavedEntry("Church_Interior", 5);
            for (int n = 5; n <= 14; n++)
                yield return ReadPage(n, n.ToString("00"), n <= 6 ? "comics5-6" : "comics_7-14");
            yield return At("Church_Interior", "church_interior");
            Assert.That(SavedEntry("Church_Interior").returnedToChurch, Is.False);
            var health = Actor.GetComponent("CombatHealth");
            health.GetType().GetMethod("TakeDamage").Invoke(health, new object[]{75f, Actor.transform.position, Vector3.zero, null});
            yield return null;
            yield return WaitFor(() => Score("church_interior").transform.parent.GetComponentsInChildren<AudioSource>()
                .Any(source => source.clip?.name == "player_heartb" && source.isPlaying && source.loop));
            health.GetType().GetMethod("RestoreHP", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(health, new object[]{68f});
            var inventory = Actor.GetComponent("PlayerInventory");
            inventory.GetType().GetMethod("TryCollect").Invoke(inventory, new object[] { "temple_m3_key" });
            yield return null;
            var shelf = GameObject.Find("Bookshelf Blockout - Parked").GetComponent("ChurchBookshelf");
            Assert.That((bool)shelf.GetType().GetProperty("Open").GetValue(shelf), Is.False);
            var interactor = Actor.GetComponent("PlayerInteractor");
            shelf.GetType().GetMethod("Interact").Invoke(shelf, new object[] { interactor });
            yield return WaitFor(() => (bool)shelf.GetType().GetProperty("Open").GetValue(shelf));
            Place(GameObject.Find("Temple Transition Landing").transform.position + Vector3.up * 0.3f);
            float officeHealth = 0;
            for (int n = 15; n <= 29; n++)
            {
                if (n is 15 or 20 or 28 or 29)
                {
                    yield return WaitFor(() => Page == n);
                    var checkpoint = SavedEntry("Church_Exterior", n);
                    if (n == 15) officeHealth = checkpoint.hp;
                    if (n == 29) Assert.That(checkpoint.dmitrySpared, Is.EqualTo(spare));
                }
                yield return ReadPage(n, n == 29 ? (spare ? "29_alive" : "29_dead") : n.ToString(), n <= 19 ? "comics15-19" : "comics20-29", spare);
            }
            yield return At("Church_Exterior", "church_exterior");
            var streetEntry = SavedEntry("Church_Exterior");
            Assert.That(streetEntry.dmitrySpared, Is.EqualTo(spare));
            Assert.That(streetEntry.magazine, Is.EqualTo(12));
            Assert.That(streetEntry.reserve, Is.EqualTo(24));
            Assert.That(officeHealth, Is.GreaterThanOrEqualTo(68f));
            Assert.That(streetEntry.hp, Is.EqualTo(officeHealth));
            var pistol = Actor.GetComponent("PistolCombat");
            pistol.GetType().GetMethod("RestoreAmmo", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(pistol, new object[] { 3, 7, 12 });
            Place(GameObject.Find("Exit - Rear Church Door").transform.position + Vector3.up * 0.1f);
            yield return At("Church_Interior", "church_interior");
            Assert.That(SavedEntry("Church_Interior").returnedToChurch, Is.True);
            Place(GameObject.Find("Temple Transition Landing").transform.position + Vector3.up * 0.35f);
            yield return At("temple_v2", "temple");
            var archiveEntry = SavedEntry("temple_v2");
            Assert.That(archiveEntry.magazine, Is.EqualTo(3));
            Assert.That(archiveEntry.reserve, Is.EqualTo(7));
            inventory = Actor.GetComponent("PlayerInventory");
            Assert.That(inventory.GetType().GetMethod("HasItem").Invoke(inventory, new object[] { "temple_m3_key" }), Is.True);
        }

        private static readonly string[] BorrowedPlayerLevels = { "Church_Interior", "temple_firts_v2" };

        [UnityTest]
        public IEnumerator DirectInteriorEntrySavesItsPlayerInTheDestination([ValueSource(nameof(BorrowedPlayerLevels))] string level)
        {
            yield return SceneManager.LoadSceneAsync(level);
            yield return At(level, level == "Church_Interior" ? "church_interior" : "temple");
            Vector3 entry = SavedEntry(level).position;
            Assert.That(Vector3.Distance(Actor.transform.position, entry), Is.LessThan(.4f));
            Assert.That(Physics.Raycast(entry + Vector3.up * .25f, Vector3.down, 1f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True, "Spawn must have a supporting floor.");
            yield return SceneManager.LoadSceneAsync("MainMenu");
        }

        [UnityTest]
        public IEnumerator DirectLevelEntryDeathAndContinueUseTheSameCheckpoint()
        {
            PlayerPrefs.DeleteKey(Key);
            yield return SceneManager.LoadSceneAsync("temple_v2", LoadSceneMode.Single);
            yield return At("temple_v2", "temple");
            Vector3 entry = SavedEntry("temple_v2").position;
            string checkpoint = PlayerPrefs.GetString(Key);
            var original = Actor;
            var health = original.GetComponent("CombatHealth");
            Place(entry + Vector3.right * 2f);
            health.GetType().GetMethod("TakeDamage").Invoke(health,
                new object[] { 200f, original.transform.position, Vector3.zero, null });
            yield return WaitFor(() => original == null);
            yield return At("temple_v2", "temple");
            Assert.That(Vector3.Distance(Actor.transform.position, entry), Is.LessThan(.4f));
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(checkpoint));

            yield return SceneManager.LoadSceneAsync("MainMenu");
            var menu = GameObject.Find("Main Menu").transform;
            yield return WaitFor(() => EventSystem.current?.currentSelectedGameObject?.name == "Continue");
            var button = menu.Find("Home/Continue").GetComponent<Button>();
            Assert.That(button.isActiveAndEnabled && button.interactable, Is.True);
            button.onClick.Invoke();
            yield return At("temple_v2", "temple");
            Assert.That(Vector3.Distance(Actor.transform.position, entry), Is.LessThan(.4f));
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(checkpoint));
        }

        [UnityTest]
        public IEnumerator ContinueAtDmitrysChoicePreservesTheChosenBranchOnStreetEntry()
        {
            PlayerPrefs.SetString(Key, "{\"version\":3,\"scene\":\"Church_Exterior\",\"comicPage\":28,\"returnedToChurch\":true,"
                + "\"hasHealth\":true,\"hp\":37,\"inventory\":[{\"id\":\"temple_m3_key\",\"held\":true}]}");
            Assert.That(Checkpoint.GetMethod("ContinueGame").Invoke(null, null), Is.EqualTo(true));
            yield return ReadPage(28, "28", "comics20-29", true);
            yield return ReadPage(29, "29_alive", "comics20-29");
            yield return At("Church_Exterior", "church_exterior");
            var street = SavedEntry("Church_Exterior");
            Assert.That(street.dmitrySpared, Is.True);
            Assert.That(street.hp, Is.EqualTo(37f));
            Assert.That(street.magazine, Is.EqualTo(12));
            Assert.That(street.reserve, Is.EqualTo(24));
            var inventory = Actor.GetComponent("PlayerInventory");
            Assert.That(inventory.GetType().GetMethod("HasItem").Invoke(inventory, new object[] { "temple_m3_key" }), Is.True);
            Assert.That(Vector3.Distance(Actor.transform.position, street.position), Is.LessThan(.4f));
            yield return SceneManager.LoadSceneAsync("MainMenu");
        }

        [UnityTest]
        public IEnumerator LeavingArenaDuringFinalDialogueCancelsThePendingEnding()
        {
            PlayerPrefs.SetString(Key, "{\"version\":2,\"scene\":\"Temple_Final_Room\",\"position\":{\"z\":-12},\"rotation\":{\"w\":1}}");
            Assert.That(Checkpoint.GetMethod("ContinueGame").Invoke(null, null), Is.EqualTo(true));
            yield return At("Temple_Final_Room", "boss_fight");

            var speech = Type.GetType("StorySpeech, Assembly-CSharp", true);
            speech.GetMethod("Say").Invoke(null, new object[] { "boss.last_altar", false });
            Assert.That(speech.GetProperty("Busy").GetValue(null), Is.EqualTo(true));
            var arena = SceneManager.GetActiveScene();
            var finish = Checkpoint.GetMethod("TryFinishRitual", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(finish.Invoke(null, new object[] { arena }), Is.EqualTo(true));
            Assert.That(Page, Is.Zero, "Ivan should finish speaking before the first ending page.");

            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            Assert.That(Page, Is.Zero);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(Checkpoint.GetField("loading", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(GameObject.Find("Game UI").GetComponent("GameCheckpoint")), Is.EqualTo(false));
        }

        private static readonly string[] Endings = { "bad", "good", "best" };

        [UnityTest]
        public IEnumerator RitualCheckpointPlaysItsEnding([ValueSource(nameof(Endings))] string ending)
        {
            bool spared = ending == "bad";
            bool truth = ending != "good";
            const int roomDocumentMask = (1 << 6) - 1;
            const int allDocumentMask = (1 << 7) - 1;
            bool atRitualEntrance = ending == "best";
            PlayerPrefs.SetString(Key, "{\"version\":" + (atRitualEntrance ? 3 : 2) + ",\"scene\":\"Temple_Final_Room\",\"comicPage\":" +
                (atRitualEntrance ? 30 : 0) + ",\"position\":{\"z\":-12}," +
                "\"rotation\":{\"w\":1},\"archiveDocuments\":" + (truth ? allDocumentMask : roomDocumentMask) +
                ",\"fullFamilyTruth\":" + (truth ? "true" : "false") + ",\"dmitrySpared\":" + (spared ? "true" : "false") + "}");
            Assert.That(Checkpoint.GetMethod("ContinueGame").Invoke(null, null), Is.EqualTo(true));
            if (atRitualEntrance)
                for (int page = 30; page <= 32; page++)
                    yield return ReadPage(page, page.ToString(), "boss_fight");
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
            var room = SceneManager.GetActiveScene().GetRootGameObjects();
            BootstrapSceneTests.AssertSpawnAtArenaEntrance(room, Actor);
            var encounter = room.Select(go => go.GetComponent("NimbEncounter")).Single(c => c != null);
            var boss = room.Select(go => go.GetComponent("NimbVulnerability")).Single(c => c != null);
            var active = encounter.GetType().GetProperty("ActiveAltar");
            var confirmed = encounter.GetType().GetProperty("ConfirmedAltars");
            var runeChangesAt = encounter.GetType().GetField("runeChangesAt", BindingFlags.Instance | BindingFlags.NonPublic);
            var actor = Actor;
            Place(boss.transform.position + Vector3.forward * 1.8f);
            yield return null;
            Assert.That(Page, Is.Zero, "Approaching a living Nimb must not start the ending.");
            for (int n = 1; n <= 6; n++)
            {
                var altar = (Component)active.GetValue(encounter);
                Assert.That(altar, Is.Not.Null);
                Place(altar.transform.position);
                Assert.That(altar.GetType().GetMethod("TryPistolStrike").Invoke(altar, new object[] {actor}), Is.True);
                Assert.That(Page, Is.Zero, "The altar opens a vulnerability before the boss can be defeated.");
                Assert.That(boss.GetType().GetMethod("TryPistolStrike").Invoke(boss, new object[] {actor}), Is.True);
                Assert.That(confirmed.GetValue(encounter), Is.EqualTo(n));
                if (n < 6)
                {
                    runeChangesAt.SetValue(encounter, Time.time);
                    yield return WaitFor(() => active.GetValue(encounter) != null);
                }
            }
            yield return ReadPage(33, "33", "boss_fight");
            for (int n = 34; n <= 38; n++)
                yield return ReadPage(n, n + "_" + ending, "comics34-38_" + ending);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "MainMenu" && Page == 0);
        }
    }
}
