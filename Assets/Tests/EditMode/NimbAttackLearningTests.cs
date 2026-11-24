using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Hackathon.Tests
{
    public sealed class NimbAttackLearningTests
    {
        private const string Override = "NimbLearning.TestDirectory";
        private const string Header = "schema_version,encounter_id,cast_index,mode,chosen_attack,distance_m,in_cover,recent_cover_count,status,hit";
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private string directory;
        private object previous;
        private MethodInfo load, apply, predict;
        private Type attackType;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "nimb-learning-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            previous = AppDomain.CurrentDomain.GetData(Override);
            AppDomain.CurrentDomain.SetData(Override, directory);
            var model = Type.GetType("NimbAttackModel, Assembly-CSharp", true);
            load = model.GetMethod("Load", Static);
            apply = Type.GetType("NimbAttackLearning, Assembly-CSharp", true).GetMethod("ApplyEncounter", Static);
            predict = model.GetMethod("Predict", Instance);
            attackType = predict.GetParameters()[0].ParameterType;
        }

        [TearDown]
        public void TearDown()
        {
            AppDomain.CurrentDomain.SetData(Override, previous);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private float Chance(object model, string attack) =>
            (float)predict.Invoke(model, new object[] { Enum.Parse(attackType, attack), 10f, 0, false });

        private string Encounter(params string[] rows)
        {
            string id = Guid.NewGuid().ToString("N");
            string path = Path.Combine(directory, id + ".csv");
            using var writer = new StreamWriter(path);
            writer.WriteLine(Header);
            for (int i = 0; i < rows.Length; i++) writer.WriteLine($"1,{id},{i + 1},{rows[i]}");
            return path;
        }

        private bool Apply(string path) => (bool)apply.Invoke(null, new object[] { path });
        private object Load() => load.Invoke(null, null);

        [Test]
        public void ConfirmedHitsAndMissesUpdateFutureEncountersOnce()
        {
            object live = Load();
            float eye = Chance(live, "EyeSeal"), wave = Chance(live, "HaloWave");
            string path = Encounter("adaptive,EyeSeal,10,0,0,interrupted,1", "adaptive,HaloWave,10,0,0,completed,0",
                "fixed,HeadDash,10,0,0,completed,1", "adaptive,HeadDash,10,0,0,interrupted,",
                "combo,LightArrows,10,0,0,completed,1", "model_fallback,HandTouch,10,0,0,completed,1");
            Assert.IsTrue(Apply(path));
            Assert.IsFalse(Apply(path));
            Assert.AreEqual(eye, Chance(live, "EyeSeal"), "A running fight retains its snapshot.");
            var next = Load();
            Assert.Greater(Chance(next, "EyeSeal"), eye);
            Assert.Less(Chance(next, "HaloWave"), wave);
            foreach (string unchanged in new[] { "HeadDash", "LightArrows", "HandTouch" })
                Assert.AreEqual(Chance(live, unchanged), Chance(next, unchanged));
            Assert.IsTrue(Apply(Encounter("adaptive,EyeSeal,10,0,0,completed,1")));
            Assert.Greater(Chance(Load(), "EyeSeal"), Chance(next, "EyeSeal"));
            Assert.IsFalse(Apply(path), "An older encounter must remain deduplicated after a second commit.");
        }

        [Test]
        public void ResetRestoresPackagedWeightsAndLearningCanStartAgain()
        {
            var reset = apply.DeclaringType.GetMethod("TryResetToPackaged", Static);
            Assert.IsTrue((bool)reset.Invoke(null, null));
            var baseline = Load();
            string previousFight = Encounter("adaptive,EyeSeal,10,0,0,completed,1", "adaptive,HaloWave,10,0,0,completed,0");
            Assert.IsTrue(Apply(previousFight));
            Assert.Greater(Chance(Load(), "EyeSeal"), Chance(baseline, "EyeSeal"));

            Assert.IsTrue((bool)reset.Invoke(null, null));
            Assert.IsFalse(Apply(previousFight));
            var fresh = Load();
            foreach (string attack in Enum.GetNames(attackType))
                Assert.AreEqual(Chance(baseline, attack), Chance(fresh, attack));
            Assert.IsTrue(Apply(Encounter("adaptive,EyeSeal,10,0,0,completed,1")));
            Assert.Greater(Chance(Load(), "EyeSeal"), Chance(baseline, "EyeSeal"));
        }

        [Test]
        public void ChangingPackagedRevisionStartsFromFreshWeights()
        {
            float baseline = Chance(Load(), "EyeSeal");
            string path = Encounter("adaptive,EyeSeal,10,0,0,completed,1");
            Assert.IsTrue(Apply(path));
            string profile = Path.Combine(directory, "profile.json");
            string json = File.ReadAllText(profile);
            json = System.Text.RegularExpressions.Regex.Replace(json,
                "(\"baseRevision\"\\s*:\\s*\")[^\"]+", "$1different-bundle");
            File.WriteAllText(profile, json);
            Assert.AreEqual(baseline, Chance(Load(), "EyeSeal"));
            Assert.IsTrue(Apply(path), "A new base model has its own training history.");
            Assert.Greater(Chance(Load(), "EyeSeal"), baseline);
        }

        [Test]
        public void MissingLabelsAndFixedOrderLeaveNoLearningProfile()
        {
            string path = Encounter("fixed,EyeSeal,10,0,0,completed,1", "adaptive,HaloWave,10,0,0,interrupted,",
                "adaptive,HeadDash,10,0,0,pending,1", "adaptive,HandTouch,10,0,0,interrupted,0");
            Assert.IsFalse(Apply(path));
            Assert.IsFalse(File.Exists(Path.Combine(directory, "profile.json")));
        }
    }
}
