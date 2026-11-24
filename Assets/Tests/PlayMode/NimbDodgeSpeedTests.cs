using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class NimbDodgeSpeedTests
    {
        private const string FinalRoom = "Assets/Scenes/Levels/Temple_Final_Room.unity";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] Attacks = { "EyeSeal", "HeadDash", "HandTouch", "LightArrows", "HaloWave" };
        private Scene room;
        private float previousStep;

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            previousStep = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            yield return SceneManager.LoadSceneAsync(FinalRoom, LoadSceneMode.Additive);
            room = SceneManager.GetSceneByPath(FinalRoom);
        }

        [UnityTest]
        public IEnumerator RunningClearsTheAttackWhereWalkingGetsHit([ValueSource(nameof(Attacks))] string name)
        {
            var roots = room.GetRootGameObjects();
            foreach (var guard in roots.Where(root => root.GetComponent("RifleNpc") != null))
                guard.SetActive(false);
            var boss = (Behaviour)roots.Select(root => root.GetComponent("AngelSealAttack")).Single(component => component != null);
            NimbAttackSelectionTests.UseFixedOrder(boss);
            var player = roots.Single(root => root.CompareTag("Player")).GetComponent<CharacterController>();
            var locomotion = player.GetComponent("IvanLocomotion");
            var health = player.GetComponent("CombatHealth");
            var hp = health.GetType().GetProperty("HP");
            var next = boss.GetType().GetField("nextAttack", Private);
            string activeFlag = name switch
            {
                "EyeSeal" => "casting",
                "HeadDash" => "dashing",
                "HandTouch" => "touching",
                "LightArrows" => "firingArrows",
                _ => "sweeping"
            };
            var active = boss.GetType().GetField(activeFlag, Private);
            yield return null;

            foreach (bool run in new[] { true, false })
            {
                Vector3 bossPosition = new Vector3(-6f, boss.transform.position.y, 2f);
                Vector3 start = new Vector3(6f, player.transform.position.y, 0f);
                Vector3 direction = Vector3.forward;
                float travel = 15f;
                if (name == "HaloWave")
                {
                    bossPosition.x = bossPosition.z = 0f;
                    var column = roots.SelectMany(root => root.GetComponentsInChildren<CapsuleCollider>())
                        .Where(c => c.name == "Hall column").OrderByDescending(c => c.bounds.center.z).First().bounds;
                    Vector3 outward = Vector3.ProjectOnPlane(column.center - bossPosition, Vector3.up).normalized;
                    direction = Vector3.Cross(Vector3.up, outward);
                    Vector3 shelter = column.center + outward * 1.9f;
                    travel = column.extents.x * 1.2f + 3.5f;
                    start = shelter - direction * travel;
                    start.y = player.transform.position.y;
                }
                Vector3 facing = Vector3.ProjectOnPlane(start - bossPosition, Vector3.up);
                boss.transform.SetPositionAndRotation(bossPosition, Quaternion.LookRotation(facing));
                player.enabled = false;
                player.transform.position = start;
                player.enabled = true;
                Physics.SyncTransforms();

                float speed = (float)locomotion.GetType().GetField(run ? "runSpeed" : "walkSpeed", Private).GetValue(locomotion);
                float before = (float)hp.GetValue(health);
                next.SetValue(boss, System.Enum.Parse(next.FieldType, name));
                boss.GetType().GetField("engaged", Private).SetValue(boss, true);
                boss.GetType().GetField("nextCast", Private).SetValue(boss, Time.time);
                float deadline = Time.time + 1f;
                while (!(bool)active.GetValue(boss) && Time.time < deadline) yield return null;
                Assert.IsTrue((bool)active.GetValue(boss), name);
                deadline = Time.time + 10f;
                while ((bool)active.GetValue(boss) && Time.time < deadline)
                {
                    float step = Mathf.Min(speed * Time.deltaTime, travel);
                    player.Move(direction * step);
                    travel -= step;
                    yield return null;
                }
                Assert.IsFalse((bool)active.GetValue(boss), name);
                Assert.AreEqual(!run, (float)hp.GetValue(health) < before,
                    $"{name} at {speed:F2} m/s on the same open path");
            }
        }

        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            Time.captureDeltaTime = previousStep;
            if (room.IsValid() && room.isLoaded) yield return SceneManager.UnloadSceneAsync(room);
        }
    }
}
