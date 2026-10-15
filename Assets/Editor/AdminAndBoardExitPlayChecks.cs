using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LogicLegends.Induction;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in offline regression: real mouse/touch UI dispatch, board exit after practice, and shared Admin.</summary>
public static class AdminAndBoardExitPlayChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] History = { "FunctionsRelations.LastVariant.", "LogicLegends.Induction.LastVariant." };
    static Mouse mouse;
    static Touchscreen touch;
    static int touchId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool("AdminBoard.RunQA", false)) return;
        SessionState.SetBool("AdminBoard.RestorePrefs", true);
        foreach (string prefix in History) for (int i = 0; i < 5; i++)
        {
            string key = prefix + i;
            SessionState.SetBool("AdminBoard.Exists." + key, PlayerPrefs.HasKey(key));
            SessionState.SetInt("AdminBoard.Value." + key, PlayerPrefs.GetInt(key, -1));
        }
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
    }
    [InitializeOnLoadMethod]
    static void RegisterRestore() { EditorApplication.playModeStateChanged -= Restore; EditorApplication.playModeStateChanged += Restore; }
    static void Restore(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("AdminBoard.RestorePrefs", false)) return;
        foreach (string prefix in History) for (int i = 0; i < 5; i++)
        {
            string key = prefix + i;
            if (SessionState.GetBool("AdminBoard.Exists." + key, false)) PlayerPrefs.SetInt(key, SessionState.GetInt("AdminBoard.Value." + key, -1));
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save(); SessionState.SetBool("AdminBoard.RestorePrefs", false);
    }
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= Loaded;
        var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
        avatar.GetComponent<MultiplayerAvatarSync>().enabled = false;
        avatar.GetComponent<CharacterController>().enabled = true;
        string spawnName = scene.name == "Induction" ? "Induction Spawn Point" : scene.name == "Functions and Relations" ? "FR_SpawnPoint" : "Proofs Player Spawn";
        var spawn = GameObject.Find(spawnName).transform;
        PrelimTeleportTransition.MovePlayerImmediately(avatar, spawn.position, spawn.rotation);
        new GameObject("Admin Board Exit QA").AddComponent<AdminBoardExitRunner>().StartCoroutine(Run(scene.name));
    }
    static IEnumerator Run(string scene)
    {
        var view = Type.GetType("UnityEditor.GameView,UnityEditor");
        var window = EditorWindow.GetWindow(view); window.Focus();
        view.GetProperty("selectedSizeIndex", Hidden | BindingFlags.Public).SetValue(window, 2, null);
        mouse = InputSystem.AddDevice<Mouse>(); touch = InputSystem.AddDevice<Touchscreen>();
        var fr = UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>();
        var induction = UnityEngine.Object.FindFirstObjectByType<InductionStageManager>();
        var proofs = UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>();
        float until = Time.realtimeSinceStartup + 40;
        while (Time.realtimeSinceStartup < until && !(fr != null ? fr.Help != null && fr.Help.IsOpen : induction != null ? induction.help != null && induction.help.IsOpen : proofs.Presentation != null && proofs.Presentation.Help.IsOpen)) yield return null;
        yield return new WaitForSecondsRealtime(.3f);
        Check(Time.timeScale == 0, "first Help open");
        if (fr != null)
        {
            yield return Click(Field<Button>(fr.Help, "beginButton"), false);
            fr.Tutorial.SkipTutorial(); yield return null; fr.Tutorial.ConfirmYes();
        }
        else if (induction != null)
        {
            yield return Click(induction.help.BeginButton, false);
            induction.onboarding.SkipTutorial(); yield return null; induction.onboarding.ConfirmYes();
        }
        else
        {
            yield return Click(Field<Button>(proofs.Presentation.Help, "beginButton"), false);
            proofs.Tutorial.SkipTutorial(); yield return null; proofs.Tutorial.ConfirmYes();
        }
        yield return null; yield return null;
        Check(!GameInput.Instance.GameplayInputBlocked && Player.LocalInstance.enabled, "practice exit restores controls");
        if (induction == null)
        {
            foreach (int size in new[] { 2, 12 })
            {
                window.Focus(); view.GetProperty("selectedSizeIndex", Hidden | BindingFlags.Public).SetValue(window, size, null);
                Crystal.SafeArea.Sim = size == 12 ? Crystal.SafeArea.SimDevice.Pixel3XL_LSR : Crystal.SafeArea.SimDevice.None;
                yield return new WaitForSecondsRealtime(.35f);
                foreach (bool finger in new[] { false, true })
                {
                    Button open = fr != null ? fr.ViewBoardButton : proofs.Presentation.ViewButton;
                    Button back = fr != null ? fr.BoardView.backButton : proofs.Presentation.BackButton;
                    // Reproduce the state left by a tutorial explanation/skip before opening again.
                    back.interactable = false;
                    var camera = Camera.main; var pos = camera.transform.position; float fov = camera.fieldOfView;
                    yield return Click(open, finger); yield return new WaitForSecondsRealtime(1f);
                    Check(fr != null ? fr.BoardView.Viewing : proofs.Presentation.BoardView.Viewing, "View Board opens from pointer input");
                    Check(back.IsInteractable() && GameInput.Instance.GameplayInputBlocked && !Player.LocalInstance.enabled, "Back enabled while gameplay input stays blocked");
                    System.IO.Directory.CreateDirectory("Temp/AdminBoardQA");
                    ScreenCapture.CaptureScreenshot("Temp/AdminBoardQA/" + scene + "-" + Screen.width + "-Board.png");
                    yield return Click(back, finger); yield return new WaitForSecondsRealtime(1f);
                    Check(!(fr != null ? fr.BoardView.Viewing : proofs.Presentation.BoardView.Viewing) && !GameInput.Instance.GameplayInputBlocked && Player.LocalInstance.enabled, "Back pointer closes view and restores controls");
                    Check(Vector3.Distance(camera.transform.position, pos) < .4f && Mathf.Abs(camera.fieldOfView - fov) < .01f, "gameplay camera restored");
                }
            }
        }
        if (fr != null || induction != null)
        {
            var admin = UnityEngine.Object.FindFirstObjectByType<AdminDebugController>();
            Check(admin != null && admin.enabled, "existing shared Admin enabled");
            yield return Viewer(admin, fr, induction, false);
            if (fr != null) fr.ActivateChallenge();
            else
            {
                var at = induction.book.position + Vector3.forward * 2;
                PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject, at, Quaternion.LookRotation(Vector3.back));
                yield return null; induction.InteractBook();
            }
            yield return null;
            Check(fr != null ? fr.CanDebugClearChallenge : induction.CanDebugClearChallenge, "active challenge eligible");
            yield return Viewer(admin, fr, induction, true);
            yield return Click(Field<Button>(admin,"adminButton"), true); yield return null;
            var clear = Field<Button>(admin,"clearChallengeButton"); Check(clear.IsInteractable(), "Clear enabled for active challenge");
            int correct = fr != null ? fr.CorrectCount : induction.Correct;
            yield return Click(clear, true); yield return new WaitForSecondsRealtime(1.8f);
            Check(fr != null ? fr.CorrectCount == correct + 1 && fr.ChallengeIndex == 1 && fr.State == FunctionsRelationsState.WAITING_FOR_ACTIVATION : induction.Correct == correct + 1 && induction.ChallengeIndex == 1 && induction.State == InductionState.WaitingForBook, "Clear uses normal next-challenge book gate exactly once");
            float remaining = fr != null ? fr.TimeRemaining : induction.timer.Remaining;
            yield return new WaitForSecondsRealtime(.15f);
            Check(remaining == (fr != null ? fr.TimeRemaining : induction.timer.Remaining) && !GameInput.Instance.GameplayInputBlocked && Player.LocalInstance.enabled, "clear pauses timer without leaving Admin input lock");
        }
        InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(touch);
        Debug.Log("ADMIN_BOARD_EXIT_PASS " + scene + " — real mouse/touch raycasts, tutorial cleanup, mobile layouts, camera/control restore, read-only banks, normal clear progression");
    }
    static IEnumerator Viewer(AdminDebugController admin, FunctionsRelationsStageManager fr, InductionStageManager induction, bool active)
    {
        yield return Click(Field<Button>(admin,"adminButton"), false); yield return null;
        Check(Field<bool>(admin,"panelOpen") && Time.timeScale == 0, "Admin opens and pauses");
        Check(Field<Button>(admin,"clearChallengeButton").IsInteractable() == active, "clear eligibility before modal pause");
        string before = Fingerprint(fr, induction);
        var root = Field<RectTransform>(admin,"panelRoot");
        yield return Click(root.GetComponentsInChildren<Button>().Single(b=>b.name=="ViewQuestionsAndAnswers"),true); yield return null;
        var viewer = root.GetComponent<AdminAnswerViewer>(); var pages = Field<IReadOnlyList<AdminAnswerPage>>(viewer,"pages");
        Check(pages.Count == 6 && pages.Skip(1).Sum(p=>p.Body.Split(new[]{"QUESTION "},StringSplitOptions.None).Length-1)==50, "viewer contains actual current answer plus fifty bank questions");
        Check(pages.Skip(1).Sum(p=>p.Body.Split(new[]{"[SELECTED THIS PLAYTHROUGH]","[CURRENT / SELECTED]"},StringSplitOptions.None).Length-1)==5, "five current randomized variants identified");
        yield return new WaitForSecondsRealtime(.2f); Check(Fingerprint(fr, induction) == before, "viewing banks preserves time, answers, inventory and progress");
        System.IO.Directory.CreateDirectory("Temp/AdminBoardQA"); ScreenCapture.CaptureScreenshot("Temp/AdminBoardQA/" + SceneManager.GetActiveScene().name + "-Admin.png");
        yield return Click(root.GetComponentsInChildren<Button>().Single(b=>b.name=="BackToAdmin"),true); yield return null;
        Check(Time.timeScale == 0 && Field<RectTransform>(admin,"card").gameObject.activeSelf, "viewer Back returns to paused Admin");
        yield return Click(root.GetComponentsInChildren<Button>().Single(b=>b.name=="CloseAdminDebug"),false); yield return null;
        Check(Time.timeScale == 1 && !GameInput.Instance.GameplayInputBlocked && Player.LocalInstance.enabled, "Admin Close restores gameplay");
    }
    static string Fingerprint(FunctionsRelationsStageManager fr, InductionStageManager induction)
    {
        return fr != null ? fr.State+"/"+fr.ChallengeIndex+"/"+fr.CorrectCount+"/"+fr.MistakeCount+"/"+fr.TimeRemaining+"/"+string.Join(",",fr.Mapping.Snapshot())+"/"+fr.ChosenProperties :
            induction.State+"/"+induction.ChallengeIndex+"/"+induction.Correct+"/"+induction.Mistakes+"/"+induction.timer.Remaining+"/"+string.Join(",",induction.slots.Select(s=>s.Tablet==null?"empty":s.Tablet.Data.id))+"/"+string.Join(",",induction.RunVariantIndices);
    }
    static IEnumerator Click(Button button, bool finger)
    {
        yield return new WaitForEndOfFrame();
        Check(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), button != null ? button.name+" interactable" : "button exists");
        Canvas.ForceUpdateCanvases(); var rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(EventSystem.current) { position = point };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data,hits);
        Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
            button.name+" unobstructed raycast: "+string.Join(",",hits.Take(3).Select(h=>h.gameObject.name)));
        if (finger)
        {
            int id = ++touchId;
            InputSystem.QueueStateEvent(touch,new TouchState { touchId=id, position=point, phase=UnityEngine.InputSystem.TouchPhase.Began });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(touch,new TouchState { touchId=id, position=point, phase=UnityEngine.InputSystem.TouchPhase.Ended });
        }
        else
        {
            InputSystem.QueueStateEvent(mouse,new MouseState { position=point }); yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState { position=point }.WithButton(MouseButton.Left));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState { position=point });
        }
        yield return null; yield return null;
    }
    static T Field<T>(object owner,string name) => (T)owner.GetType().GetField(name,Hidden).GetValue(owner);
    static void Check(bool condition,string message) { if(!condition) throw new Exception("ADMIN_BOARD_EXIT_FAIL: "+message); }
}
public sealed class AdminBoardExitRunner : MonoBehaviour { }
