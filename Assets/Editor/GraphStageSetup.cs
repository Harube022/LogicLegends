using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Graph;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors only Graph; PRELIM is a copy source and is never saved by this tool.</summary>
public static class GraphStageSetup
{
    const string Folder = "Assets/Prefabs/Graph";
    [MenuItem("Tools/Logic Legends/Build Graph Stage")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        Scene target = SceneManager.GetSceneByPath("Assets/Scenes/Graph.unity");
        if (!target.isLoaded) target = EditorSceneManager.OpenScene("Assets/Scenes/Graph.unity", OpenSceneMode.Additive);
        if (target.GetRootGameObjects().Any(g => g.GetComponent<GraphStageManager>() != null))
            throw new InvalidOperationException("Graph is already configured. Edit its assigned catalog or prefabs; this command never overwrites existing Graph gameplay.");
        Scene source = SceneManager.GetSceneByPath("Assets/Scenes/PRELIM.unity");
        bool openedSource = !source.isLoaded;
        if (openedSource) source = EditorSceneManager.OpenScene("Assets/Scenes/PRELIM.unity", OpenSceneMode.Additive);
        var names = new[] { "Main Camera", "Directional Light", "CameraPivot", "GameInput", "EventSystem", "LevelSpawner", "LevelMenuManager", "MobileInputUI", "Canvas 1", "CM_ThirdPersonCam", "Inventory_Managers", "TeleportManager" };
        var originals = source.GetRootGameObjects().Where(g => names.Contains(g.name)).ToArray();
        // Clone as a single hierarchy so references between the copied roots and button callbacks remap together.
        var temporary = new GameObject("Graph copy source"); SceneManager.MoveGameObjectToScene(temporary, source);
        foreach (var go in originals) go.transform.SetParent(temporary.transform, true);
        GameObject copy;
        try { copy = UnityEngine.Object.Instantiate(temporary); }
        finally { foreach(var go in originals) go.transform.SetParent(null,true); UnityEngine.Object.DestroyImmediate(temporary); }
        SceneManager.MoveGameObjectToScene(copy, target);
        foreach (var old in target.GetRootGameObjects().Where(g=>g != copy && g.name == "Main Camera")) UnityEngine.Object.DestroyImmediate(old);
        foreach (Transform child in copy.transform.Cast<Transform>().ToArray()) child.SetParent(null,true);
        UnityEngine.Object.DestroyImmediate(copy);
        if (openedSource) EditorSceneManager.CloseScene(source,true);
        SceneManager.SetActiveScene(target);
        var roots = target.GetRootGameObjects();
        Func<string,GameObject> root = name => roots.First(g=>g.name==name);
        var manager=new GameObject("Graph Stage").AddComponent<GraphStageManager>();
        manager.ui=manager.gameObject.AddComponent<GraphUIController>();
        var canvas = GraphVisualSetup.ConnectCanvas(root("Canvas 1"),manager);
        var safe = canvas.transform.Find("SafeArea");
        var gameplay = canvas.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Gameplay_Interface");
        var pause = canvas.GetComponentsInChildren<Transform>(true).First(t=>t.name=="PausePanel");
        // Keep PRELIM's original anchors, dimensions, scales, sprites, labels and callbacks.
        root("Inventory_Managers").SetActive(false); // Truth-block inventory is not used by Graph.
        foreach (var t in canvas.GetComponentsInChildren<TMP_Text>(true)) if(t.font==null)t.font=StageUITheme.ReadingFont;
        root("CameraPivot").GetComponent<ThirdPersonCameraController>().enabled=false; // current Player uses the copied Cinemachine rig
        var camera=root("Main Camera").GetComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=Color.clear;
        var light=root("Directional Light").GetComponent<Light>();light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(50,-35,0);
        RenderSettings.ambientLight=new Color(.65f,.69f,.65f);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        EnsureFolder(Folder);
        manager.timer=manager.gameObject.AddComponent<GraphStageClock>();
        manager.gameOver=manager.gameObject.AddComponent<GameOverManager>();
        manager.completion=manager.gameObject.AddComponent<GraphCompletion>();
        manager.ui.host=(RectTransform)safe;manager.ui.gameplayControls=gameplay.gameObject;
        manager.ui.hudMap=SpriteAt(GraphHUDArtSetup.Folder+"GraphMapFrame.png");
        manager.ui.hudObjective=SpriteAt(GraphHUDArtSetup.Folder+"GraphObjectiveFrame.png");
        manager.ui.hudStatus=SpriteAt(GraphHUDArtSetup.Folder+"GraphStatusFrame.png");
        manager.ui.parchment=SpriteAt("Assets/Interface_Sprites/customizationsh_shop_interface/customization_scroll.png");
        manager.arena=new GameObject("Graph Arena").transform;
        manager.spawnPoint=new GameObject("Graph Spawn A").transform;manager.spawnPoint.position=new Vector3(0,.15f,0);
        manager.bookStatue=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Book_Statue.prefab"),target)).transform;
        manager.bookStatue.name="Graph Book Statue";manager.bookStatue.position=new Vector3(-1.7f,0,-.7f);
        // Fit the existing model to a small pedestal on the starting platform.
        var bounds=new Bounds(manager.bookStatue.position,Vector3.zero);
        foreach(var r in manager.bookStatue.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
        manager.bookStatue.localScale*=1.9f/Mathf.Max(.1f,bounds.size.y);
        GraphGeometry.EnsureBookCollision(manager.bookStatue);
        manager.correctSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Graph/Correct.wav");
        manager.wrongSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/WORLDBGM/DAMAGETAKEN1.mp3");
        Set(manager.gameOver,"gameOverSound",AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/WORLDBGM/GAMEOVER1.mp3"));
        Set(root("LevelSpawner").GetComponent<LevelSpawner>(),"spawnPoint",manager.spawnPoint);
        Set(root("LevelSpawner").GetComponent<LevelSpawner>(),"cmCamera",root("CM_ThirdPersonCam").GetComponent<Unity.Cinemachine.CinemachineCamera>());
        var follow=root("CM_ThirdPersonCam").GetComponent<Unity.Cinemachine.CinemachineThirdPersonFollow>();follow.CameraDistance=11;
        var fade=root("TeleportManager").GetComponentInChildren<CanvasGroup>(true);
        Set(root("TeleportManager").GetComponent<TeleportManager>(),"fadeCanvasGroup",fade);
        Set(root("TeleportManager").GetComponent<TeleportManager>(),"cameraScript",null);
        var groundMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/CHEATCODE/WOODTEXTURE/Materials/Wood3.mat");
        manager.vertexPrefab=MakeVertex(groundMat);
        manager.edgePrefab=MakeEdge(groundMat);
        manager.catalog=MakeCatalog();
        // Preview geometry is replaced from the catalog at runtime.
        foreach(var v in manager.catalog.challenges[0].vertices) {var view=(GraphVertex)PrefabUtility.InstantiatePrefab(manager.vertexPrefab,manager.arena);view.Configure(v);}
        foreach(var e in manager.catalog.challenges[0].edges) {var view=(GraphEdge)PrefabUtility.InstantiatePrefab(manager.edgePrefab,manager.arena);view.Configure(e,manager.catalog.challenges[0].Vertex(e.source).position,manager.catalog.challenges[0].Vertex(e.destination).position);}
        var build=EditorBuildSettings.scenes.ToList();if(!build.Any(s=>s.path==target.path))build.Add(new EditorBuildSettingsScene(target.path,true));EditorBuildSettings.scenes=build.ToArray();
        EditorSceneManager.MarkSceneDirty(target);EditorSceneManager.SaveScene(target);AssetDatabase.SaveAssets();
        GraphVisualSetup.Apply();
        Debug.Log("Graph scene authored with PRELIM controls, independent Graph clock, ten-challenge pool, and five random rounds.");
    }
    static GraphVertex MakeVertex(Material material)
    {
        var go=new GameObject("Graph Vertex");var v=go.AddComponent<GraphVertex>();
        var platform=Primitive("Vertex platform",PrimitiveType.Cylinder,go.transform,new Vector3(0,-.35f,0),new Vector3(7,.3f,7),material);
        UnityEngine.Object.DestroyImmediate(platform.GetComponent<Collider>());
        var trigger=new GameObject("Arrival volume",typeof(BoxCollider));trigger.transform.SetParent(go.transform,false);trigger.transform.localPosition=Vector3.up;
        var volume=trigger.GetComponent<BoxCollider>();volume.isTrigger=true;volume.size=new Vector3(4.5f,3,4.5f);v.arrivalVolume=volume;
        var label=new GameObject("Vertex label",typeof(TextMeshPro));label.transform.SetParent(go.transform,false);label.transform.localPosition=new Vector3(1.8f,.04f,-.3f);label.transform.localRotation=Quaternion.Euler(90,0,0);
        v.label=label.GetComponent<TextMeshPro>();v.label.font=StageUITheme.HeadingFont;v.label.fontSize=14;v.label.alignment=TextAlignmentOptions.Center;v.label.color=StageUITheme.Gold;v.label.outlineWidth=.18f;v.label.text="A";
        v.label.rectTransform.sizeDelta=new Vector2(3,2);
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/GraphVertex.prefab").GetComponent<GraphVertex>();UnityEngine.Object.DestroyImmediate(go);return prefab;
    }
    static GraphEdge MakeEdge(Material material)
    {
        var go=new GameObject("Graph Edge");var edge=go.AddComponent<GraphEdge>();
        edge.bridge=Primitive("Bridge",PrimitiveType.Cube,go.transform,new Vector3(0,-.3f,0),Vector3.one,material,false).transform;
        foreach(float side in new[]{-1f,1f})Primitive("Safety rail",PrimitiveType.Cube,edge.bridge,new Vector3(side*.51f,1,0),new Vector3(.045f,1.6f,1),material);
        edge.arrows=new GameObject("Direction arrows");edge.arrows.transform.SetParent(go.transform,false);
        foreach(float z in new[]{-1.8f,1.8f})
        {
            Primitive("Arrow shaft",PrimitiveType.Cube,edge.arrows.transform,new Vector3(0,.025f,z-.15f),new Vector3(.18f,.05f,1.3f),material,false);
            foreach(float side in new[]{-1f,1f}){var wing=Primitive("Arrow head",PrimitiveType.Cube,edge.arrows.transform,new Vector3(side*.28f,.025f,z+.25f),new Vector3(.16f,.05f,.85f),material,false);wing.transform.localRotation=Quaternion.Euler(0,-side*45,0);}
        }
        var gate=new GameObject("One-way gate",typeof(BoxCollider));gate.transform.SetParent(go.transform,false);var col=gate.GetComponent<BoxCollider>();col.size=new Vector3(5.8f,10,.25f);edge.reverseGate=col;
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/GraphEdge.prefab").GetComponent<GraphEdge>();UnityEngine.Object.DestroyImmediate(go);return prefab;
    }
    static GameObject Primitive(string name,PrimitiveType kind,Transform parent,Vector3 pos,Vector3 size,Material material,bool collision=true)
    {var go=GameObject.CreatePrimitive(kind);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
    public static GraphChallengeCatalog MakeCatalog()
    {
        const string path="Assets/Prefabs/Graph/GraphChallenges.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<GraphChallengeCatalog>(path);
        if(catalog!=null)
        {
            AddNumberedChallenges(catalog);
            return catalog;
        }
        catalog=ScriptableObject.CreateInstance<GraphChallengeCatalog>();
        catalog.challenges.Add(C("Vertices and edges",GraphChallengeType.VerticesAndEdges,"Travel from Vertex A to Vertex B using the connecting edge.","A vertex is a point in a graph. An edge connects two vertices.\n\nCross the bridge from A to B. A visit is recorded only after a complete crossing.","B",new[]{V("A",0,0),V("B",0,12)},new[]{E("A","B")}));
        catalog.challenges.Add(C("Vertex degree",GraphChallengeType.Degree,"What is the degree of Vertex C?","The degree of an undirected vertex counts its incident edges.\n\nLook at the bridges meeting C. Select a number and confirm. A self-loop would count twice.","C",new[]{V("A",0,0),V("B",-10,12),V("C",0,12),V("D",10,12),V("E",0,24)},new[]{E("B","C"),E("A","C"),E("C","D"),E("C","E")}));
        catalog.challenges.Add(C("Find a path",GraphChallengeType.Path,"Find a valid path from A to F.","A path follows edges from one vertex to another.\n\nA simple path does not repeat a vertex. More than one route may reach the destination. Choose your own route.","F",new[]{V("A",0,0),V("B",-10,8),V("C",10,8),V("D",-10,18),V("E",10,18),V("F",0,26)},new[]{E("A","B"),E("B","D"),E("D","F"),E("A","C"),E("C","E"),E("E","F")}));
        catalog.challenges.Add(C("Directed graph",GraphChallengeType.DirectedPath,"Reach D from A while following the arrows.","A directed edge has a direction.\n\nFollow the arrow from its source to its destination. A one-way bridge blocks entry from the wrong end. Choose any valid path to D.","D",new[]{V("A",0,0),V("B",-10,12),V("C",10,12),V("D",0,24)},new[]{E("A","B",true),E("B","D",true),E("A","C",true),E("C","D",true)}));
        catalog.challenges.Add(C("Complete a cycle",GraphChallengeType.Cycle,"Start at A, travel through a cycle, and return to A without repeating another vertex.","A cycle closes a route by returning to its starting vertex.\n\nVisit at least three distinct vertices. Only the starting vertex may appear twice: at the beginning and end. Either direction is allowed.","A",new[]{V("A",0,0),V("B",-10,12),V("C",0,24),V("D",10,12)},new[]{E("A","B"),E("B","C"),E("C","D"),E("D","A")}));
        AddNumberedChallenges(catalog);
        AssetDatabase.CreateAsset(catalog,path);return catalog;
    }
    [MenuItem("Tools/Logic Legends/Expand Graph Challenge Pool")]
    public static void ExpandPool()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var catalog=MakeCatalog();
        AssetDatabase.SaveAssetIfDirty(catalog);
        Debug.Log("Graph pool contains "+catalog.challenges.Count+" challenges; each run randomly selects five without repeats.");
    }
    static void AddNumberedChallenges(GraphChallengeCatalog catalog)
    {
        var additions=new[]
        {
            C("Numbered vertices",GraphChallengeType.VerticesAndEdges,"Travel from Vertex 1 to Vertex 2 using the connecting edge.","Vertices can use numbers as well as letters.\n\nThe labels identify vertices; the bridge is their edge. Cross from 1 to 2, then confirm.","2",new[]{V("1",0,0),V("2",0,12)},new[]{E("1","2")},"1"),
            C("Numbered degree",GraphChallengeType.Degree,"What is the degree of Vertex 3?","Count the edges meeting Vertex 3.\n\nNumeric labels do not change the degree rule: count incident edges, not the number written on the platform.","3",new[]{V("1",0,0),V("2",-10,12),V("3",0,12),V("4",10,12)},new[]{E("1","3"),E("2","3"),E("3","4")},"1","3"),
            C("Numbered path",GraphChallengeType.Path,"Find a valid simple path from 1 to 5.","A simple path follows connected edges without repeating a vertex.\n\nThis graph has routes of different lengths. Either branch can reach 5; a shortest path is not required.","5",new[]{V("1",0,0),V("2",-10,8),V("3",-10,18),V("4",10,12),V("5",0,26)},new[]{E("1","2"),E("2","3"),E("3","5"),E("1","4"),E("4","5")},"1"),
            C("Numbered directions",GraphChallengeType.DirectedPath,"Reach 5 from 1 while following the arrows.","A directed bridge can be crossed only in the arrow's direction.\n\nFollow a connected route from 1 to 5. Branches can contain different numbers of edges; either valid route is accepted.","5",new[]{V("1",0,0),V("2",-10,8),V("3",10,12),V("4",-10,18),V("5",0,26)},new[]{E("1","2",true),E("2","4",true),E("4","5",true),E("1","3",true),E("3","5",true)},"1"),
            C("Numbered cycle",GraphChallengeType.Cycle,"Start at 1, complete a cycle, and return to 1 without repeating another vertex.","A triangle forms a cycle with three distinct vertices.\n\nStart at 1, visit the other platforms, and return to 1. Either direction works. Only the starting vertex may repeat.","1",new[]{V("1",0,0),V("2",-10,16),V("3",10,16)},new[]{E("1","2"),E("2","3"),E("3","1")},"1")
        };
        foreach(var challenge in additions)
            if(!catalog.challenges.Any(c=>c.title==challenge.title))
            {catalog.challenges.Add(challenge);EditorUtility.SetDirty(catalog);}
    }
    static GraphVertexData V(string id,float x,float z)=>new GraphVertexData(id,x,z);
    static GraphEdgeData E(string a,string b,bool directed=false)=>new GraphEdgeData(a,b,directed);
    static GraphChallengeData C(string title,GraphChallengeType type,string objective,string lesson,string dest,GraphVertexData[] vertices,GraphEdgeData[] edges,string start="A",string degreeVertex="C")
    {return new GraphChallengeData{title=title,type=type,objective=objective,lesson=lesson,start=start,degreeVertex=degreeVertex,destination=dest,vertices=vertices.ToList(),edges=edges.ToList()};}
    static Sprite SpriteAt(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
    static void Set(UnityEngine.Object target,string property,UnityEngine.Object value){var so=new SerializedObject(target);so.FindProperty(property).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=path.Substring(0,path.LastIndexOf('/'));EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));}
}
