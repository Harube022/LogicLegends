using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Durable, account-scoped attempt outbox. Currency and grant receipts are owned by the backend.</summary>
public static class TrialRewards
{
    [Serializable] sealed class Run
    {
        public string id, serverId, status;
        public bool beginNew, completed, adminUsed, serverAdminUsed;
        public int acknowledgedRetries, reward;
        public List<string> retryIds = new List<string>();
        [NonSerialized] public Task saving;
        [NonSerialized] public float nextSync;
        [NonSerialized] public bool retryNavigationPending;
        [NonSerialized] public int lastInPlaceRetryFrame = -1;
    }
    [Serializable] sealed class Outbox
    {
        public string uid, current;
        public int trial;
        public List<Run> runs = new List<Run>();
    }
    static readonly Dictionary<string, Outbox> stores = new Dictionary<string, Outbox>();
    static readonly Dictionary<int, string> sceneOwners = new Dictionary<int, string>();
    static readonly HashSet<int> changedAccounts = new HashSet<int>();
    static string loadedAccount;
    public static event Action Changed;
    public static int RewardForRetries(int retries)
    {
        if (retries < 0) throw new ArgumentOutOfRangeException(nameof(retries));
        return (int)Math.Floor(1000d * Math.Pow(.75d, retries));
    }
    public static int CurrentTrial
    {
        get
        {
            try { return TrialCatalog.ForScene(SceneManager.GetActiveScene().name, StageSelectionState.SelectedStage).Number; }
            catch (ArgumentException) { return 0; }
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { stores.Clear(); sceneOwners.Clear(); changedAccounts.Clear(); loadedAccount = null; Changed = null; }
    static bool OwnsGameplay(int trial)
    {
        string uid=TrialBackendClient.UserId;
        if(uid==null)return false;
        if(!sceneOwners.TryGetValue(trial,out string owner))sceneOwners[trial]=uid;
        else if(owner!=uid)changedAccounts.Add(trial);
        return !changedAccounts.Contains(trial);
    }
    static string Key(string uid, int trial) => "TrialRewardOutbox.v1." + uid + "." + trial;
    static Outbox Store(int trial)
    {
        string uid = TrialBackendClient.UserId;
        if (uid == null || trial < 1 || trial > TrialCatalog.Count) return null;
        string key = Key(uid, trial);
        if (stores.TryGetValue(key, out var store)) return store;
        try { store = JsonUtility.FromJson<Outbox>(PlayerPrefs.GetString(key, "")); } catch (Exception) { }
        if (store == null || store.uid != uid || store.trial != trial || store.runs == null)
            store = new Outbox { uid = uid, trial = trial };
        stores[key] = store;
        if (!store.runs.Any(r => r.id == store.current)) AddRun(store, false);
        return store;
    }
    static Run Current(Outbox store) => store?.runs.FirstOrDefault(r => r.id == store.current);
    static Run AddRun(Outbox store, bool fresh)
    {
        var run = new Run { id = Guid.NewGuid().ToString("N"), beginNew = fresh };
        store.runs.Add(run); store.current = run.id; Save(store); return run;
    }
    static void Save(Outbox store)
    {
        PlayerPrefs.SetString(Key(store.uid, store.trial), JsonUtility.ToJson(store));
        PlayerPrefs.Save(); Changed?.Invoke();
    }
    public static void PrepareNewAttempt(int trial)
    {
        var store = Store(trial); if (store == null) return;
        AddRun(store, true);
    }
    public static void RecordRetry(int trial = 0, bool inPlace = false)
    {
        if (trial == 0) trial = CurrentTrial;
        var store = Store(trial); if (store == null) return;
        var run = Current(store);
        if (run.completed) { AddRun(store, true); return; } // Explicit replay after a win is a new attempt.
        if (!inPlace && run.retryNavigationPending) return;
        if (inPlace && run.lastInPlaceRetryFrame == Time.frameCount) return;
        if (inPlace) run.lastInPlaceRetryFrame = Time.frameCount;
        run.retryNavigationPending = !inPlace;
        run.retryIds.Add(Guid.NewGuid().ToString("N")); Save(store);
        _ = Sync(store, run);
    }
    public static void SceneLoaded()
    {
        sceneOwners.Clear(); changedAccounts.Clear();
        foreach (var store in stores.Values) foreach (var run in store.runs) run.retryNavigationPending = false;
    }
    public static void Complete(int trial)
    {
        if (!OwnsGameplay(trial)) return;
        var store = Store(trial); if (store == null) return;
        var run = Current(store);
        if (!run.completed) { run.completed = true; Save(store); }
        _ = Sync(store, run);
    }
    public static bool AdminExclusionConfirmed
    {
        get { var run = Current(Store(CurrentTrial)); return run != null && run.adminUsed && run.serverAdminUsed; }
    }
    public static async Task<bool> PrepareAdminAction()
    {
        if (!AdminAuthorization.IsAuthorized) return false;
        int trial = CurrentTrial;
        var store = Store(trial); if (store == null) return false;
        var run = Current(store);
        // A developer may revisit a scene with an old completed receipt. Its
        // debug session gets a new, excluded attempt instead of changing that receipt.
        if (run.completed || run.status == "completed") run = AddRun(store, true);
        run.adminUsed = true; Save(store);
        await Sync(store, run);
        return AdminAuthorization.IsAuthorized && TrialBackendClient.UserId == store.uid &&
            Current(Store(trial)) == run && run.serverAdminUsed;
    }
    public static string Status(int trial)
    {
        if(changedAccounts.Contains(trial))return "Account changed — restart this trial to earn coins.";
        var store = Store(trial); var run = Current(store);
        if (run == null) return "Sign in to save your coin reward.";
        if (run.status == "completed") return run.serverAdminUsed ? "Coins earned: 0 — Admin-assisted attempt" : "Coins earned: +" + run.reward.ToString("N0");
        if (run.adminUsed) return "Coins earned: 0 — Admin-assisted attempt";
        return "Coin reward pending — reconnect to save.";
    }
    public static void Tick()
    {
        int trial = CurrentTrial;
        if (trial > 0) { OwnsGameplay(trial); Store(trial); }
        string uid = TrialBackendClient.UserId;
        if (uid == null) { loadedAccount = null; return; }
        if (loadedAccount != uid)
        {
            loadedAccount = uid;
            // Reconnect pending completions even if login returns to the menu.
            for (int savedTrial = 1; savedTrial <= TrialCatalog.Count; savedTrial++)
                if (PlayerPrefs.HasKey(Key(uid, savedTrial))) Store(savedTrial);
        }
        foreach (var store in stores.Values.ToArray())
            if (store.uid == uid)
                foreach (var run in store.runs.ToArray())
                    if ((run.completed || run.id == store.current) && NeedsSync(run) && Time.realtimeSinceStartup >= run.nextSync)
                        _ = Sync(store, run);
    }
    static bool NeedsSync(Run run) => run.serverId == null || run.acknowledgedRetries < run.retryIds.Count ||
        run.adminUsed && !run.serverAdminUsed || run.completed && run.status != "completed";
    static Task Sync(Outbox store, Run run)
    {
        if (run.saving != null && !run.saving.IsCompleted) return run.saving;
        run.saving = SaveToBackend(store, run); return run.saving;
    }
    static async Task SaveToBackend(Outbox store, Run run)
    {
        run.nextSync = Time.realtimeSinceStartup + 15;
        try
        {
            // Finish an older pending receipt before a new run replaces its server pointer.
            foreach (var previous in store.runs.TakeWhile(r => r != run))
                if (previous.completed && previous.status != "completed")
                {
                    await Sync(store, previous);
                    if (previous.status != "completed") return;
                }
            if (TrialBackendClient.UserId != store.uid) return;
            var request = new TrialBackendClient.Request { trial = store.trial, attemptId = run.serverId ?? run.id, newAttempt = run.beginNew };
            if (run.serverId == null)
            {
                var response = await TrialBackendClient.SendAttempt("start", request);
                if (TrialBackendClient.UserId != store.uid) return;
                run.serverId = request.attemptId = response.attemptId;
                Apply(run, response); Save(store);
            }
            // Ordered idempotent operations survive scene reload, timeout and app restart.
            while (run.acknowledgedRetries < run.retryIds.Count && run.status != "completed")
            {
                request.operationId = run.retryIds[run.acknowledgedRetries];
                var response = await TrialBackendClient.SendAttempt("retry", request);
                if (TrialBackendClient.UserId != store.uid) return;
                Apply(run, response); run.acknowledgedRetries++; Save(store);
            }
            if (run.adminUsed && !run.serverAdminUsed && run.status != "completed")
            {
                var response = await TrialBackendClient.SendAttempt("admin-used", request);
                if (TrialBackendClient.UserId != store.uid) return;
                Apply(run, response); Save(store);
            }
            if (run.completed && run.status != "completed")
            {
                if (run.adminUsed && !run.serverAdminUsed) return;
                var response = await TrialBackendClient.SendAttempt("complete", request);
                if (TrialBackendClient.UserId != store.uid) return;
                Apply(run, response); Save(store); CommerceClient.NotifyChanged();
            }
        }
        catch (Exception) { /* Keep the same IDs in the outbox. A timeout is not proof the server did not commit. */ }
    }
    static void Apply(Run run, TrialBackendClient.Attempt response)
    {
        run.status = response.status; run.reward = response.reward;
        run.serverAdminUsed = response.adminUsed;
    }
}
