using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Induction
{
    public sealed class InductionUI : MonoBehaviour
    {
        public RectTransform Root { get; private set; }
        public Button InteractButton { get; private set; }
        public Button ReturnButton { get; private set; }
        public Button SubmitButton { get; private set; }
        public Button ViewBoardButton { get; private set; }
        public Button BoardBackButton { get; private set; }
        public Button GetScrollButton { get; private set; }
        public Button PracticeChoice { get; private set; }
        public RectTransform TimerTarget => clock.rectTransform;
        public RectTransform ProgressTarget => progress.rectTransform;
        public bool PracticeCatalogueOpen { get; private set; }
        public GameObject Collection => collection != null ? collection.gameObject : null;
        public GameObject Reader => reader != null ? reader.gameObject : null;
        public StageResultCard Result { get; private set; }
        InductionStageManager stage;
        TMP_Text clock, progress, feedback, carry, readerTitle, readerBody, announcementText;
        RectTransform reader, announcement;
        Sprite scrollRibbon;
        RectTransform collection, catalogue, details, catalogueContent, statusHud;
        TMP_Text collectionTitle, catalogueHint, selectedFormula, availability;
        InductionTablet selectedScroll;
        bool controlsHidden;
        GameObject controls;
        bool controlsPreviouslyActive;
        float announcementEnd, feedbackEnd;
        public void Build(InductionStageManager owner, Transform host)
        {
            stage = owner; controls = host.Find("Gameplay_Interface").gameObject;
            Root = StageUITheme.Rect("Induction HUD", host); StageUITheme.Stretch(Root, Vector2.zero, Vector2.one);
            var hud = Panel("Induction Status", Root, new Vector2(.28f,.84f), new Vector2(.73f,.99f));statusHud=hud;
            var title = Text("Stage Title", hud, TrialCatalog.ForScene("Induction").Title, 28);
            Place(title.rectTransform,.06f,.56f,.94f,.96f);
            title.fontSizeMin=22;title.textWrappingMode=TextWrappingModes.Normal;
            clock = Text("Shared Countdown",hud,"18:00",48); Place(clock.rectTransform,.05f,.12f,.27f,.52f);
            progress = Text("Challenge Progress",hud,"Challenge 1 of 5\nWaiting for book",29); Place(progress.rectTransform,.3f,.12f,.94f,.52f);
            feedback = Text("Submission Feedback",Root,"",30,true); Place(feedback.rectTransform,.28f,.74f,.75f,.84f);
            var outline = feedback.gameObject.AddComponent<Outline>(); outline.effectColor=Color.black; outline.effectDistance=new Vector2(1,-1);
            carry = Text("Held Formula",Root,"",29,true); Place(carry.rectTransform,.40f,.43f,.80f,.52f);
            carry.outlineWidth=.15f;carry.outlineColor=Color.black;
            InteractButton = Button("Context Interaction",Root,"Interact",stage.TryHandleInteraction);
            Place((RectTransform)InteractButton.transform,.40f,.1f,.605f,.20f);
            ReturnButton = Button("Return Tablet",Root,"Return Scroll",()=>{if(!PracticeAction("return"))stage.ReturnHeld();});
            Place((RectTransform)ReturnButton.transform,.40f,.21f,.605f,.29f);
            ViewBoardButton=Button("View Board",Root,"View Board",stage.boardView.ViewBoard);
            Place((RectTransform)ViewBoardButton.transform,.40f,.30f,.595f,.40f);
            SubmitButton=Button("Submit Challenge",Root,"Submit",()=>{if(!PracticeAction("submit"))stage.Submit();});
            Place((RectTransform)SubmitButton.transform,.61f,.30f,.805f,.40f);SubmitButton.gameObject.SetActive(false);
            BoardBackButton=Button("Back from Board",Root,"Back",stage.boardView.Back);
            Place((RectTransform)BoardBackButton.transform,.76f,.045f,.96f,.145f);BoardBackButton.gameObject.SetActive(false);
            BuildReader();
            BuildCollection();
            announcement = Panel("Centered Challenge Notice",Root,new Vector2(.23f,.44f),new Vector2(.77f,.66f));
            announcementText = Text("Notice",announcement,"",40,true); Place(announcementText.rectTransform,.07f,.12f,.93f,.88f);
            announcement.gameObject.SetActive(false);
            Result = StageResultCard.Build(Root,()=>stage.Navigate("Main Menu"));
        }
        void BuildReader()
        {
            reader = StageUITheme.Rect("Induction Reading Panel",Root); StageUITheme.Stretch(reader,Vector2.zero,Vector2.one);
            reader.gameObject.AddComponent<Image>().color=new Color(.01f,.025f,.035f,.87f);
            var card=Panel("Reading Frame",reader,new Vector2(.10f,.08f),new Vector2(.90f,.92f));
            var paper=StageUITheme.Rect("Reading Paper",card); Place(paper,.055f,.18f,.945f,.94f);
            var image=paper.gameObject.AddComponent<Image>(); image.sprite=StageUITheme.Paper;image.type=Image.Type.Sliced;image.color=new Color(1,.96f,.9f);image.raycastTarget=false;
            readerTitle=Text("Reading Title",paper,"",40,true); Place(readerTitle.rectTransform,.07f,.80f,.93f,.94f);readerTitle.color=StageUITheme.ReadingInk;
            var viewport=StageUITheme.Rect("Text Viewport",paper); Place(viewport,.08f,.06f,.92f,.79f);viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color=Color.clear;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.viewport=viewport;
            readerBody=Text("Reading Body",viewport,"",35,true);readerBody.color=StageUITheme.ReadingInk;readerBody.alignment=TextAlignmentOptions.TopLeft;
            var rt=readerBody.rectTransform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,1);rt.anchoredPosition=Vector2.zero;rt.sizeDelta=new Vector2(0,550);
            var size=readerBody.gameObject.AddComponent<ContentSizeFitter>();size.verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=rt;scroll.scrollSensitivity=35;
            var close=Button("Close Reading",card,"Back",stage.CloseReading);Place((RectTransform)close.transform,.37f,.055f,.63f,.15f);
            reader.gameObject.SetActive(false);
        }
        void BuildCollection()
        {
            collection=StageUITheme.Rect("Formula Scrolls Catalogue",Root);StageUITheme.Stretch(collection,Vector2.zero,Vector2.one);
            collection.gameObject.AddComponent<Image>().color=new Color(.01f,.025f,.035f,.9f);
            var card=Panel("Scroll Catalogue Frame",collection,new Vector2(.1f,.08f),new Vector2(.9f,.92f));
            var paper=StageUITheme.Rect("Catalogue Reading Paper",card);Place(paper,.055f,.18f,.945f,.94f);
            var image=paper.gameObject.AddComponent<Image>();image.sprite=StageUITheme.Paper;image.type=Image.Type.Sliced;image.color=new Color(1,.96f,.9f);image.raycastTarget=false;
            collectionTitle=Text("Catalogue Title",paper,"FORMULA SCROLLS",40,true);Place(collectionTitle.rectTransform,.07f,.81f,.93f,.94f);collectionTitle.color=StageUITheme.ReadingInk;
            catalogueHint=Text("Catalogue Instructions",paper,"Select a scroll. Swipe to browse the list.",26,true);Place(catalogueHint.rectTransform,.08f,.72f,.92f,.80f);catalogueHint.color=StageUITheme.ReadingInk;
            catalogue=StageUITheme.Rect("Available Scrolls",paper);Place(catalogue,.08f,.07f,.92f,.70f);
            var viewport=StageUITheme.Rect("Catalogue Viewport",catalogue);StageUITheme.Stretch(viewport,Vector2.zero,Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();viewport.gameObject.AddComponent<Image>().color=Color.clear;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.viewport=viewport;scroll.scrollSensitivity=35;
            catalogueContent=StageUITheme.Rect("Scroll Choices",viewport);catalogueContent.anchorMin=new Vector2(0,1);catalogueContent.anchorMax=Vector2.one;
            catalogueContent.pivot=new Vector2(.5f,1);scroll.content=catalogueContent;
            details=StageUITheme.Rect("Selected Scroll",paper);Place(details,.08f,.06f,.92f,.79f);
            selectedFormula=Text("Selected Formula",details,"",40,true);Place(selectedFormula.rectTransform,.04f,.43f,.96f,.97f);
            selectedFormula.color=StageUITheme.ReadingInk;
            availability=Text("Scroll Availability",details,"",28,true);Place(availability.rectTransform,.04f,.21f,.96f,.42f);
            availability.color=StageUITheme.ReadingInk;
            GetScrollButton=Button("Get This Scroll",details,"Get This Scroll",()=>{if(!PracticeAction("get"))stage.CollectScroll(selectedScroll);});
            Place((RectTransform)GetScrollButton.transform,.25f,.02f,.75f,.19f);
            var back=Button("Back to Scroll List",card,"Back",()=>{if(PracticeCatalogueOpen){stage.onboarding.PracticeCatalogueBack(details.gameObject.activeSelf);return;}if(details.gameObject.activeSelf)ShowCatalogue();else stage.CloseReading();});
            Place((RectTransform)back.transform,.06f,.04f,.28f,.15f);
            var close=Button("Close Catalogue",card,"Close",()=>{if(!PracticeAction("close"))stage.CloseReading();});Place((RectTransform)close.transform,.70f,.04f,.94f,.15f);
            collection.gameObject.SetActive(false);
        }
        void HideControls()
        {
            if(controlsHidden)return;controlsHidden=true;controlsPreviouslyActive=controls.activeSelf;controls.SetActive(false);
        }
        public void OpenCollection()
        {
            HideControls();selectedScroll=null;
            foreach(Transform child in catalogueContent){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            ShowCatalogue();collection.gameObject.SetActive(true);collection.SetAsLastSibling();Canvas.ForceUpdateCanvases();
            collectionTitle.text="FORMULA SCROLLS — CHALLENGE "+(stage.ChallengeIndex+1);
            // Leave space within each rolled parchment for complete multi-line formulas.
            float row=Mathf.Max(180,((RectTransform)catalogueContent.parent).rect.height/2);
            catalogueContent.sizeDelta=new Vector2(0,Mathf.Max(200,stage.Tablets.Count*row));catalogueContent.anchoredPosition=Vector2.zero;
            for(int i=0;i<stage.Tablets.Count;i++)
            {
                var tablet=stage.Tablets[i];
                string status=tablet.AtStand?"Available":tablet==stage.Held?"Carrying":"Placed";
                var button=ParchmentChoice("Scroll Choice "+tablet.Data.id,catalogueContent,tablet.Data.expression+"\n"+status,()=>SelectScroll(tablet));
                var rect=(RectTransform)button.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);
                rect.anchoredPosition=new Vector2(0,-i*row);rect.sizeDelta=new Vector2(0,row-12);
                var text=button.GetComponentInChildren<TMP_Text>();if(stage.readingFont!=null)text.font=stage.readingFont;
                text.enableAutoSizing=true;text.fontSizeMin=28;text.fontSizeMax=34;text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;
            }
            ShowCatalogue();collection.gameObject.SetActive(true);collection.SetAsLastSibling();
        }
        public void SelectScroll(InductionTablet tablet)
        {
            if(!stage.BrowsingScrolls||tablet==null||!System.Linq.Enumerable.Contains(stage.Tablets,tablet))return;
            selectedScroll=tablet;selectedFormula.text=tablet.Data.expression;
            availability.text=stage.State!=InductionState.Active?"Activate the Book Statue before collecting.":!tablet.AtStand?"This scroll is already carried or placed. Remove it from the altar to rearrange it.":stage.Held!=null?"Place or return the scroll you are carrying first.":"Collect this exact scroll and place it at an active altar.";
            GetScrollButton.interactable=stage.CanCollect(tablet);catalogueHint.gameObject.SetActive(false);catalogue.gameObject.SetActive(false);details.gameObject.SetActive(true);
        }
        void ShowCatalogue(){selectedScroll=null;catalogueHint.gameObject.SetActive(true);catalogue.gameObject.SetActive(true);details.gameObject.SetActive(false);}
        public void ShowHelp(bool show)
        {
            if(show)HideControls();statusHud.gameObject.SetActive(!show);
            announcement.gameObject.SetActive(!show&&!string.IsNullOrEmpty(announcementText.text)&&announcementEnd<0);
        }
        public void ShowControlsTutorial(bool show)
        {
            if(statusHud!=null)statusHud.gameObject.SetActive(!show);
            if(announcement!=null)announcement.gameObject.SetActive(!show&&announcementText!=null&&!string.IsNullOrEmpty(announcementText.text)&&announcementEnd<0);
        }
        public void ShowBoardView(bool show)
        {
            if(show)HideControls();BoardBackButton.gameObject.SetActive(show);statusHud.gameObject.SetActive(!show);
            if(show)BoardBackButton.transform.SetAsLastSibling();
            announcement.gameObject.SetActive(!show&&!string.IsNullOrEmpty(announcementText.text)&&announcementEnd<0);
        }
        public void Refresh()
        {
            if(stage.onboarding!=null&&stage.onboarding.IsActive){RefreshPractice();return;}
            int seconds=Mathf.CeilToInt(stage.timer.Remaining);clock.text=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
            progress.text="Challenge "+(stage.ChallengeIndex+1)+" of 5\n"+(stage.State==InductionState.Active?stage.Challenge.title:stage.State==InductionState.WaitingForBook?"Waiting for book":stage.State==InductionState.StageComplete?"All completed":"Game Over");
            InteractButton.gameObject.SetActive(stage.CanAct&&stage.Target!=null&&stage.Target!=stage.book);
            InteractButton.GetComponentInChildren<TMP_Text>().text=stage.TargetAction;
            ViewBoardButton.gameObject.SetActive(stage.boardView.Available);
            SubmitButton.gameObject.SetActive(stage.CanSubmit);SubmitButton.interactable=stage.CanSubmit;
            ReturnButton.gameObject.SetActive(stage.CanAct&&stage.Held!=null);
            carry.text=stage.Reading||stage.Held==null?"":"Carrying: "+stage.Held.Data.expression;
            feedback.gameObject.SetActive(Time.unscaledTime<feedbackEnd&&!stage.Reading);
            if(announcement.gameObject.activeSelf&&announcementEnd>=0&&Time.unscaledTime>=announcementEnd)announcement.gameObject.SetActive(false);
        }
        public void SetFeedback(bool correct)
        {feedback.text=stage.Feedback;feedback.color=correct?new Color(.3f,1,.5f):new Color(1,.4f,.35f);feedbackEnd=Time.unscaledTime+5;}
        public void Announce(string text,float duration)
        {announcementText.text=text;announcementEnd=duration<0?-1:Time.unscaledTime+duration;announcement.gameObject.SetActive(true);}
        public void OpenReader()
        {
            HideControls();if(collection!=null)collection.gameObject.SetActive(false);
            reader.gameObject.SetActive(true);reader.SetAsLastSibling();
            readerTitle.text="CHALLENGE "+(stage.ChallengeIndex+1)+" — "+stage.Challenge.title;
            readerBody.text=stage.State==InductionState.WaitingForBook?"The Induction Tower — Stairway of Infinity\n\nInteract with the Book Statue to activate this challenge. Closing this panel does not start the countdown.":stage.Challenge.theorem+"\n\n"+stage.Challenge.instructions;
            readerBody.rectTransform.anchoredPosition=Vector2.zero;
        }
        public void CloseReader()
        {
            if(reader!=null)reader.gameObject.SetActive(false);if(collection!=null)collection.gameObject.SetActive(false);
            if(controls!=null&&controlsHidden)controls.SetActive(controlsPreviouslyActive);controlsHidden=false;
        }
        bool PracticeAction(string action)=>stage.onboarding!=null&&stage.onboarding.IsActive&&stage.onboarding.HandlePracticeAction(action);
        void RefreshPractice()
        {
            var step=stage.onboarding.CurrentStep;
            statusHud.gameObject.SetActive(step==InductionTutorialStep.Timer||step==InductionTutorialStep.Progress);
            int seconds=Mathf.CeilToInt(stage.startingSeconds);clock.text=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");progress.text="Challenge 1 of 5\nPractice preview";
            InteractButton.gameObject.SetActive(step==InductionTutorialStep.Stand||step==InductionTutorialStep.Place||step==InductionTutorialStep.Replace);
            InteractButton.GetComponentInChildren<TMP_Text>().text=step==InductionTutorialStep.Stand?"Formula Scrolls":step==InductionTutorialStep.Replace?"Replace Scroll":"Place Scroll";
            ReturnButton.gameObject.SetActive(step==InductionTutorialStep.Rearrange);
            SubmitButton.gameObject.SetActive(step==InductionTutorialStep.Submit);SubmitButton.interactable=true;
            ViewBoardButton.gameObject.SetActive(step==InductionTutorialStep.ViewBoard);
            BoardBackButton.gameObject.SetActive(step==InductionTutorialStep.Back);
            carry.text="";feedback.gameObject.SetActive(false);announcement.gameObject.SetActive(false);
        }
        public void OpenPracticeCollection()
        {
            PracticeCatalogueOpen=true;HideControls();selectedScroll=null;
            foreach(Transform child in catalogueContent){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            ShowCatalogue();collection.gameObject.SetActive(true);collection.SetAsLastSibling();Canvas.ForceUpdateCanvases();
            collectionTitle.text="FORMULA SCROLLS — PRACTICE";
            catalogueHint.text="Select a practice scroll. Real challenge scrolls are hidden.";
            float height=Mathf.Max(180,((RectTransform)catalogueContent.parent).rect.height/2);
            catalogueContent.sizeDelta=new Vector2(0,height);catalogueContent.anchoredPosition=Vector2.zero;
            PracticeChoice=ParchmentChoice("Practice Scroll Choice",catalogueContent,"PRACTICE SCROLL\nStatement P(n)",()=>stage.onboarding.HandlePracticeAction("select"));
            var rect=(RectTransform)PracticeChoice.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,height-12);rect.anchoredPosition=Vector2.zero;
        }
        public void SelectPracticeScroll()
        {
            selectedFormula.text="PRACTICE SCROLL\nStatement P(n)";availability.text="Practice only. This scroll does not enter your real inventory.";
            GetScrollButton.interactable=true;catalogueHint.gameObject.SetActive(false);catalogue.gameObject.SetActive(false);details.gameObject.SetActive(true);
        }
        public void PracticeShowCatalogue(){ShowCatalogue();}
        public void ClosePracticeCollection()
        {
            PracticeCatalogueOpen=false;CloseReader();catalogueHint.text="Select a scroll. Swipe to browse the list.";
            if(PracticeChoice!=null){PracticeChoice.gameObject.SetActive(false);Destroy(PracticeChoice.gameObject);}PracticeChoice=null;
        }
        public void ShowResult(bool win)
        {
            announcement.gameObject.SetActive(false);controls.SetActive(false);Result.Root.gameObject.SetActive(true);Result.Root.SetAsLastSibling();
            Result.Stage.text=TrialCatalog.ForScene("Induction").Title;Result.Heading.text=win?"STAGE COMPLETE!":"GAME OVER";
            int elapsed=Mathf.FloorToInt(stage.timer.ActiveSeconds);
            Result.Summary.text="Completion time: "+(elapsed/60).ToString("00")+":"+(elapsed%60).ToString("00")+"\nCorrect submissions: "+stage.Correct+" / 5    Mistakes: "+stage.Mistakes;
            bool hasNext=win&&!string.IsNullOrEmpty(stage.nextScene)&&UnityEngine.Application.CanStreamedLevelBeLoaded(stage.nextScene);
            Result.Primary.GetComponentInChildren<TMP_Text>().text=hasNext?"Play Next Stage":"Try Again";
            Result.Menu.GetComponentInChildren<TMP_Text>().text="Go to Menu";
            Result.Primary.onClick.RemoveAllListeners();Result.Primary.onClick.AddListener(()=>{if(hasNext)stage.Navigate(stage.nextScene);else stage.Restart();});Result.Extra.gameObject.SetActive(false);
        }
        static void Place(RectTransform rect,float x,float y,float X,float Y)=>StageUITheme.Stretch(rect,new Vector2(x,y),new Vector2(X,Y));
        static RectTransform Panel(string name,Transform parent,Vector2 min,Vector2 max)
        {var rect=StageUITheme.Rect(name,parent);StageUITheme.Stretch(rect,min,max);StageUITheme.CompactSurface(rect.gameObject.AddComponent<Image>());return rect;}
        Button ParchmentChoice(string name,Transform parent,string value,Action action)
        {
            var rect=StageUITheme.Rect(name,parent);
            var image=rect.gameObject.AddComponent<Image>();image.sprite=ScrollRibbon();image.type=Image.Type.Sliced;image.color=new Color(1,.96f,.9f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(1,.96f,.82f);colors.pressedColor=new Color(.82f,.70f,.46f);colors.selectedColor=new Color(1,.96f,.82f);button.colors=colors;
            var label=Text("Label",rect,value,34,true);Place(label.rectTransform,.075f,.15f,.925f,.80f);label.color=StageUITheme.ReadingInk;
            button.onClick.AddListener(()=>action());return button;
        }
        Sprite ScrollRibbon()
        {
            if(scrollRibbon!=null)return scrollRibbon;
            var paper=StageUITheme.Paper;var area=paper.rect;float height=paper.border.w;
            scrollRibbon=Sprite.Create(paper.texture,new Rect(area.x,area.yMax-height,area.width,height),new Vector2(.5f,.5f),paper.pixelsPerUnit,0,SpriteMeshType.FullRect,new Vector4(paper.border.x,0,paper.border.z,0));
            scrollRibbon.name="Induction Rolled Scroll Choice";return scrollRibbon;
        }
        void OnDestroy(){if(scrollRibbon!=null)Destroy(scrollRibbon);}
        TMP_Text Text(string name,Transform parent,string text,float size,bool reading=false)
        {
            var label=StageUITheme.Text(name,parent,text,size,reading);label.enableAutoSizing=true;label.fontSizeMin=24;label.fontSizeMax=size;
            if (reading && stage.readingFont != null) label.font = stage.readingFont;
            return label;
        }
        static Button Button(string name,Transform parent,string text,Action action)=>StageUITheme.Button(name,parent,text,()=>action());
        static Button Button(string name,Transform parent,string text,Func<bool> action)=>StageUITheme.Button(name,parent,text,()=>action());
    }
}
