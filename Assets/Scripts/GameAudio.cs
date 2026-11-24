using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

internal enum SoundCue
{
    StoneLeft1, StoneRight1, StoneLeft2, StoneRight2,
    EarthLeft1, EarthRight1, EarthLeft2, EarthRight2,
    Cloth, Scuff, Draw, Stow, MagazineOut, MagazineIn, DryFire, Pistol,
    BodyHit, BodyFall, Land, Grapple, WallHit, Key, Paper, Stone,
    Lever, Switch, Door, Gate, GateStop, Slide, Seal, SealFail,
    AngelCharge, AngelStrike, HaloGrip, HaloWave, MenuClick
}

internal sealed class GameAudio : MonoBehaviour
{
    internal const string BossTrack = "sounds/ambience/boss_fight";
    private const string Bank = "GameAudio/";
    private static GameAudio instance;
    private readonly Dictionary<SoundCue, AudioClip> clips = new();
    private readonly List<AudioSource> voices = new();
    private readonly AudioSource[] music = new AudioSource[2];
    private AudioReverbZone room;
    private int currentMusic;
    private string track;
    private string sceneTrack;
    private bool scoreHeld;
    private AudioSource heartbeat;
    private float nextAlert;
    private uint pitchSeed = 47;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single && instance == null)
            OnActiveSceneChanged(default, scene);
    }

    private static void OnActiveSceneChanged(Scene previous, Scene next)
    {
        string nextTrack = next.name switch
        {
            "Temple_Final_Room" => BossTrack,
            "Church_Exterior" => "sounds/ambience/church_exterior",
            "Church_Interior" => "sounds/ambience/church_interior",
            "temple_v2" or "temple_firts_v2" or "SampleScene" => "sounds/ambience/temple",
            "MainMenu" => Bank + "Undercroft",
            _ => null
        };
        if (nextTrack == null)
        {
            if (instance != null)
            {
                instance.gameObject.SetActive(false);
                Destroy(instance.gameObject);
                instance = null;
            }
            return;
        }
        if (instance == null) Create();
        instance.ChangeScene(next, nextTrack);
    }

    private static void Create()
    {
        var root = new GameObject("Game audio");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<GameAudio>();
        instance.room = root.AddComponent<AudioReverbZone>();
        instance.room.maxDistance = 520f;
        instance.room.minDistance = 500f;
        instance.room.enabled = false;
        for (int i = 0; i < instance.music.Length; i++)
        {
            var source = instance.AddSource("Score");
            source.loop = true;
            source.volume = 0f;
            source.priority = 64;
            source.bypassReverbZones = true;
            instance.music[i] = source;
        }
    }

    private void Start() => GameSoundLevels.ApplySaved();

    private AudioSource AddSource(string label)
    {
        var emitter = new GameObject(label);
        emitter.transform.SetParent(transform, false);
        var source = emitter.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.dopplerLevel = 0f;
        GameSoundLevels.Route(source, GameSoundChannel.Effects);
        return source;
    }

    private void ChangeScene(Scene next, string nextTrack)
    {
        foreach (var voice in voices) voice.Stop();
        nextAlert = 0f;
        room.enabled = next.name is "temple_v2" or "temple_firts_v2" or "Temple_Final_Room" or "Church_Interior";
        if (room.enabled)
            room.reverbPreset = next.name == "Temple_Final_Room" ? AudioReverbPreset.Auditorium : AudioReverbPreset.Stoneroom;
        sceneTrack = nextTrack;
        if (!scoreHeld) SetTrack(nextTrack);
    }

    internal static void HoldScore(string cue)
    {
        if (instance == null) Create();
        if (!instance.scoreHeld)
        {
            foreach (var voice in instance.voices) voice.Stop();
            if (instance.heartbeat != null) instance.heartbeat.Stop();
        }
        instance.scoreHeld = true;
        instance.SetTrack(cue);
    }

    internal static void ReleaseScore()
    {
        if (instance == null) return;
        instance.scoreHeld = false;
        instance.SetTrack(instance.sceneTrack);
    }

    internal static void Heartbeat(bool lowHealth)
    {
        if (instance == null) return;
        if (!lowHealth || instance.scoreHeld)
        {
            if (instance.heartbeat != null && instance.heartbeat.isPlaying) instance.heartbeat.Stop();
            return;
        }
        if (instance.heartbeat == null)
        {
            instance.heartbeat = instance.AddSource("Сердцебиение");
            instance.heartbeat.loop = true;
            instance.heartbeat.volume = 0.38f;
            instance.heartbeat.bypassReverbZones = true;
            instance.heartbeat.clip = Resources.Load<AudioClip>("sounds/player_sounds/player_heartb");
        }
        if (!instance.heartbeat.isPlaying) instance.heartbeat.Play();
    }

    private void SetTrack(string nextTrack)
    {
        if (nextTrack == track) return;
        track = nextTrack;
        currentMusic = 1 - currentMusic;
        AudioSource incoming = music[currentMusic];
        incoming.Stop();
        incoming.volume = 0f;
        bool ambience = track is "sounds/ambience/church_exterior" or "sounds/ambience/church_interior"
            or "sounds/ambience/temple" or "sounds/comics/comics5-6";
        GameSoundLevels.Route(incoming, ambience ? GameSoundChannel.Effects : GameSoundChannel.Music);
        incoming.clip = track == null ? null : Resources.Load<AudioClip>(track);
        if (incoming.clip != null) incoming.Play();
        else if (track != null) Debug.LogError($"Нет аудиоклипа Resources/{track}");
    }

    private void Update()
    {
        for (int i = 0; i < music.Length; i++)
        {
            var source = music[i];
            float target = i == currentMusic && track != null ? (scoreHeld || track == BossTrack ? 0.39f : 0.25f) : 0f;
            source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime * 0.9f);
            if (i != currentMusic && source.volume == 0f && source.isPlaying) source.Stop();
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    internal static void Alert(Vector3 position, AudioClip clip)
    {
        if (instance == null || clip == null || Time.time < instance.nextAlert) return;
        instance.nextAlert = Time.time + 3f;
        instance.PlayVoice(clip, position, 0.73f, 5f, 1f);
    }

    internal static void PistolShot(Vector3 position, AudioClip clip = null)
    {
        if (clip != null && instance != null) instance.PlayVoice(clip, position, 0.87f, 5f, 1f);
        else Play(SoundCue.Pistol, position, 0.87f, 5f);
    }

    internal static void Interface(SoundCue cue) => Emit(cue, Vector3.zero, 0.36f, 0f, 1f);

    internal static void Play(SoundCue cue, Vector3 position, float volume = 0.55f,
        float near = 2.8f, float pitch = 1f) => Emit(cue, position, volume, near, pitch);

    private static void Emit(SoundCue cue, Vector3 position, float volume, float near, float pitch)
    {
        if (instance == null) return;
        if (!instance.clips.TryGetValue(cue, out var clip))
        {
            clip = Resources.Load<AudioClip>(Bank + cue);
            instance.clips.Add(cue, clip);
        }
        if (clip == null) return;
        instance.PlayVoice(clip, position, volume, near, pitch);
    }

    private void PlayVoice(AudioClip clip, Vector3 position, float volume, float near, float pitch)
    {
        if (near > 0f && Time.timeScale == 0f) return;
        AudioSource voice = voices.Find(source => !source.isPlaying);
        if (voice == null)
        {
            if (voices.Count >= 32) return;
            voice = AddSource("One shot");
            voices.Add(voice);
        }
        // Keep foley variation independent of gameplay randomness.
        pitchSeed = unchecked(pitchSeed * 1664525u + 1013904223u);
        voice.pitch = pitch * Mathf.Lerp(0.965f, 1.035f, (pitchSeed >> 16) / 65535f);
        voice.transform.position = position;
        voice.spatialBlend = near > 0f ? 1f : 0f;
        voice.reverbZoneMix = near > 0f ? 0.56f : 0f;
        voice.minDistance = Mathf.Max(1f, near);
        voice.maxDistance = Mathf.Max(20f, near * 10f);
        voice.volume = volume;
        voice.clip = clip;
        voice.Play();
    }
}

// Accumulate actual travel only while the capsule is in contact with the floor.
internal struct PlayerFootsteps
{
    private float distance;
    private int step;
    private bool grounded;
    private float fallingSpeed;

    internal void Move(CharacterController body, bool crouched)
    {
        var velocity = body.velocity;
        if (!body.isGrounded)
        {
            fallingSpeed = Mathf.Max(fallingSpeed, -velocity.y);
            grounded = false;
            distance = 0f;
            return;
        }
        var feet = NPCMover.FootPosition(body);
        if (!grounded && fallingSpeed > 3.5f)
        {
            GameAudio.Play(SoundCue.Land, feet, 0.48f);
            if (fallingSpeed > 6f && body.CompareTag("Player") && body.gameObject.scene == SceneManager.GetActiveScene())
                StorySpeech.Say("ivan.landing_exhale", false);
        }
        grounded = true;
        fallingSpeed = 0f;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;
        if (speed < 0.12f) { distance = 0f; return; }
        float running = Mathf.InverseLerp(1.7f, 3.2f, speed);
        float stride = crouched ? 0.5f : Mathf.Lerp(0.83f, 1.38f, running);
        distance += speed * Time.deltaTime;
        if (distance < stride) return;
        distance %= stride;
        bool earth = false;
        if (Physics.Raycast(feet + Vector3.up * 0.12f, Vector3.down, out var hit, 0.5f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            string surface = hit.collider.name;
            earth = surface.Contains("Grass", StringComparison.OrdinalIgnoreCase)
                || surface.Contains("Ground", StringComparison.OrdinalIgnoreCase)
                || surface.Contains("Soil", StringComparison.OrdinalIgnoreCase);
        }
        var first = earth ? SoundCue.EarthLeft1 : SoundCue.StoneLeft1;
        GameAudio.Play((SoundCue)((int)first + step), feet, crouched ? 0.14f : Mathf.Lerp(0.34f, 0.53f, running));
        GameAudio.Play(SoundCue.Cloth, feet + Vector3.up, crouched ? 0.075f : 0.11f + 0.08f * running);
        step = (step + 1) % 4;
    }
}
