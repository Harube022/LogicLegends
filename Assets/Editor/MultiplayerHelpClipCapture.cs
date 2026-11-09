#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.OpenArchive.Topics;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Opt-in offline recordings of the real archive. All fixtures are reward-excluded and Editor-only.</summary>
public static class MultiplayerHelpClipCapture
{
    const string Folder = "Library/MultiplayerHelpCaptures";
    const int Fps = 20;
    static ArchiveTopicSession session;
    static Camera camera;
    static Behaviour brain;
    static bool brainEnabled, priorThreats, priorDefeat;
    static Vector3 cameraPosition;
    static Quaternion cameraRotation;
    static float cameraFov;
    static int oldCaptureRate;
    static MediaEncoder encoder;
    static Texture2D pixels, output;
    static RenderTexture scaled;
    static List<StageHelpVideoCatalog.Cue> cues;
    static int frame, segment;
    static MultiplayerHelpRecordingRunner runner;
    static readonly Dictionary<Behaviour, bool> disabled = new Dictionary<Behaviour, bool>();
    public static string Status { get; private set; } = "Idle";
    public static bool Running => runner != null;

    [MenuItem("Logic Legends/Help/Record Multiplayer Demonstrations (Offline Play)")]
    public static void Begin()
    {
        session = ArchiveTopicSession.Current;
        if (!Application.isPlaying || PhotonNetwork.InRoom || session == null || !session.Ready || Running)
            throw new InvalidOperationException("Use saved MultiplayerGameplay in fresh offline Play Mode.");
        Directory.CreateDirectory(Folder); camera = Camera.main;
        cameraPosition = camera.transform.position; cameraRotation = camera.transform.rotation; cameraFov = camera.fieldOfView;
        brain = camera.GetComponents<Behaviour>().FirstOrDefault(b => b.GetType().Name == "CinemachineBrain");
        brainEnabled = brain != null && brain.enabled; if (brain != null) brain.enabled = false;
        priorThreats = session.worldProgression.suppressThreatsForVerification; priorDefeat = session.suppressDefeatForVerification;
        session.worldProgression.suppressThreatsForVerification = true; session.suppressDefeatForVerification = true;
        var film = session.GetComponent<ArchiveMatchCinematic>(); if (film != null) Disable(film);
        foreach (var script in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (script.GetType().Name.EndsWith("TestControls") || script.GetType().Name.EndsWith("DevelopmentUI")) Disable(script);
        foreach (var rect in UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
            if (rect.name == "Show_Hide_Test_Tools" || rect.name == "Test_Tools_Content") rect.parent.gameObject.SetActive(false);
        foreach (var hud in UnityEngine.Object.FindObjectsByType<ArchiveCrystalHUD>(FindObjectsSortMode.None))
        { Disable(hud); var panel = hud.transform.Find("Topic_Crystal_HUD"); if (panel != null) panel.gameObject.SetActive(false); }
        var learnButton = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == "Learn_And_Hints");
        if (learnButton != null) { var group = learnButton.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false; }
        var learning = ArchiveLearningUI.Current;
        typeof(ArchiveLearningUI).GetField("onboardingDismissed", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(learning, true);
        session.RequestAdminTest(ArchiveAdminTestAction.StartMatchClock);
        oldCaptureRate = Time.captureFramerate; Time.captureFramerate = Fps;
        var view = Type.GetType("UnityEditor.GameView,UnityEditor"); if (view != null) EditorWindow.GetWindow(view).Focus();
        runner = new GameObject("Multiplayer_Help_Recording_Only").AddComponent<MultiplayerHelpRecordingRunner>();
        Status = "Preparing"; runner.StartCoroutine(Guarded());
    }
    static void Disable(Behaviour script) { if (!disabled.ContainsKey(script)) disabled[script] = script.enabled; script.enabled = false; }
    static IEnumerator Guarded()
    {
        var stack = new Stack<IEnumerator>(); stack.Push(Record()); bool ok = true;
        while (stack.Count > 0)
        {
            object yielded = null;
            try
            {
                var current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); continue; }
                if (current.Current is IEnumerator child) { stack.Push(child); continue; }
                yielded = current.Current;
            }
            catch (Exception e) { Status = "Failed: " + e; Debug.LogError(Status); ok = false; break; }
            yield return yielded;
        }
        Cleanup(); if (ok) Status = "Completed four recordings";
        File.WriteAllText(Folder + "/Status.txt", Status);
    }
    static IEnumerator Record()
    {
        yield return Hold(.5f);
        session.RequestAdminTest(ArchiveAdminTestAction.VisitTopic, 0); yield return Hold(.4f);
        var station = session.stations[0]; var book = station.transform.TransformPoint(ArchiveTopicStation.BookPosition);
        View(book + station.transform.forward * -15 + Vector3.up * 8, book + Vector3.up * 2);
        Start(0, "Read the Book to begin one shared team topic."); yield return Hold(2);
        station.points.First(p => p.role == ArchivePointRole.Book).Interact(); yield return Hold(3);
        ArchiveTopicUI.Current.Close(); Cue("Read the statement, then stand in the matching rune circle.");
        int answer = session.Question(0).correctChoice;
        var rune = station.points.First(p => p.role == ArchivePointRole.Choice && p.index == answer);
        var target = new GameObject("Tutorial_Walk_Target").transform; target.position = rune.transform.position;
        Player.LocalInstance.BeginGuidedMovement(target, 6, .35f);
        View(rune.transform.position - station.transform.forward * 17 + Vector3.up * 10, rune.transform.position + Vector3.up * 1.5f);
        yield return Hold(3); Player.LocalInstance.EndGuidedMovement(); UnityEngine.Object.Destroy(target.gameObject);
        session.Request(0, ArchiveAction.BeginChoice, answer); yield return Hold(3);
        Cue("Your team shares solved challenges. Work together to restore all ten topics."); yield return Hold(2); Finish();

        session.RequestAdminTest(ArchiveAdminTestAction.GrantAllItems, session.LocalActor);
        session.RequestAdminTest(ArchiveAdminTestAction.SpawnTeammate);
        var teammate = session.ActorPlayer(-2); if (teammate != null) Place(teammate, Player.LocalInstance.transform.position + Player.LocalInstance.transform.forward * 35);
        yield return Hold(.5f); var explorer = Player.LocalInstance;
        View(explorer.transform.position - explorer.transform.forward * 8 + Vector3.up * 4, explorer.transform.position + Vector3.up * 1.5f);
        Start(1, "Use TEAM to check your teammates. Hold PING to choose a team signal.");
        var teamwork = ArchiveTeamCoordinationUI.Current; teamwork.SetPartyPanelOpen(true); yield return Hold(2);
        teamwork.SetPartyPanelOpen(false); teamwork.TogglePingMenu(); yield return Hold(2);
        teamwork.SendPing(ArchiveTeamPingKind.HelpHere); yield return Hold(2);
        Cue("Select a hotbar item, then use its action button. You can share supplies nearby.");
        session.RequestEquipItem((int)ArchiveSurvivalItem.Flashlight); yield return Hold(2);
        session.RequestUseEquippedItem(); yield return Hold(2);
        session.RequestEquipItem((int)ArchiveSurvivalItem.Medkit); yield return Hold(2); Finish();

        session.RequestAdminTest(ArchiveAdminTestAction.TeleportToNest); yield return Hold(.5f);
        View(session.hunting.transform.position + session.hunting.transform.forward * 25 + Vector3.up * 10, session.hunting.transform.position + Vector3.up * 5);
        Start(2, "Four mistakes awaken a guardian. At seven completed topics, night brings a second guardian.");
        yield return Hold(2); session.RequestAdminTest(ArchiveAdminTestAction.PreviewNight);
        float limit = Time.realtimeSinceStartup + 25;
        while (!session.NightEntranceActive && Time.realtimeSinceStartup < limit) yield return Hold(.1f);
        while (session.NightEntranceActive && Time.realtimeSinceStartup < limit) yield return Hold(.1f);
        Cue("Prepare your flashlight before dark. Keep moving and help hunted teammates.");
        session.RequestAdminTest(ArchiveAdminTestAction.SleepMonster); session.RequestAdminTest(ArchiveAdminTestAction.SleepMonster, 1);
        session.RequestEquipItem((int)ArchiveSurvivalItem.Flashlight); session.RequestUseEquippedItem(); yield return Hold(3); Finish();

        session.RequestAdminTest(ArchiveAdminTestAction.ReturnDaylight); yield return Hold(.5f);
        session.RequestAdminTest(ArchiveAdminTestAction.PreviewBarrier);
        yield return Until(() => ArchiveLearningUI.Current.BarrierVisible, 8);
        Start(3, "Captured? You have two minutes to solve a DM1 escape challenge. Your teammates remain free."); yield return Hold(3);
        FindButton("Barrier_Next_Hint").onClick.Invoke(); Cue("Use a hint, choose an answer, then confirm when ready."); yield return Hold(3);
        FindButton("Return_To_Question").onClick.Invoke();
        var problem = session.BarrierProblem(); int correct = Enumerable.Range(0, problem.options.Length).First(i => problem.Validate(i, out _));
        FindButton("Barrier_Choice_" + correct).onClick.Invoke(); yield return Hold(2);
        FindButton("Confirm_Barrier_Answer").onClick.Invoke(); yield return Hold(2);
        if (session.State.barrier.outcome != 1) throw new InvalidOperationException("Recorded answer did not pass the existing validator.");
        session.RequestAdminTest(ArchiveAdminTestAction.ReleaseCapture); yield return Hold(.5f);
        Cue("Zero hearts? A teammate can bring a revival kit to rescue you at the nest.");
        session.RequestAdminTest(ArchiveAdminTestAction.DownAtNest, -2); yield return Hold(2);
        teammate = session.ActorPlayer(-2);
        if (teammate != null)
        {
            Place(Player.LocalInstance, teammate.transform.position + Vector3.right * 2);
            View(teammate.transform.position + new Vector3(7, 7, -10), teammate.transform.position + Vector3.up);
            session.RequestEquipItem((int)ArchiveSurvivalItem.RevivalKit); yield return Hold(2);
            if (!session.RequestUseEquippedItem(-2)) throw new InvalidOperationException("Revival kit demo was not accepted.");
            yield return Hold(3);
        }
        session.RequestAdminTest(ArchiveAdminTestAction.OpenPortal); yield return Hold(2);
        Cue("Complete all ten topics. Every surviving teammate must enter the portal before escape time ends.");
        if (session.exitPortal != null) View(session.exitPortal.Center + Vector3.back * 55 + Vector3.up * 14, session.exitPortal.Center + Vector3.up * 6);
        yield return Hold(4); Finish();
    }
    static Button FindButton(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(b => b.name == name);
    static void Place(Player player, Vector3 position)
    { var cc = player.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false; player.transform.position = position; if (cc != null) cc.enabled = true; player.ResetMotionAfterTeleport(); }
    static void View(Vector3 eye, Vector3 target) { camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye)); camera.fieldOfView = 55; }
    static void Start(int index, string caption)
    {
        segment = index; frame = 0; cues = new List<StageHelpVideoCatalog.Cue>(); Cue(caption);
        Status = "Recording " + MultiplayerBriefingVideo.ClipKeys[index];
        encoder = new MediaEncoder(Path.GetFullPath(Folder + "/" + MultiplayerBriefingVideo.ClipKeys[index] + ".mp4"),
            new VideoTrackAttributes { frameRate = new MediaRational(Fps), width = 1280, height = 720, includeAlpha = false, bitRateMode = VideoBitrateMode.Medium });
        if (pixels == null)
        {
            pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
            output = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            scaled = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32); scaled.Create();
        }
    }
    static void Cue(string caption) => cues.Add(new StageHelpVideoCatalog.Cue { seconds = frame / (float)Fps, caption = caption });
    static IEnumerator Hold(float seconds)
    {
        for (int i = 0; i < Mathf.CeilToInt(seconds * Fps); i++)
        {
            yield return new WaitForEndOfFrame();
            if (encoder == null) continue;
            pixels.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); pixels.Apply(false);
            var previous = RenderTexture.active; Graphics.Blit(pixels, scaled); RenderTexture.active = scaled;
            output.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); output.Apply(false); RenderTexture.active = previous;
            if (!encoder.AddFrame(output)) throw new InvalidOperationException("Native video encoding failed."); frame++;
        }
    }
    static IEnumerator Until(Func<bool> ready, float seconds)
    { float deadline = Time.realtimeSinceStartup + seconds; while (!ready()) { if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Capture fixture not ready"); yield return Hold(.1f); } }
    static void Finish()
    {
        encoder.Dispose(); encoder = null;
        File.WriteAllText(Folder + "/" + MultiplayerBriefingVideo.ClipKeys[segment] + ".json", JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {
            scene = "MultiplayerGameplay", source = "Current offline Unity gameplay demonstration. Editor-only reward-excluded fixtures stage supplies, teammate, night, capture and portal; no live multiplayer session or academic completion is represented.", cues = cues.ToArray() }, true));
        ScreenCapture.CaptureScreenshot(Folder + "/Review_" + segment + ".png");
    }
    static void Cleanup()
    {
        encoder?.Dispose(); encoder = null; Time.captureFramerate = oldCaptureRate;
        if (session != null) { session.worldProgression.suppressThreatsForVerification = priorThreats; session.suppressDefeatForVerification = priorDefeat; }
        foreach (var entry in disabled) if (entry.Key != null) entry.Key.enabled = entry.Value; disabled.Clear();
        if (camera != null) { camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation); camera.fieldOfView = cameraFov; }
        if (brain != null) brain.enabled = brainEnabled;
        if (pixels != null) UnityEngine.Object.Destroy(pixels); if (output != null) UnityEngine.Object.Destroy(output);
        if (scaled != null) { scaled.Release(); UnityEngine.Object.Destroy(scaled); }
        pixels = output = null; scaled = null;
        if (runner != null) UnityEngine.Object.Destroy(runner.gameObject); runner = null;
    }
}
public sealed class MultiplayerHelpRecordingRunner : MonoBehaviour { }
#endif
