using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class TempleRiddleSpeechTests
    {
        private const string Riddle = "gladivir_riddle";
        private const string Reminder = "gladivir_riddle_remind";
        private const string Encouragement = "gladivir_ecourage";
        private Scene originalScene;
        private Scene temple;
        private float originalSpeed;
        private AudioSource voice;

        [UnityTearDown]
        public IEnumerator UnloadTemple()
        {
            if (originalScene.IsValid() && originalScene.isLoaded)
                SceneManager.SetActiveScene(originalScene);
            if (temple.IsValid() && temple.isLoaded)
                yield return SceneManager.UnloadSceneAsync(temple);
            Time.timeScale = originalSpeed;
        }

        [UnityTest]
        public IEnumerator RoomEntryAltarAndCompletedDialsPlayTheirOwnDialogue()
        {
            originalSpeed = Time.timeScale;
            originalScene = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync("temple_v2", LoadSceneMode.Additive);
            temple = SceneManager.GetSceneByName("temple_v2");
            Assert.That(SceneManager.SetActiveScene(temple), Is.True);
            yield return null;
            Time.timeScale = 3f;

            var components = temple.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>()).ToArray();
            MonoBehaviour Find(string type) => components.Single(c => c.GetType().Name == type);
            var player = Find("PlayerInteractor");
            var controller = player.GetComponent<CharacterController>();
            ((Behaviour)player.GetComponent("IvanLocomotion")).enabled = false;
            foreach (var guard in components.Where(c => c.GetType().Name == "RifleNpc"))
                guard.gameObject.SetActive(false);
            voice = StoryPlaybackTests.Comics.transform.Find("Озвучка").GetComponent<AudioSource>();

            var outside = components.Single(c => c.name == "HiddenEast" && c.GetType().Name == "Node").transform.position;
            var inside = components.Single(c => c.name == "HiddenWest" && c.GetType().Name == "Node").transform.position;
            yield return Place(controller, outside);
            Assert.That(voice.clip?.name, Is.Not.EqualTo(Riddle));
            yield return Place(controller, inside);
            ExpectPlaying(Riddle);
            yield return FinishOnce(Riddle);
            yield return Place(controller, outside);
            yield return Place(controller, inside);
            Assert.That(voice.clip, Is.Null, "Returning from the archive must not replay the introduction.");

            var altar = Find("TempleRiddleReminder");
            var altarFace = altar.GetComponent<Collider>().bounds.center;
            yield return Place(controller, new Vector3(altarFace.x, inside.y, altarFace.z + 1.2f));
            var camera = Find("IvanOrbitCamera");
            camera.enabled = false;
            camera.transform.position = player.transform.position + new Vector3(.15f, 1.45f, .45f);
            camera.transform.LookAt(altarFace);
            Physics.SyncTransforms();
            var aimed = player.GetType().GetMethod("FindTargetInReach", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(aimed.Invoke(player, null), Is.SameAs(altar), "The altar must be targetable through the player interaction ray.");
            Assert.That((string)Call(altar, "GetPrompt", player, "E"), Does.Contain("E"));
            PressAltarTwice(player);
            yield return FinishOnce(Reminder);

            var puzzle = Find("TempleRunePuzzle");
            var stations = components.Where(c => c.GetType().Name == "TempleRuneStation").ToArray();
            Assert.That(stations.Length, Is.EqualTo(6));
            var last = stations.First(s => Read<int>(s, "CurrentIndex") != Read<int>(s, "TargetIndex"));
            // Keep one dial a single turn away, so the reminder can be playing at the stage transition.
            for (int turn = 0; turn < stations.Length; turn++)
            {
                foreach (var station in stations)
                {
                    int target = Read<int>(station, "TargetIndex");
                    int stop = station == last ? (target + 5) % 6 : target;
                    if (Read<int>(station, "CurrentIndex") != stop)
                        Call(station, "Interact", player);
                }
                yield return new WaitForSeconds(.85f);
                Assert.That(Read<bool>(puzzle, "AreDialsAligned"), Is.False);
                Assert.That(voice.clip?.name, Is.Not.EqualTo(Encouragement));
            }
            yield return FinishDialogue(Encouragement);
            Call(altar, "Interact", player);
            ExpectPlaying(Reminder);
            var sound = GameObject.Find("Game audio");
            AudioSource[] PlayingEffect(string name) => sound.GetComponentsInChildren<AudioSource>()
                .Where(source => source.clip?.name == name && source.isPlaying &&
                    source.outputAudioMixerGroup?.name == "Effects").ToArray();
            var dial = (Transform)last.GetType().GetField("dial",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(last);
            int DialVoices() => PlayingEffect("RuneDial").Count(source =>
                source.spatialBlend == 1f && source.transform.position == dial.position);
            int previous = DialVoices();
            Call(last, "Interact", player);
            Assert.That(DialVoices(), Is.EqualTo(previous + 1));
            Call(last, "Interact", player);
            Assert.That(DialVoices(), Is.EqualTo(previous + 1), "One physical turn has one recording.");
            yield return new WaitForSeconds(.85f);
            Assert.That(Read<bool>(puzzle, "AreDialsAligned"), Is.True);
            Assert.That(Read<bool>(puzzle, "IsSolved"), Is.False);
            ExpectPlaying(Encouragement);
            var lifts = PlayingEffect("StoneLift");
            Assert.That(lifts.Select(source => source.transform.position).Distinct().Count(), Is.EqualTo(6));
            Assert.That(lifts.All(source => source.spatialBlend == 1f && !source.loop), Is.True);
            yield return FinishOnce(Encouragement);

            var inventory = player.GetComponent("PlayerInventory");
            foreach (var station in stations)
            {
                int glyph = Read<int>(station, "StoneIndex");
                Assert.That(Call(inventory, "TryCollect", $"rune_room_{glyph + 1:00}"), Is.EqualTo(true));
                var socket = station.GetComponentsInChildren<MonoBehaviour>()
                    .Single(c => c.GetType().Name == "TempleRuneSocket");
                float deadline = Time.realtimeSinceStartup + 3f;
                while (!(bool)Call(socket, "CanInteract", player) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(Call(socket, "CanInteract", player), Is.EqualTo(true));
                Call(socket, "Interact", player);
                var picker = player.GetComponent("TempleRunePicker");
                Assert.That(Read<int>(picker, "SelectedGlyph"), Is.EqualTo(glyph));
                Call(picker, "Confirm");
                Assert.That(PlayingEffect("MenuClick").Any(source => source.spatialBlend == 0f), Is.True);
            }
            yield return new WaitForSeconds(.55f);
            Assert.That(PlayingEffect("Stone").Any(source => source.spatialBlend == 1f), Is.True);
            Assert.That(Read<bool>(puzzle, "IsSolved"), Is.True);
            ExpectPlaying("OI.150.rune_puzzle_solved");
            yield return FinishDialogue(Encouragement);
            Assert.That(Call(altar, "CanInteract", player), Is.EqualTo(false));
        }

        private void PressAltarTwice(Component player)
        {
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior;
            var routing = settings.editorInputBehaviorInPlayMode;
            var interact = InputSystem.actions.FindAction("Player/Interact", true);
            bool wasEnabled = interact.enabled;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                interact.Enable();
                InputSystem.EnableDevice(keyboard);
                var poll = player.GetType().GetMethod("UpdateTargetInReach", BindingFlags.Instance | BindingFlags.NonPublic);
                void PressE()
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    InputSystem.Update();
                    Assert.That(interact.WasPressedThisDynamicUpdate(), Is.True);
                    poll.Invoke(player, null);
                }

                PressE();
                ExpectPlaying(Reminder);
                // Seek well away from zero to distinguish continuation from an immediate restart.
                int checkpoint = voice.clip.samples / 3;
                voice.timeSamples = checkpoint;
                PressE();
                ExpectPlaying(Reminder);
                Assert.That(voice.timeSamples, Is.GreaterThan(checkpoint / 2),
                    "A second press while the reminder is speaking must preserve its playback position.");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (!wasEnabled) interact.Disable();
                settings.editorInputBehaviorInPlayMode = routing;
                settings.backgroundBehavior = background;
            }
        }

        private static IEnumerator Place(CharacterController controller, Vector3 feet)
        {
            controller.enabled = false;
            controller.transform.position = feet + Vector3.up * .02f;
            controller.enabled = true;
            Physics.SyncTransforms();
            controller.Move(Vector3.down * .025f);
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        private void ExpectPlaying(string clip)
        {
            Assert.That(voice.clip?.name, Is.EqualTo(clip));
            Assert.That(voice.isPlaying, Is.True);
            Assert.That(voice.loop, Is.False);
            Assert.That(voice.outputAudioMixerGroup?.name, Is.EqualTo("Dialogue"));
        }

        private IEnumerator FinishOnce(string clip)
        {
            voice.Stop();
            yield return null;
            yield return FinishDialogue(clip);
        }

        private IEnumerator FinishDialogue(string forbidden = null)
        {
            // End each current line on the real source. StorySpeech.Update advances its queue.
            for (int i = 0; i < 12 && voice.clip != null; i++)
            {
                Assert.That(voice.clip.name, Is.Not.EqualTo(forbidden));
                voice.Stop();
                yield return null;
            }
            Assert.That(voice.clip, Is.Null);
        }

        private static object Call(Component component, string method, params object[] args) =>
            component.GetType().GetMethod(method).Invoke(component, args);

        private static T Read<T>(Component component, string property) =>
            (T)component.GetType().GetProperty(property).GetValue(component);
    }
}
