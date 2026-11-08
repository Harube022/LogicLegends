using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LogicLegends.Students;
using LogicLegends.Graph;

// Opt-in, incremental collision audit of saved scenes. Does not run gameplay,
// change questions, save scenes, contact accounts or award rewards.
public static class StudentNpcSceneChecks
{
    static string Report="output/Students/SceneNavigationChecks.txt";
    static bool outdoorsOnly;
    static IEnumerator run;
    static Scene opened;
    static Scene original;
    static GameObject graphPreview;
    static StudentNpcCatalog settings;
    [MenuItem("Logic Legends/Students/Check saved scene walkable areas")]
    public static void Begin()
    {outdoorsOnly=false;Report="output/Students/SceneNavigationChecks.txt";Start();}
    [MenuItem("Logic Legends/Students/Check outdoor roaming and elevations")]
    public static void BeginOutdoor()
    {outdoorsOnly=true;Report="output/Students/OutdoorNavigationChecks.txt";Start();}
    static void Start()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||run!=null)throw new InvalidOperationException("Finish the current test first.");
        original=SceneManager.GetActiveScene();
        if(original.isDirty)
        {
            Directory.CreateDirectory("Assets/Tests/Students/Backups");
            EditorSceneManager.SaveScene(original,"Assets/Tests/Students/Backups/"+original.name+"_BeforeNpcSceneAudit.unity",true);
        }
        Directory.CreateDirectory("output/Students");File.WriteAllText(Report,"Saved-scene collision/navigation audit; no gameplay or account writes.\n");
        settings=UnityEngine.Object.Instantiate(Resources.Load<StudentNpcCatalog>("Students/StudentNpcCatalog"));
        settings.buildCellsPerFrame=12;
        run=Run();EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        try
        {
            double until=EditorApplication.timeSinceStartup+.015;
            do{if(!run.MoveNext()){File.AppendAllText(Report,"COMPLETE\n");Finish();return;}}while(EditorApplication.timeSinceStartup<until);
        }
        catch(Exception e){File.AppendAllText(Report,"FAILED "+e+"\n");Debug.LogException(e);Finish();}
    }
    static void Finish()
    {
        EditorApplication.update-=Tick;run=null;
        if(graphPreview!=null)UnityEngine.Object.DestroyImmediate(graphPreview);
        if(opened.IsValid()&&opened.isLoaded&&opened!=original)EditorSceneManager.CloseScene(opened,true);
        if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
        if(settings!=null)UnityEngine.Object.DestroyImmediate(settings);
    }
    static IEnumerable<Transform> Transforms(Scene scene)=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true));
    static IEnumerator Run()
    {
        var spawnNames=new[]{"","Truth_Table_SpawnPoint","Rules_Of_Inference_SpawnPoint","Proofs Player Spawn","Trial Player Spawn","Induction Spawn Point","PlayerSpawn","FR_SpawnPoint","Graph Spawn A","Trees Spawn"};
        for(int trial=1;trial<=10;trial++)
        {
            if(outdoorsOnly&&trial!=2&&trial!=3)continue;
            string path="Assets/Scenes/"+TrialCatalog.Get(trial).Scene+".unity";
            opened=original.path==path?original:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            Physics.SyncTransforms();yield return null;
            var transforms=Transforms(opened).ToArray();
            var source=opened.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).OfType<IStudentTrialSource>().FirstOrDefault();
            if(source==null)throw new Exception("Trial "+trial+" has no student action-state adapter.");
            var areas=trial==1?((QuizManager)source).StudentState().Areas:new[]{transforms.Single(t=>t.name==spawnNames[trial-1])};
            var keepOut=new List<Vector3>();var water=new List<Bounds>();
            StudentNpcDirector.CollectSafety(opened,areas,keepOut,water);
            if(trial==9)
            {
                var stage=(GraphStageManager)source;
                foreach(int challenge in new[]{0,2,3,4})
                {
                    graphPreview=new GameObject("Temporary student Graph collision audit");SceneManager.MoveGameObjectToScene(graphPreview,opened);
                    graphPreview.transform.position=stage.arena.position;graphPreview.transform.rotation=stage.arena.rotation;
                    var data=GraphGeometry.Layout(stage.catalog.variantSets[challenge].variants[0]);var vertices=new List<GraphVertex>();
                    foreach(var vertex in data.vertices){var view=((GameObject)PrefabUtility.InstantiatePrefab(stage.vertexPrefab.gameObject,graphPreview.transform)).GetComponent<GraphVertex>();view.Configure(vertex);vertices.Add(view);}
                    foreach(var edge in data.edges){var view=((GameObject)PrefabUtility.InstantiatePrefab(stage.edgePrefab.gameObject,graphPreview.transform)).GetComponent<GraphEdge>();view.Configure(edge,data.Vertex(edge.source).position,data.Vertex(edge.destination).position);}
                    Physics.SyncTransforms();
                    var map=new StudentWalkMap(settings,keepOut,water,vertices.ToArray(),opened);var build=map.Build(vertices[0].transform.position);
                    while(build.MoveNext())yield return null;
                    CheckMap(map,"Trial 9 challenge "+(challenge+1));
                    UnityEngine.Object.DestroyImmediate(graphPreview);yield return null;
                }
                File.AppendAllText(Report,"Trial 9 challenge 2: zero NPCs enforced by population policy.\n");
            }
            else foreach(var area in areas)
            {
                var map=StudentNpcDirector.CreateWalkMap(trial,settings,keepOut,water,null,opened);var build=map.Build(area.position);
                while(build.MoveNext())yield return null;
                CheckMap(map,"Trial "+trial+" / "+area.name);
            }
            if(opened!=original)EditorSceneManager.CloseScene(opened,true);
            opened=default;SceneManager.SetActiveScene(original);yield return null;
        }
    }
    static void CheckMap(StudentWalkMap map,string label)
    {
        if(map.Nodes.Count<8)throw new Exception(label+": insufficient verified floor nodes ("+map.Nodes.Count+").");
        int tested=0;
        var route=new List<int>();
        for(int i=0;i<map.Nodes.Count&&tested<12;i+=Mathf.Max(1,map.Nodes.Count/12))
        {
            int goal=map.Nodes.FindLastIndex(n=>n.Component==map.Nodes[i].Component);
            if(goal==i)continue;
            if(!map.Path(i,goal,route))throw new Exception(label+": disconnected route.");
            Vector3 previous=map.Nodes[i].Point;
            foreach(int step in route){if(!map.Segment(previous,map.Nodes[step].Point))throw new Exception(label+": unsafe path segment "+previous+" -> "+map.Nodes[step].Point);previous=map.Nodes[step].Point;}
            tested++;
        }
        File.AppendAllText(Report,label+": "+map.Nodes.Count+" safe nodes, "+tested+" complete paths checked, "+map.Nodes.Select(n=>n.Component).Distinct().Count()+" connected areas.\n");
        if(outdoorsOnly)
        {
            foreach(var component in map.Nodes.GroupBy(n=>n.Component).OrderByDescending(g=>g.Count()))
            {
                var bounds=new Bounds(component.First().Point,Vector3.zero);foreach(var node in component)bounds.Encapsulate(node.Point);
                File.AppendAllText(Report,"  Component "+component.Key+": "+component.Count()+" nodes, centre "+bounds.center+", size "+bounds.size+".\n");
            }
            int heightEdges=0;
            for(int i=0;i<map.Nodes.Count;i++)foreach(int j in map.Nodes[i].Links)
            {
                if(i>=j||Mathf.Abs(map.Nodes[i].Point.y-map.Nodes[j].Point.y)<.15f)continue;
                heightEdges++;var previous=map.Nodes[i].Point;
                int count=Mathf.CeilToInt(Vector3.Distance(previous,map.Nodes[j].Point)/.12f);
                for(int k=1;k<=count;k++)
                {
                    var desired=Vector3.Lerp(map.Nodes[i].Point,map.Nodes[j].Point,(float)k/count);
                    if(!map.GroundedStep(previous,desired,out var actual))
                    {File.AppendAllText(Report,"  BLOCKED elevation edge "+map.Nodes[i].Point+" -> "+map.Nodes[j].Point+" at "+previous+" -> "+desired+".\n");break;}
                    previous=actual;
                }
            }
            File.AppendAllText(Report,"  Elevation links sampled: "+heightEdges+".\n");
        }
        if(label.StartsWith("Trial 2 /",StringComparison.Ordinal))
        {
            var houses=opened.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TruthTableDoorInteraction>(true)).First();
            for(int door=0;door<6;door++)
            {
                var target=houses.GetTutorialDoor(door).bounds.center;
                float distance=Mathf.Sqrt(map.Nodes.Min(n=>StudentWalkMap.PlanarSquare(n.Point,target)));
                File.AppendAllText(Report,"  House "+(door+1)+" nearest safe roaming point: "+distance.ToString("F1")+" units.\n");
                if(distance>20)throw new Exception("NPC routes do not cover house "+(door+1)+" neighborhood.");
            }
        }
    }
}
