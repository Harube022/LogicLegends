using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Fake transport and browser: verifies checkout recovery without creating real payments.</summary>
public static class TopUpPaymentChecks
{
    const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    const BindingFlags StaticHidden = BindingFlags.NonPublic | BindingFlags.Static;
    static readonly List<string> report = new List<string>();
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    static void Client(string field, object value) => typeof(CommerceClient).GetField(field, StaticHidden).SetValue(null, value);
    static void Invoke(object target, string method) => target.GetType().GetMethod(method, Hidden).Invoke(target, null);
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        report.Add("PASS " + message);
    }
    [Serializable] sealed class Reply { public bool success; public long status; public string body; }
    [Serializable] sealed class Request { public string requestId; }
    static string Response(int status, string body) => JsonUtility.ToJson(new Reply { status = status, success = status == 200, body = body });
    static TMP_Text Label(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false); return go.GetComponent<TMP_Text>();
    }
    static bool running;
    [MenuItem("Logic Legends/Main Menu/Check Gem Payment Recovery")]
    public static async void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || running) throw new InvalidOperationException("Run outside Play Mode.");
        running = true; report.Clear();
        string uid = "topup-fixture-" + Guid.NewGuid().ToString("N");
        string key = "CommerceOrder_" + CommerceClient.Settings.backendUrl + "_" + uid;
        var root = new GameObject("Top-up payment fixture") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        try
        {
            string mode = CommerceClient.Sandbox ? "\"sandbox\":true,\"live\":false" : "\"sandbox\":false,\"live\":true";
            string checkoutUrl = "https://checkout.paymongo.com/fixture-checkout";
            string orderId = new string('a', 64), status = "pending";
            int posts = 0, opened = 0;
            var sentRequests = new List<string>();
            TaskCompletionSource<string> delayed = null;
            bool loseReply = false;
            Client("ValidationUid", uid); Client("warmup", null); Client("warmupUrl", null); Client("readyUntil", 0f);
            Client("ValidationRequest", (Func<string, string, string, int, Task<string>>)((url, json, token, timeout) =>
            {
                if (url.EndsWith("/health")) return Task.FromResult(Response(200, "{\"mode\":\"live\"}"));
                if (url.EndsWith("/checkout"))
                {
                    posts++; sentRequests.Add(JsonUtility.FromJson<Request>(json).requestId);
                    if (delayed != null) return delayed.Task;
                    if (loseReply) { loseReply = false; return Task.FromResult(Response(504, "")); }
                    return Task.FromResult(Response(200, "{" + mode + ",\"orderId\":\"" + orderId + "\",\"checkoutUrl\":\"" + checkoutUrl + "\"}"));
                }
                if (url.Contains("/orders/")) return Task.FromResult(Response(200, "{" + mode + ",\"orderId\":\"" + orderId + "\",\"status\":\"" + status + "\"}"));
                return Task.FromResult(Response(200, "{" + mode + ",\"paymentsEnabled\":true,\"gems\":7}"));
            }));
            typeof(PayMongoTestTopUp).GetField("ValidationOpenUrl", StaticHidden).SetValue(null, (Action<string>)(url => { Check(url == checkoutUrl, "browser receives only the existing approved checkout link"); opened++; }));
            var topUp = root.AddComponent<PayMongoTestTopUp>();
            var buy = new GameObject("Buy", typeof(RectTransform), typeof(UnityEngine.UI.Button)).GetComponent<UnityEngine.UI.Button>();
            buy.transform.SetParent(root.transform, false);
            var label = Label("BuyLabel", buy.transform); var pending = Label("Pending", root.transform);
            var name = Label("Name", root.transform); name.text = "5 Gems";
            var price = Label("Price", root.transform); price.text = "PHP 1.00";
            var confirm = new GameObject("Confirmation", typeof(RectTransform)); confirm.transform.SetParent(root.transform, false);
            Set(topUp, "buyButton", buy); Set(topUp, "buyLabel", label); Set(topUp, "pendingPayment", pending);
            Set(topUp, "packName", name); Set(topUp, "packPrice", price);
            Set(topUp, "confirmation", confirm); Set(topUp, "confirmationMessage", Label("Message", confirm.transform));
            var frame = new GameObject("PackFrame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(root.transform, false); frame.sizeDelta = new Vector2(320, 400);
            Set(topUp, "packFrame", frame);
            root.SetActive(true); Invoke(topUp, "BindAccount"); Set(topUp, "paymentsEnabled", true);
            Check(frame.sizeDelta.y == 350 && frame.anchoredPosition.y + frame.sizeDelta.y * .5f == 200,
                "normal card removes the empty payment footer while keeping its top edge fixed");
            topUp.BuyFiveGems();
            Check(confirm.activeSelf && posts == 0 && opened == 0, "Buy opens confirmation without creating checkout or opening a browser");
            topUp.CancelPurchase(); topUp.ConfirmPurchase();
            Check(posts == 0 && !PlayerPrefs.HasKey(key + "_request"), "cancel creates no order or request ID");
            topUp.BuyFiveGems(); topUp.ConfirmPurchase(); topUp.ConfirmPurchase();
            await Task.Yield();
            Check(posts == 1 && opened == 1, "double confirm creates a single checkout");
            Check(PlayerPrefs.GetString(key) == orderId && PlayerPrefs.GetString(key + "_url") == checkoutUrl, "order ID and checkout URL are saved before redirect");
            Check(pending.gameObject.activeSelf && label.text == "CONTINUE PAYMENT", "pending status and continue action stay on the gem pack");
            Check(frame.sizeDelta.y == 400 && frame.anchoredPosition.y + frame.sizeDelta.y * .5f == 200 && pending.text.Contains("Continue checkout to pay."),
                "pending card expands its footer without moving the heading or top edge");
            Invoke(topUp, "OnDisable"); Invoke(topUp, "BindAccount"); topUp.BuyFiveGems();
            Check(posts == 1 && opened == 2, "reopening the pack resumes the same checkout without a new order");
            Client("ValidationUid", uid + "-other"); Invoke(topUp, "Update");
            Check(!pending.gameObject.activeSelf && label.text == "BUY GEMS", "another account cannot inherit pending payment UI");
            Client("ValidationUid", uid); Invoke(topUp, "BindAccount"); status = "needs_review"; Invoke(topUp, "CheckOrder");
            await Task.Yield(); Check(!buy.interactable && pending.text.Contains("NEEDS REVIEW"), "review-required orders cannot resume payment");
            status = "paid"; Invoke(topUp, "CheckOrder"); await Task.Yield();
            Check(!PlayerPrefs.HasKey(key) && !PlayerPrefs.HasKey(key + "_url") && !PlayerPrefs.HasKey(key + "_request"), "confirmed payment clears the pending order and URL");
            Check(label.text == "BUY GEMS" && !pending.gameObject.activeSelf, "paid status restores the pack purchase button");
            Check(frame.sizeDelta.y == 350, "paid card returns to the compact Buy Gems layout");

            Set(topUp, "paymentsEnabled", true); loseReply = true;
            topUp.BuyFiveGems(); topUp.ConfirmPurchase(); await Task.Yield();
            string recoverId = PlayerPrefs.GetString(key + "_request");
            Check(!string.IsNullOrEmpty(recoverId) && !PlayerPrefs.HasKey(key), "lost response preserves the original request ID");
            topUp.BuyFiveGems(); await Task.Yield();
            Check(sentRequests.Last() == recoverId && sentRequests[sentRequests.Count - 2] == recoverId, "resume after timeout reuses the same idempotent checkout request");
            status = "expired"; Invoke(topUp, "CheckOrder"); await Task.Yield();
            Check(!PlayerPrefs.HasKey(key) && !PlayerPrefs.HasKey(key + "_url"), "expired checkout is cleared before another purchase");

            delayed = new TaskCompletionSource<string>(); Set(topUp, "paymentsEnabled", true);
            topUp.BuyFiveGems(); topUp.ConfirmPurchase(); int beforeClose = opened; Invoke(topUp, "OnDisable");
            delayed.SetResult(Response(200, "{" + mode + ",\"orderId\":\"" + orderId + "\",\"checkoutUrl\":\"" + checkoutUrl + "\"}"));
            await Task.Yield(); await Task.Yield();
            Check(PlayerPrefs.GetString(key + "_url") == checkoutUrl && opened == beforeClose, "closing the menu preserves a late checkout response without redirecting");
            var validate = typeof(PayMongoTestTopUp).GetMethod("ValidCheckoutUrl", StaticHidden);
            foreach (string invalid in new[] { "http://checkout.paymongo.com/a", "https://checkout.paymongo.com.attacker.test/a", "https://checkout.paymongo.com:444/a", "https://user@checkout.paymongo.com/a" })
                Check(!(bool)validate.Invoke(null, new object[] { invalid }), "unapproved checkout URL is rejected");
            report.Add("ALL GEM PAYMENT CHECKS PASSED");
        }
        catch (Exception error) { report.Add("FAIL " + error); }
        finally
        {
            Client("ValidationRequest", null); Client("ValidationUid", null); Client("warmup", null); Client("warmupUrl", null); Client("readyUntil", 0f);
            typeof(PayMongoTestTopUp).GetField("ValidationOpenUrl", StaticHidden).SetValue(null, null);
            foreach (string suffix in new[] { "", "_url", "_request" }) { PlayerPrefs.DeleteKey(key + suffix); PlayerPrefs.DeleteKey(key + "-other" + suffix); }
            PlayerPrefs.Save(); UnityEngine.Object.DestroyImmediate(root); running = false;
            System.IO.File.WriteAllLines("Library/TopUpPaymentChecks.txt", report);
            Debug.Log(report.Last());
        }
    }
}
