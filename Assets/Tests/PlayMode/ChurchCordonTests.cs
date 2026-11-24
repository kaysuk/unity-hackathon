using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class ChurchCordonTests
    {
        private const string Church = "Assets/Scenes/Levels/Church_Exterior/Church_Exterior.unity";
        private Scene scene, original;
        private Component cordon;
        private GameObject player;
        private CharacterController body;
        private float captureStep;
        private float timeScale;

        private T Read<T>(string property) =>
            (T)cordon.GetType().GetProperty(property).GetValue(cordon);

        private void Place(float x, float z)
        {
            body.enabled = false;
            player.transform.position = new Vector3(x, .08f, z);
            body.enabled = true;
            Physics.SyncTransforms();
        }

        [UnitySetUp]
        public IEnumerator EnterStreet()
        {
            original = SceneManager.GetActiveScene();
            captureStep = Time.captureDeltaTime;
            timeScale = Time.timeScale;
            Time.timeScale = 1;
            Time.captureDeltaTime = 1f / 30f;
            yield return SceneManager.LoadSceneAsync(Church, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(Church);
            player = scene.GetRootGameObjects().Single(root => root.CompareTag("Player"));
            body = player.GetComponent<CharacterController>();
            cordon = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>())
                .Single(component => component.GetType().Name == "ChurchCordon");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator LeaveStreet()
        {
            Time.captureDeltaTime = captureStep;
            Time.timeScale = timeScale;
            if (original.isLoaded) SceneManager.SetActiveScene(original);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator StreetAndReturningThroughGateDoNotAlertTheDetail()
        {
            float line = cordon.transform.position.z;
            Place(-11f, line - 1.6f);
            yield return null;
            Place(7f, line - 1.6f);
            yield return null;
            Assert.That(Read<int>("AlarmCount"), Is.Zero);

            // Approach the building from the side, then leave via the centre of the front gate.
            Place(8f, line + 3.2f);
            yield return null;
            Place(.1f, line + .9f);
            yield return null;
            for (int i = 0; i < 20; i++)
            {
                body.Move(Vector3.back * .10f);
                yield return null;
            }
            Assert.That(player.transform.position.z, Is.LessThan(line - .6f));
            Assert.That(Read<int>("AlarmCount"), Is.Zero);
            Assert.That(Read<bool>("IsAlert"), Is.False);
        }

        [UnityTest]
        public IEnumerator CrossingTheOpenGateAlertsAndDetainsIvanWithoutGunfire()
        {
            SceneManager.SetActiveScene(scene);
            var guards = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>())
                .Where(m => m.GetType().Name == "RifleNpc").ToArray();
            Assert.That(guards.Length, Is.GreaterThanOrEqualTo(10));
            Assert.That(guards.All(g => !g.enabled), Is.True);
            Assert.That(((Behaviour)player.GetComponent("PistolCombat")).enabled, Is.False);
            var origins = guards.Select(g => g.transform.position).ToArray();
            var audio = GameObject.Find("Game audio");
            bool HeardGuardStep() => audio.GetComponentsInChildren<AudioSource>().Any(source =>
                source.clip != null && source.isPlaying && source.spatialBlend == 1f && source.volume < .29f &&
                (source.clip.name.StartsWith("EarthLeft") || source.clip.name.StartsWith("EarthRight") ||
                 source.clip.name.StartsWith("StoneLeft") || source.clip.name.StartsWith("StoneRight") ||
                 source.clip.name.StartsWith("WoodStep") || source.clip.name.StartsWith("MetalStep")) &&
                guards.Any(g => Vector3.ProjectOnPlane(g.transform.position - source.transform.position, Vector3.up).sqrMagnitude < .36f));
            var hp = player.GetComponent("CombatHealth");
            float Health() => (float)hp.GetType().GetProperty("HP").GetValue(hp);
            float before = Health();
            float line = cordon.transform.position.z;
            Place(0, line - 1.15f);
            yield return null;
            Assert.That(Read<int>("AlarmCount"), Is.Zero);
            Assert.That(HeardGuardStep(), Is.False, "Officers standing at their posts must be silent.");
            bool heardSteps = false;
            for (int i = 0; i < 22; i++)
            {
                body.Move(Vector3.forward * .095f);
                yield return null;
                heardSteps |= HeardGuardStep();
            }
            Assert.That(player.transform.position.z, Is.GreaterThan(line + .28f), "Gate collision blocks the opening.");
            Assert.That(Read<bool>("IsAlert"), Is.True);
            Assert.That(Read<int>("AlarmCount"), Is.EqualTo(1));

            float waitUntil = Time.time + 7f;
            bool sawApproach = false;
            while (Read<int>("InterceptionCount") == 0 && Time.time < waitUntil)
            {
                heardSteps |= HeardGuardStep();
                sawApproach |= guards.Where((g,i) =>
                    Vector3.ProjectOnPlane(g.transform.position - origins[i], Vector3.up).sqrMagnitude > .15f).Any();
                yield return null;
            }
            Assert.That(sawApproach, Is.True, "The courtyard officers must leave their posts.");
            Assert.That(heardSteps, Is.True, "Moving officers must emit a quieter positional step from their feet.");
            Assert.That(Read<int>("InterceptionCount"), Is.EqualTo(1));
            Assert.That(player.transform.position.z, Is.LessThan(line - .7f));
            Assert.That(Health(), Is.EqualTo(before));
            Assert.That(Read<int>("AlarmCount"), Is.EqualTo(1));
            waitUntil = Time.time + 8f;
            while ((Read<bool>("IsReturning") || Read<bool>("IsAlert")) && Time.time < waitUntil)
                yield return null;
            Assert.That(Read<bool>("IsReturning") || Read<bool>("IsAlert"), Is.False);
            Place(0, line - .6f);
            yield return null;
            for (int i = 0; i < 14; i++)
            {
                body.Move(Vector3.forward * .09f);
                yield return null;
            }
            Assert.That(Read<int>("AlarmCount"), Is.EqualTo(2), "The cordon must rearm after an interception.");
        }
    }
}
