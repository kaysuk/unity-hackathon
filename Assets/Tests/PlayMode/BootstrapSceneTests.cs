using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public class BootstrapSceneTests
    {
        private Scene loadedScene;

        private const string FinalRoom = "Assets/Scenes/Levels/Temple_Final_Room.unity";
        private GameObject testTarget;

        [UnityTest]
        public IEnumerator IvanTurnsOnTheSpotAndReturnsToIdle()
        {
            yield return SceneManager.LoadSceneAsync(FinalRoom, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(FinalRoom);
            yield return null;
            var player = loadedScene.GetRootGameObjects().Single(root => root.CompareTag("Player"));
            var standing = player.GetComponent("IvanStanding");
            var animator = player.GetComponent<Animator>();
            Assert.That(standing, Is.Not.Null);
            Assert.That(animator.isHuman && animator.avatar.isValid, Is.True);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var face = standing.GetType().GetMethod("FaceDirection");
            var position = player.transform.position;
            foreach (float angle in new[] { -90f, 90f, -170f, 180f })
            {
                float start = player.transform.eulerAngles.y;
                float target = start + angle;
                face.Invoke(standing, new object[] { target });
                float deadline = Time.realtimeSinceStartup + 8f;
                do
                {
                    yield return null;
                    Assert.That(Vector3.Distance(player.transform.position, position), Is.LessThan(0.002f));
                } while ((Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, target)) > 0.5f ||
                    !animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle") ||
                    animator.IsInTransition(0)) && Time.realtimeSinceStartup < deadline);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, target)), Is.LessThan(0.5f));
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator IvanWalksRunsAndStopsInFinalRoom()
        {
            yield return SceneManager.LoadSceneAsync(FinalRoom, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(FinalRoom);
            yield return null;
            var player = loadedScene.GetRootGameObjects().Single(root => root.CompareTag("Player"));
            var animator = player.GetComponent<Animator>();
            using (var input = new MovementKeyboard())
            {
                Vector3 start = player.transform.position;
                input.Press(Key.W);
                yield return new WaitForSeconds(0.75f);
                float walk = animator.GetFloat("MoveSpeed");
                Assert.That(walk, Is.GreaterThan(0.5f));
                Assert.That(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == "Walk" && c.weight > 0.8f), Is.True);
                Assert.That(player.transform.position.z - start.z, Is.GreaterThan(0.30f));

                input.Press(Key.W, Key.LeftShift);
                yield return new WaitForSeconds(0.85f);
                Assert.That(animator.GetFloat("MoveSpeed"), Is.GreaterThan(walk * 1.5f));
                Assert.That(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == "Run" && c.weight > 0.8f), Is.True);
                input.Press();
                yield return new WaitForSeconds(1f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), Is.True);
                Vector3 stopped = player.transform.position;
                yield return new WaitForSeconds(0.2f);
                Assert.That(Vector3.Distance(stopped, player.transform.position), Is.LessThan(0.01f));
            }
        }

        private static readonly bool[] StartDuringTurn = { false, true };

        [UnityTest]
        public IEnumerator SprintSteersImmediately([ValueSource(nameof(StartDuringTurn))] bool interruptTurn)
        {
            yield return SceneManager.LoadSceneAsync(FinalRoom, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(FinalRoom);
            yield return null;
            var player = loadedScene.GetRootGameObjects().Single(root => root.CompareTag("Player"));
            var animator = player.GetComponent<Animator>();
            var standing = player.GetComponent("IvanStanding");
            var turning = standing.GetType().GetProperty("IsTurning");
            using (var input = new MovementKeyboard())
            {
                if (interruptTurn)
                {
                    input.Press(Key.D);
                    yield return null;
                    yield return null;
                    Assert.That((bool)turning.GetValue(standing), Is.True);
                }

                Vector3 start = player.transform.position;
                Key direction = interruptTurn ? Key.A : Key.D;
                float sign = interruptTurn ? -1f : 1f;
                input.Press(Key.LeftShift, direction);
                yield return new WaitForSeconds(0.15f);
                Assert.That((bool)turning.GetValue(standing), Is.False);
                Assert.That(Vector3.Distance(start, player.transform.position), Is.GreaterThan(0.025f));
                Assert.That(animator.GetFloat("RunTurn") * sign, Is.GreaterThan(0.08f));
                string clip = interruptTurn ? "Run Turn Left" : "Run Turn Right";
                Assert.That(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == clip && c.weight > 0.03f), Is.True);
                yield return new WaitForSeconds(0.55f);
                Assert.That((player.transform.position.x - start.x) * sign, Is.GreaterThan(0.45f));
                Assert.That(animator.GetFloat("MoveSpeed"), Is.GreaterThan(2.85f));

                // Reverse while already running and keep responding throughout the arc.
                input.Press(Key.LeftShift, interruptTurn ? Key.D : Key.A);
                yield return new WaitForSeconds(0.18f);
                Assert.That(animator.GetFloat("MoveSpeed"), Is.GreaterThan(1.4f));
                Assert.That(Mathf.Abs(animator.GetFloat("RunTurn")), Is.GreaterThan(0.4f));
                yield return new WaitForSeconds(0.45f);
                Assert.That(Vector3.Dot(player.transform.forward, -sign * Vector3.right), Is.GreaterThan(0.96f));
            }
        }

        [UnityTest]
        public IEnumerator CameraOrbitsWithMouseAndStickInFinalRoom()
        {
            yield return SceneManager.LoadSceneAsync(FinalRoom, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(FinalRoom);
            yield return null;
            var roots = loadedScene.GetRootGameObjects();
            var player = roots.Single(root => root.CompareTag("Player"));
            var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>())
                .Single(c => c.CompareTag("MainCamera")).transform;
            Quaternion originalFacing = player.transform.rotation;
            Vector3 originalPosition = player.transform.position;
            using (var input = new MovementKeyboard())
            {
                var mouse = InputSystem.AddDevice<Mouse>();
                var gamepad = InputSystem.AddDevice<Gamepad>();
                try
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                    yield return null;
                    yield return null;
                    float startYaw = camera.eulerAngles.y;
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100f, 0f) });
                    yield return null;
                    yield return null;
                    Assert.That(Mathf.DeltaAngle(startYaw, camera.eulerAngles.y), Is.EqualTo(14f).Within(0.25f));

                    Assert.That(Quaternion.Angle(originalFacing, player.transform.rotation), Is.LessThan(0.1f));
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(-700f, 0f) });
                    yield return new WaitForSeconds(1.5f);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(camera.eulerAngles.y, player.transform.eulerAngles.y)),
                        Is.LessThan(1f), "Ivan should finish the mouse-driven step after the mouse stops.");

                    input.Press(Key.Escape);
                    yield return null;
                    yield return null;
                    float releasedYaw = camera.eulerAngles.y;
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100f, 0f) });
                    yield return null;
                    yield return null;
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(releasedYaw, camera.eulerAngles.y)), Is.LessThan(0.1f));

                    InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.right });
                    float swept = 0f;
                    float bodySweep = 0f;
                    float previousBodyYaw = player.transform.eulerAngles.y;
                    float previousYaw = camera.eulerAngles.y;
                    float deadline = Time.time + 3f;
                    while (swept < 365f && Time.time < deadline)
                    {
                        yield return null;
                        swept += Mathf.DeltaAngle(previousYaw, camera.eulerAngles.y);
                        previousYaw = camera.eulerAngles.y;
                        bodySweep += Mathf.DeltaAngle(previousBodyYaw, player.transform.eulerAngles.y);
                        previousBodyYaw = player.transform.eulerAngles.y;
                    }
                    Assert.That(swept, Is.GreaterThan(360f));
                    Assert.That(bodySweep, Is.GreaterThan(90f), "The body must follow the ongoing stick orbit.");
                    InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.down });
                    yield return new WaitForSeconds(0.55f);
                    Assert.That(Mathf.DeltaAngle(0f, camera.eulerAngles.x), Is.EqualTo(55f).Within(0.2f));
                    yield return new WaitForSeconds(3f);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(camera.eulerAngles.y, player.transform.eulerAngles.y)),
                        Is.LessThan(32f));
                    Assert.That(Vector3.Distance(originalPosition, player.transform.position), Is.LessThan(0.01f));
                    Assert.That(player.GetComponent<Animator>().GetFloat("MoveSpeed"), Is.LessThan(0.01f));

                    InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.left });
                    yield return new WaitForSeconds(0.4f);
                    var standing = player.GetComponent("IvanStanding");
                    var turning = standing.GetType().GetProperty("IsTurning");
                    Assert.That((bool)turning.GetValue(standing), Is.True);
                    InputSystem.QueueStateEvent(gamepad, new GamepadState());
                    input.Press(Key.W, Key.LeftShift);
                    yield return new WaitForSeconds(0.15f);
                    Assert.That((bool)turning.GetValue(standing), Is.False);
                    Assert.That(player.GetComponent<Animator>().GetFloat("MoveSpeed"), Is.GreaterThan(0.2f));
                }
                finally
                {
                    InputSystem.RemoveDevice(gamepad);
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }

        [UnityTest]
        public IEnumerator IvanDrawsAndStowsWhileWalking()
        {
            yield return SceneManager.LoadSceneAsync(FinalRoom, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(FinalRoom);
            yield return null;
            var player = loadedScene.GetRootGameObjects().Single(root => root.CompareTag("Player"));
            var animator = player.GetComponent<Animator>();
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var holster = player.GetComponentsInChildren<Transform>().Single(t => t.name == "Sidearm Holster");
            var mount = holster.Find("Sidearm Mount");
            var prop = mount.GetComponentInChildren<MeshRenderer>();
            Assert.That(prop, Is.Not.Null);
            Vector3 meshCenter = prop.GetComponent<MeshFilter>().sharedMesh.bounds.center;
            Vector3 centerInMount = mount.InverseTransformPoint(prop.transform.TransformPoint(meshCenter));
            float holsteredDistance = Vector3.Distance(prop.transform.TransformPoint(meshCenter), holster.position);
            Assert.That(holsteredDistance, Is.InRange(0.05f, 0.2f));
            Assert.That(prop.enabled, Is.True);
            var layer = animator.GetLayerIndex("Sidearm");
            using (var input = new MovementKeyboard())
            {
                input.Press(Key.W, Key.Digit2);
                yield return new WaitForSeconds(0.17f);
                Assert.That(mount.parent, Is.EqualTo(holster));
                input.Press(Key.W, Key.Digit1);
                yield return new WaitForSeconds(0.35f);
                Assert.That(animator.GetLayerWeight(layer), Is.LessThan(0.001f));

                input.Press(Key.W, Key.Digit2);
                float deadline = Time.time + 0.9f;
                while (mount.parent == holster && Time.time < deadline) yield return null;
                Assert.That(mount.parent, Is.EqualTo(hand));
                Assert.That(Vector3.Distance(hand.Find("Right Palm Grip").position, holster.position), Is.LessThan(0.035f),
                    "The hand must meet the holster on the moving thigh at transfer.");
                Assert.That(Quaternion.Angle(hand.Find("Right Palm Grip").rotation, holster.rotation), Is.LessThan(10f));
                yield return new WaitForSeconds(0.85f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(layer).IsName("Sidearm.Draw"), Is.True);
                Assert.That(animator.GetLayerWeight(layer), Is.EqualTo(1f).Within(0.001f));
                Assert.That(Vector3.Distance(mount.position, holster.position), Is.GreaterThan(0.15f));
                Vector3 drawnCenter = prop.transform.TransformPoint(meshCenter);
                Assert.That(Vector3.Distance(drawnCenter, hand.Find("Right Palm Grip").TransformPoint(centerInMount)), Is.LessThan(0.002f));
                Assert.That(Vector3.Distance(drawnCenter, holster.TransformPoint(centerInMount)), Is.GreaterThan(0.12f),
                    "The mesh should travel with the hand, relative to its holstered pose in this same stride.");
                Assert.That(animator.GetFloat("MoveSpeed"), Is.GreaterThan(1f));

                input.Press(Key.Digit1);
                yield return new WaitForSeconds(0.32f);
                input.Press(Key.Digit2);
                yield return new WaitForSeconds(0.45f);
                Assert.That(mount.parent, Is.EqualTo(hand));
                input.Press(Key.Digit1);
                yield return new WaitForSeconds(1.55f);
                Assert.That(mount.parent, Is.EqualTo(holster));
                Assert.That(mount.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(animator.GetLayerWeight(layer), Is.LessThan(0.001f));
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), Is.True);
            }
        }

        private static readonly string[] AimStarts = { "behind", "small angle", "turning", "sprinting" };

        [UnityTest]
        public IEnumerator RightButtonDrawsFacesZoomsAndStows([ValueSource(nameof(AimStarts))] string start)
        {
            yield return SceneManager.LoadSceneAsync(FinalRoom, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(FinalRoom);
            yield return null;
            var roots = loadedScene.GetRootGameObjects();
            var player = roots.Single(root => root.CompareTag("Player"));
            var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>()).Single(c => c.CompareTag("MainCamera"));
            var animator = player.GetComponent<Animator>();
            // Batch Play Mode can cull finger animation when no Game view is rendering.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var holster = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).Find("Sidearm Holster");
            var mount = holster.Find("Sidearm Mount");
            if (start == "small angle")
            {
                camera.aspect = 16f / 9f;
                var head = camera.WorldToViewportPoint(animator.GetBoneTransform(HumanBodyBones.Head).position);
                var foot = camera.WorldToViewportPoint(animator.GetBoneTransform(HumanBodyBones.RightFoot).position);
                Assert.That(head.y - foot.y, Is.GreaterThan(0.60f),
                    "The normal over-the-shoulder shot should keep Ivan large enough to read.");
            }
            float lens = camera.fieldOfView;
            float yaw = camera.transform.eulerAngles.y;
            Assert.That(Vector3.Dot(camera.transform.position - player.transform.position, camera.transform.right),
                Is.GreaterThan(0.15f), "View should sit to the right of Ivan.");
            using (var input = new MovementKeyboard())
            {
                var mouse = InputSystem.AddDevice<Mouse>();
                try
                {
                    if (start == "turning")
                    {
                        input.Press(Key.A);
                        yield return null;
                        yield return null;
                        input.Press();
                        yield return new WaitForSeconds(0.27f);
                    }
                    else if (start == "sprinting")
                    {
                        input.Press(Key.D, Key.LeftShift);
                        yield return new WaitForSeconds(0.55f);
                    }
                    else player.transform.rotation = Quaternion.Euler(0f, yaw + (start == "behind" ? 179f : 16f), 0f);

                    var elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                    Vector3 elbowStart = player.transform.InverseTransformPoint(elbow.position);
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                    float deadline = Time.time + 0.5f;
                    do { yield return null; } while (animator.GetFloat("SidearmTime") < 0.15f && Time.time < deadline);
                    if (start == "small angle")
                    {
                        Vector3 elbowNow = player.transform.InverseTransformPoint(elbow.position);
                        Assert.That(elbowNow.z, Is.LessThan(elbowStart.z - 0.02f),
                            "The elbow should lead back before the prop leaves the holster.");
                        Assert.That(mount.parent, Is.EqualTo(holster));
                    }
                    Assert.That(camera.fieldOfView, Is.LessThan(lens - 1f));
                    Assert.That(animator.GetLayerWeight(animator.GetLayerIndex("Sidearm")), Is.GreaterThan(0f));
                    if (start == "sprinting")
                    {
                        Vector3 p = player.transform.position;
                        yield return new WaitForSeconds(0.25f);
                        Assert.That(Vector3.Dot(player.transform.position - p, camera.transform.right), Is.GreaterThan(0.35f));
                        Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, camera.transform.eulerAngles.y + 24f)), Is.LessThan(1f));
                        input.Press();
                    }
                    yield return new WaitForSeconds(2f);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, camera.transform.eulerAngles.y + 24f)), Is.LessThan(1f));
                    Assert.That(mount.parent, Is.EqualTo(hand));
                    Assert.That(Vector3.Dot(mount.Find("Sidearm Direction").forward, camera.transform.forward), Is.GreaterThan(0.98f));
                    if (start == "small angle")
                    {
                        Assert.That(Mathf.DeltaAngle(yaw, camera.transform.eulerAngles.y), Is.EqualTo(0f).Within(0.2f),
                            "Drawing should preserve the direction chosen with the mouse.");
                        // Check the evaluated avatar: a bad retargeted curve can exceed the
                        // thumb muscle range even with both wrists on the correct sockets.
                        using (var poseHandler = new HumanPoseHandler(animator.avatar, animator.transform))
                        {
                            var pose = new HumanPose();
                            poseHandler.GetHumanPose(ref pose);
                            for (int i = 0; i < HumanTrait.MuscleCount; i++)
                            {
                                if (HumanTrait.MuscleName[i].Contains(" Thumb "))
                                    Assert.That(pose.muscles[i], Is.InRange(-1.01f, 1.01f), HumanTrait.MuscleName[i]);
                            }
                        }
                        var head = camera.WorldToViewportPoint(animator.GetBoneTransform(HumanBodyBones.Head).position);
                        var wrist = camera.WorldToViewportPoint(hand.position);
                        var hips = camera.WorldToViewportPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position);
                        Assert.That(head.z, Is.GreaterThan(camera.nearClipPlane));
                        Assert.That(head.x, Is.InRange(0.06f, 0.34f), "The head should frame the left of the image.");
                        Assert.That(wrist.x, Is.InRange(head.x + 0.14f, 0.49f), "The weapon hand should leave the center open.");
                        Assert.That(wrist.y, Is.InRange(0.1f, 0.60f), "The weapon hand must stay visible.");
                        Assert.That(hips.y, Is.LessThan(-0.25f), "The shoulder close-up should crop the hips well below the frame.");
                    }

                    Vector3 upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
                    float elbowBend = Vector3.Angle(elbow.position - upper, hand.position - elbow.position);
                    Assert.That(elbowBend, Is.InRange(5f, 25f), "The extended arm keeps a slight elbow bend.");
                    Assert.That(animator.GetFloat("AimLean"), Is.EqualTo(1f).Within(0.01f));
                    var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    var support = mount.Find("Left Support Grip");
                    Assert.That(Vector3.Distance(leftHand.position, support.position), Is.LessThan(0.018f));
                    Assert.That(Quaternion.Angle(leftHand.rotation, support.rotation), Is.LessThan(1f));
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100f, -100f) }.WithButton(MouseButton.Right));
                    yield return new WaitForSeconds(0.65f);
                    Assert.That(Vector3.Distance(leftHand.position, support.position), Is.LessThan(0.018f));

                    if (start == "small angle")
                    {
                        var standing = player.GetComponent("IvanStanding");
                        var turning = standing.GetType().GetProperty("IsTurning");
                        foreach (float flick in new[] { 1200f, -1100f })
                        {
                            float before = camera.transform.eulerAngles.y;
                            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(flick, 0f) }.WithButton(MouseButton.Right));
                            yield return null;
                            yield return null;
                            Assert.That(Mathf.Abs(Mathf.DeltaAngle(before, camera.transform.eulerAngles.y)), Is.GreaterThan(145f));
                            Assert.That((bool)turning.GetValue(standing), Is.False, "An armed flick must not start a planted turn.");
                            Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, camera.transform.eulerAngles.y + 24f)), Is.LessThan(1f));
                            Assert.That(Vector3.Dot(mount.Find("Sidearm Direction").forward, camera.transform.forward), Is.GreaterThan(0.98f));
                        }
                        foreach (float vertical in new[] { 750f, -1000f })
                        {
                            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(0f, vertical) }.WithButton(MouseButton.Right));
                            yield return new WaitForSeconds(0.35f);
                            Assert.That(Vector3.Distance(leftHand.position, support.position), Is.LessThan(0.018f),
                                "Both hands should stay together at the camera pitch limits.");
                        }
                    }

                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    yield return new WaitForSeconds(1.55f);
                    Assert.That(camera.fieldOfView, Is.EqualTo(lens).Within(0.05f));
                    Assert.That(animator.GetFloat("AimLean"), Is.LessThan(0.001f));
                    Assert.That(mount.parent, Is.EqualTo(holster));
                    Assert.That(animator.GetLayerWeight(animator.GetLayerIndex("Sidearm")), Is.LessThan(0.001f));

                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                    yield return new WaitForSeconds(0.20f);
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    yield return new WaitForSeconds(0.38f);
                    Assert.That(mount.parent, Is.EqualTo(holster));
                    Assert.That(animator.GetLayerWeight(animator.GetLayerIndex("Sidearm")), Is.LessThan(0.001f));
                }
                finally { InputSystem.RemoveDevice(mouse); }
            }
        }

        private sealed class MovementKeyboard : System.IDisposable
        {
            private readonly InputSettings settings = InputSystem.settings;
            private readonly InputSettings.EditorInputBehaviorInPlayMode previousRouting;
            private readonly InputSettings.BackgroundBehavior previousBackground;
            private readonly Keyboard keyboard;

            public MovementKeyboard()
            {
                previousRouting = settings.editorInputBehaviorInPlayMode;
                previousBackground = settings.backgroundBehavior;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
            }

            public void Press(params Key[] keys) =>
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

            public void Dispose()
            {
                InputSystem.RemoveDevice(keyboard);
                settings.editorInputBehaviorInPlayMode = previousRouting;
                settings.backgroundBehavior = previousBackground;
            }
        }

        private static readonly string[] RifleNpcNames = { "Ch35 Rifle NPC", "SWAT Rifle NPC" };

        [UnityTest]
        public IEnumerator RifleNpcShootsReactsAndStaysDead([ValueSource(nameof(RifleNpcNames))] string npcName)
        {
            testTarget = new GameObject("NPC test target") { tag = "Player" };
            testTarget.AddComponent<AudioListener>();
            var capsule = testTarget.AddComponent<CharacterController>();
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            capsule.center = Vector3.up * 0.9f;
            testTarget.AddComponent(System.Type.GetType("CombatHealth, Assembly-CSharp"));
            var path = SceneUtility.GetScenePathByBuildIndex(0);
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(path);
            yield return null;

            var roots = loadedScene.GetRootGameObjects();
            var npc = roots.Single(root => root.name == npcName);
            foreach (var other in roots.Where(root => root != npc && root.GetComponent("RifleNpc") != null))
                other.SetActive(false);
            var player = testTarget;
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
        public IEnumerator FinalRoomLoadsWithCameraAndNoMissingScripts()
        {
            Assert.That(SceneManager.sceneCountInBuildSettings, Is.GreaterThan(0));
            var path = FinalRoom;
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
            Assert.That(player.GetComponent("IvanStanding"), Is.Not.Null);
            var cameras = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true));
            Assert.That(cameras.Any(camera => camera.isActiveAndEnabled), Is.True,
                "The final room must provide an active camera.");

            foreach (var root in roots)
            {
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    Assert.That(component == null, Is.False, $"Missing script under {root.name}");
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TempleTransitionCarriesPlayerAndCameraIntoFinalRoom()
        {
            const string temple = "Assets/Scenes/Levels/temple_v2.unity";
            yield return SceneManager.LoadSceneAsync(temple, LoadSceneMode.Additive);
            var source = SceneManager.GetSceneByPath(temple);
            loadedScene = source;
            var roots = source.GetRootGameObjects();
            var player = roots.Single(root => root.CompareTag("Player"));
            var camera = roots.Single(root => root.CompareTag("MainCamera"));
            var door = roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>())
                .Single(component => component != null && component.GetType().Name == "SceneTransitionDoor");
            Assert.That(player.GetComponent("IvanLocomotion"), Is.Not.Null);
            door.GetType().GetMethod("Enter").Invoke(door, new object[] { player.GetComponent<CharacterController>(), camera });

            var deadline = Time.realtimeSinceStartup + 20f;
            while (source.isLoaded && Time.realtimeSinceStartup < deadline)
                yield return null;

            var destination = SceneManager.GetSceneByPath(FinalRoom);
            if (destination.isLoaded) loadedScene = destination;
            Assert.That(source.isLoaded, Is.False);
            Assert.That(destination.isLoaded, Is.True);
            yield return null;

            var destinationRoots = destination.GetRootGameObjects();
            Assert.That(destinationRoots.Single(root => root.CompareTag("Player")), Is.EqualTo(player));
            Assert.That(destinationRoots.Single(root => root.CompareTag("MainCamera")), Is.EqualTo(camera));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator UnloadTestScene()
        {
            if (testTarget != null) Object.Destroy(testTarget);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
