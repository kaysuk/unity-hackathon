using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// These runners are decorative meshes above a solid floor. Their footprint affects sound only.
[RequireComponent(typeof(MeshRenderer))]
[DisallowMultipleComponent]
public sealed class FootstepCarpet : MonoBehaviour
{
    private static readonly List<FootstepCarpet> runners = new();
    private MeshRenderer mesh;

    private void OnEnable()
    {
        mesh = GetComponent<MeshRenderer>();
        runners.Add(this);
    }

    private void OnDisable() => runners.Remove(this);

    internal static bool Covers(Scene scene, Vector3 contact)
    {
        foreach (var runner in runners)
        {
            if (runner.gameObject.scene != scene) continue;
            Bounds bounds = runner.mesh.bounds;
            if (Mathf.Abs(contact.y - bounds.center.y) < 0.18f &&
                contact.x >= bounds.min.x && contact.x <= bounds.max.x &&
                contact.z >= bounds.min.z && contact.z <= bounds.max.z)
                return true;
        }
        return false;
    }
}
