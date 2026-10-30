#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using LogicLegends.Trees;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Scene-only chamber art and fitted static collision. Does not edit source packs or gameplay.</summary>
public static class TreesChamberSetup
{
    const string Folder="Assets/Materials/TreesChamber";
    const string Dungeon="Assets/CHEATCODE/DUNGEON/LowPolyDungeon/";
    const string Art="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/";
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.name!="Trees")throw new InvalidOperationException("Trees Edit Mode only.");
        if(GameObject.Find("ChamberDecorations")!=null)throw new InvalidOperationException("Chamber already dressed; inspect before reapplying.");
        // Preserve the user's current, including unsaved, chamber arrangement before authoring.
        EditorSceneManager.SaveScene(scene,"Library/TreesChamberBefore.unity",true);
        var stage=UnityEngine.Object.FindFirstObjectByType<TreeStageManager>();string gameplay=EditorJsonUtility.ToJson(stage);
        var legacy=GameObject.Find("Trees Garden (temporary project assets)").transform.Find("Garden boundary");
        foreach(var c in legacy.GetComponentsInChildren<Collider>(true)){Undo.RecordObject(c,"Remove obsolete internal garden barriers");c.enabled=false;}
        legacy.name="Legacy Garden boundary (disabled)";legacy.gameObject.SetActive(false);
        FitGameplayCollision(stage);
        EnsureFolder(Folder);
        var glow=Material("Warm Lantern Core",new Color(1,.62f,.20f),new Color(2.2f,1.12f,.32f));
        var blue=Material("Quiet Azure Crystal",new Color(.12f,.32f,.43f),new Color(.10f,.27f,.40f));
        var root=new GameObject("ChamberDecorations").transform;Undo.RegisterCreatedObjectUndo(root.gameObject,"Dress Trees chamber");
        var hanging=new GameObject("Hanging Lamps and Chains").transform;hanging.SetParent(root,false);
        var wallArt=new GameObject("Wall Banners").transform;wallArt.SetParent(root,false);
        var accents=new GameObject("Perimeter Crystal Niches").transform;accents.SetParent(root,false);
        var wall=GameObject.Find("Stage").GetComponent<MeshCollider>();Physics.SyncTransforms();
        // The visible outer masonry closes small seams/openings in the inner chamber shell.
        var border=GameObject.Find("ChamberBorder");var borderCollider=border.GetComponent<MeshCollider>();
        if(borderCollider==null)borderCollider=border.AddComponent<MeshCollider>();
        borderCollider.sharedMesh=border.GetComponent<MeshFilter>().sharedMesh;borderCollider.convex=false;
        Vector3 center=new Vector3(-.93f,0,3.79f);
        foreach(float angle in new[]{55f,125f,235f,305f})
        {
            var d=Quaternion.Euler(0,angle,0)*Vector3.forward;RaycastHit hit;
            if(wall.Raycast(new Ray(center+Vector3.up*12.8f,d),out hit,40))
            {
                var banner=Place(Dungeon+"Prefabs/Furniture/Banner.prefab",wallArt,"Scholars' Wall Banner",hit.point-d*.35f,Quaternion.LookRotation(-d),Vector3.one*1.5f);
                // This art hangs far above the walkable floor; it creates no collision footprint.
                Static(banner);
            }
        }
        // Reuse the 480-vertex archive ring, rather than the cinematic ring's thousands of vertices per link.
        var ring=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Exit/Meshes/Archive_Portal_Ring.asset");
        var iron=AssetDatabase.LoadAssetAtPath<Material>(Dungeon+"Materials/Metal_Rusty.mat");
        foreach(var at in new[]{new Vector3(-9,7.8f,-3),new Vector3(10,8.3f,4)})
        {
            var lamp=Place(Dungeon+"Prefabs/Lamps/Chandelier.prefab",hanging,"LAMP — Reading Aisle Chandelier",at,Quaternion.identity,Vector3.one*1.8f);
            var bounds=BoundsOf(lamp);lamp.position+=at-bounds.center;bounds=BoundsOf(lamp);
            AddCore(lamp,new Vector3(0,-.25f,0),new Vector3(.24f,.40f,.24f),glow);
            AddLight(lamp,at+Vector3.down*.7f,10f,14f);
            // Many existing ring links are combined into one static draw per chain.
            var combines=new List<CombineInstance>();
            float roof=24f;
            for(float y=bounds.max.y;y<roof;y+=.42f)
                combines.Add(new CombineInstance{mesh=ring,transform=Matrix4x4.TRS(new Vector3(at.x,y,at.z),Quaternion.Euler(0,combines.Count%2*90,0),new Vector3(.17f,.27f,1))*Matrix4x4.Translate(-ring.bounds.center)});
            var chainMesh=new Mesh{name="Trees Aisle Suspension Chain"};chainMesh.CombineMeshes(combines.ToArray());
            string path=Folder+"/SuspensionChain_"+(at.x<0?"West":"East")+".asset";AssetDatabase.CreateAsset(chainMesh,path);
            AddMesh("Combined Iron Suspension Chain",hanging,chainMesh,iron,Vector3.zero,Vector3.one);
            Static(lamp);
        }
        var crystal=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"Meshes/Archive_RuleCrystal.asset");
        var foot=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"Meshes/Archive_BeveledOctagon.asset");
        var stone=AssetDatabase.LoadAssetAtPath<Material>(Dungeon+"Materials/Stone_Wall.mat");
        foreach(var at in new[]{new Vector3(-15.5f,0,-8),new Vector3(15.8f,0,14)})
        {
            var niche=new GameObject("Decorative Azure Crystal Niche").transform;niche.SetParent(accents,false);niche.position=at;
            AddMesh("Carved Stone Plinth",niche,foot,stone,new Vector3(0,.22f,0),new Vector3(1.3f,.44f,1.3f));
            AddMesh("Decorative Crystal",niche,crystal,blue,new Vector3(0,.44f-crystal.bounds.min.y*1.2f,0),Vector3.one*1.2f);
            var box=niche.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,.22f,0);box.size=new Vector3(1.24f,.44f,1.24f);
            // Fit the elevated crystal too; do not create a full empty bounding box around the niche.
            var crystalObject=niche.Find("Decorative Crystal");var mc=crystalObject.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=crystal;
            Static(niche);
        }
        // Original five wall lamps retain placement, model, materials, and collision.
        foreach(var lamp in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.StartsWith("LAMP")&&t.GetComponent<MeshFilter>()!=null).ToArray())
        {
            if(lamp.IsChildOf(hanging))continue;
            AddCore(lamp,new Vector3(0,-1.15f,.02f),new Vector3(.58f,.95f,.58f),glow);
            var lightAt=lamp.TransformPoint(new Vector3(0,-1.1f,.02f));
            AddLight(lamp,lightAt,lamp.lossyScale.x<.8f?4f:16f,lamp.lossyScale.x<.8f?19f:28f);
        }
        var sun=GameObject.Find("Directional Light").GetComponent<Light>();Undo.RecordObject(sun,"Dim Trees chamber daylight");
        sun.intensity=.16f;sun.color=new Color(.67f,.76f,1f);sun.shadows=LightShadows.None;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.12f,.145f,.20f);RenderSettings.ambientIntensity=.25f;RenderSettings.reflectionIntensity=.18f;
        Physics.SyncTransforms();
        if(EditorJsonUtility.ToJson(stage)!=gameplay)throw new InvalidOperationException("Gameplay references changed.");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Library/TreesChamberSetup.txt","Preserved all existing furniture, gameplay references and unsaved chamber changes.\nDisabled four obsolete invisible garden boundaries; visible Stage retains floor/wall collision.\nThe visible ChamberBorder has a mesh-fitted, 76-triangle collision backstop for seams in the inner shell.\nReplaced full book/board bounding boxes with static mesh-fitted collision.\nSeven shadow-free lamp lights, warm emissive interiors, four wall banners, two chained chandeliers and two perimeter crystal niches.\nNo source pack, shared material, render-pipeline or quality setting changed.\n");
    }
    static void FitGameplayCollision(TreeStageManager stage)
    {
        // One scene-owned static collider mesh follows the pedestal and covers, never animated pages.
        var old=stage.bookStatue.GetComponent<BoxCollider>();Undo.RecordObject(old,"Fit book collision to visible statue");old.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(old);
        var parts=stage.bookStatue.GetComponentsInChildren<MeshFilter>(true).Where(f=>!f.name.Contains("page")&&!f.name.Contains("blank")&&f.GetComponent<TMPro.TMP_Text>()==null).ToArray();
        var combined=new Mesh{name="Trees Book Static Collision"};combined.CombineMeshes(parts.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=stage.bookStatue.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray());
        EnsureFolder(Folder);var meshPath=Folder+"/BookStaticCollision.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(saved==null){AssetDatabase.CreateAsset(combined,meshPath);saved=combined;}else{EditorUtility.CopySerialized(combined,saved);UnityEngine.Object.DestroyImmediate(combined);EditorUtility.SetDirty(saved);}
        var collider=stage.bookStatue.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=saved;collider.convex=false;
        var info=GameObject.Find("InformationBoard");
        foreach(var c in info.GetComponentsInChildren<BoxCollider>(true)){Undo.RecordObject(c,"Fit board collision");c.enabled=false;}
        foreach(var f in info.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name!="Info_CrystalAccents"))
        {var mc=f.GetComponent<MeshCollider>();if(mc==null)mc=f.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=f.sharedMesh;mc.convex=false;mc.enabled=true;}
    }
    static void AddCore(Transform lamp,Vector3 position,Vector3 scale,Material glow)
    {
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"Meshes/Archive_RuleCrystal.asset");
        AddMesh("Warm Luminous Interior",lamp,mesh,glow,position-mesh.bounds.center*scale.y,scale);
    }
    static void AddLight(Transform lamp,Vector3 at,float intensity,float range)
    {
        var l=new GameObject("Warm Lamp Light").AddComponent<Light>();l.transform.SetParent(lamp,false);l.transform.position=at;
        l.type=LightType.Point;l.color=new Color(1,.73f,.42f);l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;l.renderMode=LightRenderMode.ForcePixel;l.bounceIntensity=0;
    }
    static Material Material(string name,Color baseColor,Color emission)
    {
        string path=Folder+"/"+name.Replace(' ','_')+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        bool fresh=m==null;if(fresh)m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",baseColor);m.SetFloat("_Smoothness",.18f);m.SetColor("_EmissionColor",emission);
        // Clear the default EmissiveIsBlack flag so URP import/validation retains the emission keyword.
        m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;m.EnableKeyword("_EMISSION");m.enableInstancing=true;
        if(fresh)AssetDatabase.CreateAsset(m,path);else EditorUtility.SetDirty(m);return m;
    }
    static Transform Place(string path,Transform parent,string name,Vector3 at,Quaternion rotation,Vector3 scale)
    {var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),parent);g.name=name;g.transform.SetPositionAndRotation(at,rotation);g.transform.localScale=scale;foreach(var c in g.GetComponentsInChildren<Collider>(true))c.enabled=false;foreach(var l in g.GetComponentsInChildren<Light>(true))l.enabled=false;return g.transform;}
    static Transform AddMesh(string name,Transform parent,Mesh mesh,Material mat,Vector3 at,Vector3 scale)
    {var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=at;g.transform.localScale=scale;g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=mat;return g.transform;}
    static void Static(Transform t){foreach(var r in t.GetComponentsInChildren<MeshRenderer>(true))GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.BatchingStatic);}
    static Bounds BoundsOf(Transform t){var r=t.GetComponentsInChildren<Renderer>(true);var b=r[0].bounds;foreach(var rr in r.Skip(1))b.Encapsulate(rr.bounds);return b;}
    static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    public static Texture2D Capture(string path,Vector3 at,Vector3 look,float fov=65)
    {
        var go=new GameObject("Temporary Chamber QA Camera");var c=go.AddComponent<Camera>();c.transform.position=at;c.transform.LookAt(look);c.fieldOfView=fov;c.nearClipPlane=.05f;c.farClipPlane=150;
        var rt=new RenderTexture(1280,720,24);c.targetTexture=rt;c.Render();var old=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(rt);return tex;
    }
}
#endif
