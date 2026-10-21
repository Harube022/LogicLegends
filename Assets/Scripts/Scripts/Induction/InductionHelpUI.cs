using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Induction
{
    [DisallowMultipleComponent]
    public sealed class InductionHelpUI : MonoBehaviour
    {
        public bool showOnEntry = true;
        public bool IsOpen { get; private set; }
        public bool OnboardingPending { get; private set; } = true;
        public int PageIndex { get; private set; }
        public int PageCount => Titles.Length;
        public GameObject Overlay { get; private set; }
        public Button HelpButton { get; private set; }
        public Button PreviousButton { get; private set; }
        public Button NextButton { get; private set; }
        public Button BeginButton { get; private set; }
        InductionStageManager stage;
        TMP_Text title, body, page, notes;
        ScrollRect scroll;
        static readonly string[] Titles = {
            "What is mathematical induction?", "Begin and explore", "Challenge 1 — Base case", "Challenge 2 — Hypothesis", "Challenge 3 — Inductive goal", "Challenge 4 — Proof steps", "Challenge 5 — Full proof", "Collect, place, and submit", "Shared timer and feedback"
        };
        static readonly string[] Bodies = {
            "Induction proves a statement throughout its stated integer domain. First establish the base case. Then, for an arbitrary applicable integer k, assume the statement holds for k. Use that assumption to prove it holds for k + 1. The inductive step must work for every applicable k; testing a few examples is not enough.",
            "Use the joystick to move and the jump button to jump. Face a nearby object and tap the hand button or its contextual interaction button. Interact with Book_Statue to start a challenge. Near the altars, tap View Board to read the objective, then Back to return. Closing Help or board view never starts a challenge.",
            "Verify the statement at its starting integer. Substitute that value into both sides and simplify. Choose the scroll whose equality matches this base case, then place it at the indicated Base Case altar. Press Submit to check your choice.",
            "Identify the statement P(k) for an arbitrary integer k in the theorem's domain. This is the assumption used in the next step, rather than a separate proof of P(k). Select the matching scroll and place it at the indicated Inductive Hypothesis altar.",
            "Identify what P(k + 1) says. Apply k + 1 consistently throughout the statement, including the next term when describing a sum. Choose the goal you need to prove and place its scroll at the indicated Inductive Step altar.",
            "Arrange four proof lines at the Inductive Step altar. Begin with the expression for the next case, use the hypothesis where it applies, and make valid algebraic changes toward the goal. Follow the numbered placement prompts. Every line must follow logically from the previous line. Submit checks the full order.",
            "Complete all three indicated altars: establish the Base Case, select the Inductive Hypothesis, and arrange the numbered Inductive Step lines. Check that the step uses the hypothesis correctly and reaches the required goal. Submit checks every required position across all three stations, not just the last scroll you placed.",
            "Interact with the Formula Scrolls stand. Select a parchment scroll, inspect its formula, then tap Get This Scroll. Carry it to an indicated altar and use the hand or contextual button to place it. Remove a placed scroll to rearrange it, or place another to swap it. Return Scroll makes a held scroll available at the stand again. Press Submit after arranging your answer; placement alone is not graded.",
            "A fresh attempt starts with one shared 18-minute timer. The book starts or resumes it; it does not reset between challenges. Incorrect submissions subtract 10 seconds, keep your scrolls in place, and mark errors or report missing scrolls. Fix the arrangement and submit again. Correct answers pause the timer and unlock the next challenge. Activate the book again to continue. Help pauses gameplay and the timer. At zero, the attempt ends; complete all five challenges to finish the stage."
        };
        static readonly string[] Notes = {
            "BASE CASE\nEstablish the starting case.\n\nHYPOTHESIS\nAssume P(k).\n\nSTEP\nProve P(k + 1) using that assumption.",
            "YOUR ROUTE\nBook → View Board → Formula Scrolls → indicated altar → Submit.\n\nThe book starts challenges; it does not submit answers.",
            "CHECK\nUse the starting value specified by the theorem.\n\nSimplify both sides before choosing.",
            "CHECK\nYour assumption concerns k.\n\nKeep the theorem's domain and the complete statement.",
            "CHECK\nThe goal concerns k + 1.\n\nIdentify what must be proved, rather than repeating the hypothesis.",
            "CHECK\nKeep proof lines in order.\n\nUse valid algebra.\n\nReach the inductive goal.",
            "THREE STATIONS\nBase Case\nInductive Hypothesis\nInductive Step\n\nAll sections must be correct together.",
            "CARRY ONE SCROLL\nPlace or return it before collecting another.\n\nScrolls already carried or placed cannot be collected twice.",
            "INCORRECT\n−10 seconds; placements stay.\n\nCORRECT\nPause and unlock the next challenge.\n\nCONTINUE\nInteract with the book again."
        };
        public void Build(InductionStageManager owner,Transform host)
        {
            stage=owner;
            var settings=host.Find("Gameplay_Interface/SettingsButton") as RectTransform;
            HelpButton=StageUITheme.Button("Induction Help",settings!=null?settings.parent:host,"Help",Open);
            var helpRect=(RectTransform)HelpButton.transform;
            if(settings!=null)
            {
                helpRect.anchorMin=settings.anchorMin;helpRect.anchorMax=settings.anchorMax;helpRect.pivot=settings.pivot;
                helpRect.anchoredPosition=settings.anchoredPosition+new Vector2(settings.rect.width+24,0);helpRect.sizeDelta=new Vector2(160,settings.rect.height);
            }
            else { StageUITheme.Stretch(helpRect,new Vector2(.10f,.84f),new Vector2(.25f,.96f)); }
            StagePresentation.Help(HelpButton, settings);
            var root=StageUITheme.Rect("Induction How To Play",host);StageUITheme.Stretch(root,Vector2.zero,Vector2.one);Overlay=root.gameObject;
            root.gameObject.AddComponent<Image>().color=new Color(.012f,.025f,.03f,.88f);
            var card=StageUITheme.Rect("TutorialCard",root);StageUITheme.Stretch(card,new Vector2(.08f,.08f),new Vector2(.92f,.92f));card.gameObject.AddComponent<Image>();
            StageUITheme.Text("SectionLabel",card,"INDUCTION / HOW TO PLAY",32);
            title=StageUITheme.Text("PageTitle",card,"",44);
            var viewport=StageUITheme.Rect("PageViewport",card);StageUITheme.Stretch(viewport,new Vector2(.08f,.33f),new Vector2(.46f,.65f));
            viewport.gameObject.AddComponent<RectMask2D>();viewport.gameObject.AddComponent<Image>().color=Color.clear;
            scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.viewport=viewport;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            body=StageUITheme.Text("PageBody",viewport,"",32,true);body.alignment=TextAlignmentOptions.TopLeft;
            var content=body.rectTransform;content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
            content.sizeDelta=new Vector2(0,400);content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=content;
            PreviousButton=StageUITheme.Button("PreviousPage",card,"Previous",Previous);
            NextButton=StageUITheme.Button("NextPage",card,"Next",Next);
            BeginButton=StageUITheme.Button("BeginButton",card,"Let’s Begin",BeginFromHelp);
            page=StageUITheme.Text("PageIndicator",card,"",28);
            StageUITheme.SkinTutorial(root.gameObject);
            var barRect=StageUITheme.Rect("Help Reading Scrollbar",card);StageUITheme.Stretch(barRect,new Vector2(.466f,.33f),new Vector2(.476f,.65f));
            barRect.gameObject.AddComponent<Image>().color=new Color(.3f,.17f,.07f,.2f);
            var thumb=StageUITheme.Rect("Reading Thumb",barRect);StageUITheme.Stretch(thumb,Vector2.zero,Vector2.one);
            var thumbImage=thumb.gameObject.AddComponent<Image>();thumbImage.color=new Color(.4f,.24f,.10f,.7f);
            var bar=barRect.gameObject.AddComponent<Scrollbar>();bar.handleRect=thumb;bar.targetGraphic=thumbImage;bar.direction=Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            var swipe=StageUITheme.Text("Reading Hint",card,"Swipe the page to read more",24,true);
            StageUITheme.Stretch(swipe.rectTransform,new Vector2(.08f,.285f),new Vector2(.46f,.32f));swipe.color=StageUITheme.ReadingInk;swipe.font=stage.readingFont;
            card.Find("FieldNotesHeading").GetComponent<TMP_Text>().text="PROOF CHECKLIST";
            notes=card.Find("FieldNotesBody").GetComponent<TMP_Text>();notes.font=stage.readingFont;notes.enableAutoSizing=true;notes.fontSizeMin=24;notes.fontSizeMax=31;
            body.font=stage.readingFont;body.fontSize=30;body.enableAutoSizing=false;body.outlineWidth=0;
            title.fontSizeMin=28;title.fontSizeMax=44;title.enableAutoSizing=true;
            Overlay.SetActive(false);
        }
        public void Open()
        {
            if(IsOpen||Overlay==null||!stage.BeginModal())return;
            IsOpen=true;PageIndex=0;ShowPage();stage.ui.ShowHelp(true);Overlay.SetActive(true);Overlay.transform.SetAsLastSibling();stage.ui.Refresh();
        }
        public void Close() { if(IsOpen)stage.CloseReading(); }
        void BeginFromHelp()
        {
            if(!IsOpen)return;
            Close();
            if(OnboardingPending)stage.onboarding.Begin(stage);
        }
        internal void CompleteOnboarding(){OnboardingPending=false;}
        public void Hide()
        {
            if(!IsOpen)return;IsOpen=false;if(Overlay!=null)Overlay.SetActive(false);stage.ui.ShowHelp(false);
        }
        public void Previous(){if(IsOpen&&PageIndex>0){PageIndex--;ShowPage();}}
        public void Next(){if(IsOpen&&PageIndex<PageCount-1){PageIndex++;ShowPage();}}
        void ShowPage()
        {
            title.text=Titles[PageIndex];body.text=Bodies[PageIndex];notes.text=Notes[PageIndex];page.text=(PageIndex+1)+" / "+PageCount;
            PreviousButton.interactable=PageIndex>0;NextButton.interactable=PageIndex<PageCount-1;body.rectTransform.anchoredPosition=Vector2.zero;scroll.StopMovement();
        }
        void Update(){if(HelpButton!=null)HelpButton.gameObject.SetActive(stage.CanAct);}
        void OnDisable(){if(IsOpen&&stage!=null)stage.CloseReading();}
    }
}
