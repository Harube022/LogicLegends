using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Topics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in lobby-only checks; never connects, starts a match or drives a second timer.</summary>
public static class MultiplayerBriefingChecks
{
    [Serializable] sealed class Report
    {
        public bool passed;
        public string scope = "Native Main Menu lobby Play Mode. Reviews production briefing pages, navigation, visible scrollbar, local seen preference and unchanged match/session state. Physical Android layout is unverified.";
        public List<string> checks = new List<string>(), errors = new List<string>();
    }
    public static string Status { get; private set; } = "Idle";
    static void Check(Report report, bool pass, string message)
    { report.checks.Add((pass ? "PASS " : "FAIL ") + message); if (!pass) throw new InvalidOperationException(message); }

    [MenuItem("Tools/Logic Legends/Multiplayer/Test Lobby Match Briefing")]
    public static void Run()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "Main Menu") throw new InvalidOperationException("Use Main Menu Play Mode with the multiplayer lobby open.");
        var lobby = UnityEngine.Object.FindFirstObjectByType<MultiplayerLobbyUI>(FindObjectsInactive.Include);
        if (lobby == null || !lobby.IsOpen || lobby.Briefing == null) throw new InvalidOperationException("Open the existing multiplayer lobby first.");
        var report = new Report(); var briefing = lobby.Briefing;
        bool hadPreference = PlayerPrefs.HasKey(MultiplayerMatchBriefing.SeenPreference);
        int savedPreference = PlayerPrefs.GetInt(MultiplayerMatchBriefing.SeenPreference, 0);
        string teamCheckpoint = ArchiveTopicSession.Current != null && ArchiveTopicSession.Current.Ready ? JsonUtility.ToJson(ArchiveTopicSession.Current.State) : "";
        bool room = Photon.Pun.PhotonNetwork.InRoom;
        string roomName = room ? Photon.Pun.PhotonNetwork.CurrentRoom.Name : "";
        float scale = Time.timeScale;
        try
        {
            Status = "Checking lobby briefing";
            Check(report, MultiplayerMatchBriefing.PageCount == 4, "Four concise orientation pages are available");
            var bodies = Enumerable.Range(0, MultiplayerMatchBriefing.PageCount).Select(MultiplayerMatchBriefing.PageBody).ToArray();
            Check(report, bodies.All(s => !string.IsNullOrWhiteSpace(s)) && bodies.Distinct().Count() == 4, "All pages have distinct readable instructions");
            Check(report, bodies[0].Contains("40 minutes") && bodies[0].Contains("+2 minutes") && bodies[0].Contains("Only one topic"), "Briefing explains existing shared timer, one-time topic bonuses and one active topic");
            Check(report, bodies[1].Contains("Meaningful contributions") && bodies[1].Contains("give items") && bodies[1].Contains("revival kit"), "Briefing explains contributor crystals and teammate item support");
            Check(report, bodies[2].Contains("Four mistakes") && bodies[2].Contains("seven completed topics") && bodies[2].Contains("fewer than four mistakes"), "Night phase explains its independent awakening rule");
            Check(report, bodies[3].Contains("2 minutes") && bodies[3].Contains("zero hearts") && bodies[3].Contains("Every surviving teammate"), "Capture, rescue and the all-survivors escape rule are covered");
            Check(report, briefing.EntryButton != null && briefing.EntryButton.gameObject.activeInHierarchy, "Existing lobby offers a HOW TO PLAY action");
            Check(report, briefing.Review() && briefing.Visible && briefing.CurrentPage == 0, "Review opens at the first page without starting gameplay");
            Check(report, briefing.ReadingScroll != null && briefing.ReadingScroll.vertical && !briefing.ReadingScroll.horizontal && briefing.VisibleScrollbar != null && briefing.ReadingScroll.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.Permanent, "Instructions are vertically scrollable with a permanent visible scrollbar");
            briefing.NextPage(); briefing.NextPage();
            Check(report, briefing.CurrentPage == 2 && briefing.CurrentBody == MultiplayerMatchBriefing.PageBody(2), "Next shows the correct authored page");
            briefing.PreviousPage();
            Check(report, briefing.CurrentPage == 1, "Back preserves the page navigation sequence");
            briefing.Dismiss();
            Check(report, !briefing.Visible && briefing.HasSeen, "Skip closes immediately and remembers the local acknowledgement");
            Check(report, briefing.Review() && briefing.CurrentPage == 0, "A previously acknowledged briefing can still be reviewed");
            for (int i = 0; i < MultiplayerMatchBriefing.PageCount; i++) briefing.NextPage();
            Check(report, !briefing.Visible && briefing.HasSeen, "Got it closes the last page without a mandatory task");
            Check(report, Time.timeScale == scale && Photon.Pun.PhotonNetwork.InRoom == room && (!room || Photon.Pun.PhotonNetwork.CurrentRoom.Name == roomName), "Review does not alter time scale or room membership");
            string after = ArchiveTopicSession.Current != null && ArchiveTopicSession.Current.Ready ? JsonUtility.ToJson(ArchiveTopicSession.Current.State) : "";
            Check(report, teamCheckpoint == after, "Review never edits a gameplay checkpoint or match timer");
            report.passed = true; Status = "Passed";
        }
        catch (Exception e) { report.errors.Add(e.ToString()); Status = "Failed"; }
        finally
        {
            briefing.Dismiss();
            if (hadPreference) PlayerPrefs.SetInt(MultiplayerMatchBriefing.SeenPreference, savedPreference); else PlayerPrefs.DeleteKey(MultiplayerMatchBriefing.SeenPreference);
            PlayerPrefs.Save();
            Directory.CreateDirectory("Temp/ArchiveTeamCoordination");
            File.WriteAllText("Temp/ArchiveTeamCoordination/Briefing.json", JsonUtility.ToJson(report, true));
        }
        Debug.Log("Lobby match briefing checks: " + Status + " (" + report.checks.Count + ").");
    }
}
