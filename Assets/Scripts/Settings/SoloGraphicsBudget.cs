using UnityEngine;

// Cost limits for the existing Low / Medium / High settings. No new preference keys.
public static class SoloGraphicsBudget
{
    static readonly int[] LocalLights = { 6, 12, 24 };
    static readonly float[] GrassDistance = { 65, 100, 150 };
    static readonly int[] GrassDensity = { 2, 3, 4 };
    static readonly double[] NavigationSlices = { .0005, .001, .002 };
    public static int Tier(SoloSettingsData settings) => Mathf.Clamp(settings.graphicsQuality, 0, 2);
    public static int LightLimit(SoloSettingsData settings) => LocalLights[Mathf.Min(Tier(settings), settings.lightingQuality)];
    public static float FoliageDistance(SoloSettingsData settings) => GrassDistance[Tier(settings)];
    public static bool KeepFoliage(SoloSettingsData settings, int stableIndex) => (stableIndex & 3) < GrassDensity[Tier(settings)];
    public static bool PostProcessing(SoloSettingsData settings) => Tier(settings) > 0;
    public static double NavigationSliceSeconds => NavigationSlices[Tier(SoloGameSettings.Current)];
    public static int Shadows(SoloSettingsData settings) => Tier(settings) == 0 ? 0 : Mathf.Min(settings.shadowQuality, Tier(settings) == 1 ? 2 : 3);
}
