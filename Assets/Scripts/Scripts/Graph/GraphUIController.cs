using System.Collections.Generic;
using System.Linq;
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
        private TMP_Text objective, route, feedback, clock, penalty, resultText, lessonBody, lessonTitle, lessonPage, lessonScrollHint, fieldNotes;
        private RectTransform lessonCard, lessonViewport;
        private ScrollRect lessonScroll;
        private Button previousPage, nextPage, beginHelp;
        private int helpPage;
        private struct HelpPage
        {
            public string title, body;
            public HelpPage(string title, string body) { this.title = title; this.body = body; }
        }
        private readonly List<HelpPage> helpPages = new List<HelpPage>();
        private float penaltyUntil;
        private RectTransform root, map;
        private GameObject result, lesson, choices;
        private Vector2 mapOrigin = new Vector2(0, 12);
        private float mapScale = 6.2f;
        private Button confirm, retry, lessonButton;
        private readonly List<Button> answerButtons = new List<Button>();
        private readonly Dictionary<string, Image> mapVertices = new Dictionary<string, Image>();
        private readonly Dictionary<GraphEdgeData, Image> mapEdges = new Dictionary<GraphEdgeData, Image>();
        private RectTransform playerMarker;
        private static readonly Color ChartInk = new Color32(41, 26, 15, 255);
        private static readonly Color MapNodeFill = new Color32(80, 51, 34, 255);
        private static readonly Color MapNodeText = new Color32(255, 242, 212, 255);
        private TMP_FontAsset routeFont;
        private RectTransform announcement;
        private TMP_Text announcementText;
        private float announcementUntil = -1;
        private bool startAnnouncement;
        private StageResultCard resultCard;
        private bool completionShown;
        private readonly List<Behaviour> resultCameraInputs = new List<Behaviour>();
        public GameObject ResultPanel => result;
        public bool AnnouncementVisible => announcement != null && announcement.gameObject.activeInHierarchy;
        public string AnnouncementText => announcementText != null ? announcementText.text : "";

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
            var mapPanel = Panel("Graph minimap", root, Vector2.one, new Vector2(-30, -24), new Vector2(240, 240), Vector2.one, true);
            ExpandableMinimap.Attach(mapPanel);
            var mapTitle = Text(mapPanel, "Map title", TrialCatalog.ForScene("Graph").Title, 18, false);
            Place(mapTitle.rectTransform, new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(190, 26), new Vector2(.5f, .5f));
            map = StageUITheme.Rect("Map", mapPanel);
            Inset(map, 36, 20); map.offsetMax = new Vector2(-36, -46);
            var timerPanel = Panel("Clock", root, Vector2.one, new Vector2(-30, -305), new Vector2(270, 100), Vector2.one);
            var timerTitle = Text(timerPanel, "Clock title", "TIME LEFT", 18, false);
            Place(timerTitle.rectTransform, new Vector2(.5f, 1), new Vector2(0, -23), new Vector2(220, 24), new Vector2(.5f, .5f));
            clock = Text(timerPanel, "Countdown", "10:00", 34, false); Inset(clock.rectTransform, 12, 12);
            clock.rectTransform.offsetMax = new Vector2(-12, -40); clock.color = StageUITheme.ReadingInk;
            clock.enableAutoSizing = false; clock.fontSize = 32;
            penalty = Text(root, "Timer deduction", "-10", 36, false);
            Place(penalty.rectTransform, Vector2.one, new Vector2(-310, -355), new Vector2(78, 60), new Vector2(1, .5f));
            penalty.color = new Color(1f, .4f, .22f); penalty.gameObject.SetActive(false);

            var routePanel = Panel("Recorded route", root, new Vector2(0, 1), new Vector2(30, -330), new Vector2(360, 60), new Vector2(0, 1));
            route = Text(routePanel, "Route", stage.Challenge.start, 23, true); route.font = routeFont; Inset(route.rectTransform, 20, 10);
            var feedbackPanel = Panel("Feedback", root, new Vector2(0, 1), new Vector2(30, -400), new Vector2(360, 100), new Vector2(0, 1));
            feedback = Text(feedbackPanel, "Feedback text", "", 22, true); Inset(feedback.rectTransform, 20, 10);
            confirm = Action("Confirm", root, "CONFIRM", () => { if (stage.TutorialActive) { if (stage.controlsTutorial.CurrentStep != GraphTutorialStep.Book) stage.controlsTutorial.PracticeConfirm(); } else if (stage.State == GraphStageState.ACTIVE) stage.Confirm(); }, new Vector2(0, 160));
            retry = Action("Retry", root, "RETRY ROUTE", () => { if (stage.TutorialActive) stage.controlsTutorial.PracticeRetry(); else if (stage.Challenge.type == GraphChallengeType.Degree) stage.ClearSelection(); else stage.RetryRoute(); }, new Vector2(0, 65));
            lessonButton = Action("Lesson", root, "HELP", () => stage.SetLesson(true), new Vector2(0, 70));
            Place((RectTransform)lessonButton.transform,new Vector2(0,1),new Vector2(210,-91),new Vector2(140,72),new Vector2(0,.5f));
            var guideLabel = lessonButton.GetComponentInChildren<TMP_Text>();
            guideLabel.fontSizeMin = 18; guideLabel.fontSizeMax = 24;
            guideLabel.rectTransform.offsetMin = new Vector2(9, 6); guideLabel.rectTransform.offsetMax = new Vector2(-9, -6);

            choices = StageUITheme.Rect("Degree choices", root).gameObject;
            Place((RectTransform)choices.transform, new Vector2(.5f, 0), new Vector2(0, 250), new Vector2(480, 128), new Vector2(.5f, 0));
            for (int i = 0; i <= 6; i++)
            {
                int number = i;
                var b = StageUITheme.Button("Answer " + i, choices.transform, i.ToString(), () => { if (stage.TutorialActive) stage.controlsTutorial.PracticeSelect(number); else stage.SelectAnswer(number); });
                Place((RectTransform)b.transform, new Vector2(0, 1), new Vector2(55 + (i%4)*120, -32-(i/4)*68), new Vector2(110, 64), new Vector2(.5f, .5f));
                b.GetComponent<Image>().type = Image.Type.Simple;
                b.GetComponent<Image>().preserveAspect = true;
                var answerLabel=b.GetComponentInChildren<TMP_Text>();
                answerLabel.fontSizeMin=18;answerLabel.fontSizeMax=22;
                answerLabel.rectTransform.offsetMin=new Vector2(6,2);answerLabel.rectTransform.offsetMax=new Vector2(-6,-2);
                answerButtons.Add(b);
            }
            resultCard = StageResultCard.Build(root, stage.ReturnToMenu);
            result = resultCard.Root.gameObject;
            resultText = resultCard.Summary;
            resultText.enableAutoSizing = true; resultText.fontSizeMin = 23; resultText.fontSizeMax = 31;
            resultCard.Extra.gameObject.SetActive(false);

            BuildHelp();
            BuildAnnouncement();
        }

        private void BuildHelp()
        {
            helpPages.Add(new HelpPage("Your graph journey", "Complete " + stage.ChallengeCount + " challenges selected at random from a bank of " + stage.catalog.challenges.Count + ". Graphs use letters or numbers for their vertices. Read the objective for your current graph: your run may include travel, degree, path, directed-path, or cycle tasks. Your selection stays the same through retries and Help."));
            helpPages.Add(new HelpPage("Start at the book", "Approach the Book Statue and tap the hand button. Each challenge begins at its starting vertex. Closing Help only restores your controls; it does not activate the book or start the timer. After each correct challenge, interact with the book again to start the next one."));
            helpPages.Add(new HelpPage("Move and read the map", "Move with the joystick or WASD, and use the jump button or keyboard jump control. Platforms are vertices; bridges are edges. The minimap shows the graph, arrows, and your position. Tap it to inspect the full graph in a centered panel; Close returns to the minimap. Cross a connecting bridge completely to record a move. Available bridges glow orange; crossed bridges turn blue. The ROUTE label lists your recorded vertices."));
            helpPages.Add(new HelpPage("Vertices and edges", "A vertex is a point in a graph. An edge joins two vertices. For a travel task, start at the vertex named in the objective, cross its connecting bridge, and reach the required destination. Stop on the destination platform and tap CONFIRM. Follow the visible bridges rather than jumping between unconnected platforms."));
            helpPages.Add(new HelpPage("Vertex degree", "The degree of a vertex counts the edges meeting it. Find the vertex named in the objective, inspect its incident edges, then select a number from the on-screen choices and tap CONFIRM. Movement alone does not answer a degree question. CLEAR SELECTION removes your selected number so you can choose again."));
            helpPages.Add(new HelpPage("Find a path", "Follow connecting edges from the starting vertex to the destination named in the objective. A simple path must not repeat a vertex. Your full recorded route is checked, so tap CONFIRM at the destination rather than backtracking to the book. If you need a fresh route, use RETRY ROUTE to return to the start."));
            helpPages.Add(new HelpPage("Directed paths", "Arrows show the permitted direction of a directed edge. Travel from the stated start to the destination using the arrows. Reverse travel is blocked, and a valid simple path cannot repeat a vertex. Read the map before choosing a bridge, then tap CONFIRM while standing on the destination platform."));
            helpPages.Add(new HelpPage("Complete a cycle", "A cycle returns to its starting vertex. Visit at least three distinct vertices along connecting edges, then return to the start without repeating any intermediate vertex. The starting vertex may appear again only to close the cycle. Tap CONFIRM after arriving back at the starting platform."));
            helpPages.Add(new HelpPage("Confirm and try again", "CONFIRM checks your selected degree or entire recorded route. Interacting with the book during an active challenge also checks your answer. Correct answers show green platform feedback and advance. Incorrect answers show red feedback with a reason, cost 10 seconds, and briefly stop movement. Your route or selection stays intact so you can review it."));
            helpPages.Add(new HelpPage("Retry safely", "If your route leaves a connecting bridge, is interrupted, or repeats a vertex, tap RETRY ROUTE. It returns you to the start and clears that route, preserving the remaining countdown and mistake count. Falling also returns you to the start. Retries and Help never reshuffle your challenges. For degree questions, choose a different number or use CLEAR SELECTION."));
            helpPages.Add(new HelpPage("Watch the countdown", "The stage starts with " + StageJourneyUI.FormatTime(stage.countdownSeconds) + " on one shared countdown. Time runs only after book activation, pauses between challenges and while Help is open, and carries over to the next challenge. Every incorrect confirmation subtracts 10 seconds. At zero, Game Over ends the run."));
            helpPages.Add(new HelpPage("Complete the stage", "A correct answer pauses the clock and prepares the next graph. Activate the book again when you are ready. Finish all " + stage.ChallengeCount + " selected challenges to complete the stage and see your time played, correct answers, and mistakes. PLAY NEXT STAGE opens Trees; GO TO MENU returns to Main Menu. If time runs out, PLAY AGAIN starts a new random run."));

            lesson = Modal("Graph How To Play Overlay");
            lessonCard = StageUITheme.Rect("TutorialCard", lesson.transform);
            lessonCard.gameObject.AddComponent<Image>();
            StageUITheme.Text("SectionLabel", lessonCard, "GRAPH / HOW TO PLAY", 28);
            lessonTitle = StageUITheme.Text("PageTitle", lessonCard, "", 44);
            lessonPage = StageUITheme.Text("PageIndicator", lessonCard, "", 28);
            // Use the same book artwork, fonts, and button treatment as PRELIM.
            previousPage = StageUITheme.Button("PreviousPage", lessonCard, "PREVIOUS", PreviousHelpPage);
            nextPage = StageUITheme.Button("NextPage", lessonCard, "NEXT", NextHelpPage);
            beginHelp = StageUITheme.Button("BeginButton", lessonCard, "LET'S BEGIN", stage.BeginFromHelp);
            StageUITheme.SkinTutorial(lesson);
            fieldNotes = lessonCard.Find("FieldNotesBody").GetComponent<TMP_Text>();
            fieldNotes.text = "CHECK YOUR OBJECTIVE\n\nLabels may be letters or numbers.\n\nUse bridges to record your route.\n\nCONFIRM checks your answer.\n\nHELP pauses play without changing your route or selection.";

            lessonViewport = StageUITheme.Rect("PageViewport", lessonCard);
            var hitArea = lessonViewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            lessonViewport.gameObject.AddComponent<RectMask2D>();
            lessonScroll = lessonViewport.gameObject.AddComponent<ScrollRect>();
            lessonScroll.viewport = lessonViewport;
            lessonScroll.horizontal = false;
            lessonScroll.movementType = ScrollRect.MovementType.Clamped;
            var content = StageUITheme.Rect("PageBody", lessonViewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.offsetMin = content.offsetMax = Vector2.zero;
            // The text is the scroll content itself so its preferred height grows naturally.
            lessonBody = content.gameObject.AddComponent<TextMeshProUGUI>();
            lessonBody.font = StageUITheme.ReadingFont;
            lessonBody.fontSize = 32;
            lessonBody.color = StageUITheme.ReadingInk;
            lessonBody.alignment = TextAlignmentOptions.TopLeft;
            lessonBody.textWrappingMode = TextWrappingModes.Normal;
            lessonBody.raycastTarget = false;
            lessonBody.margin = new Vector4(0, 0, 8, 8);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            lessonScroll.content = content;
            lessonScrollHint = StageUITheme.Text("ScrollHint", lessonCard, "", 20, true);
            lessonScrollHint.color = StageUITheme.ReadingInk;
            LayoutHelp();
            lesson.SetActive(false);
        }

        private void LayoutHelp()
        {
            bool portrait = host.rect.width < host.rect.height * 1.2f;
            StageUITheme.Stretch(lessonCard, portrait ? new Vector2(.025f,.035f) : new Vector2(.05f,.055f), portrait ? new Vector2(.975f,.965f) : new Vector2(.95f,.945f));
            StageUITheme.Stretch(lessonTitle.rectTransform, new Vector2(.08f,.67f), new Vector2(portrait ? .92f : .46f,.78f));
            StageUITheme.Stretch(lessonViewport, new Vector2(portrait ? .12f : .08f,.30f), new Vector2(portrait ? .88f : .46f,.65f));
            lessonBody.fontSize = portrait ? 28 : 32;
            StageUITheme.Stretch(lessonScrollHint.rectTransform, new Vector2(.08f,.255f), new Vector2(portrait ? .92f : .46f,.285f));
            lessonScrollHint.text = lessonBody.preferredHeight > lessonViewport.rect.height + 2 ? "Swipe text to read more" : "";
            var leftPaper = (RectTransform)lessonCard.Find("LeftReadingPage");
            StageUITheme.Stretch(leftPaper, new Vector2(.045f,.25f), new Vector2(portrait ? .955f : .49f,.85f));
            foreach (string name in new[] { "RightReadingPage", "BookSpine", "FieldNotesHeading", "FieldNotesBody" })
                lessonCard.Find(name).gameObject.SetActive(!portrait);
            StageUITheme.Stretch((RectTransform)previousPage.transform, new Vector2(portrait ? .07f : .09f,.10f), new Vector2(portrait ? .34f : .28f,.21f));
            StageUITheme.Stretch((RectTransform)nextPage.transform, new Vector2(portrait ? .36f : .30f,.10f), new Vector2(portrait ? .53f : .49f,.21f));
            StageUITheme.Stretch(lessonPage.rectTransform, new Vector2(portrait ? .535f : .50f,.10f), new Vector2(portrait ? .645f : .62f,.21f));
            StageUITheme.Stretch((RectTransform)beginHelp.transform, new Vector2(portrait ? .665f : .64f,.10f), new Vector2(portrait ? .93f : .91f,.21f));
            foreach (var button in new[] { previousPage, nextPage, beginHelp })
            {
                var label = button.GetComponentInChildren<TMP_Text>();
                label.fontSizeMin = portrait ? 18 : 22;
                label.fontSizeMax = portrait ? 24 : 30;
                label.rectTransform.offsetMin = new Vector2(portrait ? 8 : 28, 10);
                label.rectTransform.offsetMax = -label.rectTransform.offsetMin;
            }
        }

        public void PreviousHelpPage() { if (helpPage > 0) { helpPage--; ShowHelpPage(); } }
        public void NextHelpPage() { if (helpPage < helpPages.Count - 1) { helpPage++; ShowHelpPage(); } }
        private void ShowHelpPage()
        {
            lessonTitle.text = helpPages[helpPage].title;
            lessonBody.text = helpPages[helpPage].body;
            lessonPage.text = (helpPage + 1) + " / " + helpPages.Count;
            previousPage.interactable = helpPage > 0;
            nextPage.interactable = helpPage < helpPages.Count - 1;
            foreach (var button in new[] { previousPage, nextPage })
            {
                var group = button.GetComponent<CanvasGroup>();
                if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
                group.alpha = button.interactable ? 1 : .55f;
            }
            Canvas.ForceUpdateCanvases();
            lessonScroll.verticalNormalizedPosition = 1;
        }
        public void Refresh()
        {
            if (lesson.activeSelf) LayoutHelp();
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
            if (stage.TutorialActive && stage.controlsTutorial.ShowingGameplayPreview)
            { RefreshPractice(); return; }
            bool active = stage.State == GraphStageState.ACTIVE;
            objective.text = "<color=#291A0F><b>GRAPH  " + (stage.ChallengeIndex + 1) + "/" + stage.ChallengeCount + "  /  " + stage.Challenge.title.ToUpperInvariant() + "</b></color>\n" + stage.Challenge.objective;
            route.text = stage.Challenge.type == GraphChallengeType.Degree ? "SELECTED DEGREE: " + (stage.Answer.HasValue ? stage.Answer.Value.ToString() : "—") : "ROUTE   " + string.Join(" → ", stage.Tracker.Route);
            feedback.text = stage.Feedback;
            clock.text = StageJourneyUI.FormatTime(stage.timer.Remaining) + (stage.timer.Running ? "" : "  PAUSED");
            confirm.GetComponentInChildren<TMP_Text>().text = "CONFIRM";
            confirm.gameObject.SetActive(active);
            confirm.interactable = stage.CanAct && active;
            retry.interactable = stage.CanAct && active;
            retry.GetComponentInChildren<TMP_Text>().text = stage.Challenge.type == GraphChallengeType.Degree ? "CLEAR SELECTION" : "RETRY ROUTE";
            lessonButton.interactable = stage.CanAct && (active || stage.State == GraphStageState.WAITING_FOR_ACTIVATION);
            if (announcementUntil >= 0 && Time.unscaledTime >= announcementUntil) HideAnnouncement();
            // The brief centered Start message reserves the middle of the HUD.
            // Degree buttons reappear afterward without changing their selection or callbacks.
            choices.SetActive(stage.Challenge.type == GraphChallengeType.Degree && active && !AnnouncementVisible);
            if (AnnouncementVisible) LayoutAnnouncement();
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
        public void RebuildMap(GraphChallengeData preview = null)
        {
            var graph = preview ?? stage.Challenge;
            float minX=graph.vertices.Min(v=>v.position.x/GraphGeometry.LayoutScale);
            float maxX=graph.vertices.Max(v=>v.position.x/GraphGeometry.LayoutScale);
            float minZ=graph.vertices.Min(v=>v.position.z/GraphGeometry.LayoutScale);
            float maxZ=graph.vertices.Max(v=>v.position.z/GraphGeometry.LayoutScale);
            mapOrigin=new Vector2((minX+maxX)*.5f,(minZ+maxZ)*.5f);
            float width=map.rect.width>0?map.rect.width:198f;
            float height=map.rect.height>0?map.rect.height:204f;
            mapScale=Mathf.Min(6.2f,(width-36f)/Mathf.Max(1f,maxX-minX),(height-36f)/Mathf.Max(1f,maxZ-minZ));
            foreach (Transform child in map) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            mapVertices.Clear();
            mapEdges.Clear();
            Vector2 mapCenter = Vector2.zero;
            foreach (var vertex in graph.vertices) mapCenter += MapPoint(vertex.position);
            mapCenter /= Mathf.Max(1, graph.vertices.Count);
            foreach (var edge in graph.edges)
            {
                Vector2 a = MapPoint(graph.Vertex(edge.source).position), b = MapPoint(graph.Vertex(edge.destination).position);
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
            foreach (var vertex in graph.vertices)
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
        private Vector2 MapPoint(Vector3 p) => new Vector2((p.x / GraphGeometry.LayoutScale - mapOrigin.x) * mapScale,
            (p.z / GraphGeometry.LayoutScale - mapOrigin.y) * mapScale);
        public void ShowPenalty() { penaltyUntil = Time.unscaledTime + 2.5f; penalty.gameObject.SetActive(true); }
        public void ShowResult(bool success)
        {
            if (success && completionShown) return;
            if (success) completionShown = true;
            HideAnnouncement();
            lesson.SetActive(false); result.SetActive(true); result.transform.SetAsLastSibling();
            if (gameplayControls != null) gameplayControls.SetActive(false);
            resultCard.Stage.text = TrialCatalog.ForScene("Graph").Title;
            resultCard.Heading.text = success ? "STAGE COMPLETE" : "TIME'S UP";
            resultText.text = "Time Played: " + StageJourneyUI.FormatTime(stage.timer.ActiveSeconds) + "\nCorrect: " + stage.Correct + "\nMistakes: " + stage.Mistakes;
            resultCard.Primary.GetComponentInChildren<TMP_Text>().text = success ? "Play Next Stage" : "Play Again";
            resultCard.Menu.GetComponentInChildren<TMP_Text>().text = "Go to Menu";
            resultCard.Primary.onClick.RemoveAllListeners();
            if (success) resultCard.Primary.onClick.AddListener(stage.PlayNextStage);
            else resultCard.Primary.onClick.AddListener(stage.RestartStage);
            stage.timer.Pause();
            GameInput.Instance?.SetGameplayInputBlocked(true); stage.LocalPlayer?.ToggleControl(false);
            foreach (var camera in FindObjectsByType<CameraTargetController>(FindObjectsSortMode.None)) PauseResultCamera(camera);
            foreach (var camera in FindObjectsByType<Unity.Cinemachine.CinemachineInputAxisController>(FindObjectsSortMode.None)) PauseResultCamera(camera);
            foreach (var camera in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None)) PauseResultCamera(camera);
            foreach (var camera in FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None)) PauseResultCamera(camera);
            MobileLookInput.ResetDelta();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Time.timeScale = 0;
            if (success) WorldStageAudio.PlayStageCompletion();
        }
        private void PauseResultCamera(Behaviour input)
        { if (input.enabled) { resultCameraInputs.Add(input); input.enabled = false; } }
        public void PrepareResultNavigation()
        {
            resultCard.Primary.interactable = false; resultCard.Menu.interactable = false;
            foreach (var input in resultCameraInputs) if (input != null) input.enabled = true;
            resultCameraInputs.Clear(); MobileLookInput.ResetDelta();
        }
        public void ShowLesson(bool open)
        {
            if (lesson == null) return; // The canvas may be destroyed before the stage during scene exit.
            lesson.SetActive(open);
            if (open)
            {
                helpPage = 0;
                LayoutHelp();
                ShowHelpPage();
                root.SetAsLastSibling();
                lesson.transform.SetAsLastSibling();
            }
        }
        public void SetControlsTutorialVisible(bool visible)
        {
            if (root != null) root.gameObject.SetActive(!visible);
        }
        private void BuildAnnouncement()
        {
            announcement = StageUITheme.Rect("Graph Challenge Announcement", root);
            var background = announcement.gameObject.AddComponent<Image>();
            background.color = new Color(.025f,.11f,.16f,.92f); background.raycastTarget = false;
            announcementText = StageUITheme.Text("Announcement message", announcement, "", 44, true);
            announcementText.fontStyle = FontStyles.Bold;
            announcementText.alignment = TextAlignmentOptions.Center;
            announcementText.enableAutoSizing = true;
            announcementText.outlineColor = new Color32(0,0,0,255); announcementText.outlineWidth = .2f;
            Inset(announcementText.rectTransform,24,14);
            announcement.gameObject.SetActive(false);
        }
        public void ShowStartAnnouncement()
        {
            if (stage.TutorialActive || stage.State != GraphStageState.ACTIVE) return;
            ShowAnnouncement("Start!", true);
            announcementUntil = Time.unscaledTime + 1.5f;
        }
        public void ShowCompletionAnnouncement(int completed, int next)
        {
            if (stage.TutorialActive || stage.State != GraphStageState.CHALLENGE_COMPLETE || next > stage.ChallengeCount) return;
            ShowAnnouncement("Challenge " + completed + " completed! Interact with the book statue to start Challenge " + next + ".", false);
        }
        private void ShowAnnouncement(string message, bool compact)
        {
            HideAnnouncement();
            startAnnouncement = compact; announcementText.text = message;
            announcementText.color = compact ? new Color(1f,.9f,.55f) : Color.white;
            announcement.gameObject.SetActive(true); announcement.SetAsLastSibling();
            LayoutAnnouncement();
        }
        private void LayoutAnnouncement()
        {
            // Keep the banner between the left objective/route column and right map/clock column.
            float width = startAnnouncement ? Mathf.Min(360,host.rect.width - 40)
                : Mathf.Min(920,Mathf.Max(360,host.rect.width - 840));
            announcementText.fontSize = announcementText.fontSizeMax = startAnnouncement ? 76 : 44;
            announcementText.fontSizeMin = startAnnouncement ? 38 : 28;
            float height = announcementText.GetPreferredValues(announcementText.text,width-48,0).y + 44;
            Place(announcement,new Vector2(.5f,.5f),Vector2.zero,
                new Vector2(width,Mathf.Clamp(height,96,host.rect.height*.36f)),new Vector2(.5f,.5f));
        }
        public void HideAnnouncement()
        {
            announcementUntil = -1;
            if (announcement != null) announcement.gameObject.SetActive(false);
        }
        private readonly Dictionary<GameObject, bool> practiceVisibility = new Dictionary<GameObject, bool>();
        public RectTransform TutorialTarget(string name) => root.Find(name) as RectTransform;
        public void ShowPracticeElements(params string[] names)
        {
            root.gameObject.SetActive(true);
            foreach (Transform child in root)
            {
                if (!practiceVisibility.ContainsKey(child.gameObject)) practiceVisibility.Add(child.gameObject, child.gameObject.activeSelf);
                child.gameObject.SetActive(System.Array.IndexOf(names, child.name) >= 0);
            }
            RefreshPractice();
        }
        private void RefreshPractice()
        {
            var tutorial = stage.controlsTutorial;
            objective.text = "<b>PRACTICE / " + tutorial.PracticeTitle + "</b>\n" + tutorial.PracticeObjective;
            route.text = tutorial.PracticeGraph != null && tutorial.PracticeGraph.type == GraphChallengeType.Degree
                ? "PRACTICE DEGREE: " + (tutorial.PracticeAnswer.HasValue ? tutorial.PracticeAnswer.Value.ToString() : "—")
                : "PRACTICE ROUTE: " + string.Join(" → ", tutorial.PracticeRoute);
            feedback.text = tutorial.PracticeFeedback;
            clock.text = StageJourneyUI.FormatTime(stage.countdownSeconds) + "  PAUSED";
            confirm.GetComponentInChildren<TMP_Text>().text = "CONFIRM";
            confirm.gameObject.SetActive(tutorial.CurrentStep != GraphTutorialStep.Book && tutorial.CanPracticeConfirm);
            confirm.interactable = tutorial.CanPracticeConfirm;
            retry.GetComponentInChildren<TMP_Text>().text = tutorial.CurrentStep == GraphTutorialStep.Degree ? "CLEAR SELECTION" : "RETRY ROUTE";
            retry.interactable = tutorial.CurrentStep == GraphTutorialStep.Degree || tutorial.CurrentStep == GraphTutorialStep.Retry;
            lessonButton.interactable = false;
            for (int i = 0; i < answerButtons.Count; i++)
            {
                answerButtons[i].interactable = tutorial.CurrentStep == GraphTutorialStep.Degree;
                answerButtons[i].GetComponentInChildren<TMP_Text>().text = tutorial.PracticeAnswer == i ? "[" + i + "]" : i.ToString();
            }
            if (playerMarker != null) playerMarker.gameObject.SetActive(false);
            foreach (var vertex in mapVertices)
            {
                vertex.Value.color = MapNodeFill;
                vertex.Value.GetComponentInChildren<TMP_Text>().color = MapNodeText;
            }
            foreach (var edge in mapEdges)
            {
                bool walked = false;
                for (int i = 1; i < tutorial.PracticeRoute.Count; i++)
                    walked |= tutorial.PracticeRoute[i - 1] == edge.Key.source && tutorial.PracticeRoute[i] == edge.Key.destination;
                edge.Value.color = walked ? GraphEdge.WalkedColor : GraphEdge.AvailableColor;
            }
        }
        public void RestorePracticeElements()
        {
            foreach (var item in practiceVisibility) if (item.Key != null) item.Key.SetActive(item.Value);
            practiceVisibility.Clear();
            if (map != null) RebuildMap();
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
