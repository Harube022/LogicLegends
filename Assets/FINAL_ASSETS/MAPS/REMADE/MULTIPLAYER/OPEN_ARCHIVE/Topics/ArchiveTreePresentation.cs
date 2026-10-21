using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Trees;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Living-tree runes and growing branch conduits, driven only by the shared session.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveTreePresentation:MonoBehaviour
    {
        public GameObject nodePrefab,branchPrefab,numberPrefab;
        public Sprite nodeCircle;
        public Mesh arrowMesh,sparkMesh;
        public Material energyMaterial,sparkMaterial;
        public Renderer[] progressMarks;
        public AudioClip selectSound,connectSound,removeSound,correctSound,incorrectSound;
        [Range(0,1)]public float feedbackVolume=.23f;
        public static readonly Color Green=new Color(.20f,.88f,.53f),Gold=new Color(1,.73f,.16f),Target=new Color(.72f,.43f,1),Selected=new Color(.18f,.67f,1),Red=new Color(1,.19f,.14f),Quiet=new Color(.12f,.23f,.23f);
        readonly List<Transform> nodes=new List<Transform>(),numbers=new List<Transform>();readonly Dictionary<int,Transform> branches=new Dictionary<int,Transform>();readonly Dictionary<int,float> grows=new Dictionary<int,float>();
        ArchiveTopicStation station;ArchiveQuestion question;MaterialPropertyBlock properties;AudioSource source;ParticleSystem sparks;int receipt=-1,previousFirst=-1,previousSecond=-1;bool observed;
        public Transform[] NodeRoots=>nodes.ToArray();public int BranchCount=>branches.Count;
        public static Vector3 NumberPosition(int index)=>new Vector3(-7.2f,.08f,4-index*1.6f);
        public static IEnumerable<int> VisibleLinks(ArchiveQuestion q,ArchiveStationState s)=>s.phase==1||s.timerExpired||s.phase==2?s.links:q.tree.initial.Select(e=>ArchiveTreeRules.Encode(q,e));
        public static bool ReadyToCheck(ArchiveQuestion q,ArchiveStationState s)=>s.phase==1&&(q.tree.Editable?s.links.Count>0:ArchiveTreeRules.NumberQuestion(q)?s.flags>=0:s.selection>=0);
        void Awake(){station=GetComponent<ArchiveTopicStation>();properties=new MaterialPropertyBlock();source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.minDistance=3;source.maxDistance=36;source.rolloffMode=AudioRolloffMode.Linear;AudioVolumeSettings.Route(source,GameAudioChannel.SoundFX);
            if(sparkMesh==null||sparkMaterial==null)return;var go=new GameObject("Tree_Rune_Feedback");go.transform.SetParent(transform,false);sparks=go.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var m=sparks.main;m.playOnAwake=false;m.loop=false;m.maxParticles=24;m.startLifetime=.6f;m.startSpeed=1.3f;m.startSize=.065f;m.simulationSpace=ParticleSystemSimulationSpace.World;var e=sparks.emission;e.enabled=false;var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=.18f;shape.angle=22;var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=sparkMesh;renderer.sharedMaterial=sparkMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
        void Point(GameObject go,ArchivePointRole role,int index,string caption){var p=go.AddComponent<ArchiveTopicPoint>();p.station=station;p.role=role;p.index=index;p.caption=caption;station.points.Add(p);}
        public void Build(ArchiveTopicStation owner,ArchiveStationState s,ArchiveQuestion q){station=owner;question=q;nodes.Clear();numbers.Clear();branches.Clear();grows.Clear();observed=false;previousFirst=previousSecond=-1;
            for(int i=0;i<q.nodes.Length;i++){var go=Instantiate(nodePrefab,station.workRoot);go.name="Living_Tree_Node_"+q.nodeIds[i];go.transform.localPosition=ArchiveTreeRules.Node(q,i);go.GetComponentInChildren<TMP_Text>(true).text=q.nodeIds[i]+(q.nodeIds[i]==q.tree.root&&q.tree.type!=TreeChallengeType.Identify&&q.tree.type!=TreeChallengeType.Spanning?"\nROOT":"")+(q.nodeIds[i]==q.tree.target?"\nTARGET":"");nodes.Add(go.transform);Point(go,ArchivePointRole.Node,i,"Select tree node "+q.nodeIds[i]);}
            if(ArchiveTreeRules.NumberQuestion(q))for(int i=0;i<=5;i++){var go=Instantiate(numberPrefab,station.workRoot);go.name="Tree_Number_"+i;go.transform.localPosition=NumberPosition(i);go.GetComponentInChildren<TMP_Text>(true).text=i.ToString();numbers.Add(go.transform);if(s.phase==1)Point(go,ArchivePointRole.Select,100+i,"Choose "+i);}
            Refresh(s);}
        public void Refresh(ArchiveStationState s){if(question==null)return;var q=question;station.title.text="T10  TREES\nTree of Connections";bool all=station.session.State.stations.All(v=>v.phase==2);station.board.text=s.phase==2?(all?"THE OPEN ARCHIVE RESTORED\n10 / 10 team topics complete":"TREE OF CONNECTIONS RESTORED\n5 / 5 shared challenges"):s.timerExpired?"TIME UP · YOUR TREE IS KEPT\nRead the Book to retry.":s.phase==0?"THE TREE OF CONNECTIONS\nRead the Book to "+(s.round==0?"begin.":"continue."):"CHALLENGE "+(s.round+1)+" / 5\n"+q.title+"\n"+(q.tree.Editable?"Parent → child · Connect / Remove":ArchiveTreeRules.NumberQuestion(q)?"Count edges from the root.":"Read the objective · Select a node");
            var links=VisibleLinks(q,s).ToArray();foreach(int key in branches.Keys.Except(links).ToArray()){if(observed)Sound(removeSound,.45f);Destroy(branches[key].gameObject);branches.Remove(key);grows.Remove(key);}
            foreach(int link in links)if(!branches.ContainsKey(link)){var from=ArchiveTreeRules.Node(q,link/100);var to=ArchiveTreeRules.Node(q,link%100);var go=Instantiate(branchPrefab,station.workRoot);go.name="Living_Branch_"+q.nodeIds[link/100]+"_"+q.nodeIds[link%100];go.transform.localPosition=(from+to)*.5f;go.transform.localRotation=Quaternion.LookRotation(to-from);float length=Vector3.Distance(from,to);go.transform.localScale=new Vector3(1,1,observed?.001f:length);branches[link]=go.transform;grows[link]=observed?Time.time:-100;
                if(q.tree.Directed){var arrow=new GameObject("Parent_To_Child",typeof(MeshFilter),typeof(MeshRenderer)).transform;arrow.SetParent(go.transform,false);arrow.localPosition=new Vector3(0,.15f,.06f);arrow.localScale=new Vector3(1,.6f,1/Mathf.Max(.001f,length));arrow.GetComponent<MeshFilter>().sharedMesh=arrowMesh;arrow.GetComponent<MeshRenderer>().sharedMaterial=energyMaterial;arrow.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;}
                if(observed){Sound(connectSound,.55f);Burst((from+to)*.5f+Vector3.up*.3f,Green,5);}}
            observed=true;Colors(s);}
        Color NodeColor(int i,ArchiveStationState s){bool wrong=s.phase==1&&s.lastAnswerRound==s.round&&s.lastAnswerResult<0&&ArchiveTopicSession.Clock-s.lastAnswerAt<.7;if(wrong)return Red;if(s.phase==2)return Green;var own=station.session.TreeSelection(station.session.LocalActor);if(question.tree.Editable&&own!=null&&(i==own.treeFirst||i==own.treeSecond)||!question.tree.Editable&&i==s.selection)return Selected;if(question.nodeIds[i]==question.tree.target)return Target;return question.tree.type!=TreeChallengeType.Identify&&question.tree.type!=TreeChallengeType.Spanning&&question.nodeIds[i]==question.tree.root?Gold:Green;}
        void Colors(ArchiveStationState s){bool wrong=s.phase==1&&s.lastAnswerRound==s.round&&s.lastAnswerResult<0&&ArchiveTopicSession.Clock-s.lastAnswerAt<.7;foreach(var pair in branches){Tint(pair.Value.Find("Branch_Inlay").GetComponent<Renderer>(),wrong?Red:Green);var arrow=pair.Value.Find("Parent_To_Child");if(arrow!=null)Tint(arrow.GetComponent<Renderer>(),wrong?Red:Gold);}for(int i=0;i<nodes.Count;i++){var c=NodeColor(i,s);Tint(nodes[i].Find("Node_Inlay").GetComponent<Renderer>(),c);Tint(nodes[i].Find("Seed_Pivot/Seed").GetComponent<Renderer>(),c);}for(int i=0;i<numbers.Count;i++)Tint(numbers[i].Find("Choice_Inlay").GetComponent<Renderer>(),i==s.flags?(wrong?Red:Selected):Green);for(int i=0;i<progressMarks.Length;i++)Tint(progressMarks[i],i<s.round?Green:Quiet);}
        void Update(){if(station==null||station.session==null||!station.session.Ready||question==null)return;var s=station.session.State.stations[9];Colors(s);float time=(float)(ArchiveTopicSession.Clock%3600);for(int i=0;i<nodes.Count;i++){var pivot=nodes[i].Find("Seed_Pivot");pivot.localPosition=new Vector3(0,.64f+Mathf.Sin(time*1.4f+i)*.025f,0);pivot.localRotation=Quaternion.Euler(0,time*12+i*25,0);}foreach(var pair in branches){float length=Vector3.Distance(ArchiveTreeRules.Node(question,pair.Key/100),ArchiveTreeRules.Node(question,pair.Key%100));pair.Value.localScale=new Vector3(1,1,length*Mathf.SmoothStep(.001f,1,(Time.time-grows[pair.Key])/.35f));}
            var own=station.session.TreeSelection(station.session.LocalActor);int first=own==null?-1:own.treeFirst,second=own==null?-1:own.treeSecond;if(first>=0&&(first!=previousFirst||second!=previousSecond))Sound(selectSound,.35f);previousFirst=first;previousSecond=second;
            if(receipt<0){receipt=s.answerSerial;return;}if(receipt==s.answerSerial)return;bool fresh=s.answerSerial>receipt&&ArchiveTopicSession.Clock-s.lastAnswerAt<2;receipt=s.answerSerial;if(fresh){Sound(s.lastAnswerResult==1?correctSound:incorrectSound,s.lastAnswerResult==1?1:.6f);Burst(Vector3.up*.3f,s.lastAnswerResult==1?Green:Red,s.lastAnswerResult==1?16:8);}}
        void LateUpdate(){if(Camera.main==null)return;foreach(var root in nodes.Concat(numbers))root.GetComponentInChildren<TMP_Text>().transform.rotation=Camera.main.transform.rotation;}
        void Tint(Renderer r,Color c){properties.SetColor("_BaseColor",c);r.SetPropertyBlock(properties);}
        bool Audible()=>Time.timeScale>0&&Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,transform.position)<40;
        void Sound(AudioClip clip,float volume){if(clip!=null&&Audible())source.PlayOneShot(clip,feedbackVolume*volume);}
        void Burst(Vector3 local,Color c,int count){if(sparks==null||!Audible())return;var m=sparks.main;m.startColor=c;sparks.transform.position=transform.TransformPoint(local);sparks.Emit(count);}
    }
}
