using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LogicLegends.Graph;

public static class GraphChamberSetup
{
    private const string Dungeon="Assets/CHEATCODE/DUNGEON/LowPolyDungeon/";
    [MenuItem("Tools/Logic Legends/Add Existing-Asset Graph Chamber")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<GraphStageManager>();
        if(stage==null)throw new InvalidOperationException("Open Graph first.");
        if(GameObject.Find("Graph Chamber")!=null)return;
        var room=new GameObject("Graph Chamber").transform;
        for(int x=-2;x<=2;x++)for(int z=-1;z<=4;z++)
            Prop("Tiles/FloorTIle.prefab",room,new Vector3(x*8,-4.5f,z*8),new Vector3(2,1,2),0);
        for(int x=-2;x<=2;x++)
        {
            Prop("Tiles/Dungeon_Big_Wall.prefab",room,new Vector3(x*8,-4.5f,29.1f),new Vector3(1,2,1),180);
            Prop("Tiles/Dungeon_Big_Wall.prefab",room,new Vector3(x*8,-4.5f,-3.1f),new Vector3(1,2,1),0);
        }
        for(int z=0;z<6;z++)foreach(float side in new[]{-1f,1f})
            Prop("Tiles/Dungeon_Big_Wall.prefab",room,new Vector3(side*14.1f,-4.5f,-7+z*8),new Vector3(1,2,1),side>0?-90:90);
        foreach(float x in new[]{-18f,18f})foreach(float z in new[]{-4f,12f,28f})
        {
            Prop("Tiles/Pillar.prefab",room,new Vector3(x,-4.5f,z),new Vector3(2,2,2),0);
            Prop("Lamps/Torch.prefab",room,new Vector3(x*.97f,2.4f,z-.9f),Vector3.one*4,0);
            var glow=new GameObject("Torch warm light",typeof(Light));glow.transform.SetParent(room,false);glow.transform.position=new Vector3(x*.9f,3,z-1);
            var light=glow.GetComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.66f,.3f);light.range=12;light.intensity=2;light.shadows=LightShadows.None;
        }
        foreach(float x in new[]{-12f,0f,12f})Prop("Furniture/Banner.prefab",room,new Vector3(x,6,34),Vector3.one,0);
        // The dungeon atlas requires its original mesh UVs. Temporary primitives use
        // the project's tiling wood and solid teal instead of sampling the atlas palette.
        var stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/CHEATCODE/WOODTEXTURE/Materials/Wood3.mat");
        var paper=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Sets/SetElement.mat");
        string vertexPath=AssetDatabase.GetAssetPath(stage.vertexPrefab);
        var vertex=PrefabUtility.LoadPrefabContents(vertexPath);
        vertex.transform.Find("Vertex platform").GetComponent<Renderer>().sharedMaterial=stone;
        PrefabUtility.SaveAsPrefabAsset(vertex,vertexPath);PrefabUtility.UnloadPrefabContents(vertex);
        string edgePath=AssetDatabase.GetAssetPath(stage.edgePrefab);
        var edge=PrefabUtility.LoadPrefabContents(edgePath);
        edge.transform.Find("Bridge").GetComponent<Renderer>().sharedMaterial=stone;
        foreach(var r in edge.transform.Find("Direction arrows").GetComponentsInChildren<Renderer>())r.sharedMaterial=paper;
        edge.GetComponent<GraphEdge>().reverseGate.GetComponent<BoxCollider>().size=new Vector3(5.8f,10,.25f);
        PrefabUtility.SaveAsPrefabAsset(edge,edgePath);PrefabUtility.UnloadPrefabContents(edge);
        RenderSettings.ambientLight=new Color(.44f,.51f,.58f);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);EditorSceneManager.SaveScene(stage.gameObject.scene);
    }
    private static void Prop(string path,Transform parent,Vector3 pos,Vector3 scale,float yaw)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Dungeon+"Prefabs/"+path);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
        go.transform.localPosition=pos;go.transform.localScale=scale;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
        go.isStatic=true;
        // Chamber scenery is below/around the graph. Only graph geometry determines valid traversal.
        foreach(var collider in go.GetComponentsInChildren<Collider>())collider.enabled=false;
    }
    public static void SetGameView(int width,int height)
    {
        var assembly=typeof(Editor).Assembly;
        var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var sizes=sizesType.BaseType.GetProperty("instance",flags).GetValue(null,null);
        var group=sizesType.GetProperty("currentGroup",flags).GetValue(sizes,null);
        var ctor=assembly.GetType("UnityEditor.GameViewSize").GetConstructors().First(c=>c.GetParameters().Length==4);
        var size=ctor.Invoke(new object[]{Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"),1),width,height,"Graph QA "+width+"x"+height});
        group.GetType().GetMethod("AddCustomSize",flags).Invoke(group,new[]{size});
        int count=(int)group.GetType().GetMethod("GetTotalCount",flags).Invoke(group,null);
        var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.Show();view.Focus();
        view.GetType().GetMethod("SizeSelectionCallback",flags).Invoke(view,new object[]{count-1,null});
    }
}
