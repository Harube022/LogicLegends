using System;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using LogicLegends.OpenArchive.Topics;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Authenticated multiplayer receipts use the existing account wallet, never a local balance.</summary>
public static class MultiplayerRewardBackendClient
{
    [Serializable] public sealed class Receipt
    {
        public string matchId, status, exclusionReason;
        public int coins, gems, reward, gemReward, baseCoins, crystalCoins, participationCoins, consolationCoins, tier;
        public int version, points, leaderboardReward, correctAnswers;
        public bool escaped;
    }
    [Serializable] sealed class ErrorReply { public string error; }
    public sealed class BackendException : InvalidOperationException
    {
        public long StatusCode { get; }
        public BackendException(long statusCode, string message) : base(message) { StatusCode = statusCode; }
    }
#if UNITY_EDITOR
    // Opt-in checks use fake identities and transport; these hooks do not exist in player builds.
    internal static Func<string, string, Task<string>> ValidationTransport;
    internal static string ValidationUid;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetValidation() { ValidationTransport = null; ValidationUid = null; }
#endif
    public static string UserId
    {
        get
        {
#if UNITY_EDITOR
            if (ValidationTransport != null) return ValidationUid;
#endif
            return FirebaseManager.IsReady ? FirebaseAuth.DefaultInstance.CurrentUser?.UserId : null;
        }
    }
    public static Task<Receipt> SendComplete(ArchiveMatchRewardReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        return SendCompleteJson(JsonUtility.ToJson(report), report.matchId);
    }
    internal static async Task<Receipt> SendCompleteJson(string json, string matchId)
    {
        string uid = UserId;
        if (string.IsNullOrEmpty(uid)) throw new InvalidOperationException("Sign in to save your multiplayer reward.");
        const string path = "/multiplayer/complete";
#if UNITY_EDITOR
        if (ValidationTransport != null)
        {
            string result = await ValidationTransport(path, json);
            if (UserId != uid) throw new InvalidOperationException("Your account changed.");
            return ReadReceipt(result, matchId);
        }
#endif
        var settings = CommerceClient.Settings;
        if (settings == null || !Uri.TryCreate(settings.backendUrl, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != "https" && !(Application.isEditor && endpoint.IsLoopback)))
            throw new InvalidOperationException("The account connection is not configured.");
        if (settings.sandbox && !Application.isEditor && !Debug.isDebugBuild)
            throw new InvalidOperationException("Test rewards are unavailable in release builds.");
        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null || user.UserId != uid) throw new InvalidOperationException("Your account changed.");
        string token = await CommerceClient.WithTimeout(user.TokenAsync(false));
        if (UserId != uid) throw new InvalidOperationException("Your account changed.");
        using (var request = new UnityWebRequest(settings.backendUrl.TrimEnd('/') + path, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 30;
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Content-Type", "application/json");
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (UserId != uid) throw new InvalidOperationException("Your account changed.");
            if (request.result != UnityWebRequest.Result.Success)
            {
                ErrorReply error = null;
                try { error = JsonUtility.FromJson<ErrorReply>(request.downloadHandler.text); } catch (Exception) { }
                throw new BackendException(request.responseCode, !string.IsNullOrEmpty(error?.error) ? error.error :
                    "Reward save pending. Check your connection.");
            }
            return ReadReceipt(request.downloadHandler.text, matchId);
        }
    }
    static Receipt ReadReceipt(string json, string matchId)
    {
        var receipt = JsonUtility.FromJson<Receipt>(json);
        if (!IsValidReceipt(receipt, matchId))
            throw new InvalidOperationException("The multiplayer reward response could not be verified. It remains pending.");
        return receipt;
    }
    internal static bool IsValidReceipt(Receipt receipt, string matchId)
    {
        if (receipt == null || receipt.matchId != matchId || receipt.status != "completed" || receipt.coins < 0 || receipt.gems < 0)
            return false;
        bool eligible = string.IsNullOrEmpty(receipt.exclusionReason);
        if (receipt.version == ArchiveMatchRewardPolicy.CurrentVersion)
            return receipt.gemReward == 0 && receipt.points >= receipt.leaderboardReward &&
                receipt.correctAnswers >= 0 && receipt.correctAnswers <= 64 &&
                (eligible
                    ? receipt.reward == (receipt.escaped ? ArchiveMatchRewardPolicy.EscapeCoins : ArchiveMatchRewardPolicy.FinishCoins) &&
                        receipt.leaderboardReward >= ArchiveMatchRewardPolicy.MinimumPoints && receipt.leaderboardReward <= ArchiveMatchRewardPolicy.MaximumPoints
                    : receipt.reward == 0 && receipt.leaderboardReward == 0);
        return (receipt.version == 0 || receipt.version == 1) &&
            receipt.reward >= 0 && receipt.reward <= 1000 &&
            receipt.gemReward >= 0 && receipt.gemReward <= 2 && receipt.tier >= 0 && receipt.tier <= 3 &&
            receipt.baseCoins >= 0 && receipt.baseCoins <= 600 && receipt.crystalCoins >= 0 && receipt.crystalCoins <= 250 &&
            receipt.participationCoins >= 0 && receipt.participationCoins <= 150 && receipt.consolationCoins >= 0 && receipt.consolationCoins <= 150 &&
            (long)receipt.baseCoins + receipt.crystalCoins + receipt.participationCoins + receipt.consolationCoins == receipt.reward &&
            (eligible || receipt.reward == 0 && receipt.gemReward == 0);
    }
}
