using System;
using NUnit.Framework;

namespace Hackathon.Tests
{
    public sealed class GameObjectivesTests
    {
        private static Type Objectives => Type.GetType("GameObjectives, Assembly-CSharp", throwOnError: true);

        private static object Call(string method, params object[] arguments) =>
            Objectives.GetMethod(method).Invoke(null, arguments);

        private static string Text(string name) => (string)Objectives.GetProperty(name).GetValue(null);

        private static void EnterStage(string name) =>
            Call("EnterStage", Enum.Parse(Objectives.GetNestedType("Stage"), name));

        [SetUp]
        [TearDown]
        public void ClearObjectives() => Call("ConfigureScene", "SampleScene");

        [Test]
        public void StoryEventUpdatesTheObjectiveWithinTheSameScene()
        {
            EnterStage("GetReadyForWork");
            Assert.That(Text("Primary"), Is.EqualTo("Собраться на службу"));

            EnterStage("AskDmitry");
            Assert.That(Text("Primary"), Is.EqualTo("Попросить Дмитрия о помощи"));
            EnterStage("InspectLetter");
            Assert.That(Text("Primary"), Is.EqualTo("Осмотреть письмо"));
            Assert.That(Text("Secondary"), Is.Empty);
        }

        [Test]
        public void ArchiveObjectiveRemainsAvailableUntilTheDocumentsAreCollected()
        {
            Call("ConfigureScene", "temple_v2");
            Assert.That(Text("Primary"), Is.EqualTo("Найти путь к главному залу"));
            Assert.That(Text("Secondary"), Is.Empty);

            Call("ReachArchive");
            Assert.That(Text("Primary"), Is.EqualTo("Добраться до ритуального зала"));
            Assert.That(Text("Secondary"), Is.EqualTo("Собрать тайные документы"));
            Call("ReachRitualEntrance");
            Assert.That(Text("Primary"), Is.EqualTo("Войти в зал"));

            Call("SetArchiveProgress", 6);
            Assert.That(Text("Secondary"), Is.EqualTo("Собрать тайные документы"));
            object checkpoint = Call("Capture");
            Call("SetArchiveProgress", 7);
            Assert.That(Text("Secondary"), Is.Empty);
            Call("Restore", checkpoint);
            Assert.That(Text("Primary"), Is.EqualTo("Войти в зал"));
            Assert.That(Text("Secondary"), Is.EqualTo("Собрать тайные документы"));
        }

        [Test]
        public void AltarObjectiveSwitchesAtSixAndRestoresCheckpointCount()
        {
            EnterStage("DestroyAltars");
            Assert.That(Text("Primary"), Is.EqualTo("Уничтожить алтари 0 из 6"));
            Call("SetAltarsDestroyed", 5);
            Assert.That(Text("Primary"), Is.EqualTo("Уничтожить алтари 5 из 6"));
            object checkpoint = Call("Capture");

            Call("SetAltarsDestroyed", 6);
            Assert.That(Text("Primary"), Is.EqualTo("Добить существо"));
            Call("Restore", checkpoint);
            Assert.That(Text("Primary"), Is.EqualTo("Уничтожить алтари 5 из 6"));
        }
    }
}
