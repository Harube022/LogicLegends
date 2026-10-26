using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local T1 reader and objective card; never pauses or intercepts movement input.</summary>
    public sealed class ArchivePropositionUI : MonoBehaviour
    {
        ArchiveTopicUI owner;
        RectTransform objective,reader,choices;
        TMP_Text objectiveTitle,objectiveQuestion,objectiveState,title,question,instructions,feedback,explanation;
        TMP_Text objectiveTimer,readerTimer;
        readonly TMP_Text[] choiceLabels=new TMP_Text[4];
        readonly Image[] choiceFaces=new Image[4];
        readonly Image[] progressPips=new Image[5];
        Image channelFill;
        Button action;
        TMP_Text actionLabel;
        int revision=-1;
        RectTransform jumpControl,healthControl;
        Vector2 lastScreen;
        Rect lastSafeArea;
        float lastCanvasScale;
        static readonly Color Ink=new Color(.16f,.11f,.07f),Teal=new Color(.10f,.32f,.31f),Green=new Color(.14f,.39f,.24f),Red=new Color(.60f,.15f,.12f);
        public bool IsOpen => reader!=null&&reader.gameObject.activeSelf;
        public bool IsObjectiveVisible => objective!=null&&objective.gameObject.activeInHierarchy;
        public void Initialize(ArchiveTopicUI source)
        {
            owner=source;
            objective=Box("T1_Current_Challenge",transform,new Vector2(1,1),new Vector2(430,248),new Vector2(-28,-224),owner.statusArt);
            objective.pivot=Vector2.one;
            objectiveTitle=Label("Round_And_Team",objective,22,owner.headingFont,Ink,new Vector2(.06f,.80f),new Vector2(.73f,.94f));
            objectiveTimer=Label("Shared_T1_Timer",objective,23,owner.headingFont,Teal,new Vector2(.75f,.80f),new Vector2(.94f,.94f));objectiveTimer.alignment=TextAlignmentOptions.MidlineRight;
            objectiveQuestion=Label("Current_Question",objective,23,owner.textFont,Ink,new Vector2(.06f,.37f),new Vector2(.94f,.78f));
            objectiveState=Label("Channel_And_Outcome",objective,19,owner.textFont,Teal,new Vector2(.06f,.14f),new Vector2(.61f,.33f));
            var track=Flat("Channel_Track",objective,new Color(.21f,.34f,.30f,.18f),new Vector2(.07f,.10f),new Vector2(.60f,.125f));
            channelFill=Flat("Channel_Fill",track,new Color(.14f,.52f,.45f),Vector2.zero,Vector2.one).GetComponent<Image>();
            channelFill.type=Image.Type.Filled;channelFill.fillMethod=Image.FillMethod.Horizontal;channelFill.fillAmount=0;
            var read=MakeButton(objective,"Read",()=>owner.Open(owner.session.stations[0]),new Vector2(.80f,.205f),new Vector2(144,64));
            var readText=read.GetComponentInChildren<TMP_Text>();readText.fontSizeMax=readText.fontSize=23;readText.fontSizeMin=21;
            foreach(var rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(rect.name=="JumpButton")jumpControl=rect;
                else if(rect.name=="Khezu_Existing_Health_System")healthControl=rect;
            }
            PositionObjective();
            reader=Box("T1_Question_Reader",transform,new Vector2(.5f,.53f),new Vector2(1180,740),Vector2.zero,owner.panelArt);
            title=Label("Topic_And_Round",reader,32,owner.headingFont,Ink,new Vector2(.055f,.84f),new Vector2(.72f,.94f));
            MakeButton(reader,"Close",owner.Close,new Vector2(.84f,.90f),new Vector2(240,88));
            readerTimer=Label("Shared_T1_Timer",reader,25,owner.textFont,Teal,new Vector2(.65f,.785f),new Vector2(.935f,.825f));readerTimer.alignment=TextAlignmentOptions.MidlineRight;
            question=Label("Question",reader,32,owner.textFont,Ink,new Vector2(.065f,.64f),new Vector2(.935f,.775f));
            choices=Rect("World_Rune_Choices",reader,new Vector2(.065f,.32f),new Vector2(.935f,.62f));
            for(int i=0;i<4;i++)
            {
                float x=i%2*.51f,y=i<2?.53f:0;
                var face=Box("Rune_"+(char)('A'+i)+"_Read_Only",choices,Vector2.zero,Vector2.zero,Vector2.zero,owner.statusArt);
                Stretch(face,new Vector2(x,y),new Vector2(x+.49f,y+.47f));
                choiceFaces[i]=face.GetComponent<Image>();
                choiceLabels[i]=Label("Letter_Symbol_Answer",face,30,owner.textFont,Teal,new Vector2(.065f,.15f),new Vector2(.935f,.85f));
            }
            explanation=Label("Solved_Explanation",reader,30,owner.textFont,Green,new Vector2(.065f,.36f),new Vector2(.935f,.63f));
            instructions=Label("How_To_Channel",reader,23,owner.textFont,Teal,new Vector2(.065f,.245f),new Vector2(.935f,.315f));
            feedback=Label("Team_Feedback",reader,25,owner.textFont,Ink,new Vector2(.065f,.14f),new Vector2(.935f,.235f));
            var button=MakeButton(reader,"Return to runes",Primary,new Vector2(.50f,.075f),new Vector2(300,88));action=button.GetComponent<Button>();actionLabel=button.GetComponentInChildren<TMP_Text>();
            for(int i=0;i<5;i++)
            {
                var pip=Flat("Completed_Challenge_"+(i+1),reader,Teal,new Vector2(.07f+i*.038f,.087f),new Vector2(.098f+i*.038f,.105f));
                progressPips[i]=pip.GetComponent<Image>();
            }
            reader.gameObject.SetActive(false);objective.gameObject.SetActive(false);
        }
        public void Open(){reader.gameObject.SetActive(true);revision=-1;Refresh();}
        public void Close(){if(reader!=null)reader.gameObject.SetActive(false);}
        void Primary()
        {
            var session=owner.session;if(!session.Ready)return;
            var state=session.State.stations[0];
            if(state.phase==0)session.Request(0,ArchiveAction.Start);
            else owner.Close();
        }
        void Update()
        {
            if(owner==null||owner.session==null||!owner.session.Ready)return;
            if(lastScreen!=new Vector2(Screen.width,Screen.height)||lastSafeArea!=Screen.safeArea||!Mathf.Approximately(lastCanvasScale,owner.GetComponent<Canvas>().scaleFactor))PositionObjective();
            var session=owner.session;
            bool near=Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,session.stations[0].transform.position)<34&&session.CanAct(session.LocalActor)&&Time.timeScale>0;
            objective.gameObject.SetActive(near&&owner.ReadingStation==null);
            if(!near){if(IsOpen)owner.Close();return;}
            var state=session.State.stations[0];
            bool relayPending=session.T1RelayPending;
            float remaining=session.TimerRemaining(state);int seconds=Mathf.CeilToInt(remaining);
            string timer=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
            objectiveTimer.gameObject.SetActive(!relayPending);readerTimer.gameObject.SetActive(!relayPending);
            objectiveTimer.text=timer;readerTimer.text="TEAM TIME  "+timer;
            objectiveTimer.color=readerTimer.color=state.timerExpired||(state.timerRunning&&remaining<=30)?Red:Teal;
            if(revision!=session.State.revision){revision=session.State.revision;Refresh();}
            string lockNotice=session.TopicLockNotice(0);
            if(IsOpen)
            {
                action.interactable=state.phase!=0||(session.CanStartTopic(0)&&session.Near(session.LocalActor,0,ArchiveTopicStation.BookPosition,ArchiveTopicSession.BookUseRadius));
                if(!string.IsNullOrEmpty(lockNotice))instructions.text=lockNotice;
                else if(state.phase==0&&!action.interactable)instructions.text="Return to the Book Statue to begin. Your movement controls remain active.";
                else if(state.phase==0)instructions.text=state.timerExpired?"Retry this challenge at the Book Statue. Solved challenges stay complete; a new six-minute attempt begins.":"Your team is ready. Use the Book Statue to begin the next challenge.";
            }
            float progress=state.channelActor==0?0:Mathf.Clamp01((float)((ArchiveTopicSession.Clock-state.channelStarted)/session.answerChannelSeconds));
            channelFill.fillAmount=progress;
            channelFill.rectTransform.anchorMax=new Vector2(progress,1);
            channelFill.transform.parent.gameObject.SetActive(!relayPending);
            if(state.channelActor!=0)
            {
                string text="Rune "+(char)('A'+state.channelChoice)+" · "+session.ActorName(state.channelActor)+" · "+Mathf.RoundToInt(progress*100)+"%";
                objectiveState.text=text;feedback.text="Channeling "+text+". Stay inside the ring; step away to cancel.";feedback.color=Teal;
            }
            else if(!relayPending&&string.IsNullOrEmpty(lockNotice)&&!state.timerExpired&&(state.lastAnswerResult==0||ArchiveTopicSession.Clock-state.lastAnswerAt>2.2))
            {
                var focus=owner.FocusedPoint;
                if(focus!=null&&focus.station.topic==0)
                    objectiveState.text=focus.role==ArchivePointRole.Book?"Book Statue\nHand / E":"Rune "+(char)('A'+focus.index)+" · Hand / E";
                else if(state.phase==1&&!state.feedback.StartsWith("Channel cancelled"))objectiveState.text="Choose a rune\nHand / E";
            }
        }
        void PositionObjective()
        {
            Canvas.ForceUpdateCanvases();
            lastScreen=new Vector2(Screen.width,Screen.height);lastSafeArea=Screen.safeArea;
            float scale=Mathf.Max(.01f,owner.GetComponent<Canvas>().scaleFactor);
            lastCanvasScale=scale;
            float right=(Screen.width-lastSafeArea.xMax)/scale+28,top=(Screen.height-lastSafeArea.yMax)/scale+224;
            if(healthControl!=null)
            {
                var corners=new Vector3[4];healthControl.GetWorldCorners(corners);
                top=Mathf.Max(top,(Screen.height-Mathf.Min(corners[0].y,corners[3].y))/scale+12);
            }
            float height=248;
            if(jumpControl!=null)
            {
                var corners=new Vector3[4];jumpControl.GetWorldCorners(corners);
                float jumpTop=Mathf.Max(corners[1].y,corners[2].y);
                height=Mathf.Clamp((Screen.height-top*scale-jumpTop)/scale-16,210,248);
            }
            objective.anchoredPosition=new Vector2(-right,-top);objective.sizeDelta=new Vector2(430,height);
        }
        void Refresh()
        {
            if(owner==null||!owner.session.Ready)return;
            var state=owner.session.State.stations[0];var q=owner.session.Question(0);
            int round=Mathf.Min(state.round+1,5);
            objectiveTitle.text=state.phase==2?"T1  ·  TEAM COMPLETE  5 / 5":"T1  ·  TEAM CHALLENGE "+round+" / 5";
            objectiveQuestion.text=state.timerExpired?"Time up. Solved challenges kept. Return to the Book Statue to retry challenge "+round+".":state.phase==1?q.prompt:state.phase==2?"Rune Choice Circle restored. Completed for the whole team.":state.round==0?"Read the Book Statue. Match each statement to its logical connective.":"Correct! Return to the Book Statue for challenge "+round+".";
            objectiveState.text=state.timerExpired?"Timeout · one mistake":state.phase==1?"Choose a world rune · Hand / E":state.phase==2?"Shared team success":"Book Statue · Hand / E";
            if(state.phase==1&&state.feedback!="Challenge active. Your team shares this work.")objectiveState.text=state.feedback.StartsWith("Rune ")?"Incorrect · one mistake recorded":state.feedback.StartsWith("Channel cancelled")?"Channel cancelled · no mistake":"Choose a world rune · Hand / E";
            title.text="T1  PROPOSITIONAL LOGIC\n"+(state.phase==2?"Rune Choice Circle · Team complete":"Rune Choice Circle · Challenge "+round+" / 5");
            question.text=state.timerExpired?"Time up. Challenge "+round+" is ready to retry. Your team's solved challenges are kept.":state.phase==1?q.prompt:state.phase==2?"Five challenges solved. The circle is restored.":state.round==0?"Identify the logical connective described by each statement.":"Challenge "+state.round+" solved by "+owner.session.ActorName(state.lastAnswerActor)+".";
            choices.gameObject.SetActive(state.phase==1);
            explanation.gameObject.SetActive(state.phase!=1);
            explanation.text=string.IsNullOrEmpty(state.reveal)?"1   Read the question with your team.\n2   Walk inside an answer rune and tap Hand / E once.\n3   Stay until the ring fills. Step away to cancel.":state.reveal;
            for(int i=0;i<4;i++)
            {
                choiceLabels[i].text=((char)('A'+i))+"    "+ArchivePropositionNotation.Symbol(q.items[i])+"    "+q.items[i];
                choiceFaces[i].color=state.channelActor!=0&&state.channelChoice==i?new Color(1,.93f,.70f):Color.white;
            }
            instructions.text=state.phase==1?"Read-only choices. Answer at a world rune; tap Hand / E once and stay in its ring.":state.phase==2?"Shared team progress is saved. Explore the remaining topic clearings.":"Use the Book Statue to begin. The world stays active while you read.";
            feedback.text=state.feedback;feedback.color=state.lastAnswerResult<0&&state.feedback.StartsWith("Rune ")?Red:Ink;
            actionLabel.text=state.timerExpired?"Retry at Book":state.phase==0?(state.round==0?"Begin at Book":"Next at Book"):state.phase==2?"Explore":"Return to runes";
            var relay=owner.session.State.coop;
            if(owner.session.T1RelayPending&&relay!=null)
            {
                string target=relay.target?"TRUE":"FALSE";
                objectiveTitle.text="T1  ·  TEAM RELAY";
                objectiveQuestion.text=relay.open?"Assignment accepted. Walk from P to Q through the relay arch to clear T1.":"TARGET: "+target+"\nSet P and Q for "+relay.connective+". Each player confirms one rune.";
                objectiveState.text=owner.session.T1RelayModeLabel;
                title.text="T1  PROPOSITIONAL LOGIC\nRequired team relay · 5 / 5 challenges solved";
                question.text="Make "+relay.connective+" evaluate "+target+". T1 clears after the passage is crossed.";
                explanation.text=!string.IsNullOrWhiteSpace(relay.explanation)?relay.explanation:
                    "1   Choose TRUE or FALSE at your P or Q rune.\n2   Confirm your own input.\n3   Reach the target output together.\n4   Walk through the open arch from P to Q.";
                instructions.text=relay.open?"Walk through the arch. Amber warns; red briefly slows your movement.":
                    relay.reducedTeam?"Set both values first, then confirm P and Q. "+owner.session.T1RelayModeLabel:
                    "One teammate per rune. Both confirm their own input. "+owner.session.T1RelayModeLabel;
                feedback.text=string.IsNullOrWhiteSpace(relay.feedback)?state.feedback:relay.feedback;feedback.color=relay.open?Green:Ink;
                actionLabel.text="Return to relay";
            }
            else if(state.phase==2&&relay!=null&&relay.policyVersion>=2&&relay.completed)
            {
                question.text="Five challenges and the team relay are complete. The circle is restored.";
                if(!string.IsNullOrWhiteSpace(relay.explanation))explanation.text=relay.explanation;
            }
            string lockNotice=owner.session.TopicLockNotice(0);
            if(!string.IsNullOrEmpty(lockNotice))
            {
                objectiveTitle.text="T1  ·  WAITING FOR TEAM";objectiveQuestion.text=question.text=lockNotice;
                objectiveState.text="Another topic is active";instructions.text=feedback.text=lockNotice;actionLabel.text="Topic in progress";
            }
            for(int i=0;i<5;i++)progressPips[i].color=i<state.round?Green:new Color(.31f,.37f,.30f,.25f);
        }
        RectTransform MakeButton(Transform parent,string text,UnityEngine.Events.UnityAction callback,Vector2 anchor,Vector2 size)
        {
            var r=Box(text.Replace(' ','_'),parent,anchor,size,Vector2.zero,owner.buttonArt);
            var b=r.gameObject.AddComponent<Button>();b.onClick.AddListener(callback);b.transition=Selectable.Transition.ColorTint;
            r.GetComponent<Image>().raycastTarget=true;
            var colors=b.colors;colors.highlightedColor=new Color(1,.96f,.82f);colors.pressedColor=new Color(.76f,.83f,.77f);colors.disabledColor=new Color(.57f,.57f,.57f);b.colors=colors;
            var label=Label("Label",r,27,owner.headingFont,new Color(1,.95f,.80f),new Vector2(.07f,.12f),new Vector2(.93f,.88f));label.text=text;label.alignment=TextAlignmentOptions.Center;
            return r;
        }
        TMP_Text Label(string name,Transform parent,float size,TMP_FontAsset font,Color color,Vector2 min,Vector2 max)
        {
            var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.color=color;t.raycastTarget=false;t.richText=false;
            t.enableAutoSizing=true;t.fontSizeMax=size;t.fontSizeMin=size-4;t.alignment=TextAlignmentOptions.MidlineLeft;t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        RectTransform Box(string name,Transform parent,Vector2 anchor,Vector2 size,Vector2 offset,Sprite sprite)
        {
            var r=Rect(name,parent,anchor,anchor);r.sizeDelta=size;r.anchoredPosition=offset;
            var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4.5f;image.raycastTarget=false;return r;
        }
        static RectTransform Flat(string name,Transform parent,Color color,Vector2 min,Vector2 max)
        {
            var r=Rect(name,parent,min,max);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return r;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);Stretch(r,min,max);return r;
        }
        static void Stretch(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
