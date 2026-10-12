using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace LogicLegends.Trees
{
    public sealed class TreeUIController : MonoBehaviour
    {
        public RectTransform host;
        public GameObject gameplayControls;
        public Sprite parchment, nodeCircle;
        public Sprite hudObjective, hudStatus, hudMap;
        TreeStageManager stage;
        TMP_Text objective,objectiveProgress,objectiveTitle,feedback,clock,selection,lessonBody;
        RectTransform diagram,owned,actions,numberRow,settingsButton;
        readonly Vector3[] settingsCorners=new Vector3[4];
        GameObject lesson,result;
        Button book,connect,remove,clear,guide;
        readonly Dictionary<string,Button> nodes=new Dictionary<string,Button>();
        RectTransform focusOverlay, focusTargets, focusNumbers, focusObjectiveCard, focusFeedbackCard, focusTimerCard, focusEdgeControls, focusActionBar;
        TMP_Text focusObjective, focusProgress, focusTitle, focusFeedback, focusClock, focusSelection, focusFeedbackHeading;
        Button focusConfirm, focusConnect, focusRemove, focusClear, focusExit, focusGuide;
        readonly Dictionary<string,Button> focusNodes=new Dictionary<string,Button>();
        bool resultVisible;
        string laidOutTitle,laidOutObjective;
        float laidOutWidth;
        string laidOutFeedback;
        public void Build(TreeStageManager owner)
        {
            // The camera viewport uses full-screen coordinates; only edge controls receive safe insets.
            host=(RectTransform)host.GetComponentInParent<Canvas>().transform;
            settingsButton=gameplayControls.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r=>r.name=="SettingsButton");
            stage=owner;owned=StageUITheme.Rect("Trees HUD",host);StageUITheme.Stretch(owned,Vector2.zero,Vector2.one);
            var info=Panel("Tree objective",owned,new Vector2(0,1),new Vector2(225,-295),new Vector2(410,250),hudObjective);
            objectiveProgress=Text(info,"Challenge progress",new Vector2(.09f,.80f),new Vector2(.91f,.92f),22);
            objectiveTitle=Text(info,"Challenge title",new Vector2(.09f,.60f),new Vector2(.91f,.79f),28);
            objective=Text(info,"Objective",new Vector2(.09f,.12f),new Vector2(.91f,.56f),26,true);
            objectiveProgress.alignment=objectiveTitle.alignment=objective.alignment=TextAlignmentOptions.TopLeft;
            var feed=Panel("Feedback",owned,new Vector2(0,1),new Vector2(225,-495),new Vector2(410,135));
            feedback=Text(feed,"Feedback",new Vector2(.09f,.12f),new Vector2(.91f,.88f),24,true);
            var map=Panel("Tree minimap",owned,new Vector2(1,1),new Vector2(-185,-180),new Vector2(330,320),hudMap);
            var caption=Text(map,"Diagram title",new Vector2(.15f,.84f),new Vector2(.85f,.94f),24);caption.text="TREE STRUCTURE";
            diagram=StageUITheme.Rect("Editable diagram",map);StageUITheme.Stretch(diagram,new Vector2(.13f,.13f),new Vector2(.87f,.82f));
            var time=Panel("Shared countdown",owned,new Vector2(1,1),new Vector2(-185,-385),new Vector2(330,85));
            clock=Text(time,"Timer",new Vector2(.1f,.1f),new Vector2(.9f,.9f),32);
            actions=StageUITheme.Rect("Tree actions",owned);Place(actions,new Vector2(.5f,0),new Vector2(0,125),new Vector2(780,175));
            book=Action("Book interaction",actions,"ACTIVATE BOOK",Vector2.zero,new Vector2(280,88),stage.InteractBook);
            selection=Text(actions,"Selected endpoints",new Vector2(0,.02f),new Vector2(1,.25f),24,true);
            Place(selection.rectTransform,new Vector2(.5f,.5f),new Vector2(0,-75),new Vector2(780,40));
            connect=Action("Connect",actions,"CONNECT",new Vector2(-250,-22),new Vector2(235,65),()=>stage.Edit(false));
            remove=Action("Remove",actions,"REMOVE",new Vector2(0,-22),new Vector2(235,65),()=>stage.Edit(true));
            clear=Action("Clear selection",actions,"CLEAR PICK",new Vector2(250,-22),new Vector2(235,65),stage.ClearSelection);
            guide=Action("Trees Field Guide",owned,"FIELD GUIDE",Vector2.zero,new Vector2(280,88),()=>stage.SetLesson(true));
            Place((RectTransform)guide.transform,new Vector2(0,1),new Vector2(320,-90),new Vector2(280,88));
            numberRow=StageUITheme.Rect("Number answers",actions);Place(numberRow,new Vector2(.5f,.5f),new Vector2(0,-22),new Vector2(720,65));
            for(int i=0;i<=5;i++){int answer=i;Action("Answer "+i,numberRow,i.ToString(),new Vector2(-300+i*120,0),new Vector2(110,65),()=>stage.SelectAnswer(answer));}
            BuildFocus();BuildBook();RebuildDiagram();Refresh();
        }
        RectTransform Panel(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,Sprite artwork=null)
        {
            var rect=StageUITheme.Rect(name,parent);Place(rect,anchor,position,size);var image=rect.gameObject.AddComponent<Image>();
            image.sprite=artwork!=null?artwork:hudStatus;image.type=Image.Type.Sliced;image.color=Color.white;
            image.pixelsPerUnitMultiplier=Mathf.Min(image.sprite.rect.width,image.sprite.rect.height)*.01f;
            image.raycastTarget=false;return rect;
        }
        static void Place(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size){rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;}
        static TMP_Text Text(Transform parent,string name,Vector2 min,Vector2 max,float size,bool reading=false)
        {var text=StageUITheme.Text(name,parent,"",size,reading);text.color=StageUITheme.ReadingInk;StageUITheme.Stretch(text.rectTransform,min,max);text.enableAutoSizing=true;text.fontSizeMin=size-3;text.fontSizeMax=size;return text;}
        static Button Action(string name,Transform parent,string title,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction callback)
        {
            var button=StageUITheme.Button(name,parent,title,callback);
            size=name.StartsWith("Node ")?size:name.Contains("Answer ")?new Vector2(96,72):new Vector2(280,88);
            Place((RectTransform)button.transform,new Vector2(.5f,.5f),pos,size);
            button.GetComponent<Image>().pixelsPerUnitMultiplier=4.5f;
            var label=button.GetComponentInChildren<TMP_Text>();label.rectTransform.offsetMin=new Vector2(24,10);label.rectTransform.offsetMax=new Vector2(-24,-10);
            return button;
        }
        public void RebuildDiagram()
        {
            foreach(Transform child in diagram){child.gameObject.SetActive(false);Destroy(child.gameObject);}nodes.Clear();
            // Edges are independent editable UI graphics; no answers are baked into artwork.
            foreach(var e in stage.board.Edges)DrawEdge(e);
            foreach(var n in stage.Challenge.nodes)
            {
                string id=n.id;var button=Action("Node "+id,diagram,id,DiagramPoint(n),new Vector2(50,55),()=>stage.SelectNode(id));
                var image=button.GetComponent<Image>();image.sprite=nodeCircle;image.color=new Color(.16f,.31f,.33f);image.type=Image.Type.Simple;image.preserveAspect=true;
                var label=button.GetComponentInChildren<TMP_Text>();label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;label.fontSizeMin=22;label.fontSizeMax=26;nodes[id]=button;
            }
            RebuildFocusTargets();
        }
        void BuildFocus()
        {
            focusOverlay=StageUITheme.Rect("Trees Focused Board",host);StageUITheme.Stretch(focusOverlay,Vector2.zero,Vector2.one);
            // A transparent modal surface consumes unused taps; only the projected node buttons edit the tree.
            focusOverlay.gameObject.AddComponent<Image>().color=Color.clear;
            var backdrop=StageUITheme.Rect("Board sidebar backdrop",focusOverlay);StageUITheme.Stretch(backdrop,Vector2.zero,new Vector2(.25f,1));
            var backdropImage=backdrop.gameObject.AddComponent<Image>();backdropImage.color=new Color(.035f,.085f,.11f);backdropImage.raycastTarget=false;
            focusTargets=StageUITheme.Rect("Screen node targets",focusOverlay);StageUITheme.Stretch(focusTargets,Vector2.zero,Vector2.one);
            focusObjectiveCard=Panel("Board objective card",focusOverlay,new Vector2(0,1),new Vector2(230,-225),new Vector2(440,390),hudObjective);
            focusProgress=Text(focusObjectiveCard,"Board challenge progress",new Vector2(.1f,.82f),new Vector2(.9f,.93f),24);
            focusTitle=Text(focusObjectiveCard,"Board challenge title",new Vector2(.1f,.62f),new Vector2(.9f,.80f),34);
            focusObjective=Text(focusObjectiveCard,"Board objective",new Vector2(.1f,.12f),new Vector2(.9f,.57f),30,true);
            focusProgress.alignment=focusTitle.alignment=focusObjective.alignment=TextAlignmentOptions.TopLeft;
            focusFeedbackCard=Panel("Board feedback card",focusOverlay,new Vector2(0,1),new Vector2(230,-565),new Vector2(440,250));
            focusFeedbackHeading=Text(focusFeedbackCard,"Board feedback heading",new Vector2(.1f,.77f),new Vector2(.9f,.91f),24);focusFeedbackHeading.text="FEEDBACK";focusFeedbackHeading.alignment=TextAlignmentOptions.TopLeft;
            focusFeedback=Text(focusFeedbackCard,"Board feedback",new Vector2(.1f,.12f),new Vector2(.9f,.69f),27,true);focusFeedback.alignment=TextAlignmentOptions.TopLeft;
            focusTimerCard=Panel("Board timer card",focusOverlay,new Vector2(0,1),new Vector2(230,-770),new Vector2(440,120));
            var timerHeading=Text(focusTimerCard,"Board timer heading",new Vector2(.1f,.60f),new Vector2(.9f,.80f),20);timerHeading.text="TIME LEFT";
            focusClock=Text(focusTimerCard,"Board timer",new Vector2(.1f,.12f),new Vector2(.9f,.59f),36);
            focusGuide=Action("Board Field Guide",focusOverlay,"FIELD GUIDE",Vector2.zero,new Vector2(300,75),()=>stage.SetLesson(true));
            Place((RectTransform)focusGuide.transform,new Vector2(0,1),new Vector2(230,-885),new Vector2(300,75));
            focusEdgeControls=StageUITheme.Rect("Board edge controls",focusOverlay);
            focusExit=Action("Exit Tree Board",focusEdgeControls,"EXIT BOARD",Vector2.zero,new Vector2(280,88),stage.ExitBoard);
            Place((RectTransform)focusExit.transform,new Vector2(1,1),new Vector2(-164,-68),new Vector2(280,88));
            var actionBar=StageUITheme.Rect("Focused board actions",focusOverlay);focusActionBar=actionBar;Place(actionBar,new Vector2(.625f,0),new Vector2(0,100),new Vector2(1100,150));
            focusConfirm=Action("Confirm Tree",actionBar,"CONFIRM",new Vector2(0,55),new Vector2(300,75),stage.Confirm);
            focusConnect=Action("Board Connect",actionBar,"CONNECT",new Vector2(-296,-30),new Vector2(280,88),()=>stage.Edit(false));
            focusRemove=Action("Board Remove",actionBar,"REMOVE",new Vector2(0,-30),new Vector2(260,70),()=>stage.Edit(true));
            focusClear=Action("Board Clear Selection",actionBar,"CLEAR PICK",new Vector2(296,-30),new Vector2(280,88),stage.ClearSelection);
            focusSelection=StageUITheme.Text("Board selection",focusOverlay,"",28,true);
            Place(focusSelection.rectTransform,new Vector2(.625f,0),new Vector2(0,220),new Vector2(1100,50));
            focusSelection.outlineColor=Color.black;focusSelection.outlineWidth=.15f;
            focusNumbers=StageUITheme.Rect("Board number answers",actionBar);Place(focusNumbers,new Vector2(.5f,.5f),new Vector2(0,-30),new Vector2(840,70));
            for(int i=0;i<=5;i++){int answer=i;Action("Board Answer "+i,focusNumbers,i.ToString(),new Vector2(-280+i*112,0),new Vector2(96,72),()=>stage.SelectAnswer(answer));}
            focusOverlay.gameObject.SetActive(false);
        }
        void RebuildFocusTargets()
        {
            foreach(Transform child in focusTargets){child.gameObject.SetActive(false);Destroy(child.gameObject);}focusNodes.Clear();
            foreach(var node in stage.Challenge.nodes)
            {
                string id=node.id;var rect=StageUITheme.Rect("Board Node "+id,focusTargets);Place(rect,new Vector2(.5f,.5f),Vector2.zero,new Vector2(100,100));
                var image=rect.gameObject.AddComponent<Image>();image.color=Color.clear;image.pixelsPerUnitMultiplier=4.5f;
                var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>stage.SelectNode(id));
                focusNodes[id]=button;
            }
        }
        void LayoutFocusSidebar()
        {
            float width=host.rect.width*.25f-48;
            if(laidOutTitle==focusTitle.text&&laidOutObjective==focusObjective.text&&laidOutFeedback==focusFeedback.text&&Mathf.Approximately(laidOutWidth,width))return;
            laidOutTitle=focusTitle.text;laidOutObjective=focusObjective.text;laidOutFeedback=focusFeedback.text;laidOutWidth=width;
            float textWidth=width-64,center=host.rect.width*.125f;
            focusTitle.fontSize=34;focusObjective.fontSize=30;
            float titleHeight=Mathf.Max(42,focusTitle.GetPreferredValues(focusTitle.text,textWidth,float.PositiveInfinity).y);
            float bodyHeight=Mathf.Max(44,focusObjective.GetPreferredValues(focusObjective.text,textWidth,float.PositiveInfinity).y);
            float height=112+titleHeight+bodyHeight;
            Place(focusObjectiveCard,new Vector2(0,1),new Vector2(center,-30-height*.5f),new Vector2(width,height));
            TopLabel(focusProgress,24,30,textWidth);TopLabel(focusTitle,64,titleHeight,textWidth);TopLabel(focusObjective,80+titleHeight,bodyHeight,textWidth);
            float nextTop=30+height+20;
            float feedbackHeight=96+Mathf.Max(64,focusFeedback.GetPreferredValues(focusFeedback.text,textWidth,float.PositiveInfinity).y);
            Place(focusFeedbackCard,new Vector2(0,1),new Vector2(center,-nextTop-feedbackHeight*.5f),new Vector2(width,feedbackHeight));
            TopLabel(focusFeedbackHeading,24,30,textWidth);TopLabel(focusFeedback,64,feedbackHeight-88,textWidth);
            nextTop+=feedbackHeight+20;
            Place(focusTimerCard,new Vector2(0,1),new Vector2(center,-nextTop-60),new Vector2(width,120));
            nextTop+=140;
            Place((RectTransform)focusGuide.transform,new Vector2(0,1),new Vector2(center,-nextTop-44),new Vector2(280,88));
        }
        static void TopLabel(TMP_Text text,float top,float height,float width)
        {
            var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(32,-top);rect.sizeDelta=new Vector2(width,height);
        }
        void LateUpdate()
        {
            if(focusEdgeControls!=null)
            {
                var safe=Screen.safeArea;var size=new Vector2(Screen.width,Screen.height);
                // An Editor view can retain stale safe bounds after a resolution change.
                safe=Rect.MinMaxRect(Mathf.Clamp(safe.xMin,0,size.x),Mathf.Clamp(safe.yMin,0,size.y),Mathf.Clamp(safe.xMax,0,size.x),Mathf.Clamp(safe.yMax,0,size.y));
                StageUITheme.Stretch(focusEdgeControls,safe.min/size,safe.max/size);
                focusActionBar.anchoredPosition=new Vector2(0,100+safe.yMin/Screen.height*host.rect.height);
                actions.anchoredPosition=new Vector2(0,125+safe.yMin/size.y*host.rect.height);
                if(settingsButton!=null)
                {
                    settingsButton.GetWorldCorners(settingsCorners);
                    var topRight=host.InverseTransformPoint(settingsCorners[2]);
                    var bottomRight=host.InverseTransformPoint(settingsCorners[3]);
                    guide.GetComponent<RectTransform>().anchoredPosition=new Vector2(topRight.x-host.rect.xMin+16+140,(topRight.y+bottomRight.y)*.5f-host.rect.yMax);
                }
            }
            if(stage==null||!stage.BoardOpen||stage.boardFocus==null||stage.boardFocus.outputCamera==null)return;
            var camera=stage.boardFocus.outputCamera;
            foreach(var node in stage.board.Nodes)
            {
                if(!focusNodes.TryGetValue(node.id,out var button))continue;
                var point=camera.WorldToScreenPoint(node.transform.position);
                if(RectTransformUtility.ScreenPointToLocalPointInRectangle(focusTargets,point,null,out var local))((RectTransform)button.transform).anchoredPosition=local;
                button.interactable=stage.CanSolve&&point.z>0;
            }
        }
        Vector2 DiagramPoint(TreeNodeData n)=>new Vector2(n.position.x*105,-85+n.position.y*170);
        void DrawEdge(TreeEdgeData edge)
        {
            var a=DiagramPoint(stage.Challenge.nodes.First(n=>n.id==edge.source));var b=DiagramPoint(stage.Challenge.nodes.First(n=>n.id==edge.destination));
            var rect=StageUITheme.Rect("Branch "+edge.source+" "+edge.destination,diagram);Place(rect,new Vector2(.5f,.5f),(a+b)*.5f,new Vector2(Vector2.Distance(a,b),3));rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
            var image=rect.gameObject.AddComponent<Image>();image.color=StageUITheme.ReadingInk;image.raycastTarget=false;
        }
        public void RefreshDiagram()=>RebuildDiagram();
        public void Refresh()
        {
            objectiveProgress.text="TREES  /  "+(stage.ChallengeIndex+1)+" OF "+stage.catalog.challenges.Count;
            objectiveTitle.text=stage.Challenge.title.ToUpperInvariant();objective.text=stage.Challenge.objective;
            feedback.text=stage.Feedback;
            int seconds=Mathf.CeilToInt(stage.timer.Remaining);clock.text=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
            book.GetComponentInChildren<TMP_Text>().text="OPEN TREE BOARD";
            bool active=stage.CanSolve;
            book.interactable=stage.CanAct&&!stage.BoardOpen&&stage.NearBook&&(stage.State==TreeStageState.ACTIVE||stage.State==TreeStageState.WAITING_FOR_ACTIVATION);
            connect.gameObject.SetActive(false);remove.gameObject.SetActive(false);clear.gameObject.SetActive(false);
            connect.interactable=remove.interactable=clear.interactable=active;
            numberRow.gameObject.SetActive(false);
            foreach(var button in numberRow.GetComponentsInChildren<Button>())button.interactable=active;
            string selected=stage.Answer.HasValue?"Number selected: "+stage.Answer:string.IsNullOrEmpty(stage.board.First)?"Tap a node on the board.":"Selected: "+stage.board.First+(string.IsNullOrEmpty(stage.board.Second)?"":" to "+stage.board.Second);
            selection.text="Use the Book Statue to open the interactive board.";selection.color=StageUITheme.Ink;
            foreach(var entry in nodes){entry.Value.interactable=false;entry.Value.GetComponentInChildren<TMP_Text>().color=entry.Key==stage.board.First||entry.Key==stage.board.Second?StageUITheme.Gold:StageUITheme.Ink;}
            guide.interactable=stage.Ready&&!stage.Moving&&(stage.State==TreeStageState.ACTIVE||stage.State==TreeStageState.WAITING_FOR_ACTIVATION);
            focusProgress.text=objectiveProgress.text;focusTitle.text=objectiveTitle.text;focusObjective.text=objective.text;
            focusFeedback.text=feedback.text;focusClock.text=clock.text;focusSelection.text=selected;
            LayoutFocusSidebar();
            focusConfirm.interactable=active;
            focusConnect.gameObject.SetActive(stage.Challenge.Editable);focusRemove.gameObject.SetActive(stage.Challenge.Editable);focusClear.gameObject.SetActive(stage.Challenge.Editable);
            focusConnect.interactable=focusRemove.interactable=focusClear.interactable=active;
            focusNumbers.gameObject.SetActive(stage.Challenge.type==TreeChallengeType.Depth||stage.Challenge.type==TreeChallengeType.Height);
            foreach(var button in focusNumbers.GetComponentsInChildren<Button>())button.interactable=active;
            focusExit.interactable=stage.BoardOpen&&!stage.Moving&&!stage.LessonOpen&&Time.timeScale>0&&(stage.State==TreeStageState.ACTIVE||stage.State==TreeStageState.VALIDATING);
            focusGuide.interactable=stage.CanAct&&stage.State==TreeStageState.ACTIVE;
            foreach(var button in focusNodes.Values)button.interactable=active;
            RefreshVisibility();
        }
        void RefreshVisibility()
        {
            owned.gameObject.SetActive(!resultVisible&&!stage.BoardOpen&&!stage.LessonOpen);
            focusOverlay.gameObject.SetActive(!resultVisible&&stage.BoardOpen&&!stage.LessonOpen);
            gameplayControls.SetActive(!resultVisible&&!stage.BoardOpen&&!stage.LessonOpen);
        }
        void BuildBook()
        {
            var overlay=StageUITheme.Rect("Trees Lesson",host);StageUITheme.Stretch(overlay,Vector2.zero,Vector2.one);var dim=overlay.gameObject.AddComponent<Image>();dim.color=new Color(0,0,0,.78f);
            var cover=StageUITheme.Rect("Book cover",overlay);Place(cover,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1480,850));StageUITheme.CompactSurface(cover.gameObject.AddComponent<Image>());
            cover.Find("CalmContentInset").gameObject.SetActive(false);
            var spine=StageUITheme.Rect("Book spine",cover);StageUITheme.Stretch(spine,new Vector2(.493f,.18f),new Vector2(.507f,.9f));var spineImage=spine.gameObject.AddComponent<Image>();spineImage.color=new Color(.17f,.075f,.032f);spineImage.raycastTarget=false;
            foreach(bool left in new[]{true,false})
            {var page=StageUITheme.Rect(left?"Left page":"Right page",cover);StageUITheme.Stretch(page,new Vector2(left?.055f:.515f,.18f),new Vector2(left?.485f:.945f,.9f));var paper=page.gameObject.AddComponent<Image>();paper.sprite=Resources.LoadAll<Sprite>("StageUI/ReadingPaper")[0];paper.type=Image.Type.Sliced;paper.pixelsPerUnitMultiplier=2;paper.raycastTarget=false;}
            var title=Text(cover,"Lesson heading",new Vector2(.09f,.75f),new Vector2(.45f,.85f),40);title.color=StageUITheme.ReadingInk;title.text="TREES FIELD GUIDE";
            lessonBody=Text(cover,"Lesson body",new Vector2(.09f,.25f),new Vector2(.45f,.73f),31,true);lessonBody.color=StageUITheme.ReadingInk;lessonBody.alignment=TextAlignmentOptions.TopLeft;
            var notes=Text(cover,"Tree conventions",new Vector2(.55f,.27f),new Vector2(.91f,.82f),31,true);notes.color=StageUITheme.ReadingInk;notes.alignment=TextAlignmentOptions.TopLeft;notes.text="TREE CHECKLIST\n\nRoot: no parent\nLeaf: no children\nEach other node: one parent\n\nDepth and height count edges.\nRoot depth and level: 0\n\nSpanning tree: connected, no cycles, <nobr>V - 1</nobr> edges.\n\nWrong confirmation: -10 seconds. Your connections are kept.";
            Action("Close lesson",cover,"RETURN TO GARDEN",new Vector2(0,-335),new Vector2(380,70),()=>stage.SetLesson(false));lesson=overlay.gameObject;lesson.SetActive(false);
        }
        public void ShowLesson(bool open){lessonBody.text=stage.Challenge.lesson;lesson.GetComponentsInChildren<Button>(true)[0].GetComponentInChildren<TMP_Text>().text=stage.BoardOpen?"BACK TO BOARD":"RETURN TO GARDEN";lesson.SetActive(open);RefreshVisibility();}
        public void ShowResult(bool success)
        {
            resultVisible=true;lesson.SetActive(false);RefreshVisibility();
            var overlay=StageUITheme.Rect("Trees Result",host);StageUITheme.Stretch(overlay,Vector2.zero,Vector2.one);overlay.gameObject.AddComponent<Image>().color=new Color(0,0,0,.8f);
            var panel=Panel("Result wood",overlay,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1040,690),hudMap);
            var title=Text(panel,"Result title",new Vector2(.08f,.72f),new Vector2(.92f,.9f),46);title.text=success?"TREES STAGE COMPLETE!":"TIME IS UP";
            var stats=Text(panel,"Statistics",new Vector2(.12f,.3f),new Vector2(.88f,.7f),34,true);int seconds=Mathf.FloorToInt(stage.timer.ActiveSeconds);stats.text="Time Played: "+(seconds/60).ToString("00")+":"+(seconds%60).ToString("00")+"\nCorrect: "+stage.Correct+"\nMistakes: "+stage.Mistakes;
            Action("NextOrRetryButton",panel,"PLAY AGAIN",new Vector2(-210,-225),new Vector2(360,80),stage.Restart);
            Action("MainMenuButton",panel,"MAIN MENU",new Vector2(210,-225),new Vector2(360,80),stage.Return);result=overlay.gameObject;
        }
    }
}
