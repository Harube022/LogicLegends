using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local T3 reader and compact objective. All actions use the existing authoritative commands.</summary>
    public sealed class ArchiveInferenceUI:MonoBehaviour
    {
        ArchiveTopicUI owner;
        RectTransform objective,reader,healthControl,jumpControl;
        TMP_Text title,timer,stage,summary,cue,readerTitle,readerTimer,premises,reference,feedback,primaryLabel,checkLabel;
        ScrollRect premiseScroll,referenceScroll;
        Button checkButton,primary,returnButton;
        readonly Image[] progress=new Image[5];
        int revision=-1;string readingKey="";Vector2 screen;Rect safe;float canvasScale;
        static readonly Color Ink=new Color(.16f,.11f,.07f),Teal=new Color(.08f,.36f,.35f),Red=new Color(.75f,.13f,.10f);
        public bool IsOpen=>reader!=null&&reader.gameObject.activeSelf;
        public bool IsObjectiveVisible=>objective!=null&&objective.gameObject.activeSelf;
        public RectTransform Objective=>objective;
        public Button CheckButton=>checkButton;
        public TMP_Text PremiseText=>premises;
        public void Initialize(ArchiveTopicUI ui){owner=ui;Build();}
        void Build(){
            objective=Box("T3_Compact_Inference_Objective",transform,Vector2.one,new Vector2(440,248),owner.statusArt);objective.pivot=Vector2.one;
            title=Label("Challenge_Progress",objective,22,owner.headingFont,Ink,new Vector2(.06f,.82f),new Vector2(.73f,.94f));
            timer=Label("Shared_T3_Timer",objective,23,owner.headingFont,Teal,new Vector2(.76f,.82f),new Vector2(.94f,.94f));timer.alignment=TextAlignmentOptions.MidlineRight;
            stage=Label("Inference_Stage",objective,23,owner.headingFont,Ink,new Vector2(.06f,.65f),new Vector2(.94f,.82f));
            summary=Label("Team_Arrangement",objective,20,owner.textFont,Teal,new Vector2(.06f,.41f),new Vector2(.94f,.65f));
            cue=Label("Hand_Action",objective,17,owner.textFont,Teal,new Vector2(.06f,.265f),new Vector2(.94f,.41f));
            MakeButton(objective,"Read",()=>owner.Open(owner.session.stations[2]),new Vector2(.23f,.155f),new Vector2(120,54),21);
            checkButton=MakeButton(objective,"Check words",Submit,new Vector2(.66f,.155f),new Vector2(206,54),22);checkLabel=checkButton.GetComponentInChildren<TMP_Text>();
            foreach(var r in FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None)){if(r.name=="JumpButton")jumpControl=r;else if(r.name=="Khezu_Existing_Health_System")healthControl=r;}
            PositionObjective();
            reader=Box("T3_Inference_Constellation_Reader",transform,new Vector2(.5f,.52f),new Vector2(1120,800),owner.panelArt);
            readerTitle=Label("Topic_And_Stage",reader,30,owner.headingFont,Ink,new Vector2(.055f,.845f),new Vector2(.72f,.95f));
            MakeButton(reader,"Close",owner.Close,new Vector2(.85f,.90f),new Vector2(230,82),27);
            readerTimer=Label("Team_Clock",reader,23,owner.textFont,Teal,new Vector2(.55f,.795f),new Vector2(.94f,.835f));readerTimer.alignment=TextAlignmentOptions.MidlineRight;
            var left=Label("Premise_Heading",reader,23,owner.headingFont,Ink,new Vector2(.065f,.745f),new Vector2(.59f,.80f));left.text="TEAM PREMISES";
            var right=Label("Reference_Heading",reader,23,owner.headingFont,Teal,new Vector2(.63f,.745f),new Vector2(.94f,.80f));right.text="FIELD GUIDE · SCROLL";
            premiseScroll=ReadingArea("Premises",reader,new Vector2(.065f,.285f),new Vector2(.59f,.745f),26,Ink,out premises);
            referenceScroll=ReadingArea("Rule_Reference",reader,new Vector2(.63f,.285f),new Vector2(.94f,.745f),23,Teal,out reference);
            feedback=Label("Shared_Feedback",reader,22,owner.textFont,Ink,new Vector2(.065f,.155f),new Vector2(.94f,.28f));
            primary=MakeButton(reader,"Begin at Book",Primary,new Vector2(.70f,.075f),new Vector2(300,82),25);primaryLabel=primary.GetComponentInChildren<TMP_Text>();
            returnButton=MakeButton(reader,"Return item",()=>owner.session.Request(2,ArchiveAction.ReturnItem),new Vector2(.35f,.075f),new Vector2(225,82),24);
            for(int i=0;i<5;i++){var r=Rect("Completed_Inference_"+(i+1),reader,new Vector2(.065f+i*.032f,.06f),new Vector2(.09f+i*.032f,.085f));progress[i]=r.gameObject.AddComponent<Image>();progress[i].raycastTarget=false;}
            reader.gameObject.SetActive(false);objective.gameObject.SetActive(false);
        }
        public void Open(){reader.gameObject.SetActive(true);revision=-1;Refresh();}
        public void Close(){if(reader!=null)reader.gameObject.SetActive(false);}
        void Primary(){var s=owner.session.State.stations[2];if(s.phase==0)owner.session.Request(2,ArchiveAction.Start);else if(s.phase==1)Submit();else owner.Close();}
        void Submit(){if(owner.session.Ready&&ArchiveInferencePresentation.Filled(owner.session.State.stations[2],owner.session.Question(2)))owner.session.Request(2,ArchiveAction.Submit);}
        void Update(){
            if(owner==null||owner.session==null||!owner.session.Ready)return;var session=owner.session;
            bool near=Player.LocalInstance!=null&&session.CanAct(session.LocalActor)&&Time.timeScale>0&&Vector3.Distance(Player.LocalInstance.transform.position,session.stations[2].transform.position)<34;
            objective.gameObject.SetActive(near&&owner.ReadingStation==null);
            if(!near){if(IsOpen)owner.Close();return;}
            if(screen!=new Vector2(Screen.width,Screen.height)||safe!=Screen.safeArea||!Mathf.Approximately(canvasScale,owner.GetComponent<Canvas>().scaleFactor))PositionObjective();
            if(revision!=session.State.revision){revision=session.State.revision;Refresh();}
            var s=session.State.stations[2];var q=session.Question(2);bool words=ArchiveInferencePresentation.IsPremises(q);
            int seconds=Mathf.CeilToInt(session.TimerRemaining(s));string value=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
            timer.text=value;readerTimer.text="TEAM TIME  "+value;timer.color=readerTimer.color=s.timerExpired||s.timerRunning&&seconds<=30?Red:Teal;
            bool canStart=session.CanStartTopic(2)&&session.Near(session.LocalActor,2,ArchiveTopicStation.BookPosition,ArchiveTopicSession.BookUseRadius);
            bool filled=ArchiveInferencePresentation.Filled(s,q);primary.interactable=s.phase==2||s.phase==0&&canStart||filled;checkButton.interactable=filled;
            returnButton.gameObject.SetActive(s.carries.Any(c=>c.actor==session.LocalActor));
            if(!string.IsNullOrEmpty(session.TopicLockNotice(2))){cue.text="Another topic is active";primary.interactable=checkButton.interactable=false;return;}
            if(s.phase==1){
                var focus=owner.FocusedPoint;var held=s.carries.FirstOrDefault(c=>c.actor==session.LocalActor);
                bool recent=s.lastAnswerResult<0&&ArchiveTopicSession.Clock-s.lastAnswerAt<2.5;
                cue.text=recent?"Incorrect · your arrangement is kept":focus!=null&&focus.station.topic==2?"Hand / E · "+focus.caption:held!=null?"Carrying "+q.items[held.item]:filled?words?"Premises filled · check words":"Crystal ready · confirm rule":words?"Take a word tablet · Hand / E":"Read a crystal name · Hand / E";
            }else if(s.phase==0)cue.text=canStart?"Book Statue · Hand / E":"Return to the Book Statue";
        }
        void Refresh(){
            var session=owner.session;if(!session.Ready)return;var s=session.State.stations[2];var q=session.Question(2);bool words=ArchiveInferencePresentation.IsPremises(q);int challenge=Mathf.Min(5,s.round+1);
            title.text=s.phase==2?"T3 · TEAM COMPLETE 5 / 5":"T3 · CHALLENGE "+challenge+" / 5";
            stage.text=s.phase==2?"Constellation restored":s.timerExpired?"Time up · work kept":s.phase==0?"Read the Book to continue":words?"1 / 2 · Restore premises":"2 / 2 · Choose Rule Crystal";
            int filled=s.placements.Count(p=>p>=0);bool display=s.phase==1||s.timerExpired;
            summary.text=!display?s.phase==2?"Five shared team challenges solved":"Premise words → rule → conclusion":words?filled+" / "+q.slots.Length+" numbered blanks filled":"Altar: "+(filled==0?"empty":q.items[s.placements[0]]);
            readerTitle.text="T3  RULES OF INFERENCE\n"+(s.phase==2?"Team complete · 5 / 5":"Challenge "+challenge+" / 5 · "+(words?"Restore premises":"Choose the rule"));
            string key=session.State.seed+":"+s.round+":"+s.subphase+":"+s.phase;if(key!=readingKey){readingKey=key;premiseScroll.verticalNormalizedPosition=referenceScroll.verticalNormalizedPosition=1;}
            premises.text=display?ArchiveInferencePresentation.Premises(q,s)+"\n\n"+(words?"Match each numbered blank to a word tablet. Numbering follows the reading order above.":"The premise words are restored. Choose the rule justified by these premises. The conclusion stays sealed until the correct crystal is submitted."):s.phase==2?"All five inference challenges are complete.\n\n"+s.reveal:s.timerExpired?"Return to the Book to retry.":s.round==0?"Restore the missing connector words in each premise, then bring the matching Rule Crystal to the altar.\n\nUse Hand / E to read, take and place. Your team shares every placement.":"Challenge "+s.round+" completed.\n\n"+s.reveal+"\n\nUse the Book to start the next challenge.";
            reference.text=words?"WORD TABLETS\n\n"+string.Join("\n",q.items.GroupBy(w=>w.ToUpperInvariant()).Select(g=>g.Key+"  × "+g.Count()))+"\n\nTake one tablet, approach a numbered socket, and use Hand / E. You can lift a placed tablet to correct it.\n\nCheck only when all blanks are filled. Duplicate words are interchangeable.":string.Join("\n\n",session.catalog.inferenceRules.Select(r=>r.abbreviation+" · "+r.ruleName+"\n"+r.instruction));
            feedback.text=session.TopicLockNotice(2);if(string.IsNullOrEmpty(feedback.text))feedback.text=s.feedback;
            feedback.color=s.lastAnswerResult<0&&s.phase==1?Red:Ink;
            checkLabel.text=words?"Check words":"Confirm rule";primaryLabel.text=s.phase==2?"Explore":s.phase==1?checkLabel.text:s.timerExpired?"Retry at Book":s.round==0?"Begin at Book":"Next at Book";
            for(int i=0;i<5;i++)progress[i].color=i<s.round?Teal:new Color(.2f,.4f,.35f,.2f);
            if(!string.IsNullOrEmpty(session.TopicLockNotice(2))){stage.text="Finish the active topic first";primaryLabel.text="Topic in progress";}
        }
        void PositionObjective(){
            Canvas.ForceUpdateCanvases();screen=new Vector2(Screen.width,Screen.height);safe=Screen.safeArea;canvasScale=Mathf.Max(.01f,owner.GetComponent<Canvas>().scaleFactor);
            float right=(Screen.width-safe.xMax)/canvasScale+28,top=(Screen.height-safe.yMax)/canvasScale+224;var corners=new Vector3[4];
            if(healthControl!=null){healthControl.GetWorldCorners(corners);top=Mathf.Max(top,(Screen.height-corners.Min(p=>p.y))/canvasScale+12);}
            float height=248;if(jumpControl!=null){jumpControl.GetWorldCorners(corners);height=Mathf.Clamp((Screen.height-top*canvasScale-corners.Max(p=>p.y))/canvasScale-16,210,248);}
            objective.anchoredPosition=new Vector2(-right,-top);objective.sizeDelta=new Vector2(440,height);
        }
        ScrollRect ReadingArea(string name,Transform parent,Vector2 min,Vector2 max,int size,Color color,out TMP_Text text){
            var view=Rect(name+"_Scroll",parent,min,max);view.gameObject.AddComponent<RectMask2D>();var hit=view.gameObject.AddComponent<Image>();hit.color=new Color(.07f,.29f,.28f,.055f);
            var scroll=view.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            var content=Rect("Text_Content",view,new Vector2(0,1),new Vector2(1,1));content.pivot=new Vector2(.5f,1);content.offsetMin=new Vector2(10,0);content.offsetMax=new Vector2(-10,0);
            text=content.gameObject.AddComponent<TextMeshProUGUI>();text.font=owner.textFont;text.fontSize=size;text.color=color;text.richText=false;text.raycastTarget=false;text.overflowMode=TextOverflowModes.Overflow;text.margin=new Vector4(6,8,6,14);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=content;scroll.viewport=view;return scroll;
        }
        Button MakeButton(Transform parent,string value,UnityEngine.Events.UnityAction callback,Vector2 anchor,Vector2 size,int fontSize){
            var r=Box(value.Replace(' ','_'),parent,anchor,size,owner.buttonArt);var b=r.gameObject.AddComponent<Button>();b.onClick.AddListener(callback);r.GetComponent<Image>().raycastTarget=true;
            var c=b.colors;c.highlightedColor=new Color(1,.96f,.82f);c.pressedColor=new Color(.76f,.83f,.77f);c.disabledColor=new Color(.57f,.57f,.57f);b.colors=c;
            var t=Label("Label",r,fontSize,owner.headingFont,new Color(1,.95f,.8f),new Vector2(.06f,.12f),new Vector2(.94f,.88f));t.text=value;t.alignment=TextAlignmentOptions.Center;return b;
        }
        TMP_Text Label(string name,Transform parent,float size,TMP_FontAsset font,Color color,Vector2 min,Vector2 max){
            var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.fontSizeMax=size;t.fontSizeMin=size-3;t.enableAutoSizing=true;t.richText=false;t.color=color;t.raycastTarget=false;t.alignment=TextAlignmentOptions.MidlineLeft;t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        RectTransform Box(string name,Transform parent,Vector2 anchor,Vector2 size,Sprite sprite){var r=Rect(name,parent,anchor,anchor);r.sizeDelta=size;var i=r.gameObject.AddComponent<Image>();i.sprite=sprite;i.type=Image.Type.Sliced;i.pixelsPerUnitMultiplier=4.5f;i.raycastTarget=false;return r;}
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
    }
}
