using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Draw the item's visible surfaces for this camera and frame. The item's own
// materials, textures and puzzle-controlled property blocks stay on its renderer.
internal sealed class InvestigationHighlight : IDisposable
{
    private readonly Dictionary<Interactable, List<(MeshRenderer renderer, MeshFilter filter)>> surfaces = new();
    private readonly HashSet<MeshRenderer> drawn = new();
    private readonly List<Material> materialSlots = new();
    private readonly Plane[] planes = new Plane[6];
    private Material glow;
    private Scene scene;

    internal void Show(Camera camera, IReadOnlyList<Interactable> targets)
    {
        if (camera == null || targets.Count == 0) return;
        if (scene != camera.gameObject.scene)
        {
            Clear();
            scene = camera.gameObject.scene;
        }

        if (glow == null)
        {
            var shader = Resources.Load<Shader>("InvestigationHighlight");
            if (shader == null || !shader.isSupported) return;
            glow = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        GeometryUtility.CalculateFrustumPlanes(camera, planes);
        drawn.Clear();
        var settings = new RenderParams(glow)
        {
            camera = camera,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            lightProbeUsage = LightProbeUsage.Off,
            reflectionProbeUsage = ReflectionProbeUsage.Off
        };

        for (int i = 0; i < targets.Count; i++)
        {
            Interactable target = targets[i];
            if (target == null || !target.isActiveAndEnabled) continue;
            if (!surfaces.TryGetValue(target, out var parts))
            {
                parts = FindSurfaces(target);
                surfaces.Add(target, parts);
            }

            foreach (var (renderer, filter) in parts)
            {
                if (renderer == null || filter == null || !renderer.enabled || renderer.forceRenderingOff ||
                    !renderer.gameObject.activeInHierarchy || !drawn.Add(renderer)) continue;
                var mesh = filter.sharedMesh;
                if (mesh == null || !GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) continue;
                settings.layer = renderer.gameObject.layer;
                settings.worldBounds = renderer.bounds;
                int first = 0, end = mesh.subMeshCount;
                if (renderer.isPartOfStaticBatch)
                {
                    // A combined mesh can also contain the walls and other props.
                    renderer.GetSharedMaterials(materialSlots);
                    first = renderer.subMeshStartIndex;
                    end = Mathf.Min(end, first + materialSlots.Count);
                }
                for (int sub = first; sub < end; sub++)
                    Graphics.RenderMesh(settings, mesh, sub, renderer.localToWorldMatrix);
            }
        }
    }

    private static List<(MeshRenderer, MeshFilter)> FindSurfaces(Interactable target)
    {
        var parts = new List<(MeshRenderer, MeshFilter)>();
        Transform root = target.HighlightRoot;
        if (root == null) return parts;

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.TryGetComponent(out MeshRenderer renderer)) continue;
            // A nested mechanism has its own interaction state (e.g. a seal button).
            if (root == target.transform && filter.GetComponentInParent<Interactable>(true) != target) continue;
            parts.Add((renderer, filter));
        }
        return parts;
    }

    internal void Clear()
    {
        surfaces.Clear();
        drawn.Clear();
        materialSlots.Clear();
    }

    public void Dispose()
    {
        Clear();
        if (glow != null) UnityEngine.Object.Destroy(glow);
        glow = null;
    }
}
