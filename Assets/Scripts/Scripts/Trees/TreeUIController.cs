using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace LogicLegends.Trees
{
    /// <summary>The puzzle and its touch targets share the InformationBoard's world-space canvas.</summary>
    public sealed class TreeUIController : MonoBehaviour
    {
        public RectTransform host;
        public GameObject gameplayControls;
        public Sprite parchment,nodeCircle,hudObjective,hudStatus,hudMap;
        TreeStageManager stage;
        RectTransform owned,map,diagram,hud,boardCanvas,targets,numberRow,helpRoot;
        TMP_Text clock,progress,question,boardProgress,status,boardClock;
        Button help,submit,connect,remove,clear,back;
        readonly Dictionary<string,Button> nodes=new Dictionary<string,Button>();
        bool resultVisible;
        public RectTransform BoardCanvas=>boardCanvas;
        public TMP_Text QuestionText=>question;
        public Button SubmitButton=>submit;
        public Button BackButton=>back;
        public Button HelpButton=>help;
        public void Build(TreeStageManager owner)
        {
            stage=owner;host=(RectTransform)host.GetComponentInParent<Canvas>().transform;
            owned=StageUITheme.Rect("Trees HUD",host);StageUITheme.Stretch(owned,Vector2.zero,Vector2.one);
            var safe=StageUITheme.Rect("Trees safe area",owned);StageUITheme.Stretch(safe,Vector2.zero,Vector2.one);safe.gameObject.AddComponent<Crystal.SafeArea>();
            map=Panel("Tree minimap",safe,hudMap);Place(map,Vector2.one,new Vector2(-24,-24),new Vector2(280,250),Vector2.one);ExpandableMinimap.Attach(map);
            var caption=Text("Map title",map,"TREE MAP",23);Place(caption.rectTransform,new Vector2(.5f,1),new Vector2(0,-28),new Vector2(230,32));
            diagram=StageUITheme.Rect("Editable diagram",map);StageUITheme.Stretch(diagram,new Vector2(.12f,.10f),new Vector2(.88f,.79f));
            hud=Panel("Shared countdown",safe,hudStatus);Place(hud,Vector2.one,new Vector2(-24,-286),new Vector2(280,150),Vector2.one);
            var title=Text("CurrentStageLabel",hud,TrialCatalog.ForScene("Trees").Title,23);StageUITheme.Stretch(title.rectTransform,new Vector2(.07f,.67f),new Vector2(.93f,.91f));
            clock=Text("Timer",hud,"10:00",34);StageUITheme.Stretch(clock.rectTransform,new Vector2(.07f,.16f),new Vector2(.37f,.62f));
            progress=Text("StageProgress",hud,"",22);StageUITheme.Stretch(progress.rectTransform,new Vector2(.40f,.14f),new Vector2(.93f,.61f));
            StagePresentation.Hud(hud,title,clock,progress);
            help=Action("Trees Help",owned,"Help",new Vector2(320,-90),new Vector2(280,88),()=>stage.SetLesson(true));
            var settings=gameplayControls.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r=>r.name=="SettingsButton");StagePresentation.Help(help,settings);
            helpRoot=StageUITheme.Rect("Trees Help panel",host);StageUITheme.Stretch(helpRoot,Vector2.zero,Vector2.one);helpRoot.gameObject.AddComponent<Image>();helpRoot.gameObject.SetActive(false);
            BuildBoard();RebuildDiagram();Refresh();
        }
        void BuildBoard()
        {
            boardCanvas=StageUITheme.Rect("InformationBoard puzzle",stage.board.transform);
            var canvas=boardCanvas.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=Camera.main;canvas.sortingOrder=10;
            boardCanvas.gameObject.AddComponent<GraphicRaycaster>();boardCanvas.sizeDelta=new Vector2(1280,800);boardCanvas.localScale=Vector3.one*.0094f;boardCanvas.localPosition=new Vector3(0,0,-.16f);
            boardProgress=Text("Board question counter",boardCanvas,"",29);Place(boardProgress.rectTransform,new Vector2(.5f,.5f),new Vector2(-85,363),new Vector2(950,40));
            boardClock=Text("Board time",boardCanvas,"10:00",30);Place(boardClock.rectTransform,new Vector2(.5f,.5f),new Vector2(510,363),new Vector2(160,40));
            question=Text("Current tree question",boardCanvas,"",36,true);Place(question.rectTransform,new Vector2(.5f,.5f),new Vector2(0,303),new Vector2(1152,80));
            targets=StageUITheme.Rect("Tree node targets",boardCanvas);StageUITheme.Stretch(targets,Vector2.zero,Vector2.one);
            status=Text("Board selection and result",boardCanvas,"",27,true);Place(status.rectTransform,new Vector2(.5f,.5f),new Vector2(0,-175),new Vector2(1152,62));
            numberRow=StageUITheme.Rect("Board number answers",boardCanvas);Place(numberRow,new Vector2(.5f,.5f),new Vector2(0,-242),new Vector2(1120,72));
            submit=Action("Submit Tree",boardCanvas,"Submit",new Vector2(405,-328),new Vector2(252,88),stage.Confirm);
            connect=Action("Board Connect",boardCanvas,"Connect",new Vector2(-405,-328),new Vector2(252,88),()=>stage.Edit(false));
            remove=Action("Board Remove",boardCanvas,"Remove",new Vector2(-135,-328),new Vector2(252,88),()=>stage.Edit(true));
            clear=Action("Board Clear Selection",boardCanvas,"Clear Pick",new Vector2(135,-328),new Vector2(252,88),stage.ClearSelection);
            // Back and Help are always screen-space, including during the camera blend.
            back=Action("Exit Tree Board",host,"Back",new Vector2(0,0),new Vector2(230,88),stage.ExitBoard);
            back.gameObject.SetActive(false);
        }
        public void RebuildDiagram()
        {
            foreach(Transform child in diagram){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            foreach(Transform child in targets){child.gameObject.SetActive(false);Destroy(child.gameObject);}nodes.Clear();
            foreach(var e in stage.board.Edges)
            {
                var a=MapPoint(stage.Challenge.nodes.First(n=>n.id==e.source));var b=MapPoint(stage.Challenge.nodes.First(n=>n.id==e.destination));
                var edge=StageUITheme.Rect("Branch "+e.source+" "+e.destination,diagram);Place(edge,new Vector2(.5f,.5f),(a+b)*.5f,new Vector2(Vector2.Distance(a,b),3));edge.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);var ink=edge.gameObject.AddComponent<Image>();ink.color=StageUITheme.Ink;ink.raycastTarget=false;
            }
            foreach(var node in stage.Challenge.nodes)
            {
                var dot=Text("Map node "+node.id,diagram,node.id,20);Place(dot.rectTransform,new Vector2(.5f,.5f),MapPoint(node),new Vector2(32,32));
                string id=node.id;var target=StageUITheme.Rect("Board Node "+id,targets);Place(target,new Vector2(.5f,.5f),new Vector2(node.position.x*500,-110+node.position.y*330),new Vector2(70,70));
                var image=target.gameObject.AddComponent<Image>();image.color=Color.clear;
                var button=target.gameObject.AddComponent<Button>();button.transition=Selectable.Transition.None;button.targetGraphic=image;button.onClick.AddListener(()=>stage.SelectNode(id));nodes[id]=button;
            }
            foreach(Transform child in numberRow){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            int count=Mathf.Max(6,stage.Challenge.nodes.Count);
            float width=Mathf.Min(112,1120f/count);
            for(int i=0;i<count;i++){int answer=i;Action("Board Answer "+i,numberRow,i.ToString(),new Vector2((i-(count-1)*.5f)*width,0),new Vector2(width-8,72),()=>stage.SelectAnswer(answer));}
        }
        static Vector2 MapPoint(TreeNodeData n)=>new Vector2(n.position.x*95,-75+n.position.y*150);
        public void RefreshDiagram()
        {
            // Editing links must not destroy touch targets that still own a finger press.
            foreach(Transform child in diagram)if(child.name.StartsWith("Branch ")){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            foreach(var e in stage.board.Edges)
            {
                var a=MapPoint(stage.Challenge.nodes.First(n=>n.id==e.source));var b=MapPoint(stage.Challenge.nodes.First(n=>n.id==e.destination));
                var edge=StageUITheme.Rect("Branch "+e.source+" "+e.destination,diagram);Place(edge,new Vector2(.5f,.5f),(a+b)*.5f,new Vector2(Vector2.Distance(a,b),3));edge.SetAsFirstSibling();edge.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
                var ink=edge.gameObject.AddComponent<Image>();ink.color=StageUITheme.Ink;ink.raycastTarget=false;
            }
            Refresh();
        }
        public void Refresh()
        {
            if(stage==null)return;
            progress.text="Challenge "+(stage.ChallengeIndex+1)+" of 5\nQuestion "+(stage.QuestionIndex+1)+" of 2";
            int seconds=Mathf.CeilToInt(stage.timer.Remaining);clock.text=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
            boardProgress.text="CHALLENGE "+(stage.ChallengeIndex+1)+" OF 5  •  QUESTION "+(stage.QuestionIndex+1)+" OF 2";
            question.text=stage.State==TreeStageState.WAITING_FOR_ACTIVATION?"Use the hand at the Book Statue to start.":stage.Challenge.objective;
            boardClock.text=clock.text;
            bool active=stage.CanSolve;
            status.text=stage.State==TreeStageState.ACTIVE?stage.Feedback:stage.State==TreeStageState.VALIDATING?"Incorrect. −10s. Your work is kept.":stage.State==TreeStageState.CHALLENGE_COMPLETE?"Correct!":"";
            status.color=stage.State==TreeStageState.VALIDATING?new Color(.65f,.08f,.08f):stage.State==TreeStageState.CHALLENGE_COMPLETE?new Color(.06f,.4f,.12f):StageUITheme.ReadingInk;
            foreach(var pair in nodes)pair.Value.interactable=active;
            bool numeric=stage.Challenge.type==TreeChallengeType.Depth||stage.Challenge.type==TreeChallengeType.Height;
            numberRow.gameObject.SetActive(numeric);
            foreach(var b in numberRow.GetComponentsInChildren<Button>())
            {b.interactable=active;b.GetComponentInChildren<TMP_Text>().color=stage.Answer.HasValue&&b.name=="Board Answer "+stage.Answer?StageUITheme.Gold:Color.white;}
            connect.gameObject.SetActive(stage.Challenge.Editable);remove.gameObject.SetActive(stage.Challenge.Editable);
            connect.interactable=remove.interactable=clear.interactable=submit.interactable=active;
            if(!stage.Challenge.Editable){Place((RectTransform)clear.transform,new Vector2(.5f,.5f),new Vector2(-145,-328),new Vector2(280,88));Place((RectTransform)submit.transform,new Vector2(.5f,.5f),new Vector2(145,-328),new Vector2(280,88));}
            else {Place((RectTransform)clear.transform,new Vector2(.5f,.5f),new Vector2(135,-328),new Vector2(252,88));Place((RectTransform)submit.transform,new Vector2(.5f,.5f),new Vector2(405,-328),new Vector2(252,88));}
            var raycaster=boardCanvas.GetComponent<GraphicRaycaster>();raycaster.enabled=stage.BoardOpen&&!stage.LessonOpen;
            owned.gameObject.SetActive(!resultVisible&&!stage.LessonOpen);map.gameObject.SetActive(!stage.BoardOpen);
            hud.gameObject.SetActive(!stage.BoardOpen);help.gameObject.SetActive(!stage.BoardOpen);
            back.gameObject.SetActive(stage.BoardOpen&&!stage.LessonOpen&&!resultVisible);back.interactable=stage.BoardOpen&&!stage.Moving&&!stage.LessonOpen;
            gameplayControls.SetActive(!resultVisible&&!stage.BoardOpen&&!stage.LessonOpen);
            help.interactable=stage.Ready&&!stage.Moving;
        }
        void LateUpdate()
        {
            if(back==null)return;
            var safe=Screen.safeArea;safe=Rect.MinMaxRect(Mathf.Clamp(safe.xMin,0,Screen.width),Mathf.Clamp(safe.yMin,0,Screen.height),Mathf.Clamp(safe.xMax,0,Screen.width),Mathf.Clamp(safe.yMax,0,Screen.height));float left=safe.xMin/Screen.width*host.rect.width;float top=(Screen.height-safe.yMax)/Screen.height*host.rect.height;
            Place((RectTransform)back.transform,new Vector2(0,1),new Vector2(left+140,-top-65),new Vector2(230,88));
            if(!map.GetComponent<ExpandableMinimap>().IsOpen)
            {
                float scale=Mathf.Clamp(Mathf.Min(host.rect.height,host.rect.width)/1080f,.60f,1f);
                map.localScale=hud.localScale=Vector3.one*scale;hud.anchoredPosition=new Vector2(-24,-24-250*scale-12);
            }
        }
        public void ShowLesson(bool open)
        {
            helpRoot.gameObject.SetActive(open);
            if(open)StageHelpPage.Install(helpRoot,"Trees",()=>stage.SetLesson(false));
            Refresh();
        }
        public void ShowResult(bool success)
        {
            resultVisible=true;helpRoot.gameObject.SetActive(false);Refresh();
            var view=StageResultCard.Build(host,stage.Return);view.Stage.text=TrialCatalog.ForScene("Trees").Title;view.Heading.text=success?"TRIAL COMPLETE":"TIME IS UP";
            view.Summary.text="Time Played: "+StageJourneyUI.FormatTime(stage.timer.ActiveSeconds)+"\nCorrect: "+stage.Correct+" of 10\nMistakes: "+stage.Mistakes;
            view.Primary.GetComponentInChildren<TMP_Text>().text="PLAY AGAIN";view.Primary.onClick.AddListener(stage.Restart);view.Extra.gameObject.SetActive(false);
            if(success)view.Root.GetComponent<StageResultLayout>().Complete(10);
        }
        RectTransform Panel(string name,Transform parent,Sprite sprite)
        {var rt=StageUITheme.Rect(name,parent);var image=rt.gameObject.AddComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4.5f;image.raycastTarget=false;return rt;}
        static TMP_Text Text(string name,Transform parent,string value,float size,bool reading=false)
        {var text=StageUITheme.Text(name,parent,value,size,reading);text.color=StageUITheme.ReadingInk;text.enableAutoSizing=true;text.fontSizeMin=size-2;text.fontSizeMax=size;text.raycastTarget=false;return text;}
        static Button Action(string name,Transform parent,string label,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction callback)
        {var b=StageUITheme.Button(name,parent,label,callback);Place((RectTransform)b.transform,new Vector2(.5f,.5f),pos,size);b.GetComponent<Image>().pixelsPerUnitMultiplier=4.5f;var t=b.GetComponentInChildren<TMP_Text>();t.fontSizeMin=24;t.fontSizeMax=30;return b;}
        static void Place(RectTransform rt,Vector2 anchor,Vector2 pos,Vector2 size,Vector2? pivot=null)
        {rt.anchorMin=rt.anchorMax=anchor;rt.pivot=pivot??new Vector2(.5f,.5f);rt.anchoredPosition=pos;rt.sizeDelta=size;}
    }
}
