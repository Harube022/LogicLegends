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
    [Serializable] sealed class Request { public string requestId, productId; }
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
            int posts = 0, opened = 0, cancellations = 0;
            bool loseCancel = false;
            string cancelStatus = "expired", lastProduct = "";
            string packJson = "[{\"id\":\"gems_5\",\"gems\":5,\"amount\":100,\"currency\":\"PHP\"},{\"id\":\"gems_25\",\"gems\":25,\"amount\":500,\"currency\":\"PHP\"},{\"id\":\"gems_50\",\"gems\":50,\"amount\":1000,\"currency\":\"PHP\"},{\"id\":\"gems_100\",\"gems\":100,\"amount\":2000,\"currency\":\"PHP\"},{\"id\":\"gems_250\",\"gems\":250,\"amount\":5000,\"currency\":\"PHP\"},{\"id\":\"gems_500\",\"gems\":500,\"amount\":10000,\"currency\":\"PHP\"}]";
            var sentRequests = new List<string>();
            TaskCompletionSource<string> delayed = null;
            TaskCompletionSource<string> delayedOrder = null, delayedWallet = null;
            bool missingCatalog = false;
            bool loseReply = false;
            Client("ValidationUid", uid); Client("warmup", null); Client("warmupUrl", null); Client("readyUntil", 0f);
            Client("ValidationRequest", (Func<string, string, string, int, Task<string>>)((url, json, token, timeout) =>
            {
                if (url.EndsWith("/health")) return Task.FromResult(Response(200, "{\"mode\":\"live\"}"));
                if (url.EndsWith("/checkout"))
                {
                    posts++; sentRequests.Add(JsonUtility.FromJson<Request>(json).requestId);
                    lastProduct = JsonUtility.FromJson<Request>(json).productId;
                    if (delayed != null) return delayed.Task;
                    if (loseReply) { loseReply = false; return Task.FromResult(Response(504, "")); }
                    return Task.FromResult(Response(200, "{" + mode + ",\"orderId\":\"" + orderId + "\",\"checkoutUrl\":\"" + checkoutUrl + "\"}"));
                }
                if (url.EndsWith("/cancel"))
                {
                    cancellations++;
                    if (loseCancel) { loseCancel = false; return Task.FromResult(Response(504, "")); }
                    status = cancelStatus;
                }
                if (url.Contains("/orders/")) return delayedOrder != null ? delayedOrder.Task : Task.FromResult(Response(200, "{" + mode + ",\"orderId\":\"" + orderId + "\",\"status\":\"" + status + "\"}"));
                if (delayedWallet != null) return delayedWallet.Task;
                if (missingCatalog) return Task.FromResult(Response(200, "{" + mode + ",\"paymentsEnabled\":true,\"gems\":7}"));
                return Task.FromResult(Response(200, "{" + mode + ",\"paymentsEnabled\":true,\"gems\":7,\"packs\":" + packJson + "}"));
            }));
            typeof(PayMongoTestTopUp).GetField("ValidationOpenUrl", StaticHidden).SetValue(null, (Action<string>)(url => { Check(url == checkoutUrl, "browser receives only the existing approved checkout link"); opened++; }));
            var topUp = root.AddComponent<PayMongoTestTopUp>();
            var cell = new GameObject("Pack", typeof(RectTransform)); cell.transform.SetParent(root.transform, false);
            var buy = new GameObject("Buy", typeof(RectTransform), typeof(UnityEngine.UI.Button)).GetComponent<UnityEngine.UI.Button>();
            buy.transform.SetParent(cell.transform, false);
            var label = Label("BuyLabel", buy.transform); var pending = Label("Pending", cell.transform);
            var name = Label("Name", cell.transform); name.text = "5 Gems";
            var price = Label("Price", cell.transform); price.text = "PHP 1.00";
            var confirm = new GameObject("Confirmation", typeof(RectTransform)); confirm.transform.SetParent(root.transform, false);
            Set(topUp, "buyButton", buy); Set(topUp, "buyLabel", label); Set(topUp, "pendingPayment", pending);
            Set(topUp, "packName", name); Set(topUp, "packPrice", price);
            Set(topUp, "confirmation", confirm); Set(topUp, "confirmationMessage", Label("Message", confirm.transform));
            var frame = new GameObject("PackFrame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(root.transform, false); frame.sizeDelta = new Vector2(320, 400);
            Set(topUp, "packFrame", frame);
            var cancelPayment = new GameObject("CancelPayment", typeof(RectTransform), typeof(UnityEngine.UI.Button)).GetComponent<UnityEngine.UI.Button>();
            cancelPayment.transform.SetParent(cell.transform, false);
            var card = cell.AddComponent<GemPackCard>();
            Set(card, "packName", name); Set(card, "packPrice", price); Set(card, "actionLabel", label);
            Set(card, "paymentStatus", pending); Set(card, "buyButton", buy); Set(card, "cancelPaymentButton", cancelPayment);
            Set(card, "pack", new CommerceClient.Pack { id = "gems_5", gems = 5, amount = 100, currency = "PHP" });
            Set(topUp, "cards", new[] { card });
            root.SetActive(true); Invoke(topUp, "BindAccount");
            missingCatalog = true; Invoke(topUp, "CheckOrder"); await Task.Yield();
            Check(card.gameObject.activeSelf && !buy.interactable && !cancelPayment.gameObject.activeSelf,
                "first wallet load without packs retains the saved preview without showing Cancel");
            missingCatalog = false; Invoke(topUp, "CheckOrder"); await Task.Yield();
            Check(frame.sizeDelta.y == 400, "normal card reserves fixed payment-control space");
            topUp.BuyFiveGems();
            Check(confirm.activeSelf && posts == 0 && opened == 0, "Buy opens confirmation without creating checkout or opening a browser");
            topUp.CancelPurchase(); topUp.ConfirmPurchase();
            Check(posts == 0 && !PlayerPrefs.HasKey(key + "_request"), "cancel creates no order or request ID");
            topUp.BuyFiveGems(); topUp.ConfirmPurchase(); topUp.ConfirmPurchase();
            await Task.Yield();
            Check(posts == 1 && opened == 1, "double confirm creates a single checkout");
            Check(PlayerPrefs.GetString(key) == orderId && PlayerPrefs.GetString(key + "_url") == checkoutUrl, "order ID and checkout URL are saved before redirect");
            Check(pending.gameObject.activeSelf && label.text == "CONTINUE PAYMENT", "pending status and continue action stay on the gem pack");
            Check(frame.sizeDelta.y == 400 && cancelPayment.gameObject.activeSelf,
                "pending card reveals Cancel without changing its height or the row below");
            Invoke(topUp, "OnDisable"); Invoke(topUp, "BindAccount");
            Check(!cancelPayment.gameObject.activeSelf && label.text == "CHECK PAYMENT", "saved checkout does not show Cancel before verification");
            delayedOrder = new TaskCompletionSource<string>(); Invoke(topUp, "CheckOrder");
            Check(!cancelPayment.gameObject.activeSelf && !buy.interactable, "Cancel stays hidden while order verification is loading");
            topUp.RequestCancellation(); Check(!confirm.activeSelf, "unverified saved checkout cannot open a cancellation confirmation");
            delayedWallet = new TaskCompletionSource<string>();
            delayedOrder.SetResult(Response(200, "{" + mode + ",\"orderId\":\"" + orderId + "\",\"status\":\"pending\"}"));
            await Task.Yield(); await Task.Yield(); delayedOrder = null;
            Check(!cancelPayment.gameObject.activeSelf && !buy.interactable, "verified order still hides Cancel until the initial wallet load finishes");
            delayedWallet.SetResult(Response(200, "{" + mode + ",\"paymentsEnabled\":true,\"packs\":" + packJson + "}"));
            await Task.Yield(); await Task.Yield(); delayedWallet = null;
            Check(cancelPayment.gameObject.activeSelf, "Cancel appears after the server confirms a pending checkout");
            topUp.BuyFiveGems();
            Check(posts == 1 && opened == 2, "reopening the pack resumes the same checkout without a new order");
            Client("ValidationUid", uid + "-other"); Invoke(topUp, "Update");
            Check(!pending.gameObject.activeSelf && label.text == "BUY GEMS", "another account cannot inherit pending payment UI");
            Client("ValidationUid", uid); Invoke(topUp, "BindAccount"); status = "needs_review"; Invoke(topUp, "CheckOrder");
            await Task.Yield(); Check(!buy.interactable && pending.text.Contains("NEEDS REVIEW"), "review-required orders cannot resume payment");
            status = "paid"; Invoke(topUp, "CheckOrder"); await Task.Yield();
            Check(!PlayerPrefs.HasKey(key) && !PlayerPrefs.HasKey(key + "_url") && !PlayerPrefs.HasKey(key + "_request"), "confirmed payment clears the pending order and URL");
            Check(label.text == "BUY GEMS" && !pending.gameObject.activeSelf, "paid status restores the pack purchase button");
            Check(frame.sizeDelta.y == 400, "paid card keeps the same grid geometry");

            var visibleCards = root.GetComponentsInChildren<GemPackCard>(true);
            missingCatalog = true; Invoke(topUp, "CheckOrder"); await Task.Yield();
            Check(visibleCards.All(c => c.gameObject.activeSelf), "legacy wallet response without packs never hides the previews");
            Check(!buy.interactable, "missing catalog cannot enable purchases at unverified prices");
            var invalidCatalog = new[] { new CommerceClient.Pack { id = "bad", gems = 5, amount = -1, currency = "PHP" } };
            var accept = typeof(PayMongoTestTopUp).GetMethod("AcceptCatalog", Hidden);
            Check(!(bool)accept.Invoke(topUp, new object[] { Array.Empty<CommerceClient.Pack>() }) &&
                !(bool)accept.Invoke(topUp, new object[] { invalidCatalog }), "empty and malformed catalogs cannot replace known previews");
            delayedWallet = new TaskCompletionSource<string>(); Invoke(topUp, "CheckOrder");
            Check(visibleCards.All(c => c.gameObject.activeSelf) && !cancelPayment.gameObject.activeSelf, "pack previews remain visible during a delayed wallet load");
            delayedWallet.SetResult(Response(200, "{" + mode + ",\"paymentsEnabled\":true,\"packs\":" + packJson + "}"));
            await Task.Yield(); await Task.Yield(); delayedWallet = null; missingCatalog = false;
            Check(buy.interactable && visibleCards.All(c => c.gameObject.activeSelf), "valid catalog restores buying without rebuilding the grid");

            Set(topUp, "paymentsEnabled", true); loseReply = true;
            topUp.BuyFiveGems(); topUp.ConfirmPurchase(); await Task.Yield();
            string recoverId = PlayerPrefs.GetString(key + "_request");
            Check(!string.IsNullOrEmpty(recoverId) && !PlayerPrefs.HasKey(key), "lost response preserves the original request ID");
            topUp.BuyFiveGems(); await Task.Yield();
            Check(sentRequests.Last() == recoverId && sentRequests[sentRequests.Count - 2] == recoverId, "resume after timeout reuses the same idempotent checkout request");
            status = "expired"; Invoke(topUp, "CheckOrder"); await Task.Yield();
            Check(!PlayerPrefs.HasKey(key) && !PlayerPrefs.HasKey(key + "_url"), "expired checkout is cleared before another purchase");

            // Cancellation must be confirmed by the server, and a paid response wins.
            Set(topUp, "paymentsEnabled", true); status = "pending";
            topUp.BuyFiveGems(); topUp.ConfirmPurchase(); await Task.Yield();
            loseCancel = true; topUp.RequestCancellation(); topUp.ConfirmPurchase(); await Task.Yield();
            Check(PlayerPrefs.HasKey(key) && cancellations == 1, "lost cancellation response retains pending state");
            cancelStatus = "processing"; topUp.RequestCancellation(); topUp.ConfirmPurchase(); await Task.Yield();
            Check(PlayerPrefs.HasKey(key) && !buy.interactable && label.text == "VERIFYING...", "in-flight payment is verified instead of replaced");
            status = "pending"; Invoke(topUp, "CheckOrder"); await Task.Yield();
            cancelStatus = "paid"; topUp.RequestCancellation(); topUp.ConfirmPurchase(); await Task.Yield();
            Check(!PlayerPrefs.HasKey(key), "payment completed during cancellation clears only after paid confirmation");

            var allCards = (GemPackCard[])typeof(PayMongoTestTopUp).GetField("cards", Hidden).GetValue(topUp);
            Check(allCards.Length == 6, "all six server-priced gem packs are rendered");
            typeof(PayMongoTestTopUp).GetMethod("SelectPack", Hidden).Invoke(topUp, new object[] { allCards[5] });
            topUp.ConfirmPurchase(); await Task.Yield();
            Check(lastProduct == "gems_500" && PlayerPrefs.GetInt(key + "_amount") == 10000, "selected pack sends its own stable ID and PHP centavos");
            status = "expired"; Invoke(topUp, "CheckOrder"); await Task.Yield();

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
            foreach (string suffix in new[] { "", "_url", "_request", "_product", "_gems", "_amount" }) { PlayerPrefs.DeleteKey(key + suffix); PlayerPrefs.DeleteKey(key + "-other" + suffix); }
            PlayerPrefs.Save(); UnityEngine.Object.DestroyImmediate(root); running = false;
            System.IO.File.WriteAllLines("Library/TopUpPaymentChecks.txt", report);
            Debug.Log(report.Last());
        }
    }
}
