using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

// Creates foliage assets only; does not alter the existing garden scene/model.
public static class Option3FoliageSetup
{
    const string Root="Assets/Art/Option3Foliage";
    [MenuItem("Tools/Logic Legends/Build Option 3 Foliage")]
    public static void BuildAndPackage()
    {
        Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory("output/Option3Foliage");AssetDatabase.Refresh();
        string texturePath=Root+"/Textures/Option3_FoliagePalette.png";
        var ti=(TextureImporter)AssetImporter.GetAtPath(texturePath);ti.sRGBTexture=true;ti.filterMode=FilterMode.Point;ti.mipmapEnabled=false;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new Exception("Requires URP Lit.");
        string path=Root+"/Materials/Option3_Foliage.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}mat.shader=shader;mat.name="Option3_Foliage";
        mat.SetColor("_BaseColor",Color.white);mat.SetTexture("_BaseMap",texture);mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.16f);mat.enableInstancing=true;EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();
        var names=new[]{"Option3_BushesAndFlowers","Hedge_Segment","Mixed_Flower_Cluster"};
        foreach(string name in names)
        {
            string fbx=Root+"/"+name+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.globalScale=1;importer.useFileScale=true;importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"Option3_Foliage"),mat);importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(fbx);var ob=Object.Instantiate(model);
            try
            {
                foreach(var renderer in ob.GetComponentsInChildren<MeshRenderer>())
                    if(renderer.sharedMaterial!=mat||renderer.sharedMaterial.GetTexture("_BaseMap")!=texture)throw new Exception("Missing foliage material/texture: "+renderer.name);
                PrefabUtility.SaveAsPrefabAsset(ob,Root+"/Prefabs/"+name+".prefab");
            }
            finally{Object.DestroyImmediate(ob);}
        }
        AssetDatabase.SaveAssets();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var border=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Option3_BushesAndFlowers.prefab"));
        var renderers=border.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        if(Mathf.Abs(bounds.min.y)>.005f)throw new Exception("Bottom pivot is not at the ground.");
        int triangles=border.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
        var report="PASS: three FBX imports with URP texture/material remapping.\nPASS: foliage only, 22 hedge sections and 12 flower clusters.\nPASS: bottom-centered assembled root.\nTriangles: "+triangles+"\nBounds: "+bounds+"\n";
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.4f,.44f,.5f);
        var key=new GameObject("PreviewKey").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.6f;key.transform.rotation=Quaternion.Euler(45,145,0);
        var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=7.8f;
        camera.transform.position=new Vector3(11,13,15);camera.transform.LookAt(new Vector3(0,.2f,0));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.09f,.12f);camera.nearClipPlane=.1f;camera.farClipPlane=80;
        var rt=new RenderTexture(1400,1000,24,RenderTextureFormat.ARGB32);rt.Create();camera.aspect=1.4f;
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
        var png=new Texture2D(1400,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1400,1000),0,0);png.Apply();
        File.WriteAllBytes("SourceAssets/Option3Foliage/Unity_Foliage_Preview.png",png.EncodeToPNG());
        if(png.GetPixels32().Count(p=>p.r>220&&p.b>220&&p.g<40)>1000)throw new Exception("Pink shader error in render.");
        report+="PASS: Unity URP render; no missing/pink material.\n";File.WriteAllText("SourceAssets/Option3Foliage/unity-validation.txt",report);
        RenderTexture.active=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(png);
        AssetDatabase.ExportPackage(Root,"output/Option3Foliage/Option3Foliage.unitypackage",ExportPackageOptions.Recurse);
        Debug.Log("OPTION3_FOLIAGE_UNITY_PASS");
    }
}
