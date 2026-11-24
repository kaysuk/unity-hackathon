#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hackathon.Tests
{
    public sealed class IvanRegenerationTests
    {
        private const string IvanPrefab = "Assets/Character/Ivan_Karamazov/Ivan_Karamazov.prefab";
        private const float TestTimeScale = 3f;
        private Scene scene;
        private Scene arena;
        private GameObject ivan;
        private Component health;
        private float previousTimeScale;
        private float previousMaximumDelta;

        [UnitySetUp]
        public IEnumerator SpawnIvan()
        {
            previousTimeScale = Time.timeScale;
            previousMaximumDelta = Time.maximumDeltaTime;
            Time.timeScale = TestTimeScale;
            Time.maximumDeltaTime = .1f;
            scene = SceneManager.CreateScene("Ivan regeneration test");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IvanPrefab);
            Assert.That(prefab, Is.Not.Null);
            ivan = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(ivan, scene);
            health = ivan.GetComponent("CombatHealth");
            Assert.That(health, Is.Not.Null);
            yield return null;
            Assert.That(GetHP(health), Is.EqualTo(100f));
        }

        [UnityTearDown]
        public IEnumerator UnloadTestScene()
        {
            Time.timeScale = previousTimeScale;
            Time.maximumDeltaTime = previousMaximumDelta;
            if (arena.IsValid() && arena.isLoaded)
                yield return SceneManager.UnloadSceneAsync(arena);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ANewHitRestartsTheDelayAndBothStagesPauseWithGameTime()
        {
            TakeDamage(health, 12f);
            double firstHitAt = Time.timeAsDouble;
            yield return new WaitUntil(() => Time.timeAsDouble >= firstHitAt + 4.2d);
            Assert.That(GetHP(health), Is.EqualTo(88f));

            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(GetHP(health), Is.EqualTo(88f), "The delay must pause.");
            Time.timeScale = TestTimeScale;
            yield return ExpectNoHealingThrough(firstHitAt + 4.95d, 88f);
            yield return new WaitUntil(() => Time.timeAsDouble >= firstHitAt + 5.6d);
            float healed = GetHP(health);
            double healingTime = Time.timeAsDouble - firstHitAt - 5d;
            Assert.That(healed, Is.EqualTo(88f + 5f * (float)healingTime).Within(.55f));

            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(GetHP(health), Is.EqualTo(healed), "Active healing must pause.");
            Time.timeScale = TestTimeScale;

            TakeDamage(health, 6f);
            double secondHitAt = Time.timeAsDouble;
            float woundedAgain = GetHP(health);
            Assert.That(woundedAgain, Is.EqualTo(healed - 6f).Within(.001f));
            yield return ExpectNoHealingThrough(secondHitAt + 4.95d, woundedAgain);
            yield return new WaitUntil(() => Time.timeAsDouble >= secondHitAt + 5.6d);
            healingTime = Time.timeAsDouble - secondHitAt - 5d;
            Assert.That(GetHP(health), Is.EqualTo(woundedAgain + 5f * (float)healingTime).Within(.55f));
        }

        [UnityTest]
        public IEnumerator WoundedIvanUsesTheArenaRecoveryRulesAfterStoryEntry()
        {
            arena = SceneManager.CreateScene("Temple_Final_Room");
            TakeDamage(health, 20f);
            double hitAt = Time.timeAsDouble;
            yield return new WaitForSeconds(3f);
            SceneManager.MoveGameObjectToScene(ivan, arena);
            yield return ExpectNoHealingThrough(hitAt + 11.9d, 80f);
            yield return new WaitUntil(() => Time.timeAsDouble >= hitAt + 14d);
            Assert.That(GetHP(health), Is.EqualTo(80f + (float)(Time.timeAsDouble - hitAt - 12d)).Within(.15f));

            TakeDamage(health, 5f);
            float wounded = GetHP(health);
            yield return new WaitForSeconds(6f);
            Assert.That(GetHP(health), Is.EqualTo(wounded), "Another hit resets the longer arena delay.");

            SceneManager.MoveGameObjectToScene(ivan, scene);
            double leftAt = Time.timeAsDouble;
            yield return new WaitUntil(() => Time.timeAsDouble >= leftAt + 1d);
            Assert.That(GetHP(health), Is.EqualTo(wounded + 5f * (float)(Time.timeAsDouble - leftAt)).Within(.55f));
        }

        [UnityTest]
        public IEnumerator BothBloodOverlaysFadeAsLivingIvanHealsAndClearAtFullHealth()
        {
            var hud = new GameObject("HUD under test", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(hud, scene);
            var bindingType = Type.GetType("GameHudBinding, Assembly-CSharp", true);
            var binding = hud.AddComponent(bindingType);
            bindingType.GetMethod("Bind", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(binding, new object[] { ivan });
            var rim = hud.transform.Find("Blood rim").GetComponent<RawImage>();
            var spatter = hud.transform.Find("Blood spatter").GetComponent<RawImage>();

            // A default CombatHealth actor shares damage handling with Ivan.
            var other = new GameObject("Other damageable actor");
            SceneManager.MoveGameObjectToScene(other, scene);
            var otherHealth = other.AddComponent(health.GetType());
            TakeDamage(otherHealth, 25f);
            TakeDamage(health, 16f);
            var overlay = scene.GetRootGameObjects().Single(root => root.name == "Ivan Damage Overlay");
            var canvas = overlay.GetComponent<Canvas>();
            var frame = overlay.transform.Find("Blood Frame").GetComponent<RawImage>();
            var layers = new[] { rim, spatter, frame };
            Assert.That(canvas.enabled, Is.True);
            Assert.That(frame.material.shader.name, Is.EqualTo("UI/Ivan Blood Frame"));

            var damageResponse = (Behaviour)ivan.GetComponent("IvanDamageResponse");
            damageResponse.enabled = false;
            Assert.That(canvas.enabled, Is.False);
            damageResponse.enabled = true;
            Assert.That(canvas.enabled, Is.True);

            yield return WaitForHP(94f, 8d);
            float[] woundedOpacity = layers.Select(layer => layer.color.a).ToArray();
            yield return WaitForHP(99f, 2d);
            Assert.That(GetHP(health), Is.LessThan(100f), "Sample the final point of health before the cap.");
            Assert.That(canvas.enabled, Is.True);
            for (int i = 0; i < layers.Length; i++)
                Assert.That(layers[i].color.a, Is.GreaterThan(0f).And.LessThan(woundedOpacity[i]), layers[i].name);

            yield return WaitForHP(100f, 1d);
            Assert.That(GetHP(health), Is.EqualTo(100f));
            foreach (RawImage layer in layers)
                Assert.That(layer.color.a, Is.Zero, layer.name);
            Assert.That(rim.enabled, Is.False);
            Assert.That(spatter.enabled, Is.False);
            Assert.That(canvas.enabled, Is.False);
            Assert.That(GetHP(otherHealth), Is.EqualTo(75f));

            TakeDamage(health, 100f);
            double deathAt = Time.timeAsDouble;
            yield return new WaitUntil(() => Time.timeAsDouble >= deathAt + 6d);
            Assert.That(GetHP(health), Is.Zero, "Regeneration must not revive Ivan.");
            Assert.That(GetHP(otherHealth), Is.EqualTo(75f));
        }

        [UnityTest]
        public IEnumerator SpatterFollowsTheAttackersScreenSideAndClearsWhenHealed()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Test camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.tag = "MainCamera";
                camera = cameraObject.GetComponent<Camera>();
            }

            var hud = new GameObject("Directional HUD", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(hud, scene);
            var bindingType = Type.GetType("GameHudBinding, Assembly-CSharp", true);
            var binding = hud.AddComponent(bindingType);
            bindingType.GetMethod("Bind", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(binding, new object[] { ivan });
            RawImage left = hud.transform.Find("Blood spatter").GetComponent<RawImage>();
            RawImage right = hud.transform.Find("Blood spatter right").GetComponent<RawImage>();
            var attacker = new GameObject("Attacker");
            SceneManager.MoveGameObjectToScene(attacker, scene);

            void HitFromScreenSide(float offset, float damage)
            {
                var eye = camera.transform;
                Vector3 position = eye.position;
                Quaternion rotation = eye.rotation;
                try
                {
                    // World left is screen right in this view.
                    eye.rotation = Quaternion.Euler(0f, 180f, 0f);
                    eye.position = ivan.transform.position - 3f * eye.forward;
                    attacker.transform.position = ivan.transform.position + offset * eye.right;
                    TakeDamage(health, damage, attacker);
                }
                finally
                {
                    eye.SetPositionAndRotation(position, rotation);
                }
            }

            HitFromScreenSide(4f, 8f);
            Assert.That(right.color.a, Is.GreaterThan(left.color.a), "The first impact is on camera right.");
            Assert.That(right.enabled, Is.True);
            yield return new WaitForSeconds(1.6f);
            HitFromScreenSide(-4f, 12f);
            Assert.That(left.color.a, Is.GreaterThan(right.color.a), "The new impact is on camera left.");
            Assert.That(right.color.a, Is.GreaterThan(0f), "The earlier stain survives the next hit.");
            yield return WaitForHP(100f, 10d);
            foreach (RawImage side in new[] { left, right })
            {
                Assert.That(side.color.a, Is.Zero, side.name);
                Assert.That(side.enabled, Is.False, side.name);
            }
        }

        private IEnumerator ExpectNoHealingThrough(double until, float expectedHP)
        {
            while (Time.timeAsDouble < until)
            {
                Assert.That(GetHP(health), Is.EqualTo(expectedHP));
                yield return null;
            }
        }

        private IEnumerator WaitForHP(float expectedHP, double timeLimit)
        {
            double deadline = Time.timeAsDouble + timeLimit;
            yield return new WaitUntil(() => GetHP(health) >= expectedHP || Time.timeAsDouble >= deadline);
            Assert.That(GetHP(health), Is.GreaterThanOrEqualTo(expectedHP));
        }

        private static float GetHP(Component target) =>
            (float)target.GetType().GetProperty("HP").GetValue(target);

        private static void TakeDamage(Component target, float amount, GameObject source = null) =>
            target.GetType().GetMethod("TakeDamage").Invoke(target,
                new object[] { amount, target.transform.position, Vector3.forward, source });
    }
}
#endif
