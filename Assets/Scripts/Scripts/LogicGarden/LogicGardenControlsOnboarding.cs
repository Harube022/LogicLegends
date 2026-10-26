using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>First-entry controls practice, independent of the wizard/library milestones.</summary>
[DefaultExecutionOrder(1100)]
[DisallowMultipleComponent]
public sealed class LogicGardenControlsOnboarding : MonoBehaviour
{
    public enum Step { Movement, Jump, Interaction, MeetLefyahj }
    [Range(.15f,.8f)] public float joystickDeadZone = .3f;
    [Min(.1f)] public float movementDistance = 1.5f;
    [Min(.1f)] public float intentionalMovementSeconds = .7f;
    [Min(1)] public float practiceInteractionRange = 4;
    [Header("Wizard preview camera")]
    [Min(.1f)] public float cameraTransitionSeconds = 1.2f;
    [Range(25,75)] public float wizardFieldOfView = 50;
    [Min(1)] public float wizardFramingPadding = 1.6f;
    [Range(.25f,.5f)] public float wizardScreenPosition = .35f;
    public static LogicGardenControlsOnboarding Active { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsPending { get; private set; }
    public Step CurrentStep { get; private set; }
    public bool AllowsJump => IsActive && CurrentStep == Step.Jump;
    public bool CameraTransitioning { get; private set; }
    public GameObject PracticeObject => null;
    public RectTransform Highlight => highlight;
    public RectTransform InstructionPanel => panel;
    public Button NextButton { get; private set; }
    public PrelimTutorialSkipControl SkipControl => skip;
    LogicGardenIntroduction introduction;
    RectTransform safeArea, joystick, joystickRing, jump, hand, panel, highlight, arrow, subject, target;
    TMP_Text text;
    MobileInputUI mobile;
    GameInput input;
    Player player;
    GameObject controls;

    PrelimTutorialSkipControl skip;
    Vector3 movementStart;
    float intentionalSeconds, jumpBaseY, previousTimeScale;
    bool jumped, previousBlocked, previousPlayerEnabled, previousCursorVisible, previousControlsVisible, returning;
    CursorLockMode previousCursor;
    readonly Dictionary<GameObject,bool> visibility = new Dictionary<GameObject,bool>();
    readonly Dictionary<Button,bool> buttonStates = new Dictionary<Button,bool>();
    readonly List<Behaviour> suspendedCameraInputs = new List<Behaviour>();
    readonly Dictionary<Renderer,bool> avatarRenderers = new Dictionary<Renderer,bool>();
    Camera cameraView;
    Vector3 cameraSavedPosition, cameraStartPosition, cameraGoalPosition;
    Quaternion cameraSavedRotation, cameraStartRotation, cameraGoalRotation;
    float cameraSavedFov, cameraStartFov, cameraElapsed, previewAspect;
    Bounds wizardBounds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Active=null;}

    public void Initialize(LogicGardenIntroduction owner,RectTransform host)
    {
        introduction=owner;safeArea=host;
        LogicGardenProgress.Load();
        IsPending=!LogicGardenProgress.ControlsTutorialCompleted&&!LogicGardenProgress.ConversationCompleted;
        if(IsPending)StartCoroutine(BeginAfterSpawn());
    }
    IEnumerator BeginAfterSpawn()
    {
        // The authoritative avatar binds its input/camera in Start after it spawns.
        while(Player.LocalInstance==null||GameInput.Instance==null||Camera.main==null||
            GameInput.Instance.GameplayInputBlocked||Time.timeScale<=0||
            (TeleportManager.Instance!=null&&(TeleportManager.Instance.IsTeleporting||TeleportManager.Instance.FadeAlpha>.001f)))yield return null;
        yield return null;
        LogicGardenProgress.Load();
        if(LogicGardenProgress.ControlsTutorialCompleted||LogicGardenProgress.ConversationCompleted){IsPending=false;introduction.RefreshAfterControlsTutorial();yield break;}
        Begin();
    }
    bool Begin()
    {
        if(IsActive||safeArea==null)return false;
        player=Player.LocalInstance;input=GameInput.Instance;
        var root=safeArea.Find("Gameplay_Interface");
        mobile=root.Find("Joystick").GetComponentInChildren<MobileInputUI>(true);
        joystick=mobile!=null?mobile.transform as RectTransform:null;
        jump=root.Find("JumpButton") as RectTransform;hand=root.Find("InteractButton") as RectTransform;
        if(joystick==null||jump==null||hand==null){IsPending=false;Debug.LogError("LogicGarden tutorial requires its existing joystick, jump and hand controls.",this);return false;}
        controls=root.gameObject;previousControlsVisible=controls.activeSelf;previousBlocked=input.GameplayInputBlocked;previousPlayerEnabled=player.enabled;previousTimeScale=Time.timeScale;
        previousCursor=Cursor.lockState;previousCursorVisible=Cursor.visible;
        foreach(var control in new[]{root.Find("Joystick").gameObject,jump.gameObject,hand.gameObject})visibility[control]=control.activeSelf;
        foreach(var button in root.GetComponentsInChildren<Button>(true)){buttonStates[button]=button.interactable;button.interactable=button.transform==jump||button.transform==hand;}
        controls.SetActive(true);BuildUI();IsActive=true;IsPending=false;Active=this;
        player.OnJumpStarted+=SuccessfulJump;input.SetGameplayInputBlocked(false);player.ToggleControl(true);
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;mobile.ResetJoystick();
        movementStart=player.transform.position;intentionalSeconds=0;
        Show(Step.Movement,"Use this joystick to move.",joystickRing);
        introduction.RefreshAfterControlsTutorial();return true;
    }
    System.Action<bool> replayFinished;
    public bool BeginReplay(LogicGardenIntroduction owner,RectTransform host,System.Action<bool> finished)
    {
        if(IsActive||Player.LocalInstance==null||GameInput.Instance==null)return false;
        StopAllCoroutines();IsPending=false;introduction=owner;safeArea=host;replayFinished=finished;
        if(Begin())return true;replayFinished=null;return false;
    }
    public void CancelReplay(){StopAllCoroutines();IsPending=false;if(IsActive)Finish(false);}
    void BuildUI()
    {
        joystickRing=StageUITheme.Rect("Garden Visible Joystick Target",joystick);joystickRing.anchorMin=joystickRing.anchorMax=joystickRing.pivot=new Vector2(.5f,.5f);
        panel=StageUITheme.Rect("Garden Tutorial Instructions",safeArea);var backing=panel.gameObject.AddComponent<Image>();backing.color=new Color(.06f,.13f,.19f,.95f);backing.raycastTarget=false;
        text=StageUITheme.Text("Instruction",panel,"",36,true);text.color=Color.white;
        NextButton=StageUITheme.Button("Garden Tutorial Next",panel,"Next",Continue);NextButton.gameObject.SetActive(false);
        highlight=StageUITheme.Rect("Garden Tutorial Highlight",safeArea);
        for(int i=0;i<4;i++)
        {
            var edge=StageUITheme.Rect("Highlight Edge",highlight);var image=edge.gameObject.AddComponent<Image>();image.color=new Color(1,.86f,.22f,.95f);image.raycastTarget=false;
            edge.anchorMin=i==0?new Vector2(0,1):i==3?new Vector2(1,0):Vector2.zero;edge.anchorMax=i==1?new Vector2(1,0):i==2?new Vector2(0,1):Vector2.one;
            edge.sizeDelta=i<2?new Vector2(0,7):new Vector2(7,0);edge.anchoredPosition=Vector2.zero;
        }
        var pointer=StageUITheme.Text("Arrow",highlight,"▼",48,true);pointer.color=new Color(1,.9f,.25f);arrow=pointer.rectTransform;
        subject=StageUITheme.Rect("Garden Tutorial Subject",safeArea);subject.anchorMin=subject.anchorMax=subject.pivot=new Vector2(.5f,.5f);subject.gameObject.SetActive(false);
        skip=PrelimTutorialSkipControl.Create(safeArea,Skip);
        var reserved=safeArea.GetComponentsInChildren<RectTransform>(true).Where(r=>r.name=="LogicGarden Spawn Title"||r.name=="ObjectivesPanel"||r.name=="ObjectivesToggleButton").ToArray();skip.Reserve(reserved);
    }
    void Show(Step step,string message,RectTransform uiTarget)
    {
        CurrentStep=step;text.text=message;target=uiTarget;NextButton.gameObject.SetActive(step==Step.MeetLefyahj);
        safeArea.Find("Gameplay_Interface/Joystick").gameObject.SetActive(step==Step.Movement);
        jump.gameObject.SetActive(step==Step.Jump);hand.gameObject.SetActive(step==Step.Interaction);
        highlight.SetParent(uiTarget,false);StageUITheme.Stretch(highlight,Vector2.zero,Vector2.one);highlight.offsetMin=-Vector2.one*10;highlight.offsetMax=Vector2.one*10;
        highlight.SetAsLastSibling();panel.SetAsLastSibling();skip.Show(true);
    }
    public bool TryGetMovementOverride(out Vector2 movement)
    {
        movement=Vector2.zero;if(!IsActive)return false;
        if(CurrentStep==Step.Movement&&mobile.CurrentInput.sqrMagnitude>joystickDeadZone*joystickDeadZone)movement=mobile.CurrentInput.normalized;
        return true;
    }
    void SuccessfulJump(){if(AllowsJump){jumped=true;jumpBaseY=player.transform.position.y;}}
    void Update()
    {
        if(!IsActive||player==null)return;
        if(CurrentStep==Step.Movement)
        {
            bool moving=mobile.CurrentInput.sqrMagnitude>joystickDeadZone*joystickDeadZone&&player.IsWalking();
            intentionalSeconds=moving?intentionalSeconds+Time.deltaTime:0;
            // Same net horizontal displacement test as PRELIM; a nudge cannot accumulate jitter.
            var distance=Vector2.Distance(new Vector2(movementStart.x,movementStart.z),new Vector2(player.transform.position.x,player.transform.position.z));
            if(moving&&intentionalSeconds>=intentionalMovementSeconds&&distance>=movementDistance){mobile.ResetJoystick();jumped=false;Show(Step.Jump,"Tap this button to jump.",jump);}
        }
        else if(CurrentStep==Step.Jump&&jumped&&player.IsJumping()&&player.transform.position.y>jumpBaseY+.1f)
        {Show(Step.Interaction,"Tap the hand button to interact with nearby objects.",hand);}
    }

    public bool TryPracticeInteraction()
    {
        if(!IsActive||CurrentStep!=Step.Interaction||Time.timeScale<=0||input.GameplayInputBlocked)return false;
        MeetWizard();return true;
    }
    void MeetWizard()
    {
        player.ToggleControl(false);input.SetGameplayInputBlocked(true);mobile.ResetJoystick();cameraView=Camera.main;
        cameraSavedPosition=cameraView.transform.position;cameraSavedRotation=cameraView.transform.rotation;cameraSavedFov=cameraView.fieldOfView;
        foreach(var behaviour in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
        {
            if(behaviour is CameraTargetController&&behaviour.GetComponentInParent<Player>()!=player)continue;
            string name=behaviour.GetType().Name;
            if(behaviour.enabled&&(behaviour==cameraView.GetComponent<CinemachineBrain>()||name=="CameraTargetController"||name=="CinemachineInputAxisController"||name=="CinemachinePinchZoom"||name=="ThirdPersonCameraController"))
            {suspendedCameraInputs.Add(behaviour);behaviour.enabled=false;}
        }
        foreach(var renderer in player.GetComponentsInChildren<Renderer>()){avatarRenderers[renderer]=renderer.enabled;renderer.enabled=false;}
        var models=introduction.Wizard.GetComponentsInChildren<Renderer>().Where(r=>r.GetComponent<TMP_Text>()==null).ToArray();
        wizardBounds=models[0].bounds;foreach(var renderer in models)wizardBounds.Encapsulate(renderer.bounds);
        Show(Step.MeetLefyahj,"Lefyahj the wizard would like to speak with you. Approach him and tap ‘Talk with Lefyahj’ or use the hand button to begin the conversation.",subject);
        FrameWizard();
    }
    void FrameWizard()
    {
        previewAspect=cameraView.aspect;Vector3 direction=introduction.Wizard.forward;direction.y=.1f;direction.Normalize();
        var rotation=Quaternion.LookRotation(-direction,Vector3.up);var right=rotation*Vector3.right;
        float distance=Mathf.Max(wizardBounds.extents.y,wizardBounds.extents.x/Mathf.Max(.3f,cameraView.aspect))/Mathf.Tan(wizardFieldOfView*Mathf.Deg2Rad*.5f)*wizardFramingPadding+wizardBounds.extents.z;
        var offset=right*((1-2*wizardScreenPosition)*distance*Mathf.Tan(wizardFieldOfView*Mathf.Deg2Rad*.5f)*cameraView.aspect);
        BeginCameraMove(wizardBounds.center+direction*distance+offset,rotation);
    }
    void BeginCameraMove(Vector3 position,Quaternion rotation)
    {
        cameraStartPosition=cameraView.transform.position;cameraStartRotation=cameraView.transform.rotation;cameraStartFov=cameraView.fieldOfView;
        cameraGoalPosition=position;cameraGoalRotation=rotation;cameraElapsed=0;CameraTransitioning=true;
    }
    void LateUpdate()
    {
        if(!IsActive)return;
        joystickRing.sizeDelta=Vector2.one*Mathf.Min(joystick.rect.width,joystick.rect.height)*.9f;
        if(cameraView!=null)
        {
            if(!returning&&!Mathf.Approximately(previewAspect,cameraView.aspect))FrameWizard();
            if(CameraTransitioning)
            {
                cameraElapsed+=Time.unscaledDeltaTime;float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(cameraElapsed/cameraTransitionSeconds));
                cameraView.transform.SetPositionAndRotation(Vector3.Lerp(cameraStartPosition,cameraGoalPosition,t),Quaternion.Slerp(cameraStartRotation,cameraGoalRotation,t));
                cameraView.fieldOfView=Mathf.Lerp(cameraStartFov,returning?cameraSavedFov:wizardFieldOfView,t);
                if(t>=1){CameraTransitioning=false;if(returning){Finish(true);return;}}
            }
            Project(wizardBounds);NextButton.interactable=!CameraTransitioning;
        }

        else subject.gameObject.SetActive(false);
        var blockers=new List<RectTransform>(ControlTutorialLayout.FindBlockers()){subject,(RectTransform)skip.transform};
        foreach(var rect in safeArea.GetComponentsInChildren<RectTransform>(true))if(rect.name=="ObjectivesPanel"||rect.name=="LogicGarden Spawn Title"||rect.name=="LogicGarden Context Interaction"||rect.name=="Return to LogicGarden")blockers.Add(rect);
        ControlTutorialLayout.Place(panel,text,(RectTransform)NextButton.transform,safeArea,target,CurrentStep==Step.MeetLefyahj,blockers.ToArray(),arrow);
    }
    void Project(Bounds bounds)
    {
        var camera=Camera.main;Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=-min;bool found=false;
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
        {
            var screen=camera.WorldToScreenPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)));if(screen.z<=0)continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea,screen,null,out Vector2 local);min=Vector2.Min(min,local);max=Vector2.Max(max,local);found=true;
        }
        subject.gameObject.SetActive(found);if(found){subject.anchoredPosition=(min+max)*.5f;subject.sizeDelta=max-min+Vector2.one*18;}
    }
    public void Continue()
    {
        if(!IsActive||CurrentStep!=Step.MeetLefyahj||CameraTransitioning||returning)return;
        returning=true;BeginCameraMove(cameraSavedPosition,cameraSavedRotation);
    }
    public void Skip(){if(IsActive)Finish(replayFinished==null);}

    void Finish(bool completed)
    {
        if(!IsActive)return;
        IsActive=false;IsPending=false;if(Active==this)Active=null;CameraTransitioning=false;returning=false;
        if(mobile!=null)mobile.ResetJoystick();
        if(cameraView!=null){cameraView.transform.SetPositionAndRotation(cameraSavedPosition,cameraSavedRotation);cameraView.fieldOfView=cameraSavedFov;}cameraView=null;
        foreach(var behaviour in suspendedCameraInputs)if(behaviour!=null)behaviour.enabled=true;suspendedCameraInputs.Clear();
        foreach(var item in avatarRenderers)if(item.Key!=null)item.Key.enabled=item.Value;avatarRenderers.Clear();MobileLookInput.ResetDelta();
        if(player!=null){player.OnJumpStarted-=SuccessfulJump;player.ToggleControl(previousPlayerEnabled);}
        foreach(var item in visibility)if(item.Key!=null)item.Key.SetActive(item.Value);visibility.Clear();
        foreach(var item in buttonStates)if(item.Key!=null)item.Key.interactable=item.Value;buttonStates.Clear();if(controls!=null)controls.SetActive(previousControlsVisible);
        if(input!=null)input.SetGameplayInputBlocked(previousBlocked);Time.timeScale=previousTimeScale;Cursor.lockState=previousCursor;Cursor.visible=previousCursorVisible;
        var replayCallback=replayFinished;replayFinished=null;
        if(skip!=null&&replayCallback==null)skip.Release();
        foreach(var rect in new[]{highlight,panel,subject,joystickRing})if(rect!=null){rect.gameObject.SetActive(false);Destroy(rect.gameObject);}
        if(completed&&replayCallback==null)LogicGardenProgress.CompleteControlsTutorial();if(introduction!=null)introduction.RefreshAfterControlsTutorial();
        replayCallback?.Invoke(completed);
    }
    void OnDisable(){StopAllCoroutines();IsPending=false;if(IsActive)Finish(false);}
}
