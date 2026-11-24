using UnityEngine;

[DisallowMultipleComponent]
public sealed class AngelSealAttack : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Material sealMaterial;
    [SerializeField, Min(0.1f)] private float radius = 1.5f;
    [SerializeField, Min(0.1f)] private float warningSeconds = 2f;
    [SerializeField, Min(0.1f)] private float recoverySeconds = 4f;
    [SerializeField, Min(0f)] private float damage = 20f;
    [SerializeField, Min(0.1f)] private float activationRange = 12f;

    [SerializeField] private Material rimMaterial;
    [SerializeField] private Sprite ritualSeal;
    [SerializeField] private AngelHaloSweep haloSweep = new AngelHaloSweep();
    private bool sweepNext;
    private bool sweeping;
    private bool sealsBroken;
    private SpriteRenderer inscription;
    private MaterialPropertyBlock rimProperties;
    private static readonly int Charge = Shader.PropertyToID("_Charge");
    private Transform fingertip;
    private LineRenderer litFinger;
    private Light fingerLight;

    private const float FlashSeconds = 0.25f;
    private CombatHealth health;
    private Transform wrist;
    private Transform finger;
    private Transform knuckle;
    private LineRenderer boundary;
    private LineRenderer pulse;
    private Vector3 center;
    private float nextCast;
    private float castStart;
    private bool casting;
    private bool struck;

    public void SetTarget(Transform target)
    {
        player = target;
        health = target.GetComponent<CombatHealth>();
        haloSweep.SetTarget(target);
    }

    private void Start()
    {
        health = player.GetComponent<CombatHealth>();
        rimProperties = new MaterialPropertyBlock();
        // The imported FBX can retain or strip the hand namespace.
        foreach (var bone in GetComponentsInChildren<Transform>(true))
        {
            if (!bone.name.Contains("arm_wrist")) continue;
            foreach (var joint in bone.GetComponentsInChildren<Transform>(true))
            {
                if (!joint.name.Contains("index_1") || joint.childCount == 0) continue;
                wrist = bone;
                knuckle = joint;
                finger = joint.GetChild(0);
                break;
            }
            if (finger != null) break;
        }

        boundary = CreateRing("Seal boundary");
        boundary.sharedMaterial = rimMaterial;
        inscription = new GameObject("Ritual inscription").AddComponent<SpriteRenderer>();
        inscription.transform.SetParent(boundary.transform, false);
        inscription.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        inscription.transform.localPosition = Vector3.down * 0.008f;
        inscription.sprite = ritualSeal;
        inscription.enabled = false;
        if (finger != null) CreateFingerHighlight();
        pulse = CreateRing("Seal pulse");
        haloSweep.Initialize(transform, player, sealMaterial);
        nextCast = Time.time + recoverySeconds;
    }

    private LineRenderer CreateRing(string ringName)
    {
        var ring = new GameObject(ringName).AddComponent<LineRenderer>();
        ring.transform.SetParent(transform, false);
        ring.sharedMaterial = sealMaterial;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 80;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = 2f * Mathf.PI * i / ring.positionCount;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
        }
        ring.enabled = false;
        return ring;
    }

    private void CreateFingerHighlight()
    {
        fingertip = finger;
        while (fingertip.childCount > 0) fingertip = fingertip.GetChild(0);

        var glow = new GameObject("Pointing finger glow");
        glow.transform.SetParent(transform, false);
        litFinger = glow.AddComponent<LineRenderer>();
        litFinger.sharedMaterial = sealMaterial;
        litFinger.positionCount = 3;
        litFinger.numCapVertices = 8;
        litFinger.numCornerVertices = 4;
        litFinger.widthMultiplier = 0.085f;
        var tint = new MaterialPropertyBlock();
        tint.SetColor("_BaseColor", new Color(0.12f, 2.6f, 3.6f));
        litFinger.SetPropertyBlock(tint);
        litFinger.enabled = false;

        fingerLight = glow.AddComponent<Light>();
        fingerLight.type = LightType.Point;
        fingerLight.color = new Color(0.15f, 0.86f, 1f);
        fingerLight.range = 1.3f;
        fingerLight.intensity = 4f;
        fingerLight.enabled = false;
    }

    private void Update() => haloSweep.RestorePose();

    private void LateUpdate()
    {
        if (Time.timeScale <= 0f) return;
        if (GameObjectives.Capture().AltarsDestroyed == 6 && gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
        {
            if (!sealsBroken)
            {
                sealsBroken = true;
                HideSeal();
                if (sweeping) { haloSweep.Stop(); sweeping = false; }
                GameAudio.Play(SoundCue.AngelDispelled, transform.position, 0.45f, 5f);
            }
            if (player != null && health != null && !health.IsDead &&
                Vector3.Distance(player.position, transform.position) < 3.5f)
                GameCheckpoint.FinishRitual();
            return;
        }
        if (health == null || health.IsDead)
        {
            enabled = false;
            return;
        }

        if (sweeping)
        {
            if (haloSweep.Tick(Time.time - castStart))
            {
                sweeping = false;
                nextCast = Time.time + recoverySeconds;
            }
            return;
        }
        if (!casting)
        {
            if (Time.time < nextCast || Vector3.Distance(transform.position, player.position) > activationRange) return;
            castStart = Time.time;
            if (sweepNext)
            {
                sweepNext = false;
                sweeping = true;
                haloSweep.Begin();
                return;
            }
            sweepNext = true;
            center = player.position;
            GameAudio.Play(SoundCue.AngelCharge, center, 0.64f, 5f);
            casting = true;
            struck = false;
            inscription.enabled = true;
            DrawRing(boundary, radius, 0.065f);
        }

        float elapsed = Time.time - castStart;
        float charge = Mathf.Clamp01(elapsed / warningSeconds);
        if (wrist != null)
        {
            var turn = Quaternion.FromToRotation(finger.position - knuckle.position, center - knuckle.position);
            wrist.rotation = Quaternion.Slerp(Quaternion.identity, turn, Mathf.Clamp01(elapsed / 0.22f)) * wrist.rotation;
            litFinger.enabled = true;
            litFinger.SetPosition(0, knuckle.position);
            litFinger.SetPosition(1, finger.position);
            litFinger.SetPosition(2, fingertip.position);
            fingerLight.transform.position = fingertip.position;
            fingerLight.enabled = true;
        }
        rimProperties.SetFloat(Charge, charge);
        boundary.SetPropertyBlock(rimProperties);
        if (elapsed < warningSeconds) return;

        if (!struck)
        {
            struck = true;
            GameAudio.Play(SoundCue.AngelStrike, center, 0.91f, 6f);
            Vector3 offset = player.position - center;
            if (Mathf.Abs(offset.y) < 1f && new Vector2(offset.x, offset.z).sqrMagnitude <= radius * radius)
                health.TakeDamage(damage, player.position, Vector3.up, gameObject);
            DrawRing(boundary, radius, 0.25f);
        }
        // A brief outward flash distinguishes the impact from the warning.
        DrawRing(pulse, Mathf.Lerp(0.08f, radius, (elapsed - warningSeconds) / FlashSeconds), 0.2f);
        if (elapsed < warningSeconds + FlashSeconds) return;
        HideSeal();
        nextCast = Time.time + recoverySeconds;
    }

    private void DrawRing(LineRenderer ring, float size, float width)
    {
        ring.transform.SetPositionAndRotation(center + Vector3.up * 0.045f, Quaternion.identity);
        ring.transform.localScale = Vector3.one * size;
        ring.widthMultiplier = width;
        ring.enabled = true;
    }

    private void HideSeal()
    {
        casting = false;
        if (boundary != null) boundary.enabled = false;
        if (inscription != null) inscription.enabled = false;
        if (pulse != null) pulse.enabled = false;
        if (litFinger != null) litFinger.enabled = false;
        if (fingerLight != null) fingerLight.enabled = false;
    }

    private void OnDisable()
    {
        HideSeal();
        haloSweep.Stop();
        sweeping = false;
    }
}
