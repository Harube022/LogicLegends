using System;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

public static class CommerceClient
{
    [Serializable] public sealed class Account
    {
        public int gems, coins;
        public string character, equippedClothes, equippedPet;
        public string[] inventory;
        public bool sandbox, live, paymentsEnabled;
    }
    [Serializable] public sealed class Checkout { public string orderId, checkoutUrl, status; public bool sandbox, live; }
    [Serializable] private sealed class AccountRequest { public string character; }
    [Serializable] private sealed class PurchaseRequest { public string itemId; }
    [Serializable] private sealed class CheckoutRequest { public string productId = "gems_5"; public string requestId; }
    [Serializable] private sealed class ErrorReply { public string error, code; }
    public sealed class ShopSetupException : InvalidOperationException
    {
        public ShopSetupException() : base("Purchases are not available yet.") { }
    }
    public static CommerceSettings Settings => Resources.Load<CommerceSettings>("CommerceSettings");
    public static bool Sandbox => Settings == null || Settings.sandbox;
    public static event Action Changed;
    private static Task<bool> warmup;
    private static string warmupUrl;
    private static float readyUntil;
    [Serializable] private sealed class HealthReply { public string mode; }
#if UNITY_EDITOR
    // Isolated connection checks use fake identities and responses, never real accounts.
    internal static Func<string, string, string, int, Task<string>> ValidationRequest;
    internal static Func<float> ValidationTime;
    internal static string ValidationUid;
#endif
    [Serializable] private sealed class Reply
    {
        public long status;
        public bool success;
        public string body;
    }
    private static float ConnectionTime
    {
        get
        {
#if UNITY_EDITOR
            if (ValidationTime != null) return ValidationTime();
#endif
            return Time.realtimeSinceStartup;
        }
    }
    internal static string UserId
    {
        get
        {
#if UNITY_EDITOR
            if (ValidationRequest != null) return ValidationUid;
#endif
            return FirebaseManager.IsReady ? FirebaseAuth.DefaultInstance.CurrentUser?.UserId : null;
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Changed = null; warmup = null; warmupUrl = null; readyUntil = 0;
#if UNITY_EDITOR
        ValidationRequest = null; ValidationTime = null; ValidationUid = null;
#endif
    }
    // Wake the hosted service while the Main Menu/login is visible rather than
    // making the first shop visit absorb the entire cold-start delay.
    public static void Prewarm()
    {
        _ = EnsureReady();
    }
    private static Task<bool> EnsureReady()
    {
        var settings = Settings;
        if (settings == null || !Uri.TryCreate(settings.backendUrl, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != "https" && !(Application.isEditor && endpoint.IsLoopback))) return Task.FromResult(false);
        string url = settings.backendUrl.TrimEnd('/');
        if (warmupUrl != url) { warmupUrl = url; warmup = null; readyUntil = 0; }
        if (ConnectionTime < readyUntil) return Task.FromResult(true);
        if (warmup == null || warmup.IsCompleted) warmup = Warmup(url);
        return warmup;
    }
    private static bool Transient(Reply reply) => reply.status == 0 || reply.status == 408 ||
        reply.status == 429 || reply.status == 502 || reply.status == 503 || reply.status == 504;
    private static async Task<bool> Warmup(string url)
    {
        // A sleeping host can take a minute to wake. Share one bounded wait
        // across clothes, top-up and menu prewarm instead of failing at 20s.
        float deadline = ConnectionTime + 80f;
        for (int attempt = 0; attempt < 40 && ConnectionTime < deadline; attempt++)
        {
            try
            {
                var reply = await Request(url + "/health", null, null, Mathf.Clamp(Mathf.CeilToInt(deadline - ConnectionTime), 1, 25));
                if (reply.success)
                {
                    HealthReply health = null;
                    try { health = JsonUtility.FromJson<HealthReply>(reply.body); } catch (Exception) { }
                    if (health != null && !string.IsNullOrEmpty(health.mode))
                    {
                        if (warmupUrl == url) readyUntil = ConnectionTime + 600f;
                        return true;
                    }
                    // A waking host may return an HTML holding page with HTTP 200.
                }
                else if (!Transient(reply)) return false;
            }
            catch (Exception) { /* Background warmup must not throw into the login flow. */ }
            if (ConnectionTime < deadline) await Task.Delay(2000);
        }
        return false;
    }
    private static async Task<Reply> Request(string url, string json, string token, int timeout)
    {
#if UNITY_EDITOR
        if (ValidationRequest != null) return JsonUtility.FromJson<Reply>(await ValidationRequest(url, json, token, timeout));
#endif
        using (var request = new UnityWebRequest(url, json == null ? "GET" : "POST"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = timeout;
            if (token != null) request.SetRequestHeader("Authorization", "Bearer " + token);
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            return new Reply { success = request.result == UnityWebRequest.Result.Success,
                status = request.responseCode, body = request.downloadHandler.text };
        }
    }
    public static void NotifyChanged() { Changed?.Invoke(); }
    public static Task<Account> LoadAccount(string character) => Send<Account>("/account", JsonUtility.ToJson(new AccountRequest { character = character }));
    public static Task<Account> Wallet() => Send<Account>("/wallet");
    public static Task<Account> BuyItem(string itemId) => Send<Account>("/purchase", JsonUtility.ToJson(new PurchaseRequest { itemId = itemId }));
    public static Task<Account> EquipItem(string itemId) => Send<Account>("/equip", JsonUtility.ToJson(new PurchaseRequest { itemId = itemId }));
    // Firebase tasks can wait indefinitely while offline. Bound the UI wait,
    // without treating a timeout as proof that a write failed.
    public static async Task<T> WithTimeout<T>(Task<T> task, int seconds = 20)
    {
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(seconds))) != task)
            throw new TimeoutException("Couldn't connect. Check the connection and retry.");
        return await task;
    }
    public static Task<Checkout> StartCheckout(string requestId) => Send<Checkout>("/checkout", JsonUtility.ToJson(new CheckoutRequest { requestId = requestId }));
    public static Task<Checkout> CheckOrder(string orderId) => Send<Checkout>("/orders/" + UnityWebRequest.EscapeURL(orderId));
    private static async Task<T> Send<T>(string path, string json = null) where T : class
    {
        var settings = Settings;
        if (settings == null || !Uri.TryCreate(settings.backendUrl, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != "https" && !(Application.isEditor && endpoint.IsLoopback)))
            throw new InvalidOperationException("The shop connection is not configured.");
        if (settings.sandbox && !Application.isEditor && !Debug.isDebugBuild)
            throw new InvalidOperationException("Test purchases are unavailable in release builds.");
        string uid = UserId;
        if (uid == null) throw new InvalidOperationException("Please sign in to use the shop.");
        bool read = path == "/account" || path == "/wallet" || path.StartsWith("/orders/", StringComparison.Ordinal);
        if (read && !await EnsureReady()) throw new InvalidOperationException("Couldn't connect to the shop. Please retry.");
        if (UserId != uid) throw new InvalidOperationException("Account changed. Please reopen the shop.");
        string token;
#if UNITY_EDITOR
        if (ValidationRequest != null) token = "fixture-token";
        else
#endif
            token = await WithTimeout(FirebaseAuth.DefaultInstance.CurrentUser.TokenAsync(false));
        if (UserId != uid) throw new InvalidOperationException("Account changed. Please reopen the shop.");
        for (int attempt = 0; ; attempt++)
        {
            if (UserId != uid) throw new InvalidOperationException("Account changed. Please reopen the shop.");
            var reply = await Request(settings.backendUrl.TrimEnd('/') + path, json, token, read ? 20 : 70);
            if (UserId != uid) throw new InvalidOperationException("Account changed. Please reopen the shop.");
            if (!reply.success)
            {
                ErrorReply error = null;
                try { error = JsonUtility.FromJson<ErrorReply>(reply.body); } catch (Exception) { }
                if (reply.status == 503 && error != null && (error.code == "SHOP_SETUP" ||
                    error.error == "The shop is being configured. Please try again later."))
                    throw new ShopSetupException();
                // Only idempotent browsing reads retry. Never replay a purchase,
                // equipment change or checkout after an ambiguous response.
                if (read && attempt == 0 && Transient(reply)) { await Task.Delay(1000); continue; }
                readyUntil = 0;
                throw new InvalidOperationException(error != null && !string.IsNullOrEmpty(error.error) ?
                    System.Text.RegularExpressions.Regex.Replace(error.error, @"\byour\s+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase) :
                    "Couldn't connect to the shop. Please retry.");
            }
            var result = JsonUtility.FromJson<T>(reply.body);
            if (result == null) throw new InvalidOperationException("Couldn't read the shop response. Please retry.");
            bool sandbox = result is Account account ? account.sandbox : ((Checkout)(object)result).sandbox;
            bool live = result is Account wallet ? wallet.live : ((Checkout)(object)result).live;
            if (sandbox != settings.sandbox || live == settings.sandbox)
                throw new InvalidOperationException("Shop environment mismatch. Check the connection settings.");
            return result;
        }
    }
}
