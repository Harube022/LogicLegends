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
    private static Task warmup;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Changed = null; warmup = null; }
    // Wake the hosted service while the Main Menu/login is visible rather than
    // making the first shop visit absorb the entire cold-start delay.
    public static void Prewarm()
    {
        if (warmup == null || warmup.IsCompleted) warmup = Warmup();
    }
    private static async Task Warmup()
    {
        var settings = Settings;
        if (settings == null || !Uri.TryCreate(settings.backendUrl, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != "https" && !(Application.isEditor && endpoint.IsLoopback))) return;
        using (var request = UnityWebRequest.Get(settings.backendUrl.TrimEnd('/') + "/health"))
        {
            request.timeout = 20;
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
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
            throw new TimeoutException("Couldn't connect. Check your connection and retry.");
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
        if (!FirebaseManager.IsReady) throw new InvalidOperationException("Please wait for login services.");
        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null) throw new InvalidOperationException("Please sign in to use the shop.");
        string token = await WithTimeout(user.TokenAsync(false));
        if (FirebaseAuth.DefaultInstance.CurrentUser?.UserId != user.UserId) throw new InvalidOperationException("Your account changed. Please reopen the shop.");
        using (var request = new UnityWebRequest(settings.backendUrl.TrimEnd('/') + path, json == null ? "GET" : "POST"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            // Browsing must reach a retry state promptly on a slow/unreachable
            // host. Purchase/checkout requests keep their existing write timeout.
            request.timeout = path == "/account" || path == "/wallet" ? 20 : 70;
            request.SetRequestHeader("Authorization", "Bearer " + token);
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (FirebaseAuth.DefaultInstance.CurrentUser?.UserId != user.UserId) throw new InvalidOperationException("Your account changed. Please reopen the shop.");
            if (request.result != UnityWebRequest.Result.Success)
            {
                ErrorReply error = null;
                try { error = JsonUtility.FromJson<ErrorReply>(request.downloadHandler.text); } catch (Exception) { }
                if (request.responseCode == 503 && error != null && (error.code == "SHOP_SETUP" ||
                    error.error == "The shop is being configured. Please try again later."))
                    throw new ShopSetupException();
                throw new InvalidOperationException(error != null && !string.IsNullOrEmpty(error.error) ? error.error : "Couldn't connect to the shop. Please retry.");
            }
            var result = JsonUtility.FromJson<T>(request.downloadHandler.text);
            bool sandbox = result is Account account ? account.sandbox : ((Checkout)(object)result).sandbox;
            bool live = result is Account wallet ? wallet.live : ((Checkout)(object)result).live;
            if (sandbox != settings.sandbox || live == settings.sandbox)
                throw new InvalidOperationException("Shop environment mismatch. Check the connection settings.");
            return result;
        }
    }
}
