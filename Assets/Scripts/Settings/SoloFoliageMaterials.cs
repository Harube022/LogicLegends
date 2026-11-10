using UnityEngine;

// Replace only the legacy card-foliage shader in solo trials. Source pack assets stay intact.
public static class SoloFoliageMaterials
{
    public static bool IsLegacy(Material source) => source != null && source.shader != null &&
        source.shader.name == "Polytope Studio/PT_Vegetation_Plants_Shader";

    public static Material Create(Material source)
    {
        var shader = Resources.Load<Shader>("Settings/SoloFoliage");
        if (shader == null) return null;
        var copy = new Material(shader) { name = source.name + " (Solo Cutout)", hideFlags = HideFlags.DontSave, enableInstancing = true };
        if (source.HasProperty("_BaseTexture")) copy.SetTexture("_BaseMap", source.GetTexture("_BaseTexture"));
        copy.SetFloat("_Cutoff", 1f - Value(source, "_LeavesThickness", .5f));
        copy.SetFloat("_Wind", source.IsKeywordEnabled("_CUSTOMWIND_ON") ? Value(source, "_WindStrength", .3f) : 0f);
        copy.SetFloat("_WindSpeed", Value(source, "_WindMovement", 1f));
        copy.SetFloat("_TintGradient", Value(source, "_CUSTOMCOLORSTINTING", 0f));
        copy.SetFloat("_Gradient", Value(source, "_Gradient", 1.4f));
        if (source.HasProperty("_TopColor")) copy.SetColor("_TopColor", source.GetColor("_TopColor"));
        if (source.HasProperty("_GroundColor")) copy.SetColor("_GroundColor", source.GetColor("_GroundColor"));
        return copy;
    }
    static float Value(Material source, string name, float fallback) => source.HasProperty(name) ? source.GetFloat(name) : fallback;
}
