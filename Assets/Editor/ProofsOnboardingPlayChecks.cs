using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in, offline onboarding and all-five-challenge regression checks; no account writes.</summary>
public static class ProofsOnboardingPlayChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool("Proofs.OnboardingQA", false)) return;
        SessionState.SetBool("Proofs.PrefsCaptured", true);
        for (int c = 0; c < 5; c++)
        {
            string key = "LogicLegends.ProofsLibrary.LastVariant." + c;
            SessionState.SetBool("Proofs.PrefExists." + c, PlayerPrefs.HasKey(key));
            SessionState.SetInt("Proofs.PrefValue." + c, PlayerPrefs.GetInt(key, -1));
        }
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
    }
    [InitializeOnLoadMethod]
    static void RegisterRestore()
    {
        EditorApplication.playModeStateChanged -= RestorePrefs; EditorApplication.playModeStateChanged += RestorePrefs;
    }
    static void RestorePrefs(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        if (!SessionState.GetBool("Proofs.PrefsCaptured", false)) return;
        for (int c = 0; c < 5; c++)
        {
            string key = "LogicLegends.ProofsLibrary.LastVariant." + c;
            if (SessionState.GetBool("Proofs.PrefExists." + c, false)) PlayerPrefs.SetInt(key, SessionState.GetInt("Proofs.PrefValue." + c, -1));
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save(); SessionState.SetBool("Proofs.PrefsCaptured", false);
    }
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Formal vs. Informal Proofs") return;
        SceneManager.sceneLoaded -= Loaded;
        var player = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
        var sync = player.GetComponent<MultiplayerAvatarSync>(); if (sync != null) sync.enabled = false;
        player.GetComponent<CharacterController>().enabled = true;
        var spawn = GameObject.Find("Proofs Player Spawn"); if (spawn != null) player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
        ProofsLibraryStageController.SuppressProgressionForValidation = true;
        new GameObject("Proofs QA Runner").AddComponent<ProofsQaRunner>().StartCoroutine(Run());
    }
    static IEnumerator Run()
    {
        int size = SessionState.GetInt("Proofs.CheckSize", 2);
        var type = Type.GetType("UnityEditor.GameView,UnityEditor"); type.GetProperty("selectedSizeIndex", BindingFlags.Public | Hidden).SetValue(EditorWindow.GetWindow(type), size, null);
        Crystal.SafeArea.Sim = size == 4 ? Crystal.SafeArea.SimDevice.iPhoneX : size == 12 ? Crystal.SafeArea.SimDevice.Pixel3XL_LSR : Crystal.SafeArea.SimDevice.None;
        float until = Time.unscaledTime + 45;
        ProofsStageUI ui = null;
        while (Time.unscaledTime < until)
        {
            ui = UnityEngine.Object.FindFirstObjectByType<ProofsStageUI>();
            if (ui != null && ui.Help != null && ui.Help.IsOpen) break;
            yield return null;
        }
        Check(ui != null && ui.Help.IsOpen && Time.timeScale == 0, "first-entry Help pauses input and time");
        var stage = ui.Stage; var help = ui.Help; float time = stage.SecondsLeft; string originalBoard = stage.BoardInstructions.text;
        Check(time == stage.StartingSeconds && !stage.Started && Field<ProofChallengeData>(stage, "activeChallenge") == null, "challenge waits before tutorial");
        string taught = "";
        for (int page = 0; page < help.PageCount; page++)
        {
            yield return null; yield return new WaitForEndOfFrame();
            var title = Field<TextMeshProUGUI>(help, "titleText"); var body = Field<TextMeshProUGUI>(help, "bodyText");
            title.ForceMeshUpdate(); body.ForceMeshUpdate(); taught += title.text + body.text;
            Check(!title.isTextOverflowing, "Help title fits mobile");
            if (page == 0) yield return Capture("Help");
            if (page < help.PageCount - 1) Click(Field<Button>(help, "nextButton"));
        }
        Check(taught.Contains("Informal does not mean incorrect") && taught.Contains("confirm") && taught.Contains("10:00") && taught.Contains("10 seconds") && taught.Contains("Challenge 5"), "Help describes actual proof kinds, timer and book confirmation");
        Click(Field<Button>(help, "beginButton")); yield return null;
        var tutorial = ProofsControlsOnboarding.Active; Check(tutorial != null && tutorial.CurrentStep == ProofsTutorialStep.Movement, "Let's Begin starts movement");
        var mobile = MobileInputUI.FindJoystick();
        SetMobile(mobile, new Vector2(.1f, 0)); yield return new WaitForSecondsRealtime(.9f);
        Check(tutorial.CurrentStep == ProofsTutorialStep.Movement, "joystick jitter does not advance");
        SetMobile(mobile, Vector2.right);
        until = Time.unscaledTime + 5;
        while (tutorial.CurrentStep == ProofsTutorialStep.Movement && Time.unscaledTime < until) yield return null;
        SetMobile(mobile, Vector2.zero);
        Check(tutorial.CurrentStep == ProofsTutorialStep.Interaction, "intentional movement advances only to hand interaction");
        yield return Layout(tutorial, "Hand");
        Check(tutorial.PracticeObject != null, "visible labeled practice object exists");
        var p = Player.LocalInstance; var delta = tutorial.PracticeObject.transform.position - p.transform.position; delta.y = 0; p.transform.rotation = Quaternion.LookRotation(delta);
        var hand = GameObject.Find("Canvas 1").GetComponentsInChildren<Button>(true).Single(b => b.name == "InteractButton");
        Click(hand); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.Jump && tutorial.PracticeObject == null, "hand practice advances only to jump");
        yield return Layout(tutorial, "Jump");
        until = Time.unscaledTime + 2;
        while (!p.GetComponent<CharacterController>().isGrounded && Time.unscaledTime < until) yield return null;
        var jump = GameObject.Find("Canvas 1").GetComponentsInChildren<Button>(true).Single(b => b.name == "JumpButton"); Click(jump);
        until = Time.unscaledTime + 3; while (tutorial.CurrentStep == ProofsTutorialStep.Jump && Time.unscaledTime < until) yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.Timer, "successful jump advances to timer");
        yield return Layout(tutorial, "Timer"); Next(tutorial); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.Progress, "progress explained separately");
        yield return Layout(tutorial, "Progress"); Next(tutorial); yield return null;
        yield return Layout(tutorial, "Book"); Click(ui.InteractionButton); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.FormalAltar, "practice book interaction leaves real challenge waiting");
        yield return Layout(tutorial, "FormalAltar"); Next(tutorial); yield return null;
        yield return Layout(tutorial, "InformalAltar"); Next(tutorial); yield return null;
        yield return Layout(tutorial, "Scroll"); Next(tutorial); yield return null;
        yield return Layout(tutorial, "ReadScroll"); Click(hand); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.Reader && stage.ReaderPanel.activeSelf, "hand opens actual reader with practice text");
        yield return Layout(tutorial, "Reader"); Click(stage.ReaderCloseButton); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.ReadScroll, "Close does not collect practice scroll");
        yield return new WaitForSecondsRealtime(.75f); Click(ui.InteractionButton); yield return null;
        yield return Layout(tutorial, "ReaderCollect"); Click(stage.CollectButton); yield return null;
        Check(tutorial.PracticeCarried && stage.CarriedScroll == -1, "Carry Scroll affects only tutorial inventory");
        yield return Layout(tutorial, "Place"); Click(hand); yield return null;
        Check(tutorial.PracticePlacement == 1 && Enumerable.Range(0,4).All(i => stage.Placement(i) == -1), "practice placement leaves real altars empty");
        yield return Layout(tutorial, "TakeBack"); Click(ui.InteractionButton); yield return null;
        yield return Layout(tutorial, "TakeBackPicker"); Click(stage.TakeBackButtons[0]); yield return null;
        Check(tutorial.PracticeCarried && tutorial.PracticePlacement == -1, "existing take-back UI retrieves only practice scroll");
        yield return Layout(tutorial, "Replace"); Click(hand); yield return null;
        yield return Layout(tutorial, "ConfirmBook"); Click(ui.InteractionButton); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.ViewBoard, "practice confirmation never evaluates real arrangement");
        yield return Layout(tutorial, "ViewBoard"); Click(ui.ViewButton); yield return new WaitForSecondsRealtime(.95f);
        Check(ui.BoardView.Viewing && tutorial.CurrentStep == ProofsTutorialStep.Board && !ui.BackButton.interactable, "View Board blends and requires explanation before Back");
        yield return Layout(tutorial, "Board"); CheckBoardFrame(ui); Next(tutorial); yield return null;
        yield return Layout(tutorial, "BoardBack"); Click(ui.BackButton); yield return new WaitForSecondsRealtime(.95f);
        Check(tutorial.CurrentStep == ProofsTutorialStep.Confirm && !ui.BoardView.Viewing, "Back restores camera and reaches confirmation");
        CheckFrozen(stage, time); Click(tutorial.NoButton); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.Movement, "No repeats tutorial from movement");
        var skip = ui.SafeArea.GetComponentsInChildren<Button>(true).Single(b => b.name == "PRELIM Shared Skip Tutorial");
        Click(skip); yield return null; Click(tutorial.NoButton); yield return null;
        // Interrupt both a guided camera and the board's own coroutine.
        Invoke(tutorial, "StartCamera");
        Invoke(tutorial, "ShowStep", ProofsTutorialStep.ViewBoard, "Practice View Board", (RectTransform)ui.ViewButton.transform, false, null);
        yield return new WaitForSecondsRealtime(.75f); Click(ui.ViewButton); Click(skip); yield return null;
        Check(!ui.BoardView.Viewing && !ui.BoardView.Transitioning && tutorial.CurrentStep == ProofsTutorialStep.Confirm, "Skip cancels camera transition");
        Click(tutorial.YesButton); yield return null; yield return null;
        Check(ProofsControlsOnboarding.Active == null && !stage.OnboardingPending && p.enabled && !GameInput.Instance.GameplayInputBlocked, "Yes restores controls and prepares book gate");
        CheckFrozen(stage, time); Check(stage.BoardInstructions.text == originalBoard && GameObject.Find("Proofs Tutorial Practice Content") == null && GameObject.Find("Proofs Practice Object") == null, "all practice content removed");
        Click(help.HelpButton); yield return null; Check(help.IsOpen, "Help reopens"); Click(Field<Button>(help,"beginButton")); yield return null;
        Check(!help.IsOpen && ProofsControlsOnboarding.Active == null && !stage.Started, "reopened guide resumes without repeating or activating");
        Click(ui.ViewButton); yield return new WaitForSecondsRealtime(.95f); CheckBoardFrame(ui);
        Check(!stage.Started && stage.SecondsLeft == time, "ordinary board view does not start or submit"); Click(ui.BackButton); yield return new WaitForSecondsRealtime(.95f);
        for (int challenge = 1; challenge <= 5; challenge++)
        {
            Move(p, stage.BookTarget.position + Vector3.back * 2); yield return null;
            stage.InteractNearby(); yield return null;
            Check(stage.Started && stage.ChallengeNumber == challenge, "book starts real challenge " + challenge);
            var q = Field<ProofChallengeData>(stage, "activeChallenge");
            if (q.kind == ProofChallengeKind.SortScrolls)
            {
                stage.ReadScroll(0); yield return new WaitForSecondsRealtime(.15f);
                Check(stage.ReaderPanel.activeSelf && stage.ReaderBody.text == q.scrolls[0].content, "real reader restored after tutorial");
                Click(stage.CollectButton); yield return null; Check(stage.CarriedScroll == 0, "real collect still works");
                stage.PlaceOnPedestal(q.scrolls[0].isFormal ? 0 : 1);
                for (int i = 1; i < q.scrolls.Length; i++) { stage.ReadScroll(i); Click(stage.CollectButton); stage.PlaceOnPedestal(q.scrolls[i].isFormal ? 0 : 1); }
                if (challenge == 1)
                {
                    int correct = q.scrolls[0].isFormal ? 0 : 1; stage.RetrieveFromPedestal(correct); stage.TakeBackScroll(0); stage.PlaceOnPedestal(1-correct);
                    int[] before = Enumerable.Range(0,4).Select(stage.Placement).ToArray(); float beforeTime = stage.SecondsLeft;
                    stage.Submit(); Check(Mathf.Abs(beforeTime - stage.SecondsLeft - stage.WrongPenalty) < .001f && before.SequenceEqual(Enumerable.Range(0,4).Select(stage.Placement)), "incorrect submission preserves placements and penalty");
                    yield return new WaitForSecondsRealtime(.85f); stage.RetrieveFromPedestal(1-correct); stage.TakeBackScroll(0); stage.PlaceOnPedestal(correct);
                    Click(help.HelpButton); yield return null; float paused = stage.SecondsLeft; yield return new WaitForSecondsRealtime(.25f);
                    Check(help.IsOpen && stage.Started && stage.SecondsLeft == paused, "Help pauses active challenge without clearing it"); Click(Field<Button>(help,"beginButton")); yield return null;
                    float readingStart = stage.SecondsLeft; Click(ui.ViewButton); yield return new WaitForSecondsRealtime(.95f);
                    Check(stage.Started && stage.SecondsLeft < readingStart, "board viewing preserves active countdown rules"); Click(ui.BackButton); yield return new WaitForSecondsRealtime(.95f);
                }
            }
            else Arrange(stage.WorldBoard, q);
            stage.Submit(); yield return new WaitForSecondsRealtime(.95f);
            Check(!stage.Started && stage.CorrectCount == challenge, "correct answer pauses and progresses " + challenge);
        }
        Check(stage.Finished && stage.Presentation.Progress.text.Contains("5 of 5"), "existing stage results remain connected");
        Debug.Log("PROOFS_ONBOARDING_PLAY_PASS " + Screen.width + "x" + Screen.height + " Help / movement-hand-jump / isolated reader and take-back / skip-restart / camera-Back / shared timer / all five validations");
    }
    static void Arrange(ProofWorldBoardController board, ProofChallengeData q)
    {
        var options = Field<ProofWorldPedestal[]>(board, "statementTablets"); var rules = Field<ProofWorldPedestal[]>(board, "ruleTablets");
        var slots = Field<ProofWorldPedestal[]>(board, "statementSlots"); var ruleSlots = Field<ProofWorldPedestal[]>(board, "ruleSlots");
        var stepIds = Field<int[]>(board, "optionToStep"); var ruleIds = Field<int[]>(board, "optionToRule");
        if (q.kind == ProofChallengeKind.MissingRule)
        { int id = Array.IndexOf(q.ruleOptions, q.expectedRule); board.Interact(rules[Array.IndexOf(ruleIds,id)]); board.Interact(ruleSlots[0]); }
        else for (int i = 0; i < q.steps.Length; i++)
        {
            int id = q.acceptableOrders[0].stepIds[i]; board.Interact(options[Array.IndexOf(stepIds,id)]); board.Interact(slots[i]);
            if (q.kind == ProofChallengeKind.Formalize) { board.Interact(rules[Array.IndexOf(ruleIds,id)]); board.Interact(ruleSlots[i]); }
        }
        Check(board.IsComplete() && board.IsCorrect(), "actual world-board validator accepts authored solution");
    }
    static IEnumerator Layout(ProofsControlsOnboarding t, string name)
    {
        yield return new WaitForSecondsRealtime(.85f); yield return new WaitForEndOfFrame();
        var text = Field<TMP_Text>(t,"instruction"); text.ForceMeshUpdate();
        Check(!text.isTextOverflowing, "instructions fit: " + name);
        var panel = Field<RectTransform>(t,"instructions"); var corners = new Vector3[4]; panel.GetWorldCorners(corners);
        Check(corners.All(c=>c.x>=-1 && c.y>=-1 && c.x<=Screen.width+1 && c.y<=Screen.height+1), "instructions stay on mobile screen: " + name);
        Check(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Count(b=>b.name=="PRELIM Shared Skip Tutorial") == 1, "one shared Skip control");
        yield return Capture(name);
    }
    static IEnumerator Capture(string name)
    {
        System.IO.Directory.CreateDirectory("Temp/ProofsOnboardingQA"); ScreenCapture.CaptureScreenshot("Temp/ProofsOnboardingQA/"+Screen.width+"-"+name+".png"); yield return null; yield return new WaitForEndOfFrame();
    }
    static void CheckBoardFrame(ProofsStageUI ui)
    {
        var b=ui.BoardView.BoardBounds; var camera=Camera.main;
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
        { var point=camera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,0))); Check(point.z>0 && point.x>=0 && point.x<=1 && point.y>=0 && point.y<=1,"complete board framed on mobile"); }
    }
    static void CheckFrozen(ProofsLibraryStageController stage,float time) => Check(!stage.Started && stage.SecondsLeft==time && stage.ChallengeNumber==1 && stage.CorrectCount==0 && stage.MistakeCount==0 && stage.CarriedScroll==-1 && Field<ProofChallengeData>(stage,"activeChallenge")==null && Enumerable.Range(0,4).All(i=>stage.Placement(i)==-1), "practice never changes real game state");
    static void SetMobile(MobileInputUI ui,Vector2 v){typeof(MobileInputUI).GetField("inputVector",Hidden).SetValue(ui,v); GameInput.Instance.SetMobileMovement(v);}
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden).GetValue(o);
    static void Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Hidden).Invoke(o,args);
    static void Next(ProofsControlsOnboarding t)=>Click(Field<Button>(t,"next"));
    static void Click(Button button){Check(button!=null && button.gameObject.activeInHierarchy && button.interactable,"button available: "+(button!=null?button.name:"null"));button.onClick.Invoke();}
    static void Check(bool okay,string message){if(!okay)throw new InvalidOperationException("PROOFS_QA: "+message);}
    static void Move(Player p,Vector3 point){var cc=p.GetComponent<CharacterController>();cc.enabled=false;point.y=.9f;p.transform.SetPositionAndRotation(point,Quaternion.LookRotation(Vector3.forward));cc.enabled=true;Physics.SyncTransforms();}
}
public sealed class ProofsQaRunner:MonoBehaviour{}
