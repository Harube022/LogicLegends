using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Reusable optional topic relay presentation. The shared session owns inputs, confirmations and completion.</summary>
    public sealed class ArchiveT1CoopTrial : MonoBehaviour
    {
        public ArchiveTopicStation station;
        public Transform gate;
        public TMP_Text sign,backSign;
        public Renderer[] pulseRunes;
        public Material calmMaterial,warningMaterial,pulseMaterial;
        public static readonly Quaternion LaneRotation=Quaternion.Euler(0,-90,0);
        public Vector3 LanePoint(Vector3 p)=>LaneRotation*(p-new Vector3(0,0,12.9f))+new Vector3(-10,0,0);
        public Vector3 LanePosition(Player p)=>Quaternion.Inverse(LaneRotation)*(transform.InverseTransformPoint(p.transform.position)-new Vector3(-10,0,0))+new Vector3(0,0,12.9f);
        public Vector3 SwitchPosition(int input)=>LanePoint(new Vector3(input==0?-7:7,.2f,9.8f));
        readonly RaycastHit[] supportHits=new RaycastHit[12];
        GameInput input;
        RectTransform panel,canvasRect;
        Canvas controlsCanvas;
        TMP_Text caption,assignment,status,confirmLabel,claims;
        ArchiveCoopLesson lesson;int observedSerial=-1,observedSeed;
        Button yes,no,confirm;
        Vector2 lastScreen;
        Rect lastSafeArea;
        float lastScale;
        int nearby=-1;
        float slowedUntil;
        void Start()
        {
            station=GetComponent<ArchiveTopicStation>();
            input=GameInput.Instance;if(input!=null)input.OnInteractAction+=Interact;
            BuildControls();
        }
        void Interact(object sender,EventArgs e)
        {
            if(!isActiveAndEnabled||nearby<0||station==null||station.session==null||ReaderOpen)return;
            var session=station.session;
            if(session.RelayCanSetInput(session.LocalActor,station.topic,nearby))session.RequestRelayInput(station.topic,nearby,true);
        }
        static bool ReaderOpen=>ArchiveTopicUI.Current!=null&&ArchiveTopicUI.Current.ReadingStation!=null;
        static string Truth(bool value)=>value?"TRUE":"FALSE";
        static string InputValue(bool isSet,bool value,bool confirmed)=>!isSet?"?":Truth(value)+(confirmed?" [READY]":" [SET]");
        public bool CrossedFromEntry(int actor,Player player)
        {
            if(player==null)return false;
            var session=station.session;
            if(!session.IsAuthority)return false;
            var c=session.Relay(station.topic);
            if(c==null||!session.RelayAvailable(station.topic)||!c.open||c.completed)return false;
            if(c.traversals==null)c.traversals=new System.Collections.Generic.List<ArchiveT1Traversal>();
            var trace=c.traversals.FirstOrDefault(t=>t.actor==actor);
            if(trace==null){trace=new ArchiveT1Traversal{actor=actor};c.traversals.Add(trace);}
            Vector3 p=LanePosition(player);
            double now=ArchiveTopicSession.Clock;
            bool corridor=p.x> -9&&p.x<9&&Mathf.Abs(p.z-12.9f)<1.65f&&Supported(player);
            float scale=Mathf.Max(.01f,Mathf.Abs(station.transform.lossyScale.x));
            double elapsed=now-trace.lastAt;
            float distance=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(trace.lastPosition.x,trace.lastPosition.z));
            bool continuous=trace.valid&&elapsed>0&&elapsed<=.65&&distance<=Mathf.Max(.45f,(float)elapsed*player.moveSpeed*2.1f/scale);
            if(!corridor||!continuous){trace.valid=false;trace.crossedMiddle=false;}
            // Re-entry is accepted only at the start. An outside detour or teleport to the
            // far pad cannot turn into a completion. Local jumps must land before restarting.
            if(corridor&&p.x< -5)
            {
                if(!trace.valid){trace.enteredAt=now;trace.crossedMiddle=false;}
                trace.valid=true;
            }
            else if(trace.valid&&trace.lastPosition.x<=0&&p.x>=0)trace.crossedMiddle=true;
            trace.lastAt=now;trace.lastPosition=p;
            float minimumSeconds=10*scale/Mathf.Max(1,player.moveSpeed*2.1f);
            return trace.valid&&trace.crossedMiddle&&p.x>5&&now-trace.enteredAt>=minimumSeconds;
        }
        bool Supported(Player player)
        {
            var capsule=player.GetComponent<CharacterController>();if(capsule==null)return false;
            Vector3 feet=LogicLegends.OpenArchive.Water.OpenArchiveWater.Feet(capsule);
            // Remote capsules are deliberately disabled. Verify physical support under their
            // replicated feet rather than relying on a remote CharacterController.isGrounded.
            int count=Physics.RaycastNonAlloc(feet+Vector3.up*.7f,Vector3.down,supportHits,1.4f,~(1<<7),QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var hit=supportHits[i];
                if(hit.collider.GetComponentInParent<Player>()!=null||hit.normal.y<.7f)continue;
                if(hit.point.y>=feet.y-.65f&&hit.point.y<=feet.y+.55f)return true;
            }
            return false;
        }
        void Update()
        {
            if(input==null&&GameInput.Instance!=null){input=GameInput.Instance;input.OnInteractAction+=Interact;}
            if(station==null||station.session==null||!station.session.Ready)
            {
                nearby=-1;if(panel!=null)panel.gameObject.SetActive(false);
                if(Player.LocalInstance!=null)Player.LocalInstance.RemoveMovementSpeedModifier(this);
                return;
            }
            var session=station.session;var c=session.Relay(station.topic);
            bool active=session.RelayAvailable(station.topic);
            if(c!=null&&(observedSerial!=c.serial||observedSeed!=session.State.seed)){observedSerial=c.serial;observedSeed=session.State.seed;lesson=session.RelayLesson(station.topic);}
            if(gate!=null)
            {
                var target=new Vector3(0,active && !c.open ? .8f : -1.3f,12.9f);
                gate.localPosition=Vector3.Lerp(gate.localPosition,target,Time.deltaTime*5);
                var collider=gate.GetComponent<Collider>();if(collider!=null)collider.enabled=active&&!c.open;
            }
            double t=ArchiveTopicSession.Clock;float pulse=(float)(t%5);
            bool pulsing=active&&c.open&&!c.completed;
            bool warning=pulsing&&pulse>=2.5f&&pulse<3.5f,hit=pulsing&&pulse>=3.5f&&pulse<4.2f;
            for(int i=0;i<pulseRunes.Length;i++)if(pulseRunes[i]!=null)
                pulseRunes[i].sharedMaterial=hit?pulseMaterial:warning||c!=null&&c.completed?warningMaterial:calmMaterial;
            if(sign!=null)sign.text=c!=null&&c.completed?"T"+(station.topic+1)+" · OPTIONAL COMPLETE\n"+(c.reducedTeam?"One-player practice":"Team bonus recorded"):
                !active?"T"+(station.topic+1)+" · OPTIONAL TEAM RELAY\n"+(c!=null&&c.closed?"Closed: main topic complete.":"Activate the main Book first.\nAvailable only while the topic runs."):
                c.open?"BONUS ARCH OPEN\nCross from P to Q before the\nmain topic is completed.":
                "OPTIONAL · T"+(station.topic+1)+"\nMain solver + support player\nSet P and Q, confirm, then cross.\nCloses when the main topic completes.";
            if(backSign!=null&&sign!=null)backSign.text=sign.text;
            nearby=-1;var player=Player.LocalInstance;
            bool can=active&&!c.completed&&player!=null&&session.CanAct(session.LocalActor)&&Time.timeScale>0;
            bool onLane=false;
            if(can)
            {
                for(int i=0;i<2;i++)if(session.Near(session.LocalActor,station.topic,SwitchPosition(i),2.8f)){nearby=i;break;}
                var p=LanePosition(player);
                onLane=p.z>11&&p.z<14.8f&&Mathf.Abs(p.x)<9&&Mathf.Abs(p.y)<4;
                if(c.open&&hit&&p.z>11&&p.z<14.8f&&Mathf.Abs(p.x)<4.6f&&Mathf.Abs(p.y)<4)slowedUntil=Time.time+.55f;
                if(Time.time<slowedUntil)player.SetMovementSpeedModifier(this,.65f);else player.RemoveMovementSpeedModifier(this);
            }
            else if(player!=null)player.RemoveMovementSpeedModifier(this);
            if(panel!=null)
            {
                if(lastScreen!=new Vector2(Screen.width,Screen.height)||lastSafeArea!=Screen.safeArea||!Mathf.Approximately(lastScale,controlsCanvas.scaleFactor))PositionControls();
                panel.gameObject.SetActive(can&&!ReaderOpen&&(nearby>=0||c.open&&onLane));
            }
            if(caption!=null&&active)
            {
                caption.text="T"+(station.topic+1)+" · OPTIONAL · "+(!PhotonNetwork.InRoom?"OFFLINE PRACTICE":c.reducedTeam?"LAST SURVIVOR PRACTICE":"TEAM BONUS");
                if(claims!=null&&lesson!=null)claims.text="SEPARATE BONUS EXAMPLE\n"+lesson.context+"\n"+lesson.claimP+"\n"+lesson.claimQ;
                assignment.text="P: "+InputValue(c.pSet,c.p,c.pConfirmed)+"     Q: "+InputValue(c.qSet,c.q,c.qConfirmed);
                string detail=!string.IsNullOrWhiteSpace(c.explanation)?c.explanation:c.feedback;
                if(string.IsNullOrWhiteSpace(detail))detail=c.open?"Walk through the arch from P to Q. Amber warns; red briefly slows.":"Choose your value, then confirm your own rune.";
                status.text=detail;
            }
            bool canSet=can&&!ReaderOpen&&nearby>=0&&session.RelayCanSetInput(session.LocalActor,station.topic,nearby);
            bool canConfirm=can&&!ReaderOpen&&nearby>=0&&session.RelayCanConfirmInput(session.LocalActor,station.topic,nearby);
            if(yes!=null){yes.interactable=canSet;no.interactable=canSet;confirm.interactable=canConfirm;}
            if(confirmLabel!=null)confirmLabel.text=c!=null&&c.open?"Gate open":nearby<0?"Confirm":(nearby==0?c.pConfirmed:c.qConfirmed)?"Confirmed":"Confirm "+(nearby==0?"P":"Q");
        }
        void BuildControls()
        {
            var root=new GameObject("T"+(station.topic+1)+"_Relay_Controls",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            controlsCanvas=root.GetComponent<Canvas>();controlsCanvas.renderMode=RenderMode.ScreenSpaceOverlay;controlsCanvas.sortingOrder=135;canvasRect=root.GetComponent<RectTransform>();
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            panel=new GameObject("Rune_Input",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();panel.SetParent(root.transform,false);
            panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);panel.sizeDelta=new Vector2(640,410);
            var face=panel.GetComponent<Image>();face.sprite=ArchiveTopicUI.Current!=null?ArchiveTopicUI.Current.statusArt:StageUITheme.Paper;face.type=Image.Type.Sliced;face.pixelsPerUnitMultiplier=5;face.color=Color.white;face.raycastTarget=false;
            caption=Text("Relay_Target",panel,new Vector2(.045f,.90f),new Vector2(.955f,.98f));caption.fontSizeMax=caption.fontSize=24;
            claims=Text("Topic_Claims",panel,new Vector2(.055f,.45f),new Vector2(.945f,.89f));claims.fontSizeMax=claims.fontSize=22;claims.fontSizeMin=18;
            assignment=Text("Shared_Assignment",panel,new Vector2(.045f,.36f),new Vector2(.955f,.44f));assignment.fontSizeMax=assignment.fontSize=22;
            status=Text("Mode_And_Explanation",panel,new Vector2(.055f,.19f),new Vector2(.945f,.35f));status.fontSizeMax=status.fontSize=20;status.fontSizeMin=18;
            yes=MakeButton("TRUE",panel,new Vector2(.04f,.025f),new Vector2(.32f,.18f),()=>{if(nearby>=0&&station.session.RelayCanSetInput(station.session.LocalActor,station.topic,nearby))station.session.RequestRelayInput(station.topic,nearby,true);});
            no=MakeButton("FALSE",panel,new Vector2(.36f,.025f),new Vector2(.64f,.18f),()=>{if(nearby>=0&&station.session.RelayCanSetInput(station.session.LocalActor,station.topic,nearby))station.session.RequestRelayInput(station.topic,nearby,false);});
            confirm=MakeButton("Confirm",panel,new Vector2(.68f,.025f),new Vector2(.96f,.18f),()=>{if(nearby>=0&&station.session.RelayCanConfirmInput(station.session.LocalActor,station.topic,nearby))station.session.RequestRelayConfirm(station.topic,nearby);});
            confirmLabel=confirm.GetComponentInChildren<TMP_Text>();
            PositionControls();
            panel.gameObject.SetActive(false);
        }
        void PositionControls()
        {
            Canvas.ForceUpdateCanvases();
            lastScreen=new Vector2(Screen.width,Screen.height);lastSafeArea=Screen.safeArea;lastScale=Mathf.Max(.01f,controlsCanvas.scaleFactor);
            panel.sizeDelta=new Vector2(Mathf.Min(640,Mathf.Max(280,lastSafeArea.width/lastScale-24)),410);
            var position=new Vector2(lastSafeArea.center.x,lastSafeArea.yMin+lastSafeArea.height*.46f);
            position.y=Mathf.Clamp(position.y,lastSafeArea.yMin+panel.sizeDelta.y*lastScale*.5f+12,lastSafeArea.yMax-panel.sizeDelta.y*lastScale*.5f-12);
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,position,null,out var local))panel.anchoredPosition=local;
        }
        TMP_Text Text(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var text=go.GetComponent<TMP_Text>();text.font=StageUITheme.ReadingFont;text.fontSize=25;text.enableAutoSizing=true;text.fontSizeMax=25;text.fontSizeMin=20;text.overflowMode=TextOverflowModes.Ellipsis;text.richText=false;text.color=new Color(.16f,.11f,.07f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            var r=text.rectTransform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return text;
        }
        Button MakeButton(string label,Transform parent,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var go=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
            var image=go.GetComponent<Image>();image.sprite=StageUITheme.ButtonArt;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4.5f;image.color=Color.white;
            var b=go.GetComponent<Button>();b.targetGraphic=image;b.onClick.AddListener(action);
            var text=Text(label,go.transform,new Vector2(.05f,.10f),new Vector2(.95f,.90f));text.text=label;text.color=new Color(1,.95f,.80f);return b;
        }
        void OnDestroy(){if(input!=null)input.OnInteractAction-=Interact;if(Player.LocalInstance!=null)Player.LocalInstance.RemoveMovementSpeedModifier(this);if(panel!=null)Destroy(panel.transform.parent.gameObject);}
        void OnDisable(){nearby=-1;if(Player.LocalInstance!=null)Player.LocalInstance.RemoveMovementSpeedModifier(this);if(panel!=null)panel.gameObject.SetActive(false);}
    }
}
