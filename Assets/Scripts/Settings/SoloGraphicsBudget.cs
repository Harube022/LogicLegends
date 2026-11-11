using UnityEngine;

// Cost limits for the existing Low / Medium / High settings. No new preference keys.
public static class SoloGraphicsBudget
{
    // Match Forward's per-object budget. Enabling more lights can cause a large
    // floor renderer to omit a nearby lamp even though that lamp is enabled.
    static readonly int[] LocalLights = { 4, 6, 8 };
    static readonly float[] LocalLightDistance = { 30, 45, 65 };
    static readonly float[] GrassDistance = { 65, 100, 150 };
    static readonly int[] GrassDensity = { 2, 3, 4 };
    static readonly double[] NavigationSlices = { .0005, .001, .002 };
    public static int Tier(SoloSettingsData settings) => Mathf.Clamp(settings.graphicsQuality, 0, 2);
    // Lighting is a separate player choice, including Low graphics with High lighting.
    public static int LightingTier(SoloSettingsData settings) => Mathf.Clamp(settings.lightingQuality, 0, 2);
    public static int LightLimit(SoloSettingsData settings) => LocalLights[LightingTier(settings)];
    public static float LightDistance(SoloSettingsData settings) => LocalLightDistance[LightingTier(settings)];
    public static float FoliageDistance(SoloSettingsData settings) => GrassDistance[Tier(settings)];
    public static bool KeepFoliage(SoloSettingsData settings, int stableIndex) => (stableIndex & 3) < GrassDensity[Tier(settings)];
    public static bool PostProcessing(SoloSettingsData settings) => Tier(settings) > 0;
    public static double NavigationSliceSeconds => NavigationSlices[Tier(SoloGameSettings.Current)];
    public static int Shadows(SoloSettingsData settings) => Mathf.Clamp(settings.shadowQuality, 0, 3);
}
