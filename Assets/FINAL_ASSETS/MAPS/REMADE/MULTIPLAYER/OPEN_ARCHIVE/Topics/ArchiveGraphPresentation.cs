using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Graph;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Ground-level graph art and feedback. The shared session owns the actual route.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveGraphPresentation:MonoBehaviour
    {
        public GameObject vertexPrefab,pathPrefab,degreePrefab;
        public Mesh arrowMesh,sparkMesh;
        public Material energyMaterial,sparkMaterial;
        public Renderer[] progressMarks;
        public AudioClip arrivalSound,correctSound,incorrectSound;
        [Range(0,1)]public float feedbackVolume=.23f;
        public static readonly Color Available=new Color(1,.57f,.13f),Walked=new Color(.14f,.62f,1),StartColor=new Color(.22f,.91f,.44f),GoalColor=new Color(1,.78f,.17f),FocusColor=new Color(.75f,.37f,1),Invalid=new Color(1,.19f,.13f);
        readonly List<Transform> vertices=new List<Transform>(),paths=new List<Transform>(),degrees=new List<Transform>();
        readonly List<int> pathIndices=new List<int>();
        ArchiveTopicStation station;ArchiveQuestion question;MaterialPropertyBlock properties;AudioSource audioSource;ParticleSystem sparks;int receipt=-1,arrivals;
        public Transform[] VertexRoots=>vertices.ToArray();
        public Transform[] PathRoots=>paths.ToArray();
        public static Vector3 DegreePosition(int index)=>new Vector3(-7.2f,.08f,5.0f-index*1.45f);
        public static string Label(ArchiveQuestion q,int index)=>index<0||index>=q.nodeIds.Length?"?":ArchiveTopicQuestions.GraphLabel(q,q.nodeIds[index]);
        public static string RouteText(ArchiveQuestion q,ArchiveStationState s)=>s.route.Count==0?"No route recorded":string.Join(" → ",s.route.Select(i=>Label(q,i)));
        public static bool ReadyToCheck(ArchiveTopicSession session){var s=session.State.stations[8];return s.phase==1&&(session.Question(8).graph.type==GraphChallengeType.Degree?s.selection>=0:s.routeActor==session.LocalActor&&s.route.Count>1);}
        public static bool Traversed(ArchiveQuestion q,ArchiveStationState s,int edgeIndex){var e=q.graph.edges[edgeIndex];for(int i=1;i<s.route.Count;i++)if(e.Allows(q.nodeIds[s.route[i-1]],q.nodeIds[s.route[i]]))return true;return false;}
        void Awake(){station=GetComponent<ArchiveTopicStation>();properties=new MaterialPropertyBlock();audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=1;audioSource.rolloffMode=AudioRolloffMode.Linear;audioSource.minDistance=3;audioSource.maxDistance=36;
            if(sparkMesh==null||sparkMaterial==null)return;var go=new GameObject("Graph_Rune_Feedback");go.transform.SetParent(transform,false);sparks=go.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var m=sparks.main;m.playOnAwake=false;m.loop=false;m.maxParticles=24;m.startLifetime=.55f;m.startSpeed=1.4f;m.startSize=.06f;m.simulationSpace=ParticleSystemSimulationSpace.World;var e=sparks.emission;e.enabled=false;var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=22;shape.radius=.2f;var r=go.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.mesh=sparkMesh;r.sharedMaterial=sparkMaterial;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
        void Point(GameObject go,ArchivePointRole role,int index,string caption){var p=go.AddComponent<ArchiveTopicPoint>();p.station=station;p.role=role;p.index=index;p.caption=caption;station.points.Add(p);}
        public void Build(ArchiveTopicStation owner,ArchiveStationState s,ArchiveQuestion q){station=owner;question=q;vertices.Clear();paths.Clear();pathIndices.Clear();degrees.Clear();arrivals=s.route.Count;
            for(int i=0;i<q.graph.edges.Count;i++){var e=q.graph.edges[i];if(!e.traversalEnabled)continue;int a=Array.IndexOf(q.nodeIds,e.source),b=Array.IndexOf(q.nodeIds,e.destination);if(a<0||b<0)continue;var from=ArchiveGraphTraversal.Vertex(q,a);var to=ArchiveGraphTraversal.Vertex(q,b);var go=Instantiate(pathPrefab,station.workRoot);go.name="Stone_Edge_"+e.source+"_"+e.destination;go.transform.localPosition=(from+to)*.5f;go.transform.localRotation=Quaternion.LookRotation((to-from).normalized);go.transform.localScale=new Vector3(1,1,Vector3.Distance(from,to));paths.Add(go.transform);pathIndices.Add(i);
                if(e.directed){var arrow=new GameObject("One_Way_Arrow",typeof(MeshFilter),typeof(MeshRenderer)).transform;arrow.SetParent(go.transform,false);arrow.localPosition=new Vector3(0,.18f,.08f);arrow.localScale=new Vector3(1.3f,.7f,1.3f/go.transform.localScale.z);arrow.GetComponent<MeshFilter>().sharedMesh=arrowMesh;arrow.GetComponent<MeshRenderer>().sharedMaterial=energyMaterial;arrow.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;}}
            for(int i=0;i<q.nodes.Length;i++){var go=Instantiate(vertexPrefab,station.workRoot);go.name="Vertex_"+q.nodeIds[i];go.transform.localPosition=ArchiveGraphTraversal.Vertex(q,i);go.GetComponentInChildren<TMP_Text>(true).text=Label(q,i)+(q.graph.type==GraphChallengeType.Degree?(q.nodeIds[i]==q.graph.degreeVertex?"\nCOUNT EDGES":""):q.nodeIds[i]==q.graph.start?(q.graph.type==GraphChallengeType.Cycle?"\nSTART / FINISH":"\nSTART"):q.nodeIds[i]==q.graph.destination?"\nGOAL":"");vertices.Add(go.transform);Point(go,ArchivePointRole.Node,i,q.nodeIds[i]==q.graph.start&&q.graph.type!=GraphChallengeType.Degree?"Claim / retry route at START":"Read graph map");}
            if(q.graph.type==GraphChallengeType.Degree)for(int i=0;i<=6;i++){var go=Instantiate(degreePrefab,station.workRoot);go.name="Degree_Answer_"+i;go.transform.localPosition=DegreePosition(i);go.GetComponentInChildren<TMP_Text>(true).text=i.ToString();degrees.Add(go.transform);if(s.phase==1)Point(go,ArchivePointRole.Select,i,"Choose degree "+i);}
            Refresh(s);}
        public void Refresh(ArchiveStationState s){if(question==null)return;var q=question;station.title.text="T9  GRAPHS\nPath of Connections";station.board.text=s.phase==2?"PATH OF CONNECTIONS RESTORED\n5 / 5 shared challenges":s.timerExpired?"TIME UP · ROUTE KEPT\nRead the Book to retry.":s.phase==0?"THE PATH OF CONNECTIONS\nRead the Book to "+(s.round==0?"begin.":"continue."):"CHALLENGE "+(s.round+1)+" / 5\n"+q.title+"\n"+(q.graph.type==GraphChallengeType.Degree?"Count the purple vertex's edges.":"Green START → gold GOAL\nOrange available · blue traversed");
            if(s.route.Count>arrivals&&s.phase==1){Sound(arrivalSound,.45f);Burst(ArchiveGraphTraversal.Vertex(q,s.route.Last())+Vector3.up*.18f,Walked,5);}arrivals=s.route.Count;Colors(s);}
        void Colors(ArchiveStationState s){bool wrong=s.phase==1&&s.lastAnswerRound==s.round&&s.lastAnswerResult<0&&ArchiveTopicSession.Clock-s.lastAnswerAt<.65;for(int i=0;i<paths.Count;i++){Color c=s.phase==2?StartColor:wrong?Invalid:Traversed(question,s,pathIndices[i])?Walked:Available;Tint(paths[i].Find("Path_Inlay").GetComponent<Renderer>(),c);var arrow=paths[i].Find("One_Way_Arrow");if(arrow!=null)Tint(arrow.GetComponent<Renderer>(),c);}
            for(int i=0;i<vertices.Count;i++){string id=question.nodeIds[i];Color c=question.graph.type==GraphChallengeType.Degree?(id==question.graph.degreeVertex?FocusColor:Available):id==question.graph.start?StartColor:id==question.graph.destination?GoalColor:Available;if(s.route.Contains(i))c=s.continuous?Walked:Invalid;if(s.phase==2)c=StartColor;Tint(vertices[i].Find("Vertex_Inlay").GetComponent<Renderer>(),c);}
            for(int i=0;i<degrees.Count;i++)Tint(degrees[i].Find("Choice_Inlay").GetComponent<Renderer>(),s.selection==i?(wrong?Invalid:Walked):Available);for(int i=0;i<progressMarks.Length;i++)Tint(progressMarks[i],i<s.round?StartColor:new Color(.12f,.21f,.24f));}
        void Update(){if(station==null||station.session==null||!station.session.Ready||question==null)return;var s=station.session.State.stations[8];Colors(s);if(receipt<0){receipt=s.answerSerial;return;}if(receipt==s.answerSerial)return;bool fresh=s.answerSerial>receipt&&ArchiveTopicSession.Clock-s.lastAnswerAt<2;receipt=s.answerSerial;if(fresh){Sound(s.lastAnswerResult==1?correctSound:incorrectSound,s.lastAnswerResult==1?1:.6f);Burst(Vector3.up*.2f,s.lastAnswerResult==1?StartColor:Invalid,s.lastAnswerResult==1?14:8);}}
        void LateUpdate(){if(Camera.main==null)return;foreach(var root in vertices.Concat(degrees))root.GetComponentInChildren<TMP_Text>().transform.rotation=Camera.main.transform.rotation;}
        void Tint(Renderer r,Color c){properties.SetColor("_BaseColor",c);r.SetPropertyBlock(properties);}
        bool Audible()=>Time.timeScale>0&&Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,transform.position)<40;
        void Sound(AudioClip clip,float volume){if(clip!=null&&Audible())audioSource.PlayOneShot(clip,feedbackVolume*volume);}
        void Burst(Vector3 local,Color c,int count){if(sparks==null||!Audible())return;var m=sparks.main;m.startColor=c;sparks.transform.position=transform.TransformPoint(local);sparks.Emit(count);}
    }
}
