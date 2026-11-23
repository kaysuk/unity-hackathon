using UnityEngine;
using UnityEngine.UI;

// Project one material slot of the collectible mesh onto the inventory tile.
public sealed class TempleRuneIcon : RawImage
{
    private static readonly Quaternion ViewAngle = Quaternion.Euler(9f, -12f, 0f);

    private Vector3[] vertices;
    private Vector2[] uvs;
    private int[] triangles;
    private Bounds bounds;

    public void SetStone(Mesh mesh, int materialSlot, Material surface)
    {
        vertices = mesh.vertices;
        uvs = mesh.uv;
        triangles = mesh.GetTriangles(materialSlot);
        bounds = mesh.bounds;
        texture = surface.mainTexture;
        uvRect = new Rect(surface.mainTextureOffset, surface.mainTextureScale);
        color = surface.color;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper output)
    {
        output.Clear();
        if (vertices == null)
            return;

        Rect rect = GetPixelAdjustedRect();
        float scale = Mathf.Min(rect.width / bounds.size.x, rect.height / bounds.size.y);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 point = ViewAngle * (vertices[i] - bounds.center);
            Vector2 position = rect.center + new Vector2(point.x, point.y) * scale;
            Vector2 uv = uvRect.position + Vector2.Scale(uvs[i], uvRect.size);
            output.AddVert(position, color, uv);
        }

        // The world material culls backfaces. The default UI shader draws both sides.
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if ((ViewAngle * normal).z < 0f)
                output.AddTriangle(a, b, c);
        }
    }
}
