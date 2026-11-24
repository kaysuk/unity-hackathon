using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class ItemEffectsTests
    {
        private Scene original, scene, carrier;
        private GameObject audio;
        private float previousTimeScale;

        private IEnumerator Load(string sceneName)
        {
            original = SceneManager.GetActiveScene();
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            string carrierName = sceneName switch
            {
                "Church_Interior" => "Church_Exterior",
                "Temple_Final_Room" => "temple_v2",
                _ => null
            };
            if (carrierName != null)
            {
                yield return SceneManager.LoadSceneAsync(carrierName, LoadSceneMode.Additive);
                carrier = SceneManager.GetSceneByName(carrierName);
            }
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName(sceneName);
            if (carrier.isLoaded)
            {
                var roots = carrier.GetRootGameObjects();
                var actor = roots.Single(root => root.CompareTag("Player") && root.activeSelf);
                var camera = roots.Single(root => root.CompareTag("MainCamera") && root.TryGetComponent<Camera>(out _));
                var transition = System.Type.GetType("SceneTransitionDoor, Assembly-CSharp", true);
                transition.GetMethod("MovePlayer", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { scene, actor.GetComponent<CharacterController>(), camera, new Vector3(0f, .1f, -13f) });
                if (sceneName == "Church_Interior" && actor.GetComponent("PlayerInteractor") == null)
                    actor.AddComponent(System.Type.GetType("PlayerInteractor, Assembly-CSharp", true));
            }
            SceneManager.SetActiveScene(scene);
            if (carrier.isLoaded) yield return SceneManager.UnloadSceneAsync(carrier);
            audio = GameObject.Find("Game audio");
            Assert.That(audio, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (original.isLoaded) SceneManager.SetActiveScene(original);
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (carrier.isLoaded) yield return SceneManager.UnloadSceneAsync(carrier);
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator JournalAndBagPlayOneEffectPerActionWhilePaused()
        {
            yield return Load("temple_v2");
            ClearEffects();
            var player = Components("PlayerInteractor").Single();
            var note = Components("TempleSealDocument").OrderBy(item => item.name).First();
            Call(note, "Interact", player);
            Expect("Paper");
            var journal = player.GetComponent("TempleArchiveJournal");
            var menu = player.GetComponent("InventoryMenu");

            Call(menu, "Open");
            Assert.That(Time.timeScale, Is.Zero);
            Expect("InventoryBag");
            Call(menu, "ChangeSection", 1);
            Expect("MenuClick");
            Call(menu, "ReadSelected");
            Expect("BookOpen");
            Call(journal, "Close");
            Expect("BookClose");
            Assert.That(Time.timeScale, Is.Zero);
            Call(menu, "Close");
            Expect("InventoryBag");

            Call(journal, "ReadDocument", 2);
            Expect("BookOpen");
            Call(journal, "FlipPage", 1);
            Expect("Paper");
            Call(journal, "Close");
            Expect("BookClose");
        }

        [UnityTest]
        public IEnumerator SealsDistinguishHandBulletBreakAndRitualEnd()
        {
            yield return Load("Temple_Final_Room");
            ClearEffects();
            var player = Components("PlayerInteractor").Single();
            var angel = Components("AngelSealAttack").Single();
            player.transform.position = angel.transform.position + Vector3.forward * 12f;
            var altars = Components("RitualAltar").ToArray();
            Assert.That(altars.Length, Is.EqualTo(6));
            var first = altars[0];
            Call(first, "Interact", player);
            Expect("Stone", first.transform.position);
            Shoot(first);
            Expect("AltarHit", first.transform.position);
            Shoot(first);
            Expect("AltarBreak", first.transform.position);
            Assert.That(first.gameObject.activeSelf, Is.False);

            foreach (var altar in altars.Skip(1))
            {
                Shoot(altar);
                Shoot(altar);
            }
            ClearEffects();
            Call(angel, "LateUpdate");
            Expect("AngelDispelled", angel.transform.position);
            Call(angel, "LateUpdate");
            Assert.That(Effects(), Is.Empty);

            void Shoot(Component altar) => Call(altar, "TakeDamage", 1f, altar.transform.position, Vector3.forward, player.gameObject);
        }

        [UnityTest]
        public IEnumerator MovingBookshelfSoundsOnceFromItsStartingPosition()
        {
            yield return Load("Church_Interior");
            ClearEffects();
            var player = Components("PlayerInteractor").Single();
            var shelf = Components("ChurchBookshelf").Single();
            Call(shelf, "Prepare", player.transform, false);
            Vector3 start = shelf.transform.position;
            Call(shelf, "Interact", player);
            Expect("CabinetMove", start);
            Call(shelf, "Interact", player);
            Assert.That(Effects(), Is.Empty);
        }

        private IEnumerable<MonoBehaviour> Components(string typeName) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>())
                .Where(component => component != null && component.GetType().Name == typeName);

        private IEnumerable<AudioSource> Effects() => audio.GetComponentsInChildren<AudioSource>()
            .Where(source => source.name == "One shot" && source.clip != null);

        private void ClearEffects()
        {
            foreach (var source in Effects())
            {
                source.Stop();
                source.clip = null;
            }
        }

        private void Expect(string name, Vector3? worldPosition = null)
        {
            var sources = Effects().ToArray();
            Assert.That(sources.Select(source => source.clip.name), Is.EqualTo(new[] { name }));
            Assert.That(sources[0].spatialBlend, Is.EqualTo(worldPosition.HasValue ? 1f : 0f));
            if (worldPosition.HasValue) Assert.That(sources[0].transform.position, Is.EqualTo(worldPosition.Value));
            ClearEffects();
        }

        private static void Call(Component component, string method, params object[] args) =>
            component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(component, args);
    }
}
