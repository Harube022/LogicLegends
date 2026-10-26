using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>World and touch presentation for the shared optional activities. The session owns every answer.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveCoopActivityView : MonoBehaviour
    {
        public ArchiveTopicStation station;
        public Material stoneMaterial, brassMaterial, tealMaterial, amberMaterial, redMaterial;
        public TMP_Text sign, backSign;
        public RectTransform Panel => panel;
        public Transform WorldRoot => worldRoot;
        public ArchiveCoopActivity Activity
        {
            get
            {
                if (station == null) station = GetComponent<ArchiveTopicStation>();
                var s = station == null ? null : station.session;
                if (s == null || !s.Ready || station.topic == 0) return activity;
                int reset = s.State.stations[station.topic].testResetSerial;
                if (activity == null || sourceSeed != s.State.seed || sourceReset != reset)
                { sourceSeed = s.State.seed; sourceReset = reset; activity = ArchiveCoopActivity.Build(station.topic, sourceSeed, reset); rebuild = true; }
                return activity;
            }
        }

        sealed class FocusPoint { public int action, index; public Vector3 position; public string caption; }
        readonly List<FocusPoint> points = new List<FocusPoint>();
        readonly List<Transform> items = new List<Transform>(), sockets = new List<Transform>(), nodes = new List<Transform>();
        readonly List<GameObject> placed = new List<GameObject>(), links = new List<GameObject>();
        readonly Dictionary<int, Transform> held = new Dictionary<int, Transform>();
        readonly List<TMP_Text> worldLabels = new List<TMP_Text>();
        readonly List<TMP_Text> itemLabels = new List<TMP_Text>();
        readonly List<TMP_Text> socketLabels = new List<TMP_Text>();
        Mesh arrowMesh;
        ArchiveCoopActivity activity; int sourceSeed, sourceReset, observedRevision = -1, focusedKey = int.MinValue;
        bool rebuild;
        string observedWork="";
        Transform worldRoot;
        GameInput input;
        FocusPoint focus;
        Canvas controlsCanvas;
        RectTransform canvasRect, panel;
        TMP_Text caption, body, feedback, actionLabel, selection;
        UnityEngine.UI.Button use, connect, remove, submit, reset;
        UnityEngine.UI.ScrollRect reader;
        Vector2 lastScreen; Rect lastSafe; float lastScale;
        static readonly Color Ink = new Color(.16f, .11f, .07f), Gold = new Color(1, .94f, .78f);
        public Vector3 ItemPosition(int index)
        {
            int count = Mathf.Max(1, Activity == null ? 1 : Activity.items.Length);
            return new Vector3(-11.0f, .2f, Along(index, count));
        }
        public Vector3 SlotPosition(int index)
        {
            int count = Mathf.Max(1, Activity == null ? 1 : Activity.slots.Length);
            return new Vector3(-9.0f, .2f, Along(index, count));
        }
        public Vector3 NodePosition(int index)
        {
            var a = Activity;
            if (a == null || index < 0 || index >= a.nodes.Length) return new Vector3(-10, .2f, 0);
            if (a.mechanic == ArchiveBonusMechanic.GraphRoute && a.nodes.Length == 5)
                return new[] { new Vector3(-10,.2f,6),new Vector3(-11,.2f,2.5f),new Vector3(-9,.2f,2.5f),new Vector3(-9,.2f,-1.5f),new Vector3(-10,.2f,-6) }[index];
            if (a.positions == null || a.positions.Length != a.nodes.Length) return new Vector3(-10 + (index % 2 == 0 ? -1.05f : 1.05f), .2f, Along(index / 2, (a.nodes.Length + 1) / 2));
            float xmin = a.positions.Min(p => p.x), xmax = a.positions.Max(p => p.x), ymin = a.positions.Min(p => p.y), ymax = a.positions.Max(p => p.y);
            return new Vector3(-10 + (Mathf.Approximately(xmin, xmax) ? 0 : Mathf.Lerp(-1.1f, 1.1f, Mathf.InverseLerp(xmin, xmax, a.positions[index].x))),
                .2f, Mathf.Approximately(ymin, ymax) ? Along(index, a.nodes.Length) : Mathf.Lerp(6, -6, Mathf.InverseLerp(ymin, ymax, a.positions[index].y)));
        }
        public Vector3 SubmitPosition => Activity != null && Activity.mechanic == ArchiveBonusMechanic.GraphRoute ? NodePosition(Activity.goalNode) : new Vector3(-8.7f, .2f, 0);
        public Vector3 CurrentItemPosition(int index)
        {
            var c = station == null || station.session == null ? null : station.session.Relay(station.topic); var a = Activity;
            if (c == null || a == null || a.reusableItems || c.bonusValues == null) return ItemPosition(index);
            int slot = a.regionPlacement ? index >= 0 && index < c.bonusValues.Length ? c.bonusValues[index] : -1 : Array.IndexOf(c.bonusValues, index);
            return slot >= 0 && slot < a.slots.Length ? SlotPosition(slot) : ItemPosition(index);
        }
        static float Along(int index, int count) => count <= 1 ? 0 : Mathf.Lerp(6, -6, (float)index / (count - 1));
        static bool ReaderOpen => ArchiveTopicUI.Current != null && ArchiveTopicUI.Current.ReadingStation != null;
        // Keep ownership stable for the entire input event, including after a pick
        // changes its eligibility. The main Hand listener may run before or after us.
        public static bool HandlesHand(ArchiveTopicSession session)
        {
            if (session == null || ReaderOpen) return false;
            int topic = session.ActiveTopic;
            if (topic <= 0 || topic >= session.stations.Length) return false;
            var view = session.stations[topic].GetComponent<ArchiveCoopActivityView>();
            return view != null && view.isActiveAndEnabled && view.focus != null && view.panel != null && view.panel.gameObject.activeSelf;
        }
        void Start()
        {
            if (station == null) station = GetComponent<ArchiveTopicStation>();
            Subscribe(); BuildControls();
        }
        void Subscribe()
        {
            if (input != null || GameInput.Instance == null) return;
            input = GameInput.Instance; input.OnInteractAction += Interact;
        }
        void Interact(object sender, EventArgs e)
        {
            if (!isActiveAndEnabled || ReaderOpen || focus == null || panel == null || !panel.gameObject.activeSelf) return;
            Send(focus.action, focus.index);
        }
        void Send(int action, int a = -1, int b = -1)
        {
            var s = station.session;
            if (s.BonusCanUse(s.LocalActor, station.topic, action, a, b)) s.RequestBonus(station.topic, action, a, b);
        }
        void SendPair(int action)
        {
            var c = station.session.Relay(station.topic);
            if (c != null) Send(action, c.bonusFirst, c.bonusSecond);
        }
        void Update()
        {
            Subscribe();
            var s = station == null ? null : station.session;
            if (s == null || !s.Ready || station.topic == 0) { if (panel != null) panel.gameObject.SetActive(false); return; }
            var a = Activity; if (a == null) return;
            if (rebuild) { BuildWorld(); rebuild = false; observedRevision = -1; }
            var c = s.Relay(station.topic); if (c == null) return;
            bool available = s.RelayAvailable(station.topic);
            if (worldRoot != null) worldRoot.gameObject.SetActive(available || c.completed);
            var player = Player.LocalInstance;
            bool local = available && player != null && s.CanAct(s.LocalActor) && Time.timeScale > 0 && s.Near(s.LocalActor, station.topic, new Vector3(-10, 0, 0), 9);
            focus = null; float nearest = float.MaxValue;
            if (local && !ReaderOpen)
                foreach (var p in points)
                {
                    if (!s.BonusCanUse(s.LocalActor, station.topic, p.action, p.index)) continue;
                    Vector3 delta = station.transform.InverseTransformPoint(player.transform.position) - (p.action == 0 ? CurrentItemPosition(p.index) : p.position);
                    float distance = delta.x * delta.x + delta.z * delta.z;
                    if (distance < nearest) { nearest = distance; focus = p; }
                }
            if (panel != null)
            {
                if (lastScreen != new Vector2(Screen.width, Screen.height) || lastSafe != Screen.safeArea || !Mathf.Approximately(lastScale, controlsCanvas.scaleFactor)) PositionControls();
                panel.gameObject.SetActive(local && !ReaderOpen);
            }
            string signText = "T" + (station.topic + 1) + " · OPTIONAL\n" + a.title + "\n" +
                (c.completed ? "Team activity complete." : c.closed ? "Closed: main topic complete." : !available ? "Activate the main Book first." : "Support your main-topic teammate.\nCloses when this topic completes.");
            if (sign != null) sign.text = signText; if (backSign != null) backSign.text = signText;
            int key = focus == null ? -1 : focus.action * 100 + focus.index;
            string work=s.State.matchId+":"+c.serial+":"+c.assignmentRevision+":"+c.closed+":"+c.completed+":"+available+":"+c.recoveryPending;
            bool workChanged=work!=observedWork;
            if (workChanged || key != focusedKey)
            { observedWork=work; focusedKey = key; RefreshLabels(); }
            if(workChanged)RefreshWorld();
            bool nodeMechanic = a.mechanic == ArchiveBonusMechanic.MappingLinks || a.mechanic == ArchiveBonusMechanic.TreeRepair;
            if (use != null) { use.interactable = focus != null; var r=(RectTransform)use.transform;r.anchorMax=new Vector2(nodeMechanic ? .34f : .52f,.175f);actionLabel.text = focus == null ? a.mechanic == ArchiveBonusMechanic.GraphRoute ? "Walk the path" : "Approach a prop" : focus.caption; }
            if (connect != null)
            {
                connect.gameObject.SetActive(nodeMechanic); remove.gameObject.SetActive(nodeMechanic);
                connect.interactable = nodeMechanic && s.BonusCanUse(s.LocalActor, station.topic, 4, c.bonusFirst, c.bonusSecond);
                remove.interactable = nodeMechanic && s.BonusCanUse(s.LocalActor, station.topic, 5, c.bonusFirst, c.bonusSecond);
            }
            if (submit != null) submit.interactable = s.BonusCanUse(s.LocalActor, station.topic, 6);
            if (reset != null) reset.interactable = s.BonusCanUse(s.LocalActor, station.topic, 7);
            UpdateHeld();
        }
        void RefreshLabels()
        {
            if (caption == null) return;
            var s = station.session; var c = s.Relay(station.topic); var a = Activity;
            caption.text = "T" + (station.topic + 1) + " · OPTIONAL · " + a.title;
            var carry = s.BonusCarry(s.LocalActor, station.topic);
            string detail = carry != null && carry.item >= 0 && carry.item < a.items.Length ? "\n\nCARRIED PROP\n" + a.items[carry.item] :
                focus != null && (focus.action == 0 || focus.action == 2) && focus.index >= 0 && focus.index < a.items.Length ? "\n\nREAD THIS PROP\n" + a.items[focus.index] : "";
            string text = "SEPARATE BONUS EXAMPLE\n" + a.context + "\n\n" + a.instructions + detail +
                (string.IsNullOrWhiteSpace(c.explanation) ? "" : "\n\nANSWER FEEDBACK\n" + c.explanation);
            if (body.text != text) { body.text = text; reader.verticalNormalizedPosition = 1; }
            feedback.text = c.feedback.StartsWith("Optional ",StringComparison.Ordinal)?"Main solver + support. This separate activity closes when the main topic is complete.":c.feedback;
            feedback.color = c.completed ? new Color(.08f, .42f, .25f) : Ink;
            if (carry != null && carry.item >= 0 && carry.item < a.items.Length) {selection.text = "Carrying: " + Short(a.items[carry.item], carry.item);feedback.text="Read the carried prop above, then deliver it to a marked tray or socket.";}
            else if (a.nodes.Length > 0 && c.bonusFirst >= 0) selection.text = "Selected: " + a.nodes[c.bonusFirst] + (c.bonusSecond >= 0 ? " → " + a.nodes[c.bonusSecond] : " · choose another node");
            else if (a.mechanic == ArchiveBonusMechanic.InductionSequence) selection.text = "Activated stages: " + c.bonusRoute.Count + " / " + a.expected.Length;
            else if (a.mechanic == ArchiveBonusMechanic.GraphRoute) selection.text = "Walked route: " + string.Join(" → ", c.bonusRoute.Where(i => i >= 0 && i < a.nodes.Length).Select(i => a.nodes[i]));
            else selection.text = "Main solver + support · bonus never blocks the topic";
        }
        static string Short(string text, int index) => text != null && text.Length <= 26 ? text : "Card " + (char)('A' + index);
        void BuildWorld()
        {
            if (worldRoot != null) { worldRoot.gameObject.SetActive(false); Destroy(worldRoot.gameObject); }
            worldRoot = new GameObject("Optional_" + Activity.mechanic + "_Props").transform; worldRoot.SetParent(station.transform, false);
            points.Clear(); items.Clear(); sockets.Clear(); nodes.Clear(); links.Clear(); placed.Clear(); held.Clear(); worldLabels.Clear(); itemLabels.Clear(); socketLabels.Clear();
            var a = Activity;
            bool sequence = a.mechanic == ArchiveBonusMechanic.InductionSequence;
            for (int i = 0; i < a.items.Length; i++)
            {
                Vector3 p = ItemPosition(i);
                var item = Primitive(sequence ? PrimitiveType.Cylinder : a.mechanic == ArchiveBonusMechanic.ProofSort ? PrimitiveType.Capsule : PrimitiveType.Cube,
                    "Bonus_Item_" + i, p + Vector3.up * .30f, sequence ? new Vector3(.75f, .25f, .75f) : new Vector3(.65f, .18f, .55f), sequence ? brassMaterial : tealMaterial);
                itemLabels.Add(Label(item, Short(a.items[i], i), new Vector3(0, .45f, 0), new Vector2(1.7f, 1.0f))); items.Add(item);
                points.Add(new FocusPoint { action = sequence ? 2 : 0, index = i, position = p, caption = sequence ? "Activate " + Short(a.items[i], i) : "Take " + Short(a.items[i], i) });
            }
            for (int i = 0; i < a.slots.Length; i++)
            {
                var p = SlotPosition(i); var tray = Primitive(PrimitiveType.Cube, "Bonus_Tray_" + i, p, new Vector3(.85f, .08f, 1.15f), brassMaterial);
                socketLabels.Add(Label(tray, a.slots[i], new Vector3(0, .45f, 0), new Vector2(1.8f, 1.0f))); sockets.Add(tray);
                points.Add(new FocusPoint { action = 1, index = i, position = p, caption = "Place in " + a.slots[i] });
            }
            for (int i = 0; i < a.nodes.Length; i++)
            {
                Vector3 p = NodePosition(i); var node = Primitive(PrimitiveType.Cylinder, "Bonus_Node_" + i, p, new Vector3(.7f, .08f, .7f), tealMaterial);
                Label(node, a.nodes[i], new Vector3(0, .65f, 0), new Vector2(1.4f, .8f)); nodes.Add(node);
                if (a.mechanic != ArchiveBonusMechanic.GraphRoute) points.Add(new FocusPoint { action = 3, index = i, position = p, caption = "Select " + a.nodes[i] });
                else if (i == a.startNode) points.Add(new FocusPoint { action = 2, index = i, position = p, caption = "Begin walked route" });
            }
            var lectern = Primitive(PrimitiveType.Cube, "Bonus_Submit_Lectern", SubmitPosition + Vector3.up * .35f, new Vector3(.55f, .65f, .55f), stoneMaterial);
            Label(lectern, a.mechanic == ArchiveBonusMechanic.GraphRoute ? "FINISH\nCHECK ROUTE" : "CHECK\nBONUS", new Vector3(0, .65f, 0), new Vector2(1.4f, .9f));
            points.Add(new FocusPoint { action = 6, index = -1, position = SubmitPosition, caption = "Check bonus" });
        }
        Transform Primitive(PrimitiveType type, string name, Vector3 p, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(worldRoot, false); go.transform.localPosition = p; go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>(); if (collider != null) Destroy(collider);
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            return go.transform;
        }
        TMP_Text Label(Transform parent, string text, Vector3 offset, Vector2 size)
        {
            var go = new GameObject("Bonus_Label", typeof(TextMeshPro)); go.transform.SetParent(worldRoot, false); go.transform.localPosition = parent.localPosition + offset;
            var label = go.GetComponent<TMP_Text>(); label.font = station.font; label.text = text; label.fontSize = 3; label.enableAutoSizing = true; label.fontSizeMin = 1.5f; label.fontSizeMax = 3;
            label.rectTransform.sizeDelta = size; label.alignment = TextAlignmentOptions.Center; label.color = Gold; label.richText = false;
            label.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            worldLabels.Add(label);
            return label;
        }
        void RefreshWorld()
        {
            worldLabels.RemoveAll(label=>label==null);
            var c = station.session.Relay(station.topic); var a = Activity;
            foreach (var p in placed) Destroy(p); placed.Clear();
            foreach (var link in links) Destroy(link); links.Clear();
            worldLabels.RemoveAll(label => label == null);
            for (int i = 0; i < items.Count; i++) { items[i].localPosition = ItemPosition(i) + Vector3.up * .30f; items[i].gameObject.SetActive(a.reusableItems || c.bonusCarries == null || !c.bonusCarries.Any(v => v.item == i)); }
            if (a.mechanic == ArchiveBonusMechanic.InductionSequence)
                for (int i = 0; i < items.Count; i++) items[i].GetComponent<Renderer>().sharedMaterial = c.bonusRoute.Contains(i) ? amberMaterial : brassMaterial;
            if (c.bonusValues != null)
                for (int i = 0; i < c.bonusValues.Length; i++)
                {
                    int value = c.bonusValues[i]; int item = a.regionPlacement ? i : value, slot = a.regionPlacement ? value : i;
                    if (value < 0 || item < 0 || item >= a.items.Length || slot < 0 || slot >= a.slots.Length) continue;
                    if (!a.reusableItems && item < items.Count) items[item].gameObject.SetActive(false);
                    var token = Primitive(PrimitiveType.Cube, "Placed_Bonus_" + i, SlotPosition(slot) + Vector3.up * (.15f + (a.regionPlacement ? i * .14f : 0)), new Vector3(.48f, .1f, .45f), a.mechanic == ArchiveBonusMechanic.TruthTiles && item == 1 ? amberMaterial : tealMaterial);
                    placed.Add(token.gameObject);
                    if(!a.regionPlacement){var tokenLabel = Label(token, Short(a.items[item], item), new Vector3(0, .35f, 0), new Vector2(1.6f, .65f)); placed.Add(tokenLabel.gameObject);}
                }
            if(a.regionPlacement&&c.bonusValues!=null)for(int region=0;region<socketLabels.Count;region++)socketLabels[region].text=a.slots[region]+"\n"+string.Join(", ",Enumerable.Range(0,Mathf.Min(c.bonusValues.Length,a.items.Length)).Where(i=>c.bonusValues[i]==region).Select(i=>Short(a.items[i],i)));
            for(int i=0;i<items.Count;i++)if(i<itemLabels.Count)itemLabels[i].gameObject.SetActive(items[i].gameObject.activeSelf);
            IEnumerable<int> visibleLinks = a.mechanic == ArchiveBonusMechanic.GraphRoute ? a.initialLinks : c.bonusLinks;
            foreach (int edge in visibleLinks)
            {
                int first = edge / 100, second = edge % 100;
                if (first < 0 || second < 0 || first >= a.nodes.Length || second >= a.nodes.Length) continue;
                Vector3 from = NodePosition(first) + Vector3.up * .08f, to = NodePosition(second) + Vector3.up * .08f, delta = to - from;
                var line = Primitive(PrimitiveType.Cube, "Bonus_Link_" + edge, (from + to) * .5f, new Vector3(.06f, .045f, delta.magnitude), brassMaterial);
                line.localRotation = Quaternion.LookRotation(delta); links.Add(line.gameObject);
                var arrow = new GameObject("Bonus_Arrow_" + edge, typeof(MeshFilter), typeof(MeshRenderer)); arrow.transform.SetParent(worldRoot, false);
                arrow.transform.localPosition = Vector3.Lerp(from, to, .65f) + Vector3.up * .025f; arrow.transform.localRotation = Quaternion.LookRotation(delta);
                if (arrowMesh == null) { arrowMesh = new Mesh { name = "Optional_Link_Arrow" }; arrowMesh.vertices = new[] { new Vector3(-.18f, 0, -.18f), new Vector3(0, 0, .28f), new Vector3(.18f, 0, -.18f) }; arrowMesh.triangles = new[] { 0, 1, 2 }; arrowMesh.RecalculateNormals(); arrowMesh.RecalculateBounds(); }
                arrow.GetComponent<MeshFilter>().sharedMesh = arrowMesh; var arrowRenderer = arrow.GetComponent<MeshRenderer>(); arrowRenderer.sharedMaterial = amberMaterial; arrowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; links.Add(arrow);
            }
            for (int i = 0; i < nodes.Count; i++) nodes[i].GetComponent<Renderer>().sharedMaterial = c.bonusFirst == i || c.bonusSecond == i || c.bonusRoute.Contains(i) ? amberMaterial : tealMaterial;
        }
        void UpdateHeld()
        {
            var c = station.session.Relay(station.topic); if (c == null || c.bonusCarries == null || worldRoot == null) return;
            foreach (var actor in held.Keys.Where(actor => !c.bonusCarries.Any(v => v.actor == actor)).ToArray()) { Destroy(held[actor].gameObject); held.Remove(actor); }
            foreach (var carry in c.bonusCarries)
            {
                var player = station.session.ActorPlayer(carry.actor); if (player == null) continue;
                if (!held.TryGetValue(carry.actor, out var token)) { token = Primitive(PrimitiveType.Cube, "Bonus_Carried_By_" + carry.actor, Vector3.zero, new Vector3(.5f, .12f, .5f), tealMaterial); held.Add(carry.actor, token); }
                token.gameObject.SetActive(true); token.position = player.transform.position + player.transform.forward * 1.1f + Vector3.up * 2.5f;
            }
        }
        void LateUpdate()
        {
            if (worldRoot == null || !worldRoot.gameObject.activeInHierarchy || Camera.main == null) return;
            foreach (var label in worldLabels) if (label != null) label.transform.rotation = Camera.main.transform.rotation;
        }
        void BuildControls()
        {
            var go = new GameObject("T" + (station.topic + 1) + "_Bonus_Controls", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene); controlsCanvas = go.GetComponent<Canvas>(); controlsCanvas.renderMode = RenderMode.ScreenSpaceOverlay; controlsCanvas.sortingOrder = 135;
            canvasRect = go.GetComponent<RectTransform>(); var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            panel = Rect("Bonus_Activity_Panel", go.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f)); panel.sizeDelta = new Vector2(660, 420);
            var face = panel.gameObject.AddComponent<UnityEngine.UI.Image>(); face.sprite = ArchiveTopicUI.Current!=null?ArchiveTopicUI.Current.statusArt:StageUITheme.Paper; face.type = UnityEngine.UI.Image.Type.Sliced; face.pixelsPerUnitMultiplier = 5; face.raycastTarget = false;
            caption = Text("Bonus_Title", panel, new Vector2(.045f, .90f), new Vector2(.955f, .98f), 23); caption.alignment = TextAlignmentOptions.Center; caption.enableAutoSizing = true; caption.fontSizeMin=18;caption.fontSizeMax=23;
            var host = Rect("Bonus_Reading", panel, new Vector2(.055f, .46f), new Vector2(.945f, .89f)); reader = host.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); reader.horizontal = false; reader.vertical = true; reader.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; reader.scrollSensitivity = 30;
            var viewport = Rect("Viewport", host, Vector2.zero, Vector2.one); viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.1f, .3f, .2f, .03f); viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport, new Vector2(0, 1), Vector2.one); content.pivot = new Vector2(.5f, 1); content.offsetMin = content.offsetMax = Vector2.zero;
            body = content.gameObject.AddComponent<TextMeshProUGUI>(); body.font = StageUITheme.ReadingFont; body.fontSize = 21; body.color = Ink; body.richText = false; body.raycastTarget = false; body.overflowMode = TextOverflowModes.Overflow; body.margin = new Vector4(5, 2, 5, 10);
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize; reader.viewport = viewport; reader.content = content;
            selection = Text("Bonus_Shared_Work", panel, new Vector2(.055f, .36f), new Vector2(.945f, .45f), 19); selection.alignment = TextAlignmentOptions.Center;
            feedback = Text("Bonus_Feedback", panel, new Vector2(.055f, .18f), new Vector2(.945f, .35f), 19); feedback.enableAutoSizing = true; feedback.fontSizeMin = 16; feedback.fontSizeMax = 19;
            use = Button("Use", new Vector2(.035f, .025f), new Vector2(.34f, .175f), () => { if (focus != null) Send(focus.action, focus.index); }); actionLabel = use.GetComponentInChildren<TMP_Text>();
            connect = Button("Connect", new Vector2(.35f, .105f), new Vector2(.52f, .175f), () => SendPair(4));
            remove = Button("Remove", new Vector2(.35f, .025f), new Vector2(.52f, .095f), () => SendPair(5));
            reset = Button("Retry", new Vector2(.53f, .025f), new Vector2(.69f, .175f), () => Send(7));
            submit = Button("Check", new Vector2(.70f, .025f), new Vector2(.965f, .175f), () => Send(6));
            ArchiveReadingScrollbars.Install(go.transform); PositionControls(); panel.gameObject.SetActive(false);
        }
        UnityEngine.UI.Button Button(string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction onClick)
        {
            var r = Rect(label, panel, min, max); var face = r.gameObject.AddComponent<UnityEngine.UI.Image>(); face.sprite = StageUITheme.ButtonArt; face.type = UnityEngine.UI.Image.Type.Sliced; face.pixelsPerUnitMultiplier = 5;
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = face; b.onClick.AddListener(onClick); b.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            var t = Text("Label", r, new Vector2(.05f, .08f), new Vector2(.95f, .92f), 21); t.text = label; t.color = Gold; t.alignment = TextAlignmentOptions.Center; t.enableAutoSizing = true; t.fontSizeMin = 15; t.fontSizeMax = 21;
            return b;
        }
        TMP_Text Text(string name, Transform parent, Vector2 min, Vector2 max, float size)
        {
            var r = Rect(name, parent, min, max); var text = r.gameObject.AddComponent<TextMeshProUGUI>(); text.font = StageUITheme.ReadingFont; text.fontSize = size; text.color = Ink; text.richText = false; text.raycastTarget = false; text.overflowMode = TextOverflowModes.Ellipsis; return text;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false); r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; return r;
        }
        void PositionControls()
        {
            Canvas.ForceUpdateCanvases(); lastScreen = new Vector2(Screen.width, Screen.height); lastSafe = Screen.safeArea; lastScale = Mathf.Max(.01f, controlsCanvas.scaleFactor);
            panel.sizeDelta = new Vector2(Mathf.Min(660, Mathf.Max(280, lastSafe.width / lastScale - 24)), Mathf.Min(420, Mathf.Max(260, lastSafe.height / lastScale - 120)));
            // Leave the character and forward route visible, above the movement pad.
            var p = new Vector2(lastSafe.xMin + lastSafe.width * .27f, lastSafe.yMin + lastSafe.height * .61f);
            p.x = Mathf.Clamp(p.x, lastSafe.xMin + panel.rect.width * lastScale * .5f + 12, lastSafe.xMax - panel.rect.width * lastScale * .5f - 12);
            p.y = Mathf.Clamp(p.y, lastSafe.yMin + panel.rect.height * lastScale * .5f + 12, lastSafe.yMax - panel.rect.height * lastScale * .5f - 12);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, p, null, out var local)) panel.anchoredPosition = local;
        }
        void OnDisable() { focus = null; if (panel != null) panel.gameObject.SetActive(false); }
        void OnDestroy() { if (input != null) input.OnInteractAction -= Interact; if (controlsCanvas != null) Destroy(controlsCanvas.gameObject); if (arrowMesh != null) Destroy(arrowMesh); }
    }
}
