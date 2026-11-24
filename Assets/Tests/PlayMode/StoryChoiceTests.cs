using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hackathon.Tests
{
    public sealed class StoryChoiceTests
    {
        private static readonly string[] Inputs = { "Mouse", "Artwork", "Keyboard", "Gamepad" };

        [UnityTest]
        public IEnumerator OfficeChoiceMatchesTheArtwork([ValueSource(nameof(Inputs))] string layout)
        {
            var comics = StoryPlaybackTests.Comics;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var play = comics.GetType().GetMethod("Play", flags);
            var close = comics.GetType().GetMethod("Close", flags);
            var choice = comics.GetType().GetProperty("DmitrySpared");
            var savedChoice = (bool)choice.GetValue(comics);
            var settings = InputSystem.settings;
            var inputRouting = settings.editorInputBehaviorInPlayMode;
            var background = settings.backgroundBehavior;
            var originalAudio = GameObject.Find("Game audio");
            var device = InputSystem.AddDevice(layout == "Artwork" ? "Mouse" : layout);
            Coroutine playback = null;
            GameObject listener = null;
            try
            {
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.EnableDevice(device);
                if (Object.FindAnyObjectByType<AudioListener>() == null)
                    listener = new GameObject("Choice test listener", typeof(AudioListener));

                foreach (bool spare in new[] { false, true })
                {
                    choice.SetValue(comics, !spare);
                    playback = comics.StartCoroutine((IEnumerator)play.Invoke(comics, new object[] { 28, 29, null, false }));
                    yield return null;
                    Assert.AreEqual(28, StoryPlaybackTests.Page);
                    Canvas.ForceUpdateCanvases();
                    var canvas = GameObject.Find("Комикс").GetComponent<Canvas>();
                    var labels = canvas.GetComponentsInChildren<Text>();
                    var killLabel = labels.Single(label => label.text.Contains("Убить Дмитрия"));
                    var spareLabel = labels.Single(label => label.text.Contains("Оставить в живых"));
                    StringAssert.Contains("1 / X", killLabel.text);
                    StringAssert.Contains("2 / B", spareLabel.text);
                    Vector2 killPoint = OnScreen(killLabel.rectTransform);
                    Vector2 sparePoint = OnScreen(spareLabel.rectTransform);
                    Assert.Less(killPoint.x, canvas.pixelRect.center.x);
                    Assert.Greater(sparePoint.x, canvas.pixelRect.center.x);

                    var picture = canvas.GetComponentInChildren<RawImage>();
                    switch (device)
                    {
                        case Mouse mouse:
                            Vector2 target = spare ? sparePoint : killPoint;
                            if (layout == "Artwork")
                            {
                                float x = spare ? .8f : .2f;
                                var ignored = new MouseState { position = OnScreen(picture.rectTransform, x, .65f) };
                                InputSystem.QueueStateEvent(mouse, ignored.WithButton(MouseButton.Left));
                                yield return null;
                                yield return null;
                                Assert.AreEqual(28, StoryPlaybackTests.Page, "The picture above the captions is inactive.");
                                InputSystem.QueueStateEvent(mouse, ignored.WithButton(MouseButton.Left, false));
                                yield return null;
                                target = OnScreen(picture.rectTransform, x, .06f);
                            }
                            var pointer = new MouseState { position = target };
                            InputSystem.QueueStateEvent(mouse, pointer.WithButton(MouseButton.Left));
                            break;
                        case Keyboard keyboard:
                            InputSystem.QueueStateEvent(keyboard, new KeyboardState(spare ? Key.Digit2 : Key.Digit1));
                            break;
                        case Gamepad pad:
                            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(spare ? GamepadButton.East : GamepadButton.West));
                            break;
                    }

                    for (int i = 0; i < 8 && StoryPlaybackTests.Page != 29; i++) yield return null;
                    Assert.AreEqual(29, StoryPlaybackTests.Page, layout);
                    Assert.AreEqual(spare, choice.GetValue(comics));
                    Assert.AreEqual(spare ? "29_alive" : "29_dead", picture.texture.name);

                    comics.StopCoroutine(playback);
                    playback = null;
                    close.Invoke(comics, null);
                    InputSystem.ResetDevice(device);
                    yield return null;
                }
            }
            finally
            {
                if (playback != null) comics.StopCoroutine(playback);
                close.Invoke(comics, null);
                choice.SetValue(comics, savedChoice);
                InputSystem.RemoveDevice(device);
                settings.backgroundBehavior = background;
                settings.editorInputBehaviorInPlayMode = inputRouting;
                if (listener != null) Object.Destroy(listener);
                if (originalAudio == null && GameObject.Find("Game audio") is GameObject createdAudio)
                    Object.Destroy(createdAudio);
            }
        }

        private static Vector2 OnScreen(RectTransform rect, float x = .5f, float y = .5f)
        {
            Vector2 localPoint = Rect.NormalizedToPoint(rect.rect, new Vector2(x, y));
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(localPoint));
        }
    }
}
