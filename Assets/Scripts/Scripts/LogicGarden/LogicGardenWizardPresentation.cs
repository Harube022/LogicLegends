using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class LogicGardenIntroduction
{
    [SerializeField] WizardActor gardenWizardActor,libraryWizardActor;
    HandThoughtBubble libraryWizardBubble;
    RectTransform returnQuestion,returnCard;
    TMP_Text returnQuestionText;
    Button returnYes,returnNo;
    bool returnQuestionOpen,returnPlayerEnabled,returnInputBlocked,returnControlsVisible;
    float returnTime;
    CursorLockMode returnCursorLock;bool returnCursorVisible;
    readonly List<Behaviour> returnLook=new List<Behaviour>();
    public WizardActor LibraryWizard => libraryWizardActor;
    public WizardActor GardenWizard => gardenWizardActor;
    public bool ReturnQuestionOpen => returnQuestionOpen;
    public Button ReturnYes => returnYes;
    public Button ReturnNo => returnNo;
    public const string ReturnQuestion="Would you like to return to the garden?";

    public void PresentWizardLine(string subtitle,bool library)
    {
        var active=library?libraryWizardActor:gardenWizardActor;
        var other=library?gardenWizardActor:libraryWizardActor;
        if(libraryWizardActor!=null&&libraryWizardActor.TryGetComponent<WizardLibraryRoaming>(out var roaming))roaming.SetPaused(true);
        if(other!=null)other.ReturnIdle();
        if(active!=null){if(library)active.ReturnIdle();else active.SpeakLine();}
    }
    public void EndWizardLine()
    {
        if(gardenWizardActor!=null)gardenWizardActor.ReturnIdle();
        if(libraryWizardActor!=null)libraryWizardActor.ReturnIdle();
    }
    void UpdateLibraryWizard()
    {
        if(returnToGardenButton!=null)returnToGardenButton.gameObject.SetActive(false);
        if(libraryWizardActor==null)return;
        if(player==null)player=FindLocalPlayer();
        bool available=inLibrary&&!OnboardingBusy&&!talking&&!teleportPending&&player!=null&&
            (input==null||!input.GameplayInputBlocked)&&(studyLibrary==null||!studyLibrary.IsInReadingView);
        if(libraryWizardActor.TryGetComponent<WizardLibraryRoaming>(out var roaming))roaming.SetPaused(!available||Time.timeScale<=0);
        if(libraryWizardBubble==null)
        {
            libraryWizardBubble=HandThoughtBubble.Create(libraryWizardActor.transform,"Library Wizard Interaction",yellowMarker:true);
            libraryWizardBubble.BoundedScreenSize=true;libraryWizardBubble.VisibilityDistance=24;
        }
        var position=libraryWizardActor.transform.position+Vector3.up*(libraryWizardActor.standingHeight+1.55f);
        libraryWizardBubble.Show(available,available&&NearLibraryWizard(),position);
        if(available)libraryWizardBubble.KeepInView(libraryWizardActor.GetComponentInChildren<TMP_Text>());
        if(returnQuestionOpen)FitReturnQuestion();
    }
    bool NearLibraryWizard()
    {
        if(player==null||libraryWizardActor==null)return false;
        var delta=player.transform.position-libraryWizardActor.transform.position;delta.y=0;
        return delta.sqrMagnitude<=interactionRange*interactionRange;
    }
    void BuildReturnQuestion()
    {
        if(returnQuestion!=null)return;
        returnQuestion=StageUITheme.Rect("Library Wizard Return Question",TutorialHost);
        StageUITheme.Stretch(returnQuestion,Vector2.zero,Vector2.one);
        var canvas=returnQuestion.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=300;
        returnQuestion.gameObject.AddComponent<GraphicRaycaster>();returnQuestion.gameObject.AddComponent<Image>().color=new Color(0,0,0,.6f);
        returnCard=StageUITheme.Rect("Return Question Card",returnQuestion);returnCard.anchorMin=returnCard.anchorMax=returnCard.pivot=new Vector2(.5f,.5f);
        StageUITheme.CompactSurface(returnCard.gameObject.AddComponent<Image>());
        returnQuestionText=StageUITheme.Text("Question",returnCard,ReturnQuestion,38,true);returnQuestionText.fontStyle=FontStyles.Bold;
        StageUITheme.Stretch(returnQuestionText.rectTransform,new Vector2(.07f,.42f),new Vector2(.93f,.91f));
        returnYes=StageUITheme.Button("Yes",returnCard,"Yes",()=>ChooseLibraryReturn(true));
        returnNo=StageUITheme.Button("No",returnCard,"No",()=>ChooseLibraryReturn(false));
        FitReturnQuestion();returnQuestion.gameObject.SetActive(false);
    }
    void FitReturnQuestion()
    {
        if(returnCard==null)return;
        float scale=Mathf.Max(.01f,returnQuestion.GetComponentInParent<Canvas>().rootCanvas.scaleFactor);
        float buttonHeight=Mathf.Max(88,48/scale);
        returnCard.sizeDelta=new Vector2(Mathf.Min(800,TutorialHost.rect.width-40),Mathf.Min(400,TutorialHost.rect.height-40));
        foreach(var button in new[]{returnYes,returnNo})
        {
            var rect=(RectTransform)button.transform;rect.anchorMin=new Vector2(button==returnYes?.08f:.54f,0);rect.anchorMax=new Vector2(button==returnYes?.46f:.92f,0);
            rect.pivot=new Vector2(.5f,0);rect.offsetMin=new Vector2(0,24);rect.offsetMax=new Vector2(0,24+buttonHeight);
        }
    }
    public bool TryLibraryWizardInteraction()
    {
        if(!inLibrary||talking||teleportPending||OnboardingBusy||(input!=null&&input.GameplayInputBlocked)||
            (studyLibrary!=null&&studyLibrary.IsInReadingView))return false;
        if(player==null)player=FindLocalPlayer();if(!NearLibraryWizard())return false;
        if(libraryWizardActor.TryGetComponent<WizardLibraryRoaming>(out var roaming))roaming.SetPaused(true);
        BuildReturnQuestion();returnQuestionOpen=true;talking=true;
        returnInputBlocked=input!=null&&input.GameplayInputBlocked;returnPlayerEnabled=player.enabled;returnTime=Time.timeScale;
        returnControlsVisible=dialogue.gameplayInterfacePanel.activeSelf;
        returnCursorLock=Cursor.lockState;returnCursorVisible=Cursor.visible;
        foreach(var control in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            if(control.enabled&&(control is CameraTargetController||control is Unity.Cinemachine.CinemachineInputAxisController||control is CinemachinePinchZoom))
            {returnLook.Add(control);control.enabled=false;}
        input?.SetGameplayInputBlocked(true);player.ToggleControl(false);dialogue.gameplayInterfacePanel.SetActive(false);
        Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;MobileLookInput.ResetDelta();
        returnQuestion.gameObject.SetActive(true);returnQuestion.SetAsLastSibling();FitReturnQuestion();PresentWizardLine(ReturnQuestion,true);
        return true;
    }
    public void ChooseLibraryReturn(bool yes)
    {
        if(!returnQuestionOpen)return;
        CloseLibraryReturnQuestion();if(yes)ReturnToGarden();
    }
    void CloseLibraryReturnQuestion()
    {
        if(!returnQuestionOpen)return;
        returnQuestionOpen=false;talking=false;if(returnQuestion!=null)returnQuestion.gameObject.SetActive(false);EndWizardLine();
        foreach(var control in returnLook)if(control!=null)control.enabled=true;returnLook.Clear();
        if(input!=null)input.SetGameplayInputBlocked(returnInputBlocked);if(player!=null)player.ToggleControl(returnPlayerEnabled);
        dialogue.gameplayInterfacePanel.SetActive(returnControlsVisible);Time.timeScale=returnTime;
        Cursor.lockState=returnCursorLock;Cursor.visible=returnCursorVisible;MobileLookInput.ResetDelta();
    }
}
