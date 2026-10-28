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
        public int QuestionIndex {get;private set;}
        public const int QuestionsPerChallenge = 2;
        readonly int[,] selectedVariants = new int[5, QuestionsPerChallenge];
        public int SelectedVariant(int challenge, int question) => selectedVariants[challenge,question];
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
        bool lessonInputBlocked, lessonPlayerEnabled, lessonTimerRunning, lessonCursorVisible;
        float lessonTimeScale;
        CursorLockMode lessonCursorLock;
        readonly System.Collections.Generic.List<Behaviour> lessonCameraInputs = new System.Collections.Generic.List<Behaviour>();
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
            SelectPlaythrough();ChooseVariant();board.Build(Challenge);timer.Initialize(countdownSeconds,HandleGameOver);ui.Build(this);
            while(Player.LocalInstance==null)yield return null;
            LocalPlayer=Player.LocalInstance;input=GameInput.Instance;input.SetGameplayInputBlocked(false);input.OnInteractAction+=OnInteract;
            PrelimTeleportTransition.MovePlayerImmediately(LocalPlayer.gameObject,spawnPoint.position,spawnPoint.rotation);
            var look=LocalPlayer.GetComponentInChildren<CameraTargetController>();if(look!=null)look.SetViewAngles(18,0);
            Ready=true;
            BookStatuePresentation.BindHand(bookStatue, () => isActiveAndEnabled && CanAct && !BoardOpen && NearBook && (State == TreeStageState.WAITING_FOR_ACTIVATION || State == TreeStageState.ACTIVE), InteractBook);
        }
        void OnDestroy(){if(input!=null){input.OnInteractAction-=OnInteract;if(BoardOpen)input.SetGameplayInputBlocked(inputBeforeBoard);}if(boardFocus!=null)boardFocus.Exit();}
        void OnInteract(object sender,EventArgs args){InteractBook();}
        void OnDisable() { SetLesson(false); BookStatuePresentation.SetRequired(bookStatue, false); }
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
            if(State==TreeStageState.WAITING_FOR_ACTIVATION){State=TreeStageState.ACTIVE;timer.Resume();Feedback="Work on the tree, then tap Submit. Back keeps your work.";}
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
            board.Select(id);Feedback=board.Second==null?"Selected "+board.First+(Challenge.Editable?". Select the second endpoint.":". Tap Submit."):"Selected "+board.First+" to "+board.Second+". Tap CONNECT or REMOVE.";ui.Refresh();
        }
        public void SelectAnswer(int number){if(!CanSolve||(Challenge.type!=TreeChallengeType.Depth&&Challenge.type!=TreeChallengeType.Height))return;Answer=number;Feedback="Selected "+number+". Tap Submit.";}
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
            Correct++;timer.Pause();State=TreeStageState.CHALLENGE_COMPLETE;Feedback=QuestionIndex==0?"Question 1 complete. A harder question is next.":"Challenge complete.";StartCoroutine(Advance());
        }
        public bool CanDebugClearChallenge => CanAct && State == TreeStageState.ACTIVE;
        public void DebugClearCurrentChallenge()
        {
        if (!AdminAuthorization.TryUseGameplayAdmin()) return;
            if (!CanDebugClearChallenge) return;
            Correct+=QuestionsPerChallenge-QuestionIndex; QuestionIndex=QuestionsPerChallenge-1;
            timer.Pause(); State = TreeStageState.CHALLENGE_COMPLETE;
            Feedback = "Challenge cleared by Admin."; StartCoroutine(Advance());
        }
        public void DebugClearCurrentQuestion()
        {
            if(!AdminAuthorization.TryUseGameplayAdmin()||!CanDebugClearChallenge)return;
            Correct++;timer.Pause();State=TreeStageState.CHALLENGE_COMPLETE;
            Feedback="Question cleared by Admin.";StartCoroutine(Advance());
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
            if(Correct==catalog.challenges.Count*QuestionsPerChallenge)
            {CloseBoard(false);State=TreeStageState.STAGE_COMPLETE;input.SetGameplayInputBlocked(true);ui.ShowResult(true);yield break;}
            Moving=true;
            CloseBoard(false);
            yield return TeleportManager.Instance.TeleportAndWait(LocalPlayer.gameObject,spawnPoint,()=>{if(++QuestionIndex==QuestionsPerChallenge){QuestionIndex=0;ChallengeIndex++;}Answer=null;ChooseVariant();board.Build(Challenge);ui.RebuildDiagram();});
            if(State==TreeStageState.GAME_OVER)yield break;
            Moving=false;LocalPlayer.ToggleControl(true);State=TreeStageState.WAITING_FOR_ACTIVATION;Feedback="Use the hand at the Book Statue to start Question "+(QuestionIndex+1)+" of 2.";
        }
        public void HandleGameOver()
        {
            if(State==TreeStageState.STAGE_COMPLETE||State==TreeStageState.GAME_OVER)return;
            CloseBoard(false);LessonOpen=false;State=TreeStageState.GAME_OVER;timer.Pause();LocalPlayer?.ToggleControl(false);input?.SetGameplayInputBlocked(true);gameOver.ShowGameOver();ui.ShowResult(false);
        }
        public void SetLesson(bool open)
        {
            if (open)
            {
                if (LessonOpen || !Ready || Moving || Time.timeScale <= 0 ||
                    (boardFocus != null && boardFocus.Transitioning) ||
                    (State != TreeStageState.ACTIVE && State != TreeStageState.WAITING_FOR_ACTIVATION)) return;
                lessonInputBlocked = input.GameplayInputBlocked;
                lessonPlayerEnabled = LocalPlayer.enabled;
                lessonTimerRunning = timer.Running;
                lessonTimeScale = Time.timeScale;
                lessonCursorLock = Cursor.lockState; lessonCursorVisible = Cursor.visible;
                lessonCameraInputs.Clear();
                foreach (var behaviour in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
                    if (behaviour.enabled && (behaviour is Unity.Cinemachine.CinemachineInputAxisController ||
                        behaviour is CinemachinePinchZoom || behaviour is ThirdPersonCameraController || behaviour is CameraTargetController))
                    { lessonCameraInputs.Add(behaviour); behaviour.enabled = false; }
                LessonOpen = true; timer.Pause(); LocalPlayer.ToggleControl(false); input.SetGameplayInputBlocked(true);
                Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                ui.ShowLesson(true);
                return;
            }
            if (!LessonOpen) return;
            LessonOpen = false; ui.ShowLesson(false);
            Time.timeScale = lessonTimeScale;
            input.SetGameplayInputBlocked(lessonInputBlocked); LocalPlayer.ToggleControl(lessonPlayerEnabled);
            foreach (var behaviour in lessonCameraInputs) if (behaviour != null) behaviour.enabled = true;
            lessonCameraInputs.Clear();
            if (lessonTimerRunning && State == TreeStageState.ACTIVE) timer.Resume();
            Cursor.lockState = lessonCursorLock; Cursor.visible = lessonCursorVisible;
            MobileLookInput.ResetDelta();
        }
        public void Restart() { TrialRewards.RecordRetry(10); Navigate("Trees"); }
        public void Return()=>Navigate("Main Menu");
        void Navigate(string scene){CloseBoard(false);Time.timeScale=1;input?.SetGameplayInputBlocked(false);TeleportManager.EnsureExists().LoadSceneWithFade(scene);}
        void SelectPlaythrough()
        {
            if (catalog.variantSets.Count != 5) throw new InvalidOperationException("Trees needs five question banks.");
            for (int challenge=0; challenge<5; challenge++)
            {
                var pair=TreeQuestionSelection.SelectPair(catalog.variantSets[challenge].variants,
                    PlayerPrefs.GetInt("LogicLegends.Trees.LastVariant."+challenge,-1));
                selectedVariants[challenge,0]=pair[0]; selectedVariants[challenge,1]=pair[1];
            }
        }
        void ChooseVariant()
        {
            ActiveVariantIndex=selectedVariants[ChallengeIndex,QuestionIndex];
            activeChallenge=catalog.variantSets[ChallengeIndex].variants[ActiveVariantIndex];
            PlayerPrefs.SetInt("LogicLegends.Trees.LastVariant."+ChallengeIndex,ActiveVariantIndex);
            PlayerPrefs.Save();
        }
    }
}
