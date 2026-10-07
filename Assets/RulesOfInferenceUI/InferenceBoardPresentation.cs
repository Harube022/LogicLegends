using System.Linq;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LogicLegends.Inference;

namespace LogicLegends.RulesOfInferenceUI
{
    // Presentation belongs to RulesOfInference only. The existing board owns all game/input logic.
    [DefaultExecutionOrder(900)]
    public sealed class InferenceBoardPresentation : MonoBehaviour
    {
        public InferenceChallenge challenge;
        public TMP_FontAsset bodyFont, displayFont;
        public Sprite actionBackground;
        public GameObject[] additionalHiddenHud = new GameObject[0];
        static readonly Color Board = new Color(.035f, .085f, .11f, 1);
        static readonly Color Workspace = new Color(.08f, .14f, .16f, 1);
        static readonly Color Gold = new Color(.89f, .76f, .46f, 1);
        static readonly Color Pale = new Color(.91f, .95f, .93f, 1);
        static readonly Color Muted = new Color(.60f, .72f, .74f, 1);
        static readonly Color Tile = new Color(.16f, .31f, .33f, 1);
        RectTransform content, actions, viewport, bankViewport;
        TMP_Text bankStatus;
        TMP_Text[] steps;
        RuleOfInferencePuzzle styledPuzzle;
        bool runtimeOverlay, presentationReady;
        sealed class HiddenHud { public CanvasGroup group; public float alpha; public bool interactable, blocksRaycasts; }
        readonly List<HiddenHud> hiddenHud = new List<HiddenHud>();
        int lastStep = -1;
        string lastFeedback;
        TMP_Text[] timers;
        sealed class TimerOverlay
        {
            public TMP_Text text; public Canvas canvas; public Vector3 position;
            public bool raycast, created, sorting; public int order;
        }
        readonly List<TimerOverlay> raisedTimers=new List<TimerOverlay>();

        void Awake()
        {
            if (!Application.isPlaying) return;
            ApplyDesign();
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            transform.localScale = Vector3.one;
            GetComponent<Image>().enabled = false;
            runtimeOverlay = true;
            challenge.validateButton.onClick.AddListener(ShowBlankFeedback);
            FitToScreen();
        }

        void OnEnable()
        {
            if (!Application.isPlaying || challenge==null) return;
            presentationReady=false;
            challenge.boardInput.alpha=0;
            challenge.boardInput.interactable=false;
            challenge.boardInput.blocksRaycasts=false;
            HideHud(challenge.gameplayHud);
            foreach(var hud in additionalHiddenHud) HideHud(hud);
            StartCoroutine(RevealAfterCameraBlend());
        }

        void HideHud(GameObject hud)
        {
            if(hud==null) return;
            var group=hud.GetComponent<CanvasGroup>();
            if(group==null) group=hud.AddComponent<CanvasGroup>();
            if(hiddenHud.Any(item=>item.group==group)) return;
            hiddenHud.Add(new HiddenHud { group=group, alpha=group.alpha, interactable=group.interactable, blocksRaycasts=group.blocksRaycasts });
            group.alpha=0; group.interactable=false; group.blocksRaycasts=false;
        }

        IEnumerator RevealAfterCameraBlend()
        {
            // OpenBoard activates this object before selecting its virtual camera.
            // Wait for the Brain's next update, then inspect the actual blend state.
            yield return null;
            var camera=Camera.main;
            var brain=camera==null ? null : camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            while(challenge.IsBoardOpen && brain!=null &&
                (brain.IsBlending || brain.ActiveVirtualCamera==null ||
                 !object.ReferenceEquals(brain.ActiveVirtualCamera, challenge.focusCamera.GetComponent<Unity.Cinemachine.CinemachineCamera>())))
                yield return null;
            if(!challenge.IsBoardOpen) yield break;
            presentationReady=true;
            challenge.boardInput.alpha=1;
            challenge.boardInput.interactable=true;
            challenge.boardInput.blocksRaycasts=true;
        }

        public void ApplyDesign()
        {
            if (challenge == null || bodyFont == null || displayFont == null) return;
            var b = challenge.dragPuzzle;
            challenge.font = bodyFont; b.font = bodyFont;
            content = (RectTransform)transform.Find("BoardContent");
            DisableLayout(content);
            Box(content, 0, 0, 1536, 810);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
            content.anchoredPosition = Vector2.zero;
            Frame(content, Board, Gold);
            var rootImage = GetComponent<Image>();
            rootImage.color = Board;
            challenge.heading.font = displayFont;
            challenge.heading.fontSize = 48;
            challenge.heading.color = Gold;
            Box(challenge.heading.rectTransform, 28, 20, 1480, 52);
            challenge.heading.raycastTarget = false;
            var intro = content.GetComponentsInChildren<TMP_Text>(true)
                .First(t => t.transform.parent == content && t != challenge.heading && t != challenge.feedback);
            Text(intro, bodyFont, 30, Pale);
            intro.text = "Complete the premises. Find a rule diamond. Reveal the conclusion.";
            Box(intro.rectTransform, 28, 78, 1480, 36);
            var strip = Rect(content, "Theme_Progress");
            Box(strip, 28, 124, 1480, 38);
            steps = new TMP_Text[3];
            string[] titles = { "1  COMPLETE PREMISES", "2  FIND A DIAMOND", "3  REVEAL CONCLUSION" };
            for (int i = 0; i < 3; i++)
            {
                var step = Rect(strip, "Step" + i);
                Box(step, i * 498, 0, 484, 38);
                Frame(step, Workspace, new Color(.20f,.32f,.34f));
                steps[i] = Label(step, "Caption", titles[i], 24, Gold);
                Stretch(steps[i].rectTransform, 5);
                steps[i].alignment = TextAlignmentOptions.Center;
            }
            var work = (RectTransform)content.Find("PuzzleWorkspace");
            DisableLayout(work); Box(work, 24, 170, 1488, 410);
            var argument = (RectTransform)work.Find("ArgumentWorkspace");
            DisableLayout(argument); Box(argument, 0, 0, 1070, 410);
            Frame(argument, Workspace, new Color(.26f,.36f,.35f));
            var caption = Label(argument, "Theme_PremisesTitle", "PREMISES", 28, Gold);
            caption.font = displayFont; Box(caption.rectTransform, 24, 18, 1000, 40);
            challenge.legend.gameObject.SetActive(false);
            viewport = ScrollArea(argument, b.premises, "Theme_PremisesScroll", 24, 64, 1022, 236);
            var vlayout = b.premises.GetComponent<VerticalLayoutGroup>();
            vlayout.padding = new RectOffset(2, 2, 3, 3);
            vlayout.spacing = 6; vlayout.childForceExpandHeight = false;
            var conclusion = (RectTransform)argument.Find("Conclusion");
            DisableLayout(conclusion); Box(conclusion, 24, 314, 1022, 80);
            Frame(conclusion, Board, new Color(.34f,.37f,.28f));
            var badge = conclusion.GetComponentsInChildren<TMP_Text>(true).First(t => t != challenge.conclusionLabel);
            Text(badge, displayFont, 42, Muted); Box(badge.rectTransform, 16, 8, 66, 64);
            badge.alignment = TextAlignmentOptions.Center;
            Text(challenge.conclusionLabel, bodyFont, 30, Pale);
            Box(challenge.conclusionLabel.rectTransform, 106, 6, 892, 68);
            challenge.conclusionLabel.enableAutoSizing = true;
            challenge.conclusionLabel.fontSizeMin = 24; challenge.conclusionLabel.fontSizeMax = 30;
            challenge.conclusionLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var bank = (RectTransform)work.Find("WordBankWorkspace");
            DisableLayout(bank); Box(bank, 1094, 0, 394, 410);
            Frame(bank, Workspace, new Color(.26f,.36f,.35f));
            Text(challenge.wordBank, displayFont, 28, Gold);
            Box(challenge.wordBank.rectTransform, 22, 18, 350, 40);
            var hint = Label(bank, "Theme_BankHint", "Drag a word, or tap then tap a blank.", 22, Muted);
            Box(hint.rectTransform, 22, 58, 350, 56);
            bankViewport = ScrollArea(bank, b.wordBank, "Theme_WordsScroll", 22, 124, 350, 262);
            var grid = b.wordBank.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
            grid.cellSize = new Vector2(160, 100); grid.spacing = new Vector2(20,12);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.padding = new RectOffset(5,5,4,4);
            bankStatus = Label(bank, "Theme_BankStatus", "", 28, Pale);
            Box(bankStatus.rectTransform, 28, 122, 338, 266);
            bankStatus.gameObject.SetActive(false);
            b.ruleChoices.gameObject.SetActive(false);
            var oldRuleTitle = bank.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.text == "SELECT A RULE");
            if (oldRuleTitle != null) oldRuleTitle.gameObject.SetActive(false);
            Text(challenge.feedback, bodyFont, 29, Pale);
            Box(challenge.feedback.rectTransform, 46, 598, 1440, 62);
            challenge.feedback.enableAutoSizing = true; challenge.feedback.fontSizeMin = 24;
            challenge.feedback.fontSizeMax = 29;
            var feedbackPanel = Rect(content, "Theme_FeedbackFrame");
            Box(feedbackPanel, 24, 586, 1488, 82);
            Frame(feedbackPanel, Board, new Color(.25f,.36f,.36f));
            feedbackPanel.SetSiblingIndex(0);
            actions = (RectTransform)content.Find("Actions");
            DisableLayout(actions); Box(actions, 24, 678, 1488, 118);
            foreach (var button in Buttons()) StyleButton(button);
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.raycastTarget = false;
            RefreshStates(); StylePuzzle(); ArrangeActions();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        void LateUpdate()
        {
            if (challenge == null || !Application.isPlaying) return;
            var group=challenge.boardInput;
            if (!presentationReady) { group.alpha=0; group.interactable=false; group.blocksRaycasts=false; }
            FitToScreen();
            if (presentationReady) RaiseActiveTimers();
            RefreshStates();
            if (styledPuzzle != challenge.dragPuzzle.Puzzle) { StylePuzzle(); styledPuzzle = challenge.dragPuzzle.Puzzle; }
            ArrangeActions();
        }

        void RaiseActiveTimers()
        {
            if(timers==null) timers=Resources.FindObjectsOfTypeAll<TMP_Text>().Where(t=>t.gameObject.scene==gameObject.scene && t.name=="TimerText" && !t.transform.IsChildOf(transform)).ToArray();
            foreach(var timer in timers)
            {
                if(timer==null || !timer.gameObject.activeInHierarchy) continue;
                var item=raisedTimers.FirstOrDefault(t=>t.text==timer);
                if(item==null)
                {
                    var existing=timer.GetComponent<Canvas>();
                    item=new TimerOverlay { text=timer, position=timer.transform.position, raycast=timer.raycastTarget, created=existing==null,
                        canvas=existing != null ? existing : timer.gameObject.AddComponent<Canvas>() };
                    item.order=item.canvas.sortingOrder; item.sorting=item.canvas.overrideSorting;
                    raisedTimers.Add(item);
                }
                item.canvas.overrideSorting=true; item.canvas.sortingOrder=31;
                timer.raycastTarget=false;
                timer.transform.position=content.TransformPoint(new Vector3(620,361,0));
            }
        }
        void OnDisable()
        {
            StopAllCoroutines(); presentationReady=false;
            foreach(var hud in hiddenHud)
            {
                if(hud.group==null) continue;
                hud.group.alpha=hud.alpha; hud.group.interactable=hud.interactable; hud.group.blocksRaycasts=hud.blocksRaycasts;
            }
            hiddenHud.Clear();
            foreach(var item in raisedTimers)
            {
                if(item.text!=null) {item.text.transform.position=item.position;item.text.raycastTarget=item.raycast;}
                if(item.canvas!=null)
                {
                    if(item.created) Destroy(item.canvas);
                    else {item.canvas.sortingOrder=item.order;item.canvas.overrideSorting=item.sorting;}
                }
            }
            raisedTimers.Clear();
        }
        void FitToScreen()
        {
            if (!runtimeOverlay || content == null) return;
            var safe = Screen.safeArea;
            safe = UnityEngine.Rect.MinMaxRect(Mathf.Clamp(safe.xMin,0,Screen.width), Mathf.Clamp(safe.yMin,0,Screen.height), Mathf.Clamp(safe.xMax,0,Screen.width), Mathf.Clamp(safe.yMax,0,Screen.height));
            if (safe.width < 1 || safe.height < 1) safe = new Rect(0, 0, Screen.width, Screen.height);
            // The stone-facing board fills the viewing area; gameplay HUD is hidden.
            float scale = Mathf.Min(safe.width * .96f / 1536f, safe.height * .94f / 810f);
            content.localScale = Vector3.one * scale;
            content.position = new Vector3(safe.x + safe.width * .5f, safe.y + safe.height * .5f, 0);
        }

        void StylePuzzle()
        {
            var b = challenge.dragPuzzle;
            foreach (var home in b.wordBank.GetComponentsInChildren<RectTransform>(true))
                if (home == b.wordBank || home.name == "WordHome") RemoveTileDecorations(home);
            foreach (Transform row in b.premises)
            {
                if (!row.gameObject.activeSelf) continue;
                var element = row.GetComponent<LayoutElement>();
                if (element != null) element.minHeight = element.preferredHeight = row.name == "PremiseLine" ? 100 : 4;
                foreach (var size in row.GetComponentsInChildren<LayoutElement>(true))
                    if (size.transform != row && size.transform.parent == row) size.minHeight = size.preferredHeight = 100;
            }
            var zones = Application.isPlaying && b.Puzzle != null ? b.Zones : b.premises.GetComponentsInChildren<DropZone>(true);
            foreach (var zone in zones)
            {
                Frame((RectTransform)zone.transform, new Color(.12f,.20f,.23f), Muted, false);
                var label = zone.GetComponentsInChildren<TMP_Text>(true).First(t => t.transform.parent == zone.transform);
                Text(label, bodyFont, 30, Muted); label.text = "...";
                var visual = zone.GetComponent<InferenceTilePresentation>() ?? zone.gameObject.AddComponent<InferenceTilePresentation>();
                visual.zone = zone;
            }
            // Use the builder list: the bank can still be hidden after the previous round.
            var words = Application.isPlaying && b.Puzzle != null ? b.Words : GetComponentsInChildren<DraggableWord>(true);
            foreach (var word in words)
            {
                var r = (RectTransform)word.transform; r.sizeDelta = new Vector2(110,100);
                Frame(r, Tile, Gold, false);
                var text = word.GetComponentInChildren<TMP_Text>(true);
                Text(text, bodyFont, 30, Pale); text.alignment = TextAlignmentOptions.Center;
                var visual = word.GetComponent<InferenceTilePresentation>() ?? word.gameObject.AddComponent<InferenceTilePresentation>();
                visual.word = word;
            }
            foreach (var label in b.premises.GetComponentsInChildren<TMP_Text>(true))
                if (label.transform.parent.name == "PremiseLine") Text(label, bodyFont, 30, Pale);
            if (b.premises.parent != null) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)b.premises.parent);
            if (viewport != null) viewport.GetComponent<ScrollRect>().verticalNormalizedPosition=1;
            if (bankViewport != null) bankViewport.GetComponent<ScrollRect>().verticalNormalizedPosition=1;
        }

        void ShowBlankFeedback()
        {
            var b=challenge.dragPuzzle;
            if (challenge.Stage!=InferencePuzzleStage.CompletingPremises || b.Puzzle==null) return;
            for(int i=0;i<b.Zones.Count;i++)
            {
                var z=b.Zones[i]; var v=z.GetComponent<InferenceTilePresentation>();
                if(v!=null) v.MarkInvalid(!string.Equals(z.Value,b.Puzzle.CorrectPlacements[i],System.StringComparison.OrdinalIgnoreCase));
            }
        }
        void RefreshStates()
        {
            if (steps == null) return;
            int stage = (int)challenge.Stage;
            if (stage != lastStep)
            {
                for (int i = 0; i < steps.Length; i++)
                {
                    steps[i].color = i <= stage ? Gold : Muted;
                    var outline = steps[i].transform.parent.GetComponent<Outline>();
                    if (outline != null) outline.effectColor = i == stage ? Gold : new Color(.20f,.32f,.34f);
                }
                lastStep = stage;
            }
            if (lastFeedback != challenge.feedback.text)
            {
                lastFeedback = challenge.feedback.text;
                if (lastFeedback == "Some blanks are incorrect." && challenge.dragPuzzle.Puzzle != null)
                {
                    for (int i=0;i<challenge.dragPuzzle.Zones.Count;i++)
                    {
                        var z=challenge.dragPuzzle.Zones[i];
                        var v=z.GetComponent<InferenceTilePresentation>();
                        if(v!=null) v.MarkInvalid(!string.Equals(z.Value,challenge.dragPuzzle.Puzzle.CorrectPlacements[i],System.StringComparison.OrdinalIgnoreCase));
                    }
                }
            }
            bool searching = challenge.Stage == InferencePuzzleStage.FindingDiamond;
            bool solved = challenge.Stage == InferencePuzzleStage.Solved;
            bankViewport.gameObject.SetActive(!searching && !solved);
            bankStatus.gameObject.SetActive(searching || solved);
            bankStatus.text = solved ? "ARGUMENT COMPLETE\n\nThe conclusion is revealed.\n\nTry another argument, or return to the island."
                : "PREMISES COMPLETE\n\nFind a rule diamond at the pillars.\n\nBring it back to the altar.";
            var hint = content.Find("PuzzleWorkspace/WordBankWorkspace/Theme_BankHint");
            hint.gameObject.SetActive(!searching && !solved);
            
            var stateTitle = Label(bankStatus.transform.parent, "Theme_SearchTitle", searching ? "CRYSTAL SEARCH" : "WELL DONE", 28, Gold); stateTitle.font = displayFont; Box(stateTitle.rectTransform, 22, 18, 350, 40); stateTitle.gameObject.SetActive(searching || solved);
            var badge = content.Find("PuzzleWorkspace/ArgumentWorkspace/Conclusion").GetComponentsInChildren<TMP_Text>(true)
                .First(t => t != challenge.conclusionLabel);
            badge.text = solved ? "∴" : "?";
            badge.font = bodyFont; badge.color = solved ? new Color(.55f,1,.78f) : Muted;
        }

        Button[] Buttons() => new[] { challenge.placeButton, challenge.validateButton, challenge.exploreButton, challenge.nextButton };
        void ArrangeActions()
        {
            var shown = Buttons().Where(b => b.gameObject.activeSelf).ToArray();
            float start = (1488 - shown.Length * 360 - Mathf.Max(0,shown.Length-1)*20)/2;
            for (int i = 0; i < shown.Length; i++) Box((RectTransform)shown[i].transform, start+i*380, 0, 360,118);
        }
        void StyleButton(Button button)
        {
            var image = button.GetComponent<Image>();
            image.sprite = actionBackground; image.type = Image.Type.Simple; image.preserveAspect = true;
            image.color = Color.white; image.raycastTarget = true;
            var cb = button.colors;
            cb.normalColor = cb.highlightedColor = cb.pressedColor = cb.selectedColor = cb.disabledColor = Color.white;
            button.colors = cb; button.transition = Selectable.Transition.None;
            var text = button.GetComponentInChildren<TMP_Text>();
            Text(text, displayFont, 32, Pale); text.alignment = TextAlignmentOptions.Center;
            Stretch(text.rectTransform, 30);
            // Feedback changes editable text and an inset overlay, leaving the bronze frame untinted.
            var face = Rect(button.transform, "Theme_FaceState");
            face.anchorMin = new Vector2(.08f,.28f); face.anchorMax = new Vector2(.92f,.70f);
            face.offsetMin = face.offsetMax = Vector2.zero;
            face.SetSiblingIndex(0);
            var overlay = face.GetComponent<Image>() ?? face.gameObject.AddComponent<Image>();
            overlay.color = Color.clear; overlay.raycastTarget = false;
            var state = button.GetComponent<InferenceButtonPresentation>() ?? button.gameObject.AddComponent<InferenceButtonPresentation>();
            state.button = button; state.face = overlay; state.label = text;
        }

        static RectTransform ScrollArea(Transform parent, RectTransform contents, string name, float x,float y,float w,float h)
        {
            var view = Rect(parent,name); Box(view,x,y,w,h);
            if (view.GetComponent<RectMask2D>() == null) view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.GetComponent<ScrollRect>() ?? view.gameObject.AddComponent<ScrollRect>();
            var background = view.GetComponent<Image>() ?? view.gameObject.AddComponent<Image>();
            background.color = Color.clear; background.raycastTarget = true;
            var fixedSize=contents.GetComponent<LayoutElement>(); if(fixedSize!=null) fixedSize.enabled=false;
            contents.SetParent(view,false); contents.anchorMin=new Vector2(0,1); contents.anchorMax=Vector2.one;
            contents.pivot=new Vector2(.5f,1); contents.anchoredPosition=Vector2.zero; contents.sizeDelta=new Vector2(0,0);
            var fitter=contents.GetComponent<ContentSizeFitter>() ?? contents.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit=ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=view; scroll.content=contents; scroll.horizontal=false; scroll.vertical=true;
                        scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=35;
            var indicator=Rect(parent,name+"Indicator"); Box(indicator,x+w+5,y,5,h);
            var track=indicator.GetComponent<Image>() ?? indicator.gameObject.AddComponent<Image>();
            track.color=new Color(.12f,.20f,.23f);track.raycastTarget=false;
            var sliding=Rect(indicator,"Sliding"); Stretch(sliding,0);
            var thumb=Rect(sliding,"Thumb"); Stretch(thumb,0);
            var thumbImage=thumb.GetComponent<Image>() ?? thumb.gameObject.AddComponent<Image>();
            thumbImage.color=new Color(.60f,.72f,.74f);thumbImage.raycastTarget=false;
            var bar=indicator.GetComponent<Scrollbar>() ?? indicator.gameObject.AddComponent<Scrollbar>();
            bar.handleRect=thumb;bar.targetGraphic=thumbImage;bar.direction=Scrollbar.Direction.BottomToTop;
            bar.transition=Selectable.Transition.None;bar.interactable=false;
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            return view;
        }
        static void DisableLayout(RectTransform r)
        {
            var layout=r.GetComponent<LayoutGroup>(); if(layout!=null) layout.enabled=false;
            var fitter=r.GetComponent<ContentSizeFitter>(); if(fitter!=null) fitter.enabled=false;
        }
        static RectTransform Rect(Transform parent,string name)
        {
            var existing=parent.Find(name) as RectTransform;
            if(existing!=null) return existing;
            var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); return (RectTransform)go.transform;
        }
        static TMP_Text Label(Transform parent,string name,string value,float size,Color color)
        {
            var r=Rect(parent,name); var label=r.GetComponent<TextMeshProUGUI>() ?? r.gameObject.AddComponent<TextMeshProUGUI>();
            label.text=value; label.fontSize=size; label.color=color; label.raycastTarget=false;
            var owner=parent.GetComponentInParent<InferenceBoardPresentation>(); if(owner!=null) label.font=owner.bodyFont;
            return label;
        }
        static void Text(TMP_Text t,TMP_FontAsset font,float size,Color color)
        { t.font=font; t.fontSize=size; t.color=color; t.raycastTarget=false; t.overflowMode=TextOverflowModes.Ellipsis; }
        static void Box(RectTransform r,float x,float y,float width,float height)
        {
            r.anchorMin=r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(width,height);
        }
        static void Stretch(RectTransform r,float inset)
        { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.one*inset;r.offsetMax=-Vector2.one*inset; }
        static void RemoveTileDecorations(Transform parent)
        {
            foreach (Transform child in parent.Cast<Transform>().Where(t => t.name == "Theme_Inset" || t.name == "Theme_TopBevel").ToArray())
            {
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
        }
        static void Frame(RectTransform rect,Color face,Color border,bool decorations = true)
        {
            var image=rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite=null; image.color=face; image.raycastTarget=rect.GetComponent<DropZone>()!=null || rect.GetComponent<DraggableWord>()!=null;
            var outline=rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>();
            outline.effectColor=border; outline.effectDistance=new Vector2(2,-2); outline.useGraphicAlpha=false;
            var shadow=rect.GetComponents<Shadow>().FirstOrDefault(s => !(s is Outline)) ?? rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor=new Color(0,0,0,.65f); shadow.effectDistance=new Vector2(0,-5);
            if (!decorations) { RemoveTileDecorations(rect); return; }
            var inset=Rect(rect,"Theme_Inset"); Stretch(inset,7); inset.SetSiblingIndex(0);
            var inner=inset.GetComponent<Image>() ?? inset.gameObject.AddComponent<Image>(); inner.color=face; inner.raycastTarget=false;
            var keyline=inset.GetComponent<Outline>() ?? inset.gameObject.AddComponent<Outline>();
            keyline.useGraphicAlpha=false; keyline.effectColor=new Color(border.r,border.g,border.b,.28f);keyline.effectDistance=new Vector2(1,-1);
            var bevel=Rect(rect,"Theme_TopBevel"); bevel.anchorMin=new Vector2(0,1);bevel.anchorMax=Vector2.one;
            bevel.pivot=new Vector2(.5f,1);bevel.sizeDelta=new Vector2(-14,2);bevel.anchoredPosition=new Vector2(0,-7);bevel.SetSiblingIndex(1);
            var light=bevel.GetComponent<Image>() ?? bevel.gameObject.AddComponent<Image>();light.color=new Color(1,1,1,.10f);light.raycastTarget=false;
        }
    }

}














