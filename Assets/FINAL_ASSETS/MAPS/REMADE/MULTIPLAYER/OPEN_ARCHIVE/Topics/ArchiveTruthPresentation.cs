using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Reusable Truth Mosaic visuals. The existing session owns every tile, answer and deadline.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveTruthPresentation:MonoBehaviour
    {
        public GameObject truthTilePrefab,rowSocketPrefab,standPrefab;
        public Renderer[] progressMarks;
        public AudioClip takeSound,placeSound,correctSound,incorrectSound;
        public Mesh sparkMesh;public Material sparkMaterial;
        [Range(0,1)] public float feedbackVolume=.23f;
        public Color trueColor=new Color(.22f,.76f,.59f),falseColor=new Color(.96f,.66f,.27f),focusColor=new Color(1,.90f,.57f),neutralColor=new Color(.16f,.34f,.35f);
        public static readonly string[] RowNames={"TT","TF","FT","FF"};
        public static Vector3 TokenPosition(int item)=>new Vector3(item==0?6.15f:-6.15f,1.05f,0);
        public static Vector3 RowPosition(int row)=>new Vector3(-3.05f,.10f,-3.6f+row*2.3f);
        public Transform[] PlacedTileRoots=>placed.Select(t=>t==null?null:t.root).ToArray();
        ArchiveTopicStation station;ArchiveQuestion question;
        sealed class Tile{public Transform root;public Renderer face;public TMP_Text symbol;public int item=-1;}
        readonly Tile[] banks=new Tile[2],placed=new Tile[4];
        readonly Renderer[] rowRims=new Renderer[4];
        readonly TMP_Text[] rowMessages=new TMP_Text[4];
        readonly Dictionary<int,Tile> carried=new Dictionary<int,Tile>();
        readonly int[] previousPlacements={-1,-1,-1,-1};
        string previousCarries="";bool hasObserved;
        int observedAnswer=-1,observedRound=-1;
        AudioSource audioSource;ParticleSystem sparks;MaterialPropertyBlock properties;
        void Awake(){
            station=GetComponent<ArchiveTopicStation>();properties=new MaterialPropertyBlock();
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=1;
            audioSource.rolloffMode=AudioRolloffMode.Linear;audioSource.minDistance=3;audioSource.maxDistance=36;
            if(sparkMesh!=null && sparkMaterial!=null){
                var go=new GameObject("Truth_Mosaic_Feedback_Sparks");go.transform.SetParent(transform,false);sparks=go.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=sparks.main;main.playOnAwake=false;main.loop=false;main.maxParticles=24;main.startLifetime=.65f;main.startSpeed=1.8f;main.startSize=.08f;main.gravityModifier=.18f;main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=sparks.emission;emission.enabled=false;var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=22;shape.radius=.2f;
                var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=sparkMesh;renderer.sharedMaterial=sparkMaterial;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
        }
        Tile TileObject(Transform parent,string name){
            var go=Instantiate(truthTilePrefab,parent);go.name=name;
            return new Tile{root=go.transform,face=go.transform.Find("Truth_Face").GetComponent<Renderer>(),symbol=go.GetComponentInChildren<TMP_Text>(true)};
        }
        void SetTile(Tile tile,int item){
            if(tile.item==item)return;tile.item=item;
            tile.symbol.text=item==0?"T":"F";tile.symbol.color=new Color(.025f,.10f,.09f);
            Tint(tile.face,item==0?trueColor:falseColor);
        }
        void Tint(Renderer renderer,Color color){if(renderer==null)return;properties.SetColor("_BaseColor",color);renderer.SetPropertyBlock(properties);}
        public void Build(ArchiveTopicStation owner,ArchiveStationState state,ArchiveQuestion q){
            station=owner;question=q;carried.Clear();hasObserved=false;previousCarries="";
            for(int i=0;i<4;i++)previousPlacements[i]=-1;
            for(int i=0;i<2;i++){
                var stand=Instantiate(standPrefab,owner.workRoot);stand.name=i==0?"TRUE_Tile_Stand":"FALSE_Tile_Stand";
                stand.transform.localPosition=TokenPosition(i)-Vector3.up*1.05f;
                var caption=stand.GetComponentInChildren<TMP_Text>(true);caption.text=i==0?"T  TRUE\nTAKE A TILE":"F  FALSE\nTAKE A TILE";
                caption.color=i==0?trueColor:falseColor;
                banks[i]=TileObject(owner.workRoot,"Reusable_Truth_Tile_"+i);banks[i].root.localPosition=TokenPosition(i);SetTile(banks[i],i);
                if(state.phase==1){
                    var p=stand.AddComponent<ArchiveTopicPoint>();p.station=owner;p.role=ArchivePointRole.Item;p.index=i;p.caption="Take "+(i==0?"T / True":"F / False")+" tile";owner.points.Add(p);
                }
            }
            for(int row=0;row<4;row++){
                var socket=Instantiate(rowSocketPrefab,owner.workRoot);socket.name="Truth_Output_Row_"+(row+1);socket.transform.localPosition=RowPosition(row);
                rowRims[row]=socket.transform.Find("Output_Inlay").GetComponent<Renderer>();
                rowMessages[row]=socket.GetComponentInChildren<TMP_Text>(true);
                placed[row]=TileObject(socket.transform,"Placed_Output_Tile_"+(row+1));placed[row].root.localPosition=new Vector3(0,.16f,0);placed[row].root.localScale=Vector3.one*.72f;placed[row].root.gameObject.SetActive(false);
                if(state.phase==1){
                    var p=socket.AddComponent<ArchiveTopicPoint>();p.station=owner;p.role=ArchivePointRole.Slot;p.index=row;p.caption="Place tile in row "+(row+1)+" · P="+RowNames[row][0]+", Q="+RowNames[row][1];owner.points.Add(p);
                }
            }
            owner.title.text="T2  TRUTH TABLE\nTruth Mosaic";
            owner.board.text=state.timerExpired?"TIME UP\nSolved tables kept.\nRead the Book to retry.":state.phase==2?"TRUTH MOSAIC RESTORED\n9 / 9 tables completed\nShared team success":state.phase==1?
                "TABLE "+(state.round+1)+" / 9  ·  "+Tier(state.round)+"\n"+q.prompt+"\nFill all four OUTPUT cells.":state.round==0?"TRUTH MOSAIC\nP   Q  →  OUTPUT\nRead the Book to begin.":"TABLE "+state.round+" RESTORED\n"+state.reveal+"\nRead the Book for the next table.";
            Refresh(state);
        }
        public static string Tier(int round)=>round<3?"EASY":round<6?"MEDIUM":"HARD";
        public void Refresh(ArchiveStationState state){
            if(question==null)return;
            bool showOutputs=state.phase==1||state.timerExpired;
            bool audible=Audible();
            for(int row=0;row<4;row++){
                int item=showOutputs && row<state.placements.Length?state.placements[row]:-1;
                if(placed[row]!=null){placed[row].root.gameObject.SetActive(item>=0);if(item>=0)SetTile(placed[row],item);}
                if(rowMessages[row]!=null)rowMessages[row].text="ROW "+(row+1)+"  ·  "+RowNames[row]+"\n"+(item<0?"OUTPUT":item==0?"TRUE":"FALSE");
                if(hasObserved && item>=0 && item!=previousPlacements[row] && state.phase==1){Burst(RowPosition(row)+Vector3.up*.3f,item==0?trueColor:falseColor,5);if(audible)Sound(placeSound,.6f);}
                previousPlacements[row]=item;
            }
            var active=new HashSet<int>(state.phase==1?state.carries.Select(c=>c.actor):Enumerable.Empty<int>());
            foreach(int actor in carried.Keys.Where(a=>!active.Contains(a)).ToArray()){var tile=carried[actor];if(tile.root!=null)Destroy(tile.root.gameObject);carried.Remove(actor);}
            if(state.phase==1)foreach(var held in state.carries){
                if(!carried.TryGetValue(held.actor,out var tile)){tile=TileObject(station.workRoot,"Carried_Truth_Tile_Actor_"+held.actor);tile.root.localScale=Vector3.one*.55f;carried.Add(held.actor,tile);}
                SetTile(tile,held.item);
            }
            string carries=string.Join(";",state.carries.Select(c=>c.actor+":"+c.item));
            if(hasObserved && carries!=previousCarries && state.carries.Count>0 && audible)Sound(takeSound,.45f);
            previousCarries=carries;hasObserved=true;
        }
        void Update(){
            if(station==null||station.session==null||!station.session.Ready)return;
            var state=station.session.State.stations[1];
            if(observedRound!=state.round){observedRound=state.round;for(int i=0;i<progressMarks.Length;i++)Tint(progressMarks[i],i<state.round?trueColor:neutralColor*.7f);}
            if(observedAnswer<0){observedAnswer=state.answerSerial;return;}
            if(observedAnswer!=state.answerSerial){
                bool fresh=state.answerSerial>observedAnswer&&ArchiveTopicSession.Clock-state.lastAnswerAt<2;observedAnswer=state.answerSerial;
                if(fresh){bool correct=state.lastAnswerResult==1;if(Audible())Sound(correct?correctSound:incorrectSound,correct?1:.55f);Burst(new Vector3(0,.2f,0),correct?trueColor:new Color(.95f,.28f,.22f),correct?14:8);}
            }
        }
        void LateUpdate(){
            if(station==null||station.session==null||!station.session.Ready)return;
            var state=station.session.State.stations[1];var focus=ArchiveTopicUI.Current==null?null:ArchiveTopicUI.Current.FocusedPoint;
            for(int i=0;i<2;i++)if(banks[i]!=null && banks[i].root!=null){
                banks[i].root.localPosition=TokenPosition(i)+Vector3.up*Mathf.Sin(Time.time*1.4f+i)*.045f;
                banks[i].root.localRotation=Quaternion.Euler(0,Mathf.Sin(Time.time*.55f+i)*9,0);
            }
            for(int row=0;row<4;row++){
                int item=state.phase==1 && row<state.placements.Length?state.placements[row]:-1;
                bool focused=focus!=null&&focus.station==station&&focus.role==ArchivePointRole.Slot&&focus.index==row;
                Tint(rowRims[row],focused?focusColor:item<0?neutralColor:item==0?trueColor:falseColor);
            }
            foreach(var pair in carried){var p=station.session.ActorPlayer(pair.Key);if(p==null||pair.Value.root==null)continue;
                pair.Value.root.position=p.transform.position+Vector3.up*1.65f+p.transform.forward*.9f;pair.Value.root.rotation=Quaternion.Euler(0,p.transform.eulerAngles.y,0);
            }
        }
        bool Audible()=>Time.timeScale>0&&Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,transform.position)<36;
        void Sound(AudioClip clip,float volume){if(clip!=null)audioSource.PlayOneShot(clip,feedbackVolume*volume);}
        void Burst(Vector3 local,Color color,int count){if(sparks==null)return;var main=sparks.main;main.startColor=color;sparks.transform.position=transform.TransformPoint(local);sparks.Emit(count);}
    }
}
