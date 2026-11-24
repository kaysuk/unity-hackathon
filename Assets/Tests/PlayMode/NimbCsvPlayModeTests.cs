using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class NimbCsvPlayModeTests
    {
        private const string FinalRoom = "Assets/Scenes/Levels/Temple_Final_Room.unity";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<string> testFiles = new List<string>();
        private Scene room;
        private Behaviour boss;
        private Component health;
        private Component encounter;
        private CharacterController player;
        private float previousCaptureStep;

        public enum HaloMode { Adaptive, ModelFallback, Fixed }

        private static readonly HaloMode[] HaloModes = { HaloMode.Adaptive, HaloMode.ModelFallback, HaloMode.Fixed };
        private static readonly bool[] BooleanCases = { false, true };

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            previousCaptureStep = Time.captureDeltaTime;
            yield return SceneManager.LoadSceneAsync(FinalRoom, LoadSceneMode.Additive);
            room = SceneManager.GetSceneByPath(FinalRoom);
            var roots = room.GetRootGameObjects();
            boss = (Behaviour)roots.Select(root => root.GetComponent("AngelSealAttack")).Single(item => item != null);
            var ivan = roots.Single(root => root.CompareTag("Player"));
            health = ivan.GetComponent("CombatHealth");
            player = ivan.GetComponent<CharacterController>();
            encounter = roots.Select(root => root.GetComponent("NimbEncounter")).Single(item => item != null);
            foreach (var guard in roots.Where(root => root.GetComponent("RifleNpc") != null))
                guard.SetActive(false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeathUpdatesTheNextModelAndIncludesTheFatalHit()
        {
            const string setting = "NimbLearning.TestDirectory";
            string directory = Path.Combine(Application.temporaryCachePath, "nimb-learning-" + Guid.NewGuid().ToString("N"));
            var previous = AppDomain.CurrentDomain.GetData(setting);
            AppDomain.CurrentDomain.SetData(setting, directory);
            try
            {
                StartCast();
                string csv = CaptureFilePath();
                var decision = Read(csv).Single();
                Assert.That(decision["mode"], Is.EqualTo("adaptive"));
                var current = Field("attackModel").GetValue(boss);
                var predict = current.GetType().GetMethod("Predict", Members);
                var features = new object[] {
                    Enum.Parse(predict.GetParameters()[0].ParameterType, decision["chosen_attack"]),
                    Number(decision["distance_m"]), int.Parse(decision["recent_cover_count"]), decision["in_cover"] == "1"
                };
                float Chance(object model) => (float)predict.Invoke(model, features);
                float before = Chance(current);
                string profile = Path.Combine(directory, "profile.json");
                health.GetType().GetMethod("TakeDamage").Invoke(health,
                    new object[] { 1f, player.transform.position, Vector3.up, player.gameObject });
                Assert.IsFalse(File.Exists(profile), "A nonlethal hit must not change the live model.");
                health.GetType().GetMethod("TakeDamage").Invoke(health,
                    new object[] { 1000f, player.transform.position, Vector3.up, boss.gameObject });
                var last = Read(csv).Single();
                Assert.That(last["status"], Is.EqualTo("interrupted"));
                Assert.That(last["hit"], Is.EqualTo("1"));
                Assert.That(File.Exists(profile), Is.True);
                Assert.That(Chance(current), Is.EqualTo(before));
                var next = current.GetType().GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                Assert.That(Chance(next), Is.GreaterThan(before));
                var saved = File.ReadAllBytes(profile);
                boss.GetType().GetMethod("LearnFromAttempt", Members).Invoke(boss, null);
                CollectionAssert.AreEqual(saved, File.ReadAllBytes(profile));
                yield return null;
                Assert.IsFalse(boss.enabled, "Death must still stop the active attack effects.");
            }
            finally
            {
                AppDomain.CurrentDomain.SetData(setting, previous);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator WinningKeepsWeightsAndClosesTheCsv([ValueSource(nameof(BooleanCases))] bool hasSavedWeights)
        {
            const string setting = "NimbLearning.TestDirectory";
            string directory = Path.Combine(Application.temporaryCachePath, "nimb-victory-" + Guid.NewGuid().ToString("N"));
            var previous = AppDomain.CurrentDomain.GetData(setting);
            AppDomain.CurrentDomain.SetData(setting, directory);
            try
            {
                PlaceInOpenArena();
                string csv = StartHalo(HaloMode.Adaptive);
                AdvanceCast(WaveDuration() + .05f);
                var first = Read(csv).Single();
                Assert.That(first["status"], Is.EqualTo("completed"));
                Assert.That(first["hit"], Is.EqualTo("1"));

                string profile = Path.Combine(directory, "profile.json");
                if (hasSavedWeights)
                {
                    // Seed a real learned profile with a different encounter ID from the winning fight.
                    Directory.CreateDirectory(directory);
                    string priorAttempt = Path.Combine(directory, "prior.csv");
                    using var liveCsv = new FileStream(csv, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(liveCsv);
                    File.WriteAllText(priorAttempt, reader.ReadToEnd()
                        .Replace(first["encounter_id"], Guid.NewGuid().ToString("N")));
                    var learning = Type.GetType("NimbAttackLearning, Assembly-CSharp", true);
                    Assert.That(learning.GetMethod("ApplyEncounter", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { priorAttempt }), Is.EqualTo(true));
                }
                byte[] saved = hasSavedWeights ? File.ReadAllBytes(profile) : null;
                void AssertProfilePreserved()
                {
                    Assert.That(File.Exists(profile), Is.EqualTo(hasSavedWeights));
                    if (hasSavedWeights) CollectionAssert.AreEqual(saved, File.ReadAllBytes(profile));
                }

                StartCast();
                SetConfirmedAltars(5);
                var active = encounter.GetType().GetProperty("ActiveAltar", Members).GetValue(encounter);
                encounter.GetType().GetMethod("OnAltarBroken", Members).Invoke(encounter, new[] { active });
                AssertProfilePreserved();

                var target = boss.GetComponent("NimbVulnerability");
                var strike = target.GetType().GetMethod("TryPistolStrike", Members);
                Assert.That(strike.Invoke(target, new object[] { player.gameObject }), Is.EqualTo(true));
                Assert.That(encounter.GetType().GetProperty("ConfirmedAltars", Members).GetValue(encounter), Is.EqualTo(6));
                Assert.That(encounter.GetComponent("NimbVictoryComic"), Is.Not.Null);
                AssertProfilePreserved();
                var rows = Read(csv);
                Assert.That(rows.Length, Is.EqualTo(2));
                Assert.That(rows[0]["status"], Is.EqualTo("completed"));
                Assert.That(rows[0]["hit"], Is.EqualTo("1"));
                Assert.That(rows[1]["status"], Is.EqualTo("interrupted"));
                Assert.That(rows[1]["hit"], Is.Empty);
                Assert.That(Field("attackCsv").GetValue(boss), Is.Null);

                var recorded = File.ReadAllBytes(csv);
                boss.GetType().GetMethod("FinishEncounter", Members).Invoke(boss, null);
                var checkpoint = Type.GetType("GameCheckpoint, Assembly-CSharp", true);
                checkpoint.GetMethod("EndArenaAttempt", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { room });
                AssertProfilePreserved();
                CollectionAssert.AreEqual(recorded, File.ReadAllBytes(csv));
            }
            finally
            {
                AppDomain.CurrentDomain.SetData(setting, previous);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            yield break;
        }

        private FieldInfo Field(string name) => boss.GetType().GetField(name, Members);

        private void Tick() => boss.GetType().GetMethod("TickAttack", Members).Invoke(boss, null);

        private void StartCast()
        {
            Field("engaged").SetValue(boss, true);
            Field("nextCast").SetValue(boss, Time.time);
            Tick();
        }

        private void StartSeal(float damage)
        {
            NimbAttackSelectionTests.UseFixedOrder(boss);
            var next = Field("nextAttack");
            next.SetValue(boss, Enum.Parse(next.FieldType, "EyeSeal"));
            Field("damage").SetValue(boss, damage);
            StartCast();
        }

        private void AdvanceCast(float elapsed)
        {
            Field("castStart").SetValue(boss, Time.time - elapsed);
            Tick();
        }

        private string CaptureFilePath()
        {
            var csv = Field("attackCsv").GetValue(boss);
            var path = (string)csv.GetType().GetProperty("FilePath", Members).GetValue(csv);
            testFiles.Add(path);
            return path;
        }

        private static Dictionary<string, string>[] Read(string path)
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(file);
            var lines = reader.ReadToEnd().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var headers = lines.First().TrimEnd('\r').Split(',');
            return lines.Skip(1).Select(line =>
            {
                var values = line.TrimEnd('\r').Split(',');
                Assert.AreEqual(headers.Length, values.Length);
                return headers.Select((name, i) => (name, value: values[i])).ToDictionary(x => x.name, x => x.value);
            }).ToArray();
        }

        private static float Number(string value) => float.Parse(value, CultureInfo.InvariantCulture);

        [UnityTest]
        public IEnumerator WeightedDecisionIsReadableDuringWindupAndInterruptionKeepsTheLabelUnknown()
        {
            var rng = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(792);
                StartCast();
            }
            finally { UnityEngine.Random.state = rng; }
            string path = CaptureFilePath();
            var row = Read(path).Single();
            Assert.AreEqual("adaptive", row["mode"]);
            Assert.AreEqual("pending", row["status"]);
            Assert.AreEqual("", row["hit"]);
            Assert.AreEqual("weighted_draw", row["choice_reason"]);

            float roll = Number(row["roll_01"]);
            Assert.That(roll, Is.GreaterThanOrEqualTo(Number(row["roll_from"])));
            Assert.That(roll, Is.LessThan(Number(row["roll_to"])));
            float cumulative = 0f;
            string sampled = null;
            string lastEligible = null;
            foreach (string attack in Enum.GetNames(Field("nextAttack").FieldType))
            {
                Assert.That(Number(row[attack + "_hit_probability"]), Is.InRange(0f, 1f));
                float chance = Number(row[attack + "_selection_probability"]);
                Assert.That(chance, Is.InRange(0f, 1f));
                if (chance > 0f) lastEligible = attack;
                cumulative += chance;
                if (sampled == null && roll < cumulative && chance > 0f) sampled = attack;
            }
            Assert.That(cumulative, Is.EqualTo(1f).Within(0.00001f));
            Assert.AreEqual(sampled ?? lastEligible, row["chosen_attack"]);
            Assert.AreEqual(Field("lastAttack").GetValue(boss).ToString(), row["chosen_attack"]);

            boss.enabled = false;
            row = Read(path).Single();
            Assert.AreEqual("interrupted", row["status"]);
            Assert.AreEqual("", row["hit"]);
            yield break;
        }

        [UnityTest]
        public IEnumerator EachFixedCastHasOneRowAndOnlyDamageFromNimbLabelsItAsAHit()
        {
            StartSeal(10f);
            string path = CaptureFilePath();
            AdvanceCast(1.5f);
            var hit = Read(path).Single();
            Assert.AreEqual("pending", hit["status"]);
            Assert.AreEqual("1", hit["hit"], "Ivan stayed inside the first rune.");
            Assert.AreEqual("fixed", hit["mode"]);
            Assert.AreEqual("fixed_order", hit["choice_reason"]);
            Assert.AreEqual("", hit["roll_01"]);
            Assert.AreEqual("1", hit["EyeSeal_selection_probability"]);
            Assert.AreEqual("0", hit["HaloWave_selection_probability"]);
            AdvanceCast(6f);
            Assert.AreEqual("completed", Read(path).Single()["status"]);

            StartSeal(0f);
            health.GetType().GetMethod("TakeDamage").Invoke(health,
                new object[] { 1f, health.transform.position, Vector3.up, health.gameObject });
            AdvanceCast(6f);
            var rows = Read(path);
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("2", rows[1]["cast_index"]);
            Assert.AreEqual("completed", rows[1]["status"]);
            Assert.AreEqual("0", rows[1]["hit"], "Neither foreign damage nor a zero-damage rune is a boss hit.");

            StartSeal(10f);
            AdvanceCast(1.5f);
            boss.enabled = false;
            var last = Read(path).Last();
            Assert.AreEqual("interrupted", last["status"]);
            Assert.AreEqual("1", last["hit"], "Damage already observed survives a mid-attack shutdown.");
            yield break;
        }

        [UnityTest]
        public IEnumerator CompletedAdaptiveHaloAddsOneDodgeableSealAtFourAltars()
        {
            Time.captureDeltaTime = .04f;
            SetConfirmedAltars(4);
            PlaceInOpenArena();
            string path = StartHalo(HaloMode.Adaptive);
            Assert.AreEqual("adaptive", Read(path).Single()["mode"]);

            var seals = boss.GetComponentsInChildren<LineRenderer>(true).Where(rune => rune.name == "Seal boundary").ToArray();
            var impact = boss.GetComponentsInChildren<MeshRenderer>(true).Single(view => view.name == "Seal light pillar");
            float waveStarted = Time.time;
            float waveDuration = WaveDuration();
            while (IsSweeping() && Time.time < waveStarted + waveDuration + .35f)
            {
                Assert.IsFalse(seals.Any(rune => rune.enabled), "No rune is cast while the halo wave is still travelling.");
                yield return null;
            }

            Assert.IsFalse(IsSweeping());
            Assert.That(Time.time - waveStarted, Is.EqualTo(waveDuration).Within(.09f));
            Assert.AreEqual("completed", Read(path).Single()["status"]);
            float waveFinished = Time.time;

            while (!seals.Any(rune => rune.enabled) && Time.time < waveFinished + 1.75f)
                yield return null;

            Assert.That(Time.time - waveFinished, Is.EqualTo(1.5f).Within(.09f));
            LineRenderer mark = seals.Single(rune => rune.enabled);
            Assert.That(Vector3.Distance(mark.transform.position, player.transform.position), Is.LessThan(.1f));
            float markedAt = Time.time;
            var rows = Read(path);
            Assert.AreEqual(2, rows.Length);
            var followUp = rows[1];
            AssertFollowUp(followUp);
            Assert.That(Number(followUp["elapsed_game_seconds"]), Is.EqualTo(waveDuration + 1.5f).Within(.12f));

            var onHit = (UnityEvent)health.GetType().GetField("onHit", Members).GetValue(health);
            bool tookHit = false;
            void RecordHit() => tookHit = true;
            onHit.AddListener(RecordHit);
            Vector3 markPosition = mark.transform.position;
            player.Move(Vector3.right * 2.5f);
            Assert.That(Vector3.ProjectOnPlane(player.transform.position - markPosition, Vector3.up).magnitude,
                Is.GreaterThan(1.5f));
            bool struck = false;
            while ((bool)Field("casting").GetValue(boss) && Time.time < markedAt + 1.55f)
            {
                Assert.IsFalse(seals.Any(rune => rune != mark && rune.enabled),
                    "The follow-up must stay a single mark, even after the normal burst interval.");
                Assert.That(Vector3.Distance(mark.transform.position, markPosition), Is.LessThan(.01f));
                if (impact.enabled && !struck)
                {
                    struck = true;
                    Assert.That(Time.time - markedAt, Is.EqualTo(.88f).Within(.09f),
                        "Ivan gets the ordinary rune-warning time before the strike.");
                    Assert.That(Vector3.Distance(impact.transform.position, markPosition), Is.LessThan(.1f));
                }
                yield return null;
            }
            onHit.RemoveListener(RecordHit);
            Assert.IsFalse(tookHit, "Moving outside the locked mark dodges the follow-up.");
            Assert.IsTrue(struck);
            Assert.IsFalse((bool)Field("casting").GetValue(boss));
            Assert.IsFalse(seals.Any(rune => rune.enabled));

            rows = Read(path);
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("completed", rows[1]["status"]);
            Assert.AreEqual("0", rows[1]["hit"]);
            StartCast();
            rows = Read(path);
            Assert.AreEqual(3, rows.Length);
            Assert.AreEqual("adaptive", rows[2]["mode"], "Ordinary attack selection resumes after the single seal.");
        }

        [UnityTest]
        public IEnumerator HaloFollowUpDependsOnProgressAndSelectionMode([ValueSource(nameof(HaloModes))] HaloMode mode)
        {
            int confirmed = mode == HaloMode.Adaptive ? 3 : 4;
            SetConfirmedAltars(confirmed);
            PlaceInOpenArena();
            string path = StartHalo(mode);
            AdvanceCast(WaveDuration() + .05f);
            Assert.IsFalse(IsSweeping());
            Assert.AreEqual("completed", Read(path).Single()["status"]);
            bool followsUp = mode == HaloMode.ModelFallback;
            float recovery = confirmed == 3 ? 2f : 1.5f;
            Assert.That((float)Field("nextCast").GetValue(boss) - Time.time, Is.EqualTo(recovery).Within(.005f));

            StartCast();
            var rows = Read(path);
            Assert.AreEqual(2, rows.Length);
            if (followsUp)
            {
                AssertFollowUp(rows[1]);
                float hp = PlayerHealth();
                AdvanceCast(1.3f);
                Assert.That(PlayerHealth(), Is.LessThan(hp), "The single seal can hit if Ivan stays in it.");
                Assert.AreEqual("1", Read(path)[1]["hit"]);
                StartCast();
                Assert.AreEqual("model_fallback", Read(path)[2]["mode"]);
            }
            else Assert.AreEqual(mode == HaloMode.Fixed ? "fixed" : "adaptive", rows[1]["mode"]);
            if (mode == HaloMode.Fixed)
                Assert.AreEqual("HandTouch", rows[1]["chosen_attack"],
                    "Four confirmed altars must not change the explicit debug queue.");
            yield break;
        }

        [UnityTest]
        public IEnumerator SuspendingHaloOrItsFollowUpPauseClearsTheCombo([ValueSource(nameof(BooleanCases))] bool duringPause)
        {
            Time.captureDeltaTime = .04f;
            SetConfirmedAltars(4);
            PlaceInOpenArena();
            string path = StartHalo(HaloMode.ModelFallback);
            AdvanceCast(duringPause ? WaveDuration() + .05f : WaveParameter("windup") + .15f);
            Assert.AreEqual(!duringPause, IsSweeping());

            var suspend = boss.GetType().GetMethod("SetEncounterSuspended", Members);
            suspend.Invoke(boss, new object[] { true });
            var row = Read(path).Single();
            Assert.AreEqual(duringPause ? "completed" : "interrupted", row["status"]);
            yield return new WaitForSeconds(1.7f);
            Assert.AreEqual(1, Read(path).Length, "No cast can start during the suspension.");

            suspend.Invoke(boss, new object[] { false });
            float deadline = Time.time + 4f;
            Dictionary<string, string>[] rows;
            do
            {
                yield return null;
                rows = Read(path);
            }
            while (rows.Length == 1 && Time.time < deadline);

            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("model_fallback", rows[1]["mode"],
                "The first resumed cast must be a regular selection, with no pending seal.");
        }

        private void AssertFollowUp(Dictionary<string, string> row)
        {
            Assert.AreEqual("combo", row["mode"]);
            Assert.AreEqual("halo_followup", row["choice_reason"]);
            Assert.AreEqual("EyeSeal", row["requested_attack"]);
            Assert.AreEqual("EyeSeal", row["chosen_attack"]);
            Assert.AreEqual("", row["roll_01"]);
            Assert.AreEqual("", row["roll_from"]);
            Assert.AreEqual("", row["roll_to"]);
            foreach (string attack in Enum.GetNames(Field("nextAttack").FieldType))
                Assert.AreEqual(attack == "EyeSeal" ? 1f : 0f, Number(row[attack + "_selection_probability"]));
        }

        private void SetConfirmedAltars(int count) =>
            encounter.GetType().GetProperty("ConfirmedAltars", Members).SetValue(encounter, count);

        private void PlaceInOpenArena()
        {
            player.enabled = false;
            Vector3 position = boss.transform.position + Vector3.right * 3.5f;
            position.y = .04f;
            player.transform.position = position;
            player.enabled = true;
            Physics.SyncTransforms();
        }

        private string StartHalo(HaloMode mode)
        {
            if (mode != HaloMode.Adaptive)
            {
                if (mode == HaloMode.Fixed) NimbAttackSelectionTests.UseFixedOrder(boss);
                Field("attackModel").SetValue(boss, null);
                var next = Field("nextAttack");
                next.SetValue(boss, Enum.Parse(next.FieldType, "HaloWave"));
                StartCast();
                Assert.IsTrue(IsSweeping());
                return CaptureFilePath();
            }

            var rng = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(47291);
                // Pick through the actual model rather than promoting a debug cast.
                for (int attempt = 0; attempt < 32; attempt++)
                {
                    StartCast();
                    string path = CaptureFilePath();
                    if (IsSweeping()) return path;
                    boss.enabled = false;
                    boss.enabled = true;
                }
            }
            finally { UnityEngine.Random.state = rng; }

            Assert.Fail("The adaptive model never selected HaloWave.");
            return null;
        }

        private bool IsSweeping() => (bool)Field("sweeping").GetValue(boss);

        private float WaveDuration() => WaveParameter("windup") + WaveParameter("reach") / WaveParameter("speed");

        private float WaveParameter(string name)
        {
            object wave = Field("haloSweep").GetValue(boss);
            return (float)wave.GetType().GetField(name, Members).GetValue(wave);
        }

        private float PlayerHealth() => (float)health.GetType().GetProperty("HP", Members).GetValue(health);

        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            Time.captureDeltaTime = previousCaptureStep;
            if (room.IsValid() && room.isLoaded) yield return SceneManager.UnloadSceneAsync(room);
            foreach (var path in testFiles) File.Delete(path);
            testFiles.Clear();
        }
    }
}
