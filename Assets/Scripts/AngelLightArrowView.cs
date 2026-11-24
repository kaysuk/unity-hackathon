using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

internal sealed class AngelLightArrowView
{
    private const float Length = 2f;
    private readonly GameObject root;
    private readonly Mesh lance;
    private readonly MeshRenderer[] spears;
    private readonly LineRenderer[] wakes;
    private readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly Color Bronze = new Color(.82f, .29f, .055f);
    private static readonly Color Amber = new Color(1.85f, .76f, .16f);
    private static readonly Color Ivory = new Color(2.65f, 1.82f, .86f);

    internal AngelLightArrowView(Transform boss, Material material, int count)
    {
        root = new GameObject("Light arrow volley");
        SceneManager.MoveGameObjectToScene(root, boss.gameObject.scene);
        lance = BuildLance();
        var materials = new[] { material, material, material };
        spears = new MeshRenderer[count];
        wakes = new LineRenderer[count];
        for (int i = 0; i < count; i++)
        {
            var spear = new GameObject($"Light arrow {i + 1}", typeof(MeshFilter), typeof(MeshRenderer));
            spear.transform.SetParent(root.transform, false);
            spear.GetComponent<MeshFilter>().sharedMesh = lance;
            var renderer = spears[i] = spear.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var wake = wakes[i] = new GameObject("Light streak").AddComponent<LineRenderer>();
            wake.transform.SetParent(spear.transform, false);
            wake.sharedMaterial = material;
            wake.shadowCastingMode = ShadowCastingMode.Off;
            wake.receiveShadows = false;
            wake.useWorldSpace = false;
            wake.positionCount = 2;
            wake.SetPosition(0, Vector3.back * Length);
            wake.SetPosition(1, Vector3.back * (Length + 1.4f));
            wake.startWidth = .045f;
            wake.endWidth = 0f;
            tint.SetColor(BaseColor, Amber);
            wake.SetPropertyBlock(tint);
            spear.SetActive(false);
        }
    }

    internal void Set(int index, Vector3 tip, Vector3 direction, float charge, bool flying)
    {
        var spear = spears[index];
        var anchor = spear.transform;
        if (direction.sqrMagnitude < .000001f) direction = anchor.forward;
        var up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > .995f ? Vector3.forward : Vector3.up;
        anchor.SetPositionAndRotation(tip, Quaternion.LookRotation(direction, up));
        SetFacetColor(spear, 0, Color.Lerp(Bronze, Amber, charge));
        SetFacetColor(spear, 1, Color.Lerp(Amber, Ivory, charge));
        SetFacetColor(spear, 2, Ivory * (1f + .55f * charge));
        wakes[index].enabled = flying;
        if (!spear.gameObject.activeSelf) spear.gameObject.SetActive(true);
    }

    private void SetFacetColor(MeshRenderer spear, int submesh, Color color)
    {
        tint.SetColor(BaseColor, color);
        spear.SetPropertyBlock(tint, submesh);
    }

    internal void Hide(int index)
    {
        var spear = spears[index];
        if (spear != null) spear.gameObject.SetActive(false);
    }

    internal void Dispose()
    {
        Object.Destroy(root);
        Object.Destroy(lance);
    }

    private static Mesh BuildLance()
    {
        // Axial position and radius. The first vertex is the collision tip.
        var profile = new[]
        {
            new Vector2(0f, 0f), new Vector2(-.16f, .058f),
            new Vector2(-.36f, .125f), new Vector2(-.56f, .039f),
            new Vector2(-.67f, .049f), new Vector2(-1.83f, .027f), new Vector2(-Length, 0f)
        };
        const int sides = 6;
        var vertices = new Vector3[profile.Length * sides];
        var facets = new[] { new List<int>(), new List<int>(), new List<int>() };
        for (int ring = 0; ring < profile.Length; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                float angle = (side + .5f) * (2f * Mathf.PI / sides);
                float radius = profile[ring].y;
                int index = ring * sides + side;
                vertices[index] = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), profile[ring].x);
                if (ring == profile.Length - 1) continue;

                // The ivory point reads head-on. Warm and shadowed faces carry the ridge in profile.
                int shade = (side == 0 || side == 1 || side == 5) ? 1 : 0;
                if (ring == 0 || (ring == 1 && side == 1)) shade = 2;
                var triangles = facets[shade];
                int next = ring * sides + (side + 1) % sides;
                if (profile[ring].y > 0f)
                {
                    triangles.Add(index);
                    triangles.Add(index + sides);
                    triangles.Add(next);
                }
                if (profile[ring + 1].y > 0f)
                {
                    triangles.Add(next);
                    triangles.Add(index + sides);
                    triangles.Add(next + sides);
                }
            }
        }
        var mesh = new Mesh { name = "Faceted light lance", vertices = vertices, subMeshCount = facets.Length };
        for (int i = 0; i < facets.Length; i++) mesh.SetTriangles(facets[i], i);
        mesh.RecalculateNormals();
        return mesh;
    }
}
