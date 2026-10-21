using System;
using System.Collections;
using LogicLegends.Graph;
using UnityEngine;
namespace LogicLegends.Trees
{
    public enum TreeStageState { WAITING_FOR_ACTIVATION, ACTIVE, VALIDATING, CHALLENGE_COMPLETE, STAGE_COMPLETE, GAME_OVER }
    public sealed class TreeStageManager : MonoBehaviour
    {
        public TreeChallengeCatalog catalog;
        public TreeBoardController board;
        public TreeUIController ui;
        public GraphStageClock timer;
        public GameOverManager gameOver;
        public TreeBoardFocus boardFocus;
        public Transform bookStatue, spawnPoint;
        public AudioClip correctSound, wrongSound;
        public float countdownSeconds=600, feedbackSeconds=1.1f;
        public TreeStageState State {get;private set;}=TreeStageState.WAITING_FOR_ACTIVATION;
        public int ChallengeIndex {get;private set;}
        public int Correct {get;private set;}
        public int Mistakes {get;private set;}
        public int? Answer {get;private set;}
        public string Feedback {get;private set;}="Approach the Book Statue and tap the hand button.";
        TreeChallengeData activeChallenge;
        public int ActiveVariantIndex {get;private set;}=-1;
        public TreeChallengeData Challenge=>activeChallenge??catalog.challenges[ChallengeIndex];
        public Player LocalPlayer {get;private set;}
        public bool Ready {get;private set;}
        public bool LessonOpen {get;private set;}
        public bool Moving {get;private set;}
        public bool BoardOpen {get;private set;}
        bool inputBeforeBoard, playerBeforeBoard;
        public bool NearBook=>LocalPlayer!=null&&Vector3.Distance(LocalPlayer.transform.position,bookStatue.position)<4.5f;
        public bool CanAct=>Ready&&!Moving&&!LessonOpen&&Time.timeScale>0&&(boardFocus==null||!boardFocus.Transitioning)&&(BoardOpen||GameInput.Instance==null||!GameInput.Instance.GameplayInputBlocked)&&(TeleportManager.Instance==null||!TeleportManager.Instance.IsTransitioning);
        public bool CanSolve=>CanAct&&BoardOpen&&State==TreeStageState.ACTIVE;
        AudioSource audioSource;
        GameInput input;
        void Awake()=>StageSelectionState.Select(10);
        IEnumerator Start()
        {
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;AudioVolumeSettings.Route(audioSource,GameAudioChannel.SoundFX);
            if(boardFocus==null)boardFocus=gameObject.AddComponent<TreeBoardFocus>();
            boardFocus.board=board.transform;
            ChooseVariant();board.Build(Challenge);timer.Initialize(countdownSeconds,HandleGameOver);ui.Build(this);
            while(Player.LocalInstance==null)yield return null;
            LocalPlayer=Player.LocalInstance;input=GameInput.Instance;input.SetGameplayInputBlocked(false);input.OnInteractAction+=OnInteract;
            PrelimTeleportTransition.MovePlayerImmediately(LocalPlayer.gameObject,spawnPoint.position,spawnPoint.rotation);
            var look=LocalPlayer.GetComponentInChildren<CameraTargetController>();if(look!=null)look.SetViewAngles(18,0);
            Ready=true;
            BookStatuePresentation.BindHand(bookStatue, () => isActiveAndEnabled && CanAct && !BoardOpen && NearBook && (State == TreeStageState.WAITING_FOR_ACTIVATION || State == TreeStageState.ACTIVE), InteractBook);
        }
        void OnDestroy(){if(input!=null){input.OnInteractAction-=OnInteract;if(BoardOpen)input.SetGameplayInputBlocked(inputBeforeBoard);}if(boardFocus!=null)boardFocus.Exit();}
        void OnInteract(object sender,EventArgs args){InteractBook();}
        void OnDisable() { BookStatuePresentation.SetRequired(bookStatue, false); }
        void Update()
        {
            BookStatuePresentation.SetRequired(bookStatue, isActiveAndEnabled && Ready && !Moving && !BoardOpen &&
                !LessonOpen && (State == TreeStageState.WAITING_FOR_ACTIVATION || State == TreeStageState.ACTIVE));
            if(!Ready)return;
            ui.Refresh();
        }
        public void InteractBook()
        {
            if(!CanAct||BoardOpen||!NearBook||(State!=TreeStageState.WAITING_FOR_ACTIVATION&&State!=TreeStageState.ACTIVE))return;
            if(!boardFocus.Enter(LocalPlayer))return;
            inputBeforeBoard=input.GameplayInputBlocked;playerBeforeBoard=LocalPlayer.enabled;
            BoardOpen=true;input.SetGameplayInputBlocked(true);LocalPlayer.ToggleControl(false);
            if(State==TreeStageState.WAITING_FOR_ACTIVATION){State=TreeStageState.ACTIVE;timer.Resume();Feedback="Work on the tree, then tap CONFIRM. EXIT BOARD keeps your work.";}
            ui.Refresh();
        }
        public void ExitBoard()
        {
            if(!BoardOpen||LessonOpen||Moving||Time.timeScale<=0||(State!=TreeStageState.ACTIVE&&State!=TreeStageState.VALIDATING))return;
            CloseBoard(true);
            Feedback="Your work is kept. Interact with the Book Statue to reopen the board.";
            ui.Refresh();
        }
        void CloseBoard(bool restorePlayer)
        {
            if(!BoardOpen)return;
            BoardOpen=false;boardFocus.Exit();input?.SetGameplayInputBlocked(inputBeforeBoard);
            if(restorePlayer&&State==TreeStageState.ACTIVE)LocalPlayer.ToggleControl(playerBeforeBoard);
        }
        public void SelectNode(string id)
        {
            if(!CanSolve)return;
            board.Select(id);Feedback=board.Second==null?"Selected "+board.First+(Challenge.Editable?". Select the second endpoint.":". Tap CONFIRM."):"Selected "+board.First+" to "+board.Second+". Tap CONNECT or REMOVE.";ui.RefreshDiagram();
        }
        public void SelectAnswer(int number){if(!CanSolve||(Challenge.type!=TreeChallengeType.Depth&&Challenge.type!=TreeChallengeType.Height))return;Answer=number;Feedback="Selected "+number+". Tap CONFIRM.";}
        public void Edit(bool remove){if(!CanSolve)return;board.Edit(remove,out var reason);Feedback=reason;ui.RefreshDiagram();}
        public void ClearSelection(){if(!CanSolve)return;board.ClearSelection();Answer=null;Feedback="Selection cleared. Connections are kept.";ui.RefreshDiagram();}
        public void Confirm()
        {
            if(!CanSolve)return;
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
        public bool CanDebugClearChallenge => CanAct && State == TreeStageState.ACTIVE;
        public void DebugClearCurrentChallenge()
        {
            if (!CanDebugClearChallenge) return;
            Correct++; timer.Pause(); State = TreeStageState.CHALLENGE_COMPLETE;
            Feedback = "Challenge cleared by Admin."; StartCoroutine(Advance());
        }
        IEnumerator Wrong()
        {
            LocalPlayer.ToggleControl(false);yield return new WaitForSeconds(feedbackSeconds);
            if(State!=TreeStageState.VALIDATING)yield break;board.RefreshSelection();LocalPlayer.ToggleControl(!BoardOpen&&playerBeforeBoard);State=TreeStageState.ACTIVE;
        }
        IEnumerator Advance()
        {
            LocalPlayer.ToggleControl(false);yield return new WaitForSeconds(feedbackSeconds);
            if(State!=TreeStageState.CHALLENGE_COMPLETE)yield break;
            if(Correct==catalog.challenges.Count)
            {CloseBoard(false);State=TreeStageState.STAGE_COMPLETE;input.SetGameplayInputBlocked(true);ui.ShowResult(true);yield break;}
            Moving=true;
            CloseBoard(false);
            yield return TeleportManager.Instance.TeleportAndWait(LocalPlayer.gameObject,spawnPoint,()=>{ChallengeIndex++;Answer=null;ChooseVariant();board.Build(Challenge);ui.RebuildDiagram();});
            if(State==TreeStageState.GAME_OVER)yield break;
            Moving=false;LocalPlayer.ToggleControl(true);State=TreeStageState.WAITING_FOR_ACTIVATION;Feedback="Next challenge ready. Interact with the Book Statue to begin.";
        }
        public void HandleGameOver()
        {
            if(State==TreeStageState.STAGE_COMPLETE||State==TreeStageState.GAME_OVER)return;
            CloseBoard(false);LessonOpen=false;State=TreeStageState.GAME_OVER;timer.Pause();LocalPlayer?.ToggleControl(false);input?.SetGameplayInputBlocked(true);gameOver.ShowGameOver();ui.ShowResult(false);
        }
        public void SetLesson(bool open)
        {
            if(!Ready||Moving||Time.timeScale<=0||(boardFocus!=null&&boardFocus.Transitioning)||(State!=TreeStageState.ACTIVE&&State!=TreeStageState.WAITING_FOR_ACTIVATION))return;
            LessonOpen=open;LocalPlayer.ToggleControl(!open&&!BoardOpen);input.SetGameplayInputBlocked(open||BoardOpen);
            if(State==TreeStageState.ACTIVE){if(open)timer.Pause();else timer.Resume();}ui.ShowLesson(open);
        }
        public void Restart()=>Navigate("Trees");
        public void Return()=>Navigate("Main Menu");
        void Navigate(string scene){CloseBoard(false);Time.timeScale=1;input?.SetGameplayInputBlocked(false);TeleportManager.EnsureExists().LoadSceneWithFade(scene);}
        void ChooseVariant()
        {
            if(catalog==null||catalog.variantSets==null||ChallengeIndex>=catalog.variantSets.Count||
               catalog.variantSets[ChallengeIndex]==null||catalog.variantSets[ChallengeIndex].variants.Count!=10)
                throw new InvalidOperationException("Trees catalog needs ten variants for every challenge.");
            var variants=catalog.variantSets[ChallengeIndex].variants;
            const string keyPrefix="LogicLegends.Trees.LastVariant.";
            string key=keyPrefix+ChallengeIndex;
            int previous=PlayerPrefs.GetInt(key,-1);
            if(previous<0||previous>=variants.Count)previous=-1;
            int pick=UnityEngine.Random.Range(0,variants.Count-(previous>=0?1:0));
            if(previous>=0&&pick>=previous)pick++;
            if(variants[pick]==null)throw new InvalidOperationException("Trees catalog contains a missing challenge variant.");
            ActiveVariantIndex=pick;
            activeChallenge=variants[pick];
            PlayerPrefs.SetInt(key,pick);
            PlayerPrefs.Save();
        }
    }
}
