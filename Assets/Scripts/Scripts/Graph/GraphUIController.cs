using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Graph
{
    public sealed class GraphUIController : MonoBehaviour
    {
        [Header("New Graph HUD artwork")]
        public Sprite hudMap, hudObjective, hudStatus;
        [Header("Original book and marker artwork")]
        public Sprite parchment;
        public Sprite pointer;
        public RectTransform settingsButton;
        public RectTransform host;
        public GameObject gameplayControls;
        private GraphStageManager stage;
        private TMP_Text objective, route, feedback, clock, penalty, resultText, lessonBody, fieldNotes;
        private float penaltyUntil;
        private RectTransform root, map;
        private GameObject result, lesson, choices;
        private Button confirm, retry, lessonButton;
        private readonly List<Button> answerButtons = new List<Button>();
        private readonly Dictionary<string, Image> mapVertices = new Dictionary<string, Image>();
        private readonly Dictionary<GraphEdgeData, Image> mapEdges = new Dictionary<GraphEdgeData, Image>();
        private RectTransform playerMarker;
        private static readonly Color ChartInk = new Color32(41, 26, 15, 255);
        private static readonly Color MapNodeFill = new Color32(80, 51, 34, 255);
        private static readonly Color MapNodeText = new Color32(255, 242, 212, 255);
        private TMP_FontAsset routeFont;
        public GameObject ResultPanel => result;

        public void Build(GraphStageManager manager)
        {
            stage = manager;
            routeFont = Instantiate(StageUITheme.ReadingFont);
            // TMP owns and destroys its atlas textures/material on font destruction.
            // A runtime font copy must therefore own copies of those resources too.
            var sourceAtlases = StageUITheme.ReadingFont.atlasTextures;
            var localAtlases = new Texture2D[sourceAtlases.Length];
            for (int i = 0; i < sourceAtlases.Length; i++)
            {
                var source = sourceAtlases[i];
                if (source == null) continue;
                // Static font atlases are not CPU-readable. Copy their GPU pixels
                // without changing the project's texture import settings.
                localAtlases[i] = new Texture2D(source.width, source.height, source.format, source.mipmapCount > 1);
                localAtlases[i].filterMode = source.filterMode;
                localAtlases[i].wrapMode = source.wrapMode;
                Graphics.CopyTexture(source, localAtlases[i]);
            }
            routeFont.atlasTextures = localAtlases;
            routeFont.material = Instantiate(StageUITheme.ReadingFont.material);
            routeFont.material.mainTexture = localAtlases[0];
            // Do not populate the project's shared dynamic LiberationSans fallback atlas.
            routeFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
            var math = Resources.Load<TMP_FontAsset>("Fonts & Materials/LogicFallback_SDF");
            if (math != null) routeFont.fallbackFontAssetTable.Add(math);
            root = StageUITheme.Rect("Graph HUD", host);
            StageUITheme.Stretch(root, Vector2.zero, Vector2.one);

            var objectivePanel = Panel("Objective", root, new Vector2(0, 1), new Vector2(30, -175), new Vector2(360, 145), new Vector2(0, 1), true);
            objective = Text(objectivePanel, "Objective text", "", 23, true);
            objective.fontSizeMin = 19;
            Inset(objective.rectTransform, 20, 16);
            var mapPanel = Panel("Graph minimap", root, Vector2.one, new Vector2(-30, -24), new Vector2(270, 270), Vector2.one, true);
            var mapTitle = Text(mapPanel, "Map title", "GRAPH MAP", 18, false);
            Place(mapTitle.rectTransform, new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(190, 26), new Vector2(.5f, .5f));
            map = StageUITheme.Rect("Map", mapPanel);
            Inset(map, 36, 20); map.offsetMax = new Vector2(-36, -46);
            var timerPanel = Panel("Clock", root, Vector2.one, new Vector2(-30, -305), new Vector2(270, 100), Vector2.one);
            var timerTitle = Text(timerPanel, "Clock title", "TIME LEFT", 18, false);
            Place(timerTitle.rectTransform, new Vector2(.5f, 1), new Vector2(0, -23), new Vector2(220, 24), new Vector2(.5f, .5f));
            clock = Text(timerPanel, "Countdown", "05:00", 34, false); Inset(clock.rectTransform, 12, 12);
            clock.rectTransform.offsetMax = new Vector2(-12, -40); clock.color = StageUITheme.ReadingInk;
            clock.enableAutoSizing = false; clock.fontSize = 32;
            penalty = Text(root, "Timer deduction", "-10", 36, false);
            Place(penalty.rectTransform, Vector2.one, new Vector2(-310, -355), new Vector2(78, 60), new Vector2(1, .5f));
            penalty.color = new Color(1f, .4f, .22f); penalty.gameObject.SetActive(false);

            var routePanel = Panel("Recorded route", root, new Vector2(0, 1), new Vector2(30, -330), new Vector2(360, 60), new Vector2(0, 1));
            route = Text(routePanel, "Route", stage.Challenge.start, 23, true); route.font = routeFont; Inset(route.rectTransform, 20, 10);
            var feedbackPanel = Panel("Feedback", root, new Vector2(0, 1), new Vector2(30, -400), new Vector2(360, 100), new Vector2(0, 1));
            feedback = Text(feedbackPanel, "Feedback text", "", 22, true); Inset(feedback.rectTransform, 20, 10);
            confirm = Action("Confirm", root, "INTERACT", () => { if (stage.State == GraphStageState.WAITING_FOR_ACTIVATION) stage.InteractBook(); else stage.Confirm(); }, new Vector2(0, 160));
            retry = Action("Retry", root, "RETRY ROUTE", () => { if (stage.Challenge.type == GraphChallengeType.Degree) stage.ClearSelection(); else stage.RetryRoute(); }, new Vector2(0, 65));
            lessonButton = Action("Lesson", root, "FIELD GUIDE", () => stage.SetLesson(true), new Vector2(0, 70));
            Place((RectTransform)lessonButton.transform,new Vector2(0,1),new Vector2(210,-91),new Vector2(140,72),new Vector2(0,.5f));
            var guideLabel = lessonButton.GetComponentInChildren<TMP_Text>();
            guideLabel.fontSizeMin = 18; guideLabel.fontSizeMax = 24;
            guideLabel.rectTransform.offsetMin = new Vector2(9, 6); guideLabel.rectTransform.offsetMax = new Vector2(-9, -6);

            choices = StageUITheme.Rect("Degree choices", root).gameObject;
            Place((RectTransform)choices.transform, new Vector2(.5f, 0), new Vector2(0, 250), new Vector2(480, 128), new Vector2(.5f, 0));
            for (int i = 0; i <= 6; i++)
            {
                int number = i;
                var b = StageUITheme.Button("Answer " + i, choices.transform, i.ToString(), () => stage.SelectAnswer(number));
                Place((RectTransform)b.transform, new Vector2(0, 1), new Vector2(55 + (i%4)*120, -32-(i/4)*68), new Vector2(110, 64), new Vector2(.5f, .5f));
                b.GetComponent<Image>().type = Image.Type.Simple;
                b.GetComponent<Image>().preserveAspect = true;
                var answerLabel=b.GetComponentInChildren<TMP_Text>();
                answerLabel.fontSizeMin=18;answerLabel.fontSizeMax=22;
                answerLabel.rectTransform.offsetMin=new Vector2(6,2);answerLabel.rectTransform.offsetMax=new Vector2(-6,-2);
                answerButtons.Add(b);
            }
            result = Modal("Graph result");
            var resultPanel = Panel("Result frame", result.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 740), new Vector2(.5f, .5f), true);
            resultText = Text(resultPanel, "Summary", "", 42, false);
            StageUITheme.Stretch(resultText.rectTransform, new Vector2(.08f, .35f), new Vector2(.92f, .9f));
            Action("Restart", resultPanel, "PLAY AGAIN", stage.RestartStage, new Vector2(-170, 160));
            Action("MainMenuButton", resultPanel, "MAIN MENU", stage.ReturnToMenu, new Vector2(170, 160));
            result.SetActive(false);

            lesson = Modal("Graph field guide");
            // Use the Study Library's brown cover and twin parchment pages for lessons.
            var cover = StageUITheme.Rect("Book cover", lesson.transform);
            Place(cover, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1320, 800), new Vector2(.5f, .5f));
            cover.gameObject.AddComponent<Image>().color = new Color(.23f, .105f, .045f);
            var coverOutline = cover.gameObject.AddComponent<Outline>();
            coverOutline.effectColor = new Color(.08f, .035f, .015f);
            coverOutline.effectDistance = new Vector2(8, -8);
            for (int i = 0; i < 2; i++)
            {
                var paper = StageUITheme.Rect("Reading page " + i, cover);
                StageUITheme.Stretch(paper, new Vector2(i == 0 ? .045f : .51f, .19f), new Vector2(i == 0 ? .49f : .955f, .92f));
                var image = paper.gameObject.AddComponent<Image>(); image.sprite = parchment; image.preserveAspect = true; image.raycastTarget = false;
            }
            var spine = StageUITheme.Rect("Spine", cover); StageUITheme.Stretch(spine, new Vector2(.494f, .19f), new Vector2(.506f, .92f));
            spine.gameObject.AddComponent<Image>().color = new Color(.23f, .105f, .045f);
            lessonBody = Text(cover, "Lesson", "", 30, true); lessonBody.color = StageUITheme.ReadingInk;
            StageUITheme.Stretch(lessonBody.rectTransform, new Vector2(.11f, .26f), new Vector2(.43f, .82f));
            fieldNotes = Text(cover, "Field notes", "", 28, true);
            fieldNotes.color = StageUITheme.ReadingInk; StageUITheme.Stretch(fieldNotes.rectTransform, new Vector2(.58f, .25f), new Vector2(.89f, .82f));
            Action("Close book", cover, "CLOSE BOOK", () => stage.SetLesson(false), new Vector2(0, 87));
            lesson.SetActive(false);
        }
        public void Refresh()
        {
            // On tablet ratios the original joystick and hand occupy the lower center.
            // Move only Graph-owned actions into the free left column.
            bool compact = host.rect.width < 1700;
            if (settingsButton != null)
            {
                var corners = new Vector3[4]; settingsButton.GetWorldCorners(corners);
                Vector3 right = root.InverseTransformPoint((corners[2] + corners[3]) * .5f);
                Place((RectTransform)lessonButton.transform, new Vector2(0,1),
                    new Vector2(right.x - root.rect.xMin + 14, right.y - root.rect.yMax), new Vector2(140,72), new Vector2(0,.5f));
            }
            penalty.gameObject.SetActive(Time.unscaledTime < penaltyUntil);
            var actionAnchor = compact ? new Vector2(0,1) : new Vector2(.5f,0);
            Place((RectTransform)confirm.transform,actionAnchor,compact?new Vector2(180,-540):new Vector2(0,155),new Vector2(240,64),new Vector2(.5f,.5f));
            Place((RectTransform)retry.transform,actionAnchor,compact?new Vector2(180,-612):new Vector2(0,80),new Vector2(240,64),new Vector2(.5f,.5f));
            Place((RectTransform)choices.transform,compact?new Vector2(0,1):new Vector2(.5f,0),
                compact?new Vector2(390,-215):new Vector2(0,250), compact?new Vector2(240,272):new Vector2(480,128), compact?new Vector2(0,1):new Vector2(.5f,0));
            for(int i=0;i<answerButtons.Count;i++)
                Place((RectTransform)answerButtons[i].transform,new Vector2(0,1),
                    compact?new Vector2(55+(i%2)*120,-32-(i/2)*68):new Vector2(55+(i%4)*120,-32-(i/4)*68),new Vector2(110,64),new Vector2(.5f,.5f));
            bool active = stage.State == GraphStageState.ACTIVE;
            objective.text = "<color=#291A0F><b>GRAPH  " + (stage.ChallengeIndex + 1) + "/" + stage.ChallengeCount + "  /  " + stage.Challenge.title.ToUpperInvariant() + "</b></color>\n" + stage.Challenge.objective;
            route.text = stage.Challenge.type == GraphChallengeType.Degree ? "SELECTED DEGREE: " + (stage.Answer.HasValue ? stage.Answer.Value.ToString() : "—") : "ROUTE   " + string.Join(" → ", stage.Tracker.Route);
            feedback.text = stage.Feedback;
            clock.text = StageJourneyUI.FormatTime(stage.timer.Remaining) + (stage.timer.Running ? "" : "  PAUSED");
            confirm.GetComponentInChildren<TMP_Text>().text = active ? "CONFIRM" : "INTERACT";
            confirm.interactable = stage.CanAct && (active || (stage.State == GraphStageState.WAITING_FOR_ACTIVATION && stage.NearBook));
            retry.interactable = stage.CanAct && active;
            retry.GetComponentInChildren<TMP_Text>().text = stage.Challenge.type == GraphChallengeType.Degree ? "CLEAR SELECTION" : "RETRY ROUTE";
            lessonButton.interactable = stage.CanAct && (active || stage.State == GraphStageState.WAITING_FOR_ACTIVATION);
            choices.SetActive(stage.Challenge.type == GraphChallengeType.Degree && active);
            for (int i = 0; i < answerButtons.Count; i++)
            {
                answerButtons[i].interactable = stage.CanAct && active;
                answerButtons[i].GetComponentInChildren<TMP_Text>().text = stage.Answer == i ? "[" + i + "]" : i.ToString();
            }
            foreach (var item in mapVertices)
            {
                bool current = item.Key == stage.Tracker.Current;
                item.Value.color = current ? StageUITheme.Gold : MapNodeFill;
                item.Value.GetComponentInChildren<TMP_Text>().color = current ? ChartInk : MapNodeText;
            }
            foreach (var item in mapEdges)
            {
                bool walked = false;
                for (int i=1; i<stage.Tracker.Route.Count && !walked; i++)
                    walked = (stage.Tracker.Route[i-1]==item.Key.source && stage.Tracker.Route[i]==item.Key.destination) ||
                        (!item.Key.directed && stage.Tracker.Route[i-1]==item.Key.destination && stage.Tracker.Route[i]==item.Key.source);
                item.Value.color = walked ? GraphEdge.WalkedColor : GraphEdge.AvailableColor;
            }
            if (playerMarker != null && stage.LocalPlayer != null)
            {
                playerMarker.anchoredPosition = MapPoint(stage.arena.InverseTransformPoint(stage.LocalPlayer.transform.position));
                // The source Pointer points down. North (+Z) points up on this map.
                playerMarker.localRotation = Quaternion.Euler(0,0,180-stage.LocalPlayer.transform.eulerAngles.y+stage.arena.eulerAngles.y);
            }
        }
        public void RebuildMap()
        {
            foreach (Transform child in map) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            mapVertices.Clear();
            mapEdges.Clear();
            Vector2 mapCenter = Vector2.zero;
            foreach (var vertex in stage.Challenge.vertices) mapCenter += MapPoint(vertex.position);
            mapCenter /= Mathf.Max(1, stage.Challenge.vertices.Count);
            foreach (var edge in stage.Challenge.edges)
            {
                Vector2 a = MapPoint(stage.Challenge.Vertex(edge.source).position), b = MapPoint(stage.Challenge.Vertex(edge.destination).position);
                var line = StageUITheme.Rect("Edge", map); Place(line, new Vector2(.5f, .5f), (a + b) / 2, new Vector2((b-a).magnitude, 3), new Vector2(.5f, .5f));
                line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y-a.y, b.x-a.x)*Mathf.Rad2Deg);
                var image = line.gameObject.AddComponent<Image>(); image.color = GraphEdge.AvailableColor; image.raycastTarget = false; mapEdges[edge] = image;
                if (edge.directed)
                {
                    var arrow = Text(map, "Direction", "→", 18, true); arrow.font = routeFont;
                    // Keep the direction glyph clear of the path, on the side
                    // facing away from the center of the graph.
                    var outward = new Vector2(-(b-a).y, (b-a).x).normalized;
                    if (Vector2.Dot(outward, (a+b)*.5f-mapCenter) < 0) outward = -outward;
                    Place(arrow.rectTransform, new Vector2(.5f, .5f), Vector2.Lerp(a,b,.55f)+outward*12, new Vector2(18,18), new Vector2(.5f,.5f));
                    arrow.rectTransform.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
                }
            }
            foreach (var vertex in stage.Challenge.vertices)
            {
                var rect = StageUITheme.Rect(vertex.id, map); Place(rect, new Vector2(.5f,.5f), MapPoint(vertex.position), new Vector2(28,28), new Vector2(.5f,.5f));
                var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget=false; mapVertices[vertex.id]=image;
                var label=Text(rect,"Label",vertex.label,21,false); Inset(label.rectTransform,0,0);
            }
            // The source arrow's tip is at its bottom center. Anchor that tip to
            // the world position, preserving the sprite ratio at every heading.
            float markerHeight = pointer == null ? 22 : 18 * pointer.rect.height / pointer.rect.width;
            playerMarker=StageUITheme.Rect("Player marker",map); Place(playerMarker,new Vector2(.5f,.5f),Vector2.zero,new Vector2(18,markerHeight),new Vector2(.5f,0));
            var marker=playerMarker.gameObject.AddComponent<Image>(); marker.sprite=pointer; marker.preserveAspect=true;
            marker.color=new Color32(22, 120, 76, 255); marker.raycastTarget=false;
        }
        private Vector2 MapPoint(Vector3 p) => new Vector2(p.x / GraphGeometry.LayoutScale * 6.2f, (p.z / GraphGeometry.LayoutScale - 12) * 6.2f);
        public void ShowPenalty() { penaltyUntil = Time.unscaledTime + 2.5f; penalty.gameObject.SetActive(true); }
        public void ShowResult(bool success)
        {
            lesson.SetActive(false); result.SetActive(true); result.transform.SetAsLastSibling();
            if (gameplayControls != null) gameplayControls.SetActive(false);
            resultText.text = (success ? "GRAPH STAGE COMPLETE!" : "TIME'S UP") + "\n\nTime Played: " + StageJourneyUI.FormatTime(stage.timer.ActiveSeconds) + "\nCorrect: " + stage.Correct + "\nMistakes: " + stage.Mistakes;
        }
        public void ShowLesson(bool open)
        {
            lesson.SetActive(open);
            if (open)
            {
                lessonBody.text = stage.Challenge.title.ToUpperInvariant() + "\n\n" + stage.Challenge.lesson;
                fieldNotes.text = "ON YOUR JOURNEY\n\nMove with WASD or the joystick.\n\nCross a bridge completely to record a move.\n\nConfirm remotely at your destination.\n\nRETRY ROUTE returns you to " + stage.Challenge.start + ". Your countdown and mistakes stay the same.\n\nWrong confirmations cost 10 seconds.";
                lesson.transform.SetAsLastSibling();
            }
        }
        private GameObject Modal(string name)
        {
            var rect=StageUITheme.Rect(name,root); StageUITheme.Stretch(rect,Vector2.zero,Vector2.one);
            var image=rect.gameObject.AddComponent<Image>(); image.color=new Color(.025f,.02f,.018f,.88f);
            return rect.gameObject;
        }
        private RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot, bool largePanel = false)
        {
            var rect=StageUITheme.Rect(name,parent); Place(rect,anchor,pos,size,pivot);
            var image=rect.gameObject.AddComponent<Image>();
            image.sprite = name == "Objective" ? hudObjective : largePanel ? hudMap : hudStatus;
            image.type=Image.Type.Sliced;
            // Size the painted frame consistently while the blank paper interior stretches.
            image.pixelsPerUnitMultiplier=Mathf.Min(image.sprite.rect.width,image.sprite.rect.height)*.01f;
            image.raycastTarget=false;
            return rect;
        }
        private TMP_Text Text(Transform parent,string name,string value,float size,bool reading)
        {
            var text=StageUITheme.Text(name,parent,value,size,reading); text.color=ChartInk;
            text.enableAutoSizing=true; text.fontSizeMin=size-5; text.fontSizeMax=size;
            return text;
        }
        private Button Action(string name,Transform parent,string title,UnityEngine.Events.UnityAction callback,Vector2 pos)
        {
            var b=StageUITheme.Button(name,parent,title,callback);
            if (name == "Confirm" || name == "Retry" || name == "Lesson")
                b.GetComponent<Image>().pixelsPerUnitMultiplier = 6;
            Place((RectTransform)b.transform,new Vector2(.5f,0),pos,new Vector2(270,78),new Vector2(.5f,.5f));
            return b;
        }
        private static void Inset(RectTransform rt,float x,float y)
        { StageUITheme.Stretch(rt,Vector2.zero,Vector2.one);rt.offsetMin=new Vector2(x,y);rt.offsetMax=new Vector2(-x,-y); }
        private static void Place(RectTransform rt,Vector2 anchor,Vector2 pos,Vector2 size,Vector2 pivot)
        {rt.anchorMin=rt.anchorMax=anchor;rt.pivot=pivot;rt.sizeDelta=size;rt.anchoredPosition=pos;}
        private void OnDestroy() { if(routeFont!=null)Destroy(routeFont); }
    }
}
