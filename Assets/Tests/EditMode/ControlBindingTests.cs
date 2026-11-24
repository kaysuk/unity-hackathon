using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Hackathon.Tests
{
    public sealed class ControlBindingTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string Preference = "Controls.KeyboardMouse.Overrides.v1";
        private static readonly Type Bindings = Type.GetType("GameBindings, Assembly-CSharp", true);

        private bool hadPreference;
        private string previousPreference;

        private static object Call(string method, params object[] arguments) =>
            Bindings.GetMethod(method, Static).Invoke(null, arguments);
        private static string Label(string path, bool pad = false) => (string)Call("Label", path, pad);
        private static object Entry(string label) =>
            ((IEnumerable)Bindings.GetProperty("Entries").GetValue(null)).Cast<object>()
                .First(entry => (string)entry.GetType().GetProperty("Label").GetValue(entry) == label);

        [SetUp]
        public void SaveUserBindings()
        {
            hadPreference = PlayerPrefs.HasKey(Preference);
            previousPreference = PlayerPrefs.GetString(Preference, "");
            Call("ResetDefaults");
        }

        [TearDown]
        public void RestoreUserBindings()
        {
            Call("ResetDefaults");
            if (!hadPreference) return;
            PlayerPrefs.SetString(Preference, previousPreference);
            PlayerPrefs.Save();
            Call("LoadSaved");
        }

        [Test]
        public void AimCanBeReboundAcrossDevicesAndRoundTripWithoutChangingGamepad()
        {
            object aim = Entry("Прицелиться");
            Assert.That(Label("Player/Aim"), Is.EqualTo("Q"));
            Assert.That(Label("Player/Attack"), Is.EqualTo("ЛКМ"));
            Assert.That(Call("Apply", aim, "<Mouse>/middleButton"), Is.Null);
            Assert.That(Label("Player/Aim"), Is.EqualTo("СКМ"));
            Assert.That(Label("Player/Aim", true), Is.EqualTo("LT"));
            string saved = PlayerPrefs.GetString(Preference);
            Assert.That(saved, Does.Contain("<Mouse>/middleButton"));

            Call("ResetDefaults");
            Assert.That(Label("Player/Aim"), Is.EqualTo("Q"));
            PlayerPrefs.SetString(Preference, saved);
            Call("LoadSaved");
            Assert.That(Label("Player/Aim"), Is.EqualTo("СКМ"));
        }

        [Test]
        public void ConflictsIncludeAlternateButtonsButPermitSeparateContexts()
        {
            object sense = Entry("Чутьё детектива");
            Assert.That(Label("Player/Investigate"), Is.EqualTo("V"));
            Assert.That(Label("Player/Investigate", true), Is.EqualTo("LB"));
            Assert.That(Call("Apply", sense, "<Keyboard>/q"), Is.Not.Null);
            Assert.That(Call("Apply", Entry("Прицелиться"), "<Keyboard>/v"), Is.Not.Null);
            object melee = Entry("Удар рукоятью");
            Assert.That(Call("Apply", melee, "<Mouse>/rightButton"), Is.Not.Null, "Right click also aims.");
            Assert.That(Label("Player/Melee"), Is.EqualTo("F"));
            Assert.That(Call("Apply", melee, "<Keyboard>/f"), Is.Null, "Holstered and drawn attacks can share F.");
            Assert.That(Call("Apply", melee, "<Keyboard>/k"), Is.Null);
            Assert.That(Call("Apply", Entry("Листать влево"), "<Keyboard>/k"), Is.Null);
            Assert.That(Call("Apply", Entry("Открыть журнал"), "<Keyboard>/k"), Is.Not.Null);
            Assert.That(Label("Player/Melee"), Is.EqualTo("K"));
            Assert.That(Label("Overlay/Left"), Is.EqualTo("K"));
        }
    }
}
