#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Read-only survey of saved scenes; preview scenes cannot write progression or rewards.
public static class SoloGraphicsChecks
{
    public const string Folder = "output/AndroidGraphicsAudit";
    [Serializable] public sealed class Survey
    {
        public string scene;
        public int renderers, activeRenderers, localLights, enabledLocalLights, shadowLights, colliders, legacyFoliage;
        public long triangles;
        public string[] foliageMaterials, unsupportedShaders;
    }
    [MenuItem("Logic Legends/Graphics/Audit all ten saved trials")]
    public static void SurveyAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Finish Play Mode first.");
        Directory.CreateDirectory(Folder);
        for (int trial = 1; trial <= 10; trial++)
        {
            string path = "Assets/Scenes/" + TrialCatalog.Get(trial).Scene + ".unity";
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var report = new Survey { scene = path };
                var foliage = new HashSet<string>(); var unsupported = new HashSet<string>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    report.colliders += root.GetComponentsInChildren<Collider>(true).Length;
                    foreach (var light in root.GetComponentsInChildren<Light>(true))
                    {
                        if (light.type == LightType.Point || light.type == LightType.Spot)
                        { report.localLights++; if (light.isActiveAndEnabled) report.enabledLocalLights++; }
                        if (light.isActiveAndEnabled && light.shadows != LightShadows.None) report.shadowLights++;
                    }
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        report.renderers++; if (renderer.isVisible || renderer.enabled && renderer.gameObject.activeInHierarchy) report.activeRenderers++;
                        bool legacy = false;
                        foreach (var material in renderer.sharedMaterials)
                        {
                            if (material == null || material.shader == null) { unsupported.Add(renderer.name + ": missing material/shader"); continue; }
                            if (!material.shader.isSupported) unsupported.Add(material.shader.name);
                            if (SoloFoliageMaterials.IsLegacy(material)) { legacy = true; foliage.Add(AssetDatabase.GetAssetPath(material)); }
                        }
                        if (legacy) report.legacyFoliage++;
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter != null && filter.sharedMesh != null)
                            for (int i = 0; i < filter.sharedMesh.subMeshCount; i++) report.triangles += filter.sharedMesh.GetIndexCount(i) / 3;
                    }
                }
                report.foliageMaterials = foliage.ToArray(); report.unsupportedShaders = unsupported.ToArray();
                File.WriteAllText(Folder + "/Trial" + trial + "_SavedScene.json", JsonUtility.ToJson(report, true));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        Debug.Log("Saved-scene graphics survey complete: " + Folder);
    }
    [MenuItem("Logic Legends/Graphics/Check quality policy")]
    public static void CheckPolicy()
    {
        var report = new System.Text.StringBuilder();
        for (int tier = 0; tier < 3; tier++)
        {
            var data = new SoloSettingsData { graphicsQuality = tier, lightingQuality = 2, shadowQuality = 3, motionBlur = true };
            int kept = 0; for (int i = 0; i < 100; i++) if (SoloGraphicsBudget.KeepFoliage(data, i)) kept++;
            if (SoloGraphicsBudget.LightLimit(data) != new[] { 6, 12, 24 }[tier] || kept != new[] { 50, 75, 100 }[tier]) throw new Exception("Quality budget mismatch.");
            if (tier == 0 && (SoloGraphicsBudget.Shadows(data) != 0 || SoloGraphicsBudget.PostProcessing(data))) throw new Exception("Low permits an expensive effect.");
            report.AppendLine("PASS tier=" + tier + " lights=" + SoloGraphicsBudget.LightLimit(data) + " grass=" + kept + "% shadows=" + SoloGraphicsBudget.Shadows(data));
        }
        var shader = Resources.Load<Shader>("Settings/SoloFoliage");
        if (shader == null) throw new Exception("Cutout shader missing from build Resources.");
        var material = new Material(shader);
        try
        {
            ShaderUtil.CompilePass(material, 0, true);
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) throw new Exception(message.message);
            report.AppendLine("PASS foliage forward pass compiled for current Editor graphics API; Android GPU execution still requires a device.");
        }
        finally { UnityEngine.Object.DestroyImmediate(material); }
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/PolicyChecks.txt", report.ToString());
    }
    [MenuItem("Logic Legends/Graphics/Compile Android foliage validation bundle")]
    public static void CompileAndroidShaders()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Finish the current Play Mode/build operation first.");
        Directory.CreateDirectory(Folder + "/AndroidShaders");
        File.WriteAllText(Folder + "/AndroidShaderBuild.txt", "QUEUED\n");
            try
            {
                const string sample = "Assets/Tests/AndroidGraphics/SoloGrass.mat", variantsPath = "Assets/Tests/AndroidGraphics/SoloFoliageVariants.shadervariants";
                Directory.CreateDirectory("Assets/Tests/AndroidGraphics");
                var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/CHEATCODE/LOWPOLYENVIRO/Lowpoly_Environments/Sources/Materials/PT_Grass_Mat.mat");
                var material = SoloFoliageMaterials.Create(source); material.hideFlags = HideFlags.None;
                if (AssetDatabase.LoadAssetAtPath<Material>(sample) != null) AssetDatabase.DeleteAsset(sample);
                AssetDatabase.CreateAsset(material, sample);
                var variants = new ShaderVariantCollection();
                foreach (var keywords in new[] { new string[0], new[] { "_ADDITIONAL_LIGHTS_VERTEX" }, new[] { "_ADDITIONAL_LIGHTS", "_MAIN_LIGHT_SHADOWS_CASCADE" }, new[] { "_ADDITIONAL_LIGHTS", "_CLUSTER_LIGHT_LOOP" } })
                    variants.Add(new ShaderVariantCollection.ShaderVariant(material.shader, UnityEngine.Rendering.PassType.ScriptableRenderPipeline, keywords));
                if (AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(variantsPath) != null) AssetDatabase.DeleteAsset(variantsPath);
                AssetDatabase.CreateAsset(variants, variantsPath);
                AssetDatabase.SaveAssetIfDirty(material); AssetDatabase.SaveAssetIfDirty(variants);
                string apis = string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Select(a => a.ToString()));
                File.WriteAllText(Folder + "/AndroidShaderBuild.txt", "BUILDING Android targets: " + apis + "\n");
                var manifest = BuildPipeline.BuildAssetBundles(Folder + "/AndroidShaders", new[] { new AssetBundleBuild { assetBundleName = "solo_foliage_android", assetNames = new[] { sample, variantsPath, "Assets/Art/TruthTableGround/TruthTableGround.shader" } } }, BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.Android);
                if (manifest == null) throw new Exception("Android validation bundle failed; inspect the Editor console.");
                File.AppendAllText(Folder + "/AndroidShaderBuild.txt", "PASS Android shader asset-bundle compilation. Not an APK and not a device rendering/performance test.\n");
            }
            catch (Exception error) { File.AppendAllText(Folder + "/AndroidShaderBuild.txt", "FAIL " + error + "\n"); Debug.LogException(error); }
    }
}
#endif
