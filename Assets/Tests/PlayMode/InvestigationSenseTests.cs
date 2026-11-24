using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Hackathon.Tests
{
    public sealed class InvestigationSenseTests
    {
        [Test]
        public void InvestigateStaysPressedUntilKeyboardOrGamepadButtonIsReleased()
        {
            var action = InputSystem.actions.FindAction("Player/Investigate", true);
            Assert.That(action.type, Is.EqualTo(InputActionType.Button));
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior;
            var routing = settings.editorInputBehaviorInPlayMode;
            bool wasEnabled = action.enabled;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.EnableDevice(keyboard);
                InputSystem.EnableDevice(pad);
                action.Enable();

                void Poll(bool expected)
                {
                    InputSystem.Update();
                    Assert.That(action.IsPressed(), Is.EqualTo(expected));
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                Poll(false);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.V));
                Poll(true);
                Poll(true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Poll(false);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
                Poll(true);
                Poll(true);
                InputSystem.QueueStateEvent(pad, new GamepadState());
                Poll(false);
            }
            finally
            {
                if (!wasEnabled) action.Disable();
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(pad);
                settings.editorInputBehaviorInPlayMode = routing;
                settings.backgroundBehavior = background;
            }
        }


        [Test]
        public void SenseIncludesEveryUsableNearbyItemAndIgnoresCollectedOnes()
        {
            const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
            System.Type Script(string name) => System.Type.GetType(name + ", Assembly-CSharp", true);
            var root = new GameObject("Sense test objects");
            root.transform.position = Vector3.up * 500f;
            try
            {
                var ivan = new GameObject("Ivan", typeof(CharacterController));
                ivan.transform.SetParent(root.transform, false);
                ivan.SetActive(false);
                var camera = new GameObject("View", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.enabled = false;
                var inventory = ivan.AddComponent(Script("PlayerInventory"));
                var interactor = ivan.AddComponent(Script("PlayerInteractor"));
                interactor.GetType().GetField("viewCamera", fields).SetValue(interactor, camera);
                var sense = ivan.AddComponent(Script("PlayerInvestigationSense"));
                ivan.SetActive(true);

                Component Pickup(string id, Vector3 offset)
                {
                    var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    prop.transform.SetParent(root.transform, false);
                    prop.transform.localPosition = offset;
                    var pickup = prop.AddComponent(Script("ItemPickup"));
                    pickup.GetType().GetField("itemId", fields).SetValue(pickup, id);
                    return pickup;
                }

                var front = Pickup("front_key", Vector3.forward * 3f);
                var behind = Pickup("back_key", Vector3.back * 4f);
                var far = Pickup("distant_key", Vector3.right * 30f);
                Physics.SyncTransforms();
                var collect = sense.GetType().GetMethod("CollectNearby", fields);
                var nearby = (IList)sense.GetType().GetField("nearby", fields).GetValue(sense);
                collect.Invoke(sense, new object[] { interactor });
                CollectionAssert.AreEquivalent(new[] { front, behind }, nearby);
                CollectionAssert.DoesNotContain(nearby, far);

                inventory.GetType().GetMethod("TryCollect").Invoke(inventory, new object[] { "back_key" });
                collect.Invoke(sense, new object[] { interactor });
                CollectionAssert.AreEqual(new[] { front }, nearby);
                front.gameObject.SetActive(false);
                collect.Invoke(sense, new object[] { interactor });
                Assert.That(nearby, Is.Empty);
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
