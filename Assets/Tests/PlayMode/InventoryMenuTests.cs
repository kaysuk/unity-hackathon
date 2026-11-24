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
    public sealed class InventoryMenuTests
    {
        private Scene scene;
        private Component player, inventory, menu;
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator LoadTemple()
        {
            previousTimeScale = Time.timeScale;
            yield return SceneManager.LoadSceneAsync("temple_v2", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("temple_v2");
            player = Components("PlayerInteractor").Single();
            inventory = player.GetComponent("PlayerInventory");
            menu = player.GetComponent("InventoryMenu");
        }

        [UnityTearDown]
        public IEnumerator UnloadTemple()
        {
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator InventoryTracksPickupsSpentKeysAndReturnedStones()
        {
            Call(menu, "Open");
            Assert.AreEqual("Пока пусто", Label("Name"));
            Assert.AreEqual(0f, Time.timeScale);
            var overlay = (Behaviour)menu;
            overlay.enabled = false;
            overlay.enabled = true;
            Assert.AreEqual(previousTimeScale, Time.timeScale);

            foreach (string id in new[] { "m0_key_room3", "m0_key_room4", "rune_room_02" })
                Call(Components("ItemPickup").Single(p => (string)Property(p, "ItemId") == id), "Interact", player);

            Call(menu, "Open");
            Assert.AreEqual("Рунный камень 2", Label("Name"));
            Assert.AreEqual("1 из 3", Label("Position"));
            Call(menu, "MoveSelection", 1);
            Assert.AreEqual("Левый ключ М0", Label("Name"));
            StringAssert.Contains("левое гнездо", Label("Description"));
            Call(menu, "MoveSelection", 1);
            Assert.AreEqual("Правый ключ М0", Label("Name"));
            Call(menu, "Close");
            Assert.IsTrue((bool)Call(inventory, "TryUsePair", "m0_key_room3", "m0_key_room4"));
            Assert.IsTrue((bool)Call(inventory, "TryUse", "rune_room_02"));
            Call(menu, "Open");
            Assert.AreEqual("Пока пусто", Label("Name"));
            Call(menu, "Close");
            Assert.IsTrue((bool)Call(inventory, "TryReturn", "rune_room_02"));
            Call(menu, "Open");
            Assert.AreEqual("Рунный камень 2", Label("Name"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SevenArchiveNotesCanBeRereadAndRevealTheRegister()
        {
            var sheets = Components("TempleSealDocument").OrderBy(s => s.name).ToArray();
            Assert.AreEqual(6, sheets.Length);
            var register = Components("TempleArchiveDocument").Single();
            Assert.AreEqual(7, Property(register, "DocumentNumber"));
            Call(sheets[0], "Interact", player);
            Call(sheets[5], "Interact", player);
            Call(register, "Interact", player);
            var journal = player.GetComponent("TempleArchiveJournal");
            string PageText() => (string)Property(journal, "VisibleDocumentText");
            StringAssert.Contains("Архивный код", PageText());
            StringAssert.DoesNotContain("Алексей Карамазов", PageText());
            Call(journal, "Close");

            Call(menu, "Open");
            Call(menu, "ChangeSection", -1);
            Assert.AreEqual("Иерархия Шести", Label("Name"));
            StringAssert.Contains("по порядку комнат", Label("Description"));
            Call(menu, "ReadSelected");
            StringAssert.Contains("Внутренний устав культа", PageText());
            Assert.AreEqual(3, Property(journal, "DocumentCount"));
            Call(journal, "Close");
            Assert.IsTrue((bool)Property(menu, "IsOpen"));
            Assert.AreEqual(0f, Time.timeScale);
            Call(menu, "MoveSelection", 1);
            Assert.AreEqual("Подготовка вместилища", Label("Name"));
            StringAssert.Contains("Шестая луна одинока.", Label("Description"));
            Call(menu, "ReadSelected");
            StringAssert.Contains("Медицинский журнал программы", PageText());
            Call(journal, "Close");
            Call(menu, "MoveSelection", 1);
            Call(menu, "ReadSelected");
            StringAssert.Contains("Архивный код", PageText());
            Assert.IsFalse((bool)Property(journal, "FullFamilyTruth"));
            Call(journal, "Close");
            Call(menu, "Close");

            foreach (var sheet in sheets.Skip(1).Take(4))
                Call(sheet, "Interact", player);
            StringAssert.StartsWith("Документы 7/7", (string)Call(sheets[0], "GetPrompt", player, "E"));

            Call(menu, "Open");
            Call(menu, "ChangeSection", 0);
            string[] passages =
            {
                "Внутренний устав культа",
                "Конфиденциальный отчёт о деятельности внешних предприятий",
                "Исследовательский протокол внутренней лаборатории",
                "Перевод надписи с внутренней стороны древнего алтарного камня",
                "Секретное досье наблюдения",
                "Медицинский журнал программы",
                "Конец записи."
            };
            foreach (string passage in passages)
            {
                Call(menu, "ReadSelected");
                StringAssert.Contains(passage, PageText());
                StringAssert.DoesNotContain("Пометка для размещения в игре", PageText());
                Call(journal, "Close");
                Call(menu, "MoveSelection", 1);
            }
            Assert.AreEqual(7, Property(journal, "DocumentCount"));
            Assert.IsTrue((bool)Property(journal, "FullFamilyTruth"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator KeyboardAndGamepadCanOpenNavigateAndClose()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = InputSystem.AddDevice<Gamepad>();
            var settings = InputSystem.settings;
            var previousInput = settings.editorInputBehaviorInPlayMode;
            var previousBackground = settings.backgroundBehavior;
            try
            {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.EnableDevice(keyboard);
                InputSystem.EnableDevice(pad);

                void Poll<T>(InputDevice device, T state) where T : struct, IInputStateTypeInfo
                {
                    InputSystem.QueueStateEvent(device, state);
                    InputSystem.Update();
                    Call(menu, "HandleInput");
                    InputSystem.QueueStateEvent(device, default(T));
                    InputSystem.Update();
                }

                Poll(keyboard, new KeyboardState(Key.Tab));
                Assert.IsTrue((bool)Property(menu, "IsOpen"));
                yield return null;
                Poll(keyboard, new KeyboardState(Key.RightArrow));
                StringAssert.Contains("записки", Label("Description"));
                Poll(keyboard, new KeyboardState(Key.Escape));
                Assert.IsFalse((bool)Property(menu, "IsOpen"));
                Poll(pad, new GamepadState().WithButton(GamepadButton.Start));
                Assert.IsTrue((bool)Property(menu, "IsOpen"));
                yield return null;
                Poll(pad, new GamepadState().WithButton(GamepadButton.DpadRight));
                StringAssert.Contains("камни и ключи", Label("Description"));
                Poll(pad, new GamepadState().WithButton(GamepadButton.East));
                Assert.IsFalse((bool)Property(menu, "IsOpen"));
                Assert.AreEqual(previousTimeScale, Time.timeScale);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(pad);
                settings.editorInputBehaviorInPlayMode = previousInput;
                settings.backgroundBehavior = previousBackground;
            }
        }

        [UnityTest]
        public IEnumerator LegacyCheckpointKeepsRoomSixRegisterAndCalculationDistinct()
        {
            const string json = "{\"version\":1,\"archiveDocuments\":32,\"inventory\":[{\"id\":\"m0_key_room3\",\"held\":false}," +
                "{\"id\":\"seal_document_06\",\"held\":true}],\"rotation\":{\"w\":1}," +
                "\"sealCalculation\":\"Решение из контрольной точки\"}";
            ApplyCheckpoint(json);
            Call(menu, "Open");
            Assert.AreEqual("Пока пусто", Label("Name"));
            Call(menu, "ChangeSection", 1);
            Assert.AreEqual("Подготовка вместилища", Label("Name"));
            Call(menu, "MoveSelection", 1);
            Call(menu, "ReadSelected");
            var journal = player.GetComponent("TempleArchiveJournal");
            StringAssert.Contains("Архивный код", (string)Property(journal, "VisibleDocumentText"));
            Assert.IsFalse((bool)Property(journal, "FullFamilyTruth"));
            Call(journal, "Close");
            Call(menu, "MoveSelection", 1);
            Assert.AreEqual("Расчёт шести печатей", Label("Name"));
            Call(menu, "ReadSelected");
            Assert.AreEqual("Решение из контрольной точки", Property(journal, "VisibleDocumentText"));
            Assert.AreEqual(2, Property(journal, "DocumentCount"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreviouslyRevealedEndingRemainsReadableAfterCheckpointUpgrade()
        {
            ApplyCheckpoint("{\"version\":1,\"archiveDocuments\":63,\"fullFamilyTruth\":true,\"rotation\":{\"w\":1}}");
            var journal = player.GetComponent("TempleArchiveJournal");
            Assert.IsTrue((bool)Property(journal, "FullFamilyTruth"));
            Assert.AreEqual(7, Property(journal, "DocumentCount"));
            Call(menu, "Open");
            Call(menu, "ChangeSection", 1);
            Call(menu, "MoveSelection", -1);
            Call(menu, "ReadSelected");
            StringAssert.Contains("Конец записи.", (string)Property(journal, "VisibleDocumentText"));
            yield return null;
        }

        private void ApplyCheckpoint(string json)
        {
            var checkpoint = System.Type.GetType("GameCheckpoint, Assembly-CSharp", true);
            var snapshotType = checkpoint.GetNestedType("Snapshot", BindingFlags.NonPublic);
            var snapshot = JsonUtility.FromJson(json, snapshotType);
            var objectives = System.Type.GetType("GameObjectives, Assembly-CSharp", true);
            snapshotType.GetField("objectives").SetValue(snapshot, objectives.GetMethod("Capture").Invoke(null, null));
            checkpoint.GetMethod("ApplySave", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new[] { snapshot, player.gameObject });
        }

        private Component[] Components(string type) => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
            .Where(c => c != null && c.GetType().Name == type).ToArray();

        private string Label(string name) => (string)Property(Components("Text")
            .Single(c => c.name == name && c.gameObject.activeInHierarchy && c.transform.root.name == "Inventory"), "text");

        private static object Property(Component target, string property) =>
            target.GetType().GetProperty(property).GetValue(target);

        private static object Call(Component target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(target, args);
    }
}
