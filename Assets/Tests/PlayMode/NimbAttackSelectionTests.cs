using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Random = UnityEngine.Random;

namespace Hackathon.Tests
{
    public sealed class NimbAttackSelectionTests
    {
        private const string FinalRoom = "Assets/Scenes/Levels/Temple_Final_Room.unity";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private Scene room;

        internal static void UseFixedOrder(Component boss)
        {
            boss.GetType().GetField("useFixedAttackOrder", Members).SetValue(boss, true);
        }

        [UnityTest]
        public IEnumerator ArenaUsesAdaptiveSelectionAndRemembersOnlyThreeCompletedCasts()
        {
            yield return SceneManager.LoadSceneAsync(FinalRoom, LoadSceneMode.Additive);
            room = SceneManager.GetSceneByPath(FinalRoom);
            var roots = room.GetRootGameObjects();
            var boss = (Behaviour)roots.Select(root => root.GetComponent("AngelSealAttack")).Single(item => item != null);
            var player = roots.Single(root => root.CompareTag("Player")).GetComponent<CharacterController>();
            foreach (var guard in roots.Where(root => root.GetComponent("RifleNpc") != null))
                guard.SetActive(false);
            yield return null;

            var type = boss.GetType();
            var fixedOrder = type.GetField("useFixedAttackOrder", Members);
            var next = type.GetField("nextAttack", Members);
            var nextCast = type.GetField("nextCast", Members);
            var castStart = type.GetField("castStart", Members);
            var engaged = type.GetField("engaged", Members);
            var recent = type.GetProperty("RecentCoverReactions", Members);
            var tick = type.GetMethod("TickAttack", Members);
            var seal = Enum.Parse(next.FieldType, "EyeSeal");
            Assert.AreEqual(false, fixedOrder.GetValue(boss), "The shipped arena should use the model.");
            var column = roots.SelectMany(root => root.GetComponentsInChildren<CapsuleCollider>())
                .Where(c => c.name == "Hall column")
                .OrderBy(c => Vector3.ProjectOnPlane(c.bounds.center - boss.transform.position, Vector3.up).sqrMagnitude)
                .First().bounds;
            var outward = Vector3.ProjectOnPlane(column.center - boss.transform.position, Vector3.up).normalized;
            var open = column.center - outward * 2.5f;
            var shelter = column.center + outward * 2.5f;
            void Place(Vector3 position)
            {
                position.y = player.transform.position.y;
                player.enabled = false;
                player.transform.position = position;
                player.enabled = true;
                Physics.SyncTransforms();
            }

            void StartAttack()
            {
                engaged.SetValue(boss, true);
                nextCast.SetValue(boss, Time.time);
                tick.Invoke(boss, null);
            }

            // Reproduce an upper-tail weighted draw, without changing the RNG seen by other tests.
            var oldRandom = Random.state;
            try
            {
                Random.InitState(42);
                Random.State beforeDraw;
                do { beforeDraw = Random.state; } while (Random.value < .98f);
                Place(open);
                Random.state = beforeDraw;
                StartAttack();
            }
            finally { Random.state = oldRandom; }
            Assert.AreNotEqual("EyeSeal", type.GetField("lastAttack", Members).GetValue(boss)?.ToString(),
                "Adaptive selection should use the weighted roll instead of the first debug attack.");
            Assert.IsTrue(new[] { "sweeping", "firingArrows", "dashing", "touching" }
                .Any(name => (bool)type.GetField(name, Members).GetValue(boss)));

            // Reuse the same telegraph and geometry to exercise history deterministically.
            boss.enabled = false;
            boss.enabled = true;
            UseFixedOrder(boss);
            type.GetField("damage", Members).SetValue(boss, 0f);
            void FinishSeal(Vector3 start, Vector3 end)
            {
                Place(start);
                next.SetValue(boss, seal);
                StartAttack();
                Place(end);
                tick.Invoke(boss, null);
                castStart.SetValue(boss, Time.time - 6f);
                tick.Invoke(boss, null);
            }

            FinishSeal(shelter, shelter);
            Assert.AreEqual(0, recent.GetValue(boss), "Already being in cover is not a reaction.");
            FinishSeal(open, shelter);
            FinishSeal(open, shelter);
            Assert.AreEqual(2, recent.GetValue(boss));
            FinishSeal(open, open);
            FinishSeal(open, open);
            Assert.AreEqual(1, recent.GetValue(boss));
            FinishSeal(open, open);
            Assert.AreEqual(0, recent.GetValue(boss));
            FinishSeal(open, shelter);
            Assert.AreEqual(1, recent.GetValue(boss));
            boss.enabled = false;
            Assert.AreEqual(0, recent.GetValue(boss), "A new encounter must start with empty history.");
        }

        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            if (room.IsValid() && room.isLoaded) yield return SceneManager.UnloadSceneAsync(room);
        }
    }
}
