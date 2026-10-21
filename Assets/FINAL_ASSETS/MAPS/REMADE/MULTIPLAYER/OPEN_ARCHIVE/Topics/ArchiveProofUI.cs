using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Readable T4 manuscripts and compact objective; no local puzzle or timer authority.</summary>
    public sealed class ArchiveProofUI:MonoBehaviour
    {
        ArchiveTopicUI owner;RectTransform objective,reader,itemContent,healthControl,jumpControl;
        TMP_Text title,timer,stage,summary,cue,readerTitle,readerTimer,body,feedback,primaryLabel;
        ScrollRect bodyScroll,itemScroll;Button checkButton,primary,returnButton;
        readonly List<Button> entries=new List<Button>();readonly List<TMP_Text> entryLabels=new List<TMP_Text>();
        readonly Image[] progress=new Image[5];
        int revision=-1,selected=-1;string readingKey="";Vector2 screen;Rect safe;float canvasScale;
        static readonly Color Ink=new Color(.16f,.11f,.07f),Teal=new Color(.08f,.36f,.35f),Red=new Color(.75f,.13f,.10f);
        public bool IsOpen=>reader!=null&&reader.gameObject.activeSelf;
        public bool IsObjectiveVisible=>objective!=null&&objective.gameObject.activeSelf;
        public RectTransform Objective=>objective;
        public Button CheckButton=>checkButton;
        public TMP_Text ReadingText=>body;
        public void Initialize(ArchiveTopicUI ui){owner=ui;Build();}
        void Build(){
            objective=Box("T4_Compact_Proof_Objective",transform,Vector2.one,new Vector2(440,248),owner.statusArt);objective.pivot=Vector2.one;
            title=Label("Challenge_Progress",objective,22,owner.headingFont,Ink,new Vector2(.06f,.82f),new Vector2(.73f,.94f));
            timer=Label("Shared_T4_Timer",objective,23,owner.headingFont,Teal,new Vector2(.76f,.82f),new Vector2(.94f,.94f));timer.alignment=TextAlignmentOptions.MidlineRight;
            stage=Label("Proof_Activity",objective,23,owner.headingFont,Ink,new Vector2(.06f,.65f),new Vector2(.94f,.82f));
            summary=Label("Team_Arrangement",objective,20,owner.textFont,Teal,new Vector2(.06f,.41f),new Vector2(.94f,.65f));
            cue=Label("Hand_Action",objective,17,owner.textFont,Teal,new Vector2(.06f,.265f),new Vector2(.94f,.41f));
            MakeButton(objective,"Read",ReadFocused,new Vector2(.23f,.155f),new Vector2(120,54),21);
            checkButton=MakeButton(objective,"Check proof",Submit,new Vector2(.66f,.155f),new Vector2(206,54),22);
            foreach(var r in FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None)){if(r.name=="JumpButton")jumpControl=r;else if(r.name=="Khezu_Existing_Health_System")healthControl=r;}
            PositionObjective();
            reader=Box("T4_Scribes_Court_Reader",transform,new Vector2(.5f,.52f),new Vector2(1120,800),owner.panelArt);
            readerTitle=Label("Topic_And_Challenge",reader,29,owner.headingFont,Ink,new Vector2(.055f,.845f),new Vector2(.72f,.95f));
            MakeButton(reader,"Close",owner.Close,new Vector2(.85f,.90f),new Vector2(230,82),27);
            readerTimer=Label("Team_Clock",reader,23,owner.textFont,Teal,new Vector2(.55f,.795f),new Vector2(.94f,.835f));readerTimer.alignment=TextAlignmentOptions.MidlineRight;
            var left=Label("Reading_Heading",reader,23,owner.headingFont,Ink,new Vector2(.065f,.745f),new Vector2(.59f,.80f));left.text="PROOF READER · SCROLL";
            var right=Label("Manuscript_Heading",reader,23,owner.headingFont,Teal,new Vector2(.63f,.745f),new Vector2(.94f,.80f));right.text="MANUSCRIPTS · TAP TO READ";
            bodyScroll=Scroll("Proof_Reading",reader,new Vector2(.065f,.285f),new Vector2(.59f,.745f),out var content);
            body=content.gameObject.AddComponent<TextMeshProUGUI>();body.font=owner.textFont;body.fontSize=25;body.color=Ink;body.richText=false;body.raycastTarget=false;body.overflowMode=TextOverflowModes.Overflow;body.margin=new Vector4(6,8,6,14);content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            itemScroll=Scroll("Manuscript_List",reader,new Vector2(.63f,.285f),new Vector2(.94f,.745f),out itemContent);
            var layout=itemContent.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(4,4,8,10);layout.spacing=8;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
            itemContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            feedback=Label("Shared_Feedback",reader,22,owner.textFont,Ink,new Vector2(.065f,.155f),new Vector2(.94f,.28f));
            MakeButton(reader,"Overview",()=>SelectItem(-1),new Vector2(.20f,.075f),new Vector2(210,82),24);
            returnButton=MakeButton(reader,"Return item",()=>owner.session.Request(3,ArchiveAction.ReturnItem),new Vector2(.44f,.075f),new Vector2(225,82),24);
            primary=MakeButton(reader,"Begin at Book",Primary,new Vector2(.75f,.075f),new Vector2(300,82),25);primaryLabel=primary.GetComponentInChildren<TMP_Text>();
            for(int i=0;i<5;i++){var r=Rect("Accepted_Proof_"+(i+1),reader,new Vector2(.065f+i*.032f,.13f),new Vector2(.09f+i*.032f,.145f));progress[i]=r.gameObject.AddComponent<Image>();progress[i].raycastTarget=false;}
            reader.gameObject.SetActive(false);objective.gameObject.SetActive(false);
        }
        public void Open(){selected=-1;reader.gameObject.SetActive(true);revision=-1;Refresh();}
        public void Close(){if(reader!=null)reader.gameObject.SetActive(false);}
        public void SelectItem(int index){selected=index;Refresh();Canvas.ForceUpdateCanvases();bodyScroll.verticalNormalizedPosition=1;}
        void ReadFocused(){
            var s=owner.session.State.stations[3];var held=s.carries.FirstOrDefault(c=>c.actor==owner.session.LocalActor);var focus=owner.FocusedPoint;
            int item=held!=null?held.item:focus!=null&&focus.station.topic==3&&focus.role==ArchivePointRole.Item?focus.index:-1;
            owner.Open(owner.session.stations[3]);if(s.phase==1||s.timerExpired)SelectItem(item);
        }
        void Primary(){var s=owner.session.State.stations[3];if(s.phase==0)owner.session.Request(3,ArchiveAction.Start);else if(s.phase==1)Submit();else owner.Close();}
        void Submit(){if(owner.session.Ready&&ArchiveProofPresentation.Filled(owner.session.State.stations[3],owner.session.Question(3)))owner.session.Request(3,ArchiveAction.Submit);}
        void Update(){
            if(owner==null||owner.session==null||!owner.session.Ready)return;var session=owner.session;
            bool near=Player.LocalInstance!=null&&session.CanAct(session.LocalActor)&&Time.timeScale>0&&Vector3.Distance(Player.LocalInstance.transform.position,session.stations[3].transform.position)<34;
            objective.gameObject.SetActive(near&&owner.ReadingStation==null);if(!near){if(IsOpen)owner.Close();return;}
            if(screen!=new Vector2(Screen.width,Screen.height)||safe!=Screen.safeArea||!Mathf.Approximately(canvasScale,owner.GetComponent<Canvas>().scaleFactor))PositionObjective();
            if(revision!=session.State.revision){revision=session.State.revision;Refresh();}
            var s=session.State.stations[3];var q=session.Question(3);int seconds=Mathf.CeilToInt(session.TimerRemaining(s));string value=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
            timer.text=value;readerTimer.text="TEAM TIME  "+value;timer.color=readerTimer.color=s.timerExpired||s.timerRunning&&seconds<=30?Red:Teal;
            bool start=session.CanStartTopic(3)&&session.Near(session.LocalActor,3,ArchiveTopicStation.BookPosition,ArchiveTopicSession.BookUseRadius);bool filled=ArchiveProofPresentation.Filled(s,q);
            primary.interactable=s.phase==2||s.phase==0&&start||filled;checkButton.interactable=filled;returnButton.gameObject.SetActive(s.carries.Any(c=>c.actor==session.LocalActor));
            if(!string.IsNullOrEmpty(session.TopicLockNotice(3))){cue.text="Another topic is active";primary.interactable=checkButton.interactable=false;return;}
            if(s.phase==1){var focus=owner.FocusedPoint;var held=s.carries.FirstOrDefault(c=>c.actor==session.LocalActor);bool recent=s.lastAnswerResult<0&&ArchiveTopicSession.Clock-s.lastAnswerAt<2.5;
                cue.text=recent?"Incorrect · your arrangement is kept":focus!=null&&focus.station.topic==3?focus.role==ArchivePointRole.Item?"Hand / E · take "+q.items[focus.index]+" · Read for text":focus.role==ArchivePointRole.Slot?"Hand / E · "+q.slots[focus.index]:"Book · Hand / E":held!=null?"Carrying "+q.items[held.item]:filled?"Arrangement ready · Check proof":"Read a manuscript, then carry it · Hand / E";
            }else if(s.phase==0)cue.text=start?"Book Statue · Hand / E":"Return to the Book Statue";
        }
        string Location(int item,ArchiveQuestion q,ArchiveStationState state){
            if(state.phase!=1&&!state.timerExpired)return "At its stand";var held=state.carries.FirstOrDefault(c=>c.item==item);if(held!=null)return "Carried by "+owner.session.ActorName(held.actor);
            int slot=q.regionPlacement?(item<state.placements.Length?state.placements[item]:-1):System.Array.IndexOf(state.placements,item);return slot<0?"At its stand":q.slots[slot];
        }
        void Refresh(){
            var session=owner.session;if(!session.Ready)return;var s=session.State.stations[3];var q=session.Question(3);int challenge=Mathf.Min(5,s.round+1);bool display=s.phase==1||s.timerExpired;
            string key=session.State.seed+":"+s.round+":"+s.phase;if(key!=readingKey){readingKey=key;selected=-1;BuildEntries(q);Canvas.ForceUpdateCanvases();bodyScroll.verticalNormalizedPosition=itemScroll.verticalNormalizedPosition=1;}
            title.text=s.phase==2?"T4 · TEAM COMPLETE 5 / 5":"T4 · CHALLENGE "+challenge+" / 5";
            stage.text=s.phase==2?"Scribes' Court restored":s.timerExpired?"Time up · work kept":s.phase==0?"Read the Book to continue":ArchiveProofPresentation.KindLabel(q);
            summary.text=display?s.placements.Count(p=>p>=0)+" / "+q.PlacementCount+(q.regionPlacement?" scrolls classified":" sockets filled"):s.phase==2?"Five shared proof challenges complete":"Scrolls → proof steps → justifications";
            readerTitle.text="T4  FORMAL & INFORMAL PROOFS\n"+(s.phase==2?"Team complete · 5 / 5":"Challenge "+challenge+" / 5 · "+ArchiveProofPresentation.KindLabel(q));
            body.text=!display?s.phase==2?"Your team restored the Scribes' Court.\n\n"+s.reveal:s.round==0?"Read and classify valid formal and informal proof scrolls. Then order statements, restore a missing rule, and build a justified formal proof.\n\nUse Hand / E at the Book to begin. Reading alone never starts the clock.":"Challenge "+s.round+" accepted.\n\n"+s.reveal+"\n\nReturn to the Book for the next challenge.":selected>=0&&selected<q.items.Length?q.items[selected].ToUpperInvariant()+"\n"+Location(selected,q,s)+"\n\n"+ArchiveProofPresentation.ItemText(selected,q)+"\n\nUse Hand / E to take this manuscript and place it in the court.":q.title+"\n\n"+q.prompt+"\n\n"+Instructions(q)+"\n\nTEAM ARRANGEMENT\n"+ArchiveProofPresentation.Arrangement(q,s);
            for(int i=0;i<entries.Count;i++){int item=q.itemOrder[i];entryLabels[i].text=q.items[item]+"\n"+Location(item,q,s);var colors=entries[i].colors;colors.normalColor=item==selected?new Color(1,.90f,.65f):Color.white;entries[i].colors=colors;}
            feedback.text=session.TopicLockNotice(3);if(string.IsNullOrEmpty(feedback.text))feedback.text=s.feedback;feedback.color=s.lastAnswerResult<0&&s.phase==1?Red:Ink;
            primaryLabel.text=s.phase==2?"Explore":s.phase==1?"Check proof":s.timerExpired?"Retry at Book":s.round==0?"Begin at Book":"Next at Book";
            for(int i=0;i<5;i++)progress[i].color=i<s.round?Teal:new Color(.2f,.4f,.35f,.2f);
            if(!string.IsNullOrEmpty(session.TopicLockNotice(3))){stage.text="Finish the active topic first";primaryLabel.text="Topic in progress";}
        }
        public static string Instructions(ArchiveQuestion q)=>q.proof.kind==ProofChallengeKind.SortScrolls?"Both proof styles can be valid. Formal proofs use explicit steps and justifications; informal proofs explain the reasoning in ordinary language. Read each scroll, carry it to FORMAL or INFORMAL, and place it with Hand / E.":q.proof.kind==ProofChallengeKind.MissingRule?"Read the incomplete proof. Carry the rule seal that supplies the missing justification to its socket.":q.proof.kind==ProofChallengeKind.OrderSteps?"Read the statement manuscripts. Fill numbered STEP sockets in a valid order: every dependency must appear before the step that uses it.":"Read the informal explanation. Fill each STEP socket with a statement and its paired RULE socket with a matching justification. Equal justifications, including Premise, are interchangeable. Dependencies still come first.";
        void BuildEntries(ArchiveQuestion q){
            foreach(Transform child in itemContent){child.gameObject.SetActive(false);Destroy(child.gameObject);}entries.Clear();entryLabels.Clear();
            foreach(int value in q.itemOrder){int item=value;var r=Box("Read_"+item,itemContent,Vector2.zero,new Vector2(0,90),owner.buttonArt);r.gameObject.AddComponent<LayoutElement>().preferredHeight=90;r.GetComponent<Image>().raycastTarget=true;var button=r.gameObject.AddComponent<Button>();button.onClick.AddListener(()=>SelectItem(item));entries.Add(button);
                var t=Label("Manuscript_And_Location",r,23,owner.textFont,new Color(1,.95f,.8f),new Vector2(.07f,.10f),new Vector2(.93f,.90f));t.alignment=TextAlignmentOptions.Center;entryLabels.Add(t);}
        }
        void PositionObjective(){
            Canvas.ForceUpdateCanvases();screen=new Vector2(Screen.width,Screen.height);safe=Screen.safeArea;canvasScale=Mathf.Max(.01f,owner.GetComponent<Canvas>().scaleFactor);
            float right=(Screen.width-safe.xMax)/canvasScale+28,top=(Screen.height-safe.yMax)/canvasScale+224;var corners=new Vector3[4];if(healthControl!=null){healthControl.GetWorldCorners(corners);top=Mathf.Max(top,(Screen.height-corners.Min(p=>p.y))/canvasScale+12);}
            float height=248;if(jumpControl!=null){jumpControl.GetWorldCorners(corners);height=Mathf.Clamp((Screen.height-top*canvasScale-corners.Max(p=>p.y))/canvasScale-16,210,248);}objective.anchoredPosition=new Vector2(-right,-top);objective.sizeDelta=new Vector2(440,height);
        }
        ScrollRect Scroll(string name,Transform parent,Vector2 min,Vector2 max,out RectTransform content){
            var view=Rect(name+"_Scroll",parent,min,max);view.gameObject.AddComponent<RectMask2D>();var hit=view.gameObject.AddComponent<Image>();hit.color=new Color(.07f,.29f,.28f,.055f);var scroll=view.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            content=Rect("Content",view,new Vector2(0,1),new Vector2(1,1));content.pivot=new Vector2(.5f,1);content.offsetMin=new Vector2(10,0);content.offsetMax=new Vector2(-10,0);scroll.content=content;scroll.viewport=view;return scroll;
        }
        Button MakeButton(Transform parent,string value,UnityEngine.Events.UnityAction callback,Vector2 anchor,Vector2 size,int fontSize){
            var r=Box(value.Replace(' ','_'),parent,anchor,size,owner.buttonArt);var b=r.gameObject.AddComponent<Button>();b.onClick.AddListener(callback);r.GetComponent<Image>().raycastTarget=true;var c=b.colors;c.highlightedColor=new Color(1,.96f,.82f);c.pressedColor=new Color(.76f,.83f,.77f);c.disabledColor=new Color(.57f,.57f,.57f);b.colors=c;
            var t=Label("Label",r,fontSize,owner.headingFont,new Color(1,.95f,.8f),new Vector2(.06f,.12f),new Vector2(.94f,.88f));t.text=value;t.alignment=TextAlignmentOptions.Center;return b;
        }
        TMP_Text Label(string name,Transform parent,float size,TMP_FontAsset font,Color color,Vector2 min,Vector2 max){var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.fontSizeMax=size;t.fontSizeMin=size-3;t.enableAutoSizing=true;t.richText=false;t.color=color;t.raycastTarget=false;t.alignment=TextAlignmentOptions.MidlineLeft;t.overflowMode=TextOverflowModes.Ellipsis;return t;}
        RectTransform Box(string name,Transform parent,Vector2 anchor,Vector2 size,Sprite sprite){var r=Rect(name,parent,anchor,anchor);r.sizeDelta=size;var i=r.gameObject.AddComponent<Image>();i.sprite=sprite;i.type=Image.Type.Sliced;i.pixelsPerUnitMultiplier=4.5f;i.raycastTarget=false;return r;}
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
    }
}
