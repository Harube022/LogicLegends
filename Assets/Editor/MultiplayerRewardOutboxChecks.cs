using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LogicLegends.OpenArchive.Topics;
using UnityEditor;
using UnityEngine;

/// <summary>Fake-account receipt checks; no real wallet, payment or authentication requests.</summary>
public static class MultiplayerRewardOutboxChecks
{
    [Serializable] sealed class Report
    {
        public bool passed;
        public string scope = "Editor Play with fake authenticated identities and fake account/reward transports. Exercises the production account-scoped durable outbox, immutable payloads, retry and runtime pump. No real account, deployed reward endpoint, or physical-device claim.";
        public List<string> checks = new List<string>(), errors = new List<string>();
    }
    [Serializable] sealed class FakeReply { public bool success = true; public long status = 200; public string body; }
    const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    const string UidA = "multiplayer-reward-qa-a", UidB = "multiplayer-reward-qa-b", Prefix = "MultiplayerRewardOutbox.v1.";
    static Task running;
    public static string Status { get; private set; } = "Idle";
    static FieldInfo BackendField(string name) => typeof(MultiplayerRewardBackendClient).GetField(name, StaticPrivate);
    static FieldInfo CommerceField(string name) => typeof(CommerceClient).GetField(name, StaticPrivate);
    static void Identity(string uid)
    {
        BackendField("ValidationUid").SetValue(null, uid);
        CommerceField("ValidationUid").SetValue(null, uid);
    }
    static void Transport(Func<string, string, Task<string>> transport) => BackendField("ValidationTransport").SetValue(null, transport);
    static IDictionary Stores => (IDictionary)typeof(MultiplayerRewardOutbox).GetField("stores", StaticPrivate).GetValue(null);
    static void Forget(string uid) => Stores.Remove(uid);
    static void RetryNow(string uid, string matchId)
    {
        object store = Stores[uid];
        var entries = (IEnumerable)store.GetType().GetField("entries").GetValue(store);
        foreach (object entry in entries)
            if ((string)entry.GetType().GetField("matchId").GetValue(entry) == matchId)
                entry.GetType().GetField("nextSync").SetValue(entry, 0f);
    }
    static ArchiveMatchRewardReport Match(string id = null) => new ArchiveMatchRewardReport
    {
        version = 1, matchId = id ?? Guid.NewGuid().ToString("N"), actor = 1, online = true, outcome = "victory",
        participants = new[] { 1, 2 }, completedTopics = Enumerable.Range(0, 10).ToArray(), restoredTopics = Enumerable.Range(0, 10).ToArray(),
        participationKeys = new[] { "work:0:0:0", "work:0:1:0", "work:0:2:0" }
    };
    static MultiplayerRewardBackendClient.Receipt Paid(string id) => new MultiplayerRewardBackendClient.Receipt
    {
        matchId = id, status = "completed", reward = 1000, baseCoins = 600, crystalCoins = 250, participationCoins = 150,
        coins = 1000, gems = 2, gemReward = 2, tier = 3
    };
    static string Json(MultiplayerRewardBackendClient.Receipt receipt) => JsonUtility.ToJson(receipt);
    static void Check(Report report, bool passed, string label)
    {
        report.checks.Add((passed ? "PASS " : "FAIL ") + label);
        if (!passed) throw new InvalidOperationException(label);
    }
    static async Task Until(Func<bool> ready, string label)
    {
        double deadline = EditorApplication.timeSinceStartup + 5;
        while (!ready())
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(label);
            EditorApplication.QueuePlayerLoopUpdate(); await Task.Delay(20);
        }
    }
    [MenuItem("Logic Legends/Multiplayer/Run Reward Outbox Checks (Play Mode)")]
    public static void Begin()
    {
        if (!Application.isPlaying || running != null && !running.IsCompleted)
            throw new InvalidOperationException("Use fresh Editor Play with the reward pump active.");
        running = Run();
    }
    static async Task Run()
    {
        var report = new Report(); Status = "Running";
        var savedBackendTransport = BackendField("ValidationTransport").GetValue(null);
        var savedBackendUid = BackendField("ValidationUid").GetValue(null);
        var savedCommerceTransport = CommerceField("ValidationRequest").GetValue(null);
        var savedCommerceUid = CommerceField("ValidationUid").GetValue(null);
        var savedLobbyService = MultiplayerLobbyService.Instance;
        string savedLobbyNotice = savedLobbyService?.Status;
        var savedPreferences = new Dictionary<string, string>();
        foreach (string uid in new[] { UidA, UidB })
        {
            string key = Prefix + uid; savedPreferences[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            PlayerPrefs.DeleteKey(key); Forget(uid);
        }
        int notifications = 0; Action walletChanged = () => notifications++;
        CommerceClient.Changed += walletChanged;
        try
        {
            CommerceField("ValidationRequest").SetValue(null, new Func<string, string, string, int, Task<string>>((url, body, token, timeout) =>
                Task.FromResult(JsonUtility.ToJson(new FakeReply { body = "{\"coins\":1000,\"gems\":2,\"sandbox\":false,\"live\":true,\"inventory\":[]}" }))));
            Identity(UidA); int calls = 0; var match = Match(); string firstPayload = null, replayPayload = null;
            Transport((path, json) =>
            {
                calls++; firstPayload = json;
                if (path != "/multiplayer/complete") throw new InvalidOperationException("Unexpected endpoint.");
                return Task.FromException<string>(new TimeoutException("Fixture: server committed but response was lost."));
            });
            Check(report, MultiplayerRewardOutbox.Queue(UidA, match), "A terminal report can be queued for its original authenticated owner");
            Check(report, calls == 1 && PlayerPrefs.HasKey(Prefix + UidA), "The immutable match report reaches persistent storage before the first request");
            Check(report, MultiplayerRewardOutbox.GetReceipt(match.matchId) == null && !MultiplayerRewardOutbox.Status(match.matchId).Contains("earned:"), "A lost response never displays coins as earned before confirmation");
            match.completedTopics = Array.Empty<int>(); match.participationKeys = Array.Empty<string>();
            Check(report, MultiplayerRewardOutbox.Queue(UidA, match) && calls == 1, "Repeated queue calls do not replace or immediately resend the same match report");
            Forget(UidA);
            Check(report, MultiplayerRewardOutbox.GetReceipt(match.matchId) == null && !MultiplayerRewardOutbox.Status(match.matchId).Contains("earned:"),
                "Reloading a pending report never mistakes a default nested receipt object for a confirmed grant");
            Transport((path, json) => { calls++; replayPayload = json; return Task.FromResult(Json(Paid(match.matchId))); });
            MultiplayerRewardOutbox.Tick();
            await Until(() => MultiplayerRewardOutbox.GetReceipt(match.matchId) != null, "Persisted receipt retry completed");
            Check(report, replayPayload == firstPayload && calls == 2, "Reloading the outbox retries the original payload and match ID after a lost response");
            Check(report, MultiplayerRewardOutbox.GetReceipt(match.matchId).reward == 1000 && notifications == 1, "Only a confirmed server receipt updates the earned display and notifies the shared wallet");
            Check(report, MultiplayerRewardOutbox.GetLatestReceipt()?.matchId == match.matchId, "The current account can retrieve its latest confirmed match for the existing lobby status");
            Forget(UidA);
            Check(report, MultiplayerRewardOutbox.GetReceipt(match.matchId)?.reward == 1000 && calls == 2 && notifications == 1,
                "A persisted confirmation marker preserves its valid matching server receipt without notifying or paying again");
            MultiplayerRewardOutbox.Queue(UidA, match); MultiplayerRewardOutbox.Tick();
            Check(report, calls == 2 && notifications == 1, "A confirmed match cannot request another payout or wallet notification");

            Identity(UidB);
            Check(report, MultiplayerRewardOutbox.GetReceipt(match.matchId) == null && MultiplayerRewardOutbox.GetLatestReceipt() == null &&
                !MultiplayerRewardOutbox.Status(match.matchId).Contains("earned:"), "Another account cannot see or claim the first account's local receipt or latest-match notice");
            Identity(UidA); var swapped = Match(); var held = new TaskCompletionSource<string>(); int heldCalls = 0;
            Transport((path, json) => { heldCalls++; return held.Task; });
            Check(report, MultiplayerRewardOutbox.Queue(UidA, swapped), "An in-flight report is retained while its request is waiting");
            MultiplayerRewardOutbox.Queue(UidA, swapped); MultiplayerRewardOutbox.Tick();
            Check(report, heldCalls == 1, "Repeated queue and pump ticks cannot send a second in-flight request for the same match");
            Identity(UidB); held.SetResult(Json(Paid(swapped.matchId))); await Task.Delay(30);
            Check(report, MultiplayerRewardOutbox.GetReceipt(swapped.matchId) == null && notifications == 1, "Switching accounts during a request cannot apply that receipt to the replacement account");
            Identity(UidA);
            Check(report, MultiplayerRewardOutbox.GetReceipt(swapped.matchId) == null, "The original account keeps an ambiguous request pending after an account swap");
            Transport((path, json) => Task.FromResult(Json(Paid(swapped.matchId)))); RetryNow(UidA, swapped.matchId); MultiplayerRewardOutbox.Tick();
            await Until(() => MultiplayerRewardOutbox.GetReceipt(swapped.matchId) != null, "Original account resumed its receipt");
            Check(report, notifications == 2, "Signing back into the original account safely retrieves its same-match receipt once");

            var absentRoute = Match();
            Transport((path, json) => Task.FromException<string>(new MultiplayerRewardBackendClient.BackendException(404, "Unknown endpoint.")));
            MultiplayerRewardOutbox.Queue(UidA, absentRoute);
            Check(report, MultiplayerRewardOutbox.GetReceipt(absentRoute.matchId) == null && MultiplayerRewardOutbox.Status(absentRoute.matchId).Contains("service unavailable"), "An undeployed route keeps the report pending and does not invent a coin grant");
            Forget(UidA); Transport((path, json) => Task.FromResult(Json(Paid(absentRoute.matchId))));
            // Do not call Tick here: the production DontDestroyOnLoad pump must
            // discover the persisted report and finish it in the current scene.
            await Until(() => MultiplayerRewardOutbox.GetReceipt(absentRoute.matchId) != null, "Runtime pump resumed a persisted pending report");
            var pumps = UnityEngine.Object.FindObjectsByType<MultiplayerRewardPump>(FindObjectsSortMode.None);
            Check(report, pumps.Length == 1 && pumps[0].isActiveAndEnabled && pumps[0].gameObject.scene.name == "DontDestroyOnLoad", "Exactly one persistent runtime pump finishes saved reports outside the original match scene");

            foreach (string reason in new[] { "admin-assisted", "offline-preview", "no-participation" })
            {
                var excluded = Match();
                Transport((path, json) => Task.FromResult(Json(new MultiplayerRewardBackendClient.Receipt
                    { matchId = excluded.matchId, status = "completed", exclusionReason = reason, coins = 1000, gems = 2 })));
                MultiplayerRewardOutbox.Queue(UidA, excluded);
                await Until(() => MultiplayerRewardOutbox.GetReceipt(excluded.matchId) != null, "Zero-reward receipt confirmed");
                string expected = reason == "admin-assisted" ? "Admin-assisted match" : reason == "offline-preview" ? "Offline practice" : "No confirmed puzzle work";
                Check(report, MultiplayerRewardOutbox.GetReceipt(excluded.matchId).reward == 0 && MultiplayerRewardOutbox.Status(excluded.matchId).Contains(expected) &&
                    !MultiplayerRewardOutbox.Status(excluded.matchId).Contains(reason), "A confirmed " + reason + " receipt shows zero coins with a player-friendly exclusion notice");
            }
            var malformed = Match();
            Transport((path, json) => Task.FromResult(Json(new MultiplayerRewardBackendClient.Receipt { matchId = malformed.matchId, status = "completed", reward = 1001, baseCoins = 1001 })));
            MultiplayerRewardOutbox.Queue(UidA, malformed);
            Check(report, MultiplayerRewardOutbox.GetReceipt(malformed.matchId) == null, "An out-of-range backend response stays pending rather than displaying an impossible grant");
            report.passed = true; Status = "Passed";
        }
        catch (Exception error) { report.errors.Add(error.ToString()); Status = "Failed"; }
        finally
        {
            CommerceClient.Changed -= walletChanged;
            BackendField("ValidationTransport").SetValue(null, savedBackendTransport); BackendField("ValidationUid").SetValue(null, savedBackendUid);
            CommerceField("ValidationRequest").SetValue(null, savedCommerceTransport); CommerceField("ValidationUid").SetValue(null, savedCommerceUid);
            foreach (var saved in savedPreferences)
            {
                if (saved.Value == null) PlayerPrefs.DeleteKey(saved.Key); else PlayerPrefs.SetString(saved.Key, saved.Value);
            }
            Forget(UidA); Forget(UidB); PlayerPrefs.Save();
            if (savedLobbyService != null) savedLobbyService.Notify(savedLobbyNotice);
            Directory.CreateDirectory("Temp/ArchiveMatchRewards"); File.WriteAllText("Temp/ArchiveMatchRewards/Outbox.json", JsonUtility.ToJson(report, true));
            Debug.Log("Multiplayer reward outbox: " + Status + " (" + report.checks.Count + " checks).");
        }
    }
}
