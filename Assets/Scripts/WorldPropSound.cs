using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class WorldPropSound : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField] private AudioClip alternate;
    [SerializeField, Range(0f, 1f)] private float gain = 0.4f;
    [SerializeField, Min(0.5f)] private float radius = 4f;
    [Tooltip("World-space room bounds; leave empty for open-air sounds.")]
    [SerializeField] private Bounds listenerRoom;
    [Tooltip("Silence in seconds between one-shots; leave at zero for a continuous loop.")]
    [SerializeField] private Vector2 interval;

    private AudioSource source;
    private System.Random variation;
    private float nextShot;

    private void Awake()
    {
        variation = new System.Random(transform.position.GetHashCode());
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.clip = clip;
        source.loop = interval.y <= 0f;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 0.5f;
        source.maxDistance = radius;
        source.volume = gain;
        source.priority = 192;
        source.reverbZoneMix = 0.18f;
        GameSoundLevels.Route(source, GameSoundChannel.Effects);
        nextShot = Time.time + 0.8f;
    }

    private void Update()
    {
        var camera = Camera.main;
        if (Time.timeScale == 0f || gameObject.scene != SceneManager.GetActiveScene() || camera == null)
        {
            if (source.isPlaying) source.Stop();
            return;
        }

        Vector3 listener = camera.transform.position;
        bool inRoom = listenerRoom.size == Vector3.zero || listenerRoom.Contains(listener);
        if (!inRoom || (listener - transform.position).sqrMagnitude >= radius * radius)
        {
            if (source.isPlaying) source.Stop();
            return;
        }

        if (source.isPlaying || clip == null) return;
        if (source.loop)
        {
            source.timeSamples = variation.Next(clip.samples);
        }
        else
        {
            if (Time.time < nextShot) return;
            source.clip = alternate != null && source.clip == clip ? alternate : clip;
            float gap = Mathf.Lerp(interval.x, interval.y, (float)variation.NextDouble());
            nextShot = Time.time + source.clip.length + gap;
        }
        source.Play();
    }

    private void OnDisable()
    {
        if (source != null) source.Stop();
    }
}
