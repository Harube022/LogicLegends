using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local reading and nearby mapping controls; the team session owns all decisions and time.</summary>
    public sealed class ArchiveMappingUI:MonoBehaviour
    {
        ArchiveTopicUI owner;RectTransform objective,reader,healthControl,jumpControl;
        TMP_Text title,timer,stage,summary,cue,readerTitle,readerTimer,body,feedback,primaryLabel,contextTitle,compactEditLabel;
        RectTransform buildControls,classControls,propertyControls;TMP_Text introduction;
        ScrollRect reading;Button checkButton,primary,undoReader,compactEdit;
        readonly Button[] domainButtons=new Button[6],outputButtons=new Button[6],classButtons=new Button[3],propertyButtons=new Button[4];
        readonly Image[] progress=new Image[5];
        int revision=-1;string readingKey="";Vector2 screen;Rect safe;float canvasScale;
        static readonly Color Ink=new Color(.16f,.11f,.07f),Teal=new Color(.08f,.36f,.35f),Red=new Color(.75f,.13f,.10f);
        public bool IsOpen=>reader!=null&&reader.gameObject.activeSelf;
        public bool IsObjectiveVisible=>objective!=null&&objective.gameObject.activeSelf;
        public RectTransform Objective=>objective;
        public Button CheckButton=>checkButton;
        public Button UndoButton=>undoReader;
        public Button[] DomainButtons=>domainButtons;
        public Button[] OutputButtons=>outputButtons;
        public Button[] ClassificationButtons=>classButtons;
        public Button[] PropertyButtons=>propertyButtons;
        public TMP_Text ReadingText=>body;
        public void Initialize(ArchiveTopicUI ui){owner=ui;Build();}
        void Build(){
            objective=Box("T8_Compact_Mapping_Objective",transform,Vector2.one,new Vector2(440,248),owner.statusArt);objective.pivot=Vector2.one;
            title=Label("Challenge_Progress",objective,22,owner.headingFont,Ink,new Vector2(.06f,.82f),new Vector2(.73f,.94f));
            timer=Label("Shared_T8_Timer",objective,23,owner.headingFont,Teal,new Vector2(.76f,.82f),new Vector2(.94f,.94f));timer.alignment=TextAlignmentOptions.MidlineRight;
            stage=Label("Mapping_Activity",objective,22,owner.headingFont,Ink,new Vector2(.06f,.66f),new Vector2(.94f,.82f));
            summary=Label("Domain_Codomain_Range",objective,18,owner.textFont,Teal,new Vector2(.06f,.36f),new Vector2(.94f,.66f));
            cue=Label("Nearby_Action",objective,17,owner.textFont,Teal,new Vector2(.06f,.25f),new Vector2(.94f,.37f));
            MakeButton(objective,"Read",Read,new Vector2(.18f,.135f),new Vector2(108,50),20);
            compactEdit=MakeButton(objective,"Undo",Edit,new Vector2(.48f,.135f),new Vector2(110,50),20);compactEditLabel=compactEdit.GetComponentInChildren<TMP_Text>();
            checkButton=MakeButton(objective,"Check",Submit,new Vector2(.79f,.135f),new Vector2(146,50),21);
            foreach(var r in FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None)){if(r.name=="JumpButton")jumpControl=r;else if(r.name=="Khezu_Existing_Health_System")healthControl=r;}
            PositionObjective();
            reader=Box("T8_Mapping_Conduit_Reader",transform,new Vector2(.5f,.52f),new Vector2(1120,800),owner.panelArt);
            readerTitle=Label("Topic_And_Challenge",reader,28,owner.headingFont,Ink,new Vector2(.055f,.845f),new Vector2(.72f,.95f));
            MakeButton(reader,"Close",owner.Close,new Vector2(.85f,.90f),new Vector2(230,82),27);
            readerTimer=Label("Shared_Clock",reader,23,owner.textFont,Teal,new Vector2(.55f,.795f),new Vector2(.94f,.835f));readerTimer.alignment=TextAlignmentOptions.MidlineRight;
            Label("Reading_Heading",reader,23,owner.headingFont,Ink,new Vector2(.065f,.745f),new Vector2(.60f,.80f)).text="MAPPING CONDUIT · READER";
            contextTitle=Label("Nearby_Controls",reader,23,owner.headingFont,Teal,new Vector2(.64f,.745f),new Vector2(.94f,.80f));
            var viewport=Rect("Mapping_Reading",reader,new Vector2(.065f,.30f),new Vector2(.60f,.745f));viewport.gameObject.AddComponent<RectMask2D>();viewport.gameObject.AddComponent<Image>().color=new Color(.07f,.29f,.28f,.055f);reading=viewport.gameObject.AddComponent<ScrollRect>();reading.horizontal=false;reading.movementType=ScrollRect.MovementType.Clamped;reading.scrollSensitivity=35;
            var content=Rect("Reading_Content",viewport,new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);content.offsetMin=new Vector2(10,0);content.offsetMax=new Vector2(-10,0);reading.content=content;reading.viewport=viewport;
            body=content.gameObject.AddComponent<TextMeshProUGUI>();body.font=owner.textFont;body.fontSize=25;body.color=Ink;body.richText=false;body.raycastTarget=false;body.overflowMode=TextOverflowModes.Overflow;body.margin=new Vector4(6,8,6,14);content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            buildControls=Rect("Nearby_Crystal_Controls",reader,new Vector2(.64f,.30f),new Vector2(.94f,.745f));
            Label("Domain_Heading",buildControls,21,owner.headingFont,Teal,new Vector2(0,.92f),new Vector2(1,.99f)).text="DOMAIN · INPUT";
            Label("Codomain_Heading",buildControls,21,owner.headingFont,Teal,new Vector2(0,.46f),new Vector2(1,.56f)).text="CODOMAIN · OUTPUT";
            for(int i=0;i<6;i++){int index=i;float x=.17f+(i%3)*.33f;domainButtons[i]=MakeButton(buildControls,(i+1).ToString(),()=>owner.session.Request(7,ArchiveAction.Select,index),new Vector2(x,.84f-(i/3)*.17f),new Vector2(82,50),25);outputButtons[i]=MakeButton(buildControls,((char)('A'+i)).ToString(),()=>Connect(index),new Vector2(x,.40f-(i/3)*.17f),new Vector2(82,50),25);}
            Label("Physical_Range_Hint",buildControls,18,owner.textFont,Teal,new Vector2(0,0),new Vector2(1,.13f)).text="Approach a crystal to enable its button. Hand / E also works.";
            classControls=Rect("Nearby_Verdict_Controls",reader,new Vector2(.64f,.30f),new Vector2(.94f,.745f));propertyControls=Rect("Nearby_Property_Controls",reader,new Vector2(.64f,.30f),new Vector2(.94f,.745f));
            for(int i=0;i<3;i++){int index=i;classButtons[i]=MakeButton(classControls,ArchiveMappingPresentation.ClassificationNames[i],()=>owner.session.Request(7,ArchiveAction.Select,index),new Vector2(.5f,.81f-i*.28f),new Vector2(302,80),23);}
            for(int i=0;i<4;i++){int index=i;propertyButtons[i]=MakeButton(propertyControls,ArchiveMappingPresentation.PropertyNames[i],()=>owner.session.Request(7,ArchiveAction.Select,ArchiveMappingPresentation.PropertyValues[index]),new Vector2(.5f,.85f-i*.24f),new Vector2(302,64),23);}
            introduction=Label("Book_Overview",reader,23,owner.textFont,Teal,new Vector2(.65f,.33f),new Vector2(.94f,.72f));introduction.text="1. Read at the Book.\n\n2. Build or inspect the lasers.\n\n3. Check the team answer.\n\nYour movement controls stay available while reading.";
            feedback=Label("Team_Feedback",reader,21,owner.textFont,Ink,new Vector2(.065f,.16f),new Vector2(.94f,.285f));
            MakeButton(reader,"Overview",()=>{reading.verticalNormalizedPosition=1;},new Vector2(.20f,.075f),new Vector2(210,82),24);
            undoReader=MakeButton(reader,"Undo laser",Undo,new Vector2(.44f,.075f),new Vector2(225,82),24);
            primary=MakeButton(reader,"Begin at Book",Primary,new Vector2(.75f,.075f),new Vector2(300,82),25);primaryLabel=primary.GetComponentInChildren<TMP_Text>();
            for(int i=0;i<5;i++)progress[i]=Rect("Accepted_Mapping_"+(i+1),reader,new Vector2(.065f+i*.032f,.13f),new Vector2(.09f+i*.032f,.145f)).gameObject.AddComponent<Image>();
            reader.gameObject.SetActive(false);objective.gameObject.SetActive(false);
        }
        public void Open(){reader.gameObject.SetActive(true);revision=-1;Refresh();}
        public void Close(){if(reader!=null)reader.gameObject.SetActive(false);}
        void Read()=>owner.Open(owner.session.stations[7]);
        void Edit(){if(owner.session.Question(7).mapping.questionKind==MappingQuestionKind.BuildExactRelation)Undo();else Read();}
        void Undo()=>owner.session.Request(7,ArchiveAction.Undo);
        void Primary(){var s=owner.session.State.stations[7];if(s.phase==0)owner.session.Request(7,ArchiveAction.Start);else if(s.phase==1)Submit();else owner.Close();}
        void Submit(){if(owner.session.Ready&&ArchiveMappingPresentation.ReadyToCheck(owner.session.Question(7),owner.session.State.stations[7]))owner.session.Request(7,ArchiveAction.Submit);}
        void Connect(int output){var actor=owner.session.State.actors.FirstOrDefault(a=>a.actor==owner.session.LocalActor);if(actor!=null&&actor.mappingRound==owner.session.State.stations[7].round&&actor.mappingInput>=0)owner.session.Request(7,ArchiveAction.Connect,actor.mappingInput,output);}
        void Update(){
            if(owner==null||owner.session==null||!owner.session.Ready)return;var session=owner.session;var s=session.State.stations[7];var q=session.Question(7);
            bool near=Player.LocalInstance!=null&&session.CanAct(session.LocalActor)&&Time.timeScale>0&&Vector3.Distance(Player.LocalInstance.transform.position,session.stations[7].transform.position)<34;
            objective.gameObject.SetActive(near&&owner.ReadingStation==null);if(!near){if(IsOpen)owner.Close();return;}
            if(screen!=new Vector2(Screen.width,Screen.height)||safe!=Screen.safeArea||!Mathf.Approximately(canvasScale,owner.GetComponent<Canvas>().scaleFactor))PositionObjective();
            if(revision!=session.State.revision){revision=session.State.revision;Refresh();}
            int seconds=Mathf.CeilToInt(session.TimerRemaining(s));string time=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");timer.text=time;readerTimer.text="TEAM TIME  "+time+(s.phase==0&&!s.timerExpired?" · paused at Book":"");timer.color=readerTimer.color=s.timerExpired||s.timerRunning&&seconds<=30?Red:Teal;
            bool unlocked=string.IsNullOrEmpty(session.TopicLockNotice(7)),active=unlocked&&s.phase==1,build=q.mapping.questionKind==MappingQuestionKind.BuildExactRelation;
            bool canCheck=active&&ArchiveMappingPresentation.ReadyToCheck(q,s)&&session.Near(session.LocalActor,7,Vector3.zero,14);checkButton.interactable=canCheck;primary.interactable=s.phase==2||canCheck||unlocked&&s.phase==0&&session.Near(session.LocalActor,7,ArchiveTopicStation.BookPosition,ArchiveTopicSession.BookUseRadius);
            bool canUndo=active&&build&&s.links.Count>(q.mapping.initialPairsLocked?q.mapping.initialPairs.Count:0)&&ArchiveMappingPresentation.NearRange(session);undoReader.gameObject.SetActive(active&&build);undoReader.interactable=canUndo;compactEdit.interactable=build?canUndo:active;
            var actor=session.State.actors.FirstOrDefault(a=>a.actor==session.LocalActor);int input=actor!=null&&actor.mappingRound==s.round?actor.mappingInput:-1;
            for(int i=0;i<6;i++){domainButtons[i].interactable=active&&build&&i<q.inputLabels.Length&&session.Near(session.LocalActor,7,session.stations[7].InputPosition(i),2.4f);outputButtons[i].interactable=active&&build&&input>=0&&i<q.outputLabels.Length&&!s.links.Contains(input*100+i)&&session.Near(session.LocalActor,7,session.stations[7].OutputPosition(i),2.4f);}
            for(int i=0;i<4;i++){bool nearby=active&&session.Near(session.LocalActor,7,ArchiveMappingPresentation.AnswerPosition(i),2.4f);if(i<3)classButtons[i].interactable=nearby;propertyButtons[i].interactable=nearby;}
            cue.text=!unlocked?"Another topic is active":s.phase!=1?"Book Statue · Hand / E":build?input<0?"Hand / E · select a Domain crystal":"Domain "+q.inputLabels[input]+" selected → Codomain":"Hand / E · choose a marked answer seal";
        }
        void Refresh(){
            var session=owner.session;if(!session.Ready)return;var s=session.State.stations[7];var q=session.Question(7);bool display=s.phase==1||s.timerExpired,build=q.mapping.questionKind==MappingQuestionKind.BuildExactRelation;
            title.text=s.phase==2?"T8 · TEAM COMPLETE 5 / 5":"T8 · CHALLENGE "+(s.round+1)+" / 5";stage.text=s.phase==2?"Mapping Conduit restored":s.timerExpired?"Time up · work kept":s.phase==0?"Read the Book to continue":q.title;
            summary.text=display?"Domain { "+string.Join(", ",q.inputLabels)+" }\nCodomain { "+string.Join(", ",q.outputLabels)+" }\n"+ArchiveMappingPresentation.RangeText(q,s):s.phase==2?"Five shared mapping challenges complete":"Five challenges · 10-minute shared clock";
            readerTitle.text="T8  FUNCTIONS & RELATIONS\n"+(s.phase==2?"Team complete · 5 / 5":"Challenge "+(s.round+1)+" / 5 · "+q.title);
            string links=string.Join("\n",s.links.Select(link=>q.inputLabels[link/100]+" → "+q.outputLabels[link%100]+(ArchiveMappingPresentation.Fixed(link,q)?"  [fixed]":"")));
            body.text=display?q.prompt+"\n\nDomain = { "+string.Join(", ",q.inputLabels)+" }\nCodomain = { "+string.Join(", ",q.outputLabels)+" }\n"+ArchiveMappingPresentation.RangeText(q,s)+"\n\nCURRENT LASERS\n"+(links.Length==0?"No connections yet.":links)+"\n\n"+(build?q.instructions:q.mapping.questionKind==MappingQuestionKind.ClassifyFunction?"Every valid set of displayed pairs is a relation. Choose Function when each declared input has exactly one output; choose Not a function when that requirement fails. Invalid pair means an endpoint lies outside the displayed domain or codomain. A function is also a relation.":"Toggle all applicable seals. Injective: different inputs have different outputs, and each input has one output. Surjective: every codomain value is reached. Bijective: both one-to-one and onto. Selected seals glow blue. Choose None of these if all three properties fail; leaving all seals untouched is not a submitted answer.")+"\n\nUse Hand / E at crystals and seals, or the enabled nearby buttons. A wrong answer costs ten seconds and keeps the current work. Reading keeps the world and active timer running.":s.phase==2?"The team restored the Mapping Conduit.\n\n"+s.reveal:s.round==0?"Five challenges: build a relation, connect ordered pairs, complete a bijection, classify a diagram, and identify its properties.\n\nThe first Book activation starts a shared ten-minute clock. The remaining time pauses between solved challenges and resumes at the next Book activation, matching solo.\n\nWrong answers retain your work, record one submitting-player mistake and deduct ten seconds.\n\nApproach the Book and use Hand / E to begin.":s.reveal+"\n\nSolved challenge kept. The remaining shared time is paused. Read at the Book to continue.";
            string key=session.State.seed+":"+s.round+":"+s.phase;if(key!=readingKey){readingKey=key;Canvas.ForceUpdateCanvases();reading.verticalNormalizedPosition=1;}
            buildControls.gameObject.SetActive(display&&build);classControls.gameObject.SetActive(display&&q.mapping.questionKind==MappingQuestionKind.ClassifyFunction);propertyControls.gameObject.SetActive(display&&q.mapping.questionKind==MappingQuestionKind.SelectProperties);introduction.gameObject.SetActive(!display);
            contextTitle.text=!display?"HOW IT WORKS":build?"NEARBY CRYSTALS":q.mapping.questionKind==MappingQuestionKind.ClassifyFunction?"VERDICT SEALS":"PROPERTY SEALS";
            for(int i=0;i<6;i++){domainButtons[i].gameObject.SetActive(i<q.inputLabels.Length);outputButtons[i].gameObject.SetActive(i<q.outputLabels.Length);domainButtons[i].GetComponentInChildren<TMP_Text>().text=i<q.inputLabels.Length?q.inputLabels[i]:"";outputButtons[i].GetComponentInChildren<TMP_Text>().text=i<q.outputLabels.Length?q.outputLabels[i]:"";}
            for(int i=0;i<4;i++){if(i<3)Tint(classButtons[i],s.selection==i);Tint(propertyButtons[i],i==3?s.selection==3:(s.flags&ArchiveMappingPresentation.PropertyValues[i])!=0);}
            compactEditLabel.text=build?"Undo":"Choose";primaryLabel.text=s.phase==2?"Explore":s.phase==1?"Check mapping":s.timerExpired?"Retry at Book":s.round==0?"Begin at Book":"Next at Book";
            feedback.text=session.TopicLockNotice(7);if(string.IsNullOrEmpty(feedback.text))feedback.text=s.feedback;feedback.color=s.lastAnswerResult<0&&s.phase==1?Red:Ink;
            for(int i=0;i<5;i++)progress[i].color=i<s.round?Teal:new Color(.2f,.4f,.35f,.2f);
        }
        static void Tint(Button button,bool selected){var colors=button.colors;colors.normalColor=selected?new Color(.72f,1,.89f):Color.white;button.colors=colors;}
        void PositionObjective(){Canvas.ForceUpdateCanvases();screen=new Vector2(Screen.width,Screen.height);safe=Screen.safeArea;canvasScale=Mathf.Max(.01f,owner.GetComponent<Canvas>().scaleFactor);float right=(Screen.width-safe.xMax)/canvasScale+28,top=(Screen.height-safe.yMax)/canvasScale+224;var corners=new Vector3[4];if(healthControl!=null){healthControl.GetWorldCorners(corners);top=Mathf.Max(top,(Screen.height-corners.Min(p=>p.y))/canvasScale+12);}float height=248;if(jumpControl!=null){jumpControl.GetWorldCorners(corners);height=Mathf.Clamp((Screen.height-top*canvasScale-corners.Max(p=>p.y))/canvasScale-16,210,248);}objective.anchoredPosition=new Vector2(-right,-top);objective.sizeDelta=new Vector2(440,height);}
        Button MakeButton(Transform parent,string value,UnityEngine.Events.UnityAction callback,Vector2 anchor,Vector2 size,int fontSize){var r=Box(value.Replace(' ','_'),parent,anchor,size,owner.buttonArt);var button=r.gameObject.AddComponent<Button>();button.onClick.AddListener(callback);r.GetComponent<Image>().raycastTarget=true;var colors=button.colors;colors.highlightedColor=new Color(1,.96f,.82f);colors.pressedColor=new Color(.76f,.83f,.77f);colors.disabledColor=new Color(.57f,.57f,.57f);button.colors=colors;var text=Label("Label",r,fontSize,owner.headingFont,new Color(1,.95f,.8f),new Vector2(.06f,.12f),new Vector2(.94f,.88f));text.text=value;text.alignment=TextAlignmentOptions.Center;return button;}
        TMP_Text Label(string name,Transform parent,float size,TMP_FontAsset font,Color color,Vector2 min,Vector2 max){var r=Rect(name,parent,min,max);var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=size;text.fontSizeMax=size;text.fontSizeMin=size-3;text.enableAutoSizing=true;text.richText=false;text.color=color;text.raycastTarget=false;text.alignment=TextAlignmentOptions.MidlineLeft;text.overflowMode=TextOverflowModes.Ellipsis;return text;}
        RectTransform Box(string name,Transform parent,Vector2 anchor,Vector2 size,Sprite sprite){var r=Rect(name,parent,anchor,anchor);r.sizeDelta=size;var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4.5f;image.raycastTarget=false;return r;}
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
    }
}
