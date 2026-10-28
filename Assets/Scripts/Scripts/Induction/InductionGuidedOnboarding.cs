using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Induction
{
    // Presentation and practice actions share the existing controls tutorial's lifetime.
    // No gameplay tablets, slots, challenge data, or clock are modified here.
    public sealed partial class InductionControlsOnboarding
    {
        [Header("Guided tutorial camera")]
        [Min(.1f)] public float cameraTransitionSeconds = .9f;
        [Range(25,75)] public float tutorialFieldOfView = 48;
        [Min(1)] public float framingPadding = 1.25f;
        [Min(1)] public float closeupPadding = 1.75f;
        [Range(.2f,.5f)] public float closeupHorizontalPosition = .35f;
        public Vector3 bookViewDirection = new Vector3(0,.4f,-1);
        public Vector3 standViewDirection = new Vector3(0,.15f,-1);
        public Vector3 altarViewDirection = new Vector3(0,.25f,-1);
        public Button NextButton { get; private set; }
        public PrelimTutorialSkipControl SkipControl => skip;
        public bool CameraTransitioning { get; private set; }
        PrelimTutorialSkipControl skip;
        Vector3 movementStart;
        RectTransform uiTarget;
        Renderer[] worldTargets;
        Camera guideCamera;
        Vector3 savedCameraPosition, cameraStart, cameraGoal;
        Quaternion savedCameraRotation, cameraStartRotation, cameraGoalRotation;
        float savedFov, cameraStartFov, cameraGoalFov, cameraElapsed;
        string[] boardText;
        readonly List<Behaviour> cameraBehaviours = new List<Behaviour>();
        readonly Dictionary<Renderer,bool> playerRenderers = new Dictionary<Renderer,bool>();
        readonly Dictionary<Renderer,bool> chamberRenderers = new Dictionary<Renderer,bool>();
        GameObject demonstrationScroll, altarPointer;
        TMP_Text demonstrationLabel, pointerLabel;
        bool practiceCollected, returningCamera;
        Renderer[] framedModels;
        Vector3 framedDirection;
        float framedFov, framedAspect;
        bool framedReserveSide;

        void BuildGuide()
        {
            NextButton=Choice(instructions,"Next",new Vector2(.55f,0),Vector2.one,Next);
            skip=PrelimTutorialSkipControl.Create(safeArea,SkipTutorial);
            skip.Reserve(stage.ui.TimerTarget.parent as RectTransform);
        }
        void StartGuide()
        {
            // Movement is suppressed by the input override; hand callbacks remain available.
            player.ToggleControl(false);input.SetGameplayInputBlocked(false);
            guideCamera=Camera.main;
            if(guideCamera!=null)
            {
                savedCameraPosition=guideCamera.transform.position;savedCameraRotation=guideCamera.transform.rotation;savedFov=guideCamera.fieldOfView;
                foreach(var behaviour in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
                {
                    string name=behaviour.GetType().Name;
                    if(behaviour.enabled&&(behaviour is CinemachineBrain||name=="CameraTargetController"||name=="CinemachineInputAxisController"||name=="CinemachinePinchZoom"||name=="ThirdPersonCameraController"))
                    {cameraBehaviours.Add(behaviour);behaviour.enabled=false;}
                }
            }
            foreach(var renderer in player.GetComponentsInChildren<Renderer>()){playerRenderers[renderer]=renderer.enabled;renderer.enabled=false;}
            // Keep suspended chamber art out of close-ups without changing collisions or lighting.
            foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                bool ceiling=renderer.name=="ChamberCeiling",suspended=false;
                for(var part=renderer.transform;part!=null;part=part.parent)
                    if(part.name=="Overhead Stone Rib"||part.name=="Suspension Chain"||part.name=="Hanging Chandelier")
                        suspended=part.GetComponentsInParent<Transform>(true).Any(t=>t.name=="ChamberDecorations");
                if(ceiling||suspended){chamberRenderers[renderer]=renderer.enabled;renderer.enabled=false;}
            }
            boardText=new[]{stage.boardTitle.text,stage.boardFormula.text,stage.boardInstructions.text};
            stage.boardTitle.text="PRACTICE CHALLENGE";stage.boardFormula.text="Practice statement P(n)";
            stage.boardInstructions.text="Build a proof using a base case, an assumption for k, and a step for k + 1.\nPractice only — no real question or answer.";
            BookStep();
        }
        Renderer[] Models(params Transform[] subjects)=>subjects.Where(t=>t!=null).SelectMany(t=>t.GetComponentsInChildren<Renderer>()).Where(r=>r.enabled&&r.GetComponent<TMP_Text>()==null).ToArray();
        Bounds SubjectBounds(Renderer[] models)
        {
            var bounds=models.Length>0?models[0].bounds:new Bounds(stage.book.position+Vector3.up,Vector3.one);
            foreach(var model in models)bounds.Encapsulate(model.bounds);
            return bounds;
        }
        void Frame(Renderer[] models,Vector3 direction,float fov,bool reserveSide=true)
        {
            if(guideCamera==null)return;
            framedModels=models;framedDirection=direction;framedFov=fov;framedReserveSide=reserveSide;framedAspect=guideCamera.aspect;
            var bounds=SubjectBounds(models);direction.Normalize();var rotation=Quaternion.LookRotation(-direction,Vector3.up);
            Vector3 up=rotation*Vector3.up,right=rotation*Vector3.right;
            float height=Vector3.Dot(bounds.extents,new Vector3(Mathf.Abs(up.x),Mathf.Abs(up.y),Mathf.Abs(up.z)));
            float width=Vector3.Dot(bounds.extents,new Vector3(Mathf.Abs(right.x),Mathf.Abs(right.y),Mathf.Abs(right.z)));
            float depth=Vector3.Dot(bounds.extents,new Vector3(Mathf.Abs(direction.x),Mathf.Abs(direction.y),Mathf.Abs(direction.z)));
            float distance=Mathf.Max(height,width/Mathf.Max(.3f,guideCamera.aspect))/Mathf.Tan(fov*Mathf.Deg2Rad*.5f)*(reserveSide?closeupPadding:framingPadding)+depth;
            Vector3 offset=reserveSide?right*((1-2*closeupHorizontalPosition)*distance*Mathf.Tan(fov*Mathf.Deg2Rad*.5f)*guideCamera.aspect):Vector3.zero;
            CameraTo(bounds.center+direction*Mathf.Max(3,distance)+offset,rotation,fov);
        }
        void CameraTo(Vector3 position,Quaternion rotation,float fov)
        {
            if(guideCamera==null)return;
            cameraStart=guideCamera.transform.position;cameraStartRotation=guideCamera.transform.rotation;cameraStartFov=guideCamera.fieldOfView;
            cameraGoal=position;cameraGoalRotation=rotation;cameraGoalFov=fov;cameraElapsed=0;CameraTransitioning=true;
        }
        void BookStep()
        {
            var models=Models(stage.book);Frame(models,bookViewDirection,tutorialFieldOfView);
            GuideStep(InductionTutorialStep.Book,"Interact with the book statue to start each challenge. Tap the hand button. This is practice; the real countdown stays paused.",hand,models,false);
        }
        void GuideStep(InductionTutorialStep step,string text,RectTransform target,Renderer[] models,bool next)
        {
            CurrentStep=step;instruction.text=text;uiTarget=target;worldTargets=models;
            foreach(var control in visibility.Keys)control.SetActive(false);
            hand.gameObject.SetActive(step==InductionTutorialStep.Book||step==InductionTutorialStep.Stand||step==InductionTutorialStep.Place||step==InductionTutorialStep.Replace);
            instructions.gameObject.SetActive(true);NextButton.gameObject.SetActive(next);
            stage.ui.Refresh();
            if(step==InductionTutorialStep.SelectScroll)AttachHighlight(worldObstacle);
            else if(target!=null)AttachHighlight(target);
            else if(models!=null&&models.Length>0)AttachHighlight(worldObstacle);
            else highlight.gameObject.SetActive(false);
            instructions.SetAsLastSibling();skip.Show(true);
        }
        void AttachHighlight(RectTransform target)
        {
            highlight.SetParent(target,false);StageUITheme.Stretch(highlight,Vector2.zero,Vector2.one);
            highlight.offsetMin=-Vector2.one*8;highlight.offsetMax=Vector2.one*8;highlight.gameObject.SetActive(true);
            arrow.anchorMin=arrow.anchorMax=new Vector2(.5f,1);arrow.sizeDelta=new Vector2(64,48);arrow.anchoredPosition=new Vector2(0,32);arrow.localRotation=Quaternion.identity;
        }
        public void Next()
        {
            if(!IsActive||CameraTransitioning||!NextButton.gameObject.activeSelf)return;
            switch(CurrentStep)
            {
                case InductionTutorialStep.Timer:
                    GuideStep(InductionTutorialStep.Progress,"This indicator shows your current challenge and progress through the five challenges.",stage.ui.ProgressTarget,null,true);break;
                case InductionTutorialStep.Progress: StandStep();break;
                case InductionTutorialStep.BaseCase:
                    GuideStep(InductionTutorialStep.Hypothesis,"Inductive Hypothesis: assume the statement holds for an arbitrary integer k.",null,Models(stage.stations[1]),true);break;
                case InductionTutorialStep.Hypothesis:
                    GuideStep(InductionTutorialStep.InductiveStep,"Inductive Step: use that assumption to prove the statement for k + 1.",null,Models(stage.stations[2]),true);break;
                case InductionTutorialStep.InductiveStep:
                    CreateAltarPointer();GuideStep(InductionTutorialStep.AltarArrow,"The animated arrow identifies the altar required for the current challenge. Scrolls are placed above its surface. Challenge 5 needs all three altars.",null,Models(stage.stations[0]),true);break;
                case InductionTutorialStep.AltarArrow:
                    GuideStep(InductionTutorialStep.Place,"Select a scroll at the stand, then approach the indicated altar. Use the hand or Place Scroll button to place it above the surface. Try placing the practice scroll.",(RectTransform)stage.ui.InteractButton.transform,null,false);break;
                case InductionTutorialStep.Replace:
                    GuideStep(InductionTutorialStep.Rearrange,"Use the hand or context button to remove a placed scroll.\nPlace it at another anchor to rearrange. Return Scroll restores it to the stand.",(RectTransform)stage.ui.ReturnButton.transform,null,true);break;
                case InductionTutorialStep.Rearrange:
                    GuideStep(InductionTutorialStep.Submit,"Submit checks every required slot.\nWrong: −10s; placements stay. Correct: time pauses.\nTap this practice Submit.",(RectTransform)stage.ui.SubmitButton.transform,null,false);break;
                case InductionTutorialStep.Board:
                    GuideStep(InductionTutorialStep.Back,"Tap Back to return to gameplay. Viewing or closing the board does not start a challenge or change its timer.",(RectTransform)stage.ui.BoardBackButton.transform,null,false);break;
            }
        }
        void StandStep()
        {
            var models=Models(stage.scrollStand);Frame(models,standViewDirection,tutorialFieldOfView);
            GuideStep(InductionTutorialStep.Stand,"The Formula Scrolls stand contains statements used to construct your proof. Open it using the hand or Formula Scrolls button to choose a practice scroll.",null,models,false);
        }
        public bool GuidedInteraction(bool dedicated)
        {
            if(!IsActive||CameraTransitioning)return false;
            // In this guided camera view the highlighted object is the practice target.
            // Ordinary range/targeting and inventory interactions never run.
            switch(CurrentStep)
            {
                case InductionTutorialStep.Book:
                    GuideStep(InductionTutorialStep.Timer,"This is the shared timer. The book starts or resumes it. Completing a challenge pauses it; the remaining time does not reset between challenges.",stage.ui.TimerTarget,null,true);return true;
                case InductionTutorialStep.Stand:
                    stage.ui.OpenPracticeCollection();GuideStep(InductionTutorialStep.SelectScroll,"Select the practice scroll.",(RectTransform)stage.ui.PracticeChoice.transform,null,false);
                    instructions.gameObject.SetActive(false);return true;
                case InductionTutorialStep.Place:
                    if(!practiceCollected)return false;
                    PlaceDemoScroll();GuideStep(InductionTutorialStep.Replace,"Replace Scroll swaps an occupied anchor.\nYou carry its old scroll afterward. No scroll is lost.",(RectTransform)stage.ui.InteractButton.transform,null,true);return true;
                case InductionTutorialStep.Replace: return true; // Explanation, no real swap.
            }
            return false;
        }
        public bool HandlePracticeAction(string action)
        {
            if(!IsActive)return false;
            if(CameraTransitioning)return true;
            if(action=="select"&&CurrentStep==InductionTutorialStep.SelectScroll)
            {
                stage.ui.SelectPracticeScroll();GuideStep(InductionTutorialStep.GetScroll,"Get the practice scroll.",(RectTransform)stage.ui.GetScrollButton.transform,null,false);instructions.gameObject.SetActive(false);
            }
            else if(action=="get"&&CurrentStep==InductionTutorialStep.GetScroll)
            {
                practiceCollected=true;stage.ui.ClosePracticeCollection();
                Frame(Models(stage.stations),altarViewDirection,58,false);
                GuideStep(InductionTutorialStep.BaseCase,"Base Case: establish the statement for its starting value. Your practice scroll is separate from gameplay inventory.",null,Models(stage.stations[0]),true);
            }
            else if(action=="close"&&stage.ui.PracticeCatalogueOpen){highlight.SetParent(safeArea,false);stage.ui.ClosePracticeCollection();StandStep();}
            else if(action=="return"&&CurrentStep==InductionTutorialStep.Rearrange){if(demonstrationScroll!=null)demonstrationScroll.SetActive(false);}
            else if(action=="submit"&&CurrentStep==InductionTutorialStep.Submit)
            {
                GuideStep(InductionTutorialStep.ViewBoard,"Practice checked — no score or penalty applied. Near the altars, tap View Board to read the objective. Try View Board now.",(RectTransform)stage.ui.ViewBoardButton.transform,null,false);
            }
            else if(action=="board"&&CurrentStep==InductionTutorialStep.ViewBoard)
            {
                var frame=stage.informationBoard.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name=="Info_GoldFrame");
                var models=frame!=null?new[]{frame}:Models(stage.informationBoard);
                Frame(models,stage.informationBoard.forward,stage.boardView.boardFieldOfView);
                GuideStep(InductionTutorialStep.Board,"This board displays the current challenge, mathematical statement, and instructions. This preview uses a clearly labeled practice statement.",null,models,true);
            }
            else if(action=="back"&&CurrentStep==InductionTutorialStep.Back)
            {
                if(guideCamera==null)ShowConfirmation();
                else {returningCamera=true;CameraTo(savedCameraPosition,savedCameraRotation,savedFov);}
            }
            return true;
        }
        public void PracticeCatalogueBack(bool details)
        {
            if(!IsActive)return;
            if(details){stage.ui.PracticeShowCatalogue();GuideStep(InductionTutorialStep.SelectScroll,"Select a practice scroll.",(RectTransform)stage.ui.PracticeChoice.transform,null,false);instructions.gameObject.SetActive(false);}
            else HandlePracticeAction("close");
        }
        void PlaceDemoScroll()
        {
            demonstrationScroll=GameObject.CreatePrimitive(PrimitiveType.Cube);demonstrationScroll.name="Induction Demonstration Scroll";demonstrationScroll.layer=2;
            demonstrationScroll.transform.position=stage.slots.First(s=>s.key.StartsWith("Base:")).transform.position+Vector3.up*1.1f;
            demonstrationScroll.transform.localScale=new Vector3(2,.12f,1.3f);demonstrationScroll.GetComponent<Collider>().isTrigger=true;
            practiceMaterial=new Material(stage.altarIndicators.arrowShader);practiceMaterial.SetColor("_BaseColor",new Color(1,.87f,.57f));demonstrationScroll.GetComponent<Renderer>().sharedMaterial=practiceMaterial;
            var label=new GameObject("Practice Statement Label");label.transform.SetParent(demonstrationScroll.transform,false);
            label.transform.localScale=new Vector3(.5f,1/.12f,1/1.3f);label.transform.position=demonstrationScroll.transform.position+Vector3.up*.7f;
            demonstrationLabel=label.AddComponent<TextMeshPro>();demonstrationLabel.font=stage.readingFont;demonstrationLabel.fontSize=3;demonstrationLabel.text="PRACTICE: P(n)";demonstrationLabel.alignment=TextAlignmentOptions.Center;demonstrationLabel.rectTransform.sizeDelta=new Vector2(4,1);demonstrationLabel.color=Color.white;
        }
        void CreateAltarPointer()
        {
            altarPointer=new GameObject("Induction Practice Altar Arrow");pointerLabel=altarPointer.AddComponent<TextMeshPro>();pointerLabel.text="▼";pointerLabel.fontSize=8;pointerLabel.alignment=TextAlignmentOptions.Center;pointerLabel.color=new Color(1,.86f,.22f);pointerLabel.rectTransform.sizeDelta=new Vector2(2,2);
        }
        void LayoutGuide()
        {
            if(guideCamera!=null&&!returningCamera&&framedModels!=null&&!Mathf.Approximately(framedAspect,guideCamera.aspect))
                Frame(framedModels,framedDirection,framedFov,framedReserveSide);
            if(guideCamera!=null&&CameraTransitioning)
            {
                cameraElapsed+=Time.unscaledDeltaTime;float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(cameraElapsed/cameraTransitionSeconds));
                guideCamera.transform.SetPositionAndRotation(Vector3.Lerp(cameraStart,cameraGoal,t),Quaternion.Slerp(cameraStartRotation,cameraGoalRotation,t));guideCamera.fieldOfView=Mathf.Lerp(cameraStartFov,cameraGoalFov,t);
                if(t>=1){CameraTransitioning=false;if(returningCamera){ShowConfirmation();return;}}
            }
            NextButton.interactable=!CameraTransitioning;
            if(demonstrationLabel!=null&&guideCamera!=null)demonstrationLabel.transform.rotation=guideCamera.transform.rotation;
            if(altarPointer!=null&&guideCamera!=null)
            {
                var bounds=SubjectBounds(Models(stage.stations[0]));altarPointer.transform.position=new Vector3(bounds.center.x,bounds.max.y+3+Mathf.Sin(Time.unscaledTime*2)*.2f,bounds.center.z);
                altarPointer.transform.rotation=guideCamera.transform.rotation;
            }
            if(CurrentStep==InductionTutorialStep.SelectScroll&&uiTarget!=null)
            {
                var corners=new Vector3[4];uiTarget.GetWorldCorners(corners);
                Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=-min;
                foreach(var corner in corners){Vector2 point=safeArea.InverseTransformPoint(corner);min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
                worldObstacle.gameObject.SetActive(true);worldObstacle.anchoredPosition=(min+max)*.5f;worldObstacle.sizeDelta=max-min;
                arrow.anchorMin=arrow.anchorMax=new Vector2(1,.5f);arrow.anchoredPosition=new Vector2(32,0);arrow.localRotation=Quaternion.Euler(0,0,-90);
            }
            else if(worldTargets!=null&&worldTargets.Length>0)ProjectBounds(SubjectBounds(worldTargets));else worldObstacle.gameObject.SetActive(false);
            var target=uiTarget!=null?uiTarget:worldObstacle.gameObject.activeSelf?worldObstacle:null;
            var blockers=new List<RectTransform>(ControlTutorialLayout.FindBlockers());blockers.Add((RectTransform)skip.transform);blockers.Add(worldObstacle);
            blockers.Add(stage.ui.TimerTarget);blockers.Add(stage.ui.ProgressTarget);
            foreach(var button in new[]{stage.ui.InteractButton,stage.ui.ViewBoardButton,stage.ui.ReturnButton,stage.ui.SubmitButton,stage.ui.BoardBackButton})blockers.Add((RectTransform)button.transform);
            ControlTutorialLayout.Place(instructions,instruction,(RectTransform)NextButton.transform,safeArea,target,true,blockers.ToArray(),arrow);
        }
        void ProjectBounds(Bounds bounds)
        {
            if(guideCamera==null)return;
            Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=-min;bool found=false;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var screen=guideCamera.WorldToScreenPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)));if(screen.z<=0)continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea,screen,null,out Vector2 local);min=Vector2.Min(min,local);max=Vector2.Max(max,local);found=true;
            }
            min=Vector2.Max(min,safeArea.rect.min+Vector2.one*20);max=Vector2.Min(max,safeArea.rect.max-Vector2.one*20);
            worldObstacle.gameObject.SetActive(found);if(found){worldObstacle.anchoredPosition=(min+max)*.5f;worldObstacle.sizeDelta=Vector2.Max(Vector2.one,max-min);}
        }
        public void SkipTutorial(){if(IsActive&&CurrentStep!=InductionTutorialStep.Confirm)ShowConfirmation();}
        void ShowConfirmation()
        {
            ResetGuide();mobile.ResetJoystick();CurrentStep=InductionTutorialStep.Confirm;stage.ui.Refresh();
            instructions.gameObject.SetActive(false);highlight.gameObject.SetActive(false);worldObstacle.gameObject.SetActive(false);
            foreach(var control in visibility.Keys)control.SetActive(false);input.SetGameplayInputBlocked(true);player.ToggleControl(false);Time.timeScale=0;
            skip.Show(false);confirmation.SetActive(true);confirmation.transform.SetAsLastSibling();
        }
        void ResetGuide()
        {
            CameraTransitioning=false;returningCamera=false;framedModels=null;
            // Detach before destroying any practice catalogue rows or objects.
            if(highlight!=null){highlight.SetParent(safeArea,false);highlight.gameObject.SetActive(false);}
            if(guideCamera!=null){guideCamera.transform.SetPositionAndRotation(savedCameraPosition,savedCameraRotation);guideCamera.fieldOfView=savedFov;}guideCamera=null;
            foreach(var behaviour in cameraBehaviours)if(behaviour!=null)behaviour.enabled=true;cameraBehaviours.Clear();
            foreach(var item in playerRenderers)if(item.Key!=null)item.Key.enabled=item.Value;playerRenderers.Clear();
            foreach(var item in chamberRenderers)if(item.Key!=null)item.Key.enabled=item.Value;chamberRenderers.Clear();
            if(boardText!=null&&stage!=null){stage.boardTitle.text=boardText[0];stage.boardFormula.text=boardText[1];stage.boardInstructions.text=boardText[2];}boardText=null;
            if(stage!=null&&stage.ui!=null)stage.ui.ClosePracticeCollection();
            if(demonstrationScroll!=null){demonstrationScroll.SetActive(false);Destroy(demonstrationScroll);}demonstrationScroll=null;demonstrationLabel=null;
            if(altarPointer!=null){altarPointer.SetActive(false);Destroy(altarPointer);}altarPointer=null;pointerLabel=null;
            if(practiceMaterial!=null)Destroy(practiceMaterial);practiceMaterial=null;practiceCollected=false;worldTargets=null;uiTarget=null;
            if(skip!=null)skip.Show(true);
        }
    }
}
