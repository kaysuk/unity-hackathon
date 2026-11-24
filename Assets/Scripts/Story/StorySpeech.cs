using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Dialogue uses unscaled audio time so that comics can pause gameplay while the actors speak.
public sealed class StorySpeech : MonoBehaviour
{
    [Serializable] private sealed class Line
    {
        public string path;
        public float start;
        public float end;
        public bool effect;
    }
    [Serializable] private sealed class Cue { public string key; public Line[] lines; }
    [Serializable] private sealed class Sheet { public Cue[] cues; }

    private static StorySpeech instance;
    private readonly Dictionary<string, Line[]> cues = new();
    private readonly HashSet<string> heard = new();
    private readonly Queue<(string key, Line line)> pending = new();
    private AudioSource source;
    private int endSample;
    private bool inComic;
    private string playingKey;

    public static bool Busy => instance != null && (instance.playingKey != null || instance.pending.Count > 0);

    private void Awake()
    {
        instance = this;
        var child = new GameObject("Озвучка");
        child.transform.SetParent(transform, false);
        source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = false;
        source.bypassReverbZones = true;
        source.priority = 32;
        var file = Resources.Load<TextAsset>("SpeechCues");
        if (file != null)
            foreach (var cue in JsonUtility.FromJson<Sheet>(file.text).cues)
                cues.Add(cue.key, cue.lines);
        SceneManager.activeSceneChanged += SceneChanged;
    }

    private void SceneChanged(Scene previous, Scene next)
    {
        heard.Clear();
        if (!inComic) StopLines();
    }

    internal static bool Page(int number, string suffix)
    {
        if (instance == null) return false;
        instance.inComic = true;
        instance.StopLines();
        return instance.Enqueue($"page.{number:00}{suffix}");
    }

    internal static void EndComic()
    {
        if (instance == null) return;
        instance.StopLines();
        instance.inComic = false;
    }

    public static void Say(string key, bool once = true) => PlayGameplayCue(key, once, interrupt: false);

    internal static void SayPrompt(string key, bool once = true) => PlayGameplayCue(key, once, interrupt: true);

    private static void PlayGameplayCue(string key, bool once, bool interrupt)
    {
        if (instance == null || instance.inComic || !instance.cues.ContainsKey(key)) return;
        if (once && !instance.heard.Add(key)) return;
        if (interrupt)
        {
            if (instance.playingKey == key) return;
            instance.StopLines();
        }
        else if (!once && Busy) return;
        instance.Enqueue(key);
    }

    private bool Enqueue(string key)
    {
        if (!cues.TryGetValue(key, out Line[] lines)) return false;
        foreach (var line in lines) pending.Enqueue((key, line));
        if (playingKey == null) Next();
        return playingKey != null;
    }

    private void Next()
    {
        playingKey = null;
        source.Stop();
        while (pending.Count > 0)
        {
            var (key, line) = pending.Dequeue();
            AudioClip clip = Resources.Load<AudioClip>(line.path);
            if (clip == null)
            {
                Debug.LogError($"Не найдена озвучка: {line.path}");
                continue;
            }
            source.clip = clip;
            GameSoundLevels.Route(source, line.effect ? GameSoundChannel.Effects : GameSoundChannel.Dialogue);
            int first = Mathf.Clamp(Mathf.RoundToInt(line.start * clip.frequency), 0, clip.samples - 1);
            endSample = line.end > 0f ? Mathf.Clamp(Mathf.RoundToInt(line.end * clip.frequency), first + 1, clip.samples) : clip.samples;
            source.timeSamples = first;
            source.Play();
            if (endSample < clip.samples)
                source.SetScheduledEndTime(AudioSettings.dspTime + (endSample - first) / (double)clip.frequency);
            playingKey = key;
            return;
        }
        source.clip = null;
    }

    private void Update()
    {
        if (playingKey != null && (!source.isPlaying || source.timeSamples >= endSample))
            Next();
    }

    private void StopLines()
    {
        pending.Clear();
        playingKey = null;
        source.Stop();
        source.clip = null;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= SceneChanged;
        if (instance == this) instance = null;
    }
}
