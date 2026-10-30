using System;
using System.Linq;
using LogicLegends.Trees;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class TreesRestoreSetup
{
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit Mode only.");
        var stage=UnityEngine.Object.FindFirstObjectByType<TreeStageManager>();var scene=stage.gameObject.scene;
        var prelim=SceneManager.GetSceneByPath("Assets/Scenes/PRELIM.unity");bool opened=!prelim.isLoaded;
        if(opened)prelim=EditorSceneManager.OpenScene("Assets/Scenes/PRELIM.unity",OpenSceneMode.Additive);
        float height;
        try
        {
            var source=prelim.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="Book_Statue"&&t.GetComponentsInChildren<MeshRenderer>(true).Length>5);
            height=WorldBounds(source).size.y;
        }
        finally{if(opened)EditorSceneManager.CloseScene(prelim,true);}
        var book=stage.bookStatue;Undo.RecordObject(book,"Match PRELIM book size");
        var before=WorldBounds(book);book.localScale*=height/before.size.y;
        var after=WorldBounds(book);book.position+=Vector3.up*(before.min.y-after.min.y);book.position=new Vector3(-3.2f,book.position.y,book.position.z);
        var body=book.GetComponent<BoxCollider>();if(body==null)body=Undo.AddComponent<BoxCollider>(book.gameObject);
        var local=LocalBounds(book,book.GetComponentsInChildren<MeshRenderer>(true));body.center=local.center;body.size=local.size;body.enabled=true;body.isTrigger=false;book.gameObject.layer=0;
        PrefabUtility.RecordPrefabInstancePropertyModifications(book);PrefabUtility.RecordPrefabInstancePropertyModifications(body);
        var info=scene.GetRootGameObjects().First(g=>g.name=="InformationBoard");
        Collider(info.transform,"InformationBoard Solid Base",info.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Info_StoneBase").ToArray());
        Collider(info.transform,"InformationBoard Solid Frame",info.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Info_StoneFrame"||r.name=="Info_GoldFrame"||r.name=="Info_ParchmentSurface").ToArray());
        foreach(var node in stage.board.GetComponentsInChildren<TreeNode>(true))Undo.DestroyObjectImmediate(node.gameObject);
        foreach(var line in stage.board.GetComponentsInChildren<LineRenderer>(true))Undo.DestroyObjectImmediate(line.gameObject);
        var data=stage.catalog.challenges[0];var points=TreeDiagramLayout.Calculate(data);
        foreach(var n in data.nodes){var node=(TreeNode)PrefabUtility.InstantiatePrefab(stage.board.nodePrefab,stage.board.transform);node.id=n.id;node.label.text=n.id;node.transform.localPosition=points[n.id];node.transform.localScale*=.84f;node.ConfigureLabel();}
        foreach(var e in data.initial){var line=(LineRenderer)PrefabUtility.InstantiatePrefab(stage.board.edgePrefab,stage.board.transform);line.useWorldSpace=false;var d=(points[e.destination]-points[e.source]).normalized;line.SetPosition(0,points[e.source]+d*.46f+Vector3.back*.08f);line.SetPosition(1,points[e.destination]-d*.46f+Vector3.back*.08f);}
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Trees: PRELIM book height "+height+", solid board/base, restored workspace and spaced diagram saved.");
    }
    static void Collider(Transform root,string name,Renderer[] renderers)
    {
        var child=root.Find(name);if(child==null){child=new GameObject(name).transform;child.SetParent(root,false);}
        var b=LocalBounds(root,renderers);var c=child.GetComponent<BoxCollider>();if(c==null)c=child.gameObject.AddComponent<BoxCollider>();c.center=b.center;c.size=b.size;c.isTrigger=false;c.enabled=true;child.gameObject.layer=0;
    }
    static Bounds WorldBounds(Transform root){var rr=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.GetComponent<TMPro.TMP_Text>()==null).ToArray();var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;}
    static Bounds LocalBounds(Transform root,Renderer[] renderers)
    {
        bool first=true;var bounds=new Bounds();foreach(var r in renderers)for(int i=0;i<8;i++){var b=r.bounds;var p=root.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}return bounds;
    }
}
