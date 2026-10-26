#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Share only chamber art. Gameplay objects and their serialized references are preserved.</summary>
public static class ShareProofChamberDecorations
{
    public const string PrefabPath="Assets/Prefabs/Environment/SharedProofChamberDecorations.prefab";
    public const string FormalScene="Assets/Scenes/Formal vs. Informal Proofs.unity";
    public const string DirectScene="Assets/Scenes/Direct&Indirect.unity";
    const string Dungeon="Assets/CHEATCODE/DUNGEON/LowPolyDungeon/";
    const string Art="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/";
    const string Materials="Assets/Materials/ChamberDecorations";
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Apply to saved scenes in Edit Mode.");
        Folder("Assets/Prefabs/Environment");Folder(Materials);
        EditorSceneManager.OpenScene(FormalScene);
        var source=GameObject.Find("ChamberDecorations");
        if(source==null)throw new InvalidOperationException("Formal chamber decorations are missing.");
        var formal=UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>();
        string formalState=EditorJsonUtility.ToJson(formal);
        string existing=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source);
        GameObject prefab;
        if(string.IsNullOrEmpty(existing))
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)!=null)throw new InvalidOperationException("A shared asset already exists; inspect before replacing it.");
            prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(source,PrefabPath,InteractionMode.AutomatedAction);
        }
        else prefab=AssetDatabase.LoadAssetAtPath<GameObject>(existing);
        if(prefab==null||EditorJsonUtility.ToJson(formal)!=formalState)throw new InvalidOperationException("Prefab conversion changed a gameplay reference.");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EditorSceneManager.OpenScene(DirectScene);
        if(GameObject.Find("ChamberDecorations")!=null)throw new InvalidOperationException("Direct already contains a decoration parent; inspect it before adding another.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DirectIndirectProofStageController>();
        string directState=EditorJsonUtility.ToJson(stage);
        var room=GameObject.Find("Stage");var roomBounds=room.GetComponent<Renderer>().bounds;
        var center=new Vector3(roomBounds.center.x,roomBounds.min.y+1.42f,roomBounds.center.z);
        var parent=new GameObject("ChamberDecorations").transform;
        var shared=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
        shared.name="Shared Chamber Decorations";
        shared.transform.SetPositionAndRotation(center,Quaternion.Euler(0,180,0));shared.transform.localScale=new Vector3(1.25f,1,1.25f);
        Physics.SyncTransforms();
        var wall=room.GetComponent<MeshCollider>();
        foreach(Transform child in shared.transform)
        {
            if(child.name!="Wall Torch"&&child.name!="Hanging Banner")continue;
            Vector3 direction=child.position-center;direction.y=0;direction.Normalize();RaycastHit hit;
            if(!wall.Raycast(new Ray(new Vector3(center.x,child.position.y,center.z),direction),out hit,40))
                throw new InvalidOperationException("No chamber wall behind "+child.name);
            child.position=hit.point-direction*(child.name=="Wall Torch"?.35f:.28f);
            child.rotation=Quaternion.LookRotation(-direction);
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(shared.transform);
        var additions=new GameObject("DirectAndIndirectAdditions").transform;additions.SetParent(parent,false);
        var stone=AssetDatabase.LoadAssetAtPath<Material>(Dungeon+"Materials/Stone_Wall.mat");
        var crystalMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"Meshes/Archive_RuleCrystal.asset");
        var baseMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"Meshes/Archive_BeveledOctagon.asset");
        var violet=CrystalMaterial("Amethyst Accent",new Color(.40f,.16f,.72f),new Color(.20f,.06f,.4f));
        var blue=CrystalMaterial("Azure Accent",new Color(.10f,.42f,.64f),new Color(.04f,.22f,.36f));
        foreach(float x in new[]{-18.5f,18.5f})foreach(float z in new[]{-8f,8f})
        {
            var alcove=new GameObject((x<0?"West":"East")+(z<0?" North":" South")+" Crystal Alcove").transform;
            alcove.SetParent(additions,false);alcove.position=center+new Vector3(x,0,z);
            var foot=MeshObject("Carved Octagonal Foot",alcove,baseMesh,stone,Vector3.zero,new Vector3(2.4f,.7f,2.4f));
            var stand=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dungeon+"Prefabs/Clutter/CrystalBall_Stand.prefab"),alcove);
            stand.name="Iron Crystal Support";foreach(var c in stand.GetComponentsInChildren<Collider>(true))c.enabled=false;
            FitHeight(stand.transform,1.4f);Ground(stand.transform,alcove.position.y+.308f);
            var b=BoundsOf(stand.transform);
            var crystal=MeshObject("Decorative Faceted Crystal",alcove,crystalMesh,z<0?violet:blue,Vector3.zero,Vector3.one*1.7f);
            crystal.position=new Vector3(alcove.position.x,b.max.y-.08f-crystalMesh.bounds.min.y*1.7f,alcove.position.z);
            crystal.rotation=Quaternion.Euler(0,x<0?24:-24,0);
            var blocker=alcove.gameObject.AddComponent<BoxCollider>();
            blocker.center=new Vector3(0,1.75f,0);blocker.size=new Vector3(2.5f,3.5f,2.5f);
            var light=new GameObject("Soft Crystal Accent Light").AddComponent<Light>();light.transform.SetParent(alcove,false);
            light.transform.localPosition=new Vector3(0,2.1f,0);light.type=LightType.Point;light.color=z<0?new Color(.63f,.36f,1):new Color(.28f,.7f,1);
            light.intensity=1.8f;light.range=5;light.shadows=LightShadows.None;
        }
        // Additional stonework lives along the side walls, clear of the central proof stations.
        foreach(float x in new[]{-21.5f,21.5f})
        {
            var pillar=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dungeon+"Prefabs/Tiles/Pillar.prefab"),additions);
            pillar.name=x<0?"West Perimeter Pilaster":"East Perimeter Pilaster";
            foreach(var c in pillar.GetComponentsInChildren<Collider>(true))c.enabled=false;
            pillar.transform.localScale=new Vector3(1.4f,1.7f,1.4f);
            var b=BoundsOf(pillar.transform);pillar.transform.position+=center+new Vector3(x,0,0)-new Vector3(b.center.x,b.min.y,b.center.z);
            b=BoundsOf(pillar.transform);var blocker=pillar.AddComponent<BoxCollider>();
            blocker.center=pillar.transform.InverseTransformPoint(b.center);
            blocker.size=new Vector3(b.size.x/pillar.transform.lossyScale.x,b.size.y/pillar.transform.lossyScale.y,b.size.z/pillar.transform.lossyScale.z);
        }
        Physics.SyncTransforms();
        if(EditorJsonUtility.ToJson(stage)!=directState)throw new InvalidOperationException("Direct gameplay references changed.");
        if(parent.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)throw new InvalidOperationException("Decoration art must not carry gameplay behaviours.");
        foreach(var r in parent.GetComponentsInChildren<Renderer>(true))
            foreach(var m in r.sharedMaterials)if(m==null||!m.shader.isSupported)throw new InvalidOperationException("Unsupported decoration material.");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText("Library/SharedProofDecorationsSetup.txt","Shared prefab: "+AssetDatabase.GetAssetPath(prefab)+"\nFormal gameplay serialization preserved.\nDirect gameplay serialization preserved.\nDirect art root: ChamberDecorations\nShared instance and DirectAndIndirectAdditions are separate.\nRenderers: "+parent.GetComponentsInChildren<Renderer>(true).Length+"\nEnabled lights: "+parent.GetComponentsInChildren<Light>(true).Count(l=>l.enabled)+"\nEnabled colliders: "+parent.GetComponentsInChildren<Collider>(true).Count(c=>c.enabled)+"\n");
        AssetDatabase.SaveAssets();
    }
    static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;Folder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
    static Transform MeshObject(string name,Transform parent,Mesh mesh,Material material,Vector3 position,Vector3 scale)
    {var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;}
    static Material CrystalMaterial(string name,Color color,Color emission)
    {
        string path=Materials+"/"+name.Replace(' ','_')+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;
        m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.6f);m.SetFloat("_Metallic",.15f);
        m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);AssetDatabase.CreateAsset(m,path);return m;
    }
    public static Bounds BoundsOf(Transform t){var rs=t.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
    static void FitHeight(Transform t,float h){var b=BoundsOf(t);t.localScale*=h/b.size.y;}
    static void Ground(Transform t,float y){var b=BoundsOf(t);t.position=new Vector3(t.parent.position.x,t.position.y+y-b.min.y,t.parent.position.z);}
}
#endif
