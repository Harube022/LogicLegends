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
        public int acknowledgedRetries, reward, serverRetries, acknowledgedQuizzes;
        public int economyVersion, baseReward, maxRetries, retryReductionPercent, quizReward, quizBonusLimit;
        public List<string> retryIds = new List<string>();
        public List<TrialBackendClient.QuizAnswer> quizAnswers = new List<TrialBackendClient.QuizAnswer>();
        public List<TrialBackendClient.QuizAnswer> serverQuizAnswers = new List<TrialBackendClient.QuizAnswer>();
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
        return SoloEconomy.BaseAfterRetries(1, retries);
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
        string uid = TrialBackendClient.UserId ?? "guest";
        if (trial < 1 || trial > TrialCatalog.Count) return null;
        string key = Key(uid, trial);
        if (stores.TryGetValue(key, out var store)) return store;
        try { store = JsonUtility.FromJson<Outbox>(PlayerPrefs.GetString(key, "")); } catch (Exception) { }
        if (store == null || store.uid != uid || store.trial != trial || store.runs == null)
            store = new Outbox { uid = uid, trial = trial };
        stores[key] = store;
        foreach (var run in store.runs)
        {
            if (run.quizAnswers == null) run.quizAnswers = new List<TrialBackendClient.QuizAnswer>();
            if (run.serverQuizAnswers == null) run.serverQuizAnswers = new List<TrialBackendClient.QuizAnswer>();
            if (run.maxRetries == 0) { SetPolicy(run, trial); run.serverRetries = run.acknowledgedRetries; }
        }
        if (!store.runs.Any(r => r.id == store.current)) AddRun(store, false);
        return store;
    }
    static Run Current(Outbox store) => store?.runs.FirstOrDefault(r => r.id == store.current);
    static Run AddRun(Outbox store, bool fresh)
    {
        var run = new Run { id = Guid.NewGuid().ToString("N"), beginNew = fresh };
        SetPolicy(run, store.trial);
        store.runs.Add(run); store.current = run.id; Save(store); return run;
    }
    static void SetPolicy(Run run, int trial)
    {
        var policy = SoloEconomy.Trial(trial);
        run.baseReward = policy.baseReward; run.quizReward = policy.quizReward; run.quizBonusLimit = policy.quizBonusLimit;
        run.maxRetries = SoloEconomy.Current.maxRetries; run.retryReductionPercent = SoloEconomy.Current.retryReductionPercent;
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
    { TryRecordRetry(trial, inPlace); }
    public static bool TryRecordRetry(int trial = 0, bool inPlace = false)
    {
        if (trial == 0) trial = CurrentTrial;
        if (TrialBackendClient.UserId != null && !OwnsGameplay(trial)) return false;
        var store = Store(trial); if (store == null) return false;
        var run = Current(store);
        if (run.completed) return false;
        if (!inPlace && run.retryNavigationPending) return false;
        if (inPlace && run.lastInPlaceRetryFrame == Time.frameCount) return false;
        if (!CanRetry(trial)) return false;
        if (inPlace) run.lastInPlaceRetryFrame = Time.frameCount;
        run.retryNavigationPending = !inPlace;
        run.retryIds.Add(Guid.NewGuid().ToString("N")); Save(store);
        _ = Sync(store, run);
        return true;
    }
    public static int RetryCount(int trial) { var run = Current(Store(trial)); return run == null ? 0 : Math.Min(run.maxRetries, run.serverRetries + Math.Max(0, run.retryIds.Count - run.acknowledgedRetries)); }
    public static bool CanRetry(int trial) { var run = Current(Store(trial)); return !changedAccounts.Contains(trial) && run != null && !run.completed && RetryCount(trial) < run.maxRetries; }
    public static int NextRetryReductionPercent(int trial) { var run = Current(Store(trial)); return run == null ? 0 : Math.Min(run.maxRetries, RetryCount(trial)+1) * run.retryReductionPercent; }
    public static string AttemptId(int trial) => Current(Store(trial))?.id;
    public static TrialBackendClient.QuizAnswer[] Answers(int trial)
    {
        var run = Current(Store(trial));
        if (run == null) return Array.Empty<TrialBackendClient.QuizAnswer>();
        var answers = run.quizAnswers.Concat(run.serverQuizAnswers)
            .GroupBy(q => q.eventKey).Select(g => g.First());
        // Trial 2's review and accuracy are for truth-table column submissions.
        // House-door pop quizzes only grant their separate quiz bonus.
        if (trial == 2) answers = answers.Where(q => q.kind == "table");
        // Trial 3's review and accuracy cover the inference challenge itself.
        // Rule-crystal pickup quizzes grant their separate quiz bonus only.
        if (trial == 3) answers = answers.Where(q => q.kind == "answer");
        return answers.ToArray();
    }
    public static int AccuracyPercent(int trial)
    {
        var answers = Answers(trial);
        if (answers.Length == 0) return 0;
        int correct = answers.Count(q => q.correct);
        return Mathf.FloorToInt(100f * correct / answers.Length);
    }
    public static void RecordAnswer(int trial, string eventKey, string questionId, string question,
        string correctAnswer, string selectedAnswer, bool correct)
    {
        Record(trial, eventKey, questionId, question, correctAnswer, selectedAnswer, correct, "answer");
    }
    public static void RecordTableAnswer(int trial, string eventKey, string questionId, string question,
        string correctAnswer, string selectedAnswer, bool correct)
    {
        Record(trial, eventKey, questionId, question, correctAnswer, selectedAnswer, correct, "table");
    }
    static void Record(int trial, string eventKey, string questionId, string question,
        string correctAnswer, string selectedAnswer, bool correct, string kind)
    {
        if (trial < 1 || trial > TrialCatalog.Count || TrialBackendClient.UserId != null && !OwnsGameplay(trial)) return;
        var store = Store(trial); var run = Current(store);
        if (run == null || run.completed || run.status == "completed" || run.adminUsed || HasQuizAnswer(trial, eventKey)) return;
        run.quizAnswers.Add(new TrialBackendClient.QuizAnswer {
            eventKey = eventKey, questionId = questionId, question = question,
            correctAnswer = correctAnswer, selectedAnswer = selectedAnswer, choice = -1, correct = correct, kind = kind
        });
        Save(store); _ = Sync(store, run);
    }
    public static bool HasQuizAnswer(int trial, string eventKey)
    { var run = Current(Store(trial)); return run != null && (run.quizAnswers.Any(q => q.eventKey == eventKey) || run.serverQuizAnswers.Any(q => q.eventKey == eventKey)); }
    public static void RecordQuizAnswer(int trial, string eventKey, string questionId, int choice, bool correct, bool practice = false,
        string question = null, string correctAnswer = null, string selectedAnswer = null)
    {
        if (practice || (trial != 2 && trial != 3)) return;
        if (TrialBackendClient.UserId == null)
        {
            Record(trial, eventKey, questionId, question ?? "", correctAnswer ?? "", selectedAnswer ?? "", correct, "quiz");
            return;
        }
        if (!OwnsGameplay(trial)) return;
        var store = Store(trial); var run = Current(store);
        if (run == null || run.completed || run.status == "completed" || run.adminUsed || HasQuizAnswer(trial, eventKey)) return;
        run.quizAnswers.Add(new TrialBackendClient.QuizAnswer { eventKey = eventKey, questionId = questionId,
            choice = choice, correct = correct, question = question, correctAnswer = correctAnswer, selectedAnswer = selectedAnswer, kind = "quiz" });
        Save(store); _ = Sync(store, run);
    }
    static int PendingBonus(Run run)
    {
        if (run == null || run.adminUsed) return 0;
        var checkedKeys = new HashSet<string>(run.serverQuizAnswers.Where(IsQuizAnswer).Select(q => q.eventKey));
        int confirmed = run.serverQuizAnswers.Where(IsQuizAnswer).Sum(q => q.bonus);
        int local = run.quizAnswers.Where(q => IsQuizAnswer(q) && !checkedKeys.Contains(q.eventKey) && q.correct).Count() * run.quizReward;
        return Math.Min(run.quizBonusLimit, confirmed + local);
    }
    static bool IsQuizAnswer(TrialBackendClient.QuizAnswer answer) => answer.kind == "quiz" || answer.choice >= 0;
    public static string Breakdown(int trial, bool completed)
    {
        if (changedAccounts.Contains(trial)) return "Account changed. Start Over to earn this trial's reward.";
        var run = Current(Store(trial)); if (run == null) return "";
        int retries = RetryCount(trial), reduction = run.baseReward - (int)((long)run.baseReward * Math.Max(0, 100 - Math.Min(retries, run.maxRetries) * run.retryReductionPercent) / 100);
        int bonus = PendingBonus(run), payout = run.adminUsed ? 0 : run.baseReward - reduction + bonus;
        string text = "Retries: " + retries + "/" + run.maxRetries + "   •   Base: " + run.baseReward.ToString("N0") + " Coins\n" +
            "Reduction: " + (reduction > 0 ? "−" : "") + reduction.ToString("N0") + " (" + Math.Min(retries, run.maxRetries) * run.retryReductionPercent + "%)";
        if (run.quizBonusLimit > 0) text += "   •   Quiz bonus: +" + bonus + "/" + run.quizBonusLimit;
        text += "\n" + (completed ? "Final payout: " : "On completion: ") + (run.status == "completed" ? run.reward : payout).ToString("N0") + " Coins";
        if (run.adminUsed) text += " • Admin-assisted: no reward";
        else if (completed) text += TrialBackendClient.UserId == null ? " • Sign in required" : run.status == "completed" ? " • Saved" : " • Save pending";
        return text;
    }
    // Display the receipt when available; otherwise display the same pending amount used by Breakdown.
    // This read-only accessor never completes an attempt or writes the wallet.
    public static int CompletionCoins(int trial)
    {
        if (changedAccounts.Contains(trial)) return 0;
        var run = Current(Store(trial));
        if (run == null || run.adminUsed) return 0;
        if (run.status == "completed") return run.reward;
        int retries = RetryCount(trial);
        int adjustedBase = (int)((long)run.baseReward * Math.Max(0, 100 - Math.Min(retries, run.maxRetries) * run.retryReductionPercent) / 100);
        return adjustedBase + PendingBonus(run);
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
        if (run.status == "completed") return Breakdown(trial, true);
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
        run.adminUsed && !run.serverAdminUsed || run.acknowledgedQuizzes < run.quizAnswers.Count || run.completed && run.status != "completed";
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
            if (!SoloEconomyConnection.Enabled)
            {
                var legacy = new TrialBackendClient.Request { economyVersion = 1, trial = store.trial,
                    attemptId = run.serverId ?? run.id, newAttempt = run.beginNew };
                if (run.serverId == null)
                {
                    var started = await TrialBackendClient.SendAttempt("start", legacy);
                    if (TrialBackendClient.UserId != store.uid) return;
                    run.serverId = legacy.attemptId = started.attemptId; Apply(run, started); Save(store);
                }
                if (run.adminUsed && !run.serverAdminUsed && run.status != "completed")
                {
                    var response = await TrialBackendClient.SendAttempt("admin-used", legacy);
                    if (TrialBackendClient.UserId != store.uid) return;
                    Apply(run, response); Save(store);
                }
                while (run.acknowledgedRetries < run.retryIds.Count && run.status != "completed")
                {
                    legacy.operationId = run.retryIds[run.acknowledgedRetries];
                    var response = await TrialBackendClient.SendAttempt("retry", legacy);
                    if (TrialBackendClient.UserId != store.uid) return;
                    Apply(run, response); run.acknowledgedRetries++; Save(store);
                }
                while (run.acknowledgedQuizzes < run.quizAnswers.Count && run.status != "completed")
                {
                    var answer = run.quizAnswers[run.acknowledgedQuizzes];
                    legacy.eventKey = answer.eventKey; legacy.questionId = answer.questionId;
                    legacy.answerType = answer.kind;
                    legacy.question = answer.question; legacy.correctAnswer = answer.correctAnswer;
                    legacy.selectedAnswer = answer.selectedAnswer; legacy.correct = answer.correct; legacy.choice = answer.choice;
                    var response = await TrialBackendClient.SendAttempt("progress-answer", legacy);
                    if (TrialBackendClient.UserId != store.uid) return;
                    Apply(run, response); run.acknowledgedQuizzes++; Save(store);
                }
                if (run.completed && run.status != "completed")
                {
                    var response = await TrialBackendClient.SendAttempt("complete", legacy);
                    if (TrialBackendClient.UserId != store.uid) return;
                    Apply(run, response); Save(store);
                }
                return;
            }
            // Finish an older pending receipt before a new run replaces its server pointer.
            foreach (var previous in store.runs.TakeWhile(r => r != run))
                if (previous.completed && previous.status != "completed")
                {
                    await Sync(store, previous);
                    if (previous.status != "completed") return;
                }
            if (TrialBackendClient.UserId != store.uid) return;
            var request = new TrialBackendClient.Request { trial = store.trial, attemptId = run.serverId ?? run.id, newAttempt = run.beginNew };
            if (run.serverId == null || run.economyVersion < 2)
            {
                var response = await TrialBackendClient.SendAttempt("start", request);
                if (TrialBackendClient.UserId != store.uid) return;
                if (response.economyVersion < 2) throw new InvalidOperationException("Updated reward service required.");
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
            while (run.acknowledgedQuizzes < run.quizAnswers.Count && run.status != "completed")
            {
                var answer = run.quizAnswers[run.acknowledgedQuizzes];
                request.eventKey = answer.eventKey; request.questionId = answer.questionId; request.choice = answer.choice;
                request.answerType = answer.kind;
                request.question = answer.question; request.correctAnswer = answer.correctAnswer;
                request.selectedAnswer = answer.selectedAnswer; request.correct = answer.correct;
                string answerAction = request.choice >= 0 ? "quiz-answer" : "progress-answer";
                var response = await TrialBackendClient.SendAttempt(answerAction, request);
                if (TrialBackendClient.UserId != store.uid) return;
                Apply(run, response); run.acknowledgedQuizzes++; Save(store);
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
        run.economyVersion = response.economyVersion;
        run.status = response.status; run.reward = response.reward;
        if (response.status == "completed") run.completed = true;
        run.serverRetries = response.retries;
        if (response.maxRetries > 0)
        {
            run.baseReward = response.baseReward; run.maxRetries = response.maxRetries;
            run.retryReductionPercent = response.retryReductionPercent; run.quizReward = response.quizReward; run.quizBonusLimit = response.quizBonusLimit;
        }
        if (response.quizAnswers != null) run.serverQuizAnswers = response.quizAnswers.ToList();
        run.serverAdminUsed = response.adminUsed;
        if (response.adminUsed) run.adminUsed = true;
    }
    static async Task SaveLegacyAdminExclusion(Outbox store, Run run)
    {
        if (TrialBackendClient.UserId != store.uid) return;
        if (store.runs.TakeWhile(r => r != run).Any(r => r.completed && r.status != "completed" && !r.adminUsed)) return;
        var request = new TrialBackendClient.Request { economyVersion = 1, trial = store.trial, attemptId = run.serverId ?? run.id, newAttempt = run.beginNew };
        if (run.serverId == null)
        {
            var started = await TrialBackendClient.SendAttempt("start", request);
            if (TrialBackendClient.UserId != store.uid) return;
            run.serverId = request.attemptId = started.attemptId;
        }
        if (!run.serverAdminUsed)
        {
            var excluded = await TrialBackendClient.SendAttempt("admin-used", request);
            if (TrialBackendClient.UserId != store.uid) return;
            run.serverAdminUsed = excluded.adminUsed;
        }
        if (run.completed && run.serverAdminUsed)
        {
            run.status = "completed"; run.reward = 0;
            run.serverRetries = RetryCount(store.trial); run.acknowledgedRetries = run.retryIds.Count;
            run.acknowledgedQuizzes = run.quizAnswers.Count;
        }
        Save(store);
    }
}
