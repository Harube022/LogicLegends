using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Renders bounded spawner copies and reviewed placements from the authoritative team state.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveSetsPresentation:MonoBehaviour
    {
        public const int CopiesPerSource=6;
        public GameObject blockPrefab,supplyPrefab,regionLabelPrefab;
        public TMP_FontAsset readingFont;
        public Mesh aOnlyMesh,intersectionMesh,bOnlyMesh,circleMesh,ringMesh;
        public Material aMaterial,bMaterial,intersectionMaterial,edgeMaterial,universeMaterial;
        public Renderer[] progressMarks;
        public AudioClip takeSound,placeSound,correctSound,incorrectSound;public Mesh sparkMesh;public Material sparkMaterial;
        [Range(0,1)]public float feedbackVolume=.23f;
        ArchiveTopicStation station;ArchiveQuestion question;AudioSource sound;ParticleSystem sparks;MaterialPropertyBlock properties;
        readonly Dictionary<int,Transform> blocks=new Dictionary<int,Transform>();readonly Dictionary<int,Renderer> rims=new Dictionary<int,Renderer>();
        readonly HashSet<int> previousCarries=new HashSet<int>();
        int[] previous=Array.Empty<int>();int receipt=-1,finished=-1;bool observed;
        static readonly Color Neutral=new Color(1,.94f,.76f),Correct=new Color(.3f,1,.45f),Incorrect=new Color(1,.24f,.24f),Azure=new Color(.22f,.8f,.75f);
        public Transform[] BlockRoots=>blocks.OrderBy(p=>p.Key).Select(p=>p.Value).ToArray();
        public static string GroupName(ArchiveQuestion q)=>new[]{"Single-set membership","Disjoint sets","Shared elements","Five-region set differences","Rearrange the complement"}[q.setGroup];
        public static string RegionName(int region,ArchiveQuestion q)=>q.setGroup==3?new[]{"FULL A","FULL B","A ∩ B","A − B","B − A"}[region]:q.setGroup==4?region==0?q.setName:"U − "+q.setName:new[]{"A ONLY","A ∩ B","B ONLY"}[region];
        public static Vector3 RegionPosition(int region,ArchiveQuestion q)=>q.setGroup==3?new[]{new Vector3(3.2f,.1f,1.25f),new Vector3(-3.2f,.1f,1.25f),new Vector3(0,.1f,-1.3f),new Vector3(3.2f,.1f,-3.6f),new Vector3(-3.2f,.1f,-3.6f)}[region]:q.setGroup==4?new Vector3((q.setName=="A"?1:-1)*(region==0?2.0f:-3.7f),.1f,-.6f):new Vector3(region==0?3.65f:region==2?-3.65f:0,.1f,-.6f);
        public static bool InsideRegion(int region,Vector3 point,ArchiveQuestion q,float margin=0){
            var p=new Vector2(point.x,point.z);if(q.setGroup==3)return Vector2.Distance(p,new Vector2(RegionPosition(region,q).x,RegionPosition(region,q).z))<=(region<2?2:1.55f)-margin;
            if(q.setGroup==4){bool inU=Mathf.Abs(point.x)<=5.6f-margin&&point.z>=-5.4f+margin&&point.z<=3.4f-margin;float d=Vector2.Distance(p,new Vector2(q.setName=="A"?2:-2,-.6f));return inU&&(region==0?d<=2.65f-margin:d>=2.65f+margin);}
            float a=Vector2.Distance(p,new Vector2(2,-.6f)),b=Vector2.Distance(p,new Vector2(-2,-.6f));return region==0?a<=3.25f-margin&&b>=3.25f+margin:region==1?a<=3.25f-margin&&b<=3.25f-margin:b<=3.25f-margin&&a>=3.25f+margin;
        }
        public static Vector3[][] Cells(ArchiveQuestion q){
            if(q.setCells!=null)return q.setCells;var result=new Vector3[q.slots.Length][];
            for(int region=0;region<result.Length;region++){
                var candidates=new List<Vector3>();for(int z=0;z<16;z++)for(int x=0;x<19;x++){var p=new Vector3(-5.58f+x*.62f,.32f,-5.2f+z*.62f);if(InsideRegion(region,p,q,.30f))candidates.Add(p);}
                if(candidates.Count==0)throw new InvalidOperationException("Sets region has no safe placement cells.");
                // Spread the first blocks across the region instead of clustering their floating labels.
                var center=RegionPosition(region,q);var ordered=new List<Vector3>();var distances=Enumerable.Repeat(float.MaxValue,candidates.Count).ToList();int next=candidates.IndexOf(candidates.OrderBy(p=>(p-center).sqrMagnitude).First());
                while(candidates.Count>0){var chosen=candidates[next];ordered.Add(chosen);candidates.RemoveAt(next);distances.RemoveAt(next);float farthest=-1;next=0;for(int i=0;i<candidates.Count;i++){distances[i]=Mathf.Min(distances[i],(candidates[i]-chosen).sqrMagnitude);if(distances[i]>farthest){farthest=distances[i];next=i;}}}
                result[region]=ordered.ToArray();
            }return q.setCells=result;
        }
        public static Vector3 TokenPosition(int item,ArchiveQuestion q){int slot=item%q.setSourceValues.Length;if(q.setGroup==4){int region=q.setInitialPlacements[item];return Cells(q)[region][item%Cells(q)[region].Length];}return new Vector3(slot<5?7.5f:-7.5f,.65f,4.5f-(slot%5)*2.25f);}
        public static Vector3 CurrentPosition(int item,ArchiveQuestion q,ArchiveStationState state){int region=item<state.placements.Length?state.placements[item]:-1;if(region<0)return TokenPosition(item,q);var cells=Cells(q)[region];int rank=q.setGroup==3?state.placements.Take(item).Count(r=>r==region):item;return cells[rank%cells.Length]+Vector3.up*(rank/cells.Length)*.42f;}
        public static int AvailableCopy(int source,ArchiveQuestion q,ArchiveStationState state){for(int i=source;i<q.items.Length;i+=q.setSourceValues.Length)if(i<state.placements.Length&&state.placements[i]<0&&!state.carries.Any(c=>c.item==i))return i;return -1;}
        public static bool CanPick(int item,ArchiveQuestion q,ArchiveStationState state)=>item>=0&&item<q.items.Length&&item<state.placements.Length&&(state.placements[item]>=0||state.carries.Any(c=>c.item==item)||q.setGroup!=3||AvailableCopy(item%q.setSourceValues.Length,q,state)==item);
        public static bool Visible(int item,ArchiveQuestion q,ArchiveStationState state)=>q.setGroup!=3||state.placements[item]>=0||state.carries.Any(c=>c.item==item)||AvailableCopy(item%q.setSourceValues.Length,q,state)==item;
        public static bool ReadyToCheck(ArchiveQuestion q,ArchiveStationState state)=>state.phase==1&&state.carries.Count==0&&state.placements.Any(r=>r>=0);
        void Awake(){station=GetComponent<ArchiveTopicStation>();properties=new MaterialPropertyBlock();sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=1;sound.rolloffMode=AudioRolloffMode.Linear;sound.minDistance=3;sound.maxDistance=36;
            if(sparkMesh==null||sparkMaterial==null)return;var go=new GameObject("Sets_Garden_Feedback_Sparks");go.transform.SetParent(transform,false);sparks=go.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var m=sparks.main;m.playOnAwake=false;m.loop=false;m.maxParticles=24;m.startLifetime=.65f;m.startSpeed=1.8f;m.startSize=.08f;m.simulationSpace=ParticleSystemSimulationSpace.World;var e=sparks.emission;e.enabled=false;var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=22;shape.radius=.2f;var r=go.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.mesh=sparkMesh;r.sharedMaterial=sparkMaterial;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
        }
        Transform Part(string name,Mesh mesh,Material mat,Vector3 position,Vector3 scale){var t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;t.SetParent(station.workRoot,false);t.localPosition=position;t.localScale=scale;t.GetComponent<MeshFilter>().sharedMesh=mesh;t.GetComponent<MeshRenderer>().sharedMaterial=mat;t.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return t;}
        void Point(GameObject go,ArchivePointRole role,int index,string caption){var p=go.AddComponent<ArchiveTopicPoint>();p.station=station;p.role=role;p.index=index;p.caption=caption;station.points.Add(p);}
        public void Build(ArchiveTopicStation owner,ArchiveStationState state,ArchiveQuestion q){station=owner;question=q;blocks.Clear();rims.Clear();previousCarries.Clear();previous=Array.Empty<int>();observed=false;owner.title.text="T7  SETS\nGarden of Sets";if(state.phase!=1&&!state.timerExpired){Refresh(state);return;}
            if(q.setGroup<3){Part("A_Only_Floor",aOnlyMesh,aMaterial,new Vector3(0,.075f,-.6f),Vector3.one);Part("Intersection_Floor",intersectionMesh,intersectionMaterial,new Vector3(0,.075f,-.6f),Vector3.one);Part("B_Only_Floor",bOnlyMesh,bMaterial,new Vector3(0,.075f,-.6f),Vector3.one);Part("A_Outline",ringMesh,edgeMaterial,new Vector3(2,.105f,-.6f),Vector3.one*6.5f);Part("B_Outline",ringMesh,edgeMaterial,new Vector3(-2,.115f,-.6f),Vector3.one*6.5f);}
            else if(q.setGroup==4){Part("Universal_Set_U",universeMesh,universeMaterial,new Vector3(0,.065f,-1),Vector3.one);foreach(float z in new[]{-5.4f,3.4f})Part("Universal_Set_Border",universeMesh,edgeMaterial,new Vector3(0,.10f,z),new Vector3(1,1,.008f));foreach(float x in new[]{-5.6f,5.6f})Part("Universal_Set_Border",universeMesh,edgeMaterial,new Vector3(x,.10f,-1),new Vector3(.006f,1,1));var u=Instantiate(regionLabelPrefab,owner.workRoot);u.name="Universal_Set_Label";u.transform.localPosition=new Vector3(5,.10f,1.2f);u.GetComponentInChildren<TMP_Text>(true).text="U";Part("Named_Set_Circle",circleMesh,q.setName=="A"?aMaterial:bMaterial,RegionPosition(0,q)+Vector3.down*.015f,Vector3.one*2.65f);Part("Named_Set_Outline",ringMesh,edgeMaterial,RegionPosition(0,q)+Vector3.up*.015f,Vector3.one*5.3f);}
            for(int r=0;r<q.slots.Length;r++){var center=RegionPosition(r,q);if(q.setGroup==3)Part("Separate_Answer_Tray_"+r,circleMesh,r==0?aMaterial:r==1?bMaterial:intersectionMaterial,new Vector3(center.x,.075f,center.z),Vector3.one*(r<2?2:1.55f));var label=Instantiate(regionLabelPrefab,owner.workRoot);label.name="Set_Region_"+r;label.transform.localPosition=center;label.GetComponentInChildren<TMP_Text>(true).text=RegionName(r,q);Point(label,ArchivePointRole.Slot,r,"Place in "+RegionName(r,q));}
            if(q.setGroup!=4)for(int s=0;s<q.setSourceValues.Length;s++){var supply=Instantiate(supplyPrefab,owner.workRoot);supply.name="Element_Supply_"+s;supply.transform.localPosition=TokenPosition(s,q)-Vector3.up*.65f;}
            Refresh(state);
        }
        public Mesh universeMesh;
        public void Refresh(ArchiveStationState state){if(question==null)return;var q=question;station.board.text=state.phase==2?"GARDEN OF SETS RESTORED\n5 / 5 challenges · 10 / 10 problems":state.timerExpired?"TIME UP · WORK KEPT\nRead the Book to retry.":state.phase==0?"GARDEN OF SETS\nTwo questions per challenge.\nRead the Book to begin.":"CHALLENGE "+(state.round/2+1)+" / 5 · PROBLEM "+(state.round%2+1)+" / 2\n"+GroupName(q)+"\nRead the sets · Hand / E to move blocks";
            if(state.phase!=1&&!state.timerExpired)return;
            for(int i=0;i<q.items.Length;i++){bool visible=Visible(i,q,state);Transform t;if(!blocks.TryGetValue(i,out t)&&visible){var go=Instantiate(blockPrefab,station.workRoot);go.name="Set_Element_"+i;go.GetComponentInChildren<TMP_Text>(true).text=q.items[i];blocks[i]=t=go.transform;rims[i]=go.transform.Find("Review_Rim").GetComponent<Renderer>();if(state.phase==1)Point(go,ArchivePointRole.Item,i,"Take element "+q.items[i]);}if(t==null)continue;t.gameObject.SetActive(visible);int grade=i<state.setGrades.Length?state.setGrades[i]:0;t.GetComponentInChildren<TMP_Text>(true).color=grade>0?Correct:grade<0?Incorrect:Neutral;Tint(rims[i],grade>0?Correct:grade<0?Incorrect:Azure);}
            if(observed&&state.phase==1){foreach(var held in state.carries.Where(c=>!previousCarries.Contains(c.item))){Sound(takeSound,.4f);Burst(CurrentPosition(held.item,q,state),Azure,3);}if(previous.Length==state.placements.Length)for(int i=0;i<previous.Length;i++)if(state.placements[i]>=0&&state.placements[i]!=previous[i]){Sound(placeSound,.5f);Burst(CurrentPosition(i,q,state),Azure,4);}}
            previousCarries.Clear();foreach(var held in state.carries)previousCarries.Add(held.item);previous=state.placements.ToArray();observed=true;
        }
        void Update(){if(station==null||station.session==null||!station.session.Ready)return;var s=station.session.State.stations[6];int progress=Math.Min(5,s.round/2);if(progress!=finished){finished=progress;foreach(var p in progressMarks.Select((r,i)=>new{r,i}))Tint(p.r,p.i<progress?Azure:new Color(.08f,.25f,.24f));}if(receipt<0){receipt=s.answerSerial;return;}if(receipt==s.answerSerial)return;bool fresh=s.answerSerial>receipt&&ArchiveTopicSession.Clock-s.lastAnswerAt<2;receipt=s.answerSerial;if(fresh){Sound(s.lastAnswerResult==1?correctSound:incorrectSound,s.lastAnswerResult==1?1:.55f);Burst(new Vector3(0,.3f,-.6f),s.lastAnswerResult==1?Azure:Incorrect,s.lastAnswerResult==1?14:8);}}
        void LateUpdate(){if(question==null||station.session==null||!station.session.Ready)return;var s=station.session.State.stations[6];foreach(var pair in blocks){var t=pair.Value;if(t==null||!t.gameObject.activeSelf)continue;var held=s.carries.FirstOrDefault(c=>c.item==pair.Key);if(held!=null){var p=station.session.ActorPlayer(held.actor);if(p!=null){t.position=p.transform.position+Vector3.up*1.55f+p.transform.forward*.9f;t.rotation=Quaternion.identity;t.localScale=Vector3.one*.7f;}}else{t.localScale=Vector3.one;t.localPosition=CurrentPosition(pair.Key,question,s);t.localRotation=Quaternion.identity;}var number=t.GetComponentInChildren<TMP_Text>();if(Camera.main!=null)number.transform.rotation=Camera.main.transform.rotation;}}
        bool Audible()=>Time.timeScale>0&&Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,transform.position)<40;
        void Tint(Renderer r,Color c){if(r==null)return;properties.SetColor("_BaseColor",c);r.SetPropertyBlock(properties);}
        void Sound(AudioClip clip,float v){if(clip!=null&&Audible())sound.PlayOneShot(clip,feedbackVolume*v);}
        void Burst(Vector3 local,Color c,int count){if(sparks==null||!Audible())return;var m=sparks.main;m.startColor=c;sparks.transform.position=transform.TransformPoint(local);sparks.Emit(count);}
    }
}
