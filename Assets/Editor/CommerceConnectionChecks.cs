using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>Connection regression checks with fake transport; no network, payments or account writes.</summary>
public static class CommerceConnectionChecks
{
    const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    static readonly List<string> results = new List<string>();
    static bool running;
    static float clock;
    static void Set(string field, object value) => typeof(CommerceClient).GetField(field, Hidden).SetValue(null, value);
    static void Check(bool condition, string text)
    {
        if (!condition) throw new Exception(text);
        results.Add("PASS " + text);
    }
    static string Reply(long status, string body) => JsonUtility.ToJson(new Response { status = status, success = status == 200, body = body });
    [Serializable] sealed class Response { public long status; public bool success; public string body; }
    static string Account => CommerceClient.Sandbox ? "{\"sandbox\":true,\"live\":false,\"gems\":7}" : "{\"sandbox\":false,\"live\":true,\"gems\":7}";
    static void Fixture(Func<string, string, string, int, Task<string>> request)
    {
        Set("warmup", null); Set("warmupUrl", null); Set("readyUntil", 0f);
        clock = Time.realtimeSinceStartup;
        Set("ValidationTime", (Func<float>)(() => clock));
        Set("ValidationRequest", (Func<string, string, string, int, Task<string>>)(async (url, json, token, timeout) => {
            var result = await request(url, json, token, timeout);
            if (url.EndsWith("/health")) clock += timeout;
            return result;
        }));
        Set("ValidationUid", "connection-fixture");
    }
    static async Task ExpectFailure(Func<Task> action, string text)
    {
        bool failed = false;
        try { await action(); } catch (InvalidOperationException) { failed = true; }
        Check(failed, text);
    }
    [MenuItem("Logic Legends/Main Menu/Check Shop Connection Recovery")]
    public static async void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || running) throw new InvalidOperationException("Run outside Play Mode.");
        running = true; results.Clear();
        try
        {
            int health = 0, accounts = 0, wallets = 0;
            var wake = new TaskCompletionSource<string>();
            Fixture((url, json, token, timeout) =>
            {
                if (url.EndsWith("/health")) { health++; Check(token == null && timeout == 25, "warmup is unauthenticated and bounded"); return wake.Task; }
                if (url.EndsWith("/account")) accounts++;
                if (url.EndsWith("/wallet")) wallets++;
                return Task.FromResult(Reply(200, Account));
            });
            CommerceClient.Prewarm();
            var shop = CommerceClient.LoadAccount("Female_Character"); var topup = CommerceClient.Wallet();
            Check(health == 1 && accounts == 0 && wallets == 0 && !shop.IsCompleted && !topup.IsCompleted,
                "clothes and top-up share the pending menu warmup");
            wake.SetResult(Reply(200, "{\"mode\":\"live\"}"));
            await Task.WhenAll(shop, topup);
            Check(accounts == 1 && wallets == 1 && shop.Result.gems == 7, "both panels recover after the host wakes");
            await CommerceClient.Wallet();
            Check(health == 1, "warm reads do not repeat health checks");

            health = 0;
            Fixture((url, json, token, timeout) => Task.FromResult(url.EndsWith("/health") ?
                (++health == 1 ? Reply(503, "starting") : health == 2 ? Reply(200, "<html>Starting</html>") : Reply(200, "{\"mode\":\"live\"}")) : Reply(200, Account)));
            await CommerceClient.Wallet();
            Check(health == 3, "temporary 503 and HTML holding page recover automatically");

            health = 0;
            Fixture((url, json, token, timeout) => { health++; return Task.FromResult(Reply(0, "")); });
            await ExpectFailure(async () => { await CommerceClient.Wallet(); }, "unreachable host eventually exits to retry");
            Check(health == 4, "offline warmup exhausts its 80 second budget, including the shorter final request");

            int reads = 0;
            Fixture((url, json, token, timeout) => Task.FromResult(url.EndsWith("/health") ? Reply(200, "{\"mode\":\"live\"}") :
                ++reads == 1 ? Reply(504, "") : Reply(200, Account)));
            await CommerceClient.Wallet(); Check(reads == 2, "transient browsing read retries once");

            foreach (int status in new[] { 401, 403 })
            {
                reads = 0;
                Fixture((url, json, token, timeout) => { if (url.EndsWith("/health")) return Task.FromResult(Reply(200, "{\"mode\":\"live\"}")); reads++; return Task.FromResult(Reply(status, "{\"error\":\"Please sign in again.\"}")); });
                await ExpectFailure(async () => { await CommerceClient.Wallet(); }, "authorization failure stays denied: " + status);
                Check(reads == 1, "authorization failure is not retried: " + status);
            }
            foreach (string path in new[] { "/purchase", "/equip", "/checkout" })
            {
                int writes = 0;
                Fixture((url, json, token, timeout) => { writes++; return Task.FromResult(Reply(504, "")); });
                await ExpectFailure(async () => {
                    if (path == "/checkout") await CommerceClient.StartCheckout("fixture");
                    else if (path == "/equip") await CommerceClient.EquipItem("fixture");
                    else await CommerceClient.BuyItem("fixture");
                }, "ambiguous write fails without replay: " + path);
                Check(writes == 1, "one request only: " + path);
            }
            reads = 0; wake = new TaskCompletionSource<string>();
            Fixture((url, json, token, timeout) => { if (url.EndsWith("/health")) return wake.Task; reads++; return Task.FromResult(Reply(200, Account)); });
            var stale = CommerceClient.Wallet(); Set("ValidationUid", "different-fixture");
            wake.SetResult(Reply(200, "{\"mode\":\"live\"}"));
            await ExpectFailure(async () => { await stale; }, "account switch invalidates a pending connection");
            Check(reads == 0, "old account is never requested after the switch");
            results.Add("ALL SHOP CONNECTION CHECKS PASSED");
        }
        catch (Exception error) { results.Add("FAIL " + error); }
        finally
        {
            Set("ValidationRequest", null); Set("ValidationTime", null); Set("ValidationUid", null); Set("warmup", null); Set("warmupUrl", null); Set("readyUntil", 0f);
            running = false;
            System.IO.File.WriteAllLines("Library/CommerceConnectionChecks.txt", results);
            Debug.Log(results[results.Count - 1]);
        }
    }
}
