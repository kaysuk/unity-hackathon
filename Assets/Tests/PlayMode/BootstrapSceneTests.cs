using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public class BootstrapSceneTests
    {
        private Scene loadedScene;

        [UnityTest]
        public IEnumerator FirstBuildSceneLoadsWithCameraAndNoMissingScripts()
        {
            Assert.That(SceneManager.sceneCountInBuildSettings, Is.GreaterThan(0));
            var path = SceneUtility.GetScenePathByBuildIndex(0);
            var operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            Assert.That(operation, Is.Not.Null);
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!operation.isDone && Time.realtimeSinceStartup < deadline)
                yield return null;
            loadedScene = SceneManager.GetSceneByPath(path);
            Assert.That(operation.isDone, Is.True, "Scene loading exceeded 30 seconds.");
            Assert.That(loadedScene.isLoaded, Is.True);
            yield return null; // Allow Start methods to execute.

            var roots = loadedScene.GetRootGameObjects();
            var cameras = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true));
            Assert.That(cameras.Any(camera => camera.isActiveAndEnabled), Is.True,
                "The first build scene must provide an active camera.");

            foreach (var root in roots)
            {
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    Assert.That(component == null, Is.False, $"Missing script under {root.name}");
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator UnloadTestScene()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
