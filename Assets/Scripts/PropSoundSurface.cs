using System;
using UnityEngine;

internal enum PropSoundSurface { Stone, Earth, Wood, Metal }

internal static class PropSounds
{
    private static bool Has(string name, string word) =>
        name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;

    private static PropSoundSurface? Material(string name)
    {
        if (Has(name, "stone") || Has(name, "brick") || Has(name, "marble") || Has(name, "slate") ||
            Has(name, "concrete") || Has(name, "plaster")) return PropSoundSurface.Stone;
        if (Has(name, "wood") || Has(name, "timber") || Has(name, "plank") || Has(name, "oak") ||
            Has(name, "walnut")) return PropSoundSurface.Wood;
        if (Has(name, "iron") || Has(name, "metal") || Has(name, "steel") || Has(name, "brass") ||
            Has(name, "bronze")) return PropSoundSurface.Metal;
        if (Has(name, "grass") || Has(name, "soil") || Has(name, "gravel") || Has(name, "dirt") ||
            Has(name, "earth") || Has(name, "ground") && !Has(name, "grounded")) return PropSoundSurface.Earth;
        return null;
    }

    internal static PropSoundSurface At(Collider collider)
    {
        if (collider.TryGetComponent(out Renderer mesh) && mesh.sharedMaterial != null)
        {
            var surface = Material(mesh.sharedMaterial.name);
            if (surface.HasValue) return surface.Value;
        }
        return Material(collider.name) ?? PropSoundSurface.Stone;
    }

    internal static SoundCue FirstStep(Collider collider, Vector3 contact)
    {
        if (FootstepCarpet.Covers(collider.gameObject.scene, contact)) return SoundCue.CarpetStep1;
        return At(collider) switch
        {
            PropSoundSurface.Wood => SoundCue.WoodStep1,
            PropSoundSurface.Metal => SoundCue.MetalStep1,
            PropSoundSurface.Earth => SoundCue.EarthLeft1,
            _ => SoundCue.StoneLeft1
        };
    }

    internal static SoundCue Impact(Collider collider) => At(collider) switch
    {
        PropSoundSurface.Wood => SoundCue.WoodImpact,
        PropSoundSurface.Metal => SoundCue.MetalImpact,
        PropSoundSurface.Earth => SoundCue.EarthImpact,
        _ => SoundCue.WallHit
    };
}
