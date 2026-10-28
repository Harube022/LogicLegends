#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Existing art only. All additions stay outside the original playable arena.</summary>
public static class SetssssBorderSetup
{
    const string Archive="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Prefabs/";
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.name!="Setssss")throw new InvalidOperationException("Open Sets in Edit Mode.");
        if(GameObject.Find("StageBorderDecorations")!=null)throw new InvalidOperationException("Inspect existing border before adding another.");
        var stage=UnityEngine.Object.FindFirstObjectByType<SetssssChallengeOne>();var original=EditorJsonUtility.ToJson(stage);
        var arena=GameObject.Find("Stage").GetComponent<Renderer>().bounds;
        var root=new GameObject("StageBorderDecorations").transform;
        var ground=new GameObject("Distant Grass Apron",typeof(MeshFilter),typeof(MeshRenderer));ground.transform.SetParent(root,false);
        var grass=GameObject.Find("Logic_Garden_Terrain_Border").GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Top_LevelGrass_ArenaPlacement_12x9p4m");
        ground.GetComponent<MeshFilter>().sharedMesh=grass.sharedMesh;ground.GetComponent<Renderer>().sharedMaterials=grass.GetComponent<Renderer>().sharedMaterials;
        ground.transform.rotation=grass.transform.rotation;ground.transform.localScale=grass.transform.lossyScale;
        Fit(ground.transform,new Vector3(arena.center.x,-1,arena.center.z),new Vector3(650,1,750),true);
        var cliffs=new GameObject("Rocky Perimeter").transform;cliffs.SetParent(root,false);
        var trees=new GameObject("Surrounding Forest").transform;trees.SetParent(root,false);
        var random=new System.Random(70126);int index=0;
        // A closed rectangle sits beyond the full original floor, including difference areas.
        float left=arena.min.x-21,right=arena.max.x+21,front=arena.min.z-21,back=arena.max.z+21;
        for(int edge=0;edge<4;edge++)
        {
            Vector3 a,b,outward;
            if(edge==0){a=new Vector3(left,0,front);b=new Vector3(right,0,front);outward=Vector3.back;}
            else if(edge==1){a=new Vector3(right,0,front);b=new Vector3(right,0,back);outward=Vector3.right;}
            else if(edge==2){a=new Vector3(right,0,back);b=new Vector3(left,0,back);outward=Vector3.forward;}
            else {a=new Vector3(left,0,back);b=new Vector3(left,0,front);outward=Vector3.left;}
            int count=Mathf.CeilToInt(Vector3.Distance(a,b)/16);
            for(int i=0;i<count;i++)
            {
                Vector3 p=Vector3.Lerp(a,b,i/(float)count);
                var cliff=Instance(Archive+"Terrain/Cliff_Column_"+(index%2==0?"A":"B")+".prefab",cliffs,"Perimeter Cliff "+index);
                cliff.rotation=Quaternion.Euler(0,index*47,0);
                Fit(cliff,p+Vector3.down*2,new Vector3(23,18+(float)random.NextDouble()*12,22),false);
                foreach(int row in new[]{0,1})
                {
                    var tree=Instance(Archive+"Nature/Archive_Tree_0"+(index%5+1)+".prefab",trees,"Border Tree "+index+"_"+row);
                    tree.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                    var at=p+outward*(row==0?-3:9)+(b-a).normalized*(row*7);
                    Fit(tree,at+Vector3.up*(row==0?5:1),new Vector3(15,26+(float)random.NextDouble()*12,15),false);
                }
                index++;
            }
        }
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))foreach(var mat in r.sharedMaterials)
            if(mat==null||!mat.shader.isSupported)throw new InvalidOperationException("Unsupported border material: "+r.name);
        // Visual scenery never adds invisible support or interferes with the puzzle's physics.
        foreach(var c in root.GetComponentsInChildren<Collider>(true))c.enabled=false;
        if(EditorJsonUtility.ToJson(stage)!=original)throw new InvalidOperationException("Gameplay references changed.");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Library/SetssssBorderSetup.txt","Closed four-side scenery ring.\nCliffs: "+index+"\nTrees: "+index*2+"\nExisting grass mesh supplies distant terrain.\nAll new colliders disabled; original physics and gameplay references unchanged.\n");
    }
    static Transform Instance(string path,Transform parent,string name)
    {var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new InvalidOperationException(path);var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);g.name=name;return g.transform;}
    static void Fit(Transform t,Vector3 bottom,Vector3 size,bool flat)
    {
        var b=ShareProofChamberDecorations.BoundsOf(t);
        t.localScale=Vector3.Scale(t.localScale,new Vector3(size.x/b.size.x,flat?1:size.y/b.size.y,size.z/b.size.z));
        b=ShareProofChamberDecorations.BoundsOf(t);t.position+=bottom-new Vector3(b.center.x,b.min.y,b.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
    }
}
#endif
