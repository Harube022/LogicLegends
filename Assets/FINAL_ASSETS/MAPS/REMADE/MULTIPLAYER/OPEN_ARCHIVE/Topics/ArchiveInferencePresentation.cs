using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Checkpoint-driven T3 props and feedback. The shared session owns both inference stages.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveInferencePresentation:MonoBehaviour
    {
        public GameObject wordTabletPrefab,ruleCrystalPrefab,standPrefab,socketPrefab,altarPrefab;
        public Renderer[] progressStars;
        public AudioClip takeSound,placeSound,correctSound,incorrectSound;
        public Mesh sparkMesh;public Material sparkMaterial;
        [Range(0,1)]public float feedbackVolume=.23f;
        public Color energyColor=new Color(.36f,.77f,.94f),focusColor=new Color(1,.85f,.47f),restColor=new Color(.10f,.24f,.28f);
        ArchiveTopicStation station;ArchiveQuestion question;
        readonly Dictionary<int,Transform> tokens=new Dictionary<int,Transform>();
        readonly List<Renderer> socketRims=new List<Renderer>();
        int[] previousPlacements=Array.Empty<int>();string previousCarries="";bool observed;
        int answerSerial=-1,round=-1;
        AudioSource sound;ParticleSystem sparks;MaterialPropertyBlock properties;
        public Transform[] TokenRoots=>tokens.OrderBy(t=>t.Key).Select(t=>t.Value).ToArray();
        public static bool IsPremises(ArchiveQuestion q)=>q.activity==ArchiveActivity.Premises;
        public static Vector3 TokenPosition(int index,ArchiveQuestion q){
            if(q.itemOrder.Length==q.items.Length)index=Array.IndexOf(q.itemOrder,index);
            int rows=(q.items.Length+1)/2,row=index%rows;
            return new Vector3(index<rows?5.7f:-5.7f,1.05f,rows<=1?0:Mathf.Lerp(4.8f,-4.8f,row/(float)(rows-1)));
        }
        public static Vector3 SocketPosition(int index,ArchiveQuestion q){
            if(!IsPremises(q))return new Vector3(0,.10f,-1.8f);
            int columns=Math.Min(4,q.slots.Length),row=index/columns;
            return new Vector3(((columns-1)*.5f-index%columns)*2.25f,.10f,2.0f-row*3.0f);
        }
        public static bool Filled(ArchiveStationState s,ArchiveQuestion q)=>s.phase==1&&s.placements.Length==q.PlacementCount&&s.placements.All(p=>p>=0&&p<q.items.Length)&&s.carries.Count==0;
        public static string Premises(ArchiveQuestion q,ArchiveStationState state)=>!IsPremises(q)?q.prompt:Regex.Replace(q.prompt,@"\[(\d+)\]",m=>{
            int slot=int.Parse(m.Groups[1].Value)-1;
            int item=slot<state.placements.Length?state.placements[slot]:-1;
            return "["+(slot+1)+": "+(item>=0&&item<q.items.Length?q.items[item].ToUpperInvariant():"___")+"]";
        });
        void Awake(){
            station=GetComponent<ArchiveTopicStation>();properties=new MaterialPropertyBlock();
            sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=1;sound.rolloffMode=AudioRolloffMode.Linear;sound.minDistance=3;sound.maxDistance=36;
            if(sparkMesh==null||sparkMaterial==null)return;
            var go=new GameObject("Inference_Constellation_Sparks");go.transform.SetParent(transform,false);sparks=go.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=sparks.main;main.playOnAwake=false;main.loop=false;main.maxParticles=24;main.startLifetime=.65f;main.startSpeed=1.8f;main.startSize=.08f;main.gravityModifier=.18f;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=sparks.emission;emission.enabled=false;var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=22;shape.radius=.2f;
            var r=go.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.mesh=sparkMesh;r.sharedMaterial=sparkMaterial;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
        }
        ArchiveTopicPoint Point(GameObject go,ArchivePointRole role,int index,string caption){
            var p=go.AddComponent<ArchiveTopicPoint>();p.station=station;p.role=role;p.index=index;p.caption=caption;station.points.Add(p);return p;
        }
        public void Build(ArchiveTopicStation owner,ArchiveStationState state,ArchiveQuestion q){
            station=owner;question=q;tokens.Clear();socketRims.Clear();previousPlacements=Enumerable.Repeat(-1,q.PlacementCount).ToArray();previousCarries="";observed=false;
            owner.title.text="T3  RULES OF INFERENCE\nInference Constellation";
            if(state.phase!=1&&!state.timerExpired){Refresh(state);return;}
            bool words=IsPremises(q);
            for(int i=0;i<q.items.Length;i++){
                var stand=Instantiate(standPrefab,owner.workRoot);stand.name="Inference_Stand_"+i;stand.transform.localPosition=TokenPosition(i,q)-Vector3.up*1.05f;
                var caption=stand.GetComponentInChildren<TMP_Text>(true);caption.text=words?"WORD TABLET":q.items[i];
                var go=Instantiate(words?wordTabletPrefab:ruleCrystalPrefab,owner.workRoot);go.name=(words?"Word_Tablet_":"Rule_Crystal_")+i;
                go.GetComponentInChildren<TMP_Text>(true).text=words?q.items[i].ToUpperInvariant():owner.session.catalog.inferenceRules[i].abbreviation;
                tokens[i]=go.transform;
                if(state.phase==1)Point(go,ArchivePointRole.Item,i,"Take "+q.items[i]+(words?" word tablet":" Rule Crystal"));
            }
            for(int i=0;i<q.slots.Length;i++){
                var socket=Instantiate(words?socketPrefab:altarPrefab,owner.workRoot);socket.name=words?"Premise_Blank_"+(i+1):"Rule_Crystal_Altar";socket.transform.localPosition=SocketPosition(i,q);
                socket.GetComponentInChildren<TMP_Text>(true).text=words?"BLANK "+(i+1):"RULE ALTAR";
                socketRims.Add(socket.transform.Find("Socket_Energy").GetComponent<Renderer>());
                if(state.phase==1)Point(socket,ArchivePointRole.Slot,i,words?"Place word in blank "+(i+1):"Place Rule Crystal on the altar");
            }
            Refresh(state);
        }
        public void Refresh(ArchiveStationState state){
            if(question==null)return;
            bool words=IsPremises(question);
            station.board.text=state.phase==2?"CONSTELLATION RESTORED\n5 / 5 team challenges complete":state.timerExpired?"TIME UP · WORK KEPT\nReturn to the Book to retry\n"+(words?"Premise words":"Rule Crystal stage"):state.phase==0?state.round==0?"RESTORE THE CONSTELLATION\n1  Complete the premise blanks\n2  Choose the Rule Crystal\nRead the Book to begin.":"CHALLENGE "+state.round+" RESTORED\nConclusion recorded in the Book.\nRead it for the next challenge.":"CHALLENGE "+(state.round+1)+" / 5\n"+(words?"1  RESTORE THE PREMISES\nMatch word tablets to numbered blanks.":"2  FIND THE RULE CRYSTAL\nBring one crystal to the central altar.")+"\nRead the Book for full premises.";
            if(observed&&state.phase==1){
                for(int i=0;i<state.placements.Length;i++)if(state.placements[i]>=0&&state.placements[i]!=previousPlacements[i]){Burst(SocketPosition(i,question)+Vector3.up*.35f,energyColor,5);Sound(placeSound,.55f);}
                string held=string.Join(";",state.carries.Select(c=>c.actor+":"+c.item));if(held!=previousCarries&&state.carries.Count>0)Sound(takeSound,.45f);
            }
            previousPlacements=state.placements.ToArray();previousCarries=string.Join(";",state.carries.Select(c=>c.actor+":"+c.item));observed=true;
        }
        void Update(){
            if(station==null||station.session==null||!station.session.Ready)return;
            var state=station.session.State.stations[2];
            if(round!=state.round){round=state.round;for(int i=0;i<progressStars.Length;i++)Tint(progressStars[i],i<state.round?energyColor:restColor);}
            if(answerSerial<0){answerSerial=state.answerSerial;return;}
            if(answerSerial==state.answerSerial)return;
            bool fresh=state.answerSerial>answerSerial&&ArchiveTopicSession.Clock-state.lastAnswerAt<2;answerSerial=state.answerSerial;
            if(fresh){bool correct=state.lastAnswerResult==1;Sound(correct?correctSound:incorrectSound,correct?1:.55f);Burst(new Vector3(0,.3f,-1.8f),correct?energyColor:new Color(.95f,.28f,.22f),correct?14:8);}
        }
        void LateUpdate(){
            if(question==null||station.session==null||!station.session.Ready)return;
            var state=station.session.State.stations[2];var focus=ArchiveTopicUI.Current==null?null:ArchiveTopicUI.Current.FocusedPoint;
            foreach(var pair in tokens){
                if(pair.Value==null)continue;var held=state.carries.FirstOrDefault(c=>c.item==pair.Key);
                if(held!=null){pair.Value.localScale=Vector3.one*(IsPremises(question)?.55f:.45f);var player=station.session.ActorPlayer(held.actor);if(player!=null){pair.Value.position=player.transform.position+Vector3.up*1.7f+player.transform.forward*.95f;pair.Value.rotation=Quaternion.Euler(0,player.transform.eulerAngles.y,0);}continue;}
                pair.Value.localScale=Vector3.one;
                bool placed=state.placements.Contains(pair.Key);pair.Value.localPosition=station.CurrentItemPosition(pair.Key,question,state)+(placed?Vector3.zero:Vector3.up*Mathf.Sin(Time.time*1.4f+pair.Key)*.045f);
                pair.Value.localRotation=Quaternion.Euler(0,placed?0:Mathf.Sin(Time.time*.55f+pair.Key)*8,0);
            }
            for(int i=0;i<socketRims.Count;i++)Tint(socketRims[i],focus!=null&&focus.station==station&&focus.role==ArchivePointRole.Slot&&focus.index==i?focusColor:state.placements.Length>i&&state.placements[i]>=0?energyColor:restColor);
        }
        void Tint(Renderer r,Color color){if(r==null)return;properties.SetColor("_BaseColor",color);r.SetPropertyBlock(properties);}
        void Sound(AudioClip clip,float volume){if(clip!=null&&Time.timeScale>0&&Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,transform.position)<36)sound.PlayOneShot(clip,feedbackVolume*volume);}
        void Burst(Vector3 local,Color color,int count){if(sparks==null)return;var main=sparks.main;main.startColor=color;sparks.transform.position=transform.TransformPoint(local);sparks.Emit(count);}
    }
}
