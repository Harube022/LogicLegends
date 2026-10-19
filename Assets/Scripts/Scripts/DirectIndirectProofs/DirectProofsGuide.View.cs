using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class DirectProofsGuide
{
    struct Lesson
    {
        public string title, body;
        public Lesson(string title, string body) { this.title = title; this.body = body; }
    }
    Lesson[] HelpPages => new[] {
        new Lesson("Direct and indirect proofs", "A proof establishes a conclusion from accepted premises using valid logical steps. A direct proof starts from the given assumptions and derives the conclusion. An indirect proof establishes the conclusion through a contrapositive or a contradiction."),
        new Lesson("The two indirect methods", "For an implication P → Q, proof by contrapositive establishes ¬Q → ¬P. This is logically equivalent to the original implication. Proof by contradiction assumes the opposite of the desired claim and derives an impossibility or a conflict with known facts. A contradiction must be logically justified, not merely an unexpected result."),
        new Lesson("Your objective and flow", "Solve five challenges, with two distinct, randomly selected questions from each challenge's existing bank: ten questions total. Use the hand at the Book Statue to start each question. Read the Information Board, use the active proof altar, then return to the book and use the hand to confirm your entire answer."),
        new Lesson("Challenge 1 — Identify the method", "Read the supplied argument. Choose Direct Proof if it derives the conclusion from the givens. Choose Indirect Proof if it proves a contrapositive or reaches a contradiction. Approach the corresponding altar and use the hand to select it; return to the book to confirm. The selection glow shows your choice, not whether it is correct."),
        new Lesson("Challenges 2 and 3 — Order the reasoning", "Challenge 2 orders a direct derivation. Challenge 3 orders a proof by contrapositive. Approach the active altar and use the hand to open its proof board. Read each statement and its justification. Drag one row onto another to swap them. Place assumptions and definitions before the steps that use them. Back to Room keeps the order; confirm at the book."),
        new Lesson("Challenges 4 and 5 — Complete the proof", "Challenge 4 orders a contradiction proof on the neutral trial altar: assumption, consequences, contradiction, and conclusion. Challenge 5 requires both a proof-strategy choice and a complete ordered proof. The available strategy buttons are Direct, Contrapositive, and Contradiction. Read the supplied statements before choosing. These are rows on the altar board; there is no gameplay scroll collection requirement."),
        new Lesson("Timing, corrections and feedback", "One shared " + StageJourneyUI.FormatTime(stage.StartingSeconds) + " countdown starts or resumes through the book. It continues while reading and arranging; Help and tutorials pause it. A wrong validated proof subtracts " + stage.WrongPenalty.ToString("0.##") + " seconds and briefly flashes the book red. Your selected strategy and row order remain. Reopen the altar and swap rows to correct mistakes. Missing prerequisite actions prompt you to finish them before validation."),
        new Lesson("Progress and practice", "Correct answers pause the timer. Activate the book for the next question; both questions must be completed before the next challenge. The timer does not reset between them. Each challenge has one short introduction. Time up offers Retry or Menu; Retry begins a fresh randomized run. Let's Begin practices controls, classification, ordering, correction, and confirmation without using real questions or countdown time.") };

    RectTransform root, card, viewport, highlight, arrow, worldHighlight;
    TMP_Text heading, body;
    ScrollRect scroll;
    Image shade;
    RectTransform controlTarget;
    RectTransform helpCard, instructionCard, helpViewport, instructionViewport;
    TMP_Text helpHeading, helpBody, instructionHeading, instructionBody, pageIndicator;
    ScrollRect helpScroll, instructionScroll;
    Button helpNext, instructionNext;
    TMP_Text scrollHint;
    TMP_Text layoutProbe;
    bool resetScroll;
    readonly Vector3[] uiCorners = new Vector3[4];
    readonly Dictionary<RectTransform, TouchLayout> touchLayouts = new Dictionary<RectTransform, TouchLayout>();
    Transform touchRoot;
    struct TouchLayout
    {
        public Vector2 min, max, pivot, position;
        public Vector3 scale;
        public TouchLayout(RectTransform r) { min = r.anchorMin; max = r.anchorMax; pivot = r.pivot; position = r.anchoredPosition; scale = r.localScale; }
        public void Restore(RectTransform r) { r.anchorMin = min; r.anchorMax = max; r.pivot = pivot; r.anchoredPosition = position; r.localScale = scale; }
    }
    void BuildUI()
    {
        HelpButton = Instantiate(Resources.Load<GameObject>("StageUI/PrelimHelpButton"), SafeArea, false).GetComponent<Button>();
        HelpButton.name = "Direct Proofs Help"; HelpButton.onClick.AddListener(OpenHelp);
        root = StageUITheme.Rect("Direct Proofs Guide Overlay", SafeArea); StageUITheme.Stretch(root, Vector2.zero, Vector2.one);
        shade = root.gameObject.AddComponent<Image>(); shade.color = new Color(.01f,.035f,.045f,.88f);
        helpCard = Instantiate(Resources.Load<GameObject>("StageUI/PrelimHelpBook"), root, false).GetComponent<RectTransform>();
        helpCard.name = "Direct Proofs Help Book";
        helpHeading = helpCard.Find("PageTitle").GetComponent<TMP_Text>();
        helpBody = helpCard.Find("PageBody").GetComponent<TMP_Text>();
        pageIndicator = helpCard.Find("PageIndicator").GetComponent<TMP_Text>();
        helpCard.Find("SectionLabel").GetComponent<TMP_Text>().text = "HOW TO PLAY";
        helpCard.Find("FieldNotesBody").GetComponent<TMP_Text>().text = "YOUR ROUTE\nBook / Information Board / Proof Altar / Book\n\nDirect: derive the conclusion.\nContrapositive: prove ¬Q → ¬P.\nContradiction: justify an impossibility.\n\nFive challenges, two questions each. One shared countdown.";
        PreviousButton = helpCard.Find("BackButton").GetComponent<Button>(); PreviousButton.onClick.AddListener(Previous);
        helpNext = helpCard.Find("NextButton").GetComponent<Button>(); helpNext.onClick.AddListener(Next);
        BeginButton = helpCard.Find("BeginButton").GetComponent<Button>(); BeginButton.onClick.AddListener(BeginFromHelp);
        AddReadingScroll(helpCard, helpBody, out helpViewport, out helpScroll);

        instructionCard = StageUITheme.Rect("Controls Tutorial Instructions", root);
        instructionCard.gameObject.AddComponent<Image>().color = new Color(.06f,.13f,.19f,.93f);
        instructionHeading = StageUITheme.Text("Tutorial Heading", instructionCard, "", 48, true);
        instructionHeading.font = TMP_Settings.defaultFontAsset; instructionHeading.color = Color.white;
        instructionBody = StageUITheme.Text("Tutorial Instruction", instructionCard, "", 38, true);
        instructionBody.font = TMP_Settings.defaultFontAsset; instructionBody.color = Color.white;
        instructionBody.alignment = TextAlignmentOptions.Center;
        layoutProbe = StageUITheme.Text("Instruction Layout Measure", instructionCard, "", 36, true);
        layoutProbe.font = instructionBody.font; layoutProbe.enabled = false;
        AddReadingScroll(instructionCard, instructionBody, out instructionViewport, out instructionScroll);
        scrollHint = StageUITheme.Text("Tutorial Scroll Hint", instructionCard, "Swipe / scroll to read more", 22, true);
        scrollHint.font = TMP_Settings.defaultFontAsset; scrollHint.color = new Color(.8f,.88f,.9f);
        instructionNext = PrelimUIPresentation.TutorialButton(instructionCard, "Next", Vector2.zero, Next);
        ActionButton = PrelimUIPresentation.TutorialButton(instructionCard, "Yes", new Vector2(-155,-71), () => PracticeAction());
        AlternateButton = PrelimUIPresentation.TutorialButton(instructionCard, "No", new Vector2(155,-71), () => PracticeAction(true));
        highlight = StageUITheme.Rect("Direct Tutorial Highlight", SafeArea); PrelimUIPresentation.Highlight(highlight);
        arrow = StageUITheme.Text("Direct Tutorial Arrow", highlight, "▼", 72, true).rectTransform;
        arrow.GetComponent<TMP_Text>().font = TMP_Settings.defaultFontAsset;
        arrow.GetComponent<TMP_Text>().color = new Color(1,.9f,.25f);
        worldHighlight = StageUITheme.Rect("Direct World Highlight", root); PrelimUIPresentation.Highlight(worldHighlight); worldHighlight.SetAsFirstSibling();
        root.gameObject.SetActive(false); highlight.gameObject.SetActive(false);
        card = helpCard; heading = helpHeading; body = helpBody; viewport = helpViewport; scroll = helpScroll; NextButton = helpNext;
    }
    static void AddReadingScroll(RectTransform parent, TMP_Text text, out RectTransform view, out ScrollRect scrolling)
    {
        var host = StageUITheme.Rect("Reading Scroll", parent); StageUITheme.Stretch(host, Vector2.zero, Vector2.one);
        scrolling = host.gameObject.AddComponent<ScrollRect>(); scrolling.horizontal = false;
        scrolling.movementType = ScrollRect.MovementType.Clamped; scrolling.scrollSensitivity = 40;
        view = StageUITheme.Rect("Viewport", host); StageUITheme.Stretch(view, Vector2.zero, Vector2.one);
        view.gameObject.AddComponent<Image>().color = Color.clear; view.gameObject.AddComponent<RectMask2D>();
        text.transform.SetParent(view, false); text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = false;
        text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrolling.viewport = view; scrolling.content = text.rectTransform;
        ResetContent(text); host.SetAsLastSibling();
    }
    static void ResetContent(TMP_Text text)
    {
        var r=text.rectTransform; r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);
        float width = Mathf.Max(1, ((RectTransform)r.parent).rect.width);
        r.sizeDelta=new Vector2(0,text.GetPreferredValues(text.text,width,0).y);
        r.anchoredPosition=new Vector2(0,r.anchoredPosition.y);
    }
    void SetCard(string title, string message)
    {
        bool help = mode == Mode.Help;
        helpCard.gameObject.SetActive(help); instructionCard.gameObject.SetActive(!help);
        card = help ? helpCard : instructionCard; heading = help ? helpHeading : instructionHeading;
        body = help ? helpBody : instructionBody; viewport = help ? helpViewport : instructionViewport;
        scroll = help ? helpScroll : instructionScroll; NextButton = help ? helpNext : instructionNext;
        heading.text = title; body.text = mode == Mode.Introduction ? "<b>" + title + "</b>\n\n" + message : message;
        body.rectTransform.anchoredPosition=Vector2.zero; scroll.verticalNormalizedPosition = 1;
        resetScroll=true; root.gameObject.SetActive(true); enteredAt = Time.unscaledTime;
    }
    void SetActions(string first, string second = null)
    {
        ActionButton.gameObject.SetActive(true); ActionButton.GetComponentInChildren<TMP_Text>().text = first;
        AlternateButton.gameObject.SetActive(second != null); AlternateButton.GetComponentInChildren<TMP_Text>().text = second ?? "";
    }
    void SetHighlight(RectTransform target)
    {
        controlTarget = target; highlight.gameObject.SetActive(target != null);
        if (target != null)
        {
            highlight.SetParent(target, false); StageUITheme.Stretch(highlight, Vector2.zero, Vector2.one);
            highlight.offsetMin = -Vector2.one * 10; highlight.offsetMax = Vector2.one * 10;
            arrow.anchorMin = arrow.anchorMax = new Vector2(.5f, 1); arrow.pivot = new Vector2(.5f, 0);
            arrow.sizeDelta = new Vector2(110, 90); arrow.anchoredPosition = new Vector2(0, 16);
        }
        else highlight.SetParent(SafeArea, false);
    }
    void LayoutUI()
    {
        if (SafeArea == null || card == null) return;
        FitPortraitControls(); stage.LayoutGuidePresentation(SafeArea);
        PrelimUIPresentation.CompactHud(hud, SafeArea, hud.Find("CurrentStageLabel").GetComponent<TMP_Text>(), stage.Clock, stage.Progress);
        LayoutHelpButton();
        if (!IsBusy) return;
        bool helpMode = mode == Mode.Help, confirm = IsPractice && Step == DirectProofsPracticeStep.Confirmation;
        shade.enabled = helpMode || confirm;
        if (helpMode) { LayoutHelpBook(); return; }
        var area = SafeArea.rect;
        instructionHeading.gameObject.SetActive(confirm);
        instructionViewport.parent.gameObject.SetActive(!confirm);
        scrollHint.gameObject.SetActive(!confirm);
        card.GetComponent<Image>().color = confirm ? new Color(.06f,.16f,.22f,.98f) : new Color(.06f,.13f,.19f,.93f);
        if (confirm)
        {
            card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);
            card.anchoredPosition=Vector2.zero; card.sizeDelta=new Vector2(Mathf.Min(660,area.width-40),280);
            heading.text="Let's start?"; heading.fontSize=48;
            var title=heading.rectTransform;title.anchorMin=title.anchorMax=title.pivot=new Vector2(.5f,1);
            title.anchoredPosition=new Vector2(0,-24);title.sizeDelta=new Vector2(card.sizeDelta.x-60,84);
            ConfirmationButton(ActionButton,-155); ConfirmationButton(AlternateButton,155); return;
        }
        bool actions=ActionButton.gameObject.activeSelf;
        RectTransform target = controlTarget != null ? controlTarget : worldHighlight.gameObject.activeSelf ? worldHighlight : null;
        var blockers = new List<RectTransform>(ControlTutorialLayout.FindBlockers());
        blockers.AddRange(new[]{hud,hand,jump,joystick,skip!=null?(RectTransform)skip.transform:null});
        // The shared layout measures a direct child. Keep its geometry changes
        // separate from the ScrollRect's content so scrolling retains its offset.
        layoutProbe.text=body.text;
        ControlTutorialLayout.Place(card, layoutProbe, (RectTransform)NextButton.transform, SafeArea, target, mode==Mode.Introduction, blockers.ToArray(), controlTarget!=null?arrow:null);
        // PRELIM spacing, with scrolling for longer proof explanations.
        card.sizeDelta=new Vector2(card.sizeDelta.x,Mathf.Min(card.sizeDelta.y+(actions?100:0),area.height*.48f));
        bool ordering=IsPractice&&Step==DirectProofsPracticeStep.Arrange;
        if(ordering)
        {
            bool portrait=area.width<area.height;
            stage.OrderingBoard.ViewArea=portrait?new Rect(.02f,.38f,.96f,.55f):new Rect(.02f,.1f,.6f,.8f);
            card.sizeDelta=portrait?new Vector2(area.width*.96f,area.height*.31f):new Vector2(area.width*.34f,area.height*.65f);
            card.anchoredPosition=portrait?new Vector2(0,-area.height*.32f):new Vector2(area.width*.31f,0);
        }
        else card.anchoredPosition=mode==Mode.Introduction ? new Vector2(0,-area.height*.5f+card.sizeDelta.y*.5f+20) : BestCardPosition(card.sizeDelta);
        StageUITheme.Stretch((RectTransform)viewport.parent, Vector2.zero,Vector2.one);
        viewport.offsetMin=new Vector2(24,NextButton.gameObject.activeSelf||actions?106:24);viewport.offsetMax=new Vector2(-24,-24);
        body.fontSize=36;ResetContent(body);
        bool overflow=body.rectTransform.rect.height>viewport.rect.height+2;
        scrollHint.gameObject.SetActive(overflow);
        var hint=scrollHint.rectTransform;hint.anchorMin=hint.anchorMax=hint.pivot=new Vector2(.5f,0);
        hint.anchoredPosition=new Vector2(0,NextButton.gameObject.activeSelf||actions?90:4);
        hint.sizeDelta=new Vector2(card.sizeDelta.x-48,26);
        ApplyScrollStart();
        bool two=AlternateButton.gameObject.activeSelf;
        Place(ActionButton,two?-145:0,14,two?260:300);Place(AlternateButton,145,14,260);
    }
    void ConfirmationButton(Button button,float x)
    {
        var r=(RectTransform)button.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
        float scale=Mathf.Min(1,(card.sizeDelta.x-40)/600);
        r.sizeDelta=new Vector2(250*scale,92);r.anchoredPosition=new Vector2(x*scale,-71);
    }
    void LayoutHelpButton()
    {
        var rect=(RectTransform)HelpButton.transform;
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.sizeDelta=new Vector2(220,88);
        var dock=settings!=null?settings:GameObject.Find("Canvas 1")?.transform.Find("SafeArea/Gameplay_Interface/SettingsButton") as RectTransform;
        if(dock==null){rect.anchoredPosition=new Vector2(230,-43);return;}
        float scale=dock.parent.lossyScale.x;rect.localScale=Vector3.one*scale/Mathf.Max(.001f,rect.parent.lossyScale.x);
        dock.GetWorldCorners(uiCorners);
        if(Screen.height>Screen.width){rect.sizeDelta=new Vector2(220,74);rect.position=uiCorners[0]-Vector3.up*18*scale;}
        else rect.position=new Vector3(uiCorners[2].x+35*scale,(uiCorners[0].y+uiCorners[1].y)*.5f+44*scale,uiCorners[1].z);
    }
    void LayoutHelpBook()
    {
        bool portrait=Screen.height>Screen.width;
        StageUITheme.Stretch(helpCard,new Vector2(.07f,.07f),new Vector2(.93f,.93f));
        StageUITheme.Stretch((RectTransform)helpCard.Find("LeftReadingPage"),new Vector2(.045f,.25f),new Vector2(portrait?.955f:.49f,.85f));
        foreach(var name in new[]{"RightReadingPage","BookSpine","FieldNotesHeading","FieldNotesBody"})helpCard.Find(name).gameObject.SetActive(!portrait);
        StageUITheme.Stretch(helpHeading.rectTransform,new Vector2(portrait?.13f:.08f,portrait?.66f:.67f),new Vector2(portrait?.87f:.46f,portrait?.80f:.78f));
        helpHeading.enableAutoSizing=true;helpHeading.fontSizeMin=portrait?26:32;helpHeading.fontSizeMax=portrait?36:44;
        var host=(RectTransform)helpViewport.parent;
        StageUITheme.Stretch(host,new Vector2(portrait?.13f:.08f,.30f),new Vector2(portrait?.87f:.46f,portrait?.64f:.65f));
        StageUITheme.Stretch(helpViewport,Vector2.zero,Vector2.one);helpBody.fontSize=portrait?27:31;
        ResetContent(helpBody);
        StageUITheme.Stretch((RectTransform)PreviousButton.transform,new Vector2(.09f,portrait?.16f:.10f),new Vector2(portrait?.39f:.28f,portrait?.23f:.21f));
        StageUITheme.Stretch((RectTransform)helpNext.transform,new Vector2(portrait?.61f:.30f,portrait?.16f:.10f),new Vector2(portrait?.91f:.49f,portrait?.23f:.21f));
        StageUITheme.Stretch(pageIndicator.rectTransform,new Vector2(portrait?.40f:.50f,portrait?.16f:.10f),new Vector2(portrait?.60f:.62f,portrait?.23f:.21f));
        StageUITheme.Stretch((RectTransform)BeginButton.transform,new Vector2(portrait?.20f:.64f,portrait?.05f:.10f),new Vector2(portrait?.80f:.91f,portrait?.12f:.21f));
        pageIndicator.text=(page+1)+" / "+HelpPages.Length;
        ApplyScrollStart();
    }
    void ApplyScrollStart()
    {
        if(!resetScroll)return;
        Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition=1;resetScroll=false;
    }
    void FitPortraitControls()
    {
        if (Screen.width >= Screen.height) { RestoreTouchLayout(); return; }
        if (touchRoot == null) touchRoot = GameObject.Find("Canvas 1")?.transform.Find("SafeArea/Gameplay_Interface");
        if (touchRoot == null) return;
        var corners = new Vector3[4]; SafeArea.GetWorldCorners(corners);
        var low = RectTransformUtility.WorldToScreenPoint(null, corners[0]); var high = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        float width = high.x - low.x;
        FitTouch(touchRoot.Find("Joystick") as RectTransform, low + new Vector2(width * .17f, width * .15f), width * .28f);
        FitTouch(touchRoot.Find("InteractButton") as RectTransform, new Vector2(high.x - width * .12f, low.y + width * .14f), width * .18f);
        FitTouch(touchRoot.Find("JumpButton") as RectTransform, new Vector2(high.x - width * .12f, low.y + width * .35f), width * .18f);
        FitTouch(touchRoot.Find("SettingsButton") as RectTransform, new Vector2(low.x + width * .075f, high.y - width * .075f), width * .095f);
    }
    void FitTouch(RectTransform r, Vector2 center, float diameter)
    {
        if (r == null || !(r.parent is RectTransform parent)) return;
        if (!touchLayouts.ContainsKey(r)) touchLayouts[r] = new TouchLayout(r);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        float scale = diameter / Mathf.Max(r.rect.width * parent.lossyScale.x, r.rect.height * parent.lossyScale.y);
        r.localScale = new Vector3(scale, scale, 1);
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, center, null, out Vector3 point)) r.position = point;
    }
    void RestoreTouchLayout()
    { foreach (var pair in touchLayouts) if (pair.Key != null) pair.Value.Restore(pair.Key); touchLayouts.Clear(); }
    static void Place(Button button, float x, float y, float width)
    { ProofsStageUI.Bottom((RectTransform)button.transform, new Vector2(x, y), new Vector2(width, 76)); }
    Rect RelativeRect(RectTransform rect)
    { var b = RectTransformUtility.CalculateRelativeRectTransformBounds(SafeArea, rect); return new Rect(b.min, b.size); }
    Vector2 BestCardPosition(Vector2 size)
    {
        var blocks = new List<Rect>();
        if (controlTarget != null) blocks.Add(RelativeRect(controlTarget));
        if (worldSubject != null && Camera.main != null)
        {
            var bounds = ObjectBounds(worldSubject); Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity; bool visible = true;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                var point = Camera.main.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                if (point.z <= 0) { visible = false; continue; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(SafeArea, point, null, out Vector2 local); min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            if (visible) blocks.Add(Rect.MinMaxRect(min.x - 16, min.y - 16, max.x + 16, max.y + 16));
        }
        foreach (var item in new[] { hand, jump, joystick, hud, skip != null ? (RectTransform)skip.transform : null })
            if (item != null && item.gameObject.activeInHierarchy) blocks.Add(RelativeRect(item));
        var area = SafeArea.rect; Vector2 best = Vector2.zero; float score = float.MaxValue;
        for (int y = 2; y <= 8; y++) for (int x = 2; x <= 8; x++)
        {
            Vector2 center = new Vector2(Mathf.Clamp(area.xMin + area.width * x / 10, area.xMin + size.x / 2 + 20, area.xMax - size.x / 2 - 20), Mathf.Clamp(area.yMin + area.height * y / 10, area.yMin + size.y / 2 + 20, area.yMax - size.y / 2 - 20));
            var box = new Rect(center - size / 2, size); float overlap = 0;
            foreach (var b in blocks) overlap += Mathf.Max(0, Mathf.Min(box.xMax, b.xMax) - Mathf.Max(box.xMin, b.xMin)) * Mathf.Max(0, Mathf.Min(box.yMax, b.yMax) - Mathf.Max(box.yMin, b.yMin));
            float value = overlap * 10000 + Vector2.Distance(center, area.center);
            if (value < score) { score = value; best = center - area.center; }
        }
        return best;
    }
}
