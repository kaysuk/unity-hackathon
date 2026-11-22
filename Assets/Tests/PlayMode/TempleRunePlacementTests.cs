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
    public sealed class TempleRunePlacementTests
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
        public IEnumerator GrilleWaitsForTheTwoSwappedRunesToBeCorrected()
        {
            Time.timeScale = 3f;
            var objects = SceneComponents();
            var player = objects.Single(c => c.GetType().Name == "PlayerInteractor");
            var puzzle = objects.Single(c => c.GetType().Name == "TempleRunePuzzle");
            var grille = Field<Component>(puzzle, "passage");
            var stations = objects.Where(c => c.GetType().Name == "TempleRuneStation")
                .OrderBy(c => Read<int>(c, "TargetIndex")).ToArray();
            var sockets = stations.Select(s => s.GetComponentsInChildren<MonoBehaviour>()
                .Single(c => c.GetType().Name == "TempleRuneSocket")).ToArray();
            var pickups = objects.Where(c => c.GetType().Name == "ItemPickup")
                .Where(c => Field<string>(c, "itemId").StartsWith("rune_room_"))
                .OrderBy(c => Field<string>(c, "itemId")).ToArray();
            Assert.That(stations.Length, Is.EqualTo(6));
            Assert.That(pickups.Length, Is.EqualTo(6));
            foreach (var pickup in pickups)
                Call(pickup, "Interact", player);

            float deadline = Time.realtimeSinceStartup + 10f;
            while (sockets.Any(s => !(bool)Call(s, "CanInteract", player)))
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "The aligned dials must raise six usable seats");
                foreach (var station in stations)
                    if (Read<int>(station, "CurrentIndex") != Read<int>(station, "TargetIndex") && (bool)Call(station, "CanInteract", player))
                        Call(station, "Interact", player);
                yield return null;
            }

            var answer = stations.Select(s => Read<int>(s, "StoneIndex")).ToArray();
            for (int i = 0; i < stations.Length; i++)
            {
                int rune = i < 2 ? answer[1 - i] : answer[i];
                yield return Place(player, stations[i], sockets[i], pickups[rune], rune, grille);
            }
            Assert.That(Read<bool>(grille, "IsOpen"), Is.False, "Two swapped stones must keep the full set locked");

            var inventory = player.GetComponent("PlayerInventory");
            for (int i = 0; i < 2; i++)
            {
                Assert.That(Call(sockets[i], "CanInteract", player), Is.EqualTo(true), "Retrieval works with an empty inventory");
                Call(sockets[i], "Interact", player);
                Assert.That(Read<int>(stations[i], "OccupantIndex"), Is.EqualTo(-1));
                Assert.That(Call(inventory, "HasItem", Field<string>(pickups[answer[1 - i]], "itemId")), Is.EqualTo(true));
            }
            for (int i = 0; i < 2; i++)
                yield return Place(player, stations[i], sockets[i], pickups[answer[i]], answer[i], grille);
            Assert.That(Read<bool>(grille, "IsOpen"), Is.True);
        }

        [UnityTest]
        public IEnumerator RimShowsTheBoundKeyAndAcceptsOneTurnAtATime()
        {
            var objects = SceneComponents();
            var player = objects.Single(c => c.GetType().Name == "PlayerInteractor");
            var station = objects.Single(c => c.GetType().Name == "TempleRuneStation" && Read<int>(c, "TargetIndex") == 0);
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

                // Aim from the aisle at the outer bronze rim of the first plate.
                var rim = station.transform.TransformPoint(new Vector3(.342f, .115f, 0f));
                void AimFrom(float distance)
                {
                    var feet = rim + station.transform.right * distance;
                    feet.y = station.transform.position.y + .04f;
                    body.enabled = false;
                    player.transform.position = feet;
                    body.enabled = true;
                    camera.transform.position = feet + Vector3.up * 1.61f;
                    camera.transform.LookAt(rim);
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

                AimFrom(1.05f);
                Poll(new KeyboardState());
                var label = SceneComponents().SingleOrDefault(c => c.name == "Binding Label" && c.GetType().Name == "Text");
                Assert.That(label, Is.Not.Null, "The visible rim must be within the plate's interaction surface");
                Assert.That(label.isActiveAndEnabled, Is.True);
                string text = Read<string>(label, "text");
                int binding = action.GetBindingIndexForControl(keyboard.eKey);
                string keyName = action.GetBindingDisplayString(binding, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                Assert.That(text, Does.Contain(keyName));
                Assert.That(text, Does.Contain("Повернуть плиту"));

                int before = Read<int>(station, "CurrentIndex");
                Poll(new KeyboardState(Key.E));
                Assert.That(action.WasPressedThisDynamicUpdate(), Is.True);
                Assert.That(label.isActiveAndEnabled, Is.True, "The prompt must stay visible during the turn");
                Assert.That(Read<string>(label, "text"), Is.EqualTo(text));
                Poll(new KeyboardState());
                Poll(new KeyboardState(Key.E));
                Assert.That(Read<int>(station, "CurrentIndex"), Is.EqualTo((before + 1) % 6));
                yield return new WaitForSeconds(.8f);
                Assert.That(Call(station, "CanInteract", player), Is.EqualTo(true));

                AimFrom(4.2f);
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

        private static IEnumerator Place(Component player, Component station, Component socket, Component pickup, int rune, Component grille)
        {
            Call(socket, "Interact", player);
            var picker = player.GetComponent("TempleRunePicker");
            Assert.That(picker, Is.Not.Null);
            Assert.That(Read<bool>(picker, "IsOpen"), Is.True);
            for (int i = 0; i < Read<int>(picker, "ChoiceCount") && Read<int>(picker, "SelectedGlyph") != rune; i++)
                Call(picker, "MoveSelection", 1);
            Assert.That(Read<int>(picker, "SelectedGlyph"), Is.EqualTo(rune));
            Call(picker, "Confirm");
            Assert.That(Read<bool>(grille, "IsOpen"), Is.False, "Opening must wait for the last stone to finish seating");
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!Read<bool>(station, "HasStone") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Read<bool>(station, "HasStone"), Is.True);
            Assert.That(Read<int>(station, "OccupantIndex"), Is.EqualTo(rune));
            var stone = Field<GameObject>(station, "placedStone");
            Assert.That(stone.activeInHierarchy, Is.True);
            Assert.That(stone.GetComponent<MeshFilter>().sharedMesh, Is.EqualTo(pickup.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(stone.GetComponent<MeshRenderer>().sharedMaterials, Is.EqualTo(pickup.GetComponent<MeshRenderer>().sharedMaterials));
        }

        private static object Call(Component target, string method, params object[] args) =>
            target.GetType().GetMethod(method).Invoke(target, args);
        private static T Read<T>(Component target, string property) =>
            (T)target.GetType().GetProperty(property).GetValue(target);
        private static T Field<T>(Component target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
}
