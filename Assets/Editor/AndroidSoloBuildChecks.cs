#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Fail a build instead of silently shipping a runtime Forward pipeline whose
// additional-light variants were stripped by a Forward+-only build profile.
public sealed class AndroidSoloBuildChecks : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android) Validate();
    }
    public static string Validate()
    {
        var registered = new List<UniversalRenderPipelineAsset>();
        BuildTarget.Android.TryGetRenderPipelineAssets(registered);
        for (int tier = 0; tier < 3; tier++)
        {
            var asset = Resources.Load<UniversalRenderPipelineAsset>("Settings/SoloLighting" + tier);
            int expectedLights = new[] { 4, 6, 8 }[tier];
            if (asset == null || !registered.Contains(asset) || asset.additionalLightsRenderingMode != LightRenderingMode.PerPixel || asset.maxAdditionalLightsCount != expectedLights)
                throw new BuildFailedException("Register SoloLighting" + tier + " in IncludeAdditionalRPAssets; retain per-pixel additional lighting.");
            if (!asset.supportsMainLightShadows || !asset.supportsSoftShadows)
                throw new BuildFailedException("SoloLighting" + tier + " must retain main-light and soft shadow support for independent Shadow quality.");
        }
        // Cover mixed choices: lowering geometry quality must not extinguish
        // fixtures when the player explicitly keeps lighting on High.
        int[] localLightLimits = { 4, 6, 8 };
        for (int graphics = 0; graphics < 3; graphics++)
            for (int lighting = 0; lighting < 3; lighting++)
            {
                var settings = new SoloSettingsData { graphicsQuality = graphics, lightingQuality = lighting };
                if (SoloGraphicsBudget.LightingTier(settings) != lighting ||
                    SoloGraphicsBudget.LightLimit(settings) != localLightLimits[lighting])
                    throw new BuildFailedException("Graphics quality is overriding the selected lighting quality.");
                for (int shadows = 0; shadows < 4; shadows++)
                {
                    settings.shadowQuality = shadows;
                    if (SoloGraphicsBudget.Shadows(settings) != shadows)
                        throw new BuildFailedException("Graphics or lighting quality is overriding the selected shadow quality.");
                }
            }
        var setup = Resources.Load<SoloLoadingResources>("Settings/SoloLoadingResources");
        if (setup == null || setup.screen == null) throw new BuildFailedException("Missing shared solo loading screen reference.");
        return "PASS: three runtime Forward presets included for Android stripping; per-pixel lights 4/6/8; nine independent graphics/lighting combinations and 36 shadow combinations; main-light shadow support retained; existing loading artwork referenced.";
    }
}
#endif
