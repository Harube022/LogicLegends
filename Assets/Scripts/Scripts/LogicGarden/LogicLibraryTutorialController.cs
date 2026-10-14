using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One tour/replay session, using the existing subtitle panel and shelf models.</summary>
[DefaultExecutionOrder(1200)]
[DisallowMultipleComponent]
public sealed class LogicLibraryTutorialController : MonoBehaviour
{
    public enum ReplayOption { GardenTutorial, LibraryTour, FullTutorial }
    [Min(.1f)] public float cameraTransitionSeconds=1.2f;
    [Range(25,75)] public float shelfFieldOfView=72;
    [Min(3)] public float shelfCameraDistance=14;
    [Min(0)] public float shelfEyeLift=1;
    [Range(.55f,.75f)] public float shelfScreenHeight=.62f;
    public static LogicLibraryTutorialController Active { get; private set; }
    public static bool InteractionsBlocked => Active!=null&&Active.IsBusy;
    public bool IsBusy => menuOpen||sessionActive||isReplaying;
    public bool TourActive { get; private set; }
    public bool IsReplaying => isReplaying;
    public bool CameraTransitioning { get; private set; }
    public int ShelfIndex { get; private set; }
    public bool Confirming => TourActive&&ShelfIndex==4;
    public Button ReplayButton { get; private set; }
    public Button YesButton { get; private set; }
    public Button NoButton { get; private set; }
    public Button GardenOption { get; private set; }
    public Button LibraryOption { get; private set; }
    public Button FullOption { get; private set; }
    public PrelimTutorialSkipControl SkipControl => skip;
    LogicGardenIntroduction owner;
    StudyLibraryController library;
    RectTransform host,overlay,card;
    TMP_Text heading;
    Button close;
    PrelimTutorialSkipControl skip;
    bool menuOpen,sessionActive,isReplaying,saveTourCompletion,cancelling,cancelAfterTravel,returnAfterReplay;
    ReplayOption replayOption;
    int generation;
    readonly string[] periods={"PRELIM","MIDTERMS","PRE-FINALS","FINALS"};
    Camera view;
    Vector3 previousPosition,startPosition,goalPosition;
    Quaternion previousRotation,startRotation,goalRotation;
    float previousFov,startFov,elapsed,lastAspect,previousTime;
    bool captured,previousBlocked,playerEnabled,controlsVisible,returnVisible,nextEnabled,nextVisible;
    bool locationVisible;
    GameObject location;
    TMP_FontAsset subtitleFont;
    CursorLockMode cursorLock;
    bool cursorVisible;
    Player player;
    readonly List<Behaviour> pausedCamera=new List<Behaviour>();
    readonly Dictionary<Renderer,bool> hiddenAvatar=new Dictionary<Renderer,bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Active=null;}
    public void Initialize(LogicGardenIntroduction introduction,StudyLibraryController controller)
    {
        owner=introduction;library=controller;host=owner.TutorialHost;Active=this;BuildUI();
    }
    void BuildUI()
    {
        ReplayButton=StageUITheme.Button("Replay Tutorial",host,"Replay Tutorial",OpenMenu);
        var replayCanvas=ReplayButton.gameObject.AddComponent<Canvas>();replayCanvas.overrideSorting=true;replayCanvas.sortingOrder=210;ReplayButton.gameObject.AddComponent<GraphicRaycaster>();
        var replay=(RectTransform)ReplayButton.transform;replay.anchorMin=replay.anchorMax=replay.pivot=Vector2.one;replay.sizeDelta=new Vector2(360,90);replay.anchoredPosition=new Vector2(-35,-155);
        overlay=StageUITheme.Rect("Library Tutorial Choices",host);StageUITheme.Stretch(overlay,Vector2.zero,Vector2.one);
        var canvas=overlay.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=250;overlay.gameObject.AddComponent<GraphicRaycaster>();
        overlay.gameObject.AddComponent<Image>().color=new Color(0,0,0,.65f);
        card=StageUITheme.Rect("Tutorial Choices Card",overlay);card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);StageUITheme.CompactSurface(card.gameObject.AddComponent<Image>());
        heading=StageUITheme.Text("Question",card,"Replay Tutorial",42,true);heading.color=Color.white;
        StageUITheme.Stretch(heading.rectTransform,new Vector2(.05f,.73f),new Vector2(.95f,.95f));
        GardenOption=Option("Garden Tutorial",new Vector2(.07f,.52f),new Vector2(.93f,.71f),()=>SelectReplay(ReplayOption.GardenTutorial));
        LibraryOption=Option("Library Tour",new Vector2(.07f,.30f),new Vector2(.93f,.49f),()=>SelectReplay(ReplayOption.LibraryTour));
        FullOption=Option("Full Tutorial",new Vector2(.07f,.08f),new Vector2(.66f,.27f),()=>SelectReplay(ReplayOption.FullTutorial));
        close=Option("Close",new Vector2(.69f,.08f),new Vector2(.93f,.27f),CloseMenu);
        YesButton=Option("Yes",new Vector2(.07f,.12f),new Vector2(.46f,.45f),ConfirmYes);
        NoButton=Option("No",new Vector2(.54f,.12f),new Vector2(.93f,.45f),ConfirmNo);
        overlay.gameObject.SetActive(false);
    }
    Button Option(string label,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
    {var button=StageUITheme.Button(label,card,label,action);StageUITheme.Stretch((RectTransform)button.transform,min,max);return button;}
    void ShowChoices(bool confirmation)
    {
        heading.text=confirmation?"Did you get all that?":"Replay Tutorial";
        foreach(var button in new[]{GardenOption,LibraryOption,FullOption,close})button.gameObject.SetActive(!confirmation);
        YesButton.gameObject.SetActive(confirmation);NoButton.gameObject.SetActive(confirmation);
        overlay.gameObject.SetActive(true);overlay.SetAsLastSibling();
    }
    void Update()
    {
        if(owner==null)return;
        ReplayButton.interactable=!IsBusy&&!owner.Talking&&!owner.Transitioning&&!owner.Onboarding.IsActive&&!owner.Onboarding.IsPending&&!GameInput.Instance.GameplayInputBlocked&&Time.timeScale>0;
        ReplayButton.gameObject.SetActive(!IsBusy&&!owner.Talking&&!owner.Onboarding.IsActive&&!owner.Onboarding.IsPending&&!library.IsReading);
        owner.ReturnButton.gameObject.SetActive(owner.InLibrary&&!IsBusy&&!owner.Talking&&!owner.Transitioning);
        card.sizeDelta=new Vector2(Mathf.Min(840,host.rect.width-40),Mathf.Min(Confirming?340:520,host.rect.height-60));
        if(cancelAfterTravel&&!owner.Transitioning&&(TeleportManager.Instance==null||!TeleportManager.Instance.IsTeleporting)){cancelAfterTravel=false;Cancel(false);}
    }
    public void OpenMenu()
    {
        if(IsBusy||owner.Talking||owner.Onboarding.IsActive||owner.Onboarding.IsPending||owner.Transitioning||GameInput.Instance.GameplayInputBlocked||Time.timeScale<=0)return;
        library.ExitActiveShelf();Capture(false);menuOpen=true;ShowChoices(false);
    }
    public void CloseMenu()
    {
        if(!menuOpen)return;menuOpen=false;overlay.gameObject.SetActive(false);Restore();
    }
    public void SelectReplay(ReplayOption option)
    {
        if(!menuOpen)return;
        CloseMenu();isReplaying=true;replayOption=option;cancelAfterTravel=false;int epoch=++generation;EnsureSkip();
        returnAfterReplay=!LogicGardenProgress.ConversationCompleted&&!owner.InLibrary;
        if(option==ReplayOption.LibraryTour)
        {
            if(owner.InLibrary)StartLibraryIntroduction();
            else if(!owner.ReplayTravel(true,ok=>{if(epoch!=generation)return;if(ok)StartLibraryIntroduction();else Cancel(false);}))Cancel(false);
        }
        else
        {
            if(owner.InLibrary)
            {if(!owner.ReplayTravel(false,ok=>{if(epoch!=generation)return;if(ok)StartGardenReplay();else Cancel(false);}))Cancel(false);}
            else StartGardenReplay();
        }
    }
    void StartGardenReplay()
    {
        if(!owner.Onboarding.BeginReplay(owner,host,ok=>
        {
            if(!isReplaying||cancelling)return;
            if(!ok){Cancel(false);return;}
            EnsureSkip();owner.ReplayDialogue(false,done=>
            {
                if(!isReplaying||cancelling)return;
                if(!done){Cancel(false);return;}
                if(replayOption==ReplayOption.GardenTutorial){Cancel(false);return;}
                int epoch=generation;
                if(!owner.ReplayTravel(true,arrived=>{if(epoch!=generation)return;if(arrived)StartLibraryIntroduction();else Cancel(false);}))Cancel(false);
            });
        }))Cancel(false);
    }
    void StartLibraryIntroduction()
    {
        sessionActive=true;TourActive=false;EnsureSkip();
        if(!owner.ReplayDialogue(true,ok=>{if(cancelling)return;if(ok)BeginTour(!isReplaying);else Cancel(false);}))Cancel(false);
    }
    public void BeginTour(bool persistCompletion)
    {
        if(TourActive)return;
        saveTourCompletion=persistCompletion;sessionActive=true;TourActive=true;ShelfIndex=0;
        library.ExitActiveShelf();foreach(var shelf in FindObjectsByType<StudyShelfZone>(FindObjectsSortMode.None))shelf.SetFocused(false);
        Capture(true);EnsureSkip();ShowShelf();
    }
    void EnsureSkip()
    {
        skip=PrelimTutorialSkipControl.Create(host,()=>Cancel(true));
        var canvas=skip.GetComponent<Canvas>();if(canvas==null){canvas=skip.gameObject.AddComponent<Canvas>();skip.gameObject.AddComponent<GraphicRaycaster>();}canvas.overrideSorting=true;canvas.sortingOrder=310;
        skip.GetComponent<Button>().interactable=true;
        skip.Reserve((RectTransform)ReplayButton.transform,(RectTransform)owner.ReturnButton.transform,
            host.Find("LogicGarden Spawn Title") as RectTransform,host.Find("Gameplay_Interface/Ch1_Objectives") as RectTransform);
    }
    void Capture(bool hideAvatar)
    {
        if(captured)return;captured=true;view=Camera.main;player=Player.LocalInstance;
        previousPosition=view.transform.position;previousRotation=view.transform.rotation;previousFov=view.fieldOfView;
        previousBlocked=GameInput.Instance.GameplayInputBlocked;playerEnabled=player.enabled;previousTime=Time.timeScale;
        controlsVisible=owner.SubtitleUI.gameplayInterfacePanel.activeSelf;returnVisible=owner.ReturnButton.gameObject.activeSelf;
        nextEnabled=owner.SubtitleNext.interactable;nextVisible=owner.SubtitleNext.gameObject.activeSelf;cursorLock=Cursor.lockState;cursorVisible=Cursor.visible;
        location=host.Find("LogicGarden Spawn Title").gameObject;locationVisible=location.activeSelf;subtitleFont=owner.SubtitleUI.dialogueText.font;
        if(hideAvatar){location.SetActive(false);owner.SubtitleUI.dialogueText.font=StageUITheme.ReadingFont;}
        foreach(var behaviour in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
        {
            if(behaviour is CameraTargetController&&behaviour.GetComponentInParent<Player>()!=player)continue;
            if(behaviour.enabled&&(behaviour==view.GetComponent<CinemachineBrain>()||behaviour is CameraTargetController||behaviour is CinemachineInputAxisController||behaviour is CinemachinePinchZoom||behaviour is ThirdPersonCameraController))
            {pausedCamera.Add(behaviour);behaviour.enabled=false;}
        }
        if(hideAvatar)foreach(var renderer in player.GetComponentsInChildren<Renderer>()){hiddenAvatar[renderer]=renderer.enabled;renderer.enabled=false;}
        GameInput.Instance.SetGameplayInputBlocked(true);player.ToggleControl(false);Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        owner.ReturnButton.gameObject.SetActive(false);
    }
    void ShowShelf()
    {
        owner.SubtitleSpeaker.text="Lefyahj";owner.SubtitleNext.gameObject.SetActive(true);
        owner.SubtitleUI.ShowDialoguePanel(StudyLibraryCatalog.TourExplanation(periods[ShelfIndex]));
        overlay.gameObject.SetActive(false);FrameShelf();skip.Show(true);
    }
    void FrameShelf()
    {
        var zone=FindObjectsByType<StudyShelfZone>(FindObjectsSortMode.None).First(s=>s.Period==periods[ShelfIndex]);
        string modelName=new[]{"BOOKSHELVES","BOOKSHELVES (1)","BOOKSHELVES (2)","BOOKSHELVES (3)"}[ShelfIndex];
        var model=GameObject.Find(modelName).GetComponent<Renderer>();var bounds=model.bounds;
        // Frame the actual wooden shelf beneath its period sign, rather than the
        // entire imported arch. Stay on its side of the room's tables and plants.
        float bottom=bounds.min.y+1,top=zone.LabelPosition.y+.8f;
        bounds=new Bounds(new Vector3(zone.LabelPosition.x,(bottom+top)*.5f,bounds.center.z),new Vector3(bounds.size.x,top-bottom,bounds.size.z));
        Quaternion rotation=zone.FocusCamera.transform.rotation;Vector3 forward=rotation*Vector3.forward,up=rotation*Vector3.up,right=rotation*Vector3.right;
        float width=Vector3.Dot(bounds.extents,new Vector3(Mathf.Abs(right.x),Mathf.Abs(right.y),Mathf.Abs(right.z)));
        float depth=Vector3.Dot(bounds.extents,new Vector3(Mathf.Abs(forward.x),Mathf.Abs(forward.y),Mathf.Abs(forward.z)));
        float distance=Mathf.Max(shelfCameraDistance,width/Mathf.Max(.3f,view.aspect)/Mathf.Tan(shelfFieldOfView*Mathf.Deg2Rad*.5f)*1.05f+depth);
        startPosition=view.transform.position;startRotation=view.transform.rotation;startFov=view.fieldOfView;
        goalPosition=bounds.center-forward*distance+up*shelfEyeLift;
        float captionAngle=Mathf.Atan((2*shelfScreenHeight-1)*Mathf.Tan(shelfFieldOfView*Mathf.Deg2Rad*.5f))*Mathf.Rad2Deg;
        goalRotation=Quaternion.LookRotation(bounds.center-goalPosition,Vector3.up)*Quaternion.Euler(captionAngle,0,0);
        elapsed=0;lastAspect=view.aspect;CameraTransitioning=true;owner.SubtitleNext.interactable=false;
    }
    void LateUpdate()
    {
        if(!TourActive||Confirming||view==null)return;
        if(!Mathf.Approximately(lastAspect,view.aspect))FrameShelf();
        if(CameraTransitioning)
        {
            elapsed+=Time.unscaledDeltaTime;float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/cameraTransitionSeconds));
            view.transform.SetPositionAndRotation(Vector3.Lerp(startPosition,goalPosition,t),Quaternion.Slerp(startRotation,goalRotation,t));view.fieldOfView=Mathf.Lerp(startFov,shelfFieldOfView,t);
            if(t>=1){CameraTransitioning=false;owner.SubtitleNext.interactable=true;}
        }
    }
    public void Next()
    {
        if(!TourActive||CameraTransitioning||Confirming)return;
        ShelfIndex++;
        if(Confirming)
        {
            owner.SubtitleUI.dialoguePanel.SetActive(false);owner.SubtitleNext.gameObject.SetActive(false);ShowChoices(true);skip.Show(true);
        }
        else ShowShelf();
    }
    public void ConfirmYes(){if(Confirming)Cancel(true);}
    public void ConfirmNo()
    {
        if(!Confirming)return;
        TourActive=false;overlay.gameObject.SetActive(false);Restore();StartLibraryIntroduction();
    }
    public void Cancel(bool acknowledged)
    {
        if(cancelling||!IsBusy)return;
        if(owner.Transitioning&&isReplaying){cancelAfterTravel=true;generation++;if(skip!=null)skip.GetComponent<Button>().interactable=false;return;}
        // A replay can demonstrate the library to a new player, but it never
        // grants normal library access before the real wizard conversation.
        if(returnAfterReplay&&owner.InLibrary&&isReplaying)
        {
            returnAfterReplay=false;cancelling=true;owner.CancelReplayDialogue();owner.SubtitleUI.HideDialoguePanel();
            TourActive=false;overlay.gameObject.SetActive(false);Restore();if(skip!=null)skip.GetComponent<Button>().interactable=false;
            if(owner.ReplayTravel(false,ok=>{cancelling=false;Cancel(false);}))return;cancelling=false;
        }
        cancelling=true;bool persist=acknowledged&&saveTourCompletion&&!isReplaying&&sessionActive;
        generation++;owner.CancelReplayDialogue();if(owner.Onboarding.IsActive)owner.Onboarding.CancelReplay();
        owner.SubtitleUI.HideDialoguePanel();menuOpen=false;sessionActive=false;isReplaying=false;TourActive=false;CameraTransitioning=false;
        overlay.gameObject.SetActive(false);Restore();if(skip!=null){skip.Release();skip=null;}
        returnAfterReplay=false;
        if(persist)LogicGardenProgress.CompleteLibraryTour();owner.RefreshAfterControlsTutorial();cancelling=false;
    }
    void Restore()
    {
        CameraTransitioning=false;
        if(!captured)return;captured=false;
        if(view!=null){view.transform.SetPositionAndRotation(previousPosition,previousRotation);view.fieldOfView=previousFov;}
        foreach(var behaviour in pausedCamera)if(behaviour!=null){behaviour.enabled=true;if(behaviour is CinemachineBrain brain)brain.ResetState();}pausedCamera.Clear();
        foreach(var pair in hiddenAvatar)if(pair.Key!=null)pair.Key.enabled=pair.Value;hiddenAvatar.Clear();
        if(player!=null)player.ToggleControl(playerEnabled);if(GameInput.Instance!=null)GameInput.Instance.SetGameplayInputBlocked(previousBlocked);
        Time.timeScale=previousTime;Cursor.lockState=cursorLock;Cursor.visible=cursorVisible;MobileLookInput.ResetDelta();
        owner.SubtitleUI.gameplayInterfacePanel.SetActive(controlsVisible);owner.SubtitleNext.interactable=nextEnabled;owner.SubtitleNext.gameObject.SetActive(nextVisible);
        owner.ReturnButton.gameObject.SetActive(returnVisible);view=null;
        if(location!=null)location.SetActive(locationVisible);owner.SubtitleUI.dialogueText.font=subtitleFont;
    }
    void OnDisable(){if(owner!=null)Cancel(false);if(Active==this)Active=null;}
    void OnDestroy(){if(ReplayButton!=null)Destroy(ReplayButton.gameObject);if(overlay!=null)Destroy(overlay.gameObject);}
}
