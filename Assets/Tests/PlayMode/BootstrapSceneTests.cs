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
        public IEnumerator MovementRecoversAfterDisablingDuringTurns()
        {
            var path = SceneUtility.GetScenePathByBuildIndex(0);
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(path);
            yield return null;

            var movement = loadedScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>())
                .Single(component => component.GetType().Name == "IvanMovement");
            var animator = movement.GetComponent<Animator>();
            int locomotion = Animator.StringToHash("Base Layer.Locomotion");
            foreach (string turn in new[] { "Turn Left", "Turn Right", "Turn Back" })
            {
                foreach (bool incomingBlend in new[] { false, true })
                {
                    animator.SetBool("AimHeld", false);
                    animator.Play(locomotion, 0, 0f);
                    animator.Update(0f);
                    int turnId = Animator.StringToHash("Base Layer." + turn);
                    if (incomingBlend)
                        animator.CrossFadeInFixedTime(turnId, 0.1f, 0);
                    else
                        animator.Play(turnId, 0, 0.5f);
                    animator.Update(0.01f);
                    Assert.That(incomingBlend
                        ? animator.GetNextAnimatorStateInfo(0).fullPathHash
                        : animator.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(turnId));

                    movement.enabled = false;
                    movement.enabled = true;
                    yield return null;
                    yield return null;
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                        Is.EqualTo(locomotion), $"Recovery failed: {turn}, blend={incomingBlend}");
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CombatAndCrouchRegressionChecks()
        {
            var path = SceneUtility.GetScenePathByBuildIndex(0);
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(path);
            yield return null;
            var components = loadedScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>()).ToArray();
            var movement = components.Single(c => c.GetType().Name == "IvanMovement");
            var combat = components.Single(c => c.GetType().Name == "PistolCombat");
            var cameraControl = components.Single(c => c.GetType().Name == "ShoulderCamera");
            var health = movement.GetComponent("CombatHealth");
            var controller = movement.GetComponent<CharacterController>();
            var animator = movement.GetComponent<Animator>();
            Assert.That(animator.parameters.Any(p => p.name == "IsReload" &&
                p.type == AnimatorControllerParameterType.Bool), Is.True);
            Assert.That(combat.GetType().GetProperty("Ammo").GetValue(combat), Is.EqualTo(8));
            Assert.That(combat.GetType().GetProperty("ReserveAmmo").GetValue(combat), Is.EqualTo(16));

            cameraControl.enabled = false;
            var camera = cameraControl.GetComponent<Camera>();
            camera.fieldOfView = 63f;
            cameraControl.GetType().GetMethod("GetAimRay").Invoke(cameraControl, null);
            Assert.That(camera.fieldOfView, Is.EqualTo(63f));
            cameraControl.enabled = true;

            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var ammoField = combat.GetType().GetField("<Ammo>k__BackingField", flags);
            var reserveField = combat.GetType().GetField("<ReserveAmmo>k__BackingField", flags);
            var reload = combat.GetType().GetMethod("CompleteReload", flags);
            ammoField.SetValue(combat, 5);
            reload.Invoke(combat, null);
            Assert.That(ammoField.GetValue(combat), Is.EqualTo(8));
            Assert.That(reserveField.GetValue(combat), Is.EqualTo(13));
            ammoField.SetValue(combat, 2);
            reserveField.SetValue(combat, 3);
            combat.GetType().GetMethod("CancelReload").Invoke(combat, null);
            Assert.That(ammoField.GetValue(combat), Is.EqualTo(2));
            Assert.That(reserveField.GetValue(combat), Is.EqualTo(3));
            reload.Invoke(combat, null);
            Assert.That(ammoField.GetValue(combat), Is.EqualTo(5));
            Assert.That(reserveField.GetValue(combat), Is.EqualTo(0));
            reload.Invoke(combat, null);
            Assert.That(ammoField.GetValue(combat), Is.EqualTo(5));
            var crouch = movement.GetType().GetMethod("SetCrouching", flags);
            float height = controller.height;
            float bottom = controller.center.y - height * 0.5f;
            crouch.Invoke(movement, new object[] { true });
            Assert.That(controller.height, Is.LessThan(height));
            Assert.That(controller.center.y - controller.height * 0.5f, Is.EqualTo(bottom).Within(0.001f));
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(ceiling, loadedScene);
            ceiling.transform.position = movement.transform.position + Vector3.up * (height - 0.1f);
            ceiling.transform.localScale = new Vector3(2f, 0.1f, 2f);
            Physics.SyncTransforms();
            crouch.Invoke(movement, new object[] { false });
            Assert.That(controller.height, Is.LessThan(height), "Cannot stand through a ceiling");
            ceiling.SetActive(false);
            Physics.SyncTransforms();
            crouch.Invoke(movement, new object[] { false });
            Assert.That(controller.height, Is.EqualTo(height));

            var muzzle = (Transform)combat.GetType().GetField("muzzle", flags).GetValue(combat);
            ceiling.SetActive(true);
            ceiling.transform.position = muzzle.position;
            ceiling.transform.localScale = Vector3.one * 0.2f;
            Physics.SyncTransforms();
            Assert.That(combat.GetType().GetMethod("IsMuzzleObstructed", flags).Invoke(combat, null), Is.True);
            ceiling.SetActive(false);

            int deaths = 0;
            var deathEvent = (UnityEngine.Events.UnityEvent)health.GetType().GetField("onDeath").GetValue(health);
            deathEvent.AddListener(() => deaths++);
            var damage = health.GetType().GetMethod("TakeDamage");
            for (int i = 0; i < 5; i++)
                damage.Invoke(health, new object[] { 25f, Vector3.zero, Vector3.forward, null });
            Assert.That(health.GetType().GetProperty("HP").GetValue(health), Is.EqualTo(0f));
            Assert.That(deaths, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        private static readonly string[] RifleNpcNames = { "Ch35 Rifle NPC", "SWAT Rifle NPC" };

        [UnityTest]
        public IEnumerator RifleNpcShootsReactsAndStaysDead([ValueSource(nameof(RifleNpcNames))] string npcName)
        {
            var path = SceneUtility.GetScenePathByBuildIndex(0);
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(path);
            yield return null;

            var roots = loadedScene.GetRootGameObjects();
            var npc = roots.Single(root => root.name == npcName);
            foreach (var other in roots.Where(root => root != npc && root.GetComponent("RifleNpc") != null))
                other.SetActive(false);
            var player = roots.Single(root => root.GetComponent("IvanMovement") != null);
            var animator = npc.GetComponent<Animator>();
            Assert.That(animator.isHuman && animator.avatar.isValid, Is.True);
            Assert.That(npc.GetComponentInChildren<SkinnedMeshRenderer>(), Is.Not.Null);
            var health = npc.GetComponent("CombatHealth");
            var playerHealth = player.GetComponent("CombatHealth");
            var hp = health.GetType().GetProperty("HP");
            var hit = health.GetType().GetMethod("TakeDamage");
            var flash = npc.GetComponentsInChildren<Light>(true).Single();
            var start = npc.transform.position;
            float initialHP = (float)hp.GetValue(playerHealth);
            float deadline = Time.time + 5f;
            while (!flash.gameObject.activeSelf && Time.time < deadline) yield return null;
            Assert.That(flash.gameObject.activeSelf, Is.True, "Expected a visible muzzle flash");
            Assert.That((float)hp.GetValue(playerHealth), Is.LessThan(initialHP));
            var travelled = Vector3.ProjectOnPlane(npc.transform.position - start, Vector3.up);
            Assert.That(travelled.magnitude, Is.GreaterThan(0.2f));

            hit.Invoke(health, new object[] { 25f, npc.transform.position, Vector3.back, player });
            yield return new WaitForSeconds(0.25f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsTag("Hit"), Is.True);
            Assert.That(flash.gameObject.activeSelf, Is.False);

            hit.Invoke(health, new object[] { 100f, npc.transform.position, Vector3.back, player });
            float hpAtDeath = (float)hp.GetValue(playerHealth);
            yield return new WaitForSeconds(3.6f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Death"), Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(1f));
            Assert.That(npc.GetComponent<CharacterController>().enabled, Is.False);
            Assert.That(hp.GetValue(playerHealth), Is.EqualTo(hpAtDeath));
            LogAssert.NoUnexpectedReceived();
        }

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
            var player = roots.Single(root => root.CompareTag("Player"));
            Assert.That(player.GetComponent("IvanMovement"), Is.Not.Null);
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
