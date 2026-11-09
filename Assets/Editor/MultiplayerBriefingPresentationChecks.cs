#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Rendered lobby guide checks. Does not host, join, ready, start, or grant gameplay progress.</summary>
public static class MultiplayerBriefingPresentationChecks
{
    [Serializable] public sealed class Report
    { public bool passed; public string scope = "Offline Main Menu, native VideoPlayer decoding and local lobby UI. No device or remote multiplayer claim."; public List<string> checks = new List<string>(), errors = new List<string>(); }
    static readonly Stack<IEnumerator> flow = new Stack<IEnumerator>();
    static Report report;
    static MultiplayerMatchBriefing guide;
    static bool running, hadSeen;
    static int seen;
    static double next;
    public static string Status { get; private set; } = "Idle";
    public static void Begin()
    {
        if (running || !Application.isPlaying || PhotonNetwork.InRoom || SceneManager.GetActiveScene().name != "Main Menu")
            throw new InvalidOperationException("Use offline Main Menu Play Mode.");
        var lobby = UnityEngine.Object.FindFirstObjectByType<MultiplayerLobbyUI>();
        if (lobby == null) throw new InvalidOperationException("Main Menu lobby is missing.");
        hadSeen = PlayerPrefs.HasKey(MultiplayerMatchBriefing.SeenPreference); seen = PlayerPrefs.GetInt(MultiplayerMatchBriefing.SeenPreference);
        lobby.Open(); guide = lobby.Briefing;
        report = new Report(); Directory.CreateDirectory("Temp/MultiplayerHelpChecks");
        running = true; Status = "Preparing lobby guide"; next = 0; flow.Clear(); flow.Push(Run()); EditorApplication.update += Tick;
    }
    static void Check(bool ok, string text) { report.checks.Add((ok ? "PASS " : "FAIL ") + text); if (!ok) throw new InvalidOperationException(text); }
    static IEnumerator Wait(Func<bool> ready, string text, float seconds = 10)
    { double end = EditorApplication.timeSinceStartup + seconds; while (!ready()) { if (EditorApplication.timeSinceStartup > end) throw new TimeoutException(text); yield return .04f; } }
    static void Tick()
    {
        if (!running) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode ended.");
            if (EditorApplication.timeSinceStartup < next) return;
            if (flow.Count == 0) { Finish(); return; }
            var current = flow.Peek(); if (!current.MoveNext()) { flow.Pop(); return; }
            if (current.Current is IEnumerator child) flow.Push(child); else next = EditorApplication.timeSinceStartup + (current.Current is float delay ? delay : 0);
        }
        catch (Exception e) { report.errors.Add(Status + ": " + e); Finish(); }
    }
    static IEnumerator Run()
    {
        yield return Wait(() => guide.Review(), "Guide becomes available without joining a room");
        Check(guide.Visible && guide.CurrentPage == 0, "How to Play opens on page one");
        for (int page = 0; page < MultiplayerMatchBriefing.PageCount; page++)
        {
            Status = "Decoding guide video " + (page + 1);
            var media = guide.Demonstration;
            Check(media.HasRecording, "Page " + (page + 1) + " has a bundled recording");
            yield return Wait(() => media.Player.isPrepared && media.Player.isPlaying && media.Player.frame > 2, "Native video frames decode");
            Check(media.Player.clip.name == MultiplayerBriefingVideo.ClipKeys[page], "Correct clip matches page " + (page + 1));
            Check(media.Player.targetTexture != null && media.Player.targetTexture.width == 960, "Video uses its bounded render texture");
            Check(!string.IsNullOrWhiteSpace(media.Caption), "Recorded actions have explanatory captions");
            media.PlaybackButton.onClick.Invoke(); yield return .15f;
            Check(!media.Player.isPlaying, "Pause works on page " + (page + 1));
            media.PlaybackButton.onClick.Invoke(); yield return .15f;
            Check(media.Player.isPlaying, "Play resumes on page " + (page + 1));
            media.ReplayButton.onClick.Invoke();
            yield return Wait(() => media.Player.isPrepared && media.Player.isPlaying && media.Player.frame > 0, "Replay reloads the video");
            Check(media.Player.time < 2, "Replay starts near the beginning");
            var reading = guide.ReadingScroll.GetComponent<RectTransform>(); var watching = media.GetComponent<RectTransform>();
            Check(!Box(reading).Overlaps(Box(watching)), "Reading and video panes do not overlap");
            ScreenCapture.CaptureScreenshot("Temp/MultiplayerHelpChecks/Page_" + page + ".png"); yield return .2f;
            if (page < MultiplayerMatchBriefing.PageCount - 1) guide.NextPage();
        }
        guide.PreviousPage(); Check(guide.CurrentPage == 2, "Back navigation returns to the prior video");
        guide.Dismiss(); yield return .1f;
        Check(!guide.Visible && guide.Demonstration.Player.targetTexture == null && !guide.Demonstration.Player.isPlaying, "Closing stops video and releases its texture");
        Check(guide.Review() && guide.CurrentPage == 0, "Reopening resets the guide to page one");
        yield return Wait(() => guide.Demonstration.Player.isPlaying, "Reopened video plays again");
        guide.Dismiss(); yield return .1f;
        Check(!PhotonNetwork.InRoom, "Local tutorial never joins a multiplayer room");
    }
    static Rect Box(RectTransform rect) { var c = new Vector3[4]; rect.GetWorldCorners(c); return Rect.MinMaxRect(c.Min(v => v.x), c.Min(v => v.y), c.Max(v => v.x), c.Max(v => v.y)); }
    static void Finish()
    {
        running = false; EditorApplication.update -= Tick;
        if (guide != null) guide.Dismiss();
        if (hadSeen) PlayerPrefs.SetInt(MultiplayerMatchBriefing.SeenPreference, seen); else PlayerPrefs.DeleteKey(MultiplayerMatchBriefing.SeenPreference);
        PlayerPrefs.Save(); report.passed = report.errors.Count == 0; Status = report.passed ? "Passed" : "Failed";
        File.WriteAllText("Temp/MultiplayerHelpChecks/Report.json", JsonUtility.ToJson(report, true)); flow.Clear();
    }
}
#endif
