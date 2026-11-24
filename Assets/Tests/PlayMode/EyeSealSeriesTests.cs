using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public class EyeSealSeriesTests
    {
        private const string FinalRoom = "Assets/Scenes/Levels/Temple_Final_Room.unity";
        private const int MarkCount = 5;
        private const float MarkInterval = .65f;
        private const float WarningSeconds = .88f;
        private Scene room;
        public enum Response { HoldPosition, StayOnLastMark, EvadeAll, Interrupt }
        private static readonly Response[] Responses =
            { Response.HoldPosition, Response.StayOnLastMark, Response.EvadeAll, Response.Interrupt };

        [UnityTest]
        public IEnumerator FiveSealsFollowIvanAndStrikeAfterTheirOwnWarnings([ValueSource(nameof(Responses))] Response response)
        {
            yield return SceneManager.LoadSceneAsync(FinalRoom, LoadSceneMode.Additive);
            room = SceneManager.GetSceneByPath(FinalRoom);
            var roots = room.GetRootGameObjects();
            foreach (var guard in roots.Where(root => root.GetComponent("RifleNpc") != null))
                guard.SetActive(false);
            var player = roots.Single(root => root.CompareTag("Player"));
            var controller = player.GetComponent<CharacterController>();
            var health = player.GetComponent("CombatHealth");
            var hp = health.GetType().GetProperty("HP");
            float initialHP = (float)hp.GetValue(health);
            var boss = (Behaviour)roots.Select(root => root.GetComponent("AngelSealAttack")).Single(attack => attack != null);
            NimbAttackSelectionTests.UseFixedOrder(boss);

            // Keep sampling at a known game-time interval, even on slow batch-mode editors.
            float previousStep = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 40f;
            try
            {
                yield return null;
                var rings = boss.GetComponentsInChildren<LineRenderer>(true)
                    .Where(renderer => renderer.name == "Seal boundary").ToArray();
                Assert.AreEqual(MarkCount, rings.Length);
                var beam = boss.GetComponentsInChildren<MeshRenderer>(true)
                    .Single(renderer => renderer.name == "Seal light pillar");
                var eyes = rings.Select(ring => ring.GetComponentsInChildren<MeshRenderer>(true)
                    .Single(renderer => renderer.name == "Eye of Providence")).ToArray();

                var seen = new Dictionary<LineRenderer, Vector3>();
                var marks = new List<Vector3>(MarkCount);
                float start = 0f;
                int strikes = 0;
                float previousBeamProgress = float.PositiveInfinity;
                var properties = new MaterialPropertyBlock();
                int progressId = Shader.PropertyToID("_BeamProgress");
                bool damaged = false;
                float deadline = Time.time + 12f;
                while (Time.time < deadline)
                {
                    for (int i = 0; i < rings.Length; i++)
                    {
                        var ring = rings[i];
                        if (!ring.enabled) continue;
                        Assert.IsTrue(eyes[i].enabled);
                        if (seen.TryGetValue(ring, out Vector3 locked))
                        {
                            Assert.Less(Vector3.Distance(locked, ring.transform.position), .002f);
                            continue;
                        }
                        if (marks.Count == 0) start = Time.time;
                        Assert.That(Time.time - start, Is.EqualTo(marks.Count * MarkInterval).Within(.051f));
                        Vector3 mark = ring.transform.position;
                        Assert.Less(Vector3.Distance(mark, player.transform.position), .10f);
                        marks.Add(mark);
                        seen.Add(ring, mark);
                        if (response == Response.EvadeAll || (response == Response.StayOnLastMark && marks.Count < MarkCount))
                        {
                            // A short square route keeps all five target positions on clear ground.
                            Vector3 step;
                            switch (marks.Count % 4)
                            {
                                case 1: step = Vector3.right; break;
                                case 2: step = Vector3.forward; break;
                                case 3: step = Vector3.left; break;
                                default: step = Vector3.back; break;
                            }
                            controller.Move(step * 2.5f);
                        }
                    }

                    // A slow frame can span the dark gap between two uses of the shared beam.
                    beam.GetPropertyBlock(properties);
                    float progress = properties.GetFloat(progressId);
                    if (beam.enabled && progress < previousBeamProgress)
                    {
                        Assert.That(strikes, Is.LessThan(marks.Count));
                        Assert.That(Time.time - start, Is.EqualTo(WarningSeconds + strikes * MarkInterval).Within(.051f));
                        Assert.Less(Vector3.Distance(beam.transform.position, marks[strikes]), .10f);
                        strikes++;
                        if (response == Response.HoldPosition || response == Response.Interrupt ||
                            (response == Response.StayOnLastMark && strikes == MarkCount))
                            damaged = true;
                    }
                    previousBeamProgress = beam.enabled ? progress : float.PositiveInfinity;
                    Assert.AreEqual(initialHP - (damaged ? 35f : 0f), (float)hp.GetValue(health));

                    if (response == Response.Interrupt && strikes == 1)
                    {
                        boss.enabled = false;
                        Assert.IsFalse(beam.enabled || rings.Any(r => r.enabled) || eyes.Any(e => e.enabled));
                        Assert.IsFalse(beam.GetComponentsInChildren<Light>(true).Any(light => light.enabled));
                        yield return new WaitForSeconds(2.5f);
                        Assert.AreEqual(initialHP - 35f, (float)hp.GetValue(health));
                        yield break;
                    }
                    if (strikes == MarkCount && !beam.enabled) break;
                    yield return null;
                }
                Assert.AreEqual(MarkCount, seen.Count);
                Assert.AreEqual(MarkCount, strikes);
                Assert.IsFalse(beam.enabled || rings.Any(r => r.enabled) || eyes.Any(e => e.enabled));
            }
            finally
            {
                Time.captureDeltaTime = previousStep;
            }
        }

        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            if (room.IsValid() && room.isLoaded) yield return SceneManager.UnloadSceneAsync(room);
        }
    }
}
