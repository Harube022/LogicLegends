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
        TreeStageManager stage;
        TMP_Text objective,feedback,clock,selection,lessonBody;
        RectTransform diagram,owned,actions,numberRow;
        GameObject lesson,result;
        Button book,connect,remove,clear,guide;
        readonly Dictionary<string,Button> nodes=new Dictionary<string,Button>();
        public void Build(TreeStageManager owner)
        {
            stage=owner;owned=StageUITheme.Rect("Trees HUD",host);StageUITheme.Stretch(owned,Vector2.zero,Vector2.one);
            var info=Panel("Tree objective",owned,new Vector2(0,1),new Vector2(225,-295),new Vector2(410,250));
            objective=Text(info,"Objective",new Vector2(.09f,.12f),new Vector2(.91f,.88f),28,true);
            var feed=Panel("Feedback",owned,new Vector2(0,1),new Vector2(225,-495),new Vector2(410,135));
            feedback=Text(feed,"Feedback",new Vector2(.09f,.12f),new Vector2(.91f,.88f),24,true);
            var map=Panel("Tree minimap",owned,new Vector2(1,1),new Vector2(-185,-180),new Vector2(330,320));
            var caption=Text(map,"Diagram title",new Vector2(.15f,.84f),new Vector2(.85f,.94f),24);caption.text="TREE STRUCTURE";
            diagram=StageUITheme.Rect("Editable diagram",map);StageUITheme.Stretch(diagram,new Vector2(.13f,.13f),new Vector2(.87f,.82f));
            var time=Panel("Shared countdown",owned,new Vector2(1,1),new Vector2(-185,-385),new Vector2(330,85));
            clock=Text(time,"Timer",new Vector2(.1f,.1f),new Vector2(.9f,.9f),32);
            actions=StageUITheme.Rect("Tree actions",owned);Place(actions,new Vector2(.5f,0),new Vector2(0,340),new Vector2(780,175));
            book=Action("Book interaction",actions,"ACTIVATE BOOK",new Vector2(0,48),new Vector2(290,65),stage.InteractBook);
            selection=Text(actions,"Selected endpoints",new Vector2(0,.02f),new Vector2(1,.25f),24,true);
            connect=Action("Connect",actions,"CONNECT",new Vector2(-250,-22),new Vector2(235,65),()=>stage.Edit(false));
            remove=Action("Remove",actions,"REMOVE",new Vector2(0,-22),new Vector2(235,65),()=>stage.Edit(true));
            clear=Action("Clear selection",actions,"CLEAR PICK",new Vector2(250,-22),new Vector2(235,65),stage.ClearSelection);
            guide=Action("Trees Field Guide",owned,"FIELD GUIDE",Vector2.zero,new Vector2(210,65),()=>stage.SetLesson(true));Place((RectTransform)guide.transform,new Vector2(0,1),new Vector2(130,-605),new Vector2(210,65));
            numberRow=StageUITheme.Rect("Number answers",actions);Place(numberRow,new Vector2(.5f,.5f),new Vector2(0,-22),new Vector2(720,65));
            for(int i=0;i<=5;i++){int answer=i;Action("Answer "+i,numberRow,i.ToString(),new Vector2(-300+i*120,0),new Vector2(110,65),()=>stage.SelectAnswer(answer));}
            BuildBook();RebuildDiagram();Refresh();
        }
        RectTransform Panel(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {var rect=StageUITheme.Rect(name,parent);Place(rect,anchor,position,size);var image=rect.gameObject.AddComponent<Image>();StageUITheme.CompactSurface(image);image.raycastTarget=false;return rect;}
        static void Place(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size){rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;}
        static TMP_Text Text(Transform parent,string name,Vector2 min,Vector2 max,float size,bool reading=false)
        {var text=StageUITheme.Text(name,parent,"",size,reading);StageUITheme.Stretch(text.rectTransform,min,max);text.enableAutoSizing=true;text.fontSizeMin=size-3;text.fontSizeMax=size;return text;}
        static Button Action(string name,Transform parent,string title,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction callback)
        {var button=StageUITheme.Button(name,parent,title,callback);Place((RectTransform)button.transform,new Vector2(.5f,.5f),pos,size);return button;}
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
        }
        Vector2 DiagramPoint(TreeNodeData n)=>new Vector2(n.position.x*105,-85+n.position.y*170);
        void DrawEdge(TreeEdgeData edge)
        {
            var a=DiagramPoint(stage.Challenge.nodes.First(n=>n.id==edge.source));var b=DiagramPoint(stage.Challenge.nodes.First(n=>n.id==edge.destination));
            var rect=StageUITheme.Rect("Branch "+edge.source+" "+edge.destination,diagram);Place(rect,new Vector2(.5f,.5f),(a+b)*.5f,new Vector2(Vector2.Distance(a,b),3));rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
            var image=rect.gameObject.AddComponent<Image>();image.color=StageUITheme.Gold;image.raycastTarget=false;
        }
        public void RefreshDiagram()=>RebuildDiagram();
        public void Refresh()
        {
            objective.text="TREES  /  "+(stage.ChallengeIndex+1)+" OF "+stage.catalog.challenges.Count+"\n"+stage.Challenge.title.ToUpperInvariant()+"\n\n"+stage.Challenge.objective;
            feedback.text=stage.Feedback;
            int seconds=Mathf.CeilToInt(stage.timer.Remaining);clock.text=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
            book.GetComponentInChildren<TMP_Text>().text=stage.State==TreeStageState.WAITING_FOR_ACTIVATION?"ACTIVATE BOOK":"CONFIRM AT BOOK";
            bool active=stage.CanAct&&stage.State==TreeStageState.ACTIVE;
            book.interactable=stage.CanAct&&stage.NearBook&&(active||stage.State==TreeStageState.WAITING_FOR_ACTIVATION);
            connect.gameObject.SetActive(stage.Challenge.Editable);remove.gameObject.SetActive(stage.Challenge.Editable);clear.gameObject.SetActive(stage.Challenge.Editable);
            connect.interactable=remove.interactable=clear.interactable=active;
            numberRow.gameObject.SetActive(stage.Challenge.type==TreeChallengeType.Depth||stage.Challenge.type==TreeChallengeType.Height);
            foreach(var button in numberRow.GetComponentsInChildren<Button>())button.interactable=active;
            selection.text=stage.Answer.HasValue?"Number selected: "+stage.Answer:stage.board.First==null?"Tap a node in the garden or the diagram.":"Selected: "+stage.board.First+(stage.board.Second==null?"":" to "+stage.board.Second);
            foreach(var entry in nodes){entry.Value.interactable=active;entry.Value.GetComponentInChildren<TMP_Text>().color=entry.Key==stage.board.First||entry.Key==stage.board.Second?StageUITheme.Gold:StageUITheme.Ink;}
            guide.interactable=stage.Ready&&!stage.Moving&&(stage.State==TreeStageState.ACTIVE||stage.State==TreeStageState.WAITING_FOR_ACTIVATION);
        }
        void BuildBook()
        {
            var overlay=StageUITheme.Rect("Trees Lesson",host);StageUITheme.Stretch(overlay,Vector2.zero,Vector2.one);var dim=overlay.gameObject.AddComponent<Image>();dim.color=new Color(0,0,0,.78f);
            var cover=Panel("Book cover",overlay,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1480,850));
            cover.Find("CalmContentInset").gameObject.SetActive(false);
            var spine=StageUITheme.Rect("Book spine",cover);StageUITheme.Stretch(spine,new Vector2(.493f,.18f),new Vector2(.507f,.9f));var spineImage=spine.gameObject.AddComponent<Image>();spineImage.color=new Color(.17f,.075f,.032f);spineImage.raycastTarget=false;
            foreach(bool left in new[]{true,false})
            {var page=StageUITheme.Rect(left?"Left page":"Right page",cover);StageUITheme.Stretch(page,new Vector2(left?.055f:.515f,.18f),new Vector2(left?.485f:.945f,.9f));var paper=page.gameObject.AddComponent<Image>();paper.sprite=Resources.LoadAll<Sprite>("StageUI/ReadingPaper")[0];paper.type=Image.Type.Sliced;paper.pixelsPerUnitMultiplier=2;paper.raycastTarget=false;}
            var title=Text(cover,"Lesson heading",new Vector2(.09f,.75f),new Vector2(.45f,.85f),40);title.color=StageUITheme.ReadingInk;title.text="TREES FIELD GUIDE";
            lessonBody=Text(cover,"Lesson body",new Vector2(.09f,.25f),new Vector2(.45f,.73f),31,true);lessonBody.color=StageUITheme.ReadingInk;lessonBody.alignment=TextAlignmentOptions.TopLeft;
            var notes=Text(cover,"Tree conventions",new Vector2(.55f,.27f),new Vector2(.91f,.82f),31,true);notes.color=StageUITheme.ReadingInk;notes.alignment=TextAlignmentOptions.TopLeft;notes.text="TREE CHECKLIST\n\nRoot: no parent\nLeaf: no children\nEach other node: one parent\n\nDepth and height count edges.\nRoot depth and level: 0\n\nSpanning tree: connected, no cycles, <nobr>V - 1</nobr> edges.\n\nWrong confirmation: -10 seconds. Your connections are kept.";
            Action("Close lesson",cover,"RETURN TO GARDEN",new Vector2(0,-335),new Vector2(380,70),()=>stage.SetLesson(false));lesson=overlay.gameObject;lesson.SetActive(false);
        }
        public void ShowLesson(bool open){lessonBody.text=stage.Challenge.lesson;lesson.SetActive(open);owned.gameObject.SetActive(!open);gameplayControls.SetActive(!open);}
        public void ShowResult(bool success)
        {
            lesson.SetActive(false);owned.gameObject.SetActive(false);gameplayControls.SetActive(false);
            var overlay=StageUITheme.Rect("Trees Result",host);StageUITheme.Stretch(overlay,Vector2.zero,Vector2.one);overlay.gameObject.AddComponent<Image>().color=new Color(0,0,0,.8f);
            var panel=Panel("Result wood",overlay,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1040,690));
            var title=Text(panel,"Result title",new Vector2(.08f,.72f),new Vector2(.92f,.9f),46);title.text=success?"TREES STAGE COMPLETE!":"TIME IS UP";
            var stats=Text(panel,"Statistics",new Vector2(.12f,.3f),new Vector2(.88f,.7f),34,true);int seconds=Mathf.FloorToInt(stage.timer.ActiveSeconds);stats.text="Time Played: "+(seconds/60).ToString("00")+":"+(seconds%60).ToString("00")+"\nCorrect: "+stage.Correct+"\nMistakes: "+stage.Mistakes;
            Action("NextOrRetryButton",panel,"PLAY AGAIN",new Vector2(-210,-225),new Vector2(360,80),stage.Restart);
            Action("MainMenuButton",panel,"MAIN MENU",new Vector2(210,-225),new Vector2(360,80),stage.Return);result=overlay.gameObject;
        }
    }
}
