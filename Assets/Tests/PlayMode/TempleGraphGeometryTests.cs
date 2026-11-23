using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class TempleGraphGeometryTests
    {
        [UnityTest]
        public IEnumerator ReinforcementChecksClosedPassageThenReturns()
        {
            yield return SceneManager.LoadSceneAsync("temple_v2", LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByName("temple_v2");
            float scale = Time.timeScale;
            try
            {
                yield return null;
                Time.timeScale = 3f;
                var scripts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>()).ToArray();
                var graph = scripts.Single(s => s.GetType().Name == "GraphManager");
                var source = scripts.Single(s => s.GetType().Name == "Node" && s.name == "Room7");
                var stop = scripts.Single(s => s.GetType().Name == "Node" && s.name == "EastHall");
                var receiver = scripts.Single(s => s.GetType().Name == "NPCChaser" && s.name == "SWAT - Room 8");
                var caller = scripts.Single(s => s.GetType().Name == "NPCChaser" && s.name == "SWAT - Room 6");
                Vector3 start = receiver.transform.position;
                graph.GetType().GetMethod("RaiseAlarm").Invoke(graph, new object[] { caller, source });
                float deadline = Time.time + 28f;
                bool checkedDoor = false, returned = false;
                Quaternion arrivalFacing = Quaternion.identity;
                bool lookedAround = false;
                while (Time.time < deadline)
                {
                    string state = receiver.GetType().GetProperty("CurrentState").GetValue(receiver).ToString();
                    if (state == "InvestigateAlarm" && Vector3.Distance(receiver.transform.position, stop.transform.position) < 0.45f)
                    {
                        if (!checkedDoor) arrivalFacing = receiver.transform.rotation;
                        checkedDoor = true;
                        lookedAround |= Quaternion.Angle(arrivalFacing, receiver.transform.rotation) > 60f;
                    }
                    if (state == "Patrol" && Vector3.Distance(start, receiver.transform.position) < 0.5f)
                    {
                        returned = true;
                        break;
                    }
                    yield return null;
                }
                Assert.IsTrue(checkedDoor, "Guard did not approach the closed door");
                Assert.IsTrue(lookedAround, "Guard did not scan at the obstruction");
                Assert.IsTrue(returned, "Guard did not resume the route from its post");
            }
            finally
            {
                Time.timeScale = scale;
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator SightingAlertsSquadAndSearchesTheLastSeenPosition()
        {
            yield return SceneManager.LoadSceneAsync("temple_v2", LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByName("temple_v2");
            float scale = Time.timeScale;
            try
            {
                yield return null;
                Time.timeScale = 3f;
                var scripts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>()).ToArray();
                var graph = scripts.Single(s => s.GetType().Name == "GraphManager");
                var nodes = scripts.Where(s => s.GetType().Name == "Node").ToDictionary(n => n.name);
                float level(string from, string to) => (float)graph.GetType().GetMethod("FootstepLevel")
                    .Invoke(graph, new object[] { nodes[from], nodes[to] });
                Assert.AreEqual(1f, level("Hall1", "M0_West"));
                Assert.AreEqual(0.5f, level("Room6", "EastHall"));
                Assert.AreEqual(0.2f, level("Room1", "Hall1"));
                var player = scripts.Single(s => s.GetType().Name == "PlayerInteractor");
                var door = scripts.Single(s => s.GetType().Name == "TempleShortDoor" &&
                    s.transform.parent.parent.name == "Room 1 - puzzle door");
                door.GetType().GetMethod("Interact").Invoke(door, new object[] { player });
                yield return new WaitForSeconds(0.6f);
                Assert.AreEqual(0.5f, level("Room1", "Hall1"));

                var args = new object[] { nodes["Room8"], nodes["Room7"], false };
                var path = (IEnumerable)graph.GetType().GetMethod("ApproachRoute").Invoke(graph, args);
                Assert.AreEqual(true, args[2]);
                Assert.AreEqual("EastHall", path.Cast<Component>().Last().name);

                var squad = scripts.Where(s => s.GetType().Name == "NPCChaser").ToArray();
                var caller = squad.Single(s => s.name == "SWAT - Room 6");
                string state(Component npc) => npc.GetType().GetProperty("CurrentState").GetValue(npc).ToString();
                Vector3 post = caller.transform.position;
                Quaternion facing = caller.transform.rotation;
                var body = player.GetComponent<CharacterController>();
                ((Behaviour)player.GetComponent("IvanLocomotion")).enabled = false;
                void place(Vector3 position)
                {
                    body.enabled = false;
                    player.transform.position = position;
                    body.enabled = true;
                    Physics.SyncTransforms();
                }
                Vector3 hide = player.transform.position;
                Vector3 seen = post + caller.transform.forward * 1.7f;
                place(seen);
                yield return null;
                yield return null;
                Assert.AreEqual("Chase", state(caller));
                foreach (var ally in squad.Where(s => s != caller)) Assert.AreEqual("InvestigateAlarm", state(ally));
                place(hide);
                bool reached = false, scanned = false;
                float deadline = Time.time + 14f;
                while (Time.time < deadline)
                {
                    if (state(caller) == "Search" && Vector3.Distance(caller.transform.position, seen) < 0.3f)
                    {
                        reached = true;
                        scanned |= Quaternion.Angle(caller.transform.rotation, facing) > 65f;
                    }
                    if (scanned && state(caller) == "Patrol" && Quaternion.Angle(caller.transform.rotation, facing) < 2f)
                        break;
                    yield return null;
                }
                Assert.IsTrue(reached, "The sentry did not reach the player's last visible position");
                Assert.IsTrue(scanned, "The sentry did not look around");
                Assert.AreEqual("Patrol", state(caller));
                Assert.Less(Vector3.Distance(post, caller.transform.position), 0.25f);
            }
            finally
            {
                Time.timeScale = scale;
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator CrouchedPlayerCanClosePuzzleDoorDuringTheEntrancePatrol()
        {
            yield return SceneManager.LoadSceneAsync("temple_v2", LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByName("temple_v2");
            float previousScale = Time.timeScale;
            try
            {
                yield return null;
                Time.timeScale = 3f;
                var scripts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>()).ToArray();
                var player = scripts.Single(s => s.GetType().Name == "PlayerInteractor");
                var guard = scripts.Single(s => s.name == "SWAT - Entrance corridor" && s.GetType().Name == "NPCChaser");
                var door = scripts.Single(s => s.GetType().Name == "TempleShortDoor" &&
                    s.transform.parent.parent.name == "Room 1 - puzzle door");
                var body = player.GetComponent<CharacterController>();
                ((Behaviour)player.GetComponent("IvanLocomotion")).enabled = false;
                var stealth = player.GetComponent("IvanStealth");
                const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance;
                stealth.GetType().GetMethod("SetCrouching", hidden).Invoke(stealth, new object[] { true });
                var camera = scripts.Single(s => s.GetType().Name == "IvanOrbitCamera");
                camera.enabled = false;
                var face = door.GetComponentInChildren<BoxCollider>();
                bool open() => (bool)door.GetType().GetProperty("IsPassable").GetValue(door);
                void useDoor()
                {
                    camera.transform.position = body.bounds.center;
                    camera.transform.LookAt(face.bounds.center);
                    Assert.AreSame(door, player.GetType().GetMethod("FindTargetInReach", hidden).Invoke(player, null));
                    door.GetType().GetMethod("Interact").Invoke(door, new object[] { player });
                }
                bool reachedGrate = false;
                void undetected()
                {
                    reachedGrate |= guard.transform.position.x > 15.8f;
                    Assert.AreEqual("Patrol", guard.GetType().GetProperty("CurrentState").GetValue(guard).ToString());
                }
                IEnumerator walkTo(Vector3 destination)
                {
                    float deadline = Time.time + 12f;
                    while (Time.time < deadline)
                    {
                        var delta = Vector3.ProjectOnPlane(destination - body.transform.position, Vector3.up);
                        if (delta.magnitude < 0.08f) yield break;
                        body.Move(Vector3.ClampMagnitude(delta, 0.95f * Time.deltaTime));
                        undetected();
                        yield return null;
                    }
                    Assert.Fail($"Blocked at {body.transform.position} on the way to {destination}");
                }

                float until = Time.time + 12f;
                while (guard.transform.position.x < 5 && Time.time < until) yield return null;
                Assert.GreaterOrEqual(guard.transform.position.x, 5f);
                // Begin at the foot of the entry stairs, after the guard has turned away.
                body.enabled = false;
                player.transform.position = new Vector3(-0.6f, 0.02f, 0);
                body.enabled = true;
                Physics.SyncTransforms();
                yield return walkTo(new Vector3(5.9f, 0, 1.45f));
                Assert.IsFalse(open());
                useDoor();
                yield return new WaitForSeconds(0.6f);
                Assert.IsTrue(open());
                yield return walkTo(new Vector3(5.9f, 0, 3.75f));
                useDoor();
                yield return new WaitForSeconds(0.6f);
                Assert.IsFalse(open());
                Assert.IsTrue(face.Raycast(new Ray(new Vector3(6f, 0.9f, 1f), Vector3.forward), out _, 3f));

                var tile = scripts.First(s => s.GetType().Name == "TempleSlidingTile" &&
                    (bool)s.GetType().GetMethod("CanInteract").Invoke(s, new object[] { player }));
                int initialSlot = (int)tile.GetType().GetProperty("Slot").GetValue(tile);
                tile.GetType().GetMethod("Interact").Invoke(tile, new object[] { player });
                Assert.AreNotEqual(initialSlot, tile.GetType().GetProperty("Slot").GetValue(tile));

                until = Time.time + 42f;
                do
                {
                    undetected();
                    if (reachedGrate && guard.transform.position.x < -0.75f) yield break;
                    yield return null;
                } while (Time.time < until);
                Assert.Fail($"Entrance patrol did not complete its round trip; last position {guard.transform.position}");
            }
            finally
            {
                Time.timeScale = previousScale;
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator OpenRoutesFitTheGuardCapsule()
        {
            yield return SceneManager.LoadSceneAsync("temple_v2", LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByName("temple_v2");
            try
            {
                yield return null;
                var scripts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>()).ToArray();
                var nodes = scripts.Where(s => s != null && s.GetType().Name == "Node").ToArray();
                var guards = scripts.Where(s => s != null && s.GetType().Name == "RifleNpc").ToArray();
                Assert.That(guards.Length, Is.EqualTo(3));
                Assert.That(nodes.Length, Is.GreaterThan(30));
                Assert.That(guards.Count(g => g.GetComponent("NPCChaser") != null), Is.EqualTo(3));
                Assert.That(scripts.Count(s => s != null && s.GetType().Name == "PlayerInteractor"), Is.EqualTo(1));
                var capsule = guards[0].GetComponent<CharacterController>();
                var problems = new List<string>();
                foreach (var node in nodes)
                {
                    var edges = (IEnumerable)node.GetType().GetField("edges").GetValue(node);
                    foreach (var edge in edges)
                    {
                        var type = edge.GetType();
                        var end = (Component)type.GetField("target").GetValue(edge);
                        Assert.IsNotNull(end);
                        if (string.CompareOrdinal(node.name, end.name) > 0 ||
                            (float)type.GetMethod("GetCost").Invoke(edge, null) == float.MaxValue) continue;
                        Vector3 a = node.transform.position, b = end.transform.position;
                        int samples = Mathf.CeilToInt(Vector3.Distance(a, b) / 0.2f);
                        for (int i = 0; i <= samples; i++)
                        {
                            Vector3 p = Vector3.Lerp(a, b, (float)i / samples);
                            var floor = Physics.RaycastAll(p + Vector3.up * 0.4f, Vector3.down, 0.85f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                                .Where(h => !(h.collider is CharacterController) && h.normal.y > 0.65f)
                                .OrderBy(h => Mathf.Abs(h.point.y - p.y)).ToArray();
                            if (floor.Length == 0) { problems.Add($"{node.name} - {end.name}: no floor at {p}"); break; }
                            p.y = floor[0].point.y;
                            var obstacle = Physics.OverlapCapsule(p + Vector3.up * (capsule.stepOffset + capsule.radius),
                                p + Vector3.up * (capsule.height - capsule.radius), capsule.radius,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                                .FirstOrDefault(h => !(h is CharacterController) && h.gameObject.scene == scene);
                            if (obstacle != null)
                            {
                                problems.Add($"{node.name} - {end.name}: {obstacle.name} at {p}");
                                break;
                            }
                        }
                    }
                }
                Assert.That(problems, Is.Empty, string.Join("\n", problems));
                var player = scripts.Single(s => s != null && s.GetType().Name == "PlayerInteractor");
                var lever = scripts.Single(s => s != null && s.GetType().Name == "TempleSouthLever");
                var button = scripts.Single(s => s != null && s.GetType().Name == "TempleSecretButton");
                var room4Gate = scripts.Single(s => s != null && s.GetType().Name == "TemplePassage" &&
                    s.name == "Mew gate to Room 4");
                var room5Gate = scripts.Single(s => s != null && s.GetType().Name == "TemplePassage" &&
                    s.name == "Mew gate to Room 5");
                bool IsPassable(Component c) => (bool)c.GetType().GetProperty("IsPassable").GetValue(c);
                Assert.IsFalse(IsPassable(room4Gate));
                Assert.IsTrue(IsPassable(room5Gate));
                lever.GetType().GetMethod("Interact").Invoke(lever, new object[] { player });
                button.GetType().GetMethod("Interact").Invoke(button, new object[] { player });
                yield return new WaitForSeconds(1.9f);
                Assert.IsTrue(IsPassable(room4Gate));
                Assert.IsFalse(IsPassable(room5Gate));
                var secret = scripts.Single(s => s != null && s.GetType().Name == "TemplePassage" &&
                    s.name == "Room 6 secret wall to Mew");
                Assert.IsTrue(IsPassable(secret));
                var document = scripts.Single(s => s != null && s.name == "Folded papers with linen wrap" &&
                    s.GetType().Name == "ItemPickup");
                Assert.That(InputSystem.actions.FindAction("Player/Interact").bindings.Any(b =>
                    b.effectivePath == "<Keyboard>/e"), Is.True);
                document.GetType().GetMethod("Interact").Invoke(document, new object[] { player });
                Assert.IsFalse(document.gameObject.activeSelf);
                var inventory = player.GetComponent("PlayerInventory");
                var collect = inventory.GetType().GetMethod("TryCollect");
                collect.Invoke(inventory, new object[] { "m0_key_room3" });
                collect.Invoke(inventory, new object[] { "m0_key_room4" });
                var gate = scripts.Single(s => s != null && s.GetType().Name == "M0GateMechanism");
                var interact = gate.GetType().GetMethod("Interact");
                for (int step = 0; step < 3; step++) interact.Invoke(gate, new object[] { player });
                var fixedGuard = guards.Single(g => g.name == "SWAT - Room 6");
                Vector3 position = fixedGuard.transform.position;
                Quaternion rotation = fixedGuard.transform.rotation;
                var mobile = guards.First(g => g != fixedGuard);
                Vector3 mobileStart = mobile.transform.position;
                yield return new WaitForSeconds(2.8f);
                Assert.That((bool)gate.GetType().GetProperty("IsOpen").GetValue(gate), Is.True);
                foreach (var chaser in guards.Where(g => g != fixedGuard).Select(g => g.GetComponent("NPCChaser")))
                    Assert.That(chaser.GetType().GetProperty("CurrentState").GetValue(chaser).ToString(),
                        Is.EqualTo("RushToGrate"));
                var exit = scripts.Single(s => s != null && s.GetType().Name == "TemplePassage" &&
                    s.transform.parent.name == "Closed exit door");
                Assert.That((bool)exit.GetType().GetProperty("IsPassable").GetValue(exit), Is.True);
                Assert.That(Vector3.ProjectOnPlane(fixedGuard.transform.position - position, Vector3.up).magnitude,
                    Is.LessThan(0.01f));
                Assert.That(Quaternion.Angle(fixedGuard.transform.rotation, rotation), Is.LessThan(0.01f));
                Assert.That(Vector3.Distance(mobile.transform.position, mobileStart), Is.GreaterThan(0.2f));
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
        }
    }
}
