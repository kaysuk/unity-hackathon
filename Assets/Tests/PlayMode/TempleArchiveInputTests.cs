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
    public sealed class TempleArchiveInputTests
    {
        private Scene scene;
        private float originalTimeScale;

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = originalTimeScale;
        }

        [UnitySetUp]
        public IEnumerator LoadTemple()
        {
            originalTimeScale = Time.timeScale;
            yield return SceneManager.LoadSceneAsync("temple_v2", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("temple_v2");
            yield return null;
        }

        private MonoBehaviour[] SceneComponents() => scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>()).ToArray();

        [UnityTest]
        public IEnumerator ArchiveLeverShowsItsBindingAndTogglesAdjacentSealsOnPress()
        {
            var objects = SceneComponents();
            var player = objects.Single(c => c.GetType().Name == "PlayerInteractor");
            var levers = objects.Where(c => c.GetType().Name == "TempleArchiveLever").OrderBy(c => c.name).ToArray();
            var lever = levers[0];
            var movement = (Behaviour)player.GetComponent("IvanLocomotion");
            var camera = (Behaviour)objects.Single(c => c.GetType().Name == "IvanOrbitCamera");
            var body = player.GetComponent<CharacterController>();
            bool movementEnabled = movement.enabled;
            bool cameraEnabled = camera.enabled;
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior;
            var editorInput = settings.editorInputBehaviorInPlayMode;
            var action = InputSystem.actions.FindAction("Player/Interact", true);
            bool actionEnabled = action.enabled;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                movement.enabled = false;
                camera.enabled = false;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                action.Enable();
                InputSystem.EnableDevice(keyboard);

                var handle = lever.GetComponentInChildren<BoxCollider>();
                void AimFrom(float z)
                {
                    var feet = new Vector3(handle.bounds.center.x, .02f, z);
                    body.enabled = false;
                    player.transform.position = feet;
                    body.enabled = true;
                    camera.transform.position = feet + new Vector3(.16f, 1.5f, -.65f);
                    camera.transform.LookAt(handle.bounds.center);
                    Physics.SyncTransforms();
                }

                // Batch mode has no captured mouse; poll the normal gameplay action and raycasts.
                var update = player.GetType().GetMethod("UpdateTargetInReach", BindingFlags.Instance | BindingFlags.NonPublic);
                void Poll(KeyboardState state)
                {
                    InputSystem.QueueStateEvent(keyboard, state);
                    InputSystem.Update();
                    update.Invoke(player, null);
                }

                AimFrom(7.25f);
                Poll(new KeyboardState());
                var label = SceneComponents().SingleOrDefault(c => c.name == "Binding Label" && c.GetType().Name == "Text");
                Assert.That(label, Is.Not.Null, "Aiming at the lever must show its prompt");
                Assert.That(label.isActiveAndEnabled, Is.True);
                string text = Read<string>(label, "text");
                int binding = action.GetBindingIndexForControl(keyboard.eKey);
                string keyName = action.GetBindingDisplayString(binding, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                Assert.That(text, Does.Contain(keyName));
                Assert.That(text, Does.Contain("Рычаг 1"));

                Assert.IsTrue(Read<bool>(lever, "IsDown"));
                Assert.IsFalse(Read<bool>(levers[1], "IsDown"));
                Poll(new KeyboardState(Key.E));
                Assert.That(action.WasPressedThisDynamicUpdate(), Is.True);
                Assert.IsFalse(Read<bool>(lever, "IsDown"));
                Assert.IsTrue(Read<bool>(levers[1], "IsDown"));
                yield return null;

                AimFrom(3f);
                Poll(new KeyboardState());
                Assert.That(label.isActiveAndEnabled, Is.False, "The prompt disappears beyond the player's reach");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (!actionEnabled) action.Disable();
                settings.backgroundBehavior = background;
                settings.editorInputBehaviorInPlayMode = editorInput;
                camera.enabled = cameraEnabled;
                movement.enabled = movementEnabled;
            }
        }

        [UnityTest]
        public IEnumerator ReboundPageKeyTurnsThePausedJournalAndAppearsInItsHint()
        {
            var actor = SceneComponents().Single(c => c.GetType().Name == "PlayerInteractor");
            var journalType = System.Type.GetType("TempleArchiveJournal, Assembly-CSharp", true);
            var journal = (Component)journalType.GetMethod("GetOrCreate").Invoke(null, new object[] { actor.gameObject });
            var right = InputSystem.actions.FindAction("Overlay/Right", true);
            int index = right.bindings.IndexOf(b => b.path == "<Keyboard>/rightArrow");
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            var oldBinding = right.bindings[index];
            using var keyboard = new BootstrapSceneTests.MovementKeyboard();
            var read = journalType.GetMethod("ReadDocument");
            var updatePage = journalType.GetMethod("UpdateOpenPage", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                right.ApplyBindingOverride(index, "<Keyboard>/p");
                read.Invoke(journal, new object[] { 1 });
                string firstPage = Read<string>(journal, "VisibleDocumentText");
                read.Invoke(journal, new object[] { 2 });
                string secondPage = Read<string>(journal, "VisibleDocumentText");
                Assert.That(firstPage, Is.Not.EqualTo(secondPage));
                var hint = SceneComponents().Single(item => item.name == "Page controls" && item.GetType().Name == "Text");
                Assert.That(Read<string>(hint, "text").ToUpperInvariant(), Does.Contain("P "));
                Assert.That(Time.timeScale, Is.Zero);

                // Batchmode may lack window focus. Exercise the same page polling as the open overlay.
                keyboard.Press(Key.RightArrow);
                InputSystem.Update();
                updatePage.Invoke(journal, new object[] { false });
                Assert.That(Read<string>(journal, "VisibleDocumentText"), Is.EqualTo(secondPage));
                keyboard.Press(Key.P);
                InputSystem.Update();
                updatePage.Invoke(journal, new object[] { false });
                Assert.That(Read<string>(journal, "VisibleDocumentText"), Is.EqualTo(firstPage));
            }
            finally
            {
                journalType.GetMethod("Close").Invoke(journal, null);
                right.RemoveBindingOverride(index);
                if (oldBinding.hasOverrides)
                    right.ApplyBindingOverride(index, oldBinding);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorkshopWheelsKeepTheSameSixRunesThroughAFullTurn()
        {
            var objects = SceneComponents();
            var stations = objects.Where(c => c.GetType().Name == "TempleRuneStation").ToArray();
            Assert.AreEqual(6, stations.Length);
            var player = objects.Single(c => c.GetType().Name == "PlayerInteractor");
            Time.timeScale = 3f;
            for (int step = 0; step <= 6; step++)
            {
                foreach (var station in stations)
                {
                    var dial = station.transform.Find("Rotating stone slab");
                    var marks = dial.Cast<Transform>().Where(t => t.name == "Rune cut into stone").ToArray();
                    Assert.AreEqual(6, marks.Length);
                    var ordered = marks.OrderBy(t => station.transform.InverseTransformPoint(t.position).z).ToArray();
                    int index = Read<int>(station, "CurrentIndex");
                    string Glyph(int n) => $"R72 glyph {n + 1:00}";
                    Assert.AreEqual(Glyph(index), ordered[0].GetComponent<Renderer>().sharedMaterial.name);
                    Assert.AreEqual(Glyph((index + 3) % 6), ordered[5].GetComponent<Renderer>().sharedMaterial.name);
                    Assert.AreEqual((Read<int>(station, "TargetIndex") + 3) % 6, Read<int>(station, "StoneIndex"));
                }
                if (step < 6)
                {
                    foreach (var station in stations)
                        station.GetType().GetMethod("Interact").Invoke(station, new object[] { player });
                    yield return new WaitForSeconds(.85f);
                }
            }

            foreach (var station in stations)
            {
                for (int turn = 0; turn < 6 && Read<int>(station, "CurrentIndex") != Read<int>(station, "TargetIndex"); turn++)
                {
                    station.GetType().GetMethod("Interact").Invoke(station, new object[] { player });
                    yield return new WaitForSeconds(.85f);
                }
            }
            Assert.IsTrue(Read<bool>(objects.Single(c => c.GetType().Name == "TempleRunePuzzle"), "AreDialsAligned"));
        }

        private static T Read<T>(Component target, string property) =>
            (T)target.GetType().GetProperty(property).GetValue(target);
    }
}
