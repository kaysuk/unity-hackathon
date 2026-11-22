using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hackathon.Tests
{
    public sealed class GameHudTests
    {
        [UnityTest]
        public IEnumerator ObjectivesAmmoAndInjuryReflectTheirCurrentState()
        {
            var root = new GameObject("HUD under test", typeof(RectTransform));
            try
            {
                var type = Type.GetType("GameHud, Assembly-CSharp", true);
                var hud = root.AddComponent(type);
                Transform objects = root.transform;
                var rim = objects.Find("Blood rim").GetComponent<RawImage>();
                Transform objectives = objects.Find("Objectives");
                Transform ammo = objects.Find("Pistol ammunition");
                Assert.That(rim.color.a, Is.Zero);

                const string primaryText = "Добраться до ритуального зала и проверить, что длинная задача полностью помещается в интерфейсе";
                const string secondaryText = "Собрать тайные документы и проверить перенос дополнительной задачи на следующую строку";
                var setObjectives = type.GetMethod("SetObjectives");
                setObjectives.Invoke(hud, new object[] { primaryText, secondaryText });
                type.GetMethod("SetAmmo").Invoke(hud, new object[] { 0, 24, true, true });
                yield return null;
                Canvas.ForceUpdateCanvases();

                var menuFont = Resources.Load<Font>("KaramazovyUI/CormorantGaramond-SemiBold");
                Assert.That(menuFont, Is.Not.Null);
                Assert.That(menuFont.HasCharacter('Ж'), Is.True);
                Assert.That(objectives.gameObject.activeInHierarchy, Is.True);
                Assert.That(ammo.gameObject.activeInHierarchy, Is.True);

                var primary = objectives.Find("Primary objective").GetComponent<Text>();
                var secondary = objectives.Find("Additional objective").GetComponent<Text>();
                var ammoLabel = ammo.Find("Magazine and reserve").GetComponent<Text>();
                var ammoCaption = ammo.Find("Weapon or reload status").GetComponent<Text>();
                foreach (Text label in new[] { primary, secondary, ammoLabel, ammoCaption })
                {
                    Assert.That(label.isActiveAndEnabled, Is.True);
                    Assert.That(label.font, Is.SameAs(menuFont));
                    Assert.That(label.rectTransform.rect.height + 1f, Is.GreaterThanOrEqualTo(label.preferredHeight),
                        $"{label.name} needs enough height to display its text.");
                }

                Assert.That(primary.text, Is.EqualTo(primaryText));
                Assert.That(secondary.text, Is.EqualTo(secondaryText));
                Assert.That(primary.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
                Assert.That(primary.preferredWidth, Is.GreaterThan(primary.rectTransform.rect.width));
                Assert.That(ammoLabel.text, Does.Contain("/ 24"));
                Assert.That(ammoCaption.text, Is.EqualTo("Перезарядка…"));
                Assert.That(ammoLabel.rectTransform.rect.width + 1f, Is.GreaterThanOrEqualTo(ammoLabel.preferredWidth));
                Assert.That(ammoCaption.rectTransform.rect.width + 1f, Is.GreaterThanOrEqualTo(ammoCaption.preferredWidth));

                var objectivesRect = (RectTransform)objectives;
                float twoObjectiveHeight = objectivesRect.rect.height;
                setObjectives.Invoke(hud, new object[] { primaryText, string.Empty });
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(secondary.gameObject.activeSelf, Is.False);
                Assert.That(primary.isActiveAndEnabled, Is.True);
                Assert.That(objectivesRect.rect.height, Is.LessThan(twoObjectiveHeight));

                type.GetMethod("SetHealth").Invoke(hud, new object[] { 75f, 100f, true });
                float hitOpacity = rim.color.a;
                type.GetMethod("SetHealth").Invoke(hud, new object[] { 75f, 100f, false });
                float lightInjury = rim.color.a;
                type.GetMethod("SetHealth").Invoke(hud, new object[] { 20f, 100f, false });
                Assert.That(hitOpacity, Is.GreaterThan(lightInjury));
                Assert.That(rim.color.a, Is.GreaterThan(lightInjury));

                type.GetMethod("SetHealth").Invoke(hud, new object[] { 100f, 100f, false });
                type.GetMethod("SetAmmo").Invoke(hud, new object[] { 0, 0, false, true });
                Assert.That(rim.color.a, Is.Zero);
                Assert.That(ammo.gameObject.activeInHierarchy, Is.True);
                Assert.That(ammoLabel.text, Does.Contain("/ 0"));
                type.GetMethod("SetGameplayVisible").Invoke(hud, new object[] { false });
                Assert.That(root.GetComponent<Canvas>().enabled, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
