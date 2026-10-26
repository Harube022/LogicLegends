using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Graph
{
    public sealed partial class GraphUIController
    {
        RectTransform mapFrame,clockFrame,objectiveFrame,routeFrame,feedbackFrame,jumpControl;
        TMP_Text titleLabel;
        void BindLayout()
        {
            var scaler=host.GetComponentInParent<CanvasScaler>();
            if(scaler!=null){scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=1;}
            jumpControl=host.Find("Gameplay_Interface/JumpButton") as RectTransform;
            if(jumpControl!=null)Place(jumpControl,new Vector2(1,0),new Vector2(-60,80),new Vector2(200,200),new Vector2(1,0));
            mapFrame=TutorialTarget("Graph minimap");clockFrame=TutorialTarget("Clock");
            objectiveFrame=TutorialTarget("Objective");routeFrame=TutorialTarget("Recorded route");feedbackFrame=TutorialTarget("Feedback");
            titleLabel=clockFrame.Find("Clock title").GetComponent<TMP_Text>();
            foreach(var rect in new[]{objectiveFrame,feedbackFrame})
            {rect.GetComponent<Image>().color=Color.clear;foreach(var label in rect.GetComponentsInChildren<TMP_Text>())
                {label.color=Color.white;label.outlineColor=Color.black;label.outlineWidth=.15f;label.fontSizeMin=23;label.fontSizeMax=25;label.textWrappingMode=TextWrappingModes.Normal;}}
            // The route is part of the map, with its own footer outside the drawing.
            routeFrame.SetParent(mapFrame,false);routeFrame.GetComponent<Image>().color=Color.clear;
            route.alignment=TextAlignmentOptions.Center;route.color=StageUITheme.ReadingInk;
            route.fontSizeMin=20;route.fontSizeMax=22;route.textWrappingMode=TextWrappingModes.Normal;
            route.overflowMode=TextOverflowModes.Overflow;
            mapFrame.Find("Map title").GetComponent<TMP_Text>().text="GRAPH MAP";
            penalty.text="−10s";penalty.fontSizeMin=28;penalty.fontSizeMax=32;
            LayoutGameplay();
        }
        void LayoutGameplay()
        {
            if(clockFrame==null)return;
            float shorter=Mathf.Min(host.rect.width,host.rect.height);
            float width=Mathf.Min(Mathf.Clamp(shorter*.275f,180,312)*.85f,shorter-48);
            float footer=Mathf.Max(36,route.GetPreferredValues(route.text,width-24,0).y+8);
            if(mapFrame.parent==root)Place(mapFrame,Vector2.one,new Vector2(-24,-24),new Vector2(width,width+footer-12),Vector2.one);
            StageUITheme.Stretch(map,Vector2.zero,Vector2.one);map.offsetMin=new Vector2(20,footer+12);map.offsetMax=new Vector2(-20,-40);
            StageUITheme.Stretch(routeFrame,new Vector2(0,0),new Vector2(1,0));routeFrame.pivot=new Vector2(.5f,0);
            routeFrame.offsetMin=new Vector2(12,10);routeFrame.offsetMax=new Vector2(-12,10+footer);Inset(route.rectTransform,0,0);
            float mapHeight=mapFrame.parent==root?mapFrame.rect.height:width+footer-12;
            Place(clockFrame,Vector2.one,new Vector2(-24,-mapHeight-36),new Vector2(width,148),Vector2.one);
            titleLabel.text=TrialCatalog.ForScene("Graph").Title;titleLabel.fontSizeMin=22;titleLabel.fontSizeMax=24;
            StageUITheme.Stretch(titleLabel.rectTransform,new Vector2(.07f,.70f),new Vector2(.93f,.91f));
            StageUITheme.Stretch(clock.rectTransform,new Vector2(.06f,.19f),new Vector2(.36f,.60f));clock.fontSize=32;
            StageUITheme.Stretch(trialProgress.rectTransform,new Vector2(.40f,.13f),new Vector2(.93f,.62f));
            trialProgress.fontSizeMin=20;trialProgress.fontSizeMax=23;trialProgress.textWrappingMode=TextWrappingModes.NoWrap;
            bool degree=stage.Challenge.type==GraphChallengeType.Degree && stage.GraphRevealed || stage.TutorialActive&&stage.controlsTutorial.CurrentStep==GraphTutorialStep.Degree;
            float y=-mapHeight-196;
            if(degree)
            {
                // Animate in screen space; only the requested entrance moves the question.
                LayoutDegreeQuestion();
                Inset(objective.rectTransform,8,6);objective.fontSizeMin=30;objective.fontSizeMax=36;objective.alignment=TextAlignmentOptions.Center;
            }
            else {objective.fontSizeMin=23;objective.fontSizeMax=25;Stack(objectiveFrame,objective,width,ref y);}
            Stack(feedbackFrame,feedback,width,ref y);
            if(jumpControl!=null)jumpControl.localScale=Vector3.one;
            var mapTitle=mapFrame.Find("Map title").GetComponent<TMP_Text>();
            mapTitle.fontSizeMin=21;mapTitle.fontSizeMax=23;mapTitle.rectTransform.sizeDelta=new Vector2(width-32,28);
            // Beside the countdown, independent of wrapped feedback below the HUD.
            Place(penalty.rectTransform,Vector2.one,new Vector2(-width-30,-mapHeight-120),new Vector2(82,48),new Vector2(1,.5f));
            penalty.color=new Color(1,.16f,.12f,Mathf.Clamp01((penaltyUntil-Time.unscaledTime)/.6f));
            Place((RectTransform)confirm.transform,new Vector2(.5f,0),new Vector2(degree?-290:0,34),new Vector2(270,100),new Vector2(.5f,0));
            Place((RectTransform)retry.transform,new Vector2(.5f,0),new Vector2(0,degree?34:148),new Vector2(270,100),new Vector2(.5f,0));
            Place((RectTransform)choices.transform,new Vector2(.5f,0),new Vector2(0,150),new Vector2(840,110),new Vector2(.5f,0));
            for(int i=0;i<answerButtons.Count;i++)
            {
                Place((RectTransform)answerButtons[i].transform,new Vector2(0,.5f),new Vector2(60+i*120,0),new Vector2(108,104),new Vector2(.5f,.5f));
                var label=answerButtons[i].GetComponentInChildren<TMP_Text>();label.fontSizeMin=36;label.fontSizeMax=42;
            }
        }
        float degreeEntranceAt=-1;
        bool degreeQuestionPresented;
        public bool DegreeQuestionEntering => degreeEntranceAt >= 0 && Time.unscaledTime-degreeEntranceAt < 1.5f;
        public void BeginDegreeQuestionEntrance()
        {
            if(degreeQuestionPresented)return;
            degreeQuestionPresented=true;degreeEntranceAt=Time.unscaledTime;
        }
        public void CancelDegreeQuestionEntrance()
        {
            degreeEntranceAt=-1;degreeQuestionPresented=false;
            if(objectiveFrame!=null){objectiveFrame.localScale=Vector3.one;objectiveFrame.gameObject.SetActive(false);}
        }
        void LayoutDegreeQuestion()
        {
            float width=Mathf.Min(860,host.rect.width-600);
            var top=new Vector2(0,host.rect.height*.5f-68);
            Vector2 position=top;float scale=1;
            bool practice=stage.TutorialActive;
            if(!practice&&degreeEntranceAt>=0)
            {
                float elapsed=Time.unscaledTime-degreeEntranceAt;
                float travel=Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-.65f)/.85f));
                position=Vector2.Lerp(Vector2.zero,top,travel);
                if(elapsed<.25f)scale=Mathf.Lerp(.18f,1.06f,Mathf.SmoothStep(0,1,elapsed/.25f));
                else if(elapsed<.4f)scale=Mathf.Lerp(1.06f,1,(elapsed-.25f)/.15f);
            }
            Place(objectiveFrame,new Vector2(.5f,.5f),position,new Vector2(width,96),new Vector2(.5f,.5f));
            objectiveFrame.localScale=Vector3.one*scale;
            var opacity=objectiveFrame.GetComponent<CanvasGroup>();if(opacity==null)opacity=objectiveFrame.gameObject.AddComponent<CanvasGroup>();
            opacity.alpha=practice||degreeQuestionPresented?1:0;opacity.blocksRaycasts=false;opacity.interactable=false;
            if(!practice)objectiveFrame.SetAsLastSibling();
        }
        public void LayoutBoardButton(RectTransform rect)
        {
            bool degree=stage.Challenge.type==GraphChallengeType.Degree&&stage.GraphRevealed;
            Place(rect,new Vector2(.5f,0),new Vector2(degree?290:0,degree?34:stage.GraphRevealed?262:34),new Vector2(270,100),new Vector2(.5f,0));
            foreach(var button in new[]{confirm,retry,rect.GetComponent<Button>()})
            {
                button.image.pixelsPerUnitMultiplier=6;
                var label=button.GetComponentInChildren<TMP_Text>();label.fontSizeMin=24;label.fontSizeMax=30;
            }
        }
        static void Stack(RectTransform panel,TMP_Text text,float width,ref float y)
        {
            float height=string.IsNullOrEmpty(text.text)?0:text.GetPreferredValues(text.text,width-8,0).y+8;
            Place(panel,Vector2.one,new Vector2(-24,y),new Vector2(width,height),Vector2.one);Inset(text.rectTransform,4,4);y-=height+8;
        }
    }
}
