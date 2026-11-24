using UnityEngine;
using UnityEngine.Audio;

public enum GameSoundChannel { Music, Dialogue, Effects }

public static class GameSoundLevels
{
    internal const string MasterPreference = "MainMenu.MasterVolume";
    private static readonly string[] Channels = { "Music", "Dialogue", "Effects" };
    private static readonly AudioMixerGroup[] groups = new AudioMixerGroup[Channels.Length];
    private static AudioMixer mixer;

    private static AudioMixer Mixer
    {
        get
        {
            if (mixer == null) mixer = Resources.Load<AudioMixer>("GameAudio/GameMix");
            return mixer;
        }
    }

    // Route sources once. Native mixer attenuation works independently of source fades and 3D falloff.
    public static void Route(AudioSource source, GameSoundChannel channel)
    {
        int index = (int)channel;
        if (groups[index] == null) groups[index] = Mixer.FindMatchingGroups("Master/" + Channels[index])[0];
        source.outputAudioMixerGroup = groups[index];
    }

    public static float Get(GameSoundChannel channel) =>
        Mathf.Clamp01(PlayerPrefs.GetFloat("Audio." + Channels[(int)channel], 1f));

    public static void Set(GameSoundChannel channel, float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("Audio." + Channels[(int)channel], value);
        Apply(channel, value);
    }

    // Unity accepts exposed mixer changes in Start, after audio initialization.
    internal static void ApplySaved()
    {
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterPreference, 1f));
        for (int i = 0; i < Channels.Length; i++)
        {
            var channel = (GameSoundChannel)i;
            Apply(channel, Get(channel));
        }
    }

    private static void Apply(GameSoundChannel channel, float linearGain)
    {
        float decibels = linearGain > 0f ? Mathf.Max(-80f, 20f * Mathf.Log10(linearGain)) : -80f;
        Mixer.SetFloat(Channels[(int)channel], decibels);
    }
}
