using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    // Runtime types live in Assembly-CSharp; use the same reflection boundary as BootstrapSceneTests.
    public sealed class TempleNavigationTests
    {
        private Scene scene;
        private GameObject npc;
        private Component player;
        private Dictionary<string, Component> nodes;
        private Component graph;
        private Component mover;

        private static object Call(Component component, string method, params object[] args) =>
            component.GetType().GetMethod(method).Invoke(component, args);

        private static T Read<T>(Component c, string property) =>
            (T)c.GetType().GetProperty(property).GetValue(c);

        private IEnumerable<Component> Components(string type) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>())
            .Where(component => component.GetType().Name == type);

        private static void Place(GameObject actor, Vector3 feet)
        {
            var body = actor.GetComponent<CharacterController>();
            Vector3 offset = actor.transform.position - new Vector3(body.bounds.center.x, body.bounds.min.y, body.bounds.center.z);
            body.enabled = false;
            actor.transform.position = feet + offset;
            body.enabled = true;
            Physics.SyncTransforms();
        }

        private void Prepare(Scene loaded)
        {
            scene = loaded;
            graph = Components("GraphManager").Single();
            nodes = Components("Node").ToDictionary(node => node.name);
            player = Components("PlayerInteractor").Single();
            ((Behaviour)player.GetComponent("IvanLocomotion")).enabled = false;
            Place(player.gameObject, nodes["SouthJunction"].transform.position);
            var guards = Components("RifleNpc").ToArray();
            npc = guards.Single(g => g.name == "SWAT - Room 6").gameObject;
            foreach (var guard in guards)
                if (guard.gameObject != npc) guard.gameObject.SetActive(false);
            Place(npc, nodes["WestGate"].transform.position);
        }

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("temple_v2", LoadSceneMode.Additive);
            yield return null;
            Prepare(SceneManager.GetSceneByName("temple_v2"));
            mover = npc.GetComponent("NPCMover");
            Assert.That(mover, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        private string[] Route(string from, string to)
        {
            var route = (IEnumerable)Call(graph, "FindPath", nodes[from], nodes[to]);
            return route?.Cast<Component>().Select(n => n.name).ToArray();
        }

        [UnityTest]
        public IEnumerator ReachesUpperLandingByStairs()
        {
            var original = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            var camera = (Behaviour)Components("IvanOrbitCamera").Single();
            var originalPosition = camera.transform.position;
            camera.enabled = false;
            try
            {
                Place(npc, nodes["Entry"].transform.position);
                Place(player.gameObject, nodes["UpperFloor"].transform.position);
                Call(graph, "RaiseAlarm", null, nodes["UpperFloor"]);
                var audio = GameObject.Find("Game audio");
                bool heardSteps = false;
                camera.transform.position = npc.transform.position + Vector3.up * 1.6f;

                // Поворот с нижнего этажа на лестницу требует смены целевого узла.
                float deadline = Time.time + 23f;
                while (npc.transform.position.y < 4.0f && Time.time < deadline)
                {
                    camera.transform.position = npc.transform.position + Vector3.up * 1.6f;
                    heardSteps |= audio.GetComponentsInChildren<AudioSource>().Any(source =>
                        source.isPlaying && source.clip != null && source.spatialBlend == 1f && source.volume < .29f &&
                        (source.clip.name.StartsWith("StoneLeft") || source.clip.name.StartsWith("StoneRight") ||
                         source.clip.name.StartsWith("WoodStep") || source.clip.name.StartsWith("MetalStep")) &&
                        Vector3.ProjectOnPlane(npc.transform.position - source.transform.position, Vector3.up).sqrMagnitude < .36f);
                    yield return null;
                }
                Assert.That(npc.transform.position.y, Is.GreaterThan(4.0f),
                    $"NPC stopped at {npc.transform.position}; next={Read<Component>(mover, "Next")?.name}");
                Assert.That(heardSteps, Is.True, "The moving rifleman must emit a positional step from the stairs.");
            }
            finally
            {
                camera.transform.position = originalPosition;
                camera.enabled = true;
                SceneManager.SetActiveScene(original);
            }
        }

        [UnityTest]
        public IEnumerator KeyboardEOpensPhysicalDoorThenEnablesBothDirections()
        {
            var original = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            var door = Components("TempleShortDoor").Single(c => c.transform.parent.parent.name.StartsWith("Room 3 -"));
            var locked = Components("TempleShortDoor").Single(c => c.transform.parent.parent.name.StartsWith("Room 2 -"));
            Place(player.gameObject, nodes["Room_Door2"].transform.position);
            Call(locked, "Interact", player);
            Assert.That(Read<bool>(locked, "IsOpen"), Is.False);
            Place(player.gameObject, nodes["Hall_Door"].transform.position);
            Call(door, "Interact", player);
            Assert.That(Read<bool>(door, "IsOpen"), Is.False, "Shortcut only opens from inside");
            var pausedCamera = (Behaviour)Components("IvanOrbitCamera").Single();
            pausedCamera.enabled = false;
            var body = player.GetComponent<CharacterController>();
            void AimAt(Component leaf, string node)
            {
                Place(player.gameObject, nodes[node].transform.position);
                pausedCamera.transform.position = body.bounds.center;
                pausedCamera.transform.LookAt(leaf.GetComponentInChildren<Collider>().bounds.center);
                Physics.SyncTransforms();
            }
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior;
            var editorInput = settings.editorInputBehaviorInPlayMode;
            var action = InputSystem.actions.FindAction("Player/Interact", true);
            bool wasEnabled = action.enabled;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                action.Enable();
                InputSystem.EnableDevice(keyboard);
                var poll = player.GetType().GetMethod("UpdateTargetInReach", BindingFlags.Instance | BindingFlags.NonPublic);
                void PressE()
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    InputSystem.Update();
                    Assert.That(action.WasPressedThisDynamicUpdate(), Is.True);
                    poll.Invoke(player, null);
                }

                AimAt(locked, "Room_Door2");
                int Rattles() => GameObject.Find("Game audio").GetComponentsInChildren<AudioSource>()
                    .Count(source => source.clip?.name == "Locked" && source.isPlaying &&
                        source.spatialBlend == 1f && source.transform.position == locked.transform.position);
                PressE();
                Assert.That(Read<bool>(locked, "IsOpen"), Is.False);
                Assert.That(Rattles(), Is.EqualTo(1));
                PressE();
                Assert.That(Rattles(), Is.EqualTo(1), "Repeated presses must not layer latch rattles.");
                AimAt(door, "Room_Door");
                PressE();
                Assert.That(Read<bool>(door, "IsOpen"), Is.True);
                Assert.That(Read<bool>(door, "IsPassable"), Is.False);
                yield return new WaitForSeconds(0.7f);
                Assert.That(Route("Hall_Door", "Room_Door"), Is.EqualTo(new[]{"Hall_Door","Room_Door"}));
                Assert.That(Route("Room_Door", "Hall_Door"), Is.EqualTo(new[]{"Room_Door","Hall_Door"}));
            }
            finally
            {
                SceneManager.SetActiveScene(original);
                InputSystem.RemoveDevice(keyboard);
                if (!wasEnabled) action.Disable();
                settings.backgroundBehavior = background;
                settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }

        [UnityTest]
        public IEnumerator MewLeverAlternatesDoorsAndClosesBeforeOpening()
        {
            var lever = Components("TempleMewLever").Single();
            var room4 = Components("TemplePassage").Single(c => c.name == "Mew gate to Room 4");
            var room5 = Components("TemplePassage").Single(c => c.name == "Mew gate to Room 5");
            Assert.That(Read<bool>(room4, "IsOpen"), Is.False);
            Assert.That(Read<bool>(room5, "IsOpen"), Is.True);

            Component previousDoor = room5;
            foreach (var nextDoor in new[] { room4, room5, room4 })
            {
                Assert.That(Call(lever, "CanInteract", player), Is.EqualTo(true));
                Call(lever, "Interact", player);
                float deadline = Time.time + 4f;

                if (previousDoor != null)
                {
                    Assert.That(Read<bool>(previousDoor, "IsMoving"), Is.True);
                    Assert.That(Read<bool>(previousDoor, "IsPassable"), Is.False);
                    Assert.That(Call(lever, "CanInteract", player), Is.EqualTo(false));

                    while (Read<bool>(previousDoor, "IsMoving") && Time.time < deadline)
                    {
                        Assert.That(Read<bool>(nextDoor, "IsOpen"), Is.False);
                        yield return null;
                    }

                    Assert.That(Read<bool>(previousDoor, "IsMoving"), Is.False);
                    Assert.That(Read<bool>(previousDoor, "IsOpen"), Is.False);
                }

                while (!Read<bool>(nextDoor, "IsPassable") && Time.time < deadline)
                    yield return null;

                Assert.That(Read<bool>(nextDoor, "IsPassable"), Is.True);
                // The lever coroutine can observe the final door position on the next frame.
                yield return null;
                previousDoor = nextDoor;
            }
        }

        [UnityTest]
        public IEnumerator ChangingGatesUpdatesDetoursForAStationaryPlayer()
        {
            var west = Components("TemplePassage").Single(c => c.name == "Mew gate to Room 4");
            var middle = Components("TemplePassage").Single(c => c.name == "Mew gate to Room 5");
            Assert.That(Route("WestGate", "SouthJunction"), Is.Null);
            foreach (int room in new[] {4, 5})
            {
                var door = Components("TempleShortDoor").Single(c => c.transform.parent.parent.name.StartsWith($"Room {room} -"));
                Place(player.gameObject, nodes[$"Room_Door{room}"].transform.position);
                Assert.That(Call(door, "CanInteract", player), Is.EqualTo(true));
                Call(door, "Interact", player);  // Тот же обработчик, который вызывает PlayerInteractor при E.
                Assert.That(Read<bool>(door, "IsOpen"), Is.True);
                Assert.That(Read<bool>(door, "IsPassable"), Is.False);
            }
            Place(player.gameObject, nodes["SouthJunction"].transform.position);
            Vector3 targetPosition = player.transform.position;
            Call(graph, "RaiseAlarm", null, nodes["SouthJunction"]);
            yield return new WaitForSeconds(0.7f);
            Call(middle, "SetOpen", true, true);
            Assert.That(Route("WestGate", "SouthJunction"), Does.Contain("Room4"));
            Call(west, "SetOpen", true, false);
            Assert.That(Route("WestGate", "SouthJunction"), Does.Contain("Room4"), "Moving gate still blocks");
            float gateDeadline = Time.time + 4f;
            while (!Read<bool>(west, "IsPassable") && Time.time < gateDeadline) yield return null;
            Assert.That(Read<bool>(west, "IsPassable"), Is.True);
            Assert.That(Route("WestGate", "SouthJunction"), Is.EqualTo(new[]{"WestGate","WestJunction","SouthJunction"}));
            Call(west, "SetOpen", false, false);
            Assert.That(Route("WestGate", "SouthJunction"), Does.Contain("Room4"), "Closing blocks immediately");
            Call(middle, "SetOpen", false, true);
            Assert.That(Route("WestGate", "SouthJunction"), Is.Null);
            Call(west, "SetOpen", true, true);
            Assert.That(Route("WestGate", "SouthJunction"), Does.Contain("WestJunction"));
            Assert.That(player.transform.position, Is.EqualTo(targetPosition));
        }
    }
}
