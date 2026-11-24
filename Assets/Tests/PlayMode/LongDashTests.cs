using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class LongDashTests
    {
        private const string ArenaPath = "Assets/Scenes/Levels/Temple_Final_Room.unity";
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private Scene arena;
        private Component boss;
        private CharacterController player;
        private Component health;
        private PropertyInfo hitPoints;
        private float previousStep;

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            previousStep = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            yield return SceneManager.LoadSceneAsync(ArenaPath, LoadSceneMode.Additive);
            arena = SceneManager.GetSceneByPath(ArenaPath);
            var roots = arena.GetRootGameObjects();
            foreach (var guard in roots.Where(root => root.GetComponent("RifleNpc") != null))
                guard.SetActive(false);
            boss = roots.Select(root => root.GetComponent("AngelSealAttack")).Single(attack => attack != null);
            NimbAttackSelectionTests.UseFixedOrder(boss);
            player = roots.Single(root => root.CompareTag("Player")).GetComponent<CharacterController>();
            health = player.GetComponent("CombatHealth");
            hitPoints = health.GetType().GetProperty("HP");
            yield return null;
        }

        private void PlacePlayer(float x, float z)
        {
            player.enabled = false;
            player.transform.position = new Vector3(x, player.transform.position.y, z);
            player.enabled = true;
            Physics.SyncTransforms();
        }

        private void QueueDash()
        {
            var type = boss.GetType();
            var next = type.GetField("nextAttack", Private);
            next.SetValue(boss, System.Enum.Parse(next.FieldType, "HeadDash"));
            type.GetField("engaged", Private).SetValue(boss, true);
            type.GetField("nextCast", Private).SetValue(boss, Time.time);
        }

        [UnityTest]
        public IEnumerator DistantIvanReceivesTheLockedDash([ValueSource(nameof(BooleanCases))] bool dodge)
        {
            Vector3 origin = new Vector3(-16f, boss.transform.position.y, 2f);
            boss.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(Vector3.right));
            PlacePlayer(16f, 2f);
            float initial = (float)hitPoints.GetValue(health);
            QueueDash();

            var line = boss.transform.Find("Locked dash path").GetComponent<LineRenderer>();
            var skull = boss.GetComponentsInChildren<Transform>().Single(t => t.name == "skeleton:head");
            var dashing = boss.GetType().GetField("dashing", Private);
            float deadline = Time.time + 2f;
            while (!line.enabled && Time.time < deadline) yield return null;
            Assert.IsTrue(line.enabled, "An open 32 m lane must allow the dash");

            // The stripe grows into the already-locked endpoint during the warning.
            if (dodge) PlacePlayer(16f, 5f);
            float farthestStripeX = line.GetPosition(1).x;
            while (line.enabled && Time.time < deadline)
            {
                farthestStripeX = Mathf.Max(farthestStripeX, line.GetPosition(1).x);
                yield return null;
            }
            Assert.GreaterOrEqual(farthestStripeX, 15.5f, "The visible warning must reach Ivan's original position");
            float farthestHeadX = skull.position.x;
            float peakRiseDegreesPerSecond = 0f;
            Quaternion previousRotation = boss.transform.rotation;
            deadline = Time.time + 7f;
            while ((bool)dashing.GetValue(boss) && Time.time < deadline)
            {
                farthestHeadX = Mathf.Max(farthestHeadX, skull.position.x);
                // After passing Ivan, the head-first dive lifts through this angle on its way to hover.
                if (farthestHeadX >= 15.5f && Vector3.Angle(boss.transform.up, Vector3.up) < 65f)
                {
                    float step = Quaternion.Angle(previousRotation, boss.transform.rotation);
                    peakRiseDegreesPerSecond = Mathf.Max(peakRiseDegreesPerSecond, step / Time.deltaTime);
                }
                previousRotation = boss.transform.rotation;
                yield return null;
            }
            Assert.IsFalse((bool)dashing.GetValue(boss));
            Assert.GreaterOrEqual(farthestHeadX, 15.5f, "The head must fly across the whole open lane");
            Assert.That(peakRiseDegreesPerSecond, Is.InRange(1f, 180f),
                "The turn from horizontal flight to upright hover must be visible and gradual");
            Assert.AreEqual(initial - (dodge ? 0f : 35f), (float)hitPoints.GetValue(health));
        }

        [UnityTest]
        public IEnumerator DashSoundsAtTheStartOfItsWindup()
        {
            var original = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(arena);
            try
            {
                boss.transform.SetPositionAndRotation(new Vector3(-6f, boss.transform.position.y, 2f),
                    Quaternion.LookRotation(Vector3.right));
                PlacePlayer(4f, 2f);
                QueueDash();
                var dashing = boss.GetType().GetField("dashing", Private);
                float deadline = Time.time + .5f;
                while (!(bool)dashing.GetValue(boss) && Time.time < deadline) yield return null;
                Assert.IsTrue((bool)dashing.GetValue(boss));
                var audio = GameObject.Find("Game audio");
                Assert.AreEqual(1, audio.GetComponentsInChildren<AudioSource>().Count(source =>
                    source.clip?.name == "HeadDash" && source.isPlaying),
                    "The cue must be audible on the first windup frame");
            }
            finally
            {
                SceneManager.SetActiveScene(original);
            }
        }

        [UnityTest]
        public IEnumerator CloseIvanGetsAShortDashWithRoomToBrake()
        {
            boss.transform.SetPositionAndRotation(new Vector3(-6f, boss.transform.position.y, 2f),
                Quaternion.LookRotation(Vector3.right));
            const float targetX = -4.5f;
            PlacePlayer(targetX, 2f);
            float initial = (float)hitPoints.GetValue(health);
            QueueDash();

            var skull = boss.GetComponentsInChildren<Transform>().Single(t => t.name == "skeleton:head");
            var line = boss.transform.Find("Locked dash path").GetComponent<LineRenderer>();
            float deadline = Time.time + 1.5f;
            while (!line.enabled && Time.time < deadline) yield return null;
            Assert.IsTrue(line.enabled, "A close player in an open lane must still allow a dash");

            var dashing = boss.GetType().GetField("dashing", Private);
            float furthest = skull.position.x;
            deadline = Time.time + 5f;
            while ((bool)dashing.GetValue(boss) && Time.time < deadline)
            {
                furthest = Mathf.Max(furthest, skull.position.x);
                yield return null;
            }
            Assert.IsFalse((bool)dashing.GetValue(boss));
            Assert.AreEqual(initial - 35f, (float)hitPoints.GetValue(health));
            Assert.That(furthest - targetX, Is.InRange(-.5f, 3.3f),
                "After a near target the boss must brake locally");
        }

        private static readonly bool[] BooleanCases = { false, true };

        [UnityTest]
        public IEnumerator DashRecoveryReturnsFromSummonToHover([ValueSource(nameof(BooleanCases))] bool interrupt)
        {
            var animator = boss.GetComponent<Animator>();
            var hover = animator.GetCurrentAnimatorClipInfo(0).Single().clip;
            int hoverState = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            int riseState = Animator.StringToHash("Base Layer.Dash Rise");
            float normalSpeed = animator.speed;
            boss.transform.SetPositionAndRotation(new Vector3(-6f, boss.transform.position.y, 2f),
                Quaternion.LookRotation(Vector3.right));
            PlacePlayer(4f, 2f);
            QueueDash();

            float deadline = Time.time + 6f;
            while (animator.GetCurrentAnimatorStateInfo(0).fullPathHash != riseState && Time.time < deadline)
                yield return null;
            Assert.AreEqual(riseState, animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                "The open dash must reach the authored recovery");
            var rise = animator.GetCurrentAnimatorClipInfo(0).Single();
            Assert.AreEqual("Summon", rise.clip.name);
            Assert.Greater(rise.weight, .5f, "The authored skeleton motion must contribute to the visible pose");

            var dashing = boss.GetType().GetField("dashing", Private);
            if (interrupt)
            {
                ((Behaviour)boss).enabled = false;
                float resetBy = Time.time + .75f;
                do
                {
                    yield return null;
                }
                while ((animator.IsInTransition(0) ||
                    animator.GetCurrentAnimatorStateInfo(0).fullPathHash != hoverState) && Time.time < resetBy);
            }
            else
            {
                var handSkins = boss.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(skin => skin.name.EndsWith(":arm_mesh", System.StringComparison.Ordinal)).ToArray();
                var sourceHands = boss.GetComponentsInChildren<Transform>(true)
                    .Where(bone => bone.name.EndsWith(":arm_root", System.StringComparison.Ordinal))
                    .Select(bone => bone.name.Replace(":arm_root", ":arm_mesh")).ToArray();
                Assert.IsNotEmpty(sourceHands);
                bool blendedBack = false;
                Vector3 previousPosition = boss.transform.position;
                Quaternion previousRotation = boss.transform.rotation;
                while ((bool)dashing.GetValue(boss) && Time.time < deadline)
                {
                    CollectionAssert.AreEquivalent(sourceHands, handSkins
                        .Where(skin => skin.enabled && skin.gameObject.activeInHierarchy && !skin.forceRenderingOff)
                        .Select(skin => skin.name), "Every hand in the imported rig should remain visible during the rise");
                    if (animator.IsInTransition(0) &&
                        animator.GetCurrentAnimatorStateInfo(0).fullPathHash == riseState &&
                        animator.GetNextAnimatorStateInfo(0).fullPathHash == hoverState)
                    {
                        var outgoing = animator.GetCurrentAnimatorClipInfo(0);
                        var incoming = animator.GetNextAnimatorClipInfo(0);
                        blendedBack |= outgoing.Any(c => c.clip == rise.clip && c.weight > .01f)
                            && incoming.Any(c => c.clip == hover && c.weight > .01f);
                    }
                    previousPosition = boss.transform.position;
                    previousRotation = boss.transform.rotation;
                    yield return null;
                }
                Assert.IsTrue(blendedBack, "Summon and hover must both contribute during the handoff");
                Assert.Less(Vector3.Distance(previousPosition, boss.transform.position), .15f,
                    "The root jumped on the last frame");
                Assert.Less(Quaternion.Angle(previousRotation, boss.transform.rotation), 5f,
                    "The root snapped upright on the last frame");
            }

            Assert.IsFalse((bool)dashing.GetValue(boss));
            Assert.AreEqual(hoverState, animator.GetCurrentAnimatorStateInfo(0).fullPathHash);
            Assert.IsFalse(animator.IsInTransition(0));
            Assert.AreSame(hover, animator.GetCurrentAnimatorClipInfo(0).Single().clip);
            Assert.AreEqual(normalSpeed, animator.speed, .001f);
            Assert.Less(Vector3.Angle(boss.transform.up, Vector3.up), 1f);
        }

        [UnityTest]
        public IEnumerator HoveringHandsStayInsideTheirCullingBounds()
        {
            var animator = boss.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            int hover = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            var hands = boss.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(skin => skin.enabled && skin.name.EndsWith(":arm_mesh", System.StringComparison.Ordinal)).ToArray();
            Assert.IsNotEmpty(hands);
            var mesh = new Mesh();
            try
            {
                for (int quarter = 0; quarter < 4; quarter++)
                {
                    animator.Play(hover, 0, quarter * .25f);
                    animator.Update(0f);
                    yield return null;
                    foreach (var hand in hands)
                    {
                        hand.BakeMesh(mesh, true);
                        mesh.RecalculateBounds();
                        Vector3 center = hand.transform.TransformPoint(mesh.bounds.center);
                        Assert.IsTrue(hand.bounds.Contains(center),
                            $"{hand.name} could be culled while its mesh is on screen at hover phase {quarter}/4");
                    }
                }
            }
            finally { UnityEngine.Object.Destroy(mesh); }
        }

        [UnityTest]
        public IEnumerator AnInterveningColumnSkipsTheDashAndStartsTheEyes()
        {
            boss.transform.position = new Vector3(0f, boss.transform.position.y, 0f);
            PlacePlayer(0f, -14f); // The south column stands between these points at z = -10.
            var line = boss.transform.Find("Locked dash path").GetComponent<LineRenderer>();
            var rings = boss.GetComponentsInChildren<LineRenderer>().Where(r => r.name == "Seal boundary").ToArray();
            var dashing = boss.GetType().GetField("dashing", Private);
            Vector3 atSelection = boss.transform.position;
            QueueDash();
            float deadline = Time.time + .5f;
            while (!rings.Any(r => r.enabled) && Time.time < deadline)
            {
                Assert.IsFalse((bool)dashing.GetValue(boss), "A blocked lane consumed a dash");
                Assert.IsFalse(line.enabled);
                yield return null;
            }
            Assert.IsTrue(rings.Any(r => r.enabled), "The normal attack selector should advance to the eyes");
            Assert.IsFalse((bool)dashing.GetValue(boss));
            Assert.Less(Vector3.Distance(atSelection, boss.transform.position), .05f);
        }

        [UnityTest]
        public IEnumerator ColumnBeyondThePlayerLeavesTheDashAvailable()
        {
            boss.transform.position = new Vector3(0f, boss.transform.position.y, 0f);
            PlacePlayer(0f, -6f); // Four metres of clear ground before the south column.
            QueueDash();
            var line = boss.transform.Find("Locked dash path").GetComponent<LineRenderer>();
            float deadline = Time.time + 1.2f;
            while (!line.enabled && Time.time < deadline) yield return null;
            Assert.IsTrue(line.enabled, "A column beyond Ivan must not suppress the dash");
        }

        [UnityTest]
        public IEnumerator DashStopsAtAnObstacleThatEntersTheLockedLane()
        {
            boss.transform.SetPositionAndRotation(new Vector3(-6f, boss.transform.position.y, 2f),
                Quaternion.LookRotation(Vector3.right));
            PlacePlayer(4f, 2f);
            float initial = (float)hitPoints.GetValue(health);
            QueueDash();

            var line = boss.transform.Find("Locked dash path").GetComponent<LineRenderer>();
            float deadline = Time.time + 1.2f;
            while (!line.enabled && Time.time < deadline) yield return null;
            Assert.IsTrue(line.enabled, "The dash must already have locked onto Ivan");

            var wall = AddDashObstacle(new Vector3(-.5f, 2f, 2f), new Vector3(1f, 4f, 4f));
            var skull = boss.GetComponentsInChildren<Transform>().Single(t => t.name == "skeleton:head");
            var dashing = boss.GetType().GetField("dashing", Private);
            float farthestHeadX = skull.position.x;
            deadline = Time.time + 7f;
            while ((bool)dashing.GetValue(boss) && Time.time < deadline)
            {
                farthestHeadX = Mathf.Max(farthestHeadX, skull.position.x);
                yield return null;
            }

            Assert.IsFalse((bool)dashing.GetValue(boss), "The dash must finish recovering after impact");
            Assert.LessOrEqual(farthestHeadX, wall.bounds.min.x - .2f, "The skull crossed the late obstacle");
            Assert.AreEqual(initial, (float)hitPoints.GetValue(health), "The dash damaged Ivan through stone");
        }

        [UnityTest]
        public IEnumerator DashFinishesClearOfAnOverhangAboveTheSkullLane()
        {
            boss.transform.SetPositionAndRotation(new Vector3(-6f, boss.transform.position.y, 2f),
                Quaternion.LookRotation(Vector3.right));
            PlacePlayer(4f, 2f);
            float initial = (float)hitPoints.GetValue(health);
            float overhangBottom = player.bounds.min.y + 1.85f;
            var overhang = AddDashObstacle(new Vector3(7.4f, overhangBottom + 1.25f, 2f),
                new Vector3(4f, 2.5f, 4f));
            QueueDash();

            var line = boss.transform.Find("Locked dash path").GetComponent<LineRenderer>();
            float deadline = Time.time + 1.2f;
            while (!line.enabled && Time.time < deadline) yield return null;
            Assert.IsTrue(line.enabled, "Ivan stands clear in front of the overhang");

            var skull = boss.GetComponentsInChildren<Transform>().Single(t => t.name == "skeleton:head");
            var dashing = boss.GetType().GetField("dashing", Private);
            deadline = Time.time + 7f;
            while ((bool)dashing.GetValue(boss) && Time.time < deadline) yield return null;

            Assert.IsFalse((bool)dashing.GetValue(boss), "The boss never recovered to hover");
            Assert.Less(Vector3.Angle(boss.transform.up, Vector3.up), 1f);
            Assert.GreaterOrEqual(Vector3.Distance(skull.position, overhang.ClosestPoint(skull.position)), .2f,
                $"The boss ended its rise with the skull at {skull.position} inside or against the overhang");
            Assert.AreEqual(initial - 35f, (float)hitPoints.GetValue(health), "The clear part of the dash still hits Ivan");
        }

        [UnityTest]
        public IEnumerator DashRemainsInsideTheArenaWhenIvanRetreatsThroughTheEntrance()
        {
            var lintel = arena.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BoxCollider>())
                .Single(collider => collider.name == "Hall entrance lintel");
            float threshold = lintel.bounds.max.z;
            boss.transform.SetPositionAndRotation(new Vector3(0f, boss.transform.position.y, threshold + 7f),
                Quaternion.LookRotation(Vector3.back));
            PlacePlayer(0f, threshold - 1.5f);
            float initial = (float)hitPoints.GetValue(health);
            QueueDash();

            var line = boss.transform.Find("Locked dash path").GetComponent<LineRenderer>();
            float deadline = Time.time + 1.2f;
            while (!line.enabled && Time.time < deadline) yield return null;
            Assert.IsTrue(line.enabled, "The entrance has an open dash lane at floor level");

            var skull = boss.GetComponentsInChildren<Transform>().Single(t => t.name == "skeleton:head");
            var dashing = boss.GetType().GetField("dashing", Private);
            float farthestHead = skull.position.z;
            deadline = Time.time + 7f;
            while ((bool)dashing.GetValue(boss) && Time.time < deadline)
            {
                farthestHead = Mathf.Min(farthestHead, skull.position.z);
                yield return null;
            }

            Assert.IsFalse((bool)dashing.GetValue(boss), "The dash must recover inside the hall");
            Assert.GreaterOrEqual(farthestHead, threshold, "The skull crossed the entrance into the corridor");
            Assert.GreaterOrEqual(boss.transform.position.z, threshold, "The boss entered the corridor");
            Assert.AreEqual(initial, (float)hitPoints.GetValue(health), "The dash reached Ivan outside the hall");
        }

        private BoxCollider AddDashObstacle(Vector3 position, Vector3 size)
        {
            var obstacle = new GameObject("Dash obstacle");
            SceneManager.MoveGameObjectToScene(obstacle, arena);
            obstacle.transform.position = position;
            var collider = obstacle.AddComponent<BoxCollider>();
            collider.size = size;
            Physics.SyncTransforms();
            return collider;
        }

        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            Time.captureDeltaTime = previousStep;
            if (arena.IsValid() && arena.isLoaded) yield return SceneManager.UnloadSceneAsync(arena);
        }
    }
}
