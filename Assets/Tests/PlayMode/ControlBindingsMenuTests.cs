using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hackathon.Tests
{
    public sealed class ControlBindingsMenuTests
    {
        private const string Preference = "Controls.KeyboardMouse.Overrides.v1";
        private const string MainMenu = "Assets/UI/MainMenu/MainMenu.unity";
        private const string Arena = "Assets/Scenes/Levels/Temple_Final_Room.unity";
        private Scene loaded;
        private string attemptCsv;

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            if (loaded.IsValid() && loaded.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loaded);
            if (attemptCsv != null)
            {
                File.Delete(attemptCsv);
                attemptCsv = null;
            }
        }

        [UnityTest]
        public IEnumerator RebindInMenuUseInGameAndResetDuringPause()
        {
            string savedOverrides = InputSystem.actions.SaveBindingOverridesAsJson();
            bool hadPreference = PlayerPrefs.HasKey(Preference);
            string savedPreference = PlayerPrefs.GetString(Preference);
            float previousTimeScale = Time.timeScale;
            var originalScene = SceneManager.GetActiveScene();
            using var keyboard = new BootstrapSceneTests.MovementKeyboard();
            try
            {
                yield return SceneManager.LoadSceneAsync(MainMenu, LoadSceneMode.Additive);
                loaded = SceneManager.GetSceneByPath(MainMenu);
                yield return new WaitForSecondsRealtime(.45f);
                var menu = loaded.GetRootGameObjects().Single(root => root.name == "Main Menu").transform;
                Click(menu.Find("Home/Settings"));
                Click(menu.Find("Settings/Controls"));
                var panel = GameObject.Find("Control bindings").transform.Find("Backdrop/Panel");
                Click(panel.Find("Defaults"));
                var aim = panel.Find("Bindings/Viewport/Content/Прицелиться/Primary");
                var label = aim.Find("Key").GetComponent<Text>();
                Assert.That(label.text, Is.EqualTo("Q"));
                Click(aim);
                keyboard.Press(Key.F);
                yield return null;
                yield return null;
                Assert.That(label.text, Is.EqualTo("Q"));
                Assert.That(panel.Find("Instructions").GetComponent<Text>().text, Does.Contain("уже"));
                keyboard.Press();
                yield return null;
                Click(aim);
                keyboard.Press(Key.P);
                yield return null;
                yield return null;
                Assert.That(label.text, Is.EqualTo("P"));
                Assert.That(PlayerPrefs.GetString(Preference), Does.Contain("<Keyboard>/p"));
                keyboard.Press();
                yield return null;
                Click(aim);
                keyboard.Press(Key.Escape);
                yield return null;
                yield return null;
                Assert.That(panel.gameObject.activeInHierarchy, Is.True, "Escape cancels the capture first.");
                Assert.That(label.text, Is.EqualTo("P"));
                keyboard.Press();
                yield return null;
                Click(panel.Find("Back"));
                Assert.That(menu.Find("Settings/Controls").gameObject.activeInHierarchy, Is.True);
                yield return SceneManager.UnloadSceneAsync(loaded);

                yield return SceneManager.LoadSceneAsync(Arena, LoadSceneMode.Additive);
                loaded = SceneManager.GetSceneByPath(Arena);
                SceneManager.SetActiveScene(loaded);
                yield return null;
                var player = loaded.GetRootGameObjects().Single(root => root.CompareTag("Player"));
                var sidearm = player.GetComponent("IvanSidearm");
                bool Aiming() => (bool)sidearm.GetType().GetProperty("IsAiming").GetValue(sidearm);
                keyboard.Press(Key.Q);
                yield return null;
                Assert.That(Aiming(), Is.False);
                keyboard.Press(Key.P);
                yield return null;
                Assert.That(Aiming(), Is.True);
                keyboard.Press();
                yield return null;
                keyboard.Press(Key.Escape);
                yield return null;
                yield return null;
                Assert.That(Time.timeScale, Is.Zero);
                panel = GameObject.Find("Control bindings").transform.Find("Backdrop/Panel");
                Click(panel.Find("Defaults"));
                keyboard.Press();
                yield return null;
                keyboard.Press(Key.Q);
                yield return null;
                Assert.That(Aiming(), Is.False, "Settings must not feed key presses to Ivan.");
                keyboard.Press();
                yield return null;
                Click(panel.Find("Back"));
                yield return null;
                Assert.That(Time.timeScale, Is.EqualTo(previousTimeScale));
                keyboard.Press(Key.Q);
                yield return null;
                Assert.That(Aiming(), Is.True, "Reset restores the original key without reloading the arena.");
            }
            finally
            {
                if (originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
                InputSystem.actions.LoadBindingOverridesFromJson(savedOverrides);
                if (hadPreference) PlayerPrefs.SetString(Preference, savedPreference);
                else PlayerPrefs.DeleteKey(Preference);
                PlayerPrefs.Save();
                Time.timeScale = previousTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator PausingKeepsTheModelAndRestartLoadsTheLearnedWeightsInAFreshArena()
        {
            const string testDirectoryKey = "NimbLearning.TestDirectory";
            const BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            string directory = Path.Combine(Application.temporaryCachePath, "NimbRestart-" + Guid.NewGuid().ToString("N"));
            string profile = Path.Combine(directory, "profile.json");
            object previousDirectory = AppDomain.CurrentDomain.GetData(testDirectoryKey);
            Scene original = SceneManager.GetActiveScene();
            float previousTime = Time.timeScale;
            using var keyboard = new BootstrapSceneTests.MovementKeyboard();
            AppDomain.CurrentDomain.SetData(testDirectoryKey, directory);
            try
            {
                yield return SceneManager.LoadSceneAsync(Arena, LoadSceneMode.Additive);
                loaded = SceneManager.GetSceneByPath(Arena);
                SceneManager.SetActiveScene(loaded);
                yield return null;

                Component Boss(Scene arena) => arena.GetRootGameObjects().Select(root => root.GetComponent("AngelSealAttack"))
                    .Single(component => component != null);
                var boss = Boss(loaded);
                var type = boss.GetType();
                type.GetField("useFixedAttackOrder", members).SetValue(boss, false);
                type.GetField("engaged", members).SetValue(boss, true);
                type.GetField("nextCast", members).SetValue(boss, Time.time);
                type.GetMethod("TickAttack", members).Invoke(boss, null);
                var csv = type.GetField("attackCsv", members).GetValue(boss);
                attemptCsv = (string)csv.GetType().GetProperty("FilePath", members).GetValue(csv);
                var attack = type.GetField("lastAttack", members).GetValue(boss);
                float Prediction(Component current)
                {
                    var model = type.GetField("attackModel", members).GetValue(current);
                    return (float)model.GetType().GetMethod("Predict", members)
                        .Invoke(model, new object[] { attack, 10f, 0, false });
                }
                float originalChance = Prediction(boss);
                var player = loaded.GetRootGameObjects().Single(root => root.CompareTag("Player"));
                var health = player.GetComponent("CombatHealth");
                health.GetType().GetMethod("TakeDamage").Invoke(health,
                    new object[] { 1f, player.transform.position, Vector3.up, boss.gameObject });

                keyboard.Press(Key.Escape);
                yield return null;
                yield return null;
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(File.Exists(profile), Is.False, "Opening the pause menu must not train.");
                Assert.That(Prediction(boss), Is.EqualTo(originalChance));
                var panel = GameObject.Find("Control bindings").transform.Find("Backdrop/Panel");
                Click(panel.Find("Restart"));

                Scene previousArena = loaded;
                float timeout = Time.realtimeSinceStartup + 15f;
                while ((previousArena.isLoaded || SceneManager.GetActiveScene().path != Arena) &&
                       Time.realtimeSinceStartup < timeout)
                    yield return null;
                loaded = SceneManager.GetSceneByPath(Arena);
                Assert.That(previousArena.isLoaded, Is.False, "Restart must replace the previous arena.");
                Assert.That(loaded.isLoaded && SceneManager.GetActiveScene() == loaded, Is.True);
                yield return null;
                Assert.That(Time.timeScale, Is.EqualTo(previousTime));
                Assert.That(File.Exists(profile), Is.True);
                Assert.That(Prediction(Boss(loaded)), Is.GreaterThan(originalChance),
                    "The replacement boss must load the updated prediction for the confirmed hit.");
            }
            finally
            {
                loaded = SceneManager.GetSceneByPath(Arena);
                if (original.isLoaded) SceneManager.SetActiveScene(original);
                Time.timeScale = previousTime;
                AppDomain.CurrentDomain.SetData(testDirectoryKey, previousDirectory);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [UnityTest]
        public IEnumerator ReachingRitualUnlocksMemoryResetInTheMainMenu()
        {
            const string profileOverride = "NimbLearning.TestDirectory";
            const string progressKey = "Karamazovy.TestReachedRitual";
            const string saveKey = "Karamazovy.StoryCheckpoint";
            const BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Static;
            string directory = Path.Combine(Application.temporaryCachePath, "NimbMenu-" + Guid.NewGuid().ToString("N"));
            object oldDirectory = AppDomain.CurrentDomain.GetData(profileOverride);
            int oldProgress = PlayerPrefs.GetInt(progressKey, -1);
            bool hadSave = PlayerPrefs.HasKey(saveKey);
            string oldSave = PlayerPrefs.GetString(saveKey);
            Scene original = SceneManager.GetActiveScene();
            Transform menu = null;
            IEnumerator OpenMenu()
            {
                yield return SceneManager.LoadSceneAsync(MainMenu, LoadSceneMode.Additive);
                loaded = SceneManager.GetSceneByPath(MainMenu);
                SceneManager.SetActiveScene(loaded);
                yield return new WaitForSecondsRealtime(.45f);
                menu = loaded.GetRootGameObjects().Single(root => root.name == "Main Menu").transform;
            }

            AppDomain.CurrentDomain.SetData(profileOverride, directory);
            try
            {
                PlayerPrefs.DeleteKey(progressKey);
                PlayerPrefs.SetString(saveKey, "{\"version\":1,\"scene\":\"temple_v2\"}");
                yield return OpenMenu();
                var reset = menu.Find("Home/Reset Nimb memory");
                Assert.IsFalse(reset != null && reset.gameObject.activeInHierarchy);
                yield return SceneManager.UnloadSceneAsync(loaded);

                yield return SceneManager.LoadSceneAsync(Arena, LoadSceneMode.Additive);
                loaded = SceneManager.GetSceneByPath(Arena);
                SceneManager.SetActiveScene(loaded);
                yield return null;
                SceneManager.SetActiveScene(original);
                yield return SceneManager.UnloadSceneAsync(loaded);
                yield return OpenMenu();
                reset = menu.Find("Home/Reset Nimb memory");
                Assert.That(reset.gameObject.activeInHierarchy, Is.True, "Entry alone should unlock the action.");

                Directory.CreateDirectory(directory);
                string csv = Path.Combine(directory, "attempt.csv");
                File.WriteAllText(csv, "schema_version,encounter_id,cast_index,mode,chosen_attack,distance_m,in_cover,recent_cover_count,status,hit\n"
                    + $"1,{Guid.NewGuid():N},1,adaptive,EyeSeal,10,0,0,completed,1\n");
                var model = Type.GetType("NimbAttackModel, Assembly-CSharp", true);
                float Prediction()
                {
                    object current = model.GetMethod("Load", hidden).Invoke(null, null);
                    var predict = model.GetMethod("Predict", BindingFlags.Instance | BindingFlags.NonPublic);
                    return (float)predict.Invoke(current, new object[] {
                        Enum.Parse(predict.GetParameters()[0].ParameterType, "EyeSeal"), 10f, 0, false });
                }
                float initial = Prediction();
                var learning = Type.GetType("NimbAttackLearning, Assembly-CSharp", true);
                Assert.That(learning.GetMethod("ApplyEncounter", hidden).Invoke(null, new object[] { csv }), Is.True);
                float trained = Prediction();
                Assert.That(trained, Is.GreaterThan(initial));
                string checkpoint = PlayerPrefs.GetString(saveKey);
                string profile = Path.Combine(directory, "profile.json");
                byte[] before = File.ReadAllBytes(profile);

                // Reopening the menu and cancelling a reset both preserve the previous attempts.
                yield return SceneManager.UnloadSceneAsync(loaded);
                yield return OpenMenu();
                reset = menu.Find("Home/Reset Nimb memory");
                var panel = menu.Find("Nimb memory");
                CollectionAssert.AreEqual(before, File.ReadAllBytes(profile));
                Click(reset);
                Assert.IsTrue(panel.gameObject.activeInHierarchy);
                Assert.AreSame(panel.Find("Back").gameObject, EventSystem.current.currentSelectedGameObject);
                Click(panel.Find("Back"));
                Assert.IsTrue(reset.gameObject.activeInHierarchy);
                CollectionAssert.AreEqual(before, File.ReadAllBytes(profile));
                Click(reset);
                Click(panel.Find("Confirm"));
                Assert.AreEqual(initial, Prediction());
                Assert.That(PlayerPrefs.GetString(saveKey), Is.EqualTo(checkpoint));
                Assert.That(File.Exists(csv), Is.True);
                Click(panel.Find("Back"));
                Assert.IsTrue(reset.gameObject.activeInHierarchy);
            }
            finally
            {
                if (original.isLoaded) SceneManager.SetActiveScene(original);
                AppDomain.CurrentDomain.SetData(profileOverride, oldDirectory);
                if (oldProgress == -1) PlayerPrefs.DeleteKey(progressKey);
                else PlayerPrefs.SetInt(progressKey, oldProgress);
                if (hadSave) PlayerPrefs.SetString(saveKey, oldSave);
                else PlayerPrefs.DeleteKey(saveKey);
                PlayerPrefs.Save();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void Click(Transform target)
        {
            Assert.That(target, Is.Not.Null);
            var button = target.GetComponent<Button>();
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, target.name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
