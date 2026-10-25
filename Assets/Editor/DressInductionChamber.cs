#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LogicLegends.Induction;

/// <summary>Fits the shared proof-chamber art without changing any gameplay objects.</summary>
public static class DressInductionChamber
{
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.name!="Induction")
            throw new InvalidOperationException("Open Induction in Edit Mode first.");
        if(GameObject.Find("ChamberDecorations")!=null)throw new InvalidOperationException("Inspect the existing decorations before adding another group.");
        var stage=UnityEngine.Object.FindFirstObjectByType<InductionStageManager>();
        string state=EditorJsonUtility.ToJson(stage);
        var room=GameObject.Find("Stage");var walls=room.GetComponent<MeshCollider>();
        Vector3 center=new Vector3(room.GetComponent<Renderer>().bounds.center.x,28.03f,room.GetComponent<Renderer>().bounds.center.z);
        var parent=new GameObject("ChamberDecorations").transform;
        var shared=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ShareProofChamberDecorations.PrefabPath),parent);
        shared.name="Shared Proof Chamber Decorations";
        shared.transform.SetPositionAndRotation(center,Quaternion.identity);
        shared.transform.localScale=new Vector3(1.25f,1,1.25f);
        Physics.SyncTransforms();
        foreach(Transform child in shared.transform)
        {
            if(child.name=="Overhead Stone Rib"||child.name=="Suspension Chain"||child.name=="Hanging Chandelier")
            {
                child.position+=Vector3.up*4;PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                foreach(var lamp in child.GetComponentsInChildren<Light>(true).Where(l=>l.enabled))
                {lamp.range=20;PrefabUtility.RecordPrefabInstancePropertyModifications(lamp);}
            }
            if(child.name!="Wall Torch"&&child.name!="Hanging Banner")continue;
            Vector3 direction=child.position-center;direction.y=0;direction.Normalize();RaycastHit hit;
            var origin=new Vector3(center.x,child.position.y,center.z);
            bool found=walls.Raycast(new Ray(origin,direction),out hit,45);
            // Some chamber sides have open arches. Choose the nearest actual wall face.
            for(int angle=8;!found&&angle<=96;angle+=8)foreach(int sign in new[]{1,-1})
            {
                var candidate=Quaternion.Euler(0,angle*sign,0)*direction;
                if(walls.Raycast(new Ray(origin,candidate),out hit,45)){direction=candidate;found=true;break;}
            }
            if(!found)throw new InvalidOperationException("No wall behind "+child.name);
            child.position=hit.point-direction*(child.name=="Wall Torch"?.4f:.35f);
            child.rotation=Quaternion.LookRotation(-direction);
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(shared.transform);
        var additions=new GameObject("Induction Perimeter Accents").transform;additions.SetParent(parent,false);
        string art="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(art+"Meshes/Archive_RuleCrystal.asset");
        var footMesh=AssetDatabase.LoadAssetAtPath<Mesh>(art+"Meshes/Archive_BeveledOctagon.asset");
        var stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/CHEATCODE/DUNGEON/LowPolyDungeon/Materials/Stone_Wall.mat");
        var blue=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ChamberDecorations/Azure_Accent.mat");
        var violet=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ChamberDecorations/Amethyst_Accent.mat");
        // Keep every new floor object against the side perimeter, away from proof stations.
        foreach(float x in new[]{-22f,22f})foreach(float z in new[]{-6f,6f})
        {
            var accent=new GameObject((x<0?"West":"East")+(z<0?" Entry":" Rear")+" Crystal Niche").transform;
            accent.SetParent(additions,false);accent.position=center+new Vector3(x,0,z);
            AddMesh("Carved Stone Foot",accent,footMesh,stone,new Vector3(0,.35f,0),new Vector3(2,.7f,2));
            var crystal=AddMesh("Decorative Crystal",accent,mesh,z<0?blue:violet,Vector3.zero,Vector3.one*1.6f);
            crystal.localPosition=new Vector3(0,.7f-mesh.bounds.min.y*1.6f,0);
            var box=accent.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,1.2f,0);box.size=new Vector3(2,2.4f,2);
            var light=new GameObject("Soft Crystal Light").AddComponent<Light>();light.transform.SetParent(accent,false);
            light.transform.localPosition=new Vector3(0,2,0);light.type=LightType.Point;light.intensity=1.4f;light.range=5;
            light.color=z<0?new Color(.28f,.70f,1):new Color(.63f,.36f,1);light.shadows=LightShadows.None;
        }
        Physics.SyncTransforms();
        if(EditorJsonUtility.ToJson(stage)!=state)throw new InvalidOperationException("Gameplay serialization changed.");
        if(parent.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)throw new InvalidOperationException("Decorations must contain art only.");
        foreach(var r in parent.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)
            if(m==null||!m.shader.isSupported)throw new InvalidOperationException("Unsupported art material on "+r.name);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Library/InductionChamberSetup.txt","Shared Formal proof-chamber prefab preserved and connected.\nInduction gameplay serialization unchanged.\nAll new art under ChamberDecorations.\nRenderers: "+parent.GetComponentsInChildren<Renderer>(true).Length+"\nEnabled lights: "+parent.GetComponentsInChildren<Light>(true).Count(l=>l.enabled)+"\nEnabled colliders: "+parent.GetComponentsInChildren<Collider>(true).Count(c=>c.enabled)+"\n");
    }
    static Transform AddMesh(string name,Transform parent,Mesh mesh,Material mat,Vector3 position,Vector3 scale)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.transform.localPosition=position;go.transform.localScale=scale;
        go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go.transform;
    }
}
#endif
