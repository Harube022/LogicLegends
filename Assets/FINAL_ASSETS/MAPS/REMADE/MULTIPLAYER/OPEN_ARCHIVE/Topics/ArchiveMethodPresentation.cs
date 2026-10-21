using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Proof Forge visuals read the shared session; tablets and method selection have no local authority.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveMethodPresentation:MonoBehaviour
    {
        public GameObject tabletPrefab,standPrefab,socketPrefab,methodAltarPrefab;
        public Renderer[] progressMarks;
        public AudioClip takeSound,placeSound,correctSound,incorrectSound;
        public Mesh sparkMesh;public Material sparkMaterial;
        [Range(0,1)]public float feedbackVolume=.23f;
        public Color energyColor=new Color(1,.69f,.26f),focusColor=new Color(1,.9f,.58f),restColor=new Color(.21f,.14f,.07f);
        ArchiveTopicStation station;ArchiveQuestion question;AudioSource sound;ParticleSystem sparks;MaterialPropertyBlock properties;
        readonly Dictionary<int,Transform> tokens=new Dictionary<int,Transform>();readonly List<Renderer> socketRims=new List<Renderer>();readonly Dictionary<int,Renderer> methodRims=new Dictionary<int,Renderer>();
        int[] previousPlacements=Array.Empty<int>();string previousCarries="";bool observed;int answerSerial=-1,round=-1,previousMethod=-1;
        public Transform[] TokenRoots=>tokens.OrderBy(t=>t.Key).Select(t=>t.Value).ToArray();
        public static bool NeedsMethod(ArchiveQuestion q)=>q.trial.kind!=TrialProofKind.ArrangeSteps;
        public static string MethodName(int method)=>method==0?"Direct":method==1?"Contrapositive":method==2?"Contradiction":"Not selected";
        public static string KindLabel(ArchiveQuestion q)=>q.trial.kind==TrialProofKind.IdentifyMethod?"Identify the proof method":q.trial.kind==TrialProofKind.ChooseAndArrange?"Choose a method · build the proof":"Build a "+MethodName((int)q.trial.station).ToLowerInvariant()+" proof";
        public static Vector3 MethodPosition(int method)=>new Vector3((1-method)*4.2f,.1f,4.25f);
        public static Vector3 TokenPosition(int item,ArchiveQuestion q){int index=Array.IndexOf(q.itemOrder,item);return new Vector3(index<3?6:-6,.95f,1.4f-(index%3)*3);}
        public static Vector3 SocketPosition(int index,ArchiveQuestion q)=>new Vector3(0,.10f,1.4f-index*1.7f);
        public static bool Filled(ArchiveStationState state,ArchiveQuestion q)=>state.phase==1&&state.carries.Count==0&&(!NeedsMethod(q)||state.selection>=0&&state.selection<=2)&&state.placements.Length==q.PlacementCount&&state.placements.All(p=>p>=0&&p<q.items.Length);
        public static string ItemText(int item,ArchiveQuestion q)=>item>=0&&item<q.details.Length?q.details[item]:"";
        public static string Arrangement(ArchiveQuestion q,ArchiveStationState state)=>"METHOD: "+MethodName(NeedsMethod(q)?state.selection:(int)q.trial.station)+(q.slots.Length==0?"":"\n"+string.Join("\n",q.slots.Select((name,i)=>name+": "+(i<state.placements.Length&&state.placements[i]>=0?q.items[state.placements[i]]:"empty"))));
        // Adapt only solo interaction wording. All mathematical premises and tablet text stay in the original catalog.
        public static string Problem(ArchiveQuestion q)=>System.Text.RegularExpressions.Regex.Replace(q.prompt,@"\s*Interact with .*?(?:\.\s*|$)"," ")
            .Replace("Activate the matching altar, then confirm at the Book Statue.","")
            .Replace("Is it a direct or an indirect proof?","Which proof method does the argument use?")
            .Replace("Is this proof direct or indirect?","Which proof method does the argument use?")
            .Replace("on the active altar","in the numbered sockets").Replace("on the trial altar","in the numbered sockets").Trim();
        void Awake(){
            station=GetComponent<ArchiveTopicStation>();properties=new MaterialPropertyBlock();sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=1;sound.rolloffMode=AudioRolloffMode.Linear;sound.minDistance=3;sound.maxDistance=36;
            if(sparkMesh==null||sparkMaterial==null)return;var go=new GameObject("Proof_Forge_Feedback_Sparks");go.transform.SetParent(transform,false);sparks=go.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=sparks.main;main.playOnAwake=false;main.loop=false;main.maxParticles=24;main.startLifetime=.65f;main.startSpeed=1.8f;main.startSize=.08f;main.gravityModifier=.18f;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=sparks.emission;emission.enabled=false;var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=22;shape.radius=.2f;
            var r=go.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.mesh=sparkMesh;r.sharedMaterial=sparkMaterial;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
        }
        void Point(GameObject go,ArchivePointRole role,int index,string caption){var p=go.AddComponent<ArchiveTopicPoint>();p.station=station;p.role=role;p.index=index;p.caption=caption;station.points.Add(p);}
        public void Build(ArchiveTopicStation owner,ArchiveStationState state,ArchiveQuestion q){
            station=owner;question=q;tokens.Clear();socketRims.Clear();methodRims.Clear();observed=false;previousPlacements=Array.Empty<int>();previousCarries="";previousMethod=state.selection;
            owner.title.text="T5  DIRECT & INDIRECT PROOFS\nProof Forge";
            if(state.phase!=1&&!state.timerExpired){Refresh(state);return;}
            for(int i=0;i<q.items.Length;i++){
                var stand=Instantiate(standPrefab,owner.workRoot);stand.name="Tablet_Stand_"+i;stand.transform.localPosition=TokenPosition(i,q)-Vector3.up*.95f;stand.GetComponentInChildren<TMP_Text>(true).text=q.items[i];
                var go=Instantiate(tabletPrefab,owner.workRoot);go.name="Proof_Tablet_"+i;go.GetComponentInChildren<TMP_Text>(true).text=q.items[i].Substring(q.items[i].LastIndexOf(' ')+1);
                tokens[i]=go.transform;if(state.phase==1)Point(go,ArchivePointRole.Item,i,"Take "+q.items[i]+" · Read for text");
            }
            for(int i=0;i<q.slots.Length;i++){
                var go=Instantiate(socketPrefab,owner.workRoot);go.name="Numbered_Proof_Socket_"+i;go.transform.localPosition=SocketPosition(i,q);go.GetComponentInChildren<TMP_Text>(true).text="STEP "+(i+1);
                socketRims.Add(go.transform.Find("Socket_Energy").GetComponent<Renderer>());if(state.phase==1)Point(go,ArchivePointRole.Slot,i,"Place in "+q.slots[i]);
            }
            for(int method=0;method<3;method++){
                if(!NeedsMethod(q)&&method!=(int)q.trial.station)continue;
                var go=Instantiate(methodAltarPrefab,owner.workRoot);go.name="Proof_Method_Altar_"+method;go.transform.localPosition=NeedsMethod(q)?MethodPosition(method):new Vector3(0,.1f,4.25f);
                go.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Method_Name").text=MethodName(method).ToUpperInvariant();
                go.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Method_Sigil").text=method==0?"P → Q":method==1?"¬Q → ¬P":"P ∧ ¬Q → ⊥";
                methodRims[method]=go.transform.Find("Socket_Energy").GetComponent<Renderer>();if(NeedsMethod(q)&&state.phase==1)Point(go,ArchivePointRole.Select,method,"Choose "+MethodName(method));
            }
            Refresh(state);
        }
        public void Refresh(ArchiveStationState state){
            if(question==null)return;
            station.board.text=state.phase==2?"PROOF FORGE RESTORED\n5 / 5 team challenges complete":state.timerExpired?"TIME UP · WORK KEPT\nMethod and tablets preserved.\nReturn to the Book to retry.":state.phase==0?state.round==0?"THE PROOF FORGE\nDirect · Contrapositive · Contradiction\nRead the Book to begin.":"CHALLENGE "+state.round+" ACCEPTED\nRead the completed proof in the Book.\nUse it for the next challenge.":"CHALLENGE "+(state.round+1)+" / 5\n"+KindLabel(question)+"\n"+(NeedsMethod(question)?"METHOD: "+MethodName(state.selection):"Hand / E · take and place tablets");
            if(observed&&state.phase==1&&previousPlacements.Length==state.placements.Length){
                for(int i=0;i<state.placements.Length;i++)if(state.placements[i]>=0&&state.placements[i]!=previousPlacements[i]){Burst(SocketPosition(i,question)+Vector3.up*.3f,energyColor,5);Sound(placeSound,.55f);}
                string held=string.Join(";",state.carries.Select(c=>c.actor+":"+c.item));if(held!=previousCarries&&state.carries.Count>0)Sound(takeSound,.45f);
                if(state.selection>=0&&previousMethod!=state.selection){Burst(MethodPosition(state.selection)+Vector3.up*.3f,energyColor,5);Sound(placeSound,.55f);}
            }
            previousPlacements=state.placements.ToArray();previousCarries=string.Join(";",state.carries.Select(c=>c.actor+":"+c.item));previousMethod=state.selection;observed=true;
        }
        void Update(){
            if(station==null||station.session==null||!station.session.Ready)return;var state=station.session.State.stations[4];
            if(round!=state.round){round=state.round;for(int i=0;i<progressMarks.Length;i++)Tint(progressMarks[i],i<state.round?energyColor:restColor);}
            if(answerSerial<0){answerSerial=state.answerSerial;return;}if(answerSerial==state.answerSerial)return;
            bool fresh=state.answerSerial>answerSerial&&ArchiveTopicSession.Clock-state.lastAnswerAt<2;answerSerial=state.answerSerial;
            if(fresh){bool correct=state.lastAnswerResult==1;Sound(correct?correctSound:incorrectSound,correct?1:.55f);Burst(new Vector3(0,.3f,-1.5f),correct?energyColor:new Color(.95f,.28f,.22f),correct?14:8);}
        }
        void LateUpdate(){
            if(question==null||station.session==null||!station.session.Ready)return;var state=station.session.State.stations[4];var focus=ArchiveTopicUI.Current==null?null:ArchiveTopicUI.Current.FocusedPoint;
            foreach(var pair in tokens){if(pair.Value==null)continue;var held=state.carries.FirstOrDefault(c=>c.item==pair.Key);
                if(held!=null){pair.Value.localScale=Vector3.one*.55f;var p=station.session.ActorPlayer(held.actor);if(p!=null){pair.Value.position=p.transform.position+Vector3.up*1.7f+p.transform.forward*.95f;pair.Value.rotation=Quaternion.Euler(0,p.transform.eulerAngles.y,0);}continue;}
                pair.Value.localScale=Vector3.one;int slot=Array.IndexOf(state.placements,pair.Key);pair.Value.localPosition=slot>=0?SocketPosition(slot,question)+Vector3.up*.18f:TokenPosition(pair.Key,question)+Vector3.up*Mathf.Sin(Time.time*1.4f+pair.Key)*.035f;pair.Value.localRotation=Quaternion.Euler(0,0,0);
            }
            for(int i=0;i<socketRims.Count;i++)Tint(socketRims[i],focus!=null&&focus.station==station&&focus.role==ArchivePointRole.Slot&&focus.index==i?focusColor:state.placements.Length>i&&state.placements[i]>=0?energyColor:restColor);
            foreach(var pair in methodRims)Tint(pair.Value,!NeedsMethod(question)||state.selection==pair.Key?energyColor:focus!=null&&focus.station==station&&focus.role==ArchivePointRole.Select&&focus.index==pair.Key?focusColor:restColor);
        }
        bool Audible()=>Time.timeScale>0&&Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,transform.position)<36;
        void Tint(Renderer r,Color color){if(r==null)return;properties.SetColor("_BaseColor",color);r.SetPropertyBlock(properties);}
        void Sound(AudioClip clip,float volume){if(clip!=null&&Audible())sound.PlayOneShot(clip,feedbackVolume*volume);}
        void Burst(Vector3 local,Color color,int count){if(sparks==null||!Audible())return;var main=sparks.main;main.startColor=color;sparks.transform.position=transform.TransformPoint(local);sparks.Emit(count);}
    }
}
