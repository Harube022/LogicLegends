using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>A local, detached reading view. Owns no gameplay clock, rewards or progression.</summary>
    [DefaultExecutionOrder(3210)]
    public sealed class ArchiveLearningRecapUI : MonoBehaviour
    {
        public static ArchiveLearningRecapUI Current{get;private set;}
        static ArchiveLearningRecapData snapshot;
        static string account;
        RectTransform safe,overlay,card,content;
        TMP_Text heading,byline,body,footnote;
        Button summaryButton,mistakesButton,studyButton,returnButton,closeButton;
        ScrollRect scroll;
        TMP_FontAsset readingFont;
        Vector2 screen;
        Rect screenSafe;
        int tab;
        float checkIdentityAt;
        ArchiveTopicSession observedSession;
        public static bool HasReview=>snapshot!=null&&account==MultiplayerRewardBackendClient.UserId;
        public static bool IsVisible=>Current!=null&&Current.overlay!=null&&Current.overlay.gameObject.activeSelf;
        public static ArchiveLearningRecapData Snapshot=>snapshot==null?null:JsonUtility.FromJson<ArchiveLearningRecapData>(JsonUtility.ToJson(snapshot));
        public Button SummaryButton=>summaryButton;
        public Button MistakesButton=>mistakesButton;
        public Button StudyButton=>studyButton;
        public Button ReturnButton=>returnButton;
        public Button CloseButton=>closeButton;
        public ScrollRect ReadingScroll=>scroll;
        public RectTransform ReviewRoot=>card;
        public string VisibleText=>body!=null?body.text:"";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){Current=null;snapshot=null;account=null;}
        public static void EnsureCreated()
        {
            if(Current!=null)return;
            new GameObject("Archive_Post_Match_Learning",typeof(RectTransform)).AddComponent<ArchiveLearningRecapUI>();
        }
        public static void Watch(ArchiveTopicSession session)
        {EnsureCreated();Current.BindSession(session);}
        void BindSession(ArchiveTopicSession session)
        {
            if(ReferenceEquals(observedSession,session))return;
            if(!ReferenceEquals(observedSession,null))observedSession.Changed-=CaptureChangedMatch;
            observedSession=session;
            if(session!=null)session.Changed+=CaptureChangedMatch;
        }
        void CaptureChangedMatch()=>Observe(observedSession);
        void Awake()
        {
            if(Current!=null&&Current!=this){Destroy(gameObject);return;}
            Current=this;DontDestroyOnLoad(gameObject);
            readingFont=ArchiveLearningUI.Current!=null&&ArchiveLearningUI.Current.readingFont!=null?ArchiveLearningUI.Current.readingFont:StageUITheme.ReadingFont;
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1100;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            safe=Rect("Recap_Safe_Area",transform,Vector2.zero,Vector2.one);
            overlay=Rect("Optional_Learning_Review",safe,Vector2.zero,Vector2.one);
            var shade=overlay.gameObject.AddComponent<Image>();shade.color=new Color(.01f,.035f,.045f,.94f);
            card=Rect("Learning_Review_Card",overlay,Vector2.one*.5f,Vector2.one*.5f);
            var frame=card.gameObject.AddComponent<Image>();StagePresentation.Frame(frame,true);frame.pixelsPerUnitMultiplier=3.5f;
            heading=Text("Review_Title",card,"MATCH LEARNING",34,true);Stretch(heading.rectTransform,new Vector2(.055f,.875f),new Vector2(.95f,.94f));
            byline=Text("Personal_Review_Identity",card,"",23,false);Stretch(byline.rectTransform,new Vector2(.055f,.815f),new Vector2(.95f,.87f));
            summaryButton=Button("Review_Summary",card,"SUMMARY",()=>ShowTab(0));
            mistakesButton=Button("Review_Mistakes",card,"REVISIT MISTAKES",()=>ShowTab(1));
            studyButton=Button("Review_Study",card,"STUDY NEXT",()=>ShowTab(2));
            Stretch((RectTransform)summaryButton.transform,new Vector2(.055f,.715f),new Vector2(.335f,.80f));
            Stretch((RectTransform)mistakesButton.transform,new Vector2(.36f,.715f),new Vector2(.64f,.80f));
            Stretch((RectTransform)studyButton.transform,new Vector2(.665f,.715f),new Vector2(.945f,.80f));
            var host=Rect("Learning_Review_Reading",card,new Vector2(.055f,.225f),new Vector2(.945f,.695f));
            host.gameObject.AddComponent<Image>().color=new Color(.16f,.10f,.06f,.025f);
            var viewport=Rect("Viewport",host,Vector2.zero,Vector2.one);viewport.gameObject.AddComponent<RectMask2D>();
            content=Rect("Reading_Content",viewport,new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(10,12,8,18);
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            body=Text("Learning_Review_Text",content,"",25,false);body.lineSpacing=8;body.richText=false;
            scroll=host.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
            scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;
            footnote=Text("Review_Notice",card,"Optional review · Reopen it from Review Last Match in the party lobby.",21,false);
            Stretch(footnote.rectTransform,new Vector2(.055f,.155f),new Vector2(.945f,.215f));
            closeButton=Button("Close_Learning_Review",card,"BACK TO RESULTS",CloseReview);
            returnButton=Button("Review_Return_To_Party",card,"RETURN TO PARTY",ReturnToParty);
            ArchiveReadingScrollbars.Install(overlay);Place();overlay.gameObject.SetActive(false);
        }
        public static void Observe(ArchiveTopicSession session)
        {
            if(session==null||!session.Ready||!session.TeamWon&&!session.TeamDefeated||!session.LearningReviewAccountMatches)return;
            EnsureCreated();
            if(HasReview&&snapshot.matchId==session.State.matchId&&snapshot.actor==session.LocalActor)return;
            snapshot=ArchiveLearningRecap.Build(session.catalog,session.State,session.LocalActor,session.ActorName(session.LocalActor));
            account=MultiplayerRewardBackendClient.UserId;
            if(IsVisible)Current.ShowTab(0);
        }
        public static void ClearLastReview()
        {if(Current!=null)Current.CloseReview();snapshot=null;account=null;}
        public static void OpenLastReview()
        {
            if(!HasReview)return;
            var session=ArchiveTopicSession.Current;
            bool terminal=session!=null&&session.Ready&&(session.TeamWon||session.TeamDefeated)&&session.State.matchId==snapshot.matchId;
            bool lobby=SceneManager.GetActiveScene().name=="Main Menu"&&FindFirstObjectByType<MultiplayerLobbyUI>()?.IsOpen==true;
            if(!terminal&&!lobby)return;
            EnsureCreated();Current.overlay.gameObject.SetActive(true);Current.ShowTab(0);Current.Place();
        }
        void Update()
        {
            if(Time.unscaledTime>=checkIdentityAt)
            {
                checkIdentityAt=Time.unscaledTime+.5f;
                if(snapshot!=null&&account!=MultiplayerRewardBackendClient.UserId)ClearLastReview();
            }
            var session=ArchiveTopicSession.Current;
            BindSession(session);
            if(snapshot!=null&&session!=null&&session.Ready&&SceneManager.GetActiveScene().name=="MultiplayerGameplay"&&session.State.matchId!=snapshot.matchId)ClearLastReview();
            Observe(session);
            if(!IsVisible)return;
            string scene=SceneManager.GetActiveScene().name;
            if(scene=="Main Menu"&&MultiplayerLobbyService.Instance?.Starting==true||scene!="Main Menu"&&scene!="MultiplayerGameplay"||session!=null&&session.Ready&&
                (!session.TeamWon&&!session.TeamDefeated||session.State.matchId!=snapshot?.matchId))
            {CloseReview();return;}
            // A retained party may move to its lobby while this detached view is
            // open. No player must wait for another player's reading or a new clock.
            bool inGame=scene=="MultiplayerGameplay";
            returnButton.gameObject.SetActive(inGame);
            returnButton.interactable=true;
            closeButton.GetComponentInChildren<TMP_Text>().text=inGame?"BACK TO RESULTS":"BACK TO PARTY";
            LayoutFooter(inGame);
            if(screen!=new Vector2(Screen.width,Screen.height)||screenSafe!=Screen.safeArea)Place();
        }
        void Place()
        {
            screen=new Vector2(Screen.width,Screen.height);screenSafe=Screen.safeArea;
            safe.anchorMin=new Vector2(screenSafe.xMin/Screen.width,screenSafe.yMin/Screen.height);
            safe.anchorMax=new Vector2(screenSafe.xMax/Screen.width,screenSafe.yMax/Screen.height);
            safe.offsetMin=safe.offsetMax=Vector2.zero;Canvas.ForceUpdateCanvases();
            card.pivot=Vector2.one*.5f;card.anchoredPosition=Vector2.zero;
            card.sizeDelta=new Vector2(Mathf.Min(1120,safe.rect.width-48),Mathf.Min(850,safe.rect.height-48));
            LayoutFooter(SceneManager.GetActiveScene().name=="MultiplayerGameplay");
        }
        void LayoutFooter(bool inGame)
        {
            Stretch((RectTransform)closeButton.transform,new Vector2(inGame ? .12f : .32f,.055f),new Vector2(inGame ? .48f : .68f,.14f));
            Stretch((RectTransform)returnButton.transform,new Vector2(.52f,.055f),new Vector2(.88f,.14f));
        }
        public void CloseReview()
        {
            if(overlay==null)return;
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(selected!=null&&selected.transform.IsChildOf(overlay))EventSystem.current.SetSelectedGameObject(null);
            overlay.gameObject.SetActive(false);
        }
        public void ShowSummary()=>ShowTab(0);
        public void ShowMistakes()=>ShowTab(1);
        public void ShowStudy()=>ShowTab(2);
        void ShowTab(int value)
        {
            if(!HasReview)return;tab=value;
            heading.text="MATCH LEARNING";
            byline.text=snapshot.playerName+"  ·  "+snapshot.outcome+(snapshot.practice?"  ·  PRACTICE / ADMIN PREVIEW":"");
            var text=new StringBuilder();
            if(tab==0)text.Append(snapshot.summary).Append("\n\nTOPICS PRACTICED\n").Append(snapshot.topicsText).Append("\n\nTEAMWORK\n").Append(snapshot.teamworkText);
            else if(tab==1)
            {
                text.Append("UP TO 3 RECENT PERSONAL PROBLEMS\nA repeated mistake on the same problem appears once. Shared-clock timeouts are listed separately in Summary.\n\n");
                if(snapshot.reviews.Length==0)text.Append("No detailed personal problem context is available. Summary still shows any recorded mistakes. You can use Study Next to plan practice.");
                foreach(var review in snapshot.reviews)
                    text.Append(review.title).Append('\n').Append(review.prompt).Append("\n\nFEEDBACK FROM YOUR ATTEMPT\n").Append(review.feedback)
                        .Append("\n\nREASONING TO PRACTICE\n").Append(review.reasoning).Append("\n\nMODULE\n").Append(review.module).Append("\n\n────────────────────────\n\n");
            }
            else
            {
                text.Append("SUGGESTED LOGIC GARDEN REVIEW\nChoose a topic to practise next time. These suggestions describe this match's opportunities, not your ability level.\n\n");
                if(snapshot.suggestions.Length==0)text.Append("You have a personal practice record in every topic and no recorded personal answer mistakes. Choose a course module to revisit, explain one solution to a teammate, and try a fresh problem next match.");
                foreach(var suggestion in snapshot.suggestions)
                    text.Append(suggestion.title).Append('\n').Append(suggestion.reason).Append("\n\n").Append(suggestion.concept).Append("\n\nMODULE\n").Append(suggestion.module).Append("\n\n────────────────────────\n\n");
            }
            body.text=text.ToString();
            var buttons=new[]{summaryButton,mistakesButton,studyButton};
            for(int i=0;i<buttons.Length;i++)buttons[i].image.color=i==tab?new Color(.70f,1,.91f):Color.white;
            Canvas.ForceUpdateCanvases();scroll.StopMovement();scroll.verticalNormalizedPosition=1;
        }
        void ReturnToParty()
        {
            var session=ArchiveTopicSession.Current;if(session==null||!session.Ready||!session.TeamWon&&!session.TeamDefeated)return;
            CloseReview();
            if(MultiplayerLobbyService.Instance!=null)MultiplayerLobbyService.Instance.ReturnToLobbyAfterMatch();
            else MultiplayerLobbyService.EnsureExists().ReturnToLobbyAfterMatch();
        }
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {var rect=StageUITheme.Rect(name,parent);Stretch(rect,min,max);return rect;}
        static void Stretch(RectTransform rect,Vector2 min,Vector2 max)=>StageUITheme.Stretch(rect,min,max);
        TMP_Text Text(string name,Transform parent,string caption,int size,bool title)
        {
            var label=StageUITheme.Text(name,parent,caption,size,!title);label.font=title?StageUITheme.HeadingFont:readingFont;
            label.color=StageUITheme.ReadingInk;label.richText=false;label.raycastTarget=false;label.alignment=TextAlignmentOptions.TopLeft;
            label.textWrappingMode=TextWrappingModes.Normal;return label;
        }
        static Button Button(string name,Transform parent,string caption,UnityEngine.Events.UnityAction action)
        {
            var button=StageUITheme.Button(name,parent,caption,action);
            var label=button.GetComponentInChildren<TMP_Text>();label.enableAutoSizing=true;label.fontSizeMin=20;label.fontSizeMax=27;
            return button;
        }
        void OnDestroy(){BindSession(null);if(Current==this)Current=null;}
    }
}
