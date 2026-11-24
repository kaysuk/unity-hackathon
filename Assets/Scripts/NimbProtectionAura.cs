using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
internal sealed class NimbProtectionAura : MonoBehaviour
{
    private const string MaterialResource = "NimbProtectionAura";
    private Mesh shell;
    private MeshRenderer view;

    internal static NimbProtectionAura Create(Transform chest)
    {
        if (chest == null) return null;
        Material material = Resources.Load<Material>(MaterialResource);
        if (material == null)
        {
            Debug.LogError("Nimb's protection aura material is missing.", chest);
            return null;
        }

        var auraObject = new GameObject("Nimb protective aura");
        var aura = auraObject.AddComponent<NimbProtectionAura>();
        aura.transform.SetParent(chest, false);
        aura.FitTorso(chest);
        aura.shell = CreateShell();
        auraObject.AddComponent<MeshFilter>().sharedMesh = aura.shell;
        aura.view = auraObject.AddComponent<MeshRenderer>();
        aura.view.sharedMaterial = material;
        aura.view.shadowCastingMode = ShadowCastingMode.Off;
        aura.view.receiveShadows = false;
        aura.view.lightProbeUsage = LightProbeUsage.Off;
        aura.view.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return aura;
    }

    internal void SetProtected(bool active) => view.enabled = active;

    private void FitTorso(Transform chest)
    {
        Transform boss = chest.root;
        Transform pelvis = null;
        Transform crown = null;
        Transform leftShoulder = null;
        Transform rightShoulder = null;
        foreach (Transform bone in boss.GetComponentsInChildren<Transform>(true))
        {
            switch (bone.name)
            {
                case "skeleton:root_pelvis":
                case "root_pelvis": pelvis = bone; break;
                case "skeleton:head_top":
                case "head_top": crown = bone; break;
                case "skeleton:shoulder_l":
                case "shoulder_l": leftShoulder = bone; break;
                case "skeleton:shoulder_r":
                case "shoulder_r": rightShoulder = bone; break;
            }
        }

        Vector3 lower = pelvis != null ? pelvis.position : chest.position - boss.up * .6f;
        Vector3 upper = crown != null ? crown.position : chest.position + boss.up * .8f;
        Vector3 spine = upper - lower;
        Vector3 up = spine.sqrMagnitude > .01f ? spine.normalized : boss.up;
        Vector3 forward = Vector3.ProjectOnPlane(boss.forward, up);
        if (forward.sqrMagnitude < .01f)
            forward = Vector3.Cross(boss.right, up);

        float shoulders = leftShoulder != null && rightShoulder != null
            ? Vector3.Distance(leftShoulder.position, rightShoulder.position) : .85f;
        float width = Mathf.Max(.76f, shoulders * .53f + .35f);
        // The mesh extends slightly beyond the bright edge to give the glow room to fall off.
        var radii = 1.08f * new Vector3(width, Mathf.Max(.99f, spine.magnitude * .62f + .18f), width * .84f);
        transform.SetPositionAndRotation((upper + lower) * .5f, Quaternion.LookRotation(forward, up));

        // The imported rig's chest can carry a scale from the FBX. Fit each axis in world metres.
        Quaternion basis = transform.localRotation;
        var axisScale = new Vector3(
            chest.TransformVector(basis * Vector3.right).magnitude,
            chest.TransformVector(basis * Vector3.up).magnitude,
            chest.TransformVector(basis * Vector3.forward).magnitude);
        transform.localScale = new Vector3(
            radii.x / Mathf.Max(.0001f, axisScale.x),
            radii.y / Mathf.Max(.0001f, axisScale.y),
            radii.z / Mathf.Max(.0001f, axisScale.z));
    }

    private static Mesh CreateShell()
    {
        const int slices = 36;
        const int rows = 24;
        const int stride = slices + 1;
        var vertices = new Vector3[(rows + 1) * stride];
        var indices = new int[rows * slices * 6];
        int index = 0;
        for (int row = 0; row <= rows; row++)
        {
            float latitude = Mathf.PI * row / rows;
            float ring = Mathf.Sin(latitude);
            float y = Mathf.Cos(latitude);
            for (int slice = 0; slice <= slices; slice++)
            {
                float longitude = 2f * Mathf.PI * slice / slices;
                int vertex = row * stride + slice;
                vertices[vertex] = new Vector3(ring * Mathf.Cos(longitude), y, ring * Mathf.Sin(longitude));

                if (row == rows || slice == slices) continue;
                indices[index++] = vertex;
                indices[index++] = vertex + 1;
                indices[index++] = vertex + stride;
                indices[index++] = vertex + 1;
                indices[index++] = vertex + stride + 1;
                indices[index++] = vertex + stride;
            }
        }

        var mesh = new Mesh { name = "Nimb aura shell", vertices = vertices, normals = vertices, triangles = indices };
        mesh.UploadMeshData(true);
        return mesh;
    }

    private void OnDestroy()
    {
        if (shell != null) Destroy(shell);
    }
}
