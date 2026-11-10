#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class AndroidLightingChecks
{
    public static bool CompileAndroidValidation()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)throw new InvalidOperationException("Finish Play Mode/build first.");
        AndroidSoloBuildChecks.Validate();
        const string path="Assets/Tests/AndroidOptimization/LightingVariants.shadervariants", output="output/mobile-performance/AndroidLightingValidation";
        Directory.CreateDirectory("Assets/Tests/AndroidOptimization");Directory.CreateDirectory(output);AssetDatabase.Refresh();
        var variants=AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
        if(variants==null){variants=new ShaderVariantCollection();AssetDatabase.CreateAsset(variants,path);}
        foreach(var name in new[]{"Universal Render Pipeline/Lit","Universal Render Pipeline/Simple Lit"})
            foreach(var keys in new[]{new string[0],new[]{"_ADDITIONAL_LIGHTS"},new[]{"_ADDITIONAL_LIGHTS","_MAIN_LIGHT_SHADOWS_CASCADE"}})
                variants.Add(new ShaderVariantCollection.ShaderVariant(Shader.Find(name),PassType.ScriptableRenderPipeline,keys));
        EditorUtility.SetDirty(variants);AssetDatabase.SaveAssetIfDirty(variants);
        File.WriteAllText("output/mobile-performance/AndroidLightingBuild.txt","BUILDING Android lighting validation bundle; not an APK.\n");
        var build=new AssetBundleBuild{assetBundleName="solo_lighting_validation",assetNames=new[]{path,"Assets/Resources/Settings/SoloOpaqueLit.mat","Assets/Materials/FormalInformalProofs/Torch_Flame.mat"}};
        bool passed=BuildPipeline.BuildAssetBundles(output,new[]{build},BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.Android)!=null;
        File.AppendAllText("output/mobile-performance/AndroidLightingBuild.txt",passed?"PASS Android player-script and shader/material asset-bundle compilation; not an APK; device rendering not verified.\n":"FAIL\n");
        return passed;
    }
    public static string CheckBuildCopies()
    {
        var log = new System.Text.StringBuilder(AndroidSoloBuildChecks.Validate() + "\n");
        var catalog = Resources.Load<SoloMobileMeshes>("Settings/SoloMobileMeshes");
        foreach (var name in new[] { "TruthTable", "RulesOfInference" })
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
            try
            {
                int colliders = 0; long before = 0, after = 0;
                foreach (var root in scene.GetRootGameObjects()) foreach (var c in root.GetComponentsInChildren<MeshCollider>(true))
                    if (c.sharedMesh != null) { colliders++; before += c.sharedMesh.triangles.Length / 3; }
                int changed = AndroidEnvironmentBuildProcessor.Apply(scene);
                int count = 0;
                foreach (var root in scene.GetRootGameObjects()) foreach (var c in root.GetComponentsInChildren<MeshCollider>(true))
                    if (c.sharedMesh != null) { count++; after += c.sharedMesh.triangles.Length / 3; }
                if (count != colliders) throw new Exception("Collider count changed.");
                foreach (var entry in catalog.entries)
                {
                    if (entry.source.subMeshCount != entry.low.subMeshCount || entry.source.bounds != entry.low.bounds)
                        throw new Exception("Mobile mesh lost material slots/bounds.");
                    if (catalog.Resolve(entry.high, 0) != entry.low) throw new Exception("Build-copy Low selection broken.");
                }
                log.AppendLine("PASS " + name + ": " + changed + " render filters; same " + count + " MeshColliders; collision triangle inventory " + before + " -> " + after + "; material slots/bounds preserved.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        File.WriteAllText("output/mobile-performance/BuildCopyChecks.txt", log.ToString()); return log.ToString();
    }

    // Editor GPU execution, not an Android rendering claim.
    public static string CheckPixels()
    {
        var log = new System.Text.StringBuilder("Editor D3D12: additional point-light contribution on the existing solo Simple Lit material. Android GPU remains unverified.\n");
        var previousPipeline = QualitySettings.renderPipeline;
        var previousAmbient = RenderSettings.ambientProbe;
        var oldCamera = Camera.main;
        var root = new GameObject("Isolated lamp regression");
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(root.transform);
        floor.transform.position = new Vector3(1000, 0, 0); floor.transform.localScale = new Vector3(5, .05f, 5); floor.layer = 31;
        var material = new Material(Resources.Load<Material>("Settings/SoloOpaqueLit"));
        material.SetColor("_BaseColor", new Color(.3f,.3f,.3f)); floor.GetComponent<Renderer>().sharedMaterial = material;
        var lamp = new GameObject("Point light").AddComponent<Light>(); lamp.transform.SetParent(root.transform); lamp.transform.position = new Vector3(1000, 2, 0);
        lamp.type = LightType.Point; lamp.range = 10; lamp.intensity = 12; lamp.shadows = LightShadows.None;
        var camera = new GameObject("Lamp regression camera").AddComponent<Camera>();camera.transform.SetParent(root.transform);
        camera.enabled = false; camera.transform.position = new Vector3(1000, 3, -4); camera.transform.LookAt(new Vector3(1000, 0, 0));
        camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;camera.backgroundColor = Color.black;
        camera.targetTexture = new RenderTexture(96,96,24);camera.targetTexture.Create();
        var image = new Texture2D(96,96,TextureFormat.RGB24,false);
        var directions = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        var states = new bool[directions.Length];
        for(int i=0;i<directions.Length;i++){states[i]=directions[i].enabled;if(directions[i].type==LightType.Directional)directions[i].enabled=false;}
        SoloSceneGraphics gate = null;
        try
        {
            RenderSettings.ambientProbe = new SphericalHarmonicsL2();
            for(int tier=0;tier<3;tier++)
            {
                QualitySettings.renderPipeline = Resources.Load<UniversalRenderPipelineAsset>("Settings/SoloLighting" + tier);
                lamp.enabled=false;float off=Read(camera,image);lamp.enabled=true;float on=Read(camera,image);
                if(on-off<.005f)throw new Exception("No point-light pool at tier " + tier+" off="+off+" on="+on);
                log.AppendLine("PASS tier="+tier+" mean pixel brightness off="+off.ToString("F4")+" on="+on.ToString("F4"));
            }
            if(oldCamera!=null)oldCamera.tag="Untagged";camera.tag="MainCamera";camera.enabled=true;
            lamp.enabled=false;gate=root.AddComponent<SoloSceneGraphics>();gate.Apply(new SoloSettingsData());
            var update=typeof(SoloSceneGraphics).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            update.Invoke(gate,null);lamp.enabled=true;update.Invoke(gate,null);
            if(!lamp.enabled || gate.ActiveLocalLights==0)throw new Exception("Late-activated light stayed latched off or camera gate did not run.");
            log.AppendLine("PASS initially disabled fixture activated after discovery.");
        }
        finally
        {
            if(gate!=null)UnityEngine.Object.DestroyImmediate(gate);
            if(oldCamera!=null)oldCamera.tag="MainCamera";
            for(int i=0;i<directions.Length;i++)if(directions[i]!=null)directions[i].enabled=states[i];
            QualitySettings.renderPipeline=previousPipeline;RenderSettings.ambientProbe=previousAmbient;
            var target=camera.targetTexture;camera.targetTexture=null;target.Release();
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(root);
        }
        File.WriteAllText("output/mobile-performance/LightingPixelChecks.txt",log.ToString());return log.ToString();
    }
    static float Read(Camera camera,Texture2D image)
    {
        camera.Render();var previous=RenderTexture.active;RenderTexture.active=camera.targetTexture;
        image.ReadPixels(new Rect(0,0,96,96),0,0);image.Apply();RenderTexture.active=previous;
        float sum=0;var colors=image.GetPixels32();foreach(var color in colors)sum+=(color.r+color.g+color.b)/765f;
        return sum/colors.Length;
    }
}
#endif
