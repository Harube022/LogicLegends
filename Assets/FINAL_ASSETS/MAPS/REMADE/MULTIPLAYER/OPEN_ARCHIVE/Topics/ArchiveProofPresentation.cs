using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Physical manuscripts and sockets display the existing shared proof arrangement.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveProofPresentation:MonoBehaviour
    {
        public GameObject scrollPrefab,ruleSealPrefab,standPrefab,socketPrefab,classificationAltarPrefab;
        public Renderer[] progressMarks;
        public AudioClip takeSound,placeSound,correctSound,incorrectSound;
        public Mesh sparkMesh;public Material sparkMaterial;
        [Range(0,1)]public float feedbackVolume=.23f;
        public Color energyColor=new Color(.33f,.78f,.68f),focusColor=new Color(1,.85f,.47f),restColor=new Color(.08f,.25f,.24f);
        ArchiveTopicStation station;ArchiveQuestion question;AudioSource sound;ParticleSystem sparks;MaterialPropertyBlock properties;
        readonly Dictionary<int,Transform> tokens=new Dictionary<int,Transform>();readonly List<Renderer> socketRims=new List<Renderer>();
        int[] previousPlacements=Array.Empty<int>();string previousCarries="";bool observed;int answerSerial=-1,round=-1;
        public Transform[] TokenRoots=>tokens.OrderBy(t=>t.Key).Select(t=>t.Value).ToArray();
        public static bool IsRule(int item,ArchiveQuestion q)=>q.proof.kind==ProofChallengeKind.MissingRule||q.proof.kind==ProofChallengeKind.Formalize&&item>=q.statementCount;
        public static string KindLabel(ArchiveQuestion q)=>q.proof.kind==ProofChallengeKind.SortScrolls?"Classify proof styles":q.proof.kind==ProofChallengeKind.OrderSteps?"Order the proof steps":q.proof.kind==ProofChallengeKind.MissingRule?"Restore the justification":"Pair statements and rules";
        public static string ItemText(int item,ArchiveQuestion q)=>item>=0&&item<q.details.Length?q.details[item]:item>=0&&item<q.items.Length?q.items[item]:"";
        public static Vector3 TokenPosition(int item,ArchiveQuestion q){
            int[] order=q.itemOrder.Length==q.items.Length?q.itemOrder:Enumerable.Range(0,q.items.Length).ToArray();
            if(q.proof.kind==ProofChallengeKind.Formalize){
                bool rule=IsRule(item,q);int[] bank=order.Where(i=>IsRule(i,q)==rule).ToArray();int row=Array.IndexOf(bank,item);
                return new Vector3(rule?-6:6,.95f,Mathf.Lerp(4.5f,-4.5f,row/(float)Math.Max(1,bank.Length-1)));
            }
            int index=Array.IndexOf(order,item),rows=(q.items.Length+1)/2;
            return new Vector3(index<rows?6:-6,.95f,rows<=1?2.6f:Mathf.Lerp(4.5f,-4.5f,(index%rows)/(float)(rows-1)));
        }
        public static Vector3 SocketPosition(int index,ArchiveQuestion q){
            if(q.regionPlacement)return new Vector3(index==0?2.8f:-2.8f,.10f,-1.5f);
            if(q.proof.kind==ProofChallengeKind.MissingRule)return new Vector3(0,.10f,-1.5f);
            int count=q.statementCount,row=index%count;float x=q.proof.kind==ProofChallengeKind.Formalize?(index<count?1.6f:-1.6f):0;
            return new Vector3(x,.10f,Mathf.Lerp(4.5f,-4.5f,row/(float)Math.Max(1,count-1)));
        }
        public static Vector3 CurrentPosition(int item,ArchiveQuestion q,ArchiveStationState state){
            int slot=q.regionPlacement?(item<state.placements.Length?state.placements[item]:-1):Array.IndexOf(state.placements,item);
            if(slot<0)return TokenPosition(item,q);var point=SocketPosition(slot,q)+Vector3.up*.18f;
            if(q.regionPlacement){int order=state.placements.Take(item).Count(v=>v==slot);point+=new Vector3((order%2-.5f)*1.2f,0,(order/2-.5f)*1.0f);}
            return point;
        }
        public static bool Filled(ArchiveStationState state,ArchiveQuestion q)=>state.phase==1&&state.carries.Count==0&&state.placements.Length==q.PlacementCount&&state.placements.All(p=>p>=0&&p<(q.regionPlacement?q.slots.Length:q.items.Length));
        public static string Arrangement(ArchiveQuestion q,ArchiveStationState state){
            if(q.regionPlacement)return string.Join("\n",q.items.Select((name,i)=>name+" → "+(i<state.placements.Length&&state.placements[i]>=0?q.slots[state.placements[i]]:"unplaced")));
            return string.Join("\n",q.slots.Select((name,i)=>name+": "+(i<state.placements.Length&&state.placements[i]>=0?q.items[state.placements[i]]:"empty")));
        }
        void Awake(){
            station=GetComponent<ArchiveTopicStation>();properties=new MaterialPropertyBlock();sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=1;sound.rolloffMode=AudioRolloffMode.Linear;sound.minDistance=3;sound.maxDistance=36;
            if(sparkMesh==null||sparkMaterial==null)return;var go=new GameObject("Scribes_Court_Feedback_Sparks");go.transform.SetParent(transform,false);sparks=go.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=sparks.main;main.playOnAwake=false;main.loop=false;main.maxParticles=24;main.startLifetime=.65f;main.startSpeed=1.8f;main.startSize=.08f;main.gravityModifier=.18f;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=sparks.emission;emission.enabled=false;var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=22;shape.radius=.2f;
            var r=go.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.mesh=sparkMesh;r.sharedMaterial=sparkMaterial;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
        }
        void Point(GameObject go,ArchivePointRole role,int index,string caption){var p=go.AddComponent<ArchiveTopicPoint>();p.station=station;p.role=role;p.index=index;p.caption=caption;station.points.Add(p);}
        public void Build(ArchiveTopicStation owner,ArchiveStationState state,ArchiveQuestion q){
            station=owner;question=q;tokens.Clear();socketRims.Clear();observed=false;previousPlacements=Array.Empty<int>();previousCarries="";
            owner.title.text="T4  FORMAL & INFORMAL PROOFS\nScribes' Court";
            if(state.phase!=1&&!state.timerExpired){Refresh(state);return;}
            for(int i=0;i<q.items.Length;i++){
                var stand=Instantiate(standPrefab,owner.workRoot);stand.name="Manuscript_Stand_"+i;stand.transform.localPosition=TokenPosition(i,q)-Vector3.up*.95f;stand.GetComponentInChildren<TMP_Text>(true).text=q.items[i];
                var go=Instantiate(IsRule(i,q)?ruleSealPrefab:scrollPrefab,owner.workRoot);go.name=(IsRule(i,q)?"Justification_Seal_":"Proof_Manuscript_")+i;
                go.GetComponentInChildren<TMP_Text>(true).text=q.proof.kind==ProofChallengeKind.MissingRule?"RULE":q.items[i].Substring(q.items[i].LastIndexOf(' ')+1);
                tokens[i]=go.transform;if(state.phase==1)Point(go,ArchivePointRole.Item,i,"Take "+q.items[i]+" · Read for text");
            }
            for(int i=0;i<q.slots.Length;i++){
                var go=Instantiate(q.regionPlacement?classificationAltarPrefab:socketPrefab,owner.workRoot);go.name="Proof_Answer_"+i;go.transform.localPosition=SocketPosition(i,q);go.GetComponentInChildren<TMP_Text>(true).text=q.proof.kind==ProofChallengeKind.MissingRule?"RULE SLOT":q.proof.kind==ProofChallengeKind.Formalize&&i>=q.statementCount?"RULE "+(i-q.statementCount+1):q.slots[i].ToUpperInvariant();
                socketRims.Add(go.transform.Find("Socket_Energy").GetComponent<Renderer>());if(state.phase==1)Point(go,ArchivePointRole.Slot,i,"Place on "+q.slots[i]);
            }
            Refresh(state);
        }
        public void Refresh(ArchiveStationState state){
            if(question==null)return;
            station.board.text=state.phase==2?"SCRIBES' COURT RESTORED\n5 / 5 team challenges complete":state.timerExpired?"TIME UP · WORK KEPT\nReturn to the Book to retry.":state.phase==0?state.round==0?"THE SCRIBES' COURT\nRead and classify proof scrolls.\nThen order statements and rules.\nRead the Book to begin.":"CHALLENGE "+state.round+" ACCEPTED\nThe completed proof is in the Book.\nRead it for the next challenge.":"CHALLENGE "+(state.round+1)+" / 5\n"+KindLabel(question)+"\nRead manuscripts with the Read button.\nHand / E · take and place";
            if(observed&&state.phase==1&&previousPlacements.Length==state.placements.Length){
                for(int i=0;i<state.placements.Length;i++)if(state.placements[i]>=0&&state.placements[i]!=previousPlacements[i]){Burst(CurrentPosition(question.regionPlacement?i:state.placements[i],question,state)+Vector3.up*.2f,energyColor,5);Sound(placeSound,.55f);}
                string held=string.Join(";",state.carries.Select(c=>c.actor+":"+c.item));if(held!=previousCarries&&state.carries.Count>0)Sound(takeSound,.45f);
            }
            previousPlacements=state.placements.ToArray();previousCarries=string.Join(";",state.carries.Select(c=>c.actor+":"+c.item));observed=true;
        }
        void Update(){
            if(station==null||station.session==null||!station.session.Ready)return;var state=station.session.State.stations[3];
            if(round!=state.round){round=state.round;for(int i=0;i<progressMarks.Length;i++)Tint(progressMarks[i],i<state.round?energyColor:restColor);}
            if(answerSerial<0){answerSerial=state.answerSerial;return;}if(answerSerial==state.answerSerial)return;
            bool fresh=state.answerSerial>answerSerial&&ArchiveTopicSession.Clock-state.lastAnswerAt<2;answerSerial=state.answerSerial;
            if(fresh){bool correct=state.lastAnswerResult==1;Sound(correct?correctSound:incorrectSound,correct?1:.55f);Burst(new Vector3(0,.3f,-1.5f),correct?energyColor:new Color(.95f,.28f,.22f),correct?14:8);}
        }
        void LateUpdate(){
            if(question==null||station.session==null||!station.session.Ready)return;var state=station.session.State.stations[3];var focus=ArchiveTopicUI.Current==null?null:ArchiveTopicUI.Current.FocusedPoint;
            foreach(var pair in tokens){if(pair.Value==null)continue;var held=state.carries.FirstOrDefault(c=>c.item==pair.Key);
                if(held!=null){pair.Value.localScale=Vector3.one*.55f;var p=station.session.ActorPlayer(held.actor);if(p!=null){pair.Value.position=p.transform.position+Vector3.up*1.7f+p.transform.forward*.95f;pair.Value.rotation=Quaternion.Euler(0,p.transform.eulerAngles.y,0);}continue;}
                pair.Value.localScale=Vector3.one;bool placed=question.regionPlacement?pair.Key<state.placements.Length&&state.placements[pair.Key]>=0:state.placements.Contains(pair.Key);
                pair.Value.localPosition=CurrentPosition(pair.Key,question,state)+(placed?Vector3.zero:Vector3.up*Mathf.Sin(Time.time*1.4f+pair.Key)*.035f);pair.Value.localRotation=Quaternion.Euler(0,placed?0:Mathf.Sin(Time.time*.55f+pair.Key)*6,0);
            }
            for(int i=0;i<socketRims.Count;i++){bool occupied=question.regionPlacement?state.placements.Contains(i):state.placements.Length>i&&state.placements[i]>=0;Tint(socketRims[i],focus!=null&&focus.station==station&&focus.role==ArchivePointRole.Slot&&focus.index==i?focusColor:occupied?energyColor:restColor);}
        }
        bool Audible()=>Time.timeScale>0&&Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,transform.position)<36;
        void Tint(Renderer r,Color color){if(r==null)return;properties.SetColor("_BaseColor",color);r.SetPropertyBlock(properties);}
        void Sound(AudioClip clip,float volume){if(clip!=null&&Audible())sound.PlayOneShot(clip,feedbackVolume*volume);}
        void Burst(Vector3 local,Color color,int count){if(sparks==null||!Audible())return;var main=sparks.main;main.startColor=color;sparks.transform.position=transform.TransformPoint(local);sparks.Emit(count);}
    }
}
