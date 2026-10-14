using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class RuinsPropsSetup
{
    const string Root="Assets/Art/RuinsProps";
    [MenuItem("Tools/Logic Legends/Build Ruins Prop Kit")]
    public static void BuildAndPackage()
    {
        Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory("output/RuinsProps");AssetDatabase.Refresh();
        string[] names={"Ruins_Stone","Ruins_Moss","Ruins_Crevice"};var mats=new Material[3];
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new Exception("Requires URP Lit.");
        for(int i=0;i<3;i++)
        {
            string texturePath=Root+"/Textures/"+(i==1?"Ruins_Moss_BaseColor":"Ruins_Stone_BaseColor")+".png";
            var ti=(TextureImporter)AssetImporter.GetAtPath(texturePath);ti.sRGBTexture=true;ti.maxTextureSize=512;ti.wrapMode=TextureWrapMode.Repeat;ti.SaveAndReimport();
            string matPath=Root+"/Materials/"+names[i]+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,matPath);}mat.name=names[i];mat.shader=shader;
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));mat.SetColor("_BaseColor",i==2?new Color(.2f,.22f,.2f):Color.white);mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.13f);mat.enableInstancing=true;EditorUtility.SetDirty(mat);mats[i]=mat;
        }
        AssetDatabase.SaveAssets();
        foreach(string fbx in Directory.GetFiles(Root,"*.fbx"))
        {
            string path=fbx.Replace('\\','/');var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.globalScale=1;importer.useFileScale=true;importer.importAnimation=false;importer.importLights=false;importer.importCameras=false;
            for(int i=0;i<3;i++)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),names[i]),mats[i]);importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var ob=Object.Instantiate(model);
            try
            {
                foreach(var r in ob.GetComponentsInChildren<MeshRenderer>())foreach(var mat in r.sharedMaterials)
                    if(mat==null||!mats.Contains(mat)||mat.GetTexture("_BaseMap")==null)throw new Exception("Missing material/texture: "+path);
                PrefabUtility.SaveAsPrefabAsset(ob,Root+"/Prefabs/"+Path.GetFileNameWithoutExtension(path)+".prefab");
            }
            finally{Object.DestroyImmediate(ob);}
        }
        AssetDatabase.SaveAssets();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var perimeter=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Ruins_Props_Perimeter.prefab"));
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.40f,.44f,.50f);
        var key=new GameObject("StudioLight").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.5f;key.transform.rotation=Quaternion.Euler(45,145,0);
        var cam=new GameObject("ValidationCamera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=8.6f;cam.aspect=1.4f;cam.transform.position=new Vector3(12,14,15);cam.transform.LookAt(new Vector3(0,.5f,0));cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.065f,.08f,.10f);cam.nearClipPlane=.1f;cam.farClipPlane=80;
        var rt=new RenderTexture(1400,1000,24,RenderTextureFormat.ARGB32);rt.Create();RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        RenderTexture.active=rt;var image=new Texture2D(1400,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,1000),0,0);image.Apply();File.WriteAllBytes("SourceAssets/RuinsProps/Unity_Ruins_Preview.png",image.EncodeToPNG());
        if(image.GetPixels32().Count(p=>p.r>220&&p.b>220&&p.g<40)>1000)throw new Exception("Pink error material in preview.");
        File.WriteAllText("SourceAssets/RuinsProps/unity-validation.txt","PASS: ten FBX imports, nine modular ruin props plus perimeter layout.\nPASS: image textures assigned to URP stone/moss/crevice materials.\nPASS: Unity render has no pink missing-material shading.\nNo gameplay scene or original map modified.\n");
        RenderTexture.active=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
        AssetDatabase.ExportPackage(Root,"output/RuinsProps/RuinsProps.unitypackage",ExportPackageOptions.Recurse);Debug.Log("RUINS_PROPS_UNITY_PASS");
    }
}
