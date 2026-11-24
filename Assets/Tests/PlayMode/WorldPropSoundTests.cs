using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class WorldPropSoundTests
    {
        private Scene original;
        private Scene level;
        private float originalScale;
        private Camera view;
        private Vector3 cameraPosition;
        private MonoBehaviour[] cameraControllers;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            original = SceneManager.GetActiveScene();
            originalScale = Time.timeScale;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (view != null)
            {
                view.transform.position = cameraPosition;
                foreach (var controller in cameraControllers) controller.enabled = true;
            }
            if (original.isLoaded) SceneManager.SetActiveScene(original);
            if (level.isLoaded) yield return SceneManager.UnloadSceneAsync(level);
            Time.timeScale = originalScale;
        }

        private IEnumerator Enter(string sceneName)
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            level = SceneManager.GetSceneByName(sceneName);
            SceneManager.SetActiveScene(level);
            yield return null;
            view = Camera.main;
            cameraPosition = view.transform.position;
            cameraControllers = view.GetComponents<MonoBehaviour>().Where(item => item.enabled).ToArray();
            foreach (var controller in cameraControllers) controller.enabled = false;
        }

        private AudioSource[] Props() => level.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>())
            .Where(item => item != null && item.GetType().Name == "WorldPropSound")
            .Select(item => item.GetComponent<AudioSource>()).ToArray();

        [UnityTest]
        public IEnumerator FountainOnlyPlaysWithinRangeAndDuringGameplay()
        {
            yield return Enter("Church_Exterior");
            var stream = Props().Single();
            Assert.That(stream.clip.name, Is.EqualTo("FountainStream"));
            Assert.That(stream.outputAudioMixerGroup.name, Is.EqualTo("Effects"));
            Assert.That(stream.spatialBlend, Is.EqualTo(1f));
            Assert.That(stream.loop, Is.True);
            view.transform.position = stream.transform.position;
            yield return null;
            Assert.That(stream.isPlaying, Is.True);
            Time.timeScale = 0f;
            yield return null;
            Assert.That(stream.isPlaying, Is.False);
            Time.timeScale = 1f;
            yield return null;
            Assert.That(stream.isPlaying, Is.True);
            view.transform.position += Vector3.right * (stream.maxDistance + 1f);
            yield return null;
            Assert.That(stream.isPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator CellarDropsStayBelowTheChurchFloor()
        {
            yield return Enter("Church_Interior");
            var bucket = Props().Single();
            Assert.That(bucket.loop, Is.False);
            view.transform.position = bucket.transform.position + Vector3.up;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!bucket.isPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(bucket.isPlaying, Is.True);
            Assert.That(bucket.clip.name, Does.StartWith("CellarDrip"));
            view.transform.position = bucket.transform.position + Vector3.up * 3.8f;
            Assert.That(3.8f, Is.LessThan(bucket.maxDistance));
            yield return null;
            Assert.That(bucket.isPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator ArchiveFiresAreLocalToTheirFixtures()
        {
            yield return Enter("temple_v2");
            var fires = Props();
            Assert.That(fires.Count(f => f.clip.name == "TorchFlame"), Is.EqualTo(5));
            Assert.That(fires.Count(f => f.clip.name == "BrazierCoals"), Is.EqualTo(4));
            var coal = fires.First(f => f.clip.name == "BrazierCoals");
            view.transform.position = coal.transform.position + Vector3.up * 0.4f;
            yield return null;
            Assert.That(coal.isPlaying, Is.True);
            Assert.That(fires.Count(f => f.isPlaying), Is.EqualTo(1));
            var west = fires.Where(f => f.clip.name == "TorchFlame" && f.transform.position.z > 10f)
                .OrderBy(f => f.transform.position.x).First();
            view.transform.position = west.transform.position;
            yield return null;
            Assert.That(west.isPlaying, Is.True);
            view.transform.position += Vector3.left * 1.8f;
            Assert.That(1.8f, Is.LessThan(west.maxDistance));
            yield return null;
            Assert.That(west.isPlaying, Is.False);
        }
    }
}
