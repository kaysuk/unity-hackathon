using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class TempleArchiveInputTests
    {
        private Scene scene;
        private float originalTimeScale;

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = originalTimeScale;
        }

        [UnitySetUp]
        public IEnumerator LoadTemple()
        {
            originalTimeScale = Time.timeScale;
            yield return SceneManager.LoadSceneAsync("temple_v2", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("temple_v2");
            yield return null;
        }

        private MonoBehaviour[] SceneComponents() => scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>()).ToArray();

        [UnityTest]
        public IEnumerator ArchiveLeverShowsTheBoundKeyAndChangesDirectionOnPress()
        {
            var objects = SceneComponents();
            var player = objects.Single(c => c.GetType().Name == "PlayerInteractor");
            var lever = objects.First(c => c.GetType().Name == "TempleArchiveLever");
            var movement = (Behaviour)player.GetComponent("IvanLocomotion");
            var camera = (Behaviour)objects.Single(c => c.GetType().Name == "IvanOrbitCamera");
            var body = player.GetComponent<CharacterController>();
            bool movementEnabled = movement.enabled;
            bool cameraEnabled = camera.enabled;
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior;
            var editorInput = settings.editorInputBehaviorInPlayMode;
            var action = InputSystem.actions.FindAction("Player/Interact", true);
            bool actionEnabled = action.enabled;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                movement.enabled = false;
                camera.enabled = false;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                action.Enable();
                InputSystem.EnableDevice(keyboard);

                var handle = lever.GetComponentInChildren<BoxCollider>();
                void AimFrom(float z)
                {
                    var feet = new Vector3(handle.bounds.center.x, .02f, z);
                    body.enabled = false;
                    player.transform.position = feet;
                    body.enabled = true;
                    camera.transform.position = feet + new Vector3(.16f, 1.5f, -.65f);
                    camera.transform.LookAt(handle.bounds.center);
                    Physics.SyncTransforms();
                }

                // Batch mode has no captured mouse; poll the normal gameplay action and raycasts.
                var update = player.GetType().GetMethod("UpdateTargetInReach", BindingFlags.Instance | BindingFlags.NonPublic);
                void Poll(KeyboardState state)
                {
                    InputSystem.QueueStateEvent(keyboard, state);
                    InputSystem.Update();
                    update.Invoke(player, null);
                }

                AimFrom(7.25f);
                Poll(new KeyboardState());
                var label = SceneComponents().SingleOrDefault(c => c.name == "Binding Label" && c.GetType().Name == "Text");
                Assert.That(label, Is.Not.Null, "Aiming at the lever must show its prompt");
                Assert.That(label.isActiveAndEnabled, Is.True);
                string text = Read<string>(label, "text");
                int binding = action.GetBindingIndexForControl(keyboard.eKey);
                string keyName = action.GetBindingDisplayString(binding, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                Assert.That(text, Does.Contain(keyName));
                Assert.That(text, Does.Contain("Опустить рычаг"));

                Assert.IsFalse(Read<bool>(lever, "IsDown"));
                Poll(new KeyboardState(Key.E));
                Assert.That(action.WasPressedThisDynamicUpdate(), Is.True);
                Assert.IsTrue(Read<bool>(lever, "IsDown"));
                Assert.That(Read<string>(label, "text"), Does.Contain("Поднять рычаг"));
                yield return null;

                AimFrom(3f);
                Poll(new KeyboardState());
                Assert.That(label.isActiveAndEnabled, Is.False, "The prompt disappears beyond the player's reach");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (!actionEnabled) action.Disable();
                settings.backgroundBehavior = background;
                settings.editorInputBehaviorInPlayMode = editorInput;
                camera.enabled = cameraEnabled;
                movement.enabled = movementEnabled;
            }
        }

        private static T Read<T>(Component target, string property) =>
            (T)target.GetType().GetProperty(property).GetValue(target);
    }
}
