using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>T2 reading and compact objective only. Commands still use the shared authoritative session.</summary>
    public sealed class ArchiveTruthUI:MonoBehaviour
    {
        ArchiveTopicUI owner;
        RectTransform objective,reader,healthControl,jumpControl;
        TMP_Text title,timer,formula,cue,readerTitle,readerTimer,readerFormula,feedback,primaryLabel;
        readonly TMP_Text[] miniRows=new TMP_Text[4],readerOutputs=new TMP_Text[4];
        readonly Image[] outputFaces=new Image[4],progress=new Image[9];
        Button checkButton,primary,returnButton;
        int revision=-1;Vector2 screen;Rect safe;float canvasScale;
        static readonly Color Ink=new Color(.16f,.11f,.07f),Teal=new Color(.08f,.36f,.35f),True=new Color(.09f,.48f,.35f),False=new Color(.67f,.36f,.10f),Red=new Color(.75f,.13f,.10f);
        public bool IsOpen=>reader!=null&&reader.gameObject.activeSelf;
        public bool IsObjectiveVisible=>objective!=null&&objective.gameObject.activeSelf;
        public RectTransform Objective=>objective;
        public Button CheckButton=>checkButton;
        public void Initialize(ArchiveTopicUI ui){owner=ui;Build();}
        void Build(){
            objective=Box("T2_Compact_Truth_Objective",transform,new Vector2(1,1),new Vector2(440,248),Vector2.zero,owner.statusArt);objective.pivot=Vector2.one;
            title=Label("Table_And_Tier",objective,22,owner.headingFont,Ink,new Vector2(.06f,.82f),new Vector2(.73f,.94f));
            timer=Label("Shared_T2_Timer",objective,23,owner.headingFont,Teal,new Vector2(.76f,.82f),new Vector2(.94f,.94f));timer.alignment=TextAlignmentOptions.MidlineRight;
            formula=Label("Current_Expression",objective,27,owner.textFont,Ink,new Vector2(.06f,.64f),new Vector2(.94f,.82f));formula.alignment=TextAlignmentOptions.Center;
            for(int row=0;row<4;row++){
                float x=.06f+row*.225f;var cell=Flat("Row_"+(row+1),objective,new Color(.08f,.33f,.32f,.08f),new Vector2(x,.39f),new Vector2(x+.205f,.64f));
                miniRows[row]=Label("Inputs_And_Output",cell,21,owner.textFont,Teal,new Vector2(.03f,.02f),new Vector2(.97f,.98f));miniRows[row].alignment=TextAlignmentOptions.Center;
            }
            cue=Label("Hand_Action_Or_Feedback",objective,17,owner.textFont,Teal,new Vector2(.06f,.265f),new Vector2(.94f,.39f));
            MakeButton(objective,"Read",()=>owner.Open(owner.session.stations[1]),new Vector2(.23f,.155f),new Vector2(120,54),21);
            checkButton=MakeButton(objective,"Check table",Submit,new Vector2(.66f,.155f),new Vector2(206,54),22);
            foreach(var r in FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
                if(r.name=="JumpButton")jumpControl=r;else if(r.name=="Khezu_Existing_Health_System")healthControl=r;
            }
            PositionObjective();
            reader=Box("T2_Truth_Mosaic_Reader",transform,new Vector2(.5f,.53f),new Vector2(1120,740),Vector2.zero,owner.panelArt);
            readerTitle=Label("Truth_Table_And_Progress",reader,31,owner.headingFont,Ink,new Vector2(.055f,.845f),new Vector2(.72f,.95f));
            MakeButton(reader,"Close",owner.Close,new Vector2(.85f,.90f),new Vector2(230,82),27);
            readerTimer=Label("Shared_Team_Clock",reader,23,owner.textFont,Teal,new Vector2(.55f,.79f),new Vector2(.94f,.835f));readerTimer.alignment=TextAlignmentOptions.MidlineRight;
            readerFormula=Label("Logical_Expression",reader,34,owner.textFont,Ink,new Vector2(.07f,.69f),new Vector2(.93f,.785f));readerFormula.alignment=TextAlignmentOptions.Center;
            var table=Rect("Read_Only_Team_Table",reader,new Vector2(.13f,.305f),new Vector2(.87f,.68f));
            for(int col=0;col<3;col++){
                float x=col==0?.04f:col==1?.35f:.67f;
                var header=Label("Column_Header_"+col,table,25,owner.headingFont,Ink,new Vector2(x,.82f),new Vector2(x+(col==2?.27f:.24f),.98f));header.text=col==0?"P":col==1?"Q":"OUTPUT";header.alignment=TextAlignmentOptions.Center;
            }
            for(int row=0;row<4;row++){
                float y=.62f-row*.195f;var strip=Flat("Truth_Row_"+(row+1),table,new Color(.07f,.29f,.28f,row%2==0?.09f:.035f),new Vector2(0,y),new Vector2(1,y+.185f));
                for(int col=0;col<2;col++){
                    var value=Label(col==0?"P":"Q",strip,28,owner.textFont,Ink,new Vector2(.04f+col*.31f,.06f),new Vector2(.28f+col*.31f,.94f));value.text=ArchiveTruthPresentation.RowNames[row][col].ToString();value.alignment=TextAlignmentOptions.Center;
                }
                var face=Flat("Output_Cell",strip,new Color(.12f,.32f,.32f,.12f),new Vector2(.67f,.08f),new Vector2(.94f,.92f));outputFaces[row]=face.GetComponent<Image>();
                readerOutputs[row]=Label("Placed_Tile_Value",face,27,owner.textFont,Teal,Vector2.zero,Vector2.one);readerOutputs[row].alignment=TextAlignmentOptions.Center;
            }
            var instruction=Label("World_Interaction_Instructions",reader,22,owner.textFont,Teal,new Vector2(.07f,.195f),new Vector2(.93f,.30f));
            instruction.text="Take T / True or F / False at a tile stand. Carry it to a row's OUTPUT and use Hand / E. You can replace any placed tile. Check when all four rows are filled.";
            feedback=Label("Shared_Team_Feedback",reader,22,owner.textFont,Ink,new Vector2(.07f,.12f),new Vector2(.93f,.195f));
            primary=MakeButton(reader,"Begin at Book",Primary,new Vector2(.69f,.065f),new Vector2(290,82),26);primaryLabel=primary.GetComponentInChildren<TMP_Text>();
            returnButton=MakeButton(reader,"Return tile",()=>owner.session.Request(1,ArchiveAction.ReturnItem),new Vector2(.34f,.065f),new Vector2(230,82),24);
            for(int i=0;i<9;i++)progress[i]=Flat("Solved_Table_"+(i+1),reader,new Color(.2f,.4f,.35f,.2f),new Vector2(.07f+i*.021f,.06f),new Vector2(.085f+i*.021f,.085f)).GetComponent<Image>();
            reader.gameObject.SetActive(false);objective.gameObject.SetActive(false);
        }
        public void Open(){reader.gameObject.SetActive(true);revision=-1;Refresh();}
        public void Close(){if(reader!=null)reader.gameObject.SetActive(false);}
        void Primary(){
            var state=owner.session.State.stations[1];
            if(state.phase==0)owner.session.Request(1,ArchiveAction.Start);
            else if(state.phase==1){Submit();}
            else owner.Close();
        }
        void Submit(){if(owner.session.Ready && Filled(owner.session.State.stations[1]))owner.session.Request(1,ArchiveAction.Submit);}
        static bool Filled(ArchiveStationState state)=>state.phase==1&&state.placements.Length==4&&state.placements.All(p=>p>=0)&&state.carries.Count==0;
        void Update(){
            if(owner==null||owner.session==null||!owner.session.Ready)return;
            var session=owner.session;
            bool near=Player.LocalInstance!=null&&session.CanAct(session.LocalActor)&&Time.timeScale>0&&Vector3.Distance(Player.LocalInstance.transform.position,session.stations[1].transform.position)<34;
            objective.gameObject.SetActive(near&&owner.ReadingStation==null);
            if(!near){if(IsOpen)owner.Close();return;}
            if(screen!=new Vector2(Screen.width,Screen.height)||safe!=Screen.safeArea||!Mathf.Approximately(canvasScale,owner.GetComponent<Canvas>().scaleFactor))PositionObjective();
            if(revision!=session.State.revision){revision=session.State.revision;Refresh();}
            var state=session.State.stations[1];int seconds=Mathf.CeilToInt(session.TimerRemaining(state));string value=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
            timer.text=value;readerTimer.text="TEAM TIME  "+value;timer.color=readerTimer.color=state.timerExpired||(state.timerRunning&&seconds<=30)?Red:Teal;
            bool canStart=session.CanStartTopic(1)&&session.Near(session.LocalActor,1,ArchiveTopicStation.BookPosition,ArchiveTopicSession.BookUseRadius);
            primary.interactable=state.phase==2||state.phase==0&&canStart||Filled(state);
            checkButton.interactable=Filled(state);returnButton.gameObject.SetActive(state.carries.Any(c=>c.actor==session.LocalActor));
            string locked=session.TopicLockNotice(1);
            if(!string.IsNullOrEmpty(locked)){cue.text="Another topic is active";feedback.text=locked;primary.interactable=false;checkButton.interactable=false;return;}
            if(state.phase==1){
                var focus=owner.FocusedPoint;var carried=state.carries.FirstOrDefault(c=>c.actor==session.LocalActor);
                bool recent=state.lastAnswerResult<0&&ArchiveTopicSession.Clock-state.lastAnswerAt<2.5;
                cue.text=recent?"Incorrect · correct the placed tiles":focus!=null&&focus.station.topic==1?focus.role==ArchivePointRole.Item?focus.index==0?"Hand / E · take T / True":"Hand / E · take F / False":focus.role==ArchivePointRole.Slot?"Hand / E · "+(carried==null?"row "+(focus.index+1):"place "+(carried.item==0?"T":"F")+" in row "+(focus.index+1)):"Book · Hand / E":carried!=null?"Carrying "+(carried.item==0?"T / True":"F / False")+" · choose an output":Filled(state)?"Four outputs ready · check table":"Take a T or F tile · Hand / E";
            }
            else if(state.phase==0)cue.text=canStart?"Book Statue · Hand / E":"Return to the Book Statue";
        }
        void Refresh(){
            var session=owner.session;if(!session.Ready)return;var state=session.State.stations[1];var q=session.Question(1);int round=Mathf.Min(9,state.round+1);
            title.text=state.phase==2?"T2 · TEAM COMPLETE 9 / 9":"T2 · "+ArchiveTruthPresentation.Tier(state.round)+"  "+round+" / 9";
            formula.text=state.phase==1?q.prompt:state.timerExpired?"Time up · solved tables kept":state.phase==2?"Truth Mosaic restored":state.round==0?"Read the Book to begin":"Table "+state.round+" restored";
            readerTitle.text="T2  TRUTH TABLE\n"+(state.phase==2?"Truth Mosaic · Team complete":ArchiveTruthPresentation.Tier(state.round)+" · Table "+round+" / 9");
            readerFormula.text=state.phase==1?q.prompt:state.timerExpired?"Retry the unfinished table at the Book Statue":state.phase==2?"All nine tables solved by your team":"Three Easy · three Medium · three Hard tables";
            bool outputs=state.phase==1||state.timerExpired;
            for(int row=0;row<4;row++){
                int item=outputs&&state.placements.Length>row?state.placements[row]:-1;string value=item<0?"—":item==0?"T":"F";
                miniRows[row].text=ArchiveTruthPresentation.RowNames[row]+"\n"+value;miniRows[row].color=item<0?Teal:item==0?True:False;
                readerOutputs[row].text=item<0?"Empty":item==0?"T / True":"F / False";readerOutputs[row].color=item==1?False:True;
                outputFaces[row].color=item<0?new Color(.12f,.32f,.32f,.10f):item==0?new Color(.15f,.65f,.45f,.18f):new Color(.89f,.60f,.22f,.22f);
            }
            cue.text=state.timerExpired?"Timeout · one mistake recorded":state.phase==2?"Shared team success":"Book Statue · Hand / E";
            feedback.text=state.feedback;feedback.color=state.lastAnswerResult<0&&state.phase==1?Red:Ink;
            primaryLabel.text=state.phase==2?"Explore":state.phase==1?"Check table":state.timerExpired?"Retry at Book":state.round==0?"Begin at Book":"Next at Book";
            for(int i=0;i<9;i++)progress[i].color=i<state.round?True:new Color(.2f,.4f,.35f,.2f);
            string locked=session.TopicLockNotice(1);if(!string.IsNullOrEmpty(locked)){title.text="T2 · WAITING FOR TEAM";formula.text="Finish the active topic first";feedback.text=locked;primaryLabel.text="Topic in progress";}
        }
        void PositionObjective(){
            Canvas.ForceUpdateCanvases();screen=new Vector2(Screen.width,Screen.height);safe=Screen.safeArea;canvasScale=Mathf.Max(.01f,owner.GetComponent<Canvas>().scaleFactor);
            float right=(Screen.width-safe.xMax)/canvasScale+28,top=(Screen.height-safe.yMax)/canvasScale+224;
            var corners=new Vector3[4];if(healthControl!=null){healthControl.GetWorldCorners(corners);top=Mathf.Max(top,(Screen.height-corners.Min(p=>p.y))/canvasScale+12);}
            float height=248;if(jumpControl!=null){jumpControl.GetWorldCorners(corners);height=Mathf.Clamp((Screen.height-top*canvasScale-corners.Max(p=>p.y))/canvasScale-16,210,248);}
            objective.anchoredPosition=new Vector2(-right,-top);objective.sizeDelta=new Vector2(440,height);
        }
        Button MakeButton(Transform parent,string text,UnityEngine.Events.UnityAction callback,Vector2 anchor,Vector2 size,int fontSize){
            var r=Box(text.Replace(' ','_'),parent,anchor,size,Vector2.zero,owner.buttonArt);var b=r.gameObject.AddComponent<Button>();b.onClick.AddListener(callback);r.GetComponent<Image>().raycastTarget=true;
            var c=b.colors;c.highlightedColor=new Color(1,.96f,.82f);c.pressedColor=new Color(.76f,.83f,.77f);c.disabledColor=new Color(.57f,.57f,.57f);b.colors=c;
            var t=Label("Label",r,fontSize,owner.headingFont,new Color(1,.95f,.8f),new Vector2(.06f,.12f),new Vector2(.94f,.88f));t.text=text;t.alignment=TextAlignmentOptions.Center;return b;
        }
        TMP_Text Label(string name,Transform parent,float size,TMP_FontAsset font,Color color,Vector2 min,Vector2 max){
            var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.fontSizeMax=size;t.fontSizeMin=size-3;t.enableAutoSizing=true;t.richText=false;t.color=color;t.raycastTarget=false;t.alignment=TextAlignmentOptions.MidlineLeft;t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        RectTransform Box(string name,Transform parent,Vector2 anchor,Vector2 size,Vector2 offset,Sprite sprite){var r=Rect(name,parent,anchor,anchor);r.sizeDelta=size;r.anchoredPosition=offset;var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4.5f;image.raycastTarget=false;return r;}
        static RectTransform Flat(string name,Transform parent,Color color,Vector2 min,Vector2 max){var r=Rect(name,parent,min,max);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return r;}
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
    }
}
