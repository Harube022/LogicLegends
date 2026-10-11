using System;
using System.Collections;
using LogicLegends.Graph;
using UnityEngine;
using UnityEngine.InputSystem;
namespace LogicLegends.Trees
{
    public enum TreeStageState { WAITING_FOR_ACTIVATION, ACTIVE, VALIDATING, CHALLENGE_COMPLETE, STAGE_COMPLETE, GAME_OVER }
    public sealed class TreeStageManager : MonoBehaviour
    {
        public TreeChallengeCatalog catalog;
        public TreeBoardController board;
        public TreeUIController ui;
        public GraphStageClock timer;
        public GraphCompletion completion;
        public GameOverManager gameOver;
        public Transform bookStatue, spawnPoint;
        public AudioClip correctSound, wrongSound;
        public float countdownSeconds=300, feedbackSeconds=1.1f;
        public TreeStageState State {get;private set;}=TreeStageState.WAITING_FOR_ACTIVATION;
        public int ChallengeIndex {get;private set;}
        public int Correct {get;private set;}
        public int Mistakes {get;private set;}
        public int? Answer {get;private set;}
        public string Feedback {get;private set;}="Approach the Book Statue and tap INTERACT to begin.";
        public TreeChallengeData Challenge=>catalog.challenges[ChallengeIndex];
        public Player LocalPlayer {get;private set;}
        public bool Ready {get;private set;}
        public bool LessonOpen {get;private set;}
        public bool Moving {get;private set;}
        public bool NearBook=>LocalPlayer!=null&&Vector3.Distance(LocalPlayer.transform.position,bookStatue.position)<4.5f;
        public bool CanAct=>Ready&&!Moving&&!LessonOpen&&Time.timeScale>0&&(GameInput.Instance==null||!GameInput.Instance.GameplayInputBlocked)&&(TeleportManager.Instance==null||!TeleportManager.Instance.IsTransitioning);
        AudioSource audioSource;
        GameInput input;
        Vector2 pointerStart;
        bool pointerDown;
        void Awake()=>StageSelectionState.Select(10);
        IEnumerator Start()
        {
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;AudioVolumeSettings.Route(audioSource,GameAudioChannel.SoundFX);
            board.Build(Challenge);timer.Initialize(countdownSeconds,HandleGameOver);ui.Build(this);
            while(Player.LocalInstance==null)yield return null;
            LocalPlayer=Player.LocalInstance;input=GameInput.Instance;input.SetGameplayInputBlocked(false);input.OnInteractAction+=OnInteract;
            PrelimTeleportTransition.MovePlayerImmediately(LocalPlayer.gameObject,spawnPoint.position,spawnPoint.rotation);
            var look=LocalPlayer.GetComponentInChildren<CameraTargetController>();if(look!=null)look.SetViewAngles(18,0);
            Ready=true;
        }
        void OnDestroy(){if(input!=null)input.OnInteractAction-=OnInteract;}
        void OnInteract(object sender,EventArgs args){InteractBook();}
        void Update()
        {
            if(!Ready)return;
            ui.Refresh();
            if(!CanAct||State!=TreeStageState.ACTIVE)return;
            var pointer=Pointer.current;
            if(pointer==null)return;
            if(pointer.press.wasPressedThisFrame){pointerStart=pointer.position.ReadValue();pointerDown=true;}
            if(pointer.press.wasReleasedThisFrame&&pointerDown)
            {
                pointerDown=false;var pos=pointer.position.ReadValue();
                if(Vector2.Distance(pos,pointerStart)>25)return;
                var events=UnityEngine.EventSystems.EventSystem.current;
                if(events!=null)
                {
                    var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events){position=pos},hits);
                    if(hits.Exists(h=>h.gameObject.GetComponentInParent<UnityEngine.UI.Button>()!=null||h.gameObject.GetComponentInParent<MobileInputUI>()!=null))return;
                }
                var camera=Camera.main;if(camera!=null&&Physics.Raycast(camera.ScreenPointToRay(pos),out var hit,80))
                {var node=hit.collider.GetComponentInParent<TreeNode>();if(node!=null)SelectNode(node.id);}
            }
        }
        public void InteractBook()
        {
            if(!CanAct||!NearBook)return;
            if(State==TreeStageState.WAITING_FOR_ACTIVATION){State=TreeStageState.ACTIVE;timer.Resume();Feedback=Challenge.objective;}
            else Confirm();
        }
        public void SelectNode(string id)
        {
            if(!CanAct||State!=TreeStageState.ACTIVE)return;
            board.Select(id);Feedback=board.Second==null?"Selected "+board.First+(Challenge.Editable?". Select the second endpoint.":". Confirm at the Book Statue."):"Selected "+board.First+" to "+board.Second+". Tap CONNECT or REMOVE.";ui.RefreshDiagram();
        }
        public void SelectAnswer(int number){if(!CanAct||State!=TreeStageState.ACTIVE||(Challenge.type!=TreeChallengeType.Depth&&Challenge.type!=TreeChallengeType.Height))return;Answer=number;Feedback="Selected "+number+". Confirm at the Book Statue.";}
        public void Edit(bool remove){if(!CanAct||State!=TreeStageState.ACTIVE)return;board.Edit(remove,out var reason);Feedback=reason;ui.RefreshDiagram();}
        public void ClearSelection(){if(!CanAct||State!=TreeStageState.ACTIVE)return;board.ClearSelection();Answer=null;Feedback="Selection cleared. Connections are kept.";ui.RefreshDiagram();}
        public void Confirm()
        {
            if(!CanAct||State!=TreeStageState.ACTIVE||!NearBook)return;
            State=TreeStageState.VALIDATING;
            bool valid=TreeAnswerValidator.Validate(Challenge,board.Edges,board.First,Answer,out var reason);
            foreach(var node in board.Nodes)node.ShowFeedback(valid);
            var clip=valid?correctSound:wrongSound;if(clip!=null)audioSource.PlayOneShot(clip);
            if(!valid)
            {
                Mistakes++;Feedback="INCORRECT  /  -10 seconds\n"+reason;timer.Penalize(10);
                if(State!=TreeStageState.GAME_OVER)StartCoroutine(Wrong());return;
            }
            Correct++;timer.Pause();State=TreeStageState.CHALLENGE_COMPLETE;Feedback="CHALLENGE COMPLETE";StartCoroutine(Advance());
        }
        IEnumerator Wrong()
        {
            LocalPlayer.ToggleControl(false);yield return new WaitForSeconds(feedbackSeconds);
            if(State!=TreeStageState.VALIDATING)yield break;board.RefreshSelection();LocalPlayer.ToggleControl(true);State=TreeStageState.ACTIVE;
        }
        IEnumerator Advance()
        {
            LocalPlayer.ToggleControl(false);yield return new WaitForSeconds(feedbackSeconds);
            if(State!=TreeStageState.CHALLENGE_COMPLETE)yield break;
            if(Correct==catalog.challenges.Count)
            {State=TreeStageState.STAGE_COMPLETE;input.SetGameplayInputBlocked(true);completion.Complete(Mistakes);ui.ShowResult(true);yield break;}
            Moving=true;
            yield return TeleportManager.Instance.TeleportAndWait(LocalPlayer.gameObject,spawnPoint,()=>{ChallengeIndex++;Answer=null;board.Build(Challenge);ui.RebuildDiagram();});
            if(State==TreeStageState.GAME_OVER)yield break;
            Moving=false;LocalPlayer.ToggleControl(true);State=TreeStageState.WAITING_FOR_ACTIVATION;Feedback="Next challenge ready. Interact with the Book Statue to begin.";
        }
        public void HandleGameOver()
        {
            if(State==TreeStageState.STAGE_COMPLETE||State==TreeStageState.GAME_OVER)return;
            State=TreeStageState.GAME_OVER;timer.Pause();LocalPlayer?.ToggleControl(false);input?.SetGameplayInputBlocked(true);gameOver.ShowGameOver();ui.ShowResult(false);
        }
        public void SetLesson(bool open)
        {
            if(!Ready||Moving||(State!=TreeStageState.ACTIVE&&State!=TreeStageState.WAITING_FOR_ACTIVATION))return;
            LessonOpen=open;LocalPlayer.ToggleControl(!open);input.SetGameplayInputBlocked(open);
            if(State==TreeStageState.ACTIVE){if(open)timer.Pause();else timer.Resume();}ui.ShowLesson(open);
        }
        public void Restart()=>Navigate("Trees");
        public void Return()=>Navigate("Main Menu");
        void Navigate(string scene){Time.timeScale=1;input?.SetGameplayInputBlocked(false);TeleportManager.Instance.LoadSceneWithFade(scene);}
    }
}
