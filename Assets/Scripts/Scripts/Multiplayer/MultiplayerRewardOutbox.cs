using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LogicLegends.OpenArchive.Topics;
using UnityEngine;

/// <summary>Account-bound immutable terminal reports survive scenes, reconnects and app restart.</summary>
public static class MultiplayerRewardOutbox
{
    [Serializable] sealed class Entry
    {
        public string matchId, reportJson, error;
        public bool rejected, confirmed;
        public long confirmedAt;
        public MultiplayerRewardBackendClient.Receipt receipt;
        [NonSerialized] public Task saving;
        [NonSerialized] public float nextSync;
    }
    [Serializable] sealed class Outbox
    {
        public string uid;
        public List<Entry> entries = new List<Entry>();
    }
    const string Prefix = "MultiplayerRewardOutbox.v1.";
    static readonly Dictionary<string, Outbox> stores = new Dictionary<string, Outbox>();
    static readonly Regex MatchId = new Regex("^[a-f0-9]{32}$", RegexOptions.CultureInvariant);
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { stores.Clear(); Changed = null; }
    static bool ValidOwner(string uid) => !string.IsNullOrEmpty(uid) && uid.Length <= 128;
    static Outbox Store(string uid)
    {
        if (!ValidOwner(uid)) return null;
        if (stores.TryGetValue(uid, out var store)) return store;
        try { store = JsonUtility.FromJson<Outbox>(PlayerPrefs.GetString(Prefix + uid, "")); } catch (Exception) { }
        if (store == null || store.uid != uid || store.entries == null) store = new Outbox { uid = uid };
        // Invalid local data must never turn into a different account or match request.
        store.entries = store.entries.Where(e => e != null && MatchId.IsMatch(e.matchId ?? "") &&
            !string.IsNullOrEmpty(e.reportJson) && e.reportJson.Length <= 32768)
            .GroupBy(e => e.matchId).Select(g => g.First()).ToList();
        // JsonUtility can deserialize a serialized null reference as a default
        // nested object. Only a persisted marker plus a valid matching receipt
        // is confirmation; object existence is never proof of a server grant.
        foreach (var entry in store.entries)
            if (!entry.confirmed || !MultiplayerRewardBackendClient.IsValidReceipt(entry.receipt, entry.matchId))
            { entry.confirmed = false; entry.receipt = null; }
        stores[uid] = store;
        return store;
    }
    static void Save(Outbox store)
    {
        // Keep all pending work and a bounded local history. The backend remains
        // the permanent deduplication authority even after local history prunes.
        var old = store.entries.Where(e => e.confirmed || e.rejected).ToArray();
        foreach (var entry in old.Take(Math.Max(0, old.Length - 100))) store.entries.Remove(entry);
        PlayerPrefs.SetString(Prefix + store.uid, JsonUtility.ToJson(store));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
    public static bool Queue(string ownerUid, ArchiveMatchRewardReport report)
    {
        if (!ValidOwner(ownerUid) || report == null || !MatchId.IsMatch(report.matchId ?? "")) return false;
        var store = Store(ownerUid);
        if (store.entries.Any(e => e.matchId == report.matchId)) return true;
        string json = JsonUtility.ToJson(report);
        if (json.Length > 32768) return false;
        var entry = new Entry { matchId = report.matchId, reportJson = json };
        store.entries.Add(entry);
        Save(store); // The immutable report reaches disk before any request starts.
        if (MultiplayerRewardBackendClient.UserId == ownerUid && !Saving(store)) _ = Sync(store, entry);
        return true;
    }
    public static MultiplayerRewardBackendClient.Receipt GetReceipt(string matchId)
    {
        var store = Store(MultiplayerRewardBackendClient.UserId);
        return store?.entries.FirstOrDefault(e => e.matchId == matchId && e.confirmed)?.receipt;
    }
    public static MultiplayerRewardBackendClient.Receipt GetLatestReceipt()
    {
        var store = Store(MultiplayerRewardBackendClient.UserId);
        return store?.entries.Where(e => e.confirmed).OrderByDescending(e => e.confirmedAt).Select(e => e.receipt).FirstOrDefault();
    }
    public static string Status(string matchId)
    {
        var store = Store(MultiplayerRewardBackendClient.UserId);
        if (store == null) return "Sign in to save your multiplayer reward.";
        var entry = store.entries.FirstOrDefault(e => e.matchId == matchId);
        if (entry == null) return "Rewards are calculated when the team match ends.";
        if (entry.confirmed)
        {
            var receipt = entry.receipt;
            if (!string.IsNullOrEmpty(receipt.exclusionReason)) return "Coins earned: 0 — " + ExclusionNotice(receipt.exclusionReason);
            return "Coins earned: +" + receipt.reward.ToString("N0") +
                (receipt.gemReward > 0 ? "  ·  Gems earned: +" + receipt.gemReward : "");
        }
        if (entry.rejected) return "Reward report could not be accepted — " + entry.error;
        if (entry.error == "service-unavailable") return "Coins pending — reward service unavailable. Your report is saved.";
        return "Coins pending — reconnect to save. Your report is saved.";
    }
    static string ExclusionNotice(string reason)
    {
        switch (reason)
        {
            case "admin-assisted": return "Admin-assisted match — no permanent rewards.";
            case "offline-preview": return "Offline practice — no permanent rewards.";
            case "no-participation": return "No confirmed puzzle work or teammate support.";
            default: return "This match is not eligible for rewards.";
        }
    }
    public static void Tick()
    {
        string uid = MultiplayerRewardBackendClient.UserId;
        if (!ValidOwner(uid)) return;
        var store = Store(uid);
        if (Saving(store)) return;
        var entry = store.entries.FirstOrDefault(e => !e.confirmed && !e.rejected && Time.realtimeSinceStartup >= e.nextSync);
        if (entry != null) _ = Sync(store, entry);
    }
    // A long outage can accumulate multiple matches. Drain them one request at
    // a time rather than creating a burst against the shared account service.
    static bool Saving(Outbox store) => store.entries.Any(e => e.saving != null && !e.saving.IsCompleted);
    static Task Sync(Outbox store, Entry entry)
    {
        if (entry.saving != null && !entry.saving.IsCompleted) return entry.saving;
        entry.saving = SaveToBackend(store, entry);
        return entry.saving;
    }
    static async Task SaveToBackend(Outbox store, Entry entry)
    {
        entry.nextSync = Time.realtimeSinceStartup + 15f;
        try
        {
            if (MultiplayerRewardBackendClient.UserId != store.uid) return;
            var receipt = await MultiplayerRewardBackendClient.SendCompleteJson(entry.reportJson, entry.matchId);
            if (MultiplayerRewardBackendClient.UserId != store.uid) return;
            entry.receipt = receipt; entry.confirmed = true; entry.error = null; entry.confirmedAt = DateTime.UtcNow.Ticks;
            Save(store);
            CommerceClient.NotifyChanged();
        }
        catch (MultiplayerRewardBackendClient.BackendException error)
        {
            // A missing route during deployment is not a rejected match. Keep its
            // exact report so it can sync once the reward service is available.
            entry.error = error.StatusCode == 404 ? "service-unavailable" : error.Message;
            entry.rejected = error.StatusCode == 400 || error.StatusCode == 422;
            Save(store);
        }
        catch (Exception)
        {
            // A lost response may follow a successful server commit. Retrying
            // this same match ID retrieves that receipt rather than paying twice.
        }
    }
}
