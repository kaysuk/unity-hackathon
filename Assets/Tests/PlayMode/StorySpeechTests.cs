using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hackathon.Tests
{
    public sealed class StorySpeechTests
    {
        [UnityTest]
        public IEnumerator OfficePhoneAlternatesSpeakersAndClearsDialogueWhenLeaving()
        {
            var comics = StoryPlaybackTests.Comics;
            var voice = comics.transform.Find("Озвучка").GetComponent<AudioSource>();
            var play = comics.GetType().GetMethod("Play", BindingFlags.Instance | BindingFlags.NonPublic);
            var close = comics.GetType().GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic);
            var nextPage = comics.GetType().GetMethod("NextPage");
            var busy = comics.GetComponent("StorySpeech").GetType().GetProperty("Busy");
            var originalAudio = GameObject.Find("Game audio");
            Assert.That(StoryPlaybackTests.Page, Is.Zero);

            GameObject listener = null;
            Coroutine playback = null;
            try
            {
                if (Object.FindAnyObjectByType<AudioListener>() == null)
                    listener = new GameObject("Office speech test listener", typeof(AudioListener));

                playback = comics.StartCoroutine((IEnumerator)play.Invoke(comics, new object[] { 21, 22, null }));
                yield return ExpectLine(voice, 21, "OI.27.ask_about_raid");
                SeekToLastMoment(voice);
                yield return ExpectLine(voice, 21, "ON.01.phone_identify_colleague");
                SeekToLastMoment(voice);
                yield return ExpectLine(voice, 21, "OI.28.who_is_this_where_is_she");

                // Skip while Ivan is speaking and the nurse still has two replies queued.
                nextPage.Invoke(comics, null);
                yield return ExpectLine(voice, 22, "OI.30.i_called_her");
                SeekToLastMoment(voice);
                yield return ExpectLine(voice, 22, "OD.07.warns_against_involving_colleague");

                close.Invoke(comics, null);
                yield return null;
                Assert.That(StoryPlaybackTests.Page, Is.Zero);
                Assert.That(voice.isPlaying, Is.False);
                Assert.That(voice.clip, Is.Null);
                Assert.That((bool)busy.GetValue(null), Is.False, "Closing must also cancel queued replies.");
            }
            finally
            {
                if (playback != null) comics.StopCoroutine(playback);
                close.Invoke(comics, null);
                if (listener != null) Object.Destroy(listener);
                if (originalAudio == null && GameObject.Find("Game audio") is GameObject createdAudio)
                    Object.Destroy(createdAudio);
            }
        }

        private static void SeekToLastMoment(AudioSource voice)
        {
            // Let the actual source finish and let the normal Update start the next speaker.
            var clip = voice.clip;
            voice.timeSamples = Mathf.Max(0, clip.samples - clip.frequency / 12);
        }

        private static IEnumerator ExpectLine(AudioSource voice, int page, string name)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((voice.clip == null || voice.clip.name != name || !voice.isPlaying) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(StoryPlaybackTests.Page, Is.EqualTo(page));
            Assert.That(voice.clip?.name, Is.EqualTo(name));
            Assert.That(voice.isPlaying, Is.True);
            Assert.That(voice.loop, Is.False);
            Assert.That(voice.outputAudioMixerGroup?.name, Is.EqualTo("Dialogue"));
        }
    }
}
