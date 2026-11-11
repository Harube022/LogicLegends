#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Validate the multiplayer pipeline is included when Unity strips Android shader variants.
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
        var profile = Resources.Load<SoloGraphicsProfile>("Settings/SoloGraphicsProfile");
        if (profile == null || profile.pipeline == null || !registered.Contains(profile.pipeline))
            throw new BuildFailedException("The Open Archive pipeline used by solo graphics must be included in Android quality settings.");
        if (!profile.pipeline.supportsHDR || !profile.pipeline.supportsMainLightShadows || !profile.pipeline.supportsSoftShadows ||
            profile.atmosphere == null || !profile.atmosphere.TryGet<Bloom>(out _))
            throw new BuildFailedException("The shared Open Archive profile must include HDR, directional shadows and Bloom.");
        var setup = Resources.Load<SoloLoadingResources>("Settings/SoloLoadingResources");
        if (setup == null || setup.screen == null) throw new BuildFailedException("Missing shared solo loading screen reference.");
        return "PASS: Open Archive quality pipeline included in Android shader stripping; HDR, directional shadows, Bloom and the shared solo loading artwork are configured.";
    }
}
#endif
