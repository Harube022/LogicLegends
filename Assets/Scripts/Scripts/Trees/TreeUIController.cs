using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace LogicLegends.Trees
{
    /// <summary>Restored Trees sidebar and board workspace, with readable screen-space actions.</summary>
    public sealed class TreeUIController : MonoBehaviour
    {
        public RectTransform host;
        public GameObject gameplayControls;
        public Sprite parchment,nodeCircle,hudObjective,hudStatus,hudMap;
        TreeStageManager stage;
        RectTransform owned,map,diagram,hud,focus,targets,numbers,helpRoot,sidebar,questionCard,timerCard,actions;
        TMP_Text clock,progress,question,boardProgress,status,boardClock,boardTitle;
        Button help,focusHelp,submit,connect,remove,clear,back,next;
        readonly Dictionary<string,Button> nodes=new Dictionary<string,Button>();
        bool resultVisible;
        TreeChallengeIntroductions Intro=>stage.Introductions;
        bool Practicing=>Intro!=null&&Intro.IsOpen;
        TreeBoardController DisplayBoard=>Practicing?Intro.PracticeBoard:stage.board;
        TreeChallengeData DisplayQuestion=>Practicing?Intro.Practice:stage.Challenge;
        public RectTransform BoardCanvas=>focus;
        public TMP_Text QuestionText=>question;
        public TMP_Text StatusText=>status;
        public Button SubmitButton=>submit;
        public Button BackButton=>back;
        public Button HelpButton=>help;
        public Button NextButton=>next;
        public RectTransform Target(string id)=>nodes.TryGetValue(id,out var button)?(RectTransform)button.transform:null;
        public void Build(TreeStageManager owner)
        {
            stage=owner;host=(RectTransform)host.GetComponentInParent<Canvas>().transform;
            owned=StageUITheme.Rect("Trees HUD",host);StageUITheme.Stretch(owned,Vector2.zero,Vector2.one);
            var safe=StageUITheme.Rect("Trees safe area",owned);StageUITheme.Stretch(safe,Vector2.zero,Vector2.one);safe.gameObject.AddComponent<Crystal.SafeArea>();
            map=Panel("Tree minimap",safe,hudMap);Place(map,Vector2.one,new Vector2(-24,-24),new Vector2(340,290),Vector2.one);ExpandableMinimap.Attach(map);
            var caption=Text("Map title",map,"TREE MAP",26,true);Place(caption.rectTransform,new Vector2(.5f,1),new Vector2(0,-28),new Vector2(240,36));
            diagram=StageUITheme.Rect("Editable diagram",map);StageUITheme.Stretch(diagram,new Vector2(.12f,.10f),new Vector2(.88f,.79f));
            hud=Panel("Shared countdown",safe,hudStatus);Place(hud,Vector2.one,new Vector2(-24,-326),new Vector2(340,170),Vector2.one);
            var title=Text("CurrentStageLabel",hud,TrialCatalog.ForScene("Trees").Title,25,true);StageUITheme.Stretch(title.rectTransform,new Vector2(.07f,.67f),new Vector2(.93f,.91f));
            clock=Text("Timer",hud,"10:00",34,true);StageUITheme.Stretch(clock.rectTransform,new Vector2(.07f,.16f),new Vector2(.37f,.62f));
            progress=Text("StageProgress",hud,"",23,true);StageUITheme.Stretch(progress.rectTransform,new Vector2(.40f,.14f),new Vector2(.94f,.61f));
            help=Action("Trees Help",owned,"Help",Vector2.zero,new Vector2(240,88),()=>stage.SetLesson(true));
            var settings=gameplayControls.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r=>r.name=="SettingsButton");StagePresentation.Help(help,settings);Readable(help.GetComponentInChildren<TMP_Text>(),true);
            helpRoot=StageUITheme.Rect("Trees Help panel",host);StageUITheme.Stretch(helpRoot,Vector2.zero,Vector2.one);helpRoot.gameObject.AddComponent<Image>();helpRoot.gameObject.SetActive(false);
            BuildFocus();RebuildDiagram();Refresh();
        }
        void BuildFocus()
        {
            focus=StageUITheme.Rect("Trees Focused Board",host);StageUITheme.Stretch(focus,Vector2.zero,Vector2.one);focus.gameObject.AddComponent<Image>().color=Color.clear;
            sidebar=StageUITheme.Rect("Board sidebar backdrop",focus);StageUITheme.Stretch(sidebar,Vector2.zero,new Vector2(.25f,1));var shade=sidebar.gameObject.AddComponent<Image>();shade.color=new Color(.035f,.085f,.11f);shade.raycastTarget=false;
            targets=StageUITheme.Rect("Screen node targets",focus);StageUITheme.Stretch(targets,Vector2.zero,Vector2.one);
            questionCard=Panel("Board objective card",sidebar,hudObjective);
            boardProgress=Text("Board challenge progress",questionCard,"",25,true);boardTitle=Text("Board challenge title",questionCard,"",34,true);
            question=Text("Current tree question",questionCard,"",32);question.alignment=boardProgress.alignment=boardTitle.alignment=TextAlignmentOptions.TopLeft;
            status=Text("Board selection and result",sidebar,"",28);status.color=Color.white;status.alignment=TextAlignmentOptions.TopLeft;
            timerCard=Panel("Board timer card",sidebar,hudStatus);boardClock=Text("Board time",timerCard,"10:00",36,true);StageUITheme.Stretch(boardClock.rectTransform,new Vector2(.1f,.12f),new Vector2(.9f,.88f));
            focusHelp=Action("Board Help",sidebar,"Help",Vector2.zero,new Vector2(260,88),()=>stage.SetLesson(true));
            next=Action("Tree Tutorial Next",sidebar,"Next",Vector2.zero,new Vector2(260,88),()=>Intro.Next());
            actions=StageUITheme.Rect("Focused board actions",focus);
            submit=Action("Submit Tree",actions,"Submit",Vector2.zero,new Vector2(280,88),()=>{if(Practicing)Intro.Submit();else stage.Confirm();});
            connect=Action("Board Connect",actions,"Connect",Vector2.zero,new Vector2(280,88),()=>Edit(false));
            remove=Action("Board Remove",actions,"Remove",Vector2.zero,new Vector2(280,88),()=>Edit(true));
            clear=Action("Board Clear Selection",actions,"Clear Pick",Vector2.zero,new Vector2(280,88),()=>{if(Practicing)Intro.Clear();else stage.ClearSelection();});
            numbers=StageUITheme.Rect("Board number answers",actions);
            back=Action("Exit Tree Board",focus,"Back",Vector2.zero,new Vector2(230,88),stage.ExitBoard);
            focus.gameObject.SetActive(false);
        }
        void Edit(bool deleting){if(Practicing)Intro.Edit(deleting);else stage.Edit(deleting);}
        public void RebuildDiagram()
        {
            if(targets==null||DisplayQuestion==null)return;
            foreach(Transform child in targets){child.gameObject.SetActive(false);Destroy(child.gameObject);}nodes.Clear();
            foreach(var node in DisplayQuestion.nodes)
            {
                string id=node.id;var rt=StageUITheme.Rect("Board Node "+id,targets);Place(rt,new Vector2(.5f,.5f),Vector2.zero,new Vector2(90,90));var image=rt.gameObject.AddComponent<Image>();image.color=Color.clear;
                var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=image;b.transition=Selectable.Transition.None;b.onClick.AddListener(()=>{if(Practicing)Intro.Select(id);else stage.SelectNode(id);});nodes[id]=b;
            }
            foreach(Transform child in numbers){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            int count=Mathf.Max(6,DisplayQuestion.nodes.Count);
            for(int i=0;i<count;i++){int answer=i;Action("Board Answer "+i,numbers,i.ToString(),Vector2.zero,new Vector2(110,80),()=>{if(Practicing)Intro.SelectNumber(answer);else stage.SelectAnswer(answer);});}
            RefreshDiagram();
        }
        public void RefreshDiagram()
        {
            if(diagram==null)return;
            foreach(Transform child in diagram){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            foreach(var e in stage.board.Edges)
            {
                var a=MapPoint(stage.Challenge.nodes.First(n=>n.id==e.source));var b=MapPoint(stage.Challenge.nodes.First(n=>n.id==e.destination));
                var rt=StageUITheme.Rect("Branch "+e.source+" "+e.destination,diagram);Place(rt,new Vector2(.5f,.5f),(a+b)*.5f,new Vector2(Vector2.Distance(a,b),3));rt.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);var ink=rt.gameObject.AddComponent<Image>();ink.color=StageUITheme.ReadingInk;ink.raycastTarget=false;
            }
            foreach(var n in stage.Challenge.nodes){var t=Text("Map node "+n.id,diagram,n.id,24,true);Place(t.rectTransform,new Vector2(.5f,.5f),MapPoint(n),new Vector2(32,32));}
            Refresh();
        }
        static Vector2 MapPoint(TreeNodeData n)=>new Vector2(n.position.x*100,-75+n.position.y*150);
        public void Refresh()
        {
            if(stage==null||DisplayQuestion==null)return;
            progress.text="Challenge "+(stage.ChallengeIndex+1)+" of 5\nQuestion "+(stage.QuestionIndex+1)+" of 2";
            int seconds=Mathf.CeilToInt(stage.timer.Remaining);clock.text=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");boardClock.text=clock.text;
            boardProgress.text=Practicing?"PRACTICE • CHALLENGE "+(stage.ChallengeIndex+1)+"\nStep "+(Intro.PageIndex+1)+" of "+Intro.StepCount:"Challenge "+(stage.ChallengeIndex+1)+" of 5\nQuestion "+(stage.QuestionIndex+1)+" of 2";
            boardTitle.text=Practicing?Intro.Heading:stage.Challenge.title;
            question.text=Practicing?Intro.Explanation:stage.Challenge.objective;
            status.text=Practicing?Intro.Feedback:stage.Feedback;
            status.color=!Practicing&&stage.State==TreeStageState.VALIDATING?new Color(1,.5f,.5f):!Practicing&&stage.State==TreeStageState.CHALLENGE_COMPLETE?new Color(.5f,1,.6f):Color.white;
            bool active=Practicing?!Intro.Returning:stage.CanSolve;
            foreach(var entry in nodes)entry.Value.interactable=active&&(!Practicing||Intro.AllowsNode(entry.Key));
            connect.gameObject.SetActive(DisplayQuestion.Editable);remove.gameObject.SetActive(DisplayQuestion.Editable);
            connect.interactable=active&&(!Practicing||Intro.AllowsControl(connect.name));
            remove.interactable=active&&(!Practicing||Intro.AllowsControl(remove.name));
            clear.interactable=active&&(!Practicing||Intro.AllowsControl(clear.name));
            submit.interactable=active&&(!Practicing||Intro.AllowsControl(submit.name));
            bool numeric=DisplayQuestion.type==TreeChallengeType.Depth||DisplayQuestion.type==TreeChallengeType.Height;numbers.gameObject.SetActive(numeric);
            foreach(var b in numbers.GetComponentsInChildren<Button>())b.interactable=active&&(!Practicing||Intro.AllowsControl(b.name));
            owned.gameObject.SetActive(!resultVisible&&!stage.BoardOpen&&!stage.LessonOpen&&!Practicing);
            focus.gameObject.SetActive(!resultVisible&&!stage.LessonOpen&&(stage.BoardOpen||Practicing)&&!(Practicing&&Intro.Returning));
            gameplayControls.SetActive(!resultVisible&&!stage.BoardOpen&&!stage.LessonOpen&&!Practicing);
            back.gameObject.SetActive(!Practicing);back.interactable=stage.BoardOpen&&!stage.Moving;
            focusHelp.gameObject.SetActive(!Practicing);next.gameObject.SetActive(Practicing&&Intro.CanNext);next.interactable=Practicing&&Intro.AllowsControl(next.name);
            help.interactable=stage.Ready&&!stage.Moving;
        }
        void LateUpdate()
        {
            if(stage==null||DisplayQuestion==null)return;
            var safe=Screen.safeArea;safe=Rect.MinMaxRect(Mathf.Clamp(safe.xMin,0,Screen.width),Mathf.Clamp(safe.yMin,0,Screen.height),Mathf.Clamp(safe.xMax,0,Screen.width),Mathf.Clamp(safe.yMax,0,Screen.height));
            float top=(Screen.height-safe.yMax)/Screen.height*host.rect.height;
            Place((RectTransform)back.transform,Vector2.one,new Vector2(-150,-top-65),new Vector2(230,88));
            LayoutSidebar(top);
            float available=host.rect.width*.75f-64,width=Mathf.Min(280,(available-42)/4);
            Place(actions,new Vector2(.625f,0),new Vector2(0,Mathf.Max(24,safe.yMin/Screen.height*host.rect.height+24)),new Vector2(available,220),new Vector2(.5f,0));
            var visible=DisplayQuestion.Editable?new[]{connect,remove,clear,submit}:new[]{clear,submit};
            for(int i=0;i<visible.Length;i++)Place((RectTransform)visible[i].transform,new Vector2(.5f,0),new Vector2((i-(visible.Length-1)*.5f)*(width+14),0),new Vector2(width,88),new Vector2(.5f,0));
            Place(numbers,new Vector2(.5f,0),new Vector2(0,110),new Vector2(available,80),new Vector2(.5f,0));
            var choices=numbers.GetComponentsInChildren<Button>();float space=Mathf.Min(128,available/Mathf.Max(1,choices.Length));for(int i=0;i<choices.Length;i++)Place((RectTransform)choices[i].transform,new Vector2(.5f,.5f),new Vector2((i-(choices.Length-1)*.5f)*space,0),new Vector2(space-10,80));
            if(focus.gameObject.activeInHierarchy&&DisplayBoard!=null&&Camera.main!=null)
            foreach(var node in DisplayBoard.Nodes)
            {
                if(!nodes.TryGetValue(node.id,out var button))continue;
                var screen=Camera.main.WorldToScreenPoint(node.transform.position);
                if(RectTransformUtility.ScreenPointToLocalPointInRectangle(targets,screen,null,out var local))((RectTransform)button.transform).anchoredPosition=local;
                float hitSize=Mathf.Max(88,42/host.lossyScale.x);((RectTransform)button.transform).sizeDelta=Vector2.one*hitSize;
                if(DisplayQuestion.type==TreeChallengeType.Depth||DisplayQuestion.type==TreeChallengeType.Height)button.interactable=Practicing&&Intro.AllowsNode(node.id);
            }
            if(Practicing&&!Intro.Returning){var target=Intro.HighlightControl==null?Target(Intro.HighlightNode):focus.GetComponentsInChildren<Button>().Where(b=>b.name==Intro.HighlightControl).Select(b=>(RectTransform)b.transform).FirstOrDefault();if(target!=null)TutorialSpotlight.Follow(questionCard,target,null);}
        }
        void LayoutSidebar(float safeTop)
        {
            float width=host.rect.width*.25f-40,textWidth=width-48,top=safeTop+(Practicing?126:24);
            float titleH=Mathf.Max(44,boardTitle.GetPreferredValues(boardTitle.text,textWidth,0).y);
            float bodyH=Mathf.Max(64,question.GetPreferredValues(question.text,textWidth,0).y);
            float height=132+titleH+bodyH;
            Place(questionCard,new Vector2(.5f,1),new Vector2(0,-top),new Vector2(width,height),new Vector2(.5f,1));
            Top(boardProgress,24,64,textWidth);Top(boardTitle,96,titleH,textWidth);Top(question,112+titleH,bodyH,textWidth);
            float after=top+height+20;
            float statusH=Mathf.Max(64,status.GetPreferredValues(status.text,width-8,0).y);
            Place(status.rectTransform,new Vector2(.5f,1),new Vector2(0,-after),new Vector2(width-8,statusH),new Vector2(.5f,1));after+=statusH+16;
            Place(timerCard,new Vector2(.5f,1),new Vector2(0,-after),new Vector2(width,112),new Vector2(.5f,1));after+=132;
            Place((RectTransform)focusHelp.transform,new Vector2(.5f,1),new Vector2(0,-after),new Vector2(Mathf.Min(width,260),88),new Vector2(.5f,1));
            Place((RectTransform)next.transform,new Vector2(.5f,0),new Vector2(0,32),new Vector2(Mathf.Min(width,260),88),new Vector2(.5f,0));
        }
        static void Top(TMP_Text t,float top,float height,float width){Place(t.rectTransform,new Vector2(0,1),new Vector2(24,-top),new Vector2(width,height),new Vector2(0,1));}
        public void ShowLesson(bool open){helpRoot.gameObject.SetActive(open);if(open)StageHelpPage.Install(helpRoot,"Trees",()=>stage.SetLesson(false));Refresh();}
        public void ShowResult(bool success)
        {
            resultVisible=true;helpRoot.gameObject.SetActive(false);Refresh();var view=StageResultCard.Build(host,stage.Return);view.Stage.text=TrialCatalog.ForScene("Trees").Title;view.Heading.text=success?"TRIAL COMPLETE":"TIME IS UP";
            view.Summary.text="Time Played: "+StageJourneyUI.FormatTime(stage.timer.ActiveSeconds)+"\nCorrect: "+stage.Correct+" of 10\nMistakes: "+stage.Mistakes;
            view.Primary.GetComponentInChildren<TMP_Text>().text="PLAY AGAIN";view.Primary.onClick.AddListener(stage.Restart);view.Extra.gameObject.SetActive(false);if(success)view.Root.GetComponent<StageResultLayout>().Complete(10);
        }
        RectTransform Panel(string name,Transform parent,Sprite sprite){var rt=StageUITheme.Rect(name,parent);var image=rt.gameObject.AddComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4.5f;image.raycastTarget=false;return rt;}
        static void Readable(TMP_Text text,bool bold=false){text.font=StageUITheme.ReadingFont;text.fontSharedMaterial=StageUITheme.ReadingFont.material;text.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;}
        static TMP_Text Text(string name,Transform parent,string value,float size,bool bold=false){var t=StageUITheme.Text(name,parent,value,size,true);Readable(t,bold);t.color=StageUITheme.ReadingInk;t.enableAutoSizing=false;t.raycastTarget=false;return t;}
        static Button Action(string name,Transform parent,string label,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction callback){var b=StageUITheme.Button(name,parent,label,callback);Place((RectTransform)b.transform,new Vector2(.5f,.5f),pos,size);b.GetComponent<Image>().pixelsPerUnitMultiplier=4.5f;var t=b.GetComponentInChildren<TMP_Text>();Readable(t,true);t.fontSizeMin=26;t.fontSizeMax=30;return b;}
        static void Place(RectTransform rt,Vector2 anchor,Vector2 pos,Vector2 size,Vector2? pivot=null){rt.anchorMin=rt.anchorMax=anchor;rt.pivot=pivot??new Vector2(.5f,.5f);rt.anchoredPosition=pos;rt.sizeDelta=size;}
    }
}
