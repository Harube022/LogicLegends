using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class RuinsMapBorderSetup
{
    const string Root="Assets/Art/RuinsMapBorder";
    [MenuItem("Tools/Logic Legends/Build Full Ruins Map Border")]
    public static void BuildAndPackage()
    {
        Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory("output/RuinsMapBorder");AssetDatabase.Refresh();
        var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Ruins_Stone_BaseColor.png");if(tex==null)throw new Exception("Stone texture missing.");
        var shadow=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Ruins_StoneShadow_BaseColor.png");if(shadow==null)throw new Exception("Baked shadow texture missing.");
        var textures=new[]{tex,shadow};
        var shader=Shader.Find("Universal Render Pipeline/Lit");var mats=new Material[2];string[] names={"Border_Stone","Border_StoneShadow"};
        for(int i=0;i<2;i++)
        {
            string path=Root+"/Materials/"+names[i]+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}mat.shader=shader;mat.name=names[i];mat.SetTexture("_BaseMap",textures[i]);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(mat);mats[i]=mat;
        }
        AssetDatabase.SaveAssets();
        foreach(string name in new[]{"Ruins_Map_FullBorder","Ruins_Map_FullBorder_NativeAligned"})
        {
            string path=Root+"/"+name+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.globalScale=1;importer.useFileScale=true;importer.importAnimation=false;
            for(int i=0;i<2;i++)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),names[i]),mats[i]);importer.SaveAndReimport();
            var ob=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                foreach(var r in ob.GetComponentsInChildren<MeshRenderer>())foreach(var mat in r.sharedMaterials)
                    if(mat==null||!mats.Contains(mat)||mat.GetTexture("_BaseMap")!=textures[Array.IndexOf(mats,mat)])throw new Exception("Unmapped border material.");
                if(ob.GetComponentsInChildren<MeshFilter>().Length!=4)throw new Exception("Unexpected border mesh count.");
                PrefabUtility.SaveAsPrefabAsset(ob,Root+"/Prefabs/"+name+".prefab");
            }
            finally{Object.DestroyImmediate(ob);}
        }
        AssetDatabase.SaveAssets();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var border=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Ruins_Map_FullBorder.prefab"));
        var rens=border.GetComponentsInChildren<MeshRenderer>();var bounds=rens[0].bounds;foreach(var r in rens)bounds.Encapsulate(r.bounds);
        if(Mathf.Abs(bounds.min.y)>.005f)throw new Exception("Centered border bottom pivot invalid.");
        // Compare the native-aligned border directly against the actual original FLOOR FBX.
        var native=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Ruins_Map_FullBorder_NativeAligned.prefab"));
        var floor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/MAPS/REMADE/REMAKE TUTS/2nd/FLOOR.fbx"));
        var floorR=floor.GetComponentsInChildren<MeshRenderer>();var floorBounds=floorR[0].bounds;foreach(var r in floorR)floorBounds.Encapsulate(r.bounds);
        var nativeR=native.GetComponentsInChildren<MeshRenderer>();var nativeBounds=nativeR[0].bounds;foreach(var r in nativeR)nativeBounds.Encapsulate(r.bounds);
        if(Mathf.Abs(nativeBounds.min.y-floorBounds.min.y)>.01f||nativeBounds.min.x>floorBounds.min.x||nativeBounds.max.x<floorBounds.max.x||nativeBounds.min.z>floorBounds.min.z||nativeBounds.max.z<floorBounds.max.z)throw new Exception("Border does not cover original floor bounds.");
        Object.DestroyImmediate(native);Object.DestroyImmediate(floor);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.4f,.44f,.5f);
        var key=new GameObject("PreviewLight").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.5f;key.transform.rotation=Quaternion.Euler(45,145,0);
        var cam=new GameObject("PreviewCamera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=90;cam.aspect=1.4f;cam.transform.position=new Vector3(95,150,160);cam.transform.LookAt(new Vector3(0,4,0));cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.07f,.09f,.11f);cam.nearClipPlane=.1f;cam.farClipPlane=500;
        var rt=new RenderTexture(1400,1000,24,RenderTextureFormat.ARGB32);rt.Create();RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
        var png=new Texture2D(1400,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1400,1000),0,0);png.Apply();File.WriteAllBytes("SourceAssets/RuinsMapBorder/Unity_Border_Preview.png",png.EncodeToPNG());
        if(png.GetPixels32().Count(p=>p.r>220&&p.b>220&&p.g<40)>1000)throw new Exception("Missing shader pink detected.");
        RenderTexture.active=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(png);
        File.WriteAllText("SourceAssets/RuinsMapBorder/unity-validation.txt","PASS: centered and native-aligned FBXs import with URP image materials.\nPASS: native-aligned border encloses original floor on all sides and matches its bottom height.\nOriginal floor bounds: "+floorBounds+"\nAligned border bounds: "+nativeBounds+"\nPASS: border-only Unity render, no missing material.\n");
        AssetDatabase.ExportPackage(Root,"output/RuinsMapBorder/RuinsMapBorder.unitypackage",ExportPackageOptions.Recurse);Debug.Log("RUINS_MAP_BORDER_UNITY_PASS");
    }
}
