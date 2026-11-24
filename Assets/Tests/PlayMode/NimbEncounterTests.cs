using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using MovementKeyboard = Hackathon.Tests.BootstrapSceneTests.MovementKeyboard;

namespace Hackathon.Tests
{
    public sealed class NimbEncounterTests
    {
        private const string Arena = "Assets/Scenes/Levels/Temple_Final_Room.unity";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly bool[] AttackModes = { false, true };
        private Scene room;
        private float initialTimeScale;
        private Component encounter;
        private Component boss;
        private Component vulnerability;
        private Component[] altars;
        private CharacterController player;

        [UnityTest]
        public IEnumerator OnePistolHitBreaksOnlyActiveAltarAndExpiredWindowRestoresIt()
        {
            yield return LoadArena();

            var active = Get<Component>(encounter, "ActiveAltar");
            Assert.That(altars.Count(altar => State(altar) == "Active"), Is.EqualTo(1));
            AssertFreshRuneWindow();
            yield return new WaitForSeconds(.6f);
            foreach (var altar in altars)
            {
                var gate = (BoxCollider)Field(altar, "gateCollider").GetValue(altar);
                Assert.That(gate.enabled, Is.EqualTo(altar != active),
                    "Only the active altar's entrance should admit Ivan.");
            }

            foreach (var waiting in altars.Where(altar => altar != active))
            {
                Assert.That(State(waiting), Is.EqualTo("Waiting"));
                Assert.That(waiting.GetComponent<SphereCollider>().enabled, Is.False);
                ApproachAltar(waiting);
                Shoot(waiting);
                Shoot(waiting);
                Assert.That(Strike(waiting), Is.False);
                Assert.That(State(waiting), Is.EqualTo("Waiting"));
            }

            Assert.That(Strike(vulnerability), Is.False, "Nimb must resist before the active altar breaks.");
            ApproachAltar(active);
            Shoot(active);
            Assert.That(State(active), Is.EqualTo("Broken"), "One pistol hit must break the active altar.");
            Assert.That(active.GetComponent<SphereCollider>().enabled, Is.False);
            Assert.That(Get<bool>(encounter, "IsVulnerable"), Is.True);
            Assert.That(Get<int>(encounter, "ConfirmedAltars"), Is.Zero);

            var stunDeadline = Field(encounter, "recoversAt");
            Assert.That((float)stunDeadline.GetValue(encounter) - Time.time, Is.EqualTo(15f).Within(.05f));
            stunDeadline.SetValue(encounter, Time.time - .01f);
            yield return null;

            Assert.That(Get<Component>(encounter, "ActiveAltar"), Is.SameAs(active));
            AssertFreshRuneWindow();
            Assert.That(State(active), Is.EqualTo("Active"));
            Assert.That(active.GetComponent<SphereCollider>().enabled, Is.True);
            Assert.That(Get<bool>(encounter, "IsVulnerable"), Is.False);
            Assert.That(Get<int>(encounter, "ConfirmedAltars"), Is.Zero);
            Assert.That(Strike(vulnerability), Is.False);
            Assert.That(Strike(active), Is.True);
            Assert.That(State(active), Is.EqualTo("Broken"), "One grip strike must break a reformed altar.");
            Assert.That(Get<bool>(encounter, "IsVulnerable"), Is.True);
            Assert.That(Strike(active), Is.False, "A second strike cannot break the same altar twice.");
        }

        [UnityTest]
        public IEnumerator IncomingAttackPathStopsAtEachInnerDoorPlane()
        {
            yield return LoadArena();
            var limit = encounter.GetType().GetMethod("LimitToChamberEntrance",
                BindingFlags.Instance | BindingFlags.NonPublic);
            float Travel(Vector3 start, Vector3 heading) =>
                (float)limit.Invoke(encounter, new object[] { start, heading, 4f });
            foreach (var altar in altars)
            {
                var sector = (Transform)Field(altar, "geometry").GetValue(altar);
                var lintel = sector.Find("Entrance lintel");
                Vector3 doorway = sector.InverseTransformPoint(Get<Vector3>(altar, "EntryAnchor"));
                doorway.z = lintel.localPosition.z + .5f * lintel.localScale.z;
                Vector3 boundary = sector.TransformPoint(doorway);
                Vector3 incoming = (sector.forward + .15f * sector.right).normalized;
                Vector3 start = boundary - incoming * 1.8f;
                Assert.That(Travel(start, incoming), Is.EqualTo(1.8f).Within(.003f), sector.name);
                Assert.That(Travel(start - Vector3.up * 3f, incoming), Is.EqualTo(4f), "Below the chamber floor");
                Assert.That(Travel(boundary + incoming, -incoming), Is.EqualTo(4f), "Outgoing path");
            }
        }

        [UnityTest]
        public IEnumerator AllSixAltarsRequireIvanToCrossTheirOwnDoorwayBeforeDamage()
        {
            yield return LoadArena();
            var seen = new HashSet<Component>();
            for (int index = 0; index < altars.Length; index++)
            {
                var active = Get<Component>(encounter, "ActiveAltar");
                Assert.That(active, Is.Not.Null);
                Assert.That(seen.Add(active), Is.True);
                yield return new WaitForSeconds(.6f);

                var sector = (Transform)Field(active, "geometry").GetValue(active);
                Vector3 doorway = Get<Vector3>(active, "EntryAnchor");
                Quaternion facing = Quaternion.LookRotation(sector.forward);
                PlacePlayerOnFloor(doorway - sector.forward * 1.48f, facing);
                AssertOutsideAltarRejectsDamage(active, "the upper gallery");

                PlacePlayerOnFloor(doorway - sector.forward * .24f, facing);
                Assert.That(sector.InverseTransformPoint(player.transform.position).z,
                    Is.LessThan(sector.InverseTransformPoint(doorway).z),
                    "Ivan's feet must be on the gallery side of the doorway.");
                AssertOutsideAltarRejectsDamage(active, "the outside threshold");

                foreach (float offset in new[] { -.45f, 0f, .45f })
                {
                    PlacePlayerOnFloor(doorway + sector.right * offset, facing);
                    Vector3 eye = player.transform.position + Vector3.up * 1.64f;
                    Assert.That(Physics.Linecast(eye, active.transform.position, out RaycastHit cover,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True);
                    Assert.That(cover.transform.IsChildOf(sector), Is.True,
                        "The room's stonework must screen the garnet from the open doorway.");
                    AssertOutsideAltarRejectsDamage(active, "the doorway");
                }

                // Place Ivan behind another chamber's door to exercise the sector
                // check separately from the physical grille and weapon raycasts.
                ApproachAltar(altars.First(altar => altar != active));
                AssertOutsideAltarRejectsDamage(active, "another altar chamber");

                if (index == 0)
                {
                    ApproachAltar(active, 3.5f);
                    FirePistolAtAltar(active);
                }
                else
                {
                    ApproachAltar(active);
                    Assert.That(Strike(active), Is.True);
                }

                Assert.That(State(active), Is.EqualTo("Broken"));
                Assert.That(Get<bool>(encounter, "IsVulnerable"), Is.True);
                Assert.That(Strike(vulnerability), Is.True);
                if (index + 1 < altars.Length)
                    yield return AdvanceDarkness();
            }
            CollectionAssert.AreEquivalent(altars, seen);
        }

        [UnityTest]
        public IEnumerator EnteringAnyAltarRoomStopsTheWaveButNotTheRunes()
        {
            yield return LoadArena();
            var health = player.GetComponent("CombatHealth");
            var wave = Field(boss, "haloSweep").GetValue(boss);
            wave.GetType().GetField("damage", Members).SetValue(wave, 1f);
            Field(boss, "damage").SetValue(boss, 1f);
            foreach (Component altar in altars)
            {
                var sector = (Transform)Field(altar, "geometry").GetValue(altar);
                Vector3 landing = Get<Vector3>(altar, "EntryAnchor") - sector.forward * 1.48f;
                Quaternion facing = Quaternion.LookRotation(sector.forward);
                PlacePlayerOnFloor(landing, facing);
                // Keep the caster on the gallery side of this particular chamber so that
                // arena pillars cannot accidentally satisfy the shelter assertion.
                boss.transform.SetPositionAndRotation(player.transform.position - sector.forward * 2f + Vector3.up, facing);
                Physics.SyncTransforms();
                float startHP = Get<float>(health, "HP");
                StartFixedAttack("HaloWave");
                ApproachAltar(altar);
                AdvanceAttack(.1f); // Ivan has crossed the doorway during the windup.
                AdvanceAttack(8f);
                Assert.That(Get<float>(health, "HP"), Is.EqualTo(startHP), altar.transform.parent.name);

                StartFixedAttack("EyeSeal");
                AdvanceAttack(8f);
                Assert.That(Get<float>(health, "HP"), Is.EqualTo(startHP - 1f),
                    "A marked Ivan standing inside must still take rune damage.");

                StartFixedAttack("HaloWave");
                PlacePlayerOnFloor(landing, facing);
                AdvanceAttack(.1f);
                AdvanceAttack(8f);
                Assert.That(Get<float>(health, "HP"), Is.EqualTo(startHP - 2f),
                    "The same wave must hit Ivan if he leaves before its front arrives.");
            }
        }

        [UnityTest]
        public IEnumerator SpearsAndHandCanHitOnlyOnTheGallerySideOfTheSameDoor()
        {
            yield return LoadArena();
            yield return new WaitForSeconds(.6f);
            var active = Get<Component>(encounter, "ActiveAltar");
            var sector = (Transform)Field(active, "geometry").GetValue(active);
            Vector3 doorway = sector.InverseTransformPoint(Get<Vector3>(active, "EntryAnchor"));
            doorway.z = (float)Field(active, "chamberFront").GetValue(active);
            Vector3 threshold = sector.TransformPoint(doorway);
            Quaternion facing = Quaternion.LookRotation(sector.forward);
            PlacePlayerOnFloor(threshold + sector.forward * .32f, facing);
            var health = player.GetComponent("CombatHealth");
            float startHP = Get<float>(health, "HP");
            Field(boss, "damage").SetValue(boss, 5f);
            // Aim through the open door at chest height so the lintel does not stop
            // the volley before it reaches Ivan on either side of the threshold.
            boss.transform.rotation = facing;
            var skull = (Transform)Field(boss, "skull").GetValue(boss);
            boss.transform.position += player.bounds.center - sector.forward * 2.5f - skull.position;
            Physics.SyncTransforms();

            StartFixedAttack("LightArrows");
            AdvanceAttack(1.2f); // The first spear has crossed Ivan; the second is still aiming.
            Assert.That(Get<float>(health, "HP"), Is.EqualTo(startHP));
            PlacePlayerOnFloor(threshold - sector.forward * .32f, facing);
            AdvanceAttack(3f);
            Assert.That(Get<float>(health, "HP"), Is.EqualTo(startHP - 5f),
                "A blocked spear must not consume the volley's one damaging hit.");

            foreach (bool inside in new[] { true, false })
            {
                PlacePlayerOnFloor(threshold + sector.forward * (inside ? .32f : -.32f), facing);
                float beforeHand = Get<float>(health, "HP");
                StartFixedAttack("HandTouch");
                AdvanceAttack(.56f);
                AdvanceAttack(1f);
                AdvanceAttack(8f);
                Assert.That(Get<float>(health, "HP"), Is.EqualTo(inside ? beforeHand : beforeHand - 5f),
                    "A thrust through this open doorway can only injure Ivan on the landing.");
            }
        }

        [UnityTest]
        public IEnumerator SealHaloAndHandSoundAtTheirTelegraphedPhases()
        {
            var original = SceneManager.GetActiveScene();
            yield return LoadArena();
            SceneManager.SetActiveScene(room);
            try
            {
                var audio = GameObject.Find("Game audio");
                AudioSource[] Playing(string cue) => audio.GetComponentsInChildren<AudioSource>()
                    .Where(source => source.clip?.name == cue && source.isPlaying).ToArray();
                AudioSource ExpectWorldCue(string cue)
                {
                    var playing = Playing(cue);
                    Assert.That(playing.Length, Is.EqualTo(1), cue);
                    Assert.That(playing[0].outputAudioMixerGroup.name, Is.EqualTo("Effects"), cue);
                    Assert.That(playing[0].spatialBlend, Is.EqualTo(1f), cue);
                    return playing[0];
                }

                StartFixedAttack("EyeSeal");
                Vector3 firstMark = player.transform.position;
                Assert.That(ExpectWorldCue("AngelCharge").transform.position, Is.EqualTo(firstMark));
                Assert.That(Playing("AngelStrike"), Is.Empty);
                float warning = (float)Field(boss, "warningSeconds").GetValue(boss);
                AdvanceAttack(warning + .01f);
                Assert.That(ExpectWorldCue("AngelStrike").transform.position, Is.EqualTo(firstMark));
                AdvanceAttack(8f);

                StartFixedAttack("HaloWave");
                ExpectWorldCue("HaloGrip");
                Assert.That(Playing("HaloWave"), Is.Empty);
                var sweep = Field(boss, "haloSweep").GetValue(boss);
                float windup = (float)sweep.GetType().GetField("windup", Members).GetValue(sweep);
                AdvanceAttack(windup + .01f);
                ExpectWorldCue("HaloWave");
                AdvanceAttack(8f);

                StartFixedAttack("HandTouch");
                var chime = boss.transform.Find("Hand touch tell").GetComponent<AudioSource>();
                Assert.That(chime.clip.name, Is.EqualTo("NimbHandChime"));
                Assert.That(chime.isPlaying, Is.True);
                Assert.That(chime.outputAudioMixerGroup.name, Is.EqualTo("Effects"));
                var skull = (Transform)Field(boss, "skull").GetValue(boss);
                Assert.That(Vector3.Distance(chime.transform.position, skull.position), Is.LessThan(.001f));
                Call(boss, "SetEncounterSuspended", true);
                Assert.That(chime.isPlaying, Is.False, "An interrupted hand attack should stop its warning.");
            }
            finally { SceneManager.SetActiveScene(original); }
        }

        private void StartFixedAttack(string attack)
        {
            Field(boss, "useFixedAttackOrder").SetValue(boss, true);
            var next = Field(boss, "nextAttack");
            next.SetValue(boss, Enum.Parse(next.FieldType, attack));
            Field(boss, "engaged").SetValue(boss, true);
            Field(boss, "nextCast").SetValue(boss, Time.time);
            Call(boss, "TickAttack");
            Assert.That(Field(boss, "lastAttack").GetValue(boss).ToString(), Is.EqualTo(attack));
        }

        private void AdvanceAttack(float seconds)
        {
            Field(boss, "castStart").SetValue(boss, Time.time - seconds);
            Call(boss, "TickAttack");
        }

        private void AssertOutsideAltarRejectsDamage(Component active, string position)
        {
            Shoot(active);
            Assert.That(Strike(active), Is.False, $"The altar accepted a pistol grip strike from {position}.");
            Assert.That(State(active), Is.EqualTo("Active"));
            Assert.That(Get<bool>(encounter, "IsVulnerable"), Is.False);
        }

        [UnityTest]
        public IEnumerator AltarThreadBreaksAsItsRunesBurnAwayAndReturnsOnTimeout()
        {
            yield return LoadArena();
            var active = Get<Component>(encounter, "ActiveAltar");
            var glyphs = Runes(active);
            var materials = glyphs.Select(glyph => glyph.sharedMaterial).ToArray();
            var originalVisibility = glyphs.Select(IsDrawn).ToArray();
            var otherGlyphs = altars.Where(altar => altar != active).SelectMany(Runes).ToArray();
            var otherVisibility = otherGlyphs.Select(IsDrawn).ToArray();
            Assert.That(glyphs, Has.Length.EqualTo(6));
            Assert.That(originalVisibility.Count(visible => visible), Is.GreaterThanOrEqualTo(5));
            yield return new WaitForSeconds(.25f);
            AssertThreadConnects(active);

            ApproachAltar(active);
            Shoot(active);
            Assert.That(Get<bool>(encounter, "IsVulnerable"), Is.True);
            yield return new WaitForSeconds(.16f);
            var properties = new MaterialPropertyBlock();
            foreach (MeshRenderer glyph in glyphs.Where(glyph => glyph.gameObject.activeInHierarchy))
            {
                Assert.That(IsDrawn(glyph), Is.True);
                Assert.That(glyph.sharedMaterial.shader.name, Is.EqualTo("Temple/Nimb Rune Dissolve"));
                glyph.GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_RuneCut"), Is.GreaterThan(0f).And.LessThan(1f));
            }
            yield return new WaitForSeconds(.65f);
            Assert.That(glyphs.Any(IsDrawn), Is.False);
            Assert.That(VisibleThreads(), Is.Empty);
            CollectionAssert.AreEqual(otherVisibility, otherGlyphs.Select(IsDrawn).ToArray());

            Field(encounter, "recoversAt").SetValue(encounter, Time.time - .01f);
            yield return new WaitForSeconds(.75f);
            Assert.That(Get<Component>(encounter, "ActiveAltar"), Is.SameAs(active));
            CollectionAssert.AreEqual(originalVisibility, glyphs.Select(IsDrawn).ToArray());
            CollectionAssert.AreEqual(materials, glyphs.Select(glyph => glyph.sharedMaterial).ToArray());
            AssertThreadConnects(active);

            Shoot(active);
            float confirmationAt = Time.time;
            Assert.That(Strike(vulnerability), Is.True);
            float relightAt = RuneChangesAt;
            Assert.That(relightAt - confirmationAt, Is.EqualTo(8f).Within(.05f));
            Assert.That(Get<Component>(encounter, "ActiveAltar"), Is.Null);
            yield return new WaitForSeconds(.85f);
            Assert.That(glyphs.Any(IsDrawn), Is.False, "A fast confirmation must allow the old rune to disappear.");
            AssertDarkness(1);

            float deadline = Time.realtimeSinceStartup + 18f;
            while (Time.time < relightAt && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(Get<Component>(encounter, "ActiveAltar"), Is.Null,
                    "No rune may be selected before the dark interval elapses.");
                yield return null;
            }
            Assert.That(Time.time, Is.GreaterThanOrEqualTo(relightAt));
            yield return null;

            var next = Get<Component>(encounter, "ActiveAltar");
            Assert.That(next, Is.Not.Null);
            Assert.That(next, Is.Not.SameAs(active));
            AssertFreshRuneWindow();
            AssertThreadConnects(next);
        }

        [UnityTest]
        public IEnumerator ExpiredRuneGoesDarkAndSkipsItAfterPause()
        {
            yield return LoadArena();
            var expired = Get<Component>(encounter, "ActiveAltar");
            Assert.That(expired, Is.Not.Null, "The first rune must be active as soon as the room loads.");
            AssertFreshRuneWindow();
            AssertThreadConnects(expired);

            var gate = (BoxCollider)Field(expired, "gateCollider").GetValue(expired);
            yield return new WaitForSeconds(.6f);
            Assert.That(gate.enabled, Is.False);
            float openGateHeight = gate.transform.localPosition.y;
            ApproachAltar(expired);
            yield return ObserveLitWindow(expired);
            Assert.That(RuneChangesAt - Time.time, Is.InRange(7.85f, 8.05f));
            AssertDarkness(0);
            Assert.That(State(expired), Is.EqualTo("Waiting"));
            Assert.That(gate.enabled, Is.False, "The expired gate must stay open while Ivan is in the chamber.");
            Shoot(expired);
            Assert.That(Strike(expired), Is.False);
            Assert.That(State(expired), Is.EqualTo("Waiting"));

            // Move close to the dark phase's deadline so this short real-time wait
            // would relight a rune if the timer kept running through a pause.
            Field(encounter, "runeChangesAt").SetValue(encounter, Time.time + .10f);
            float pausedAt = Time.time;
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(Time.time, Is.EqualTo(pausedAt).Within(.001f));
                AssertDarkness(0);
            }
            finally
            {
                Time.timeScale = scale;
            }

            yield return new WaitForSeconds(.18f);
            var next = Get<Component>(encounter, "ActiveAltar");
            Assert.That(next, Is.Not.Null);
            Assert.That(next, Is.Not.SameAs(expired), "An unspent alternative must replace an expired rune.");
            Assert.That(Get<int>(encounter, "ConfirmedAltars"), Is.Zero);
            AssertFreshRuneWindow();
            AssertThreadConnects(next);
            Assert.That(gate.enabled, Is.False, "The previous gate must stay open even after the next rune lights.");

            var sector = (Transform)Field(expired, "geometry").GetValue(expired);
            Vector3 entry = Get<Vector3>(expired, "EntryAnchor");
            Quaternion facing = Quaternion.LookRotation(-sector.forward);
            PlacePlayerOnFloor(entry - sector.forward * .24f, facing);
            yield return null;
            Assert.That(gate.enabled, Is.False, "The gate must leave room for Ivan to clear the threshold.");

            PlacePlayerOnFloor(entry - sector.forward * 1.48f, facing);
            yield return new WaitForSeconds(.6f);
            Assert.That(gate.enabled, Is.True, "Once Ivan exits, the old chamber must close.");
            Assert.That(gate.transform.localPosition.y, Is.LessThan(openGateHeight - 2.3f));
        }

        private IEnumerator ObserveLitWindow(Component altar)
        {
            float expiresAt = RuneChangesAt;
            var lamp = (Light)Field(altar, "entranceLight").GetValue(altar);
            float brightnessThreshold = (float)Field(altar, "entranceLightIntensity").GetValue(altar) * 1.65f;
            int normalPulses = 0, warningPulses = 0;
            bool wasBright = lamp.intensity >= brightnessThreshold;
            float previousStep = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = .05f;
                while (Time.time < expiresAt)
                {
                    Assert.That(State(altar), Is.EqualTo("Active"));
                    float remaining = expiresAt - Time.time;
                    bool bright = lamp.intensity >= brightnessThreshold;
                    if (bright && !wasBright)
                    {
                        if (remaining is > 6f and <= 10f) normalPulses++;
                        if (remaining <= 4f) warningPulses++;
                    }
                    wasBright = bright;
                    yield return null;
                }
                Assert.That(normalPulses, Is.GreaterThan(0));
                Assert.That(warningPulses, Is.GreaterThan(normalPulses + 1),
                    "The entrance should pulse visibly faster before its rune expires.");
            }
            finally
            {
                Time.captureDeltaTime = previousStep;
            }
        }

        [UnityTest]
        public IEnumerator UnloadingDuringDarknessStartsFreshOnReentry()
        {
            yield return LoadArena();
            for (int confirmed = 0; confirmed < 2; confirmed++)
            {
                var altar = Get<Component>(encounter, "ActiveAltar");
                ApproachAltar(altar);
                Shoot(altar);
                Assert.That(Strike(vulnerability), Is.True);
                if (confirmed == 0)
                    yield return AdvanceDarkness();
            }
            yield return new WaitForSeconds(.75f);
            AssertDarkness(2);
            Assert.That(Get<float>(boss, "RecoverySeconds"), Is.EqualTo(2f).Within(.001f));
            Component previousEncounter = encounter;
            yield return SceneManager.UnloadSceneAsync(room);
            Assert.That(previousEncounter == null, Is.True, "The old timer and thread live only inside the arena.");

            yield return LoadArena();
            var active = Get<Component>(encounter, "ActiveAltar");
            Assert.That(active, Is.Not.Null, "The new room must activate a rune immediately.");
            Assert.That(Get<int>(encounter, "ConfirmedAltars"), Is.Zero);
            AssertFreshRuneWindow();
            AssertAttackRecovery(2.8f, useFixedOrder: false);
            AssertThreadConnects(active);
        }

        private float RuneChangesAt => (float)Field(encounter, "runeChangesAt").GetValue(encounter);

        private void AssertFreshRuneWindow() =>
            Assert.That(RuneChangesAt - Time.time, Is.InRange(29.25f, 30.05f));

        private void ExpireRuneTimer() =>
            Field(encounter, "runeChangesAt").SetValue(encounter, Time.time - .01f);

        private void AssertDarkness(int confirmed)
        {
            Assert.That(Get<Component>(encounter, "ActiveAltar"), Is.Null);
            Assert.That(Get<int>(encounter, "ConfirmedAltars"), Is.EqualTo(confirmed));
            Assert.That(Get<bool>(encounter, "IsVulnerable"), Is.False);
            Assert.That(altars.Count(altar => State(altar) == "Spent"), Is.EqualTo(confirmed));
            Assert.That(altars.Where(altar => State(altar) != "Spent").All(altar =>
                State(altar) == "Waiting" && !altar.GetComponent<SphereCollider>().enabled), Is.True);
            var properties = new MaterialPropertyBlock();
            Runes(altars.First(altar => State(altar) == "Waiting")).First(IsDrawn).GetPropertyBlock(properties);
            var emission = properties.GetColor("_EmissionColor");
            Assert.That(Mathf.Max(emission.r, emission.g, emission.b), Is.LessThan(.001f));
            Assert.That(VisibleThreads(), Is.Empty);
        }

        private IEnumerator AdvanceDarkness()
        {
            Assert.That(Get<Component>(encounter, "ActiveAltar"), Is.Null);
            ExpireRuneTimer();
            yield return null;
            Assert.That(Get<Component>(encounter, "ActiveAltar"), Is.Not.Null);
        }

        private static MeshRenderer[] Runes(Component altar) =>
            (MeshRenderer[])Field(altar, "runes").GetValue(altar);

        private LineRenderer[] VisibleThreads() =>
            encounter.GetComponentsInChildren<LineRenderer>(true)
                .Where(line => line.name.StartsWith("Nimb altar thread") && IsDrawn(line)).ToArray();

        private void AssertThreadConnects(Component altar)
        {
            var lines = VisibleThreads();
            Assert.That(lines, Has.Length.EqualTo(2), "The chamber and arena share one thread through the pillar.");
            var chamber = lines.Single(line => line.name.EndsWith("chamber"));
            var arena = lines.Single(line => line.name.EndsWith("arena"));
            Assert.That(Vector3.Distance(LinePoint(chamber, 0), Get<Vector3>(altar, "CrystalAnchor")),
                Is.LessThan(.35f));
            Vector3 pillar = Get<Vector3>(altar, "PillarAnchor");
            Vector3 chest = Get<Vector3>(vulnerability, "TargetPoint");
            Assert.That(Vector3.Distance(LinePoint(arena, 0), pillar), Is.LessThan(.15f));
            Assert.That(Vector3.Distance(LinePoint(arena, arena.positionCount - 1), chest), Is.LessThan(.35f));
            // With Nimb in open view of the rune, the thread should leave the stone in his direction.
            Vector3 towardBoss = (chest - pillar).normalized;
            for (int i = 1; i < arena.positionCount; i++)
                Assert.That(Vector3.Dot(LinePoint(arena, i) - LinePoint(arena, i - 1), towardBoss),
                    Is.GreaterThanOrEqualTo(-.01f));
        }

        private static Vector3 LinePoint(LineRenderer line, int index) =>
            line.useWorldSpace ? line.GetPosition(index) : line.transform.TransformPoint(line.GetPosition(index));

        [UnityTest]
        public IEnumerator ProtectionDropsIntoTwoArmedPoseAndRecoversAfterGroundedPistolStrike()
        {
            yield return LoadArena();

            var aura = boss.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(component => component != null && component.GetType().Name == "NimbProtectionAura");
            var shell = aura.GetComponentInChildren<MeshRenderer>(true);
            Assert.That(shell, Is.Not.Null);
            Assert.That(shell.sharedMaterial.shader.name, Does.Not.Contain("InternalErrorShader"));
            Assert.That(IsDrawn(shell), Is.True);

            string[] haloNames = { "halo_outer_ring", "inner_torus", "outter_torus" };
            var halos = boss.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => haloNames.Contains(renderer.name)).ToArray();
            CollectionAssert.AreEquivalent(haloNames, halos.Select(renderer => renderer.name));
            Assert.That(halos.All(IsDrawn), Is.True);

            var skins = boss.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var hands = skins.Where(skin => skin.sharedMaterials.Any(material =>
                material != null && material.name == "hand_arm_mat")).ToArray();
            Assert.That(hands, Has.Length.EqualTo(10));
            var originalVisibility = hands.Select(IsDrawn).ToArray();
            Assert.That(originalVisibility.Count(visible => visible), Is.GreaterThan(2));
            var body = skins.Where(skin => !hands.Contains(skin) && !halos.Contains(skin) && IsDrawn(skin)).ToArray();
            Assert.That(body, Is.Not.Empty);

            var retainedHand = hands.Single(renderer => renderer.name == "hand:arm_mesh");
            Transform retainedWrist = Bone("hand:arm_wrist");
            var sameSideHands = Enumerable.Range(1, 4).Select(index => (
                Mesh: hands.Single(renderer => renderer.name == $"hand{index}:arm_mesh"),
                Wrist: Bone($"hand{index}:arm_wrist"))).ToArray();
            var animator = boss.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            int furthestHand = 0;
            float originalGap = 0f;
            float floatDeadline = Time.time + 1.25f;
            bool floatEvaluated;
            do
            {
                // A regular frame lets the Animator and LateUpdate produce the pose
                // even in batch mode, where WaitForEndOfFrame does not resume.
                yield return null;
                floatEvaluated = animator.GetCurrentAnimatorClipInfo(0).Any(info => info.clip.name == "Float");
                originalGap = 0f;
                for (int i = 0; i < sameSideHands.Length; i++)
                {
                    var candidate = sameSideHands[i];
                    float gap = Vector3.Distance(retainedWrist.position, candidate.Wrist.position);
                    if (!IsDrawn(candidate.Mesh) || gap <= originalGap) continue;
                    furthestHand = i;
                    originalGap = gap;
                }
            }
            while ((!floatEvaluated || originalGap <= .25f) && Time.time < floatDeadline);

            Assert.That(floatEvaluated, Is.True, "The authored Float clip must be evaluated before breaking the altar.");
            var (mergingHand, mergingWrist) = sameSideHands[furthestHand];
            originalGap = Vector3.Distance(retainedWrist.position, mergingWrist.position);
            Assert.That(originalGap, Is.GreaterThan(.25f), "The captured hover pose must show separate forearms.");

            Transform chest = Bone("skeleton:chest");
            Transform head = Bone("skeleton:head");
            Transform crown = Bone("skeleton:head_top");
            Vector3 hoveringChest = Get<Vector3>(vulnerability, "TargetPoint");
            float headAboveChest = crown.position.y - hoveringChest.y;
            Quaternion raisedHead = Quaternion.Inverse(chest.rotation) * head.rotation;
            Vector3 hoverUp = boss.transform.up;
            var active = Get<Component>(encounter, "ActiveAltar");
            ApproachAltar(active);
            Shoot(active);
            float descentStarted = Time.time;
            Assert.That(IsDrawn(shell), Is.False);
            Assert.That(halos.Any(IsDrawn), Is.False);
            Assert.That(Get<bool>(vulnerability, "IsStunned"), Is.True);

            float closestVisibleGap = originalGap;
            while (Time.time - descentStarted < 1f && IsDrawn(mergingHand))
            {
                yield return null;
                if (IsDrawn(retainedHand) && IsDrawn(mergingHand))
                    closestVisibleGap = Mathf.Min(closestVisibleGap,
                        Vector3.Distance(retainedWrist.position, mergingWrist.position));
            }
            Assert.That(closestVisibleGap, Is.LessThan(originalGap - .08f),
                "A detached hand must visibly approach the retained wrist before disappearing.");
            yield return new WaitForSeconds(Mathf.Max(0f, descentStarted + 1.5f - Time.time));

            Vector3 loweredChest = Get<Vector3>(vulnerability, "TargetPoint");
            Assert.That(Vector3.ProjectOnPlane(loweredChest - hoveringChest, Vector3.up).magnitude,
                Is.LessThan(.20f), "The torso should descend vertically.");
            Assert.That(loweredChest.y, Is.LessThan(hoveringChest.y - .55f));
            Assert.That(Vector3.Angle(hoverUp, boss.transform.up), Is.LessThan(25f),
                "The boss must lower the head and torso without pitching the entire rig onto its side.");
            Assert.That(crown.position.y - loweredChest.y, Is.LessThan(headAboveChest - .04f),
                "The crown should lower relative to the torso as well as relative to the floor.");
            Assert.That(Quaternion.Angle(raisedHead, Quaternion.Inverse(chest.rotation) * head.rotation),
                Is.GreaterThan(20f), "The head must also bow at its articulated joints.");
            Assert.That(body.All(IsDrawn), Is.True);

            string[] pairedHands = { "hand:arm_mesh", "hand5:arm_mesh" };
            CollectionAssert.AreEquivalent(pairedHands, hands.Where(IsDrawn).Select(renderer => renderer.name),
                "The two original mirrored forearms should remain once the ten hands fold together.");
            AssertHandsBesideTorso(loweredChest);

            using var input = new MovementKeyboard();
            AimAt(loweredChest, player.transform.position - loweredChest);
            yield return DrawPistol(input);
            input.Press(Key.F);
            float contactDeadline = Time.time + .8f;
            while (Get<bool>(vulnerability, "IsStunned") && Time.time < contactDeadline)
                yield return null;
            Assert.That(Get<int>(encounter, "ConfirmedAltars"), Is.EqualTo(1),
                "The drawn pistol must reach the exposed torso while Ivan is on the floor.");
            Assert.That(IsDrawn(shell), Is.True);
            Assert.That(halos.All(IsDrawn), Is.True);
            Assert.That(Get<bool>(vulnerability, "IsStunned"), Is.False);
            // Sample the handoff to Float before the boss turns to track Ivan's new position.
            float recoveryDeadline = Time.time + 2f;
            while (animator.speed == 0f && Time.time < recoveryDeadline)
                yield return null;
            Assert.That(animator.speed, Is.GreaterThan(0f));

            Vector3 recoveredChest = Get<Vector3>(vulnerability, "TargetPoint");
            Assert.That(recoveredChest.y, Is.GreaterThan(loweredChest.y + .55f));
            Assert.That(crown.position.y - recoveredChest.y, Is.GreaterThan(headAboveChest - .08f));
            CollectionAssert.AreEqual(originalVisibility, hands.Select(IsDrawn).ToArray());
        }

        private Transform Bone(string name) =>
            boss.GetComponentsInChildren<Transform>(true).Single(bone => bone.name == name);

        private void AssertHandsBesideTorso(Vector3 chest)
        {
            Transform first = Bone("hand:arm_root");
            Transform second = Bone("hand5:arm_root");
            Vector3 right = boss.transform.right;
            float firstSide = Vector3.Dot(first.position - chest, right);
            float secondSide = Vector3.Dot(second.position - chest, right);
            Assert.That(firstSide * secondSide, Is.LessThan(0f), "One forearm belongs on each side of the body.");
            Assert.That(Vector3.ProjectOnPlane(first.position - chest, Vector3.up).magnitude,
                Is.LessThan(.9f));
            Assert.That(Vector3.ProjectOnPlane(second.position - chest, Vector3.up).magnitude,
                Is.LessThan(.9f));
            Transform leftArm = Bone("skeleton:arm_l");
            Transform rightArm = Bone("skeleton:arm_r");
            Assert.That(Vector3.Distance(first.position, rightArm.position),
                Is.LessThan(Vector3.Distance(first.position, leftArm.position)));
            Assert.That(Vector3.Distance(second.position, leftArm.position),
                Is.LessThan(Vector3.Distance(second.position, rightArm.position)));

            float floorHeight = FindArenaFloor(chest).point.y;
            foreach (string hand in new[] { "hand:", "hand5:" })
            {
                Assert.That(Bone(hand + "arm_wrist").position.y - floorHeight, Is.GreaterThanOrEqualTo(.25f),
                    hand + "wrist must remain visibly above the stone floor.");
                Assert.That(Bone(hand + "middle_5").position.y - floorHeight, Is.GreaterThanOrEqualTo(.25f),
                    hand + "fingers must remain visibly above the stone floor.");
            }
        }

        private static bool IsDrawn(Renderer renderer) =>
            renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy;

        [UnityTest]
        public IEnumerator SixUniqueAltarsCanBeConfirmedWithNoAmmoAndOpenVictoryComic(
            [ValueSource(nameof(AttackModes))] bool useFixedOrder)
        {
            yield return LoadArena();

            Component pistol = player.GetComponent("PistolCombat");
            Call(pistol, "RestoreAmmo", 0, 0, 12);
            using var input = new MovementKeyboard();
            var seen = new HashSet<Component>();

            for (int index = 0; index < altars.Length; index++)
            {
                var active = Get<Component>(encounter, "ActiveAltar");
                Assert.That(active, Is.Not.Null);
                Assert.That(seen.Add(active), Is.True, "A confirmed altar cannot be selected again.");
                Assert.That(Get<int>(encounter, "ConfirmedAltars"), Is.EqualTo(index));

                if (index == altars.Length - 1)
                {
                    ExpireRuneTimer();
                    yield return null;
                    AssertDarkness(index);
                    yield return AdvanceDarkness();
                    Assert.That(Get<Component>(encounter, "ActiveAltar"), Is.SameAs(active),
                        "The only unspent altar must return after darkness when it expires.");
                    AssertFreshRuneWindow();
                }

                float expectedRecovery = index < 2 ? 2.8f : index < 4 ? 2f : 1.5f;
                float resumeDeadline = Time.realtimeSinceStartup + 4f;
                while (AttackSuspended && Time.realtimeSinceStartup < resumeDeadline)
                    yield return null;
                Assert.That(AttackSuspended, Is.False, "Nimb must recover before the next cast.");
                AssertAttackRecovery(expectedRecovery, useFixedOrder);

                if (index == 1)
                {
                    // A missed vulnerability at one confirmed altar must not cross the tempo boundary.
                    ApproachAltar(active);
                    Assert.That(Strike(active), Is.True);
                    Assert.That(Get<bool>(encounter, "IsVulnerable"), Is.True);
                    Field(encounter, "recoversAt").SetValue(encounter, Time.time - .01f);
                    yield return null;
                    Assert.That(Get<Component>(encounter, "ActiveAltar"), Is.SameAs(active));
                    Assert.That(Get<int>(encounter, "ConfirmedAltars"), Is.EqualTo(index));
                    Assert.That(Get<float>(boss, "RecoverySeconds"), Is.EqualTo(expectedRecovery).Within(.001f));
                }

                if (index == 0)
                    yield return CheckMissesAndDrawPistol(input, active);
                ApproachAltar(active);
                yield return Swing(input);
                Assert.That(State(active), Is.EqualTo("Broken"),
                    $"One pistol grip strike must break the altar in {active.transform.parent.name}.");
                Assert.That(Get<bool>(encounter, "IsVulnerable"), Is.True);
                Assert.That(Get<int>(encounter, "ConfirmedAltars"), Is.EqualTo(index));

                if (index == altars.Length - 1)
                {
                    yield return new WaitForSeconds(1.5f);
                    var target = Get<Vector3>(vulnerability, "TargetPoint");
                    AimAt(target, player.transform.position - target);
                    yield return Swing(input);
                }
                else
                    Assert.That(Strike(vulnerability), Is.True);

                Assert.That(State(active), Is.EqualTo("Spent"));
                Assert.That(Get<int>(encounter, "ConfirmedAltars"), Is.EqualTo(index + 1));
                Assert.That(Get<bool>(encounter, "IsVulnerable"), Is.False);
                Assert.That(Get<bool>(encounter, "IsComplete"), Is.EqualTo(index == altars.Length - 1));
                Assert.That(Strike(vulnerability), Is.False, "A vulnerability can be confirmed only once.");
                if (index + 1 < altars.Length)
                    yield return AdvanceDarkness();
            }

            Assert.That(seen.Count, Is.EqualTo(6));
            Assert.That(altars.All(altar => State(altar) == "Spent"), Is.True);
            Assert.That(Get<Component>(encounter, "ActiveAltar"), Is.Null);
            Assert.That(Get<int>(pistol, "Ammo"), Is.Zero);
            Assert.That(Get<int>(pistol, "ReserveAmmo"), Is.Zero);

            Component comic = encounter.GetComponent("NimbVictoryComic");
            Assert.That(comic, Is.Not.Null);
            Assert.That(Get<bool>(comic, "IsOpen"), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(VisibleThreads(), Is.Empty);
            ExpireRuneTimer();
            Field(encounter, "recoversAt").SetValue(encounter, Time.time - .01f);
            Call(encounter, "Update");
            Assert.That(Get<bool>(encounter, "IsComplete"), Is.True);
            Assert.That(Get<Component>(encounter, "ActiveAltar"), Is.Null,
                "Victory must suppress even an expired rune or stun timer.");
            Assert.That(VisibleThreads(), Is.Empty);
            Assert.That(Get<float>(boss, "RecoverySeconds"), Is.EqualTo(1.5f).Within(.001f));
            Field(boss, "nextCast").SetValue(boss, Time.time - .01f);
            Call(boss, "LateUpdate");
            Assert.That(AnyAttackRunning(), Is.False, "Nimb cannot start a cast after the sixth confirmation.");
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(altars.SelectMany(Runes).Any(IsDrawn), Is.False);
        }

        private bool AttackSuspended => (bool)Field(boss, "encounterSuspended").GetValue(boss);

        private bool AnyAttackRunning() => new[] { "casting", "sweeping", "touching", "firingArrows", "dashing" }
            .Any(flag => (bool)Field(boss, flag).GetValue(boss));

        private void AssertAttackRecovery(float expected, bool useFixedOrder)
        {
            int confirmed = Get<int>(encounter, "ConfirmedAltars");
            Assert.That(Get<float>(boss, "RecoverySeconds"), Is.EqualTo(expected).Within(.001f),
                $"Recovery at {confirmed} confirmed altars.");
            Assert.That(AnyAttackRunning(), Is.False);
            AimAt(Get<Vector3>(vulnerability, "TargetPoint"), Vector3.back, 3.15f);
            Field(boss, "useFixedAttackOrder").SetValue(boss, useFixedOrder);
            var next = Field(boss, "nextAttack");
            next.SetValue(boss, Enum.Parse(next.FieldType, "EyeSeal"));
            Field(boss, "engaged").SetValue(boss, true);
            var nextCast = Field(boss, "nextCast");
            nextCast.SetValue(boss, Time.time);

            if (!useFixedOrder)
            {
                // EyeSeal occupies the first weighted interval. Preserve the game's RNG
                // and clear only the repeat guard so every tier uses the same cast.
                Field(boss, "consecutiveAttacks").SetValue(boss, 0);
                var previousRandom = UnityEngine.Random.state;
                try
                {
                    UnityEngine.Random.InitState(802);
                    UnityEngine.Random.State eyeDraw;
                    do { eyeDraw = UnityEngine.Random.state; } while (UnityEngine.Random.value >= .001f);
                    UnityEngine.Random.state = eyeDraw;
                    Call(boss, "LateUpdate");
                }
                finally
                {
                    UnityEngine.Random.state = previousRandom;
                }
            }
            else
                Call(boss, "LateUpdate");

            Assert.That((bool)Field(boss, "casting").GetValue(boss), Is.True);
            Assert.That(Field(boss, "lastAttack").GetValue(boss).ToString(), Is.EqualTo("EyeSeal"));
            Field(boss, "castStart").SetValue(boss, Time.time - 6f);
            Call(boss, "LateUpdate");
            Assert.That(AnyAttackRunning(), Is.False, "The EyeSeal cast must finish before recovery.");
            Assert.That((float)nextCast.GetValue(boss) - Time.time, Is.EqualTo(expected).Within(.001f),
                $"Scheduled recovery at {confirmed} confirmed altars with fixed order {useFixedOrder}.");

            Call(boss, "LateUpdate");
            Assert.That(AnyAttackRunning(), Is.False, "A second cast cannot bypass the scheduled recovery.");
        }

        private IEnumerator LoadArena()
        {
            initialTimeScale = Time.timeScale;
            yield return SceneManager.LoadSceneAsync(Arena, LoadSceneMode.Additive);
            room = SceneManager.GetSceneByPath(Arena);
            var roots = room.GetRootGameObjects();
            encounter = roots.Select(root => root.GetComponent("NimbEncounter")).Single(component => component != null);
            boss = roots.Select(root => root.GetComponent("AngelSealAttack")).Single(component => component != null);
            vulnerability = boss.GetComponent("NimbVulnerability");
            Assert.That(vulnerability, Is.Not.Null);
            player = roots.Single(root => root.CompareTag("Player")).GetComponent<CharacterController>();
            altars = roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null && component.GetType().Name == "NimbAltar").ToArray();
            Assert.That(altars, Has.Length.EqualTo(6));

            Field(boss, "damage").SetValue(boss, 0f);
            foreach (var guard in roots.Where(root => root.GetComponent("RifleNpc") != null))
                guard.SetActive(false);
            yield return null;
        }

        private IEnumerator DrawPistol(MovementKeyboard input)
        {
            player.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
            input.Press(Key.Digit2);
            yield return new WaitForSeconds(1.4f);
            Assert.That(Get<bool>(player.GetComponent("IvanSidearm"), "IsSidearmActive"), Is.True);
        }

        private static IEnumerator Swing(MovementKeyboard input)
        {
            input.Press();
            yield return null;
            input.Press(Key.F);
            // The victory comic pauses game time on contact with the last target.
            float until = Time.time + .8f;
            while (Time.timeScale > 0f && Time.time < until)
                yield return null;
        }

        private IEnumerator CheckMissesAndDrawPistol(MovementKeyboard input, Component altar)
        {
            Vector3 target = Get<Vector3>(altar, "CrystalAnchor");
            Vector3 origin = ApproachAltar(altar);

            yield return Swing(input);
            Assert.That(State(altar), Is.EqualTo("Active"), "F with the pistol holstered cannot hit the altar.");
            yield return DrawPistol(input);
            input.Press(Key.V);
            yield return new WaitForSeconds(.7f);
            Assert.That(State(altar), Is.EqualTo("Active"), "V must not damage an altar.");

            var obstacle = new GameObject("Pistol strike test wall");
            SceneManager.MoveGameObjectToScene(obstacle, room);
            obstacle.transform.SetPositionAndRotation(Vector3.Lerp(origin, target, .5f), player.transform.rotation);
            obstacle.AddComponent<BoxCollider>().size = new Vector3(1.8f, 2.5f, .04f);
            Physics.SyncTransforms();
            yield return Swing(input);
            Assert.That(State(altar), Is.EqualTo("Active"), "The grip must not hit an altar through cover.");

            obstacle.SetActive(false);
            ApproachAltar(altar, 2.6f);
            yield return Swing(input);
            Assert.That(State(altar), Is.EqualTo("Active"), "A swing outside melee reach must miss.");
        }

        private Vector3 ApproachAltar(Component altar, float distance = 1.15f)
        {
            Vector3 target = Get<Vector3>(altar, "CrystalAnchor");
            return AimAt(target, Get<Vector3>(altar, "EntryAnchor") - target, distance);
        }

        private Vector3 AimAt(Vector3 target, Vector3 outward, float distance = 1.15f)
        {
            Vector3 direction = Vector3.ProjectOnPlane(outward, Vector3.up).normalized;
            if (direction.sqrMagnitude < .01f) direction = Vector3.forward;
            Vector3 standingPosition = target + direction * distance;
            var facing = Quaternion.LookRotation(-direction);
            Vector3 origin = PlacePlayerOnFloor(standingPosition, facing);
            Camera camera = PlayerView();
            ((Behaviour)camera.GetComponent("IvanOrbitCamera")).enabled = false;
            Vector3 eye = origin + direction * .68f + Vector3.up * .70f + player.transform.right * .19f;
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            return origin;
        }

        private Vector3 PlacePlayerOnFloor(Vector3 standingPosition, Quaternion facing)
        {
            ((Behaviour)player.GetComponent("IvanLocomotion")).enabled = false;
            RaycastHit floor = FindArenaFloor(standingPosition);
            standingPosition.y = floor.point.y + player.height * .5f - player.center.y + .05f;
            player.enabled = false;
            player.transform.SetPositionAndRotation(standingPosition, facing);
            player.enabled = true;
            Physics.SyncTransforms();
            CollisionFlags grounded = player.Move(Vector3.down * .16f);
            Physics.SyncTransforms();
            Assert.That((grounded & CollisionFlags.Below) != 0, Is.True);
            Assert.That(player.bounds.min.y, Is.EqualTo(floor.point.y).Within(.08f),
                "Ivan's feet must stay on an actual floor surface.");
            return player.transform.TransformPoint(player.center);
        }

        private Camera PlayerView() => room.GetRootGameObjects()
            .Single(root => root.CompareTag("MainCamera")).GetComponent<Camera>();

        private void FirePistolAtAltar(Component altar)
        {
            Component pistol = player.GetComponent("PistolCombat");
            int before = Get<int>(pistol, "Ammo");
            Assert.That(before, Is.GreaterThan(0));
            Call(pistol, "Fire");
            Assert.That(Get<int>(pistol, "Ammo"), Is.EqualTo(before - 1));
            Assert.That(State(altar), Is.EqualTo("Broken"),
                "The ordinary pistol ray must hit the garnet after Ivan enters its chamber.");
        }

        private RaycastHit FindArenaFloor(Vector3 position)
        {
            RaycastHit floor = Physics.RaycastAll(position + Vector3.up, Vector3.down, 8f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Where(hit => hit.collider.gameObject.scene == room && hit.normal.y > .65f &&
                    !hit.collider.transform.IsChildOf(player.transform) &&
                    !hit.collider.transform.IsChildOf(boss.transform))
                .OrderBy(hit => hit.distance).FirstOrDefault();
            Assert.That(floor.collider, Is.Not.Null, "The pose needs a real floor surface beneath it.");
            return floor;
        }

        private bool Strike(Component target) =>
            (bool)Call(target, "TryPistolStrike", player.gameObject);

        private void Shoot(Component altar) => Call(altar, "TakeDamage", 25f,
            altar.transform.position, Vector3.forward, player.gameObject);

        private static string State(Component altar) => Get<object>(altar, "State").ToString();

        private static T Get<T>(Component component, string property) =>
            (T)component.GetType().GetProperty(property, Members).GetValue(component);

        private static FieldInfo Field(Component component, string field) =>
            component.GetType().GetField(field, Members);

        private static object Call(Component component, string method, params object[] arguments) =>
            component.GetType().GetMethod(method, Members).Invoke(component, arguments);

        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            if (room.IsValid() && room.isLoaded)
                yield return SceneManager.UnloadSceneAsync(room);

            float restoredTimeScale = Time.timeScale;
            Time.timeScale = initialTimeScale;
            Assert.That(restoredTimeScale, Is.EqualTo(initialTimeScale), "Unloading must release the victory pause.");
        }
    }
}
