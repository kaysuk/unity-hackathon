using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// World-space effects stay fixed after release even as the Angel recoils.
public sealed class AngelHaloWaveView
{
    private const int Segments = 128;
    private readonly GameObject root;
    private readonly MeshRenderer floor;
    private readonly MeshRenderer wall;
    private readonly Mesh floorMesh;
    private readonly Mesh wallMesh;
    private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

    private static readonly int Origin = Shader.PropertyToID("_WaveOrigin");
    private static readonly int Cover = Shader.PropertyToID("_Pillars");
    private static readonly int Reach = Shader.PropertyToID("_Reach");
    private static readonly int ChargeAmount = Shader.PropertyToID("_Charge");
    private static readonly int Radius = Shader.PropertyToID("_Radius");
    private static readonly int IsWall = Shader.PropertyToID("_IsWall");
    private float wallHeight;

    public AngelHaloWaveView(Transform boss, Material material)
    {
        root = new GameObject("Halo wave");
        SceneManager.MoveGameObjectToScene(root, boss.gameObject.scene);
        floorMesh = new Mesh { name = "Halo wave floor" };
        floorMesh.vertices = new[] { new Vector3(-1f,0f,-1f), new Vector3(-1f,0f,1f),
            new Vector3(1f,0f,1f), new Vector3(1f,0f,-1f) };
        floorMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        wallMesh = CreateOpenCylinder(Segments, "Halo wave curtain");
        floor = CreateRenderer("Light cast by the halo", floorMesh, material);
        wall = CreateRenderer("Golden wave front", wallMesh, material);
        Hide();
    }

    internal void SetChambers(NimbEncounter encounter)
    {
        var chambers = encounter != null ? encounter.Chambers : null;
        properties.SetFloat("_ChamberCount", chambers?.Count ?? 0);
        if (chambers == null || chambers.Count == 0) return;

        var transforms = new Matrix4x4[chambers.Count];
        var limits = new Vector4[chambers.Count];
        for (int i = 0; i < chambers.Count; i++)
        {
            transforms[i] = chambers[i].ChamberWorldToLocal;
            limits[i] = chambers[i].ChamberLimits;
        }
        properties.SetMatrixArray("_ChamberWorldToLocal", transforms);
        properties.SetVectorArray("_ChamberLimits", limits);
        properties.SetFloat("_ChamberSlope", NimbAltar.SectorSlope);
    }

    private MeshRenderer CreateRenderer(string name, Mesh mesh, Material material)
    {
        var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        child.transform.SetParent(root.transform, false);
        child.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    internal static Mesh CreateOpenCylinder(int segments, string name)
    {
        var vertices = new Vector3[(segments + 1) * 2];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float along = (float)i / segments;
            float angle = along * 2f * Mathf.PI;
            int lo = 2 * i;
            vertices[lo] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[lo + 1] = vertices[lo] + Vector3.up;
            uv[lo] = new Vector2(along, 0f);
            uv[lo + 1] = new Vector2(along, 1f);
            if (i == segments) break;
            int index = i * 6;
            triangles[index] = lo;
            triangles[index + 1] = lo + 1;
            triangles[index + 2] = lo + 2;
            triangles[index + 3] = lo + 2;
            triangles[index + 4] = lo + 1;
            triangles[index + 5] = lo + 3;
        }
        return new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles };
    }

    public void Begin(Vector3 origin, float reach, float height, Vector4[] pillars)
    {
        root.transform.position = origin;
        wallHeight = height;
        floor.transform.localPosition = Vector3.up * 0.045f;
        floor.transform.localScale = new Vector3(reach, 1f, reach);
        properties.SetVector(Origin, new Vector4(origin.x, origin.y, origin.z, 0f));
        properties.SetVectorArray(Cover, pillars);
        properties.SetFloat(Reach, reach);
        properties.SetFloat(Radius, -1f);
        floor.enabled = true;
        wall.enabled = false;
        Charge(0f);
    }

    public void Charge(float progress)
    {
        properties.SetFloat(ChargeAmount, Mathf.Clamp01(progress));
        properties.SetFloat(IsWall, 0f);
        floor.SetPropertyBlock(properties);
    }

    public void Release(float radius)
    {
        wall.enabled = true;
        wall.transform.localScale = new Vector3(radius, wallHeight, radius);
        properties.SetFloat(ChargeAmount, 1f);
        properties.SetFloat(Radius, radius);
        properties.SetFloat(IsWall, 0f);
        floor.SetPropertyBlock(properties);
        properties.SetFloat(IsWall, 1f);
        wall.SetPropertyBlock(properties);
    }

    public void Hide()
    {
        if (floor != null) floor.enabled = false;
        if (wall != null) wall.enabled = false;
    }

    public void Dispose()
    {
        Object.Destroy(root);
        Object.Destroy(floorMesh);
        Object.Destroy(wallMesh);
    }
}
