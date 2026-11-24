using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class SurfaceSoundTests
    {
        private static readonly Type resolver = Type.GetType("PropSounds, Assembly-CSharp", true);
        private static string FirstStep(Collider floor, Vector3 contact) =>
            resolver.GetMethod("FirstStep", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { floor, contact }).ToString();

        [UnityTearDown]
        public IEnumerator UnloadChurch()
        {
            var level = SceneManager.GetSceneByName("Church_Interior");
            if (level.isLoaded) yield return SceneManager.UnloadSceneAsync(level);
        }

        [UnityTest]
        public IEnumerator ChurchRunnersSoftenTheFloorBeneathEachMesh()
        {
            var original = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync("Church_Interior", LoadSceneMode.Additive);
            var level = SceneManager.GetSceneByName("Church_Interior");
            try
            {
                SceneManager.SetActiveScene(level);
                var runners = level.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshRenderer>())
                    .Where(renderer => renderer.GetComponents<MonoBehaviour>().Any(c => c.GetType().Name == "FootstepCarpet"))
                    .ToArray();
                Assert.That(runners.Length, Is.EqualTo(3));
                Physics.SyncTransforms();
                foreach (var runner in runners)
                {
                    Vector3 point = runner.bounds.center;
                    var hit = Physics.RaycastAll(point + Vector3.up * 0.25f, Vector3.down, 0.52f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        .Where(h => h.collider.gameObject.scene == level).OrderBy(h => h.distance).First();
                    Assert.That(FirstStep(hit.collider, hit.point), Is.EqualTo("CarpetStep1"), runner.name);
                    Vector3 outside = hit.point + Vector3.right * (runner.bounds.extents.x + 0.5f);
                    Assert.That(FirstStep(hit.collider, outside), Is.Not.EqualTo("CarpetStep1"), runner.name);
                }
            }
            finally { SceneManager.SetActiveScene(original); }
        }

        [TestCase("Iron meeting stile", "MetalStep1", "MetalImpact")]
        [TestCase("Vertical planks", "WoodStep1", "WoodImpact")]
        [TestCase("Walkable Grassland", "EarthLeft1", "EarthImpact")]
        [TestCase("Stone gate jamb", "StoneLeft1", "WallHit")]
        public void SurfaceSelectsFootfallsAndHits(string colliderName, string step, string impact)
        {
            var surface = new GameObject(colliderName);
            try
            {
                var collider = surface.AddComponent<BoxCollider>();
                Assert.That(FirstStep(collider, Vector3.zero), Is.EqualTo(step));
                Assert.That(resolver.GetMethod("Impact", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { collider }).ToString(), Is.EqualTo(impact));
            }
            finally { UnityEngine.Object.DestroyImmediate(surface); }
        }
    }
}
