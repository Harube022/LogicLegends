#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Refreshes existing shop portraits in place, retaining their sprite GUIDs and UI references.</summary>
public static class LatestSkinPortraitSetup
{
    public static string UpdateAll()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var result=JsonUtility.FromJson<LatestSkinPreparation.Result>(File.ReadAllText(LatestSkinPreparation.Root+"/Preparation/PreparedSkins.json"));
        var scene=EditorSceneManager.NewPreviewScene();int count=0;
        try
        {
            foreach(var skin in result.skins.Where(s=>s.category!="Uniform"))
            {
                var path="Assets/Art/ShopPortraits/"+skin.category+(skin.gender=="Female"?"_Costume":"_Skin")+".png";
                if(!File.Exists(path))throw new InvalidOperationException("Existing portrait missing: "+path);
                var preview=new PreviewRenderUtility();
                try
                {
                    var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(skin.prefab),scene);
                    preview.AddSingleGO(g);g.transform.position=Vector3.zero;g.transform.rotation=Quaternion.identity;
                    var animator=g.GetComponentInChildren<Animator>();animator.enabled=false;
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(skin.clips.Single(c=>c.action=="Idle").path).SampleAnimation(animator.gameObject,0);
                    var b=LatestSkinPreparation.BakedBounds(g);var center=new Vector3(b.center.x,b.min.y+b.size.y*.70f,b.center.z);
                    preview.cameraFieldOfView=32;float distance=b.size.y*.34f/Mathf.Tan(16*Mathf.Deg2Rad);
                    preview.camera.transform.position=center+Vector3.forward*distance;preview.camera.transform.LookAt(center);
                    preview.camera.nearClipPlane=.02f;preview.camera.farClipPlane=100;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=Color.clear;
                    preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(35,205,0);
                    preview.lights[1].intensity=.8f;preview.lights[1].transform.rotation=Quaternion.Euler(20,25,0);preview.ambientColor=Color.grey;
                    preview.BeginStaticPreview(new Rect(0,0,425,320));preview.Render(true,true);
                    // EndStaticPreview creates an RGB thumbnail; read RGBA before ending to retain the clear background.
                    var previous=RenderTexture.active;RenderTexture.active=preview.camera.targetTexture;
                    var image=new Texture2D(425,320,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,425,320),0,0);image.Apply();RenderTexture.active=previous;
                    var thumbnail=preview.EndStaticPreview();Object.DestroyImmediate(thumbnail);
                    try
                    {
                        if(image.GetPixel(0,0).a>.01f)throw new InvalidOperationException("Portrait background did not remain transparent; originals preserved.");
                        var backup="Temp/LatestSkinsIntegrationBackup/"+path;Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!File.Exists(backup))File.Copy(path,backup);
                        File.WriteAllBytes(path,image.EncodeToPNG());count++;
                    }
                    finally{Object.DestroyImmediate(image);}
                }
                finally{preview.Cleanup();}
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        AssetDatabase.Refresh();return "Updated "+count+" transparent shop portraits in place; sprite GUIDs and all item/UI IDs retained.";
    }
}
#endif
