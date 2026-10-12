using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Creates assets only. Does not modify or populate any gameplay scene.
public static class InductionAltarSetup
{
    const string Root = "Assets/Art/InductionAltar";
    static readonly string[] VariantNames = { "BaseCase", "InductiveHypothesis", "InductiveStep" };
    static readonly Color[] Colors = { new Color(.025f,.48f,1f), new Color(.63f,.18f,1f), new Color(1f,.59f,.055f) };
    static readonly string[] Titles = { "BASE CASE", "INDUCTIVE HYPOTHESIS", "INDUCTIVE STEP" };
    static readonly string[] First = { "n = 1", "Assume P(k)", "Use P(k)" };
    static readonly string[] Second = { "P(1)", "k >= 1", "Prove P(k + 1)" };

    [MenuItem("Tools/Logic Legends/Build Induction Altar Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Prefabs");
        AssetDatabase.Refresh();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Install URP or the Standard shader.");
        var shared = new Dictionary<string, Material>();
        shared["Altar_Stone"] = MakeMaterial("Altar_Stone", shader, Color.white, 0, .15f, 0, "Stone_BaseColor.png");
        shared["Altar_StoneEdge"] = MakeMaterial("Altar_StoneEdge", shader, new Color(.43f,.45f,.53f), 0, .18f);
        shared["Altar_Bronze"] = MakeMaterial("Altar_Bronze", shader, new Color(.66f,.49f,.27f), .65f, .5f);
        shared["Altar_TitleFace"] = MakeMaterial("Altar_TitleFace", shader, new Color(.018f,.055f,.12f), .1f, .25f);
        shared["Altar_Parchment"] = MakeMaterial("Altar_Parchment", shader, new Color(.91f,.77f,.53f), 0, .12f);
        shared["Altar_EmblemFace"] = MakeMaterial("Altar_EmblemFace", shader, new Color(.03f,.075f,.15f), .3f, .5f);
        var accents = new Material[3]; var crystals = new Material[3];
        for (int v=0;v<3;v++)
        {
            accents[v] = MakeMaterial("Altar_Accent_"+VariantNames[v],shader,Colors[v],.2f,.65f,2f);
            crystals[v] = MakeMaterial("Altar_Crystal_"+VariantNames[v],shader,Colors[v],.3f,.7f,.75f);
        }
        foreach (string file in new[] { "InductionAltar_Shared.fbx", "InductionAltar_Boards.fbx" })
        {
            var importer = AssetImporter.GetAtPath(Root+"/"+file) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing FBX: "+file);
            importer.globalScale=1; importer.useFileScale=true; importer.importAnimation=false;
            importer.isReadable=false; importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.importCameras=false; importer.importLights=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            foreach (var pair in shared) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"Altar_Accent"),accents[0]);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"Altar_Crystal"),crystals[0]);
            importer.SaveAndReimport();
        }
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/InductionAltar_Shared.fbx");
        var font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            string guid=AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset").FirstOrDefault();
            if (guid!=null) font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
        }
        if (font==null) throw new InvalidOperationException("Import TextMeshPro Essential Resources, then run Build again.");
        GameObject firstPrefab=null;
        for (int v=0;v<3;v++)
        {
            var altar = new GameObject("Altar_"+VariantNames[v]);
            try
            {
                var geometry=(GameObject)PrefabUtility.InstantiatePrefab(model);
                geometry.name="SharedAltarGeometry"; geometry.transform.SetParent(altar.transform,false);
                ApplyVariant(geometry, accents[v], crystals[v]);
                AddLabels(altar,geometry,font,v);
                var saved=PrefabUtility.SaveAsPrefabAsset(altar,Root+"/Prefabs/Altar_"+VariantNames[v]+".prefab");
                if (v==0) firstPrefab=saved;
                else AssertSharedMeshes(firstPrefab,saved);
            }
            finally { Object.DestroyImmediate(altar); }

            var boards = new GameObject("Boards_"+VariantNames[v]);
            try
            {
                // Use the same FBX submeshes as full altar instances, even for standalone boards.
                var geometry=Object.Instantiate(model); geometry.name="SharedBoardGeometry";
                geometry.transform.SetParent(boards.transform,false);
                foreach (var r in geometry.GetComponentsInChildren<MeshRenderer>())
                    if (r.name!="TitleBoard" && !r.name.StartsWith("ParchmentPanel_")) Object.DestroyImmediate(r.gameObject);
                ApplyVariant(geometry,accents[v],crystals[v]);
                AddLabels(boards,geometry,font,v);
                PrefabUtility.SaveAsPrefabAsset(boards,Root+"/Prefabs/Boards_"+VariantNames[v]+".prefab");
            }
            finally { Object.DestroyImmediate(boards); }
        }
        AssetDatabase.SaveAssets();
        var renderers=firstPrefab.GetComponentsInChildren<MeshRenderer>();
        var bounds=renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
        if (Mathf.Abs(bounds.min.y)>.005f) throw new Exception("Altar pivot is not at the base: "+bounds.min);
        if (Mathf.Abs(bounds.center.x)>.005f) throw new Exception("Altar pivot is not horizontally centered.");
        if (firstPrefab.GetComponentsInChildren<TMP_Text>().Length!=3) throw new Exception("Three editable labels required.");
        if (firstPrefab.GetComponentsInChildren<MeshFilter>().Count(f=>f.sharedMesh!=null)!=9) throw new Exception("Unexpected geometry count.");
        File.WriteAllText("SourceAssets/InductionAltar/unity-validation.txt",
            "PASS: three station prefabs and three interchangeable board prefabs.\n"+
            "PASS: all station variants reference the exact same FBX mesh assets.\n"+
            "PASS: 3 TextMeshPro labels per station; blank FBX boards.\n"+
            "PASS: bottom-centered pivot. Bounds: "+bounds+"\n"+
            "Font: "+AssetDatabase.GetAssetPath(font)+"\n"+
            string.Join("\n",firstPrefab.GetComponentsInChildren<Transform>().Where(t=>t.name.EndsWith("TextAnchor")).Select(t=>t.name+": "+t.position)));
        Debug.Log("INDUCTION_ALTAR_UNITY_VALIDATION_PASS");
    }
    static Material MakeMaterial(string name, Shader shader, Color color, float metallic, float smooth, float glow=0, string texture=null)
    {
        string path=Root+"/Materials/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
        mat.shader=shader; mat.name=name;
        mat.SetColor(mat.HasProperty("_BaseColor")?"_BaseColor":"_Color",color);
        mat.SetFloat("_Metallic",metallic);mat.SetFloat(mat.HasProperty("_Smoothness")?"_Smoothness":"_Glossiness",smooth);
        if(texture!=null)mat.SetTexture(mat.HasProperty("_BaseMap")?"_BaseMap":"_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+texture));
        mat.SetColor("_EmissionColor",color*glow);
        mat.globalIlluminationFlags=glow>0?MaterialGlobalIlluminationFlags.RealtimeEmissive:MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        if(glow>0)mat.EnableKeyword("_EMISSION");else mat.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(mat);return mat;
    }
    static void ApplyVariant(GameObject geometry,Material accent,Material crystal)
    {
        foreach(var r in geometry.GetComponentsInChildren<MeshRenderer>())
        {
            var materials=r.sharedMaterials;
            for(int i=0;i<materials.Length;i++)
            {
                if(materials[i].name.StartsWith("Altar_Accent"))materials[i]=accent;
                else if(materials[i].name.StartsWith("Altar_Crystal"))materials[i]=crystal;
            }
            r.sharedMaterials=materials;
        }
    }
    static void AddLabels(GameObject root,GameObject geometry,TMP_FontAsset font,int variant)
    {
        var anchors=geometry.GetComponentsInChildren<Transform>();
        string[] names={"TitleTextAnchor","Panel01TextAnchor","Panel02TextAnchor"};
        string[] labels={Titles[variant],First[variant],Second[variant]};
        for(int i=0;i<3;i++)
        {
            var anchor=anchors.Single(t=>t.name==names[i]);
            var go=new GameObject(i==0?"TitleLabel":"PanelLabel_"+i,typeof(RectTransform));go.transform.SetParent(anchor,false);
            // TMP's front faces local -Z. Anchor Z identifies the outward-facing side of this asset.
            Vector3 outward=anchor.position.z>=0?Vector3.forward:Vector3.back;
            go.transform.rotation=Quaternion.LookRotation(-outward,Vector3.up);
            var text=go.AddComponent<TextMeshPro>();text.font=font;text.text=labels[i];
            text.alignment=TextAlignmentOptions.Center;text.fontStyle=FontStyles.Bold;
            text.enableAutoSizing=true;text.fontSize=i==0?2.1f:2.4f;text.fontSizeMin=.65f;text.fontSizeMax=i==0?2.1f:2.4f;
            text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;
            text.color=i==0?new Color(.9f,.95f,1):new Color(.12f,.075f,.035f);
            text.rectTransform.sizeDelta=i==0?new Vector2(1.76f,.31f):new Vector2(1.04f,.31f);
            text.margin=Vector4.zero;text.raycastTarget=false;
            text.ForceMeshUpdate();
        }
    }
    static void AssertSharedMeshes(GameObject first,GameObject other)
    {
        var baseline=first.GetComponentsInChildren<MeshFilter>().ToDictionary(f=>f.name,f=>f.sharedMesh);
        foreach(var f in other.GetComponentsInChildren<MeshFilter>())
            if(!baseline.TryGetValue(f.name,out var mesh)||mesh!=f.sharedMesh)throw new Exception("Variant duplicates mesh "+f.name);
    }

    public static void PreviewAndPackage()
    {
        Directory.CreateDirectory("output/InductionAltar");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var report=new List<string>();
        for(int v=0;v<3;v++)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Altar_"+VariantNames[v]+".prefab");
            var instance=Object.Instantiate(prefab);instance.transform.position=new Vector3((1-v)*2.55f,0,0);
            foreach(var text in instance.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                if(text.textInfo.characterCount==0 || text.bounds.size.x<=0)throw new Exception("Empty label: "+text.text);
                if(text.isTextOverflowing)throw new Exception("Label overflows: "+text.text);
                report.Add(VariantNames[v]+" | "+text.text+" | font size "+text.fontSize+" | bounds "+text.bounds.size);
            }
        }
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.32f,.36f,.45f);
        var key=new GameObject("PreviewKey").AddComponent<Light>();key.type=LightType.Directional;key.intensity=2f;
        key.transform.rotation=Quaternion.Euler(35,150,0);
        var camera=new GameObject("PreviewCamera").AddComponent<Camera>();
        camera.transform.position=new Vector3(0,3.3f,10);camera.transform.LookAt(new Vector3(0,1.45f,0));
        camera.orthographic=true;camera.orthographicSize=1.9f;camera.aspect=2.2f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.046f,.075f);
        camera.nearClipPlane=.1f;camera.farClipPlane=30;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var rt=new RenderTexture(1980,900,24,RenderTextureFormat.ARGB32);rt.Create();
        if(GraphicsSettings.currentRenderPipeline!=null)
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        else {camera.targetTexture=rt;camera.Render();}
        RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
        File.WriteAllBytes("SourceAssets/InductionAltar/Unity_AllVariants_Preview.png",image.EncodeToPNG());
        RenderTexture.active=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
        File.WriteAllLines("SourceAssets/InductionAltar/label-validation.txt",report);
        // Keep font/atlas dependencies, without exporting unrelated project scripts.
        var content=AssetDatabase.FindAssets("",new[]{Root}).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p=>!AssetDatabase.IsValidFolder(p)&&!p.EndsWith(".cs")).ToArray();
        var packagePaths=AssetDatabase.GetDependencies(content,true)
            .Where(p=>p.StartsWith("Assets/")&&!p.EndsWith(".cs")&&!AssetDatabase.IsValidFolder(p))
            .Concat(new[]{Root+"/Editor/InductionAltarSetup.cs"}).Distinct().ToArray();
        AssetDatabase.ExportPackage(packagePaths,"output/InductionAltar/InductionAltar.unitypackage",ExportPackageOptions.Default);
        Debug.Log("INDUCTION_ALTAR_PREVIEW_AND_PACKAGE_PASS");
    }

    public static void BuildAndPackage(){Build();PreviewAndPackage();}
}
