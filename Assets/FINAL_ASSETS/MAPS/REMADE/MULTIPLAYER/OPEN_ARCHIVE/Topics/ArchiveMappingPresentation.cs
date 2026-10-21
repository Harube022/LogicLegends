using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Displays crystals, directed lasers and verdict seals from the existing shared session.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveMappingPresentation:MonoBehaviour
    {
        public static readonly string[] ClassificationNames={"Function Only","Not a Function","Relation Only"};
        public static readonly string[] PropertyNames={"Injective","Surjective","Bijective"};
        public GameObject emitterPrefab,receiverPrefab,answerPrefab;
        public Mesh arrowMesh,sparkMesh;
        public Material beamMaterial,arrowMaterial,sparkMaterial;
        public Renderer[] progressMarks;
        public AudioClip selectSound,connectSound,undoSound,correctSound,incorrectSound;
        [Range(0,1)] public float feedbackVolume=.23f;
        readonly List<Transform> ports=new List<Transform>(),answers=new List<Transform>();
        readonly Dictionary<int,LineRenderer> beams=new Dictionary<int,LineRenderer>();
        readonly Dictionary<int,Renderer> arrows=new Dictionary<int,Renderer>();
        readonly HashSet<int> previousLinks=new HashSet<int>();
        ArchiveTopicStation station;ArchiveQuestion question;AudioSource audioSource;ParticleSystem sparks;MaterialPropertyBlock properties;
        bool observed;int receipt=-1,previousInput=-1;
        static readonly Color Blue=new Color(.16f,.67f,1),Amber=new Color(1,.66f,.15f),Green=new Color(.22f,1,.48f),Red=new Color(1,.19f,.14f),Inactive=new Color(.11f,.23f,.27f);
        public int BeamCount=>beams.Count;
        public Transform[] PortRoots=>ports.ToArray();
        public Transform[] AnswerRoots=>answers.ToArray();
        public static Vector3 AnswerPosition(int index)=>new Vector3((index-1)*3.6f,.08f,-6.3f);
        public static bool ReadyToCheck(ArchiveQuestion q,ArchiveStationState s)=>s.phase==1&&(q.mapping.questionKind==MappingQuestionKind.BuildExactRelation?s.links.Count>(q.mapping.initialPairsLocked?q.mapping.initialPairs.Count:0):q.mapping.questionKind==MappingQuestionKind.ClassifyFunction?s.selection>=0&&s.selection<=2:s.flags!=0);
        public static bool Fixed(int link,ArchiveQuestion q)=>q.mapping.initialPairsLocked&&q.mapping.initialPairs.Any(p=>Array.IndexOf(q.inputLabels,p.domain)*100+Array.IndexOf(q.outputLabels,p.codomain)==link);
        public static string RangeText(ArchiveQuestion q,ArchiveStationState s)=>"Range = { "+string.Join(", ",q.outputLabels.Where((label,index)=>s.links.Any(link=>link%100==index)))+" }";
        public static bool NearRange(ArchiveTopicSession session)=>Enumerable.Range(0,session.Question(7).outputLabels.Length).Any(i=>session.Near(session.LocalActor,7,session.stations[7].OutputPosition(i),2.4f));
        void Awake(){
            station=GetComponent<ArchiveTopicStation>();properties=new MaterialPropertyBlock();audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=1;audioSource.rolloffMode=AudioRolloffMode.Linear;audioSource.minDistance=3;audioSource.maxDistance=36;
            if(sparkMesh==null||sparkMaterial==null)return;
            var go=new GameObject("Mapping_Conduit_Feedback");go.transform.SetParent(transform,false);sparks=go.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var m=sparks.main;m.playOnAwake=false;m.loop=false;m.maxParticles=24;m.startLifetime=.6f;m.startSpeed=1.6f;m.startSize=.07f;m.simulationSpace=ParticleSystemSimulationSpace.World;var e=sparks.emission;e.enabled=false;var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=20;shape.radius=.2f;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=sparkMesh;renderer.sharedMaterial=sparkMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void Point(GameObject go,ArchivePointRole role,int index,string caption){var point=go.AddComponent<ArchiveTopicPoint>();point.station=station;point.role=role;point.index=index;point.caption=caption;station.points.Add(point);}
        public void Build(ArchiveTopicStation owner,ArchiveStationState s,ArchiveQuestion q){
            station=owner;question=q;ports.Clear();answers.Clear();beams.Clear();arrows.Clear();previousLinks.Clear();observed=false;previousInput=-1;
            bool editing=s.phase==1&&q.mapping.questionKind==MappingQuestionKind.BuildExactRelation;
            for(int side=0;side<2;side++)for(int i=0;i<(side==0?q.inputLabels.Length:q.outputLabels.Length);i++){
                var go=Instantiate(side==0?emitterPrefab:receiverPrefab,owner.workRoot);go.name=(side==0?"Domain_Crystal_":"Codomain_Crystal_")+i;go.transform.localPosition=side==0?owner.InputPosition(i):owner.OutputPosition(i);go.GetComponentInChildren<TMP_Text>(true).text=(side==0?q.inputLabels[i]:q.outputLabels[i]);ports.Add(go.transform);
                if(editing)Point(go,side==0?ArchivePointRole.Input:ArchivePointRole.Output,i,side==0?"Select Domain "+q.inputLabels[i]:"Connect to Codomain "+q.outputLabels[i]);
            }
            if((s.phase==1||s.timerExpired)&&q.mapping.questionKind!=MappingQuestionKind.BuildExactRelation)for(int i=0;i<3;i++){
                var go=Instantiate(answerPrefab,owner.workRoot);go.name="Mapping_Answer_Seal_"+i;go.transform.localPosition=AnswerPosition(i);go.GetComponentInChildren<TMP_Text>(true).text=q.mapping.questionKind==MappingQuestionKind.ClassifyFunction?ClassificationNames[i]:PropertyNames[i];answers.Add(go.transform);
                if(s.phase==1)Point(go,ArchivePointRole.Select,q.mapping.questionKind==MappingQuestionKind.ClassifyFunction?i:new[]{2,4,8}[i],"Choose "+go.GetComponentInChildren<TMP_Text>(true).text);
            }
            Refresh(s);
        }
        public void Refresh(ArchiveStationState s){
            if(question==null)return;var q=question;station.title.text="T8  FUNCTIONS & RELATIONS\nMapping Conduit";
            station.board.text=s.phase==2?"MAPPING CONDUIT RESTORED\n5 / 5 shared challenges":s.timerExpired?"TIME UP · WORK KEPT\nRead the Book to retry.":s.phase==0?"THE MAPPING CONDUIT\nRead the Book to "+(s.round==0?"begin.":"continue."):"CHALLENGE "+(s.round+1)+" / 5\n"+q.title+"\nDomain { "+string.Join(", ",q.inputLabels)+" } → Codomain { "+string.Join(", ",q.outputLabels)+" }";
            bool display=s.phase==1||s.timerExpired||s.phase==2;
            foreach(int key in beams.Keys.Where(link=>!display||!s.links.Contains(link)).ToArray()){Destroy(beams[key].gameObject);Destroy(arrows[key].gameObject);beams.Remove(key);arrows.Remove(key);}
            if(display)foreach(int link in s.links){
                if(!beams.ContainsKey(link)){
                    var line=new GameObject("Directed_Mapping_"+link).AddComponent<LineRenderer>();line.transform.SetParent(station.workRoot,false);line.useWorldSpace=false;line.sharedMaterial=beamMaterial;line.positionCount=2;line.widthMultiplier=.055f;line.startColor=line.endColor=Color.white;line.numCapVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
                    var a=station.InputPosition(link/100)+Vector3.up*1.18f;var b=station.OutputPosition(link%100)+Vector3.up*1.18f;line.SetPositions(new[]{a,b});beams[link]=line;
                    var arrow=new GameObject("Mapping_Arrow_"+link,typeof(MeshFilter),typeof(MeshRenderer)).transform;arrow.SetParent(station.workRoot,false);arrow.localPosition=Vector3.Lerp(a,b,.68f);arrow.localRotation=Quaternion.LookRotation((b-a).normalized,Vector3.up);arrow.GetComponent<MeshFilter>().sharedMesh=arrowMesh;var renderer=arrow.GetComponent<MeshRenderer>();renderer.sharedMaterial=arrowMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;arrows[link]=renderer;
                }
                if(observed&&!previousLinks.Contains(link)){Sound(connectSound,.6f);Burst(station.OutputPosition(link%100)+Vector3.up,Blue,5);}
            }
            if(observed&&s.phase==1&&previousLinks.Count>s.links.Count)Sound(undoSound,.4f);
            var own=station.session.State.actors.FirstOrDefault(a=>a.actor==station.session.LocalActor);int selected=s.phase==1&&own!=null&&own.mappingRound==s.round?own.mappingInput:-1;
            if(observed&&selected>=0&&selected!=previousInput)Sound(selectSound,.4f);previousInput=selected;previousLinks.Clear();foreach(int link in s.links)previousLinks.Add(link);observed=true;
            UpdateColors(s);
        }
        void UpdateColors(ArchiveStationState s){
            var q=question;bool wrong=s.phase==1&&s.lastAnswerRound==s.round&&s.lastAnswerResult<0&&ArchiveTopicSession.Clock-s.lastAnswerAt<.65;
            foreach(var pair in beams){Color color=s.phase==2?Green:wrong?Red:Fixed(pair.Key,q)?Amber:Blue;Tint(pair.Value,color);Tint(arrows[pair.Key],color);}
            for(int i=0;i<ports.Count;i++){bool input=i<q.inputLabels.Length;int index=input?i:i-q.inputLabels.Length;bool used=!input&&s.links.Any(link=>link%100==index);var core=ports[i].Find("Crystal_Pivot/Crystal").GetComponent<Renderer>();Tint(core,s.phase==2?Green:wrong?Red:input&&index==previousInput?Amber:input||used?Blue:Inactive);}
            for(int i=0;i<answers.Count;i++){bool selected=q.mapping.questionKind==MappingQuestionKind.ClassifyFunction?s.selection==i:(s.flags&new[]{2,4,8}[i])!=0;Tint(answers[i].Find("Choice_Inlay").GetComponent<Renderer>(),selected?(wrong?Red:Blue):Inactive);}
            for(int i=0;i<progressMarks.Length;i++)Tint(progressMarks[i],i<s.round?Green:Inactive);
        }
        void Update(){
            if(station==null||station.session==null||!station.session.Ready||question==null)return;var s=station.session.State.stations[7];UpdateColors(s);
            for(int i=0;i<ports.Count;i++){var pivot=ports[i].Find("Crystal_Pivot");float time=(float)(ArchiveTopicSession.Clock%3600);pivot.localPosition=new Vector3(0,1.18f+Mathf.Sin(time*1.5f+i*.8f)*.05f,0);pivot.localRotation=Quaternion.Euler(0,time*18+i*30,0);}
            if(receipt<0){receipt=s.answerSerial;return;}if(receipt==s.answerSerial)return;bool fresh=s.answerSerial>receipt&&ArchiveTopicSession.Clock-s.lastAnswerAt<2;receipt=s.answerSerial;if(fresh){Sound(s.lastAnswerResult==1?correctSound:incorrectSound,s.lastAnswerResult==1?1:.6f);Burst(new Vector3(0,.4f,0),s.lastAnswerResult==1?Green:Red,s.lastAnswerResult==1?14:8);}
        }
        void LateUpdate(){if(Camera.main==null)return;foreach(var port in ports){var label=port.GetComponentInChildren<TMP_Text>();label.transform.rotation=Camera.main.transform.rotation;}foreach(var answer in answers){var label=answer.GetComponentInChildren<TMP_Text>();label.transform.rotation=Camera.main.transform.rotation;}}
        void Tint(Renderer renderer,Color color){if(renderer==null)return;properties.SetColor("_BaseColor",color);renderer.SetPropertyBlock(properties);}
        bool Audible()=>Time.timeScale>0&&Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,transform.position)<40;
        void Sound(AudioClip clip,float volume){if(clip!=null&&Audible())audioSource.PlayOneShot(clip,feedbackVolume*volume);}
        void Burst(Vector3 local,Color color,int count){if(sparks==null||!Audible())return;var main=sparks.main;main.startColor=color;sparks.transform.position=transform.TransformPoint(local);sparks.Emit(count);}
    }
}
