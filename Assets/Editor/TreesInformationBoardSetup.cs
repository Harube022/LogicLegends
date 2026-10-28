using System;
using System.Linq;
using LogicLegends.Trees;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TreesInformationBoardSetup
{
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit Mode only.");
        var stage=UnityEngine.Object.FindFirstObjectByType<TreeStageManager>();
        if(stage==null)throw new InvalidOperationException("Open Trees first.");
        var model=GameObject.Find("InformationBoard");
        if(model==null)model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/MAPS/REMADE/Induction/InductionBoards/Prefabs/InformationBoard.prefab"));
        Undo.RecordObject(model.transform,"Fit Trees InformationBoard");
        model.transform.SetPositionAndRotation(new Vector3(0,0,14),Quaternion.Euler(0,180,0));model.transform.localScale=new Vector3(7.5f,4,5);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
        var parchment=model.GetComponentsInChildren<Renderer>().First(r=>r.name=="Info_ParchmentSurface").bounds;
        Undo.RecordObject(stage.board.transform,"Reconnect tree diagram");
        stage.board.transform.SetPositionAndRotation(new Vector3(parchment.center.x,parchment.center.y,parchment.min.z-.04f),Quaternion.identity);
        stage.board.transform.localScale=Vector3.one;stage.boardFocus.board=stage.board.transform;
        // Only obsolete visual models are removed. Keep the original controller and its references.
        foreach(var name in new[]{"Original wood tree board","Tree content surface"})
        {var old=GameObject.Find(name);if(old!=null)Undo.DestroyObjectImmediate(old);}
        foreach(var node in stage.board.GetComponentsInChildren<TreeNode>(true))Undo.DestroyObjectImmediate(node.gameObject);
        foreach(var line in stage.board.GetComponentsInChildren<LineRenderer>(true))Undo.DestroyObjectImmediate(line.gameObject);
        var q=stage.catalog.challenges[0];
        foreach(var n in q.nodes)
        {var node=(TreeNode)PrefabUtility.InstantiatePrefab(stage.board.nodePrefab,stage.board.transform);node.id=n.id;node.label.text=n.id;node.transform.localPosition=TreeBoardController.Position(n);node.transform.localScale*=.58f;}
        foreach(var e in q.initial)
        {var line=(LineRenderer)PrefabUtility.InstantiatePrefab(stage.board.edgePrefab,stage.board.transform);line.useWorldSpace=false;line.SetPosition(0,TreeBoardController.Position(q.nodes.First(n=>n.id==e.source))+Vector3.back*.08f);line.SetPosition(1,TreeBoardController.Position(q.nodes.First(n=>n.id==e.destination))+Vector3.back*.08f);}
        stage.boardFocus.focusCamera.transform.SetPositionAndRotation(stage.board.transform.position-Vector3.forward*5,Quaternion.identity);
        var lens=stage.boardFocus.focusCamera.Lens;lens.OrthographicSize=4.8f;lens.NearClipPlane=.05f;lens.FarClipPlane=150;stage.boardFocus.focusCamera.Lens=lens;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
}
