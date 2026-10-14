using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class LogicGardenIntroduction
{
    [SerializeField] private LogicLibraryTutorialController libraryTutorial;
    public LogicLibraryTutorialController LibraryTutorial => libraryTutorial;
    public RectTransform TutorialHost => locationTitle.transform.parent as RectTransform;
    public DialogueManager SubtitleUI => dialogue;
    public Button SubtitleNext => nextButton;
    public TMP_Text SubtitleSpeaker => speakerText;
    public Button ReturnButton => returnToGardenButton;
    public bool Transitioning => teleportPending;
    bool replayDialogue;
    float replayDialogueTime;
    CursorLockMode replayCursorLock;
    bool replayCursorVisible;
    Unity.Cinemachine.CinemachineBrain replayBrain;
    bool replayIgnoreTime;
    Action<bool> replayDialogueFinished;

    public bool ReplayDialogue(bool library,Action<bool> finished)
    {
        if(talking||teleportPending)return false;
        if(player==null)player=FindLocalPlayer();
        replayDialogue=true;replayDialogueFinished=finished;replayDialogueTime=Time.timeScale;
        replayCursorLock=Cursor.lockState;replayCursorVisible=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        replayBrain=Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>();replayIgnoreTime=replayBrain.IgnoreTimeScale;replayBrain.IgnoreTimeScale=true;
        foreach(var control in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            if(control.enabled&&(control is CameraTargetController||control is Unity.Cinemachine.CinemachineInputAxisController||control is CinemachinePinchZoom))
            {control.enabled=false;pausedCameraInputs.Add(control);}
        libraryDialogue=library;OpenDialogue(library?LibraryLines:Lines);Time.timeScale=0;return true;
    }
    void FinishReplayDialogue(bool completed)
    {
        var callback=replayDialogueFinished;replayDialogueFinished=null;replayDialogue=false;Time.timeScale=replayDialogueTime;
        Cursor.lockState=replayCursorLock;Cursor.visible=replayCursorVisible;
        if(replayBrain!=null)replayBrain.IgnoreTimeScale=replayIgnoreTime;replayBrain=null;
        UpdateWizardAttention();callback?.Invoke(completed);
    }
    public void CancelReplayDialogue(){if(replayDialogue&&talking)CloseDialogue(false);}
    public bool ReplayTravel(bool toLibrary,Action<bool> arrived)
    {
        if(teleportPending||TeleportManager.Instance==null||TeleportManager.Instance.IsTeleporting)return false;
        if(player==null)player=FindLocalPlayer();if(player==null)return false;
        if(studyLibrary!=null)studyLibrary.ExitActiveShelf();
        Transform destination=toLibrary?libraryDoor.TeleportDestination:gardenEntry;
        teleportPending=true;TeleportManager.Instance.StartTeleport(player.gameObject,destination);
        StartCoroutine(FinishReplayTravel(destination,toLibrary,arrived));return true;
    }
    IEnumerator FinishReplayTravel(Transform destination,bool toLibrary,Action<bool> arrived)
    {
        float until=Time.realtimeSinceStartup+8;
        while(Time.realtimeSinceStartup<until)
        {
            if(!TeleportManager.Instance.IsTeleporting&&Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(destination.position.x,destination.position.z))<3)
            {
                inLibrary=toLibrary;teleportPending=false;locationTitle.text=toLibrary?"Logic Library":"LogicGarden";
                returnToGardenButton.gameObject.SetActive(toLibrary);UpdateWizardAttention();arrived?.Invoke(true);yield break;
            }
            yield return null;
        }
        teleportPending=false;arrived?.Invoke(false);
    }
}
