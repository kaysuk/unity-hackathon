using UnityEngine;
using UnityEngine.Rendering;

internal sealed class AngelEyeMark
{
    internal const float FlashSeconds = .25f;
    private static readonly int SealLayer = Shader.PropertyToID("_SealLayer");
    private static readonly int Charge = Shader.PropertyToID("_Charge");
    private readonly LineRenderer ring;
    private readonly MeshRenderer eye;
    private readonly MaterialPropertyBlock ringProperties = new MaterialPropertyBlock();
    private readonly MaterialPropertyBlock eyeProperties = new MaterialPropertyBlock();
    private Quaternion orientation;
    private bool struck;

    internal Vector3 Center { get; private set; }
    internal Vector3 SurfacePosition => Center + Vector3.up * .045f;

    internal AngelEyeMark(Transform boss, Material material, int index)
    {
        ring = new GameObject("Seal boundary").AddComponent<LineRenderer>();
        ring.transform.SetParent(boss, false);
        ring.sharedMaterial = material;
        ring.widthMultiplier = .035f;
        ring.shadowCastingMode = ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 80;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = 2f * Mathf.PI * i / ring.positionCount;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
        }

        var glyph = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glyph.name = "Eye of Providence";
        var collider = glyph.GetComponent<Collider>();
        collider.enabled = false;
        Object.Destroy(collider);
        glyph.transform.SetParent(ring.transform, false);
        glyph.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        glyph.transform.localPosition = Vector3.down * .005f;
        glyph.transform.localScale = new Vector3(2f, 2f, 1f);
        eye = glyph.GetComponent<MeshRenderer>();
        eye.sharedMaterial = material;
        eye.shadowCastingMode = ShadowCastingMode.Off;
        eye.receiveShadows = false;
        eyeProperties.SetFloat(SealLayer, 1f);

        // When Ivan stands still, the eye closest to striking stays legible on top.
        eye.sortingOrder = ring.sortingOrder = -index;
        Hide();
    }

    internal void Place(Vector3 center, float radius)
    {
        Center = center;
        var camera = Camera.main;
        Vector3 forward = camera ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up) : Vector3.forward;
        orientation = forward.sqrMagnitude > .0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
        ring.transform.localScale = Vector3.one * radius;
        ring.transform.SetPositionAndRotation(SurfacePosition, orientation);
        struck = false;
        ring.enabled = true;
        eye.enabled = true;
    }

    // The caller resolves a hit only on the frame this particular eye opens.
    internal bool Tick(float age, float warningSeconds)
    {
        if (!ring.enabled) return false;
        ring.transform.SetPositionAndRotation(SurfacePosition, orientation);
        float charge = Mathf.Clamp01(age / warningSeconds);
        ringProperties.SetFloat(Charge, charge);
        ring.SetPropertyBlock(ringProperties);
        eyeProperties.SetFloat(Charge, charge);
        eye.SetPropertyBlock(eyeProperties);

        bool strikeNow = !struck && age >= warningSeconds;
        if (strikeNow) struck = true;
        if (age >= warningSeconds + FlashSeconds) Hide();
        return strikeNow;
    }

    internal void Hide()
    {
        ring.enabled = false;
        eye.enabled = false;
    }
}
