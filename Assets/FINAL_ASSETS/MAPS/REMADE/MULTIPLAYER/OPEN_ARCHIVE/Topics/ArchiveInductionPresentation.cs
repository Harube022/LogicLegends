using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Formula scrolls and three proof regions display the existing shared induction arrangement.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveInductionPresentation:MonoBehaviour
    {
        public GameObject scrollPrefab,standPrefab,altarPrefab,stepSocketPrefab,linkPrefab;
        public Renderer[] progressMarks;
        public AudioClip takeSound,placeSound,correctSound,incorrectSound;
        public Mesh sparkMesh;public Material sparkMaterial;
        [Range(0,1)]public float feedbackVolume=.23f;
        public Color energyColor=new Color(.28f,.76f,.85f),focusColor=new Color(1,.88f,.52f),restColor=new Color(.07f,.22f,.28f);
        ArchiveTopicStation station;ArchiveQuestion question;AudioSource sound;ParticleSystem sparks;MaterialPropertyBlock properties;
        readonly Dictionary<int,Transform> tokens=new Dictionary<int,Transform>();readonly List<Renderer> socketRims=new List<Renderer>();
        int[] previousPlacements=Array.Empty<int>();string previousCarries="";bool observed;int answerSerial=-1,round=-1;
        public Transform[] TokenRoots=>tokens.OrderBy(t=>t.Key).Select(t=>t.Value).ToArray();
        public static string KindLabel(ArchiveQuestion q)=>q.title;
        // The existing reading font has no U+22EF glyph. Three periods preserve its ellipsis meaning.
        public static string FormulaText(string value)=>(value??"").Replace("\u22EF","...");
        public static string SlotLabel(int index,ArchiveQuestion q){string key=q.slots[index];return key.StartsWith("Base:")?"BASE · P(1)":key.StartsWith("Hypothesis:")?"HYPOTHESIS · P(k)":q.slots.Count(k=>k.StartsWith("Step:"))==1?"GOAL · P(k + 1)":"STEP "+key.Split(':')[1];}
        public static Vector3 TokenPosition(int item,ArchiveQuestion q){int index=Array.IndexOf(q.itemOrder,item),rows=(q.items.Length+1)/2;return new Vector3(index<rows?6:-6,.95f,rows<=1?0:Mathf.Lerp(2,-4,(index%rows)/(float)(rows-1)));}
        public static Vector3 SocketPosition(int index,ArchiveQuestion q){string key=q.slots[index];if(key.StartsWith("Base:"))return new Vector3(3.2f,.1f,3.2f);if(key.StartsWith("Hypothesis:"))return new Vector3(-3.2f,.1f,3.2f);return new Vector3(0,.1f,q.slots.Count(k=>k.StartsWith("Step:"))==1?-1.4f:1-(int.Parse(key.Split(':')[1])-1)*2.1f);}
        public static bool Filled(ArchiveStationState state,ArchiveQuestion q)=>state.phase==1&&state.carries.Count==0&&state.placements.Length==q.PlacementCount&&state.placements.All(p=>p>=0&&p<q.items.Length);
        public static string ItemText(int item,ArchiveQuestion q)=>item>=0&&item<q.details.Length?q.details[item]:"";
        public static string Arrangement(ArchiveQuestion q,ArchiveStationState state)=>string.Join("\n",q.slots.Select((key,i)=>SlotLabel(i,q)+": "+(i<state.placements.Length&&state.placements[i]>=0?q.items[state.placements[i]]:"empty")));
        void Awake(){
            station=GetComponent<ArchiveTopicStation>();properties=new MaterialPropertyBlock();sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=1;sound.rolloffMode=AudioRolloffMode.Linear;sound.minDistance=3;sound.maxDistance=36;
            if(sparkMesh==null||sparkMaterial==null)return;var go=new GameObject("Induction_Court_Feedback_Sparks");go.transform.SetParent(transform,false);sparks=go.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=sparks.main;main.playOnAwake=false;main.loop=false;main.maxParticles=24;main.startLifetime=.65f;main.startSpeed=1.8f;main.startSize=.08f;main.gravityModifier=.18f;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=sparks.emission;emission.enabled=false;var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=22;shape.radius=.2f;
            var r=go.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.mesh=sparkMesh;r.sharedMaterial=sparkMaterial;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
        }
        void Point(GameObject go,ArchivePointRole role,int index,string caption){var p=go.AddComponent<ArchiveTopicPoint>();p.station=station;p.role=role;p.index=index;p.caption=caption;station.points.Add(p);}
        public void Build(ArchiveTopicStation owner,ArchiveStationState state,ArchiveQuestion q){
            station=owner;question=q;tokens.Clear();socketRims.Clear();observed=false;previousPlacements=Array.Empty<int>();previousCarries="";
            owner.title.text="T6  MATHEMATICAL INDUCTION\nInduction Court";
            if(state.phase!=1&&!state.timerExpired){Refresh(state);return;}
            for(int i=0;i<q.items.Length;i++){
                var stand=Instantiate(standPrefab,owner.workRoot);stand.name="Formula_Stand_"+i;stand.transform.localPosition=TokenPosition(i,q)-Vector3.up*.95f;stand.GetComponentInChildren<TMP_Text>(true).text=q.items[i];
                var go=Instantiate(scrollPrefab,owner.workRoot);go.name="Formula_Scroll_"+i;go.GetComponentInChildren<TMP_Text>(true).text=q.items[i].Substring(q.items[i].LastIndexOf(' ')+1);
                tokens[i]=go.transform;if(state.phase==1)Point(go,ArchivePointRole.Item,i,"Take "+q.items[i]+" · Read for formula");
            }
            for(int i=0;i<q.slots.Length;i++){
                var go=Instantiate(q.slots[i].StartsWith("Step:")?stepSocketPrefab:altarPrefab,owner.workRoot);go.name="Induction_Answer_"+q.slots[i];go.transform.localPosition=SocketPosition(i,q);go.GetComponentInChildren<TMP_Text>(true).text=SlotLabel(i,q);
                socketRims.Add(go.transform.Find("Socket_Energy").GetComponent<Renderer>());if(state.phase==1)Point(go,ArchivePointRole.Slot,i,"Place at "+SlotLabel(i,q));
            }
            var steps=Enumerable.Range(0,q.slots.Length).Where(i=>q.slots[i].StartsWith("Step:")).ToArray();for(int i=1;i<steps.Length;i++){var link=Instantiate(linkPrefab,owner.workRoot);link.name="Inductive_Chain_Link_"+i;link.transform.localPosition=(SocketPosition(steps[i-1],q)+SocketPosition(steps[i],q))*.5f;}
            Refresh(state);
        }
        public void Refresh(ArchiveStationState state){
            if(question==null)return;
            station.board.text=state.phase==2?"INDUCTION COURT RESTORED\n5 / 5 team challenges complete":state.timerExpired?"TIME UP · FORMULAS KEPT\nReturn to the Book to retry.":state.phase==0?state.round==0?"THE INDUCTION COURT\nP(1) · P(k) · P(k + 1)\nRead the Book to begin.":"CHALLENGE "+state.round+" ACCEPTED\nRead the accepted proof in the Book.\nUse it for the next challenge.":"CHALLENGE "+(state.round+1)+" / 5\n"+KindLabel(question)+"\nHand / E · carry and place formulas";
            if(observed&&state.phase==1&&previousPlacements.Length==state.placements.Length){
                for(int i=0;i<state.placements.Length;i++)if(state.placements[i]>=0&&state.placements[i]!=previousPlacements[i]){Burst(SocketPosition(i,question)+Vector3.up*.3f,energyColor,5);Sound(placeSound,.55f);}
                string held=string.Join(";",state.carries.Select(c=>c.actor+":"+c.item));if(held!=previousCarries&&state.carries.Count>0)Sound(takeSound,.45f);
            }
            previousPlacements=state.placements.ToArray();previousCarries=string.Join(";",state.carries.Select(c=>c.actor+":"+c.item));observed=true;
        }
        void Update(){
            if(station==null||station.session==null||!station.session.Ready)return;var state=station.session.State.stations[5];
            if(round!=state.round){round=state.round;for(int i=0;i<progressMarks.Length;i++)Tint(progressMarks[i],i<state.round?energyColor:restColor);}
            if(answerSerial<0){answerSerial=state.answerSerial;return;}if(answerSerial==state.answerSerial)return;
            bool fresh=state.answerSerial>answerSerial&&ArchiveTopicSession.Clock-state.lastAnswerAt<2;answerSerial=state.answerSerial;
            if(fresh){bool correct=state.lastAnswerResult==1;Sound(correct?correctSound:incorrectSound,correct?1:.55f);Burst(new Vector3(0,.3f,-1.5f),correct?energyColor:new Color(.95f,.28f,.22f),correct?14:8);}
        }
        void LateUpdate(){
            if(question==null||station.session==null||!station.session.Ready)return;var state=station.session.State.stations[5];var focus=ArchiveTopicUI.Current==null?null:ArchiveTopicUI.Current.FocusedPoint;
            foreach(var pair in tokens){if(pair.Value==null)continue;var held=state.carries.FirstOrDefault(c=>c.item==pair.Key);
                if(held!=null){pair.Value.localScale=Vector3.one*.55f;var p=station.session.ActorPlayer(held.actor);if(p!=null){pair.Value.position=p.transform.position+Vector3.up*1.7f+p.transform.forward*.95f;pair.Value.rotation=Quaternion.Euler(0,p.transform.eulerAngles.y,0);}continue;}
                pair.Value.localScale=Vector3.one;int slot=Array.IndexOf(state.placements,pair.Key);pair.Value.localPosition=slot>=0?SocketPosition(slot,question)+Vector3.up*.18f:TokenPosition(pair.Key,question)+Vector3.up*Mathf.Sin(Time.time*1.4f+pair.Key)*.035f;pair.Value.localRotation=Quaternion.identity;
            }
            for(int i=0;i<socketRims.Count;i++)Tint(socketRims[i],focus!=null&&focus.station==station&&focus.role==ArchivePointRole.Slot&&focus.index==i?focusColor:state.placements.Length>i&&state.placements[i]>=0?energyColor:restColor);
        }
        bool Audible()=>Time.timeScale>0&&Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,transform.position)<36;
        void Tint(Renderer r,Color color){if(r==null)return;properties.SetColor("_BaseColor",color);r.SetPropertyBlock(properties);}
        void Sound(AudioClip clip,float volume){if(clip!=null&&Audible())sound.PlayOneShot(clip,feedbackVolume*volume);}
        void Burst(Vector3 local,Color color,int count){if(sparks==null||!Audible())return;var main=sparks.main;main.startColor=color;sparks.transform.position=transform.TransformPoint(local);sparks.Emit(count);}
    }
}
