using System;
using LogicLegends.OpenArchive.Cinematics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Read-only shared clock HUD. All countdowns and rewards belong to the session.</summary>
    [DefaultExecutionOrder(185),DisallowMultipleComponent]
    public sealed class ArchiveMatchClockView : MonoBehaviour
    {
        public ArchiveTopicSession session;
        [Header("Local presentation only")]
        [Range(2,8)] public float noticeSeconds=4.5f;
        public bool Visible=>card!=null&&card.gameObject.activeInHierarchy;
        public string ClockText=>clock!=null?clock.text:"";
        public string PhaseText=>phase!=null?phase.text:"";
        public string NoticeText=>notice!=null&&notice.gameObject.activeInHierarchy?notice.text:"";
        public RectTransform Card=>card;
        public int WarningMask=>warningMask;
        public int ObservedBonusCount=>highestBonusCount;
        static readonly Color Ink=new Color(.12f,.16f,.13f),Teal=new Color(.025f,.28f,.29f),
            Cream=new Color(1,.96f,.82f),Amber=new Color(.76f,.30f,.055f),Red=new Color(.73f,.075f,.085f);
        Canvas canvas;
        RectTransform root,card,noticeCard,teamBanner,portalBanner;
        TMP_Text phase,clock,notice;
        Image clockFace,accent;
        KhezuCinematicJumpscareController primaryFilm,secondaryFilm;
        ArchiveTopicTestControls testTools;
        Vector2 screen;
        Rect safe;
        float scale,nextRead,nextPlacement,noticeUntil,lastRemaining=float.NaN;
        string match="",noticeValue="";
        int warningMask,highestBonusCount,noticePriority;
        bool tracking,wasEscape;

        void Start()
        {
            if(session==null)session=GetComponent<ArchiveTopicSession>();
            if(session==null){enabled=false;return;}
            Build();Place();
        }
        void Build()
        {
            var go=new GameObject("Shared_Match_Clock_HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup));
            go.transform.SetParent(transform,false);root=go.GetComponent<RectTransform>();canvas=go.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=140;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var group=go.GetComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
            card=Rect("Shared_Match_Clock",root,Vector2.zero,Vector2.one);
            card.anchorMin=card.anchorMax=new Vector2(.5f,1);card.pivot=new Vector2(.5f,1);
            var paper=card.gameObject.AddComponent<Image>();paper.sprite=StageUITheme.Paper;paper.type=Image.Type.Sliced;
            paper.pixelsPerUnitMultiplier=4.5f;paper.raycastTarget=false;
            var clockPlate=Rect("Teal_Clock_Face",card,new Vector2(.69f,.12f),new Vector2(.975f,.88f));
            clockFace=clockPlate.gameObject.AddComponent<Image>();clockFace.sprite=StageUITheme.ButtonArt;
            clockFace.type=Image.Type.Sliced;clockFace.pixelsPerUnitMultiplier=5;clockFace.raycastTarget=false;
            phase=Text("Shared_Clock_Phase",card,26,StageUITheme.HeadingFont,Teal);
            Stretch(phase.rectTransform,new Vector2(.055f,.21f),new Vector2(.68f,.80f));phase.alignment=TextAlignmentOptions.MidlineLeft;
            clock=Text("Shared_Time_Remaining",clockPlate,35,StageUITheme.ReadingFont,Cream);
            Stretch(clock.rectTransform,new Vector2(.08f,.08f),new Vector2(.92f,.92f));clock.alignment=TextAlignmentOptions.Center;
            var bar=Rect("Clock_State_Accent",card,new Vector2(.055f,.13f),new Vector2(.63f,.17f));
            accent=bar.gameObject.AddComponent<Image>();accent.color=Teal;accent.raycastTarget=false;
            noticeCard=Rect("Shared_Clock_Notice",card,Vector2.zero,Vector2.one);
            noticeCard.anchorMin=noticeCard.anchorMax=new Vector2(.5f,0);noticeCard.pivot=new Vector2(.5f,1);
            noticeCard.anchoredPosition=new Vector2(0,-5);
            var shade=noticeCard.gameObject.AddComponent<Image>();shade.sprite=StageUITheme.ButtonArt;
            shade.type=Image.Type.Sliced;shade.pixelsPerUnitMultiplier=5;shade.raycastTarget=false;
            notice=Text("Shared_Clock_Notice_Text",noticeCard,22,StageUITheme.ReadingFont,Cream);
            Stretch(notice.rectTransform,new Vector2(.05f,.08f),new Vector2(.95f,.92f));notice.alignment=TextAlignmentOptions.Center;
            noticeCard.gameObject.SetActive(false);card.gameObject.SetActive(false);
        }
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {var r=StageUITheme.Rect(name,parent);Stretch(r,min,max);return r;}
        static void Stretch(RectTransform r,Vector2 min,Vector2 max)
        {r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
        static TMP_Text Text(string name,Transform parent,float size,TMP_FontAsset font,Color color)
        {
            var r=Rect(name,parent,Vector2.zero,Vector2.one);var t=r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font=font;t.fontSize=size;t.color=color;t.raycastTarget=false;t.richText=false;
            t.enableAutoSizing=true;t.fontSizeMin=size-3;t.fontSizeMax=size;t.textWrappingMode=TextWrappingModes.NoWrap;
            t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        void Update()
        {
            if(card==null||session==null||Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+.1f;
            if(!session.Ready){card.gameObject.SetActive(false);return;}
            var current=session.MatchTimePhase;
            bool active=current==ArchiveMatchTimePhase.Active,escape=current==ArchiveMatchTimePhase.Escape;
            bool running=active||escape;float remaining=session.MatchTimeRemaining;
            ObserveClock(running,escape,remaining);
            bool show=!SuppressHud();card.gameObject.SetActive(show);if(!show)return;
            if(current==ArchiveMatchTimePhase.WaitingForPlayers)
            {
                phase.text="TEAM PREPARATION";clock.text=FormatTime(session.MatchTimeReadinessRemaining);SetAccent(Teal);
            }
            else if(current==ArchiveMatchTimePhase.Countdown)
            {
                phase.text="MATCH STARTS IN";clock.text=Mathf.Max(0,Mathf.CeilToInt(session.MatchTimeCountdownRemaining)).ToString("00");SetAccent(Teal);
            }
            else
            {
                phase.text=escape?"ESCAPE · ALL SURVIVORS":"SHARED MATCH TIME";clock.text=FormatTime(remaining);
                SetAccent(remaining<=60?Red:escape||remaining<=300?Amber:Teal);
            }
            bool hasNotice=Time.unscaledTime<noticeUntil&&!string.IsNullOrEmpty(noticeValue);
            // Persist the escape objective after the brief transition notice. The
            // clock stays shared; this view never changes who must reach the portal.
            noticeCard.gameObject.SetActive(hasNotice||escape);
            notice.text=hasNotice?noticeValue:"EVERY SURVIVING TEAMMATE MUST ENTER THE PORTAL";
            if(screen!=new Vector2(Screen.width,Screen.height)||safe!=Screen.safeArea||!Mathf.Approximately(scale,canvas.scaleFactor)||Time.unscaledTime>=nextPlacement)
            {nextPlacement=Time.unscaledTime+.5f;Place();}
        }
        void ObserveClock(bool running,bool escape,float remaining)
        {
            string id=session.State.matchId??"";
            if(!tracking||match!=id)
            {
                tracking=true;match=id;warningMask=0;noticePriority=0;noticeValue="";noticeUntil=0;
                highestBonusCount=session.MatchTimeBonusCount;wasEscape=escape;lastRemaining=running?remaining:float.NaN;
                // A late join or recovered checkpoint never replays old thresholds.
                if(running)MarkPassed(remaining);
                return;
            }
            bool freshBonus=session.MatchTimeBonusCount>highestBonusCount;
            if(freshBonus)
            {
                int gained=session.MatchTimeBonusCount-highestBonusCount;highestBonusCount=session.MatchTimeBonusCount;
                var bonus=session.State.matchTime;
                if(bonus!=null&&bonus.lastBonusAt>=0&&ArchiveTopicSession.Clock-bonus.lastBonusAt<=5)
                    Announce(gained==1?"T"+(bonus.lastBonusTopic+1)+" COMPLETE  ·  TEAM BONUS +02:00":"TOPICS COMPLETE  ·  TEAM BONUS +"+FormatTime(gained*120),1);
            }
            if(escape&&!wasEscape)
            {
                Announce("FINAL ESCAPE · BRING EVERY SURVIVOR TO THE PORTAL",3);
                MarkPassed(remaining); // A phase cap is not a 10/5-minute crossing.
            }
            else if(running&&!float.IsNaN(lastRemaining))
            {
                int[] thresholds={600,300,60};
                for(int i=0;i<thresholds.Length;i++)
                {
                    int bit=1<<i,threshold=thresholds[i];
                    if((warningMask&bit)!=0||lastRemaining<=threshold||remaining>threshold)continue;
                    warningMask|=bit;
                    Announce(threshold==60?(escape?"1 MINUTE · GET EVERY SURVIVOR TO THE PORTAL":"1 MINUTE OF SHARED MATCH TIME REMAINS"):
                        (threshold/60)+" MINUTES OF SHARED MATCH TIME REMAIN",2);
                }
            }
            wasEscape=escape;lastRemaining=running?remaining:float.NaN;
        }
        void MarkPassed(float remaining)
        {if(remaining<=600)warningMask|=1;if(remaining<=300)warningMask|=2;if(remaining<=60)warningMask|=4;}
        void Announce(string value,int priority)
        {
            if(Time.unscaledTime<noticeUntil&&priority<noticePriority)return;
            noticeValue=value;noticePriority=priority;noticeUntil=Time.unscaledTime+noticeSeconds;
        }
        void SetAccent(Color tint)
        {phase.color=tint;accent.color=tint;clock.color=tint==Red?new Color(1,.77f,.66f):Cream;}
        bool SuppressHud()
        {
            if(!session.MatchTimeAllowsGameplay&&session.MatchTimePhase==ArchiveMatchTimePhase.Expired)return true;
            if(session.TeamWon||session.TeamDefeated||session.HasEscapedActor(session.LocalActor)||Time.timeScale<=0||MultiplayerLobbyService.IsGameplayRecovering||session.NightEntranceActive)return true;
            if(session.worldProgression!=null&&session.worldProgression.FullscreenVisible)return true;
            if(ArchiveTopicUI.Current!=null&&ArchiveTopicUI.Current.ReadingStation!=null)return true;
            if(ArchiveLearningUI.Current!=null&&(ArchiveLearningUI.Current.GuideVisible||ArchiveLearningUI.Current.BarrierVisible))return true;
            if(testTools==null)testTools=session.GetComponent<ArchiveTopicTestControls>();
            if(testTools!=null&&testTools.AdminPanelOpen)return true;
            if(primaryFilm==null&&session.hunting!=null)primaryFilm=session.hunting.GetComponent<KhezuCinematicJumpscareController>();
            if(secondaryFilm==null&&session.worldProgression!=null&&session.worldProgression.secondary!=null)
                secondaryFilm=session.worldProgression.secondary.GetComponent<KhezuCinematicJumpscareController>();
            return primaryFilm!=null&&primaryFilm.IsPlaying||secondaryFilm!=null&&secondaryFilm.IsPlaying;
        }
        static float BottomInset(RectTransform rect,float canvasScale)
        {
            if(rect==null||!rect.gameObject.activeInHierarchy)return 0;
            var corners=new Vector3[4];rect.GetWorldCorners(corners);float bottom=corners[0].y;
            for(int i=1;i<4;i++)bottom=Mathf.Min(bottom,corners[i].y);
            return (Screen.height-bottom)/canvasScale;
        }
        void Place()
        {
            if(card==null)return;Canvas.ForceUpdateCanvases();screen=new Vector2(Screen.width,Screen.height);safe=Screen.safeArea;scale=Mathf.Max(.01f,canvas.scaleFactor);
            if(teamBanner==null&&ArchiveTopicUI.Current!=null)teamBanner=ArchiveTopicUI.Current.transform.Find("Shared_Topic_Status") as RectTransform;
            if(portalBanner==null)
            {
                // Existing escape UI is a scene sibling, not a child of the session.
                ArchiveExitUI exit=null;
                foreach(var candidate in FindObjectsByType<ArchiveExitUI>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    if(candidate.session==session){exit=candidate;break;}
                if(exit!=null)portalBanner=exit.transform.Find("PortalOpenNotice") as RectTransform;
            }
            float width=Mathf.Clamp(safe.width/scale*.31f,370,470);
            float top=(Screen.height-safe.yMax)/scale+96;
            top=Mathf.Max(top,BottomInset(teamBanner,scale)+8,BottomInset(portalBanner,scale)+8);
            card.sizeDelta=new Vector2(Mathf.Min(width,safe.width/scale-30),70);
            noticeCard.sizeDelta=new Vector2(Mathf.Min(620,safe.width/scale*.58f),49);
            float centerX=(safe.center.x-Screen.width*.5f)/scale;
            card.anchoredPosition=new Vector2(centerX,-top);
        }
        public static string FormatTime(float seconds)
        {int whole=Mathf.Max(0,Mathf.CeilToInt(seconds));return (whole/60).ToString("00")+":"+(whole%60).ToString("00");}
        void OnDisable(){if(card!=null)card.gameObject.SetActive(false);}
        void OnDestroy(){if(root!=null)Destroy(root.gameObject);}
    }
}
