using System;
using UnityEngine;

// Evaluated over the Angel's animated pose by the attack cycle in LateUpdate.
[Serializable]
public sealed class AngelHaloSweep
{
    [SerializeField, Min(0.5f)] private float windup = 2f;
    [SerializeField, Min(1f)] private float speed = 5f;
    [SerializeField, Min(1f)] private float reach = 16f;
    [SerializeField, Min(0f)] private float damage = 20f;
    [SerializeField, Min(0.1f)] private float chestHeight = 1.43f;

    [SerializeField] private Material gripMaterial;
    private readonly Renderer[] handMeshes = new Renderer[3];
    private readonly Material[] handMaterials = new Material[3];
    private MaterialPropertyBlock gripProperties;
    private bool handsLit;
    private Vector3 raisedCenter;
    private static readonly int Palm = Shader.PropertyToID("_Palm");
    private static readonly int Signal = Shader.PropertyToID("_Signal");

    private const float WaveThickness = 0.14f;
    private readonly Transform[] arms = new Transform[3];
    private readonly Transform[] palms = new Transform[3];
    private readonly Vector3[] savedArmPositions = new Vector3[3];
    private readonly Quaternion[] savedArmRotations = new Quaternion[3];
    private Vector3 savedHaloPosition;
    private Quaternion savedHaloRotation;
    private bool posed;
    private Transform halo;
    private Bounds meshBounds;
    private Transform boss;
    private CharacterController capsule;
    private CombatHealth victim;
    private LineRenderer wave;
    private Vector3 origin;
    private float releaseRadius;
    private float previousGap;
    private bool released;
    private bool hit;

    public void Initialize(Transform angel, Transform target, Material material)
    {
        boss = angel;
        capsule = target.GetComponent<CharacterController>();
        victim = target.GetComponent<CombatHealth>();
        gripProperties = new MaterialPropertyBlock();
        var bones = angel.GetComponentsInChildren<Transform>(true);
        foreach (var bone in bones)
        {
            if (bone.name == "outter_torus") halo = bone;
            for (int i = 0; i < arms.Length; i++)
            {
                if (bone.name == $"hand{i + 1}:arm_mesh")
                {
                    handMeshes[i] = bone.GetComponent<Renderer>();
                    handMaterials[i] = handMeshes[i].sharedMaterial;
                }
                if (bone.name == $"hand{i + 1}:arm_root") arms[i] = bone;
                if (bone.name == $"hand{i + 1}:middle_1") palms[i] = bone;
            }
        }
        meshBounds = halo.GetComponent<MeshFilter>().sharedMesh.bounds;
        wave = new GameObject("Halo wave").AddComponent<LineRenderer>();
        wave.transform.SetParent(angel, false);
        wave.sharedMaterial = material;
        wave.useWorldSpace = true;
        wave.loop = true;
        wave.positionCount = 120;
        wave.widthMultiplier = WaveThickness;
        wave.enabled = false;
    }

    public void Begin()
    {
        var floor = boss.position;
        if (Physics.Raycast(floor + Vector3.up * 0.2f, Vector3.down, out var ground, 6f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) floor = ground.point;
        origin = floor + Vector3.up * chestHeight;
        // Present the grip beside Ivan's silhouette, then lower to the original release point.
        Vector3 side = Vector3.Cross(Vector3.up, boss.position - capsule.transform.position).normalized;
        raisedCenter = origin + side * 1.65f + Vector3.up * 1.25f;
        released = false;
        hit = false;
    }

    // Undo last frame's procedural offsets before the Animator supplies the next pose.
    public void RestorePose()
    {
        if (!posed) return;
        halo.SetLocalPositionAndRotation(savedHaloPosition, savedHaloRotation);
        for (int i = 0; i < arms.Length; i++)
            arms[i].SetLocalPositionAndRotation(savedArmPositions[i], savedArmRotations[i]);
        posed = false;
    }

    public bool Tick(float elapsed)
    {
        float recover = Mathf.SmoothStep(1f, 0f, (elapsed - windup) / 0.5f);
        if (recover > 0f) PoseHalo(elapsed, recover);
        if (elapsed < windup) return false;

        if (!released)
        {
            SetHandsLit(false);
            released = true;
            previousGap = DistanceToWaveOrigin() - releaseRadius;
        }
        float radius = Mathf.Min(reach, releaseRadius + (elapsed - windup) * speed);
        wave.enabled = true;
        for (int i = 0; i < wave.positionCount; i++)
        {
            float a = i * (2f * Mathf.PI / wave.positionCount);
            wave.SetPosition(i, origin + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
        }

        var body = capsule.bounds;
        float gap = DistanceToWaveOrigin() - radius;
        float clearance = body.extents.x + WaveThickness * 0.5f;
        if (!hit && Mathf.Min(previousGap, gap) <= clearance && Mathf.Max(previousGap, gap) >= -clearance &&
            body.max.y >= origin.y - WaveThickness * 0.5f && body.min.y <= origin.y + WaveThickness * 0.5f)
        {
            hit = true;
            victim.TakeDamage(damage, body.center, (body.center - origin).normalized, boss.gameObject);
        }
        previousGap = gap;
        if (radius < reach) return false;
        Stop();
        return true;
    }

    private float DistanceToWaveOrigin()
    {
        Vector3 offset = capsule.bounds.center - origin;
        return new Vector2(offset.x, offset.z).magnitude;
    }

    private void PoseHalo(float elapsed, float recover)
    {
        savedHaloPosition = halo.localPosition;
        savedHaloRotation = halo.localRotation;
        for (int i = 0; i < arms.Length; i++)
        {
            savedArmPositions[i] = arms[i].localPosition;
            savedArmRotations[i] = arms[i].localRotation;
        }
        posed = true;

        Vector3 center = halo.TransformPoint(meshBounds.center);
        // The torus may inherit a mirrored/non-uniform transform from the FBX.
        Vector3 axisX = halo.TransformVector(Vector3.right * meshBounds.extents.x);
        Vector3 axisZ = halo.TransformVector(Vector3.forward * meshBounds.extents.z);
        Vector3 normal = Vector3.Cross(axisZ, axisX).normalized;
        if (normal.y < 0f) normal = -normal;
        float turn = Mathf.SmoothStep(0f, 1f, (elapsed / windup - 0.24f) / 0.43f) * recover;
        Quaternion horizontal = Quaternion.FromToRotation(normal, Vector3.up);
        Quaternion tilt = Quaternion.Slerp(Quaternion.identity, horizontal, turn);
        float lower = Mathf.SmoothStep(0f, 1f, (elapsed / windup - 0.58f) / 0.36f) * recover;
        float grip = Mathf.SmoothStep(0f, 1f, elapsed / (windup * 0.24f)) * recover;
        // Face one grip toward Ivan so the other two flank it in the shoulder view.
        Vector3 towardIvan = Vector3.ProjectOnPlane(capsule.transform.position - raisedCenter, Vector3.up);
        float yaw = Vector3.SignedAngle(horizontal * axisX, towardIvan, Vector3.up);
        Quaternion gripRotation = tilt * Quaternion.AngleAxis(yaw * grip, normal);
        halo.rotation = gripRotation * halo.rotation;
        Vector3 presentation = Vector3.Lerp(center, raisedCenter, grip);
        halo.position += Vector3.Lerp(presentation, origin, lower) - halo.TransformPoint(meshBounds.center);
        SetHandsLit(grip > 0.36f && elapsed < windup);
        gripProperties.SetFloat(Signal, Mathf.SmoothStep(0f, 1f, (turn - 0.90f) / 0.10f));
        for (int i = 0; i < arms.Length; i++)
        {
            float a = i * (2f * Mathf.PI / arms.Length);
            Vector3 localGrip = meshBounds.center + new Vector3(Mathf.Cos(a) * meshBounds.extents.x,
                0f, Mathf.Sin(a) * meshBounds.extents.z);
            arms[i].rotation = gripRotation * arms[i].rotation;
            arms[i].position += (halo.TransformPoint(localGrip) - palms[i].position) * grip;
            if (handsLit)
            {
                gripProperties.SetVector(Palm, palms[i].position);
                handMeshes[i].SetPropertyBlock(gripProperties);
            }
        }
        if (!released) releaseRadius = (axisX.magnitude + axisZ.magnitude) * 0.5f;
    }

    private void SetHandsLit(bool lit)
    {
        if (handsLit == lit) return;
        handsLit = lit;
        for (int i = 0; i < handMeshes.Length; i++)
        {
            handMeshes[i].sharedMaterial = lit ? gripMaterial : handMaterials[i];
            if (!lit) handMeshes[i].SetPropertyBlock(null);
        }
    }

    public void Stop()
    {
        RestorePose();
        SetHandsLit(false);
        if (wave != null) wave.enabled = false;
    }
}
