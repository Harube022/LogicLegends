using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local presentation of existing actor health, encounter state and host-approved team signals.</summary>
    [DefaultExecutionOrder(188),DisallowMultipleComponent]
    public sealed class ArchiveTeamCoordinationUI : MonoBehaviour
    {
        public ArchiveTopicSession session;
        public static ArchiveTeamCoordinationUI Current {get;private set;}
        public bool PartyPanelOpen=>partyOpen;
        public bool PingMenuOpen=>pingOpen;
        public bool Visible=>dock!=null&&dock.gameObject.activeInHierarchy;
        public RectTransform TeamPanel=>dock;
        public RectTransform ObjectivePanel=>objectiveCard;
        public int ShownRowCount {get;private set;}
        public string ObjectiveText=>objectiveLabel!=null?objectiveLabel.text:"";
        public string PingToastText=>toast!=null&&toast.gameObject.activeInHierarchy?toast.text:"";
        public float ObjectiveDistance {get;private set;}
        public Vector3 ObjectiveWorldPosition {get;private set;}
        public Button[] TeamPingButtons=>pingButtons;
        public bool ObjectiveVisible=>objectiveCard!=null&&objectiveCard.gameObject.activeInHierarchy;
        public int WorldMarkerRendererCount=>ring==null?0:2;

        static readonly Color Ink=new Color(.10f,.17f,.15f),Teal=new Color(.025f,.29f,.30f),Cream=new Color(1,.96f,.82f),
            Gold=new Color(.82f,.48f,.13f),Red=new Color(.76f,.12f,.13f),Muted=new Color(.35f,.40f,.37f);
        Canvas canvas;
        RectTransform root,dock,header,roster,pingMenu,objectiveCard,toastCard,testHeader,testContent,clockCard,portalBanner;
        TMP_Text teamCaption,pingCaption,pingHint,objectiveLabel,objectiveDetail,toast;
        Button teamToggle,pingToggle;
        readonly Button[] pingButtons=new Button[4];
        readonly TMP_Text[] pingButtonCaptions=new TMP_Text[4];
        readonly Row[] rows=new Row[4];
        readonly HashSet<string> seenPings=new HashSet<string>();
        ArchiveTeamArrowGraphic arrow;
        ArchiveTopicTestControls tools;
        KhezuCinematicJumpscareController primaryFilm,secondaryFilm;
        ArchiveTeamObjective objective;
        LineRenderer ring,beam;
        Material markerMaterial;
        readonly Vector3[] ringPoints=new Vector3[25];
        Vector3 groundPoint,lastGroundRequest;
        Vector2 screen;
        Rect safe;
        float scale,nextRead,nextLayout,nextGround,toastUntil;
        string match="",toastValue="",localFeedback="";
        float localFeedbackUntil;
        bool partyOpen,pingOpen,hasGround;

        sealed class Row
        {
            public RectTransform root;
            public TMP_Text name,status,health;
            public ArchiveTeamHeartGraphic heart;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatic(){Current=null;}
        void Awake(){Current=this;}
        void Start()
        {
            if(session==null)session=GetComponent<ArchiveTopicSession>();
            if(session==null){enabled=false;return;}
            Build();Place();
        }
        void Build()
        {
            var go=new GameObject("Archive_Team_Coordination_HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.transform.SetParent(transform,false);root=go.GetComponent<RectTransform>();canvas=go.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=141;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            dock=Fixed("Team_Dock",root,new Vector2(0,1),new Vector2(0,1),new Vector2(370,68));
            header=Rect("Team_Header",dock,Vector2.zero,Vector2.one);
            teamToggle=Button("Toggle_Team_Roster",header,"TEAM",new Vector2(0,0),new Vector2(.69f,1),TogglePartyPanel);
            teamCaption=teamToggle.GetComponentInChildren<TMP_Text>();
            pingToggle=Button("Toggle_Quick_Team_Signals",header,"PING",new Vector2(.71f,0),Vector2.one,TogglePingMenu);
            pingCaption=pingToggle.GetComponentInChildren<TMP_Text>();
            roster=Fixed("Compact_Party_Status",dock,new Vector2(0,1),new Vector2(0,1),new Vector2(370,286));roster.anchoredPosition=new Vector2(0,-76);Paper(roster);
            for(int i=0;i<rows.Length;i++)
            {
                var row=new Row();rows[i]=row;
                row.root=Rect("Teammate_Row_"+(i+1),roster,new Vector2(.045f,1-(i+1)*.24f),new Vector2(.955f,.98f-i*.24f));
                row.root.anchorMin=new Vector2(.045f,1);row.root.anchorMax=new Vector2(.955f,1);row.root.pivot=new Vector2(.5f,1);
                row.root.sizeDelta=new Vector2(0,64);row.root.anchoredPosition=new Vector2(0,-12-i*66);
                row.name=Text("Teammate_Name",row.root,21,true,Ink);Stretch(row.name.rectTransform,new Vector2(0,.43f),new Vector2(.72f,1));
                row.status=Text("Authoritative_Status",row.root,18,false,Teal);Stretch(row.status.rectTransform,new Vector2(0,.02f),new Vector2(1,.49f));
                var icon=Rect("Heart_Icon",row.root,new Vector2(.75f,.55f),new Vector2(.815f,.91f));row.heart=icon.gameObject.AddComponent<ArchiveTeamHeartGraphic>();row.heart.raycastTarget=false;
                row.health=Text("Heart_Count",row.root,20,false,Red);Stretch(row.health.rectTransform,new Vector2(.825f,.43f),Vector2.one);row.health.alignment=TextAlignmentOptions.MidlineRight;
                if(i<3){var line=Rect("Separator",row.root,new Vector2(0,0),new Vector2(1,.014f));var image=line.gameObject.AddComponent<Image>();image.color=new Color(.1f,.25f,.22f,.18f);image.raycastTarget=false;}
            }
            pingMenu=Fixed("Quick_Signal_Menu",dock,new Vector2(0,1),new Vector2(0,1),new Vector2(370,216));pingMenu.anchoredPosition=new Vector2(0,-76);Paper(pingMenu);
            string[] captions={"HELP HERE","NEED MEDKIT","REVIVE TEAMMATE","GO TO PORTAL"};
            for(int i=0;i<4;i++)
            {
                int choice=i;float left=i%2==0?.035f:.515f,top=i<2?.945f:.575f;
                pingButtons[i]=Button("Ping_"+((ArchiveTeamPingKind)i),pingMenu,captions[i],new Vector2(left,top-.325f),new Vector2(left+.45f,top),()=>SendPing((ArchiveTeamPingKind)choice));
                pingButtonCaptions[i]=pingButtons[i].GetComponentInChildren<TMP_Text>();pingButtonCaptions[i].fontSize=20;pingButtonCaptions[i].fontSizeMin=16;
            }
            pingHint=Text("Signal_Cooldown_And_Feedback",pingMenu,17,false,Teal);Stretch(pingHint.rectTransform,new Vector2(.045f,.035f),new Vector2(.955f,.22f));
            pingHint.textWrappingMode=TextWrappingModes.Normal;pingHint.alignment=TextAlignmentOptions.Center;
            objectiveCard=Fixed("Team_Objective_Direction",root,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(450,64));Paper(objectiveCard);
            var ar=Rect("Objective_Bearing_Arrow",objectiveCard,new Vector2(.025f,.22f),new Vector2(.11f,.81f));arrow=ar.gameObject.AddComponent<ArchiveTeamArrowGraphic>();arrow.raycastTarget=false;arrow.color=Teal;
            objectiveLabel=Text("Objective_Label",objectiveCard,21,true,Teal);Stretch(objectiveLabel.rectTransform,new Vector2(.14f,.44f),new Vector2(.97f,.92f));
            objectiveDetail=Text("Objective_Distance",objectiveCard,17,false,Ink);Stretch(objectiveDetail.rectTransform,new Vector2(.14f,.07f),new Vector2(.97f,.49f));
            toastCard=Fixed("Incoming_Team_Signal",objectiveCard,new Vector2(.5f,0),new Vector2(.5f,1),new Vector2(570,58));toastCard.anchoredPosition=new Vector2(0,-6);
            var toastBackground=toastCard.gameObject.AddComponent<Image>();toastBackground.sprite=StageUITheme.ButtonArt;toastBackground.type=Image.Type.Sliced;toastBackground.pixelsPerUnitMultiplier=5;toastBackground.raycastTarget=false;
            toast=Text("Signal_Message",toastCard,20,false,Cream);Stretch(toast.rectTransform,new Vector2(.045f,.08f),new Vector2(.955f,.92f));toast.alignment=TextAlignmentOptions.Center;
            roster.gameObject.SetActive(false);pingMenu.gameObject.SetActive(false);objectiveCard.gameObject.SetActive(false);toastCard.gameObject.SetActive(false);dock.gameObject.SetActive(false);
        }
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max){var r=StageUITheme.Rect(name,parent);Stretch(r,min,max);return r;}
        static void Stretch(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
        static RectTransform Fixed(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 size)
        {var r=StageUITheme.Rect(name,parent);r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.sizeDelta=size;return r;}
        static void Paper(RectTransform r)
        {var image=r.gameObject.AddComponent<Image>();image.sprite=StageUITheme.Paper;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=5;image.raycastTarget=false;}
        static TMP_Text Text(string name,Transform parent,float size,bool heading,Color color)
        {
            var t=Rect(name,parent,Vector2.zero,Vector2.one).gameObject.AddComponent<TextMeshProUGUI>();
            t.font=heading?StageUITheme.HeadingFont:StageUITheme.ReadingFont;t.fontSize=size;t.color=color;t.raycastTarget=false;t.richText=false;
            t.alignment=TextAlignmentOptions.MidlineLeft;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;
            t.enableAutoSizing=true;t.fontSizeMin=size-2;t.fontSizeMax=size;return t;
        }
        static Button Button(string name,Transform parent,string caption,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var button=StageUITheme.Button(name,parent,caption,action);button.navigation=new Navigation{mode=Navigation.Mode.None};
            Stretch((RectTransform)button.transform,min,max);var label=button.GetComponentInChildren<TMP_Text>();label.fontSize=24;label.fontSizeMin=20;label.fontSizeMax=24;label.richText=false;
            label.rectTransform.offsetMin=new Vector2(10,6);label.rectTransform.offsetMax=new Vector2(-10,-6);return button;
        }
        public void TogglePartyPanel(){SetPartyPanelOpen(!partyOpen);}
        public void SetPartyPanelOpen(bool open){partyOpen=open;if(open)pingOpen=false;RefreshPanels();}
        public void TogglePingMenu(){pingOpen=!pingOpen;if(pingOpen)partyOpen=false;RefreshPanels();}
        void RefreshPanels(){if(roster!=null)roster.gameObject.SetActive(partyOpen);if(pingMenu!=null)pingMenu.gameObject.SetActive(pingOpen);}
        public bool SendPing(ArchiveTeamPingKind kind)
        {
            if(session==null)return false;
            bool allowed=session.CanTeamPing(kind,out string reason);
            bool sent=allowed&&session.RequestTeamPing(kind);
            localFeedback=sent?"Signal sent to your team.":allowed?session.LocalNotice:reason;localFeedbackUntil=Time.unscaledTime+3;
            if(sent){pingOpen=false;RefreshPanels();}return sent;
        }
        void Update()
        {
            if(root==null||session==null)return;
            if(Time.unscaledTime>=nextRead)
            {
                nextRead=Time.unscaledTime+.15f;ReadTeam();
                if(screen!=new Vector2(Screen.width,Screen.height)||safe!=Screen.safeArea||!Mathf.Approximately(scale,canvas.scaleFactor)||Time.unscaledTime>=nextLayout)
                {nextLayout=Time.unscaledTime+.5f;Place();}
            }
            bool hidden=SuppressHud();
            dock.gameObject.SetActive(!hidden&&(testContent==null||!testContent.gameObject.activeInHierarchy));
            bool showObjective=!hidden&&session.CanAct(session.LocalActor)&&objective!=null&&Camera.main!=null;
            objectiveCard.gameObject.SetActive(showObjective);
            if(showObjective)PresentObjective();else SetMarkerVisible(false);
        }
        void ReadTeam()
        {
            if(!session.Ready){ShownRowCount=0;objective=null;return;}
            if(match!=session.State.matchId){match=session.State.matchId;seenPings.Clear();toastValue="";toastUntil=0;localFeedbackUntil=0;partyOpen=pingOpen=false;RefreshPanels();}
            var rosterData=session.GetTeamRoster();ShownRowCount=Mathf.Min(4,rosterData.Length);
            teamCaption.text="TEAM "+ShownRowCount+" / 4 "+(partyOpen?"-":"+");
            float cooldown=session.TeamPingCooldownRemaining(session.LocalActor);pingCaption.text=cooldown>0?Mathf.CeilToInt(cooldown)+"s":"PING";
            for(int i=0;i<rows.Length;i++)
            {
                rows[i].root.gameObject.SetActive(i<ShownRowCount);if(i>=ShownRowCount)continue;var data=rosterData[i];var row=rows[i];
                row.name.text=(data.local?"YOU - ":"")+(data.name??"Teammate");row.name.color=data.local?Teal:Ink;
                row.status.text=(data.status??"Exploring")+" · Credit "+session.RewardContributionCount(data.actor)+" / 3";row.status.color=StatusColor(data);
                row.health.text=data.healthKnown?data.hearts+" / "+data.maxHearts:"? / ?";row.health.color=data.healthKnown&&data.hearts>0?Red:Muted;
                row.heart.color=data.healthKnown&&data.hearts>0?Red:Muted;
            }
            for(int i=0;i<4;i++)pingButtons[i].interactable=session.CanTeamPing((ArchiveTeamPingKind)i,out _);
            pingHint.text=Time.unscaledTime<localFeedbackUntil?localFeedback:cooldown>0?"Next signal in "+Mathf.CeilToInt(cooldown)+"s. Signals last 12 seconds.":"Tap a signal. Available requests follow your current situation.";
            foreach(var ping in session.ActiveTeamPings.OrderBy(p=>p.createdAt))
            {
                string key=ping.actor+":"+ping.sequence;if(!seenPings.Add(key))continue;
                toastValue=session.TeamPingText(ping);toastUntil=Time.unscaledTime+Mathf.Min(5,Mathf.Max(0,(float)(ping.createdAt+ArchiveTopicSession.TeamPingLifetimeSeconds-ArchiveTopicSession.Clock)));
            }
            if(seenPings.Count>128)seenPings.Clear();
            objective=session.TryGetTeamObjective(out var nextObjective)?nextObjective:null;
        }
        static Color StatusColor(ArchiveTeammateInfo info)
        {
            if(info.inactive)return Gold;string text=(info.status??"").ToUpperInvariant();
            if(info.healthKnown&&info.hearts<=0||text.Contains("CAPTUR")||text.Contains("HUNT")||text.Contains("DOWN")||text.Contains("CARR"))return Red;
            return text.Contains("RECONNECT")||text.Contains("LOAD")?Gold:Teal;
        }
        bool SuppressHud()
        {
            if(session==null||!session.Ready||Player.LocalInstance==null||session.TeamWon||session.TeamDefeated||session.HasEscapedActor(session.LocalActor)||Time.timeScale<=0||MultiplayerLobbyService.IsGameplayRecovering||session.NightEntranceActive)return true;
            if(GameInput.Instance!=null&&GameInput.Instance.GameplayInputBlocked)return true;
            if(session.worldProgression!=null&&session.worldProgression.FullscreenVisible)return true;
            if(ArchiveTopicUI.Current!=null&&ArchiveTopicUI.Current.ReadingStation!=null)return true;
            if(ArchiveLearningUI.Current!=null&&(ArchiveLearningUI.Current.GuideVisible||ArchiveLearningUI.Current.BarrierVisible))return true;
            if(tools==null)tools=session.GetComponent<ArchiveTopicTestControls>();if(tools!=null&&tools.AdminPanelOpen)return true;
            if(primaryFilm==null&&session.hunting!=null)primaryFilm=session.hunting.GetComponent<KhezuCinematicJumpscareController>();
            if(secondaryFilm==null&&session.worldProgression!=null&&session.worldProgression.secondary!=null)secondaryFilm=session.worldProgression.secondary.GetComponent<KhezuCinematicJumpscareController>();
            return primaryFilm!=null&&primaryFilm.IsPlaying||secondaryFilm!=null&&secondaryFilm.IsPlaying;
        }
        void PresentObjective()
        {
            var camera=Camera.main;Vector3 delta=objective.position-Player.LocalInstance.transform.position;delta.y=0;
            ObjectiveDistance=delta.magnitude;ObjectiveWorldPosition=objective.position;
            objectiveLabel.text=objective.label??"Team objective";objectiveDetail.text=(ObjectiveDistance<4?"AT THE MARKER":"FOLLOW THE MARKER")+"  |  "+Mathf.RoundToInt(ObjectiveDistance)+" m";
            Vector3 forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up);if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Player.LocalInstance.transform.forward,Vector3.up);
            float bearing=delta.sqrMagnitude<.01f?0:Vector3.SignedAngle(forward,delta,Vector3.up);arrow.rectTransform.localRotation=Quaternion.Euler(0,0,-bearing);
            Color tint=objective.kind==ArchiveTeamObjectiveKind.Rescue?Gold:objective.kind==ArchiveTeamObjectiveKind.Ping?new Color(.11f,.43f,.63f):Teal;
            arrow.color=tint;objectiveLabel.color=tint;
            bool showToast=Time.unscaledTime<toastUntil&&!string.IsNullOrEmpty(toastValue);toastCard.gameObject.SetActive(showToast);toast.text=toastValue;
            UpdateMarker(objective.position,tint);
        }
        float BottomInset(RectTransform rect)
        {
            if(rect==null||!rect.gameObject.activeInHierarchy)return 0;
            var corners=new Vector3[4];rect.GetWorldCorners(corners);return (Screen.height-corners.Min(c=>c.y))/scale;
        }
        void Place()
        {
            if(root==null)return;Canvas.ForceUpdateCanvases();screen=new Vector2(Screen.width,Screen.height);safe=Screen.safeArea;scale=Mathf.Max(.01f,canvas.scaleFactor);
            if(testHeader==null||testContent==null)
            {
                var rects=FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                if(testHeader==null)testHeader=rects.FirstOrDefault(r=>r.name=="Show_Hide_Test_Tools");
                if(testContent==null)testContent=rects.FirstOrDefault(r=>r.name=="Test_Tools_Content");
            }
            if(clockCard==null){var clock=session.GetComponent<ArchiveMatchClockView>();if(clock!=null)clockCard=clock.Card;}
            if(portalBanner==null){var exit=FindObjectsByType<ArchiveExitUI>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(v=>v.session==session);if(exit!=null)portalBanner=exit.transform.Find("PortalOpenNotice") as RectTransform;}
            float width=Mathf.Min(370,safe.width/scale*.30f),left=safe.xMin/scale+22,top=(Screen.height-safe.yMax)/scale+160;
            top=Mathf.Max(top,BottomInset(testHeader)+12);dock.sizeDelta=new Vector2(width,68);dock.anchoredPosition=new Vector2(left,-top);
            roster.sizeDelta=new Vector2(width,20+Mathf.Max(1,ShownRowCount)*66);pingMenu.sizeDelta=new Vector2(width,216);
            float centerX=(safe.center.x-Screen.width*.5f)/scale;
            float objectiveTop=Mathf.Max((Screen.height-safe.yMax)/scale+176,BottomInset(clockCard)+10,BottomInset(portalBanner)+10);
            // The clock's transient bonus/final-escape objective already extends beneath its plate.
            if(clockCard!=null&&clockCard.gameObject.activeInHierarchy)
            {var extra=clockCard.Find("Shared_Clock_Notice") as RectTransform;objectiveTop=Mathf.Max(objectiveTop,BottomInset(extra)+8);}
            objectiveCard.sizeDelta=new Vector2(Mathf.Min(450,safe.width/scale*.38f),64);objectiveCard.anchoredPosition=new Vector2(centerX,-objectiveTop);
            toastCard.sizeDelta=new Vector2(Mathf.Min(570,safe.width/scale*.48f),58);
        }
        void EnsureMarker()
        {
            if(ring!=null)return;var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Sprites/Default");if(shader==null)return;
            markerMaterial=new Material(shader){name="Archive_Team_Objective_Marker_Runtime",hideFlags=HideFlags.DontSave};
            LineRenderer Line(string name,int count,float width)
            {
                var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=markerMaterial;
                line.positionCount=count;line.startWidth=line.endWidth=width;line.useWorldSpace=true;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
                line.numCapVertices=0;line.numCornerVertices=0;line.alignment=LineAlignment.View;line.textureMode=LineTextureMode.Stretch;return line;
            }
            ring=Line("Team_Objective_Ground_Ring",25,.07f);beam=Line("Team_Objective_Short_Beacon",2,.09f);
        }
        void UpdateMarker(Vector3 point,Color tint)
        {
            EnsureMarker();if(ring==null)return;
            if(!hasGround||Time.unscaledTime>=nextGround&&(point-lastGroundRequest).sqrMagnitude>.04f)
            {
                nextGround=Time.unscaledTime+.5f;lastGroundRequest=point;groundPoint=point;
                foreach(var hit in Physics.RaycastAll(point+Vector3.up*8,Vector3.down,40,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                {
                    if(hit.collider.GetComponentInParent<Player>()!=null||hit.collider.GetComponentInParent<KhezuHuntingController>()!=null||hit.normal.y<.35f)continue;
                    // Don't project a river-side objective onto a bridge or arch far above its real anchor.
                    if(hit.point.y>point.y+2.5f)continue;groundPoint=hit.point;break;
                }
                groundPoint+=Vector3.up*.10f;hasGround=true;
            }
            Color markerTint=Color.Lerp(tint,new Color(.65f,1,.85f),.38f);if(markerMaterial.HasProperty("_BaseColor"))markerMaterial.SetColor("_BaseColor",markerTint);else markerMaterial.color=markerTint;
            float radius=.78f+Mathf.Sin(Time.unscaledTime*2.3f)*.045f;
            for(int i=0;i<ringPoints.Length;i++){float a=i/(float)(ringPoints.Length-1)*Mathf.PI*2;ringPoints[i]=groundPoint+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;}
            ring.SetPositions(ringPoints);beam.SetPosition(0,groundPoint+Vector3.up*.1f);beam.SetPosition(1,groundPoint+Vector3.up*1.2f);SetMarkerVisible(true);
        }
        void SetMarkerVisible(bool visible){if(ring!=null)ring.enabled=visible;if(beam!=null)beam.enabled=visible;}
        void OnDisable(){if(root!=null)root.gameObject.SetActive(false);SetMarkerVisible(false);}
        void OnEnable(){if(root!=null)root.gameObject.SetActive(true);}
        void OnDestroy(){if(Current==this)Current=null;if(root!=null)Destroy(root.gameObject);if(ring!=null)Destroy(ring.gameObject);if(beam!=null)Destroy(beam.gameObject);if(markerMaterial!=null)Destroy(markerMaterial);}
    }

    /// <summary>Code-drawn icons avoid missing mathematical/font glyphs and extra texture assets.</summary>
    public sealed class ArchiveTeamArrowGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();var r=GetPixelAdjustedRect();Vector2[] p={new Vector2(.5f,.98f),new Vector2(.96f,.35f),new Vector2(.64f,.45f),new Vector2(.64f,.04f),new Vector2(.36f,.04f),new Vector2(.36f,.45f),new Vector2(.04f,.35f)};
            for(int i=0;i<p.Length;i++)helper.AddVert(new Vector3(r.xMin+p[i].x*r.width,r.yMin+p[i].y*r.height,0),color,Vector2.zero);
            helper.AddTriangle(0,1,2);helper.AddTriangle(0,2,5);helper.AddTriangle(0,5,6);helper.AddTriangle(2,3,4);helper.AddTriangle(2,4,5);
        }
    }
    public sealed class ArchiveTeamHeartGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();var r=GetPixelAdjustedRect();Vector2[] p={new Vector2(.5f,.05f),new Vector2(.04f,.49f),new Vector2(.02f,.72f),new Vector2(.16f,.93f),new Vector2(.37f,.96f),new Vector2(.5f,.80f),new Vector2(.63f,.96f),new Vector2(.84f,.93f),new Vector2(.98f,.72f),new Vector2(.96f,.49f)};
            helper.AddVert(new Vector3(r.center.x,r.yMin+r.height*.55f,0),color,Vector2.zero);
            for(int i=0;i<p.Length;i++)helper.AddVert(new Vector3(r.xMin+p[i].x*r.width,r.yMin+p[i].y*r.height,0),color,Vector2.zero);
            for(int i=0;i<p.Length;i++)helper.AddTriangle(0,i+1,(i+1)%p.Length+1);
        }
    }
}
