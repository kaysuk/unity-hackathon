using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hackathon.Tests
{
    public class BootstrapSceneTests
    {
        private Scene loadedScene;

        private const string FinalRoom = "Assets/Scenes/Levels/Temple_Final_Room.unity";
        private GameObject testTarget;

        private static readonly bool[] CheckpointCases = { false, true };

        [UnityTest]
        public IEnumerator MainMenuShowsAvailableActionsAndNavigates([ValueSource(nameof(CheckpointCases))] bool hasSave)
        {
            const string menuScene = "Assets/UI/MainMenu/MainMenu.unity";
            const string saveKey = "Karamazovy.StoryCheckpoint";
            const string volumeKey = "MainMenu.MasterVolume";
            const float savedVolume = 0.42f;
            Assert.That(SceneUtility.GetScenePathByBuildIndex(0), Is.EqualTo(menuScene));

            bool hadCheckpoint = PlayerPrefs.HasKey(saveKey);
            string previousCheckpoint = PlayerPrefs.GetString(saveKey);
            bool hadVolumePreference = PlayerPrefs.HasKey(volumeKey);
            float previousVolumePreference = PlayerPrefs.GetFloat(volumeKey);
            float previousVolume = AudioListener.volume;
            try
            {
                if (hasSave)
                    PlayerPrefs.SetString(saveKey, "{\"version\":1,\"scene\":\"temple_v2\"}");
                else
                    PlayerPrefs.DeleteKey(saveKey);
                PlayerPrefs.SetFloat(volumeKey, savedVolume);

                yield return SceneManager.LoadSceneAsync(menuScene, LoadSceneMode.Additive);
                loadedScene = SceneManager.GetSceneByPath(menuScene);
                yield return null;

                var roots = loadedScene.GetRootGameObjects();
                var menu = roots.Single(root => root.name == "Main Menu").transform;
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                    .All(script => script != null), Is.True, "All scripts attached to the menu must resolve.");

                var backgrounds = menu.Find("Hallway Backdrop").GetComponentsInChildren<RawImage>(true);
                Assert.That(backgrounds, Has.Length.EqualTo(1));
                Assert.That(backgrounds[0].name, Is.EqualTo("Clear day"));
                Assert.That(backgrounds[0].isActiveAndEnabled, Is.True);
                Assert.That(backgrounds[0].texture?.name, Is.EqualTo("HallwayDay"));

                var fade = menu.Find("Fade").GetComponent<CanvasGroup>();
                float deadline = Time.realtimeSinceStartup + 5f;
                while (fade.blocksRaycasts && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(fade.blocksRaycasts, Is.False, "The intro fade must release the menu.");

                var events = roots.Single(root => root.name == "Event System").GetComponent<EventSystem>();
                var input = events.GetComponent<InputSystemUIInputModule>();
                Assert.That(input.move?.action, Is.Not.Null);
                Assert.That(input.submit?.action, Is.Not.Null);

                var home = menu.Find("Home");
                var newGame = home.Find("New Game").GetComponent<Button>();
                var continueGame = home.Find("Continue").GetComponent<Button>();
                var settings = home.Find("Settings").GetComponent<Button>();
                var quit = home.Find("Quit").GetComponent<Button>();
                var first = hasSave ? continueGame : newGame;
                Assert.That(home.Find("Game title").GetComponent<Text>().text, Is.EqualTo("Братья\nКарамазовы"));
                Assert.That(home.Find("Chapter"), Is.Null);
                Assert.That(continueGame.GetComponentInChildren<Text>(true).text, Is.EqualTo("Продолжить"));
                Assert.That(continueGame.gameObject.activeInHierarchy, Is.EqualTo(hasSave));
                Assert.That(newGame.isActiveAndEnabled, Is.True);
                Assert.That(events.currentSelectedGameObject, Is.SameAs(first.gameObject));

                var move = new AxisEventData(events) { moveDir = MoveDirection.Up, moveVector = Vector2.up };
                ExecuteEvents.Execute(first.gameObject, move, ExecuteEvents.moveHandler);
                Assert.That(events.currentSelectedGameObject, Is.SameAs(quit.gameObject));

                move = new AxisEventData(events) { moveDir = MoveDirection.Down, moveVector = Vector2.down };
                ExecuteEvents.Execute(quit.gameObject, move, ExecuteEvents.moveHandler);
                Assert.That(events.currentSelectedGameObject, Is.SameAs(first.gameObject));

                var settingsPanel = menu.Find("Settings");
                var volume = settingsPanel.Find("Volume").GetComponent<Slider>();
                var volumeValue = settingsPanel.Find("Volume value").GetComponent<Text>();
                var fullscreen = settingsPanel.Find("Fullscreen").GetComponent<Button>();
                settings.onClick.Invoke();
                Assert.That(home.gameObject.activeInHierarchy, Is.False);
                Assert.That(volume.gameObject.activeInHierarchy, Is.True);
                Assert.That(fullscreen.interactable, Is.True);
                Assert.That(fullscreen.GetComponentInChildren<Text>().text,
                    Is.EqualTo(Screen.fullScreen ? "Полный экран" : "Оконный"));
                Assert.That(events.currentSelectedGameObject, Is.SameAs(volume.gameObject));
                Assert.That(volume.value, Is.EqualTo(savedVolume).Within(0.001f));
                Assert.That(volumeValue.text, Is.EqualTo("42%"));
                Assert.That(AudioListener.volume, Is.EqualTo(savedVolume).Within(0.001f));

                volume.value = 0.67f;
                Assert.That(PlayerPrefs.GetFloat(volumeKey), Is.EqualTo(0.67f).Within(0.001f));
                Assert.That(AudioListener.volume, Is.EqualTo(0.67f).Within(0.001f));
                Assert.That(volumeValue.text, Is.EqualTo("67%"));

                ExecuteEvents.Execute(volume.gameObject, new BaseEventData(events), ExecuteEvents.cancelHandler);
                Assert.That(home.gameObject.activeInHierarchy, Is.True);
                Assert.That(events.currentSelectedGameObject, Is.SameAs(settings.gameObject));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (hadCheckpoint)
                    PlayerPrefs.SetString(saveKey, previousCheckpoint);
                else
                    PlayerPrefs.DeleteKey(saveKey);
                if (hadVolumePreference)
                    PlayerPrefs.SetFloat(volumeKey, previousVolumePreference);
                else
                    PlayerPrefs.DeleteKey(volumeKey);
                AudioListener.volume = previousVolume;
            }
        }

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

        [UnityTest]
        public IEnumerator StairsAreSilentAndReadyPistolUsesSwatHitZones()
        {
            const string path = "Assets/Scenes/Levels/temple_v2.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(path);
            yield return null;
            var scripts = loadedScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>()).ToArray();
            var graph = scripts.Single(s => s.GetType().Name == "GraphManager");
            var player = scripts.Single(s => s.GetType().Name == "IvanSidearm").gameObject;
            var muzzle = player.GetComponentsInChildren<Transform>().Single(t => t.name == "Sidearm Direction");
            var flash = muzzle.Find("Muzzle flash").gameObject;
            Assert.IsFalse(flash.activeSelf);
            Assert.IsTrue(flash.GetComponent<Light>().enabled);
            Assert.IsNotNull(flash.GetComponent<MeshFilter>().sharedMesh);
            var guards = scripts.Where(s => s.GetType().Name == "RifleNpc").ToArray();
            var playerBody = player.GetComponent<CharacterController>();
            var npcBody = guards[0].GetComponent<CharacterController>();
            var source = graph.GetType().GetMethod("FootstepSource");
            var floors = loadedScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoxCollider>())
                .Where(c => c.gameObject.layer == LayerMask.NameToLayer("SilentStairs")).ToArray();
            Assert.That(floors.Length, Is.GreaterThan(20));
            foreach (var floor in floors)
            {
                var feet = floor.bounds.center + Vector3.up * floor.bounds.extents.y;
                Assert.IsNull(source.Invoke(graph, new object[] {feet, npcBody, playerBody}), floor.name);
            }
            Assert.IsNotNull(source.Invoke(graph, new object[] {new Vector3(6f, 0f, 0.9f), npcBody, playerBody}));

            var camera = scripts.Single(s => s.GetType().Name == "IvanOrbitCamera");
            camera.enabled = false;
            ((Behaviour)player.GetComponent("IvanLocomotion")).enabled = false;
            void Place(GameObject actor, Vector3 position)
            {
                var capsule = actor.GetComponent<CharacterController>();
                capsule.enabled = false;
                actor.transform.SetPositionAndRotation(position, Quaternion.identity);
                capsule.enabled = true;
            }
            Vector3 origin = Vector3.right * 120f;
            Place(player, origin);
            for (int i = 0; i < guards.Length; i++)
            {
                guards[i].enabled = false;
                Place(guards[i].gameObject, origin + Vector3.forward * 5f + Vector3.right * i * 2f);
            }
            camera.transform.position = origin + new Vector3(0.3f, 1.5f, -1f);
            var target = guards[0].GetComponent("CombatHealth");
            float HP(Component health) => (float)health.GetType().GetProperty("HP").GetValue(health);
            var torso = guards[0].GetComponentsInChildren<CapsuleCollider>().Single(c => c.name == "Torso hit zone");
            var head = guards[1].GetComponentsInChildren<SphereCollider>().Single(c => c.name == "Head hit zone");
            using (var routing = new MovementKeyboard())
            {
                var mouse = InputSystem.AddDevice<Mouse>();
                var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    cover.transform.position = origin + new Vector3(0f, 1f, 2f);
                    cover.transform.localScale = new Vector3(2, 3, 0.2f);
                    cover.SetActive(false);
                    IEnumerator Click(Collider zone, bool aim, bool expectFlash = true)
                    {
                        yield return new WaitForSeconds(0.20f);
                        var buttons = aim ? new MouseState().WithButton(MouseButton.Right) : new MouseState();
                        InputSystem.QueueStateEvent(mouse, buttons);
                        yield return null;
                        Physics.SyncTransforms();
                        camera.transform.LookAt(zone.bounds.center);
                        InputSystem.QueueStateEvent(mouse, buttons.WithButton(MouseButton.Left));
                        yield return null;
                        bool flashed = flash.activeInHierarchy;
                        yield return null;
                        Assert.AreEqual(expectFlash, flashed || flash.activeInHierarchy, "Pistol muzzle flash");
                    }
                    yield return Click(torso, false, false);
                    Assert.AreEqual(100f, HP(target), "Holstered pistol fired");
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                    yield return Click(torso, true, false);
                    Assert.AreEqual(100f, HP(target), "Pistol fired before it was drawn");
                    yield return new WaitForSeconds(1.4f);
                    cover.SetActive(true);
                    yield return Click(torso, true);
                    Assert.AreEqual(100f, HP(target), "Shot passed through cover");
                    yield return new WaitForSeconds(.1f);
                    Assert.IsFalse(flash.activeSelf, "The muzzle flash should extinguish between shots");
                    cover.SetActive(false);
                    for (int i = 1; i <= 3; i++)
                    {
                        yield return Click(torso, true);
                        Assert.That(HP(target), Is.EqualTo(100f * (3 - i) / 3f).Within(0.001f), $"Torso shot {i}");
                    }
                    yield return Click(head, true);
                    Assert.AreEqual(0f, HP(guards[1].GetComponent("CombatHealth")), "Headshot must be fatal");
                }
                finally
                {
                    Object.Destroy(cover);
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }

        [UnityTest]
        public IEnumerator WoundedSwatTurnsTowardsIncomingFire()
        {
            const string path = "Assets/Scenes/Levels/temple_v2.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(path);
            yield return null;
            var guard = loadedScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>())
                .Single(s => s.GetType().Name == "RifleNpc" && (bool)s.GetType().GetProperty("HoldsPosition").GetValue(s));
            var zones = guard.GetComponent("SwatHitZones");
            var torso = guard.GetComponentsInChildren<CapsuleCollider>().Single(c => c.name == "Torso hit zone");
            var shooter = GameObject.FindGameObjectWithTag("Player");

            void ShootFrom(Vector3 towardShooter)
            {
                Physics.SyncTransforms();
                var ray = new Ray(torso.bounds.center + towardShooter * 2f, -towardShooter);
                Assert.IsTrue(torso.Raycast(ray, out var hit, 3f));
                zones.GetType().GetMethod("ApplyShot").Invoke(zones, new object[] { hit, ray.direction, shooter });
            }

            var behind = -guard.transform.forward;
            var flank = guard.transform.right;
            foreach (var bearing in new[] { behind, flank })
            {
                ShootFrom(bearing);
                float deadline = Time.time + 1f;
                while (Vector3.Angle(guard.transform.forward, bearing) > 2f && Time.time < deadline)
                    yield return null;
                Assert.That(Vector3.Angle(guard.transform.forward, bearing), Is.LessThanOrEqualTo(2f));
            }
            var lastFacing = guard.transform.rotation;
            ShootFrom(behind);
            yield return new WaitForSeconds(0.55f);
            Assert.IsFalse(torso.enabled);
            Assert.That(Quaternion.Angle(guard.transform.rotation, lastFacing), Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator SixSealDocumentsCanBeReadInTheTemple()
        {
            const string path = "Assets/Scenes/Levels/temple_v2.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(path);
            yield return null;
            var scripts = loadedScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>()).ToArray();
            var document = scripts.Single(s => s.name == "D01 - Supply note" && s.GetType().Name == "TempleSealDocument");
            var player = scripts.Single(s => s.GetType().Name == "PlayerInteractor");
            var orbit = scripts.Single(s => s.GetType().Name == "IvanOrbitCamera");
            orbit.enabled = false;
            ((Behaviour)player.GetComponent("IvanLocomotion")).enabled = false;
            var body = player.GetComponent<CharacterController>();
            void AimAt(Component sheet, Vector3 feet, Vector3 cameraPosition)
            {
                body.enabled = false;
                player.transform.position = feet;
                body.enabled = true;
                orbit.transform.position = cameraPosition;
                Physics.SyncTransforms();
                orbit.transform.LookAt(sheet.GetComponent<BoxCollider>().bounds.center);
            }
            AimAt(document, new Vector3(7.47f, 0.01f, 5.45f), new Vector3(7.7f, 1.35f, 4.5f));
            var findTarget = player.GetType().GetMethod("FindTargetInReach",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.AreNotSame(document, findTarget.Invoke(player, null), "Closed niche");

            var tiles = scripts.Where(s => s.GetType().Name == "TempleSlidingTile")
                .ToDictionary(s => (int)s.GetType().GetProperty("Number").GetValue(s));
            foreach (int number in new[] { 4, 8, 7, 6, 10, 11, 15 })
                tiles[number].GetType().GetMethod("Interact").Invoke(tiles[number], new object[] { player });
            yield return new WaitForSeconds(0.65f);
            Assert.AreSame(document, findTarget.Invoke(player, null), "Aim at the paper through the open niche");

            var prompt = document.GetType().GetMethod("GetPrompt");
            var read = document.GetType().GetMethod("Interact");
            var sheets = scripts.Where(s => s.GetType() == document.GetType()).OrderBy(s => s.name).ToArray();
            Assert.That(sheets.Length, Is.EqualTo(6));
            int count = 0;
            void ReadOnce(Component sheet)
            {
                Assert.AreSame(sheet, findTarget.Invoke(player, null), sheet.name);
                Assert.That((string)prompt.Invoke(sheet, new object[] { player, "E" }), Does.Contain("Прочитать"));
                read.Invoke(sheet, new object[] { player });
                read.Invoke(sheet, new object[] { player });
                Assert.That((string)prompt.Invoke(sheet, new object[] { player, "E" }), Does.Contain($"{++count}/6"));
            }
            ReadOnce(document);
            // Rooms can be searched in any order, including while the library is closed.
            foreach (var (room, feet, camera) in new[]
            {
                (3, new Vector3(19.7f, 0.01f, 5.65f), new Vector3(19.9f, 1.45f, 4.8f)),
                (2, new Vector3(14.1f, 0.01f, 5.7f), new Vector3(14.3f, 1.55f, 4.8f)),
                (6, new Vector3(19.25f, 0.01f, -5.75f), new Vector3(19.45f, 1.45f, -4.8f)),
                (5, new Vector3(12.1f, 0.01f, -5.7f), new Vector3(12.3f, 1.45f, -4.8f)),
                (4, new Vector3(4.9f, 0.01f, -6.55f), new Vector3(5.7f, 1.5f, -6.65f))
            })
            {
                var sheet = sheets[room - 1];
                AimAt(sheet, feet, camera);
                ReadOnce(sheet);
            }
            Assert.That((string)prompt.Invoke(document, new object[] { player, "E" }),
                Does.Contain("6/6").And.Contain("по порядку комнат"));
        }

        [UnityTest]
        public IEnumerator ArchiveLeversOpenThePassageToTheRuneWorkshop()
        {
            const string path = "Assets/Scenes/Levels/temple_v2.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(path);
            yield return null;
            var scripts = loadedScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>()).ToArray();
            var player = scripts.Single(s => s.GetType().Name == "PlayerInteractor");
            var orbit = scripts.Single(s => s.GetType().Name == "IvanOrbitCamera");
            var door = scripts.Single(s => s.GetType().Name == "TempleShortDoor" && s.name == "Room 7.1 - Archive entrance");
            var wall = scripts.Single(s => s.GetType().Name == "TemplePassage" && s.name == "Concealed archive wall");
            var graph = scripts.Single(s => s.GetType().Name == "GraphManager");
            var east = scripts.Single(s => s.GetType().Name == "Node" && s.name == "HiddenEast");
            var west = scripts.Single(s => s.GetType().Name == "Node" && s.name == "HiddenWest");
            bool HasRoute(Component from, Component to) =>
                graph.GetType().GetMethod("FindPath").Invoke(graph, new object[] { from, to }) != null;
            bool WallOpen() => (bool)wall.GetType().GetProperty("IsOpen").GetValue(wall);
            var controller = player.GetComponent<CharacterController>();
            orbit.enabled = false;
            ((Behaviour)player.GetComponent("IvanLocomotion")).enabled = false;
            var find = player.GetType().GetMethod("FindTargetInReach",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            void Use(Component target) => target.GetType().GetMethod("Interact").Invoke(target, new object[] { player });
            void Aim(Vector3 point)
            {
                orbit.transform.position = player.transform.position + new Vector3(.16f, 1.5f, -.65f);
                orbit.transform.LookAt(point);
                Physics.SyncTransforms();
            }
            void Place(Vector3 feet)
            {
                controller.enabled = false;
                player.transform.position = feet;
                controller.enabled = true;
            }
            Place(new Vector3(31.7f, .02f, 1.15f));
            Aim(new Vector3(31.45f, 1.28f, 2.28f));
            Assert.AreSame(door, find.Invoke(player, null));
            Use(door);
            yield return new WaitForSeconds(.55f);
            Assert.IsTrue((bool)door.GetType().GetProperty("IsPassable").GetValue(door));
            for (int i = 0; i < 28; i++) controller.Move(Vector3.forward * .14f);
            Assert.Greater(player.transform.position.z, 4.4f, "Both door leaves must clear the passage");

            Place(east.transform.position + Vector3.up * .02f);
            for (int i = 0; i < 20; i++) controller.Move(Vector3.left * .12f);
            Assert.Greater(player.transform.position.x, wall.transform.position.x, "The sealed wall must stop Ivan");
            Assert.IsFalse(HasRoute(east, west));
            var levers = scripts.Where(s => s.GetType().Name == "TempleArchiveLever")
                .OrderBy(s => s.transform.position.x).ToArray();
            Assert.AreEqual(6, levers.Length);
            Assert.AreEqual("Room 7.1", levers[0].transform.parent.parent.name);
            var workshop = loadedScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "Room 7.2");
            var workshopScripts = workshop.GetComponentsInChildren<MonoBehaviour>(true);
            Assert.That(workshopScripts.Count(s => s.GetType().Name == "TempleRuneStation"), Is.EqualTo(6));
            Assert.IsFalse(workshopScripts.Any(s => s.GetType().Name == "TempleSealPanel" || s.GetType().Name == "TempleSealButton"));
            void Pull(int number)
            {
                var lever = levers[number - 1];
                var handle = lever.GetComponentInChildren<BoxCollider>();
                Place(new Vector3(handle.bounds.center.x, .02f, 7.25f));
                Aim(handle.bounds.center);
                Assert.AreSame(lever, find.Invoke(player, null), lever.name);
                Use(lever);
                Physics.SyncTransforms();
            }
            Pull(1);
            Pull(6);
            Pull(2);
            Pull(4);
            Assert.IsFalse(WallOpen(), "An extra lowered lever must keep the wall closed");
            Pull(1);
            Assert.IsTrue(WallOpen(), "The lever combination alone opens the passage");
            Assert.IsFalse(HasRoute(east, west), "The wall is still moving");
            var shutter = wall.GetComponent<BoxCollider>();
            float deadline = Time.time + 5f;
            var moving = wall.GetType().GetProperty("IsMoving");
            while ((bool)moving.GetValue(wall) && Time.time < deadline) yield return null;
            Physics.SyncTransforms();
            Assert.Less(shutter.bounds.max.y, 0f);
            Assert.IsTrue(HasRoute(east, west));
            Assert.IsTrue(HasRoute(west, east));
            Place(east.transform.position + Vector3.up * .02f);
            for (int i = 0; i < 22; i++) controller.Move(Vector3.left * .12f);
            Assert.LessOrEqual(player.transform.position.x, west.transform.position.x, "Ivan must enter room 7.2");
            for (int i = 0; i < 22; i++) controller.Move(Vector3.right * .12f);
            Assert.Greater(player.transform.position.x, wall.transform.position.x, "The return passage must stay open");
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
            const string path = "Assets/Scenes/SampleScene.unity";
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
        public IEnumerator AngelSealLocksToGroundAndStrikesOnce([ValueSource(nameof(StartDuringTurn))] bool escape)
        {
            yield return SceneManager.LoadSceneAsync(FinalRoom, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(FinalRoom);
            var roots = loadedScene.GetRootGameObjects();
            var player = roots.Single(root => root.CompareTag("Player"));
            var boss = roots.Single(root => root.GetComponent("AngelSealAttack") != null);
            foreach (var guard in roots.Where(root => root.GetComponent("RifleNpc") != null))
                guard.SetActive(false);

            yield return null;
            var health = player.GetComponent("CombatHealth");
            var hp = health.GetType().GetProperty("HP");
            var initial = (float)hp.GetValue(health);
            var ring = boss.transform.Find("Seal boundary").GetComponent<LineRenderer>();
            float deadline = Time.time + 6f;
            while (!ring.enabled && Time.time < deadline) yield return null;
            Assert.That(ring.enabled, Is.True);
            Vector3 marked = ring.transform.position;
            Assert.That(Vector3.Distance(marked, player.transform.position), Is.LessThan(0.15f));
            if (escape) player.GetComponent<CharacterController>().Move(Vector3.right * 2.5f);
            yield return new WaitForSeconds(0.7f);
            Assert.That((float)hp.GetValue(health), Is.EqualTo(initial));
            Assert.That(ring.transform.position, Is.EqualTo(marked));

            var attack = boss.GetComponent("AngelSealAttack");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var finger = (Transform)attack.GetType().GetField("finger", flags).GetValue(attack);
            var knuckle = (Transform)attack.GetType().GetField("knuckle", flags).GetValue(attack);
            Assert.That(finger, Is.Not.Null);
            Assert.That(Vector3.Angle(finger.position - knuckle.position, marked - knuckle.position), Is.LessThan(15f));

            yield return new WaitForSeconds(1.7f);
            Assert.That(ring.enabled, Is.False);
            Assert.That((float)hp.GetValue(health), Is.EqualTo(initial - (escape ? 0f : 20f)));
        }

        private static readonly bool[] HaloStances = { false, true };

        [UnityTest]
        public IEnumerator HaloWavePassesOverCrouchingIvan([ValueSource(nameof(HaloStances))] bool crouch)
        {
            yield return SceneManager.LoadSceneAsync(FinalRoom, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(FinalRoom);
            var roots = loadedScene.GetRootGameObjects();
            var player = roots.Single(root => root.CompareTag("Player"));
            var boss = roots.Single(root => root.GetComponent("AngelSealAttack") != null);
            foreach (var guard in roots.Where(root => root.GetComponent("RifleNpc") != null))
                guard.SetActive(false);
            yield return null;
            var health = player.GetComponent("CombatHealth");
            var hp = health.GetType().GetProperty("HP");
            var wave = boss.transform.Find("Halo wave").GetComponent<LineRenderer>();
            float deadline = Time.time + 16f;
            while (!wave.enabled && Time.time < deadline) yield return null;
            Assert.IsTrue(wave.enabled);
            Assert.IsFalse(boss.transform.Find("Seal boundary").GetComponent<LineRenderer>().enabled);
            // Read the rendered LateUpdate pose before the next Update restores the rig.
            yield return new WaitForFixedUpdate();
            var bones = boss.GetComponentsInChildren<Transform>();
            var halo = bones.Single(bone => bone.name == "outter_torus");
            var mesh = halo.GetComponent<MeshFilter>().sharedMesh.bounds;
            Vector3 center = halo.TransformPoint(mesh.center);
            float height = wave.GetPosition(0).y;
            Assert.That(center.y, Is.EqualTo(height).Within(0.12f));
            for (int i = 0; i < 3; i++)
            {
                float a = 2f * Mathf.PI * i / 3f;
                var grip = mesh.center + new Vector3(Mathf.Cos(a) * mesh.extents.x, 0f, Mathf.Sin(a) * mesh.extents.z);
                Vector3 rim = halo.TransformPoint(grip);
                Assert.That(rim.y, Is.EqualTo(height).Within(0.12f));
                var palm = bones.Single(bone => bone.name == $"hand{i + 1}:middle_1");
                Assert.That(Vector3.Distance(palm.position, rim), Is.LessThan(0.12f));
            }
            float before = (float)hp.GetValue(health);
            using (var input = new MovementKeyboard())
            {
                if (crouch) input.Press(Key.C);
                while (wave.enabled && Time.time < deadline + 4f) yield return null;
                Assert.IsFalse(wave.enabled);
                Assert.That((float)hp.GetValue(health), Is.EqualTo(before - (crouch ? 0f : 20f)));
            }
        }

        [UnityTest]
        public IEnumerator TempleTransitionCarriesPlayerAndCameraIntoFinalRoom()
        {
            const string temple = "Assets/Scenes/Levels/temple_v2.unity";
            // The empty test-runner scene has no listener while the first level streams in.
            testTarget = new GameObject("Loading listener");
            var loadingListener = testTarget.AddComponent<AudioListener>();
            void OnTempleLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name == "temple_v2") loadingListener.enabled = false;
            }
            SceneManager.sceneLoaded += OnTempleLoaded;
            try { yield return SceneManager.LoadSceneAsync(temple, LoadSceneMode.Additive); }
            finally { SceneManager.sceneLoaded -= OnTempleLoaded; }
            var source = SceneManager.GetSceneByPath(temple);
            loadedScene = source;
            var roots = source.GetRootGameObjects();
            var player = roots.Single(root => root.CompareTag("Player"));
            var camera = roots.Single(root => root.CompareTag("MainCamera"));
            var door = roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>())
                .Single(component => component != null && component.GetType().Name == "SceneTransitionDoor");
            Assert.That(player.GetComponent("IvanLocomotion"), Is.Not.Null);
            Assert.That(camera.GetComponent<Camera>()?.isActiveAndEnabled, Is.True,
                "The gameplay scene must provide an active camera.");
            foreach (var root in roots)
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    Assert.That(component == null, Is.False, $"Missing script under {root.name}");

            yield return null;
            var pistol = (Behaviour)player.GetComponent("PistolCombat");
            Assert.That(pistol, Is.Not.Null);
            Assert.That(pistol.isActiveAndEnabled, Is.True);
            var pistolType = pistol.GetType();
            pistolType.GetMethod("RestoreAmmo",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(pistol, new object[] { 7, 24, 12 });

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
            var boss = destinationRoots.Select(root => root.GetComponent("AngelSealAttack")).Single(c => c != null);
            var bossTarget = boss.GetType().GetField("player", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).GetValue(boss);
            Assert.That(bossTarget, Is.EqualTo(player.transform));
            Assert.That(pistolType.GetProperty("Ammo").GetValue(pistol), Is.EqualTo(7));
            Assert.That(pistolType.GetProperty("ReserveAmmo").GetValue(pistol), Is.EqualTo(24));
            Assert.That(pistolType.GetProperty("MagazineCapacity").GetValue(pistol), Is.EqualTo(12));
            Assert.That(camera.GetComponent<AudioListener>().isActiveAndEnabled, Is.True);
            Assert.That(destinationRoots.SelectMany(r => r.GetComponentsInChildren<AudioListener>())
                .Count(l => l.isActiveAndEnabled), Is.EqualTo(1));
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
