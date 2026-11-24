using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    // The boss restores its bones in Update and poses them in LateUpdate. Sample what will render.
    [DefaultExecutionOrder(30000)]
    public sealed class HandTouchFrameObserver : MonoBehaviour
    {
        public System.Action Capture;
        public System.Action AfterCapture;

        private void LateUpdate()
        {
            Capture?.Invoke();
            if (AfterCapture == null) return;
            var action = AfterCapture;
            AfterCapture = null;
            action();
        }
    }

    public sealed class HandTouchTests
    {
        private const string FinalRoom = "Assets/Scenes/Levels/Temple_Final_Room.unity";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private Scene arena;
        private Behaviour boss;
        private CharacterController player;
        private Component health;
        private object hand;
        private FieldInfo touching, next;
        private PropertyInfo locked, returning, palm, hp;
        private Transform[] wrists;
        private Vector3[] renderedWrists;
        private Transform forearm, strikingWrist, fingertip;
        private Transform[,] digits;
        private Vector3 renderedTip, renderedForearmDirection;
        private float renderedFingerAngle;
        private HandTouchFrameObserver observer;
        private Vector3 renderedPalm, renderedRoot;
        private Bounds renderedBody;
        private bool renderedTouching, renderedLocked, renderedReturning;
        private float renderedHp;
        private float previousStep;

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            previousStep = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 40f;
            yield return SceneManager.LoadSceneAsync(FinalRoom, LoadSceneMode.Additive);
            arena = SceneManager.GetSceneByPath(FinalRoom);
            var roots = arena.GetRootGameObjects();
            foreach (var guard in roots.Where(root => root.GetComponent("RifleNpc") != null))
                guard.SetActive(false);
            boss = (Behaviour)roots.Select(root => root.GetComponent("AngelSealAttack")).Single(attack => attack != null);
            NimbAttackSelectionTests.UseFixedOrder(boss);
            player = roots.Single(root => root.CompareTag("Player")).GetComponent<CharacterController>();
            health = player.GetComponent("CombatHealth");
            hp = health.GetType().GetProperty("HP");
            yield return null;
            var type = boss.GetType();
            touching = type.GetField("touching", Members);
            next = type.GetField("nextAttack", Members);
            hand = type.GetField("handTouch", Members).GetValue(boss);
            locked = hand.GetType().GetProperty("Locked", Members);
            returning = hand.GetType().GetProperty("Returning", Members);
            palm = hand.GetType().GetProperty("PalmPosition", Members);
            var bones = boss.GetComponentsInChildren<Transform>();
            wrists = bones.Where(bone => bone.name.EndsWith(":arm_wrist", System.StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(10, wrists.Length);
            renderedWrists = new Vector3[wrists.Length];
            forearm = bones.Single(b => b.name == "hand1:arm_root");
            strikingWrist = bones.Single(b => b.name == "hand1:arm_wrist");
            string[] names = { "index", "middle", "ring", "little" };
            digits = new Transform[names.Length, 4];
            for (int d = 0; d < names.Length; d++)
                for (int j = 0; j < 4; j++)
                    digits[d, j] = bones.Single(b => b.name == $"hand1:{names[d]}_{j + 2}");
            fingertip = digits[1, 3];
            observer = boss.gameObject.AddComponent<HandTouchFrameObserver>();
            observer.Capture = CapturePose;
        }

        private void CapturePose()
        {
            for (int i = 0; i < wrists.Length; i++) renderedWrists[i] = wrists[i].position;
            renderedPalm = (Vector3)palm.GetValue(hand);
            renderedTip = fingertip.position;
            renderedForearmDirection = (strikingWrist.position - forearm.position).normalized;
            renderedFingerAngle = 0f;
            for (int d = 0; d < digits.GetLength(0); d++)
                for (int j = 1; j < digits.GetLength(1); j++)
                    renderedFingerAngle = Mathf.Max(renderedFingerAngle, Vector3.Angle(renderedForearmDirection,
                        digits[d, j].position - digits[d, j - 1].position));
            renderedRoot = boss.transform.position;
            renderedBody = player.bounds;
            renderedTouching = (bool)touching.GetValue(boss);
            renderedLocked = (bool)locked.GetValue(hand);
            renderedReturning = (bool)returning.GetValue(hand);
            renderedHp = (float)hp.GetValue(health);
        }

        private void PlacePlayer(float x, float z)
        {
            player.enabled = false;
            player.transform.position = new Vector3(x, player.transform.position.y, z);
            player.enabled = true;
            Physics.SyncTransforms();
        }

        private void Queue(string attack)
        {
            next.SetValue(boss, System.Enum.Parse(next.FieldType, attack));
            boss.GetType().GetField("engaged", Members).SetValue(boss, true);
            boss.GetType().GetField("nextCast", Members).SetValue(boss, Time.time);
        }

        private IEnumerator WaitForHand(float timeout)
        {
            float deadline = Time.time + timeout;
            while (!renderedTouching && Time.time < deadline) yield return null;
            Assert.IsTrue(renderedTouching, "The selected attack must raise one of the detached hands");
        }

        public enum Response { Stand, DodgeAndCrossReturn, Interrupt }
        private static readonly Response[] Responses = { Response.Stand, Response.DodgeAndCrossReturn, Response.Interrupt };

        [UnityTest]
        public IEnumerator OneRealHandReachesIvanWhileTheBossHovers([ValueSource(nameof(Responses))] Response response)
        {
            Vector3 start = new Vector3(-6f, boss.transform.position.y, 2f);
            boss.transform.SetPositionAndRotation(start, Quaternion.LookRotation(Vector3.right));
            PlacePlayer(6f, 2f);
            float initialHp = (float)hp.GetValue(health);
            int hits = 0;
            Vector3 tipAtHit = default;
            bool retractingAtHit = false;
            var hitEvent = (UnityEvent)health.GetType().GetField("onHit").GetValue(health);
            hitEvent.AddListener(() =>
            {
                ++hits;
                tipAtHit = fingertip.position;
                retractingAtHit = (bool)returning.GetValue(hand);
            });
            Queue("HandTouch");
            yield return WaitForHand(.6f);

            bool evaded = false, crossedReturn = false, sawContact = false;
            float farthestHand = 0f;
            var outbound = new List<Vector3>();
            float deadline = Time.time + 10f;
            while (renderedTouching && Time.time < deadline)
            {
                Vector3 palmCentre = renderedPalm;
                Assert.Less(Vector3.Distance(start, renderedRoot), .025f, "The attack displaced the boss");
                var reaches = renderedWrists.Select(wrist => wrist.x - start.x).OrderByDescending(reach => reach).ToArray();
                farthestHand = Mathf.Max(farthestHand, reaches[0]);
                Assert.Less(reaches[1], 3f, "Only the striking hand may leave the fan");
                if (renderedLocked && !renderedReturning && reaches[0] > 3f)
                {
                    if (outbound.Count > 0)
                    {
                        Vector3 travel = renderedTip - outbound[outbound.Count - 1];
                        if (travel.sqrMagnitude > .0001f)
                            Assert.Less(Vector3.Angle(renderedForearmDirection, travel), 16f,
                                "The forearm must point along the thrust");
                    }
                    Assert.Less(renderedFingerAngle, 24f, "The four striking fingers must stay extended in line with the wrist");
                    outbound.Add(renderedTip);
                }

                if (response == Response.DodgeAndCrossReturn)
                {
                    if (!evaded && renderedLocked)
                    {
                        PlacePlayer(6f, 5.2f);
                        evaded = true;
                    }
                    if (!crossedReturn && renderedReturning)
                    {
                        // Enter the hand itself just as it starts back, after dodging the outbound strike.
                        PlacePlayer(palmCentre.x, palmCentre.z);
                        Assert.Less(player.bounds.SqrDistance(palmCentre), .36f);
                        crossedReturn = true;
                    }
                    Assert.AreEqual(initialHp, renderedHp);
                }
                else if (response == Response.Interrupt && reaches[0] > 5f)
                {
                    // Disable after the next pose has been applied, while the actual hand is extended.
                    bool cancelledInFlight = false, releasedWrists = false;
                    observer.AfterCapture = () =>
                    {
                        cancelledInFlight = renderedTouching && !renderedReturning && renderedHp == initialHp
                            && renderedWrists.Any(wrist => wrist.x - start.x > 5f);
                        boss.enabled = false;
                        releasedWrists = wrists.All(wrist => Mathf.Abs(wrist.position.x - start.x) < 3f);
                    };
                    yield return null;
                    Assert.IsTrue(cancelledInFlight);
                    Assert.IsFalse((bool)touching.GetValue(boss));
                    Assert.IsTrue(releasedWrists, "Disabling the attack must relinquish the animated hand immediately");
                    yield return new WaitForSeconds(1f);
                    Assert.AreEqual(initialHp, hp.GetValue(health));
                    yield break;
                }
                else if (response == Response.Stand && !sawContact && renderedHp < initialHp)
                {
                    Assert.AreEqual(1, hits);
                    Assert.IsFalse(retractingAtHit, "First contact must happen on the outbound thrust");
                    Assert.Less(renderedBody.SqrDistance(tipAtHit), .12f * .12f,
                        "Damage must coincide with the real extended fingertip reaching Ivan");
                    sawContact = true;
                }
                yield return null;
            }
            Assert.IsFalse((bool)touching.GetValue(boss), "The hand should finish retracting");
            Assert.AreNotEqual(Response.Interrupt, response, "The real wrist never left the boss");
            Assert.Greater(farthestHand, 10f, "The actual rig must reach the far side of the open lane");
            Assert.IsTrue(renderedWrists.All(wrist => Mathf.Abs(wrist.x - start.x) < 3f));
            Assert.AreEqual("LightArrows", next.GetValue(boss).ToString());
            Assert.Greater(outbound.Count, 2);
            Vector3 direction = (outbound[outbound.Count - 1] - outbound[0]).normalized;
            foreach (Vector3 point in outbound)
                Assert.Less(Vector3.Cross(point - outbound[0], direction).magnitude, .06f,
                    "The hand curved away from its locked flight line");
            if (response == Response.DodgeAndCrossReturn)
                Assert.IsTrue(evaded && crossedReturn);
            else
            {
                Assert.IsTrue(sawContact);
                Assert.Greater(farthestHand + start.x, renderedBody.max.x + .05f,
                    "The wrist should visibly pass through the far side of Ivan's body");
            }
            Assert.AreEqual(response == Response.Stand ? 1 : 0, hits);
            Assert.AreEqual(initialHp - (response == Response.Stand ? 35f : 0f), hp.GetValue(health));
        }

        private static readonly bool[] PlayerInFront = { false, true };

        [UnityTest]
        public IEnumerator WaveLeadsToTouchAndTheColumnStopsTheFinger(
            [ValueSource(nameof(PlayerInFront))] bool playerBeforeStone)
        {
            Vector3 start = new Vector3(0f, boss.transform.position.y, 0f);
            boss.transform.SetPositionAndRotation(start, Quaternion.LookRotation(Vector3.back));
            var column = arena.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CapsuleCollider>())
                .Where(collider => collider.name == "Hall column").OrderBy(collider => collider.bounds.center.z).First();
            var stone = column.bounds;
            float playerZ = playerBeforeStone ? stone.max.z + player.bounds.extents.z + .06f : stone.center.z - 2.5f;
            PlacePlayer(stone.center.x, playerZ);
            Queue("HaloWave");
            yield return WaitForHand(20f);
            float previousHp = (float)hp.GetValue(health);
            float damageTaken = 0f;
            int hits = 0;
            var healthChanged = health.GetType().GetEvent("Changed");
            System.Action<float> recordHealthChange = currentHp =>
            {
                if (currentHp < previousHp)
                {
                    hits++;
                    damageTaken += previousHp - currentHp;
                }
                previousHp = currentHp;
            };
            healthChanged.AddEventHandler(health, recordHealthChange);

            float nearestStone = float.PositiveInfinity;
            float deadline = Time.time + 9f;
            while (renderedTouching && Time.time < deadline)
            {
                nearestStone = Mathf.Min(nearestStone, Vector3.Distance(renderedTip, column.ClosestPoint(renderedTip)));
                Assert.Greater(renderedTip.z, stone.max.z - .15f, "The fingertip continued into the column");
                Assert.Less(Vector3.Distance(start, renderedRoot), .025f);
                yield return null;
            }
            healthChanged.RemoveEventHandler(health, recordHealthChange);
            Assert.IsFalse((bool)touching.GetValue(boss));
            Assert.Less(nearestStone, .16f, "The fingertip must reach the near surface of the column");
            Assert.AreEqual(playerBeforeStone ? 1 : 0, hits);
            Assert.That(damageTaken, Is.EqualTo(playerBeforeStone ? 35f : 0f).Within(.001f));
        }

        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            if (observer != null) observer.Capture = observer.AfterCapture = null;
            Time.captureDeltaTime = previousStep;
            if (arena.IsValid() && arena.isLoaded) yield return SceneManager.UnloadSceneAsync(arena);
        }
    }
}
