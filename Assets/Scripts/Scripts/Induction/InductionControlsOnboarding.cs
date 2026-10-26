using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Induction
{
    public enum InductionTutorialStep { Movement, Jump, Interaction, Book, Timer, Progress, Stand, SelectScroll, GetScroll, BaseCase, Hypothesis, InductiveStep, AltarArrow, Place, Replace, Rearrange, Submit, ViewBoard, Board, Back, Confirm }

    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed partial class InductionControlsOnboarding : MonoBehaviour
    {
        [Range(.15f,.8f)] public float joystickDeadZone = .3f;
        [Min(.1f)] public float movementDistance = 1.5f;
        [Min(.1f)] public float intentionalMovementSeconds = .7f;
        [Min(1)] public float interactionRange = 3.5f;
        public static InductionControlsOnboarding Active { get; private set; }
        public bool IsActive { get; private set; }
        public InductionTutorialStep CurrentStep { get; private set; }
        public bool AllowsJump => IsActive && CurrentStep == InductionTutorialStep.Jump;
        public GameObject PracticeObject => null;
        public RectTransform Highlight => highlight;
        public RectTransform InstructionPanel => instructions;
        public Button YesButton { get; private set; }
        public Button NoButton { get; private set; }
        InductionStageManager stage;
        GameInput input;
        Player player;
        MobileInputUI mobile;
        RectTransform safeArea, joystick, joystickRing, jump, hand, highlight, arrow, instructions, card, worldObstacle;
        TMP_Text instruction;
        GameObject confirmation, controls;
        Material practiceMaterial; // Material for the later proof-scroll demonstration.
        Button settings;
        bool settingsWasEnabled, inputWasBlocked, playerWasEnabled, controlsWereVisible, cursorWasVisible;
        CursorLockMode previousCursor;
        float previousTimeScale, movedDistance, jumpBaseY;
        bool jumpSucceeded;
        Vector3 originalPosition, lastPosition;
        Quaternion originalRotation;
        readonly Dictionary<GameObject,bool> visibility = new Dictionary<GameObject,bool>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){Active=null;}

        public bool Begin(InductionStageManager owner)
        {
            if(IsActive||owner.State!=InductionState.WaitingForBook||owner.ChallengeIndex!=0||!owner.help.OnboardingPending||!owner.CanAct)return false;
            stage=owner;input=GameInput.Instance;player=Player.LocalInstance;safeArea=owner.uiHost as RectTransform;
            controls=safeArea.Find("Gameplay_Interface").gameObject;
            var host=safeArea.Find("Gameplay_Interface/Joystick");mobile=host.GetComponentInChildren<MobileInputUI>(true);
            joystick=mobile!=null?mobile.transform as RectTransform:null;
            jump=safeArea.Find("Gameplay_Interface/JumpButton") as RectTransform;
            hand=safeArea.Find("Gameplay_Interface/InteractButton") as RectTransform;
            if(input==null||player==null||joystick==null||jump==null||hand==null)
            {Debug.LogError("Induction onboarding needs the existing joystick, jump and hand controls.",this);return false;}
            originalPosition=player.transform.position;originalRotation=player.transform.rotation;
            playerWasEnabled=player.enabled;inputWasBlocked=input.GameplayInputBlocked;previousTimeScale=Time.timeScale;
            cursorWasVisible=Cursor.visible;previousCursor=Cursor.lockState;controlsWereVisible=controls.activeSelf;
            foreach(var target in new[]{host.gameObject,jump.gameObject,hand.gameObject})visibility[target]=target.activeSelf;
            settings=safeArea.Find("Gameplay_Interface/SettingsButton")?.GetComponent<Button>();
            if(settings!=null){settingsWasEnabled=settings.interactable;settings.interactable=false;}
            controls.SetActive(true);BuildUI();Active=this;IsActive=true;player.OnJumpStarted+=OnSuccessfulJump;
            stage.ui.ShowControlsTutorial(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            BuildGuide();BeginMovement();return true;
        }
        void BuildUI()
        {
            // The visible joystick ring excludes the prefab's transparent touch padding.
            joystickRing=StageUITheme.Rect("Induction Visible Joystick Target",joystick);
            joystickRing.anchorMin=joystickRing.anchorMax=joystickRing.pivot=new Vector2(.5f,.5f);
            instructions=StageUITheme.Rect("Induction Control Instructions",safeArea);
            var backing=instructions.gameObject.AddComponent<Image>();backing.color=new Color(.06f,.13f,.19f,.95f);backing.raycastTarget=false;
            instruction=StageUITheme.Text("Instruction",instructions,"",36,true);instruction.color=Color.white;
            highlight=StageUITheme.Rect("Induction Control Highlight",safeArea);
            for(int i=0;i<4;i++)
            {
                var edge=StageUITheme.Rect("Highlight Edge",highlight);var image=edge.gameObject.AddComponent<Image>();
                image.color=new Color(1,.86f,.22f,.95f);image.raycastTarget=false;
                edge.anchorMin=i==0?new Vector2(0,1):i==3?new Vector2(1,0):Vector2.zero;
                edge.anchorMax=i==1?new Vector2(1,0):i==2?new Vector2(0,1):Vector2.one;
                edge.sizeDelta=i<2?new Vector2(0,7):new Vector2(7,0);edge.anchoredPosition=Vector2.zero;
            }
            var pointer=StageUITheme.Text("Arrow",highlight,"▼",48,true);pointer.color=new Color(1,.9f,.25f);arrow=pointer.rectTransform;
            var shade=StageUITheme.Rect("Induction Tutorial Confirmation",safeArea);StageUITheme.Stretch(shade,Vector2.zero,Vector2.one);
            shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.55f);confirmation=shade.gameObject;
            card=StageUITheme.Rect("Confirmation Card",shade);card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);
            card.gameObject.AddComponent<Image>().color=new Color(.06f,.16f,.22f,.98f);
            var question=StageUITheme.Text("Ready Question",card,"Let’s start?",48,true);question.color=Color.white;
            StageUITheme.Stretch(question.rectTransform,new Vector2(.06f,.54f),new Vector2(.94f,.88f));
            YesButton=Choice(card,"Yes",new Vector2(.07f,.1f),new Vector2(.46f,.43f),ConfirmYes);
            NoButton=Choice(card,"No",new Vector2(.54f,.1f),new Vector2(.93f,.43f),ConfirmNo);
            confirmation.SetActive(false);
            worldObstacle=StageUITheme.Rect("Induction Practice Subject",safeArea);
            worldObstacle.anchorMin=worldObstacle.anchorMax=worldObstacle.pivot=new Vector2(.5f,.5f);worldObstacle.gameObject.SetActive(false);
        }
        static Button Choice(Transform parent,string text,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var rect=StageUITheme.Rect(text+" Button",parent);StageUITheme.Stretch(rect,min,max);
            rect.gameObject.AddComponent<Image>().color=new Color(.12f,.48f,.43f);
            var button=rect.gameObject.AddComponent<Button>();button.onClick.AddListener(action);
            var label=StageUITheme.Text(text,rect,text,40,true);label.color=Color.white;label.enableAutoSizing=true;label.fontSizeMin=24;label.fontSizeMax=40;
            StageUITheme.Stretch(label.rectTransform,new Vector2(.04f,.08f),new Vector2(.96f,.92f));return button;
        }
        void BeginMovement()
        {
            ResetGuide();
            confirmation.SetActive(false);Time.timeScale=previousTimeScale;input.SetGameplayInputBlocked(false);player.ToggleControl(true);
            PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,originalPosition,originalRotation);
            mobile.ResetJoystick();movedDistance=0;lastPosition=player.transform.position;movementStart=lastPosition;
            ShowStep(InductionTutorialStep.Movement,"Use this joystick to move.",joystickRing);
        }
        void ShowStep(InductionTutorialStep step,string message,RectTransform target)
        {
            CurrentStep=step;instruction.text=message;
            uiTarget=target;worldTargets=null;NextButton.gameObject.SetActive(false);
            safeArea.Find("Gameplay_Interface/Joystick").gameObject.SetActive(step==InductionTutorialStep.Movement);
            jump.gameObject.SetActive(step==InductionTutorialStep.Jump);hand.gameObject.SetActive(step==InductionTutorialStep.Interaction);
            highlight.SetParent(target,false);StageUITheme.Stretch(highlight,Vector2.zero,Vector2.one);
            highlight.offsetMin=-Vector2.one*10;highlight.offsetMax=Vector2.one*10;highlight.gameObject.SetActive(true);
            highlight.SetAsLastSibling();instructions.gameObject.SetActive(true);instructions.SetAsLastSibling();
        }
        public bool TryGetMovementOverride(out Vector2 movement)
        {
            movement=Vector2.zero;if(!IsActive)return false;
            if(CurrentStep==InductionTutorialStep.Movement&&mobile.CurrentInput.sqrMagnitude>joystickDeadZone*joystickDeadZone)
                movement=mobile.CurrentInput.normalized;
            return true;
        }
        void OnSuccessfulJump()
        {
            if(AllowsJump){jumpSucceeded=true;jumpBaseY=player.transform.position.y;}
        }
        void Update()
        {
            if(!IsActive||player==null||Time.timeScale<=0||input.GameplayInputBlocked)return;
            var position=player.transform.position;
            if(position.y<originalPosition.y-3){BeginMovement();return;}
            if(CurrentStep==InductionTutorialStep.Movement)
            {
                // PRELIM uses net displacement, rather than accumulating joystick jitter.
                if(mobile.CurrentInput.sqrMagnitude>joystickDeadZone*joystickDeadZone&&player.IsWalking())
                    movedDistance+=Time.deltaTime;
                else movedDistance=0;
                if(movedDistance>=intentionalMovementSeconds&&Vector2.Distance(new Vector2(movementStart.x,movementStart.z),new Vector2(position.x,position.z))>=movementDistance)
                {mobile.ResetJoystick();jumpSucceeded=false;ShowStep(InductionTutorialStep.Jump,"Tap this button to jump.",jump);}
            }
            else if(CurrentStep==InductionTutorialStep.Jump&&jumpSucceeded&&player.IsJumping()&&position.y>jumpBaseY+.1f)
            {ShowStep(InductionTutorialStep.Interaction,"Tap the hand button to interact with nearby objects.",hand);}
            lastPosition=position;
        }

        public bool TryPracticeInteraction()
        {
            if(IsActive&&CurrentStep>=InductionTutorialStep.Book)return GuidedInteraction(false);
            if(!IsActive||CurrentStep!=InductionTutorialStep.Interaction||Time.timeScale<=0||input.GameplayInputBlocked||!player.enabled)return false;
            mobile.ResetJoystick();StartGuide();return true;
        }
        void LateUpdate()
        {
            if(!IsActive)return;
            card.sizeDelta=new Vector2(Mathf.Min(660,safeArea.rect.width-40),280);
            float diameter=Mathf.Min(joystick.rect.width,joystick.rect.height)*.9f;joystickRing.sizeDelta=Vector2.one*diameter;
            worldObstacle.gameObject.SetActive(false);
            if(CurrentStep==InductionTutorialStep.Confirm)return;
            if(CurrentStep>=InductionTutorialStep.Book){LayoutGuide();return;}
            var blockers=new List<RectTransform>(ControlTutorialLayout.FindBlockers());blockers.Add(worldObstacle);
            var target=CurrentStep==InductionTutorialStep.Movement?joystickRing:CurrentStep==InductionTutorialStep.Jump?jump:hand;
            blockers.Add((RectTransform)skip.transform);
            ControlTutorialLayout.Place(instructions,instruction,null,safeArea,target,false,blockers.ToArray(),arrow);
        }

        public void ConfirmYes()
        {
            if(!IsActive||CurrentStep!=InductionTutorialStep.Confirm)return;
            stage.help.CompleteOnboarding();Cleanup();
        }
        public void ConfirmNo(){if(IsActive&&CurrentStep==InductionTutorialStep.Confirm)BeginMovement();}

        void Cleanup()
        {
            if(!IsActive)return;IsActive=false;if(Active==this)Active=null;
            ResetGuide();if(skip!=null)skip.Release();if(mobile!=null)mobile.ResetJoystick();
            if(player!=null){player.OnJumpStarted-=OnSuccessfulJump;PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,originalPosition,originalRotation);player.ToggleControl(playerWasEnabled);}
            foreach(var state in visibility)if(state.Key!=null)state.Key.SetActive(state.Value);visibility.Clear();if(controls!=null)controls.SetActive(controlsWereVisible);
            if(settings!=null)settings.interactable=settingsWasEnabled;
            if(input!=null)input.SetGameplayInputBlocked(inputWasBlocked);Time.timeScale=previousTimeScale;
            Cursor.lockState=previousCursor;Cursor.visible=cursorWasVisible;
            if(stage!=null&&stage.ui!=null)stage.ui.ShowControlsTutorial(false);
            foreach(var rect in new[]{highlight,instructions,joystickRing,worldObstacle})if(rect!=null){rect.gameObject.SetActive(false);Destroy(rect.gameObject);}
            if(confirmation!=null){confirmation.SetActive(false);Destroy(confirmation);}
        }
        void OnDisable(){if(IsActive)Cleanup();}
    }
}
