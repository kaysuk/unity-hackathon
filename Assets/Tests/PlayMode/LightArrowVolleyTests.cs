using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class LightArrowVolleyTests
    {
        private const string FinalRoom = "Assets/Scenes/Levels/Temple_Final_Room.unity";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene arena;
        private Behaviour attack;
        private CharacterController player;
        private Component health;
        private FieldInfo firing;
        private FieldInfo next;
        private PropertyInfo hp;
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
            attack = (Behaviour)roots.Select(root => root.GetComponent("AngelSealAttack")).Single(boss => boss != null);
            NimbAttackSelectionTests.UseFixedOrder(attack);
            player = roots.Single(root => root.CompareTag("Player")).GetComponent<CharacterController>();
            health = player.GetComponent("CombatHealth");
            hp = health.GetType().GetProperty("HP");
            firing = attack.GetType().GetField("firingArrows", Private);
            next = attack.GetType().GetField("nextAttack", Private);
            yield return null;
        }

        private void PlacePlayer(float x, float z)
        {
            player.enabled = false;
            player.transform.position = new Vector3(x, player.transform.position.y, z);
            player.enabled = true;
            Physics.SyncTransforms();
        }

        private void QueueArrows()
        {
            next.SetValue(attack, System.Enum.Parse(next.FieldType, "LightArrows"));
            attack.GetType().GetField("engaged", Private).SetValue(attack, true);
            attack.GetType().GetField("nextCast", Private).SetValue(attack, Time.time);
        }

        private Transform[] FindArrows()
        {
            var view = arena.GetRootGameObjects().Single(root => root.name == "Light arrow volley").transform;
            return Enumerable.Range(1, 6).Select(i => view.Find("Light arrow " + i)).ToArray();
        }

        [UnityTest]
        public IEnumerator EachReleasedSpearSoundsOnceAtItsMount()
        {
            var original = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(arena);
            try
            {
                attack.transform.SetPositionAndRotation(new Vector3(0f, attack.transform.position.y, 0f),
                    Quaternion.LookRotation(Vector3.back));
                PlacePlayer(0f, -7f);
                QueueArrows();
                float deadline = Time.time + .5f;
                while (!(bool)firing.GetValue(attack) && Time.time < deadline) yield return null;
                Assert.IsTrue((bool)firing.GetValue(attack));

                var origins = FindArrows().Select(arrow => arrow.position).ToArray();
                var volley = attack.GetType().GetField("lightArrows", Private).GetValue(attack);
                var tick = volley.GetType().GetMethod("Tick", Private);
                var audio = GameObject.Find("Game audio");
                AudioSource[] SpearVoices() => audio.GetComponentsInChildren<AudioSource>()
                    .Where(source => source.clip?.name == "LightSpear" && source.isPlaying).ToArray();
                void Tick(float elapsed) => tick.Invoke(volley, new object[] { elapsed, 0f });

                // The first release is exact. Later boundaries need a small tolerance for float rounding.
                for (int i = 0; i < origins.Length; i++)
                {
                    float release = i == 0 ? 1f : 1f + i * .32f + .0001f;
                    Tick(release - .04f);
                    Assert.IsEmpty(SpearVoices(), $"Spear {i + 1} sounded during the lock flash");
                    Tick(release);
                    var voices = SpearVoices();
                    Assert.AreEqual(1, voices.Length, $"Expected one sound on release of spear {i + 1}");
                    Assert.Less(Vector3.Distance(voices[0].transform.position, origins[i]), .01f);
                    // Return the source to the pool so an accidental retrigger is observable without DSP timing.
                    voices[0].Stop();
                    Tick(release);
                    Tick(release + .03f);
                    Assert.IsEmpty(SpearVoices(), $"Spear {i + 1} sounded again during flight");
                }
            }
            finally
            {
                SceneManager.SetActiveScene(original);
            }
        }

        private static float VisibleLength(Transform spear)
        {
            var mesh = spear.GetComponentInChildren<MeshFilter>();
            var points = mesh.sharedMesh.vertices.Select(vertex =>
                Vector3.Dot(mesh.transform.TransformVector(vertex), spear.forward)).ToArray();
            return points.Max() - points.Min();
        }

        private static void AssertPointInGameplayView(Camera camera, Transform spear, float length)
        {
            Vector3 towardCamera = camera.transform.position - spear.position;
            Assert.Less(Vector3.Angle(spear.forward, towardCamera), .5f,
                "The viewer should see the tip of each waiting spear");
            Vector2 tip = camera.WorldToViewportPoint(spear.position);
            Vector2 tail = camera.WorldToViewportPoint(spear.position - spear.forward * length);
            Assert.Less(Vector2.Distance(tip, tail), .003f,
                "The projected shaft should fit behind the point");
        }

        [UnityTest]
        public IEnumerator DisablingBossAfterProjectileRootDestructionIsSafe()
        {
            GameObject root = arena.GetRootGameObjects().Single(obj => obj.name == "Light arrow volley");
            Assert.IsTrue(attack.isActiveAndEnabled);
            Object.Destroy(root);
            yield return null;
            Assert.IsTrue(root == null, "The scene has already destroyed the projectile renderers");

            attack.enabled = false;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        public enum Response { Stand, Cover, Interrupt }
        private static readonly Response[] Responses = { Response.Stand, Response.Cover, Response.Interrupt };

        [UnityTest]
        public IEnumerator VolleyResolvesOnIvanOrCoverAndCleansUp([ValueSource(nameof(Responses))] Response response)
        {
            attack.transform.SetPositionAndRotation(new Vector3(0f, attack.transform.position.y, 0f),
                Quaternion.LookRotation(Vector3.back));
            // The southern column is at z=-10. Put Ivan on either side along the same line.
            PlacePlayer(0f, response == Response.Cover ? -12.5f : -7f);
            float initialHP = (float)hp.GetValue(health);
            QueueArrows();
            float deadline = Time.time + .5f;
            while (!(bool)firing.GetValue(attack) && Time.time < deadline) yield return null;
            Assert.IsTrue((bool)firing.GetValue(attack));
            var arrows = FindArrows();
            Assert.IsTrue(arrows.All(a => a != null && a.gameObject.activeInHierarchy),
                "All six spears should be visible together on the first cast frame");
            Assert.IsTrue(arrows.All(a => a.position.y > player.bounds.max.y),
                "The waiting volley should form above the characters");
            var camera = arena.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>())
                .Single(view => view.isActiveAndEnabled && view.CompareTag("MainCamera"));
            var lengthsAtSpawn = arrows.Select(VisibleLength).ToArray();
            Assert.IsTrue(lengthsAtSpawn.All(length => length > 1f), "Each spear must be visible at full length");
            for (int i = 0; i < arrows.Length; i++)
                AssertPointInGameplayView(camera, arrows[i], lengthsAtSpawn[i]);

            // Reframe while all six shots are waiting, then return to the original hit/cover lane.
            Vector3 start = player.transform.position;
            Vector3 cameraStart = camera.transform.position;
            PlacePlayer(start.x + 1.4f, start.z);
            yield return new WaitForSeconds(.35f);
            Assert.Greater(Vector3.Distance(cameraStart, camera.transform.position), .5f);
            for (int i = 0; i < arrows.Length; i++)
            {
                Assert.That(VisibleLength(arrows[i]), Is.EqualTo(lengthsAtSpawn[i]).Within(.02f),
                    "A waiting spear must have its full length from the first frame");
                AssertPointInGameplayView(camera, arrows[i], lengthsAtSpawn[i]);
            }
            PlacePlayer(start.x, start.z);
            Assert.AreEqual(initialHP, hp.GetValue(health));

            if (response == Response.Interrupt)
            {
                // Interrupt on the first moving frame, before the fast shot reaches Ivan.
                Vector3 rackPosition = arrows[0].position;
                deadline = Time.time + 1.2f;
                while (Vector3.Distance(arrows[0].position, rackPosition) < .05f && Time.time < deadline)
                    yield return null;
                Assert.IsTrue(arrows[0].gameObject.activeInHierarchy);
                Assert.Greater(Vector3.Distance(arrows[0].position, rackPosition), .05f);
                attack.enabled = false;
                Assert.IsFalse(arrows.Any(a => a.gameObject.activeInHierarchy));
                yield return new WaitForSeconds(3f);
                Assert.AreEqual(initialHP, hp.GetValue(health));
                yield break;
            }

            deadline = Time.time + 8f;
            while ((bool)firing.GetValue(attack) && Time.time < deadline) yield return null;
            Assert.IsFalse((bool)firing.GetValue(attack));
            Assert.IsFalse(arrows.Any(a => a.gameObject.activeInHierarchy));
            Assert.AreEqual(initialHP - (response == Response.Stand ? 35f : 0f), hp.GetValue(health));
            Assert.AreEqual("HeadDash", next.GetValue(attack).ToString());
        }

        [UnityTest]
        public IEnumerator SixSeparateLaunchesLockTheirOwnAimAndCanAllBeDodged()
        {
            attack.transform.SetPositionAndRotation(new Vector3(-7f, attack.transform.position.y, 2f),
                Quaternion.LookRotation(Vector3.right));
            float targetZ = -3.5f;
            PlacePlayer(7f, targetZ);
            float initialHP = (float)hp.GetValue(health);
            QueueArrows();
            yield return new WaitForSeconds(.65f);
            var arrows = FindArrows();
            var previous = arrows.Select(a => a.position).ToArray();
            var headings = new Vector3[arrows.Length];
            var origins = (Vector3[])previous.Clone();
            var launchSpeeds = new float[arrows.Length];
            float lastLaunch = 0f;
            int launches = 0;
            float deadline = Time.time + 8f;
            while ((bool)firing.GetValue(attack) && Time.time < deadline)
            {
                yield return null;
                for (int i = 0; i < arrows.Length; i++)
                {
                    Transform arrow = arrows[i];
                    if (!arrow.gameObject.activeInHierarchy) continue;
                    Vector3 step = arrow.position - previous[i];
                    previous[i] = arrow.position;
                    if (headings[i].sqrMagnitude > 0f)
                    {
                        Assert.Less(Vector3.Angle(headings[i], arrow.forward), 1f,
                            "A flying arrow turned after Ivan evaded");
                        Assert.That(step.magnitude / Time.deltaTime, Is.EqualTo(launchSpeeds[i]).Within(1.5f));
                        continue;
                    }

                    // The rack hovers in place. The first substantial step is the projectile release.
                    if (Vector3.Dot(step, arrow.forward) < 10f * Time.deltaTime) continue;
                    Assert.Less(Vector3.Angle(arrow.forward, player.bounds.center - arrow.position), 4f,
                        "Each new release must aim at Ivan's updated position");
                    if (launches > 0)
                        Assert.That(Time.time - lastLaunch, Is.EqualTo(.32f).Within(.06f));
                    lastLaunch = Time.time;
                    headings[i] = arrow.forward;
                    launchSpeeds[i] = Vector3.Distance(origins[i], player.bounds.center) / .10f;
                    launches++;
                    targetZ += 1.35f;
                    PlacePlayer(7f, targetZ);
                }
            }
            Assert.AreEqual(6, launches);
            Assert.IsFalse((bool)firing.GetValue(attack));
            Assert.AreEqual(initialHP, hp.GetValue(health));
        }

        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            Time.captureDeltaTime = previousStep;
            if (arena.IsValid() && arena.isLoaded) yield return SceneManager.UnloadSceneAsync(arena);
        }
    }
}
