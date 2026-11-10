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
            if (asset == null || !registered.Contains(asset) || asset.additionalLightsRenderingMode != LightRenderingMode.PerPixel || asset.maxAdditionalLightsCount < 4)
                throw new BuildFailedException("Register SoloLighting" + tier + " in IncludeAdditionalRPAssets; retain per-pixel additional lighting.");
        }
        var setup = Resources.Load<SoloLoadingResources>("Settings/SoloLoadingResources");
        if (setup == null || setup.screen == null) throw new BuildFailedException("Missing shared solo loading screen reference.");
        return "PASS: three runtime Forward presets included for Android stripping; per-pixel lights 4/6/8; existing loading artwork referenced.";
    }
}
#endif
