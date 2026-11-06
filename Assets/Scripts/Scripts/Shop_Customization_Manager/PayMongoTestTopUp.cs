using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>Server-priced packs with one account-bound pending checkout and confirmed cancellation.</summary>
public class PayMongoTestTopUp : MonoBehaviour
{
    // Kept for migration of the original Main Menu card; the grid now owns the views.
    [SerializeField] private UnityEngine.UI.Button buyButton;
    [SerializeField] private TMP_Text buyLabel, pendingPayment, packName, packPrice;
    [SerializeField] private ShopManager shop;
    [SerializeField] private GameObject confirmation;
    [SerializeField] private TMP_Text confirmationMessage, confirmationHeading;
    [SerializeField] private UnityEngine.UI.Button confirmButton, cancelButton;
    [SerializeField] private RectTransform packFrame;
    [SerializeField] private float normalCardHeight = 400f, pendingCardHeight = 400f;
    [SerializeField] private GemPackCard[] cards;
    private bool busy, paymentsEnabled, needsReview, processing, cancelling, orderVerified, initialLoadComplete;
    private float nextCheck;
    private int generation;
    private string accountKey;
    private CommerceClient.Pack selected;
    private CommerceClient.Pack[] catalog;
#if UNITY_EDITOR
    internal static Action<string> ValidationOpenUrl;
#endif
    private string CurrentKey => CommerceClient.Settings == null || string.IsNullOrEmpty(CommerceClient.UserId) ? null :
        "CommerceOrder_" + CommerceClient.Settings.backendUrl + "_" + CommerceClient.UserId;
    private bool HasPending => accountKey != null &&
        (PlayerPrefs.HasKey(accountKey) || PlayerPrefs.HasKey(accountKey + "_request"));
    private string PendingProduct => PlayerPrefs.GetString(accountKey + "_product", "gems_5");
    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmPurchase);
        if (cancelButton != null) cancelButton.onClick.AddListener(CancelPurchase);
        CancelPurchase();
    }
    private void OnEnable() { BindAccount(); }
    private void OnDisable() { generation++; busy = false; CancelPurchase(); }
    private void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(ConfirmPurchase);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(CancelPurchase);
    }
    private void BindAccount()
    {
        generation++; busy = paymentsEnabled = needsReview = processing = orderVerified = initialLoadComplete = false;
        accountKey = CurrentKey; selected = null; nextCheck = 0;
        if (cards != null)
            foreach (var card in cards)
                if (card != null && card.Pack != null) card.Bind(card.Pack, SelectPack, RequestCancellation);
        CancelPurchase(); RefreshCard();
    }
    private bool Current(int version, string key) => this != null && isActiveAndEnabled &&
        version == generation && key == accountKey && key == CurrentKey;
    private void Update()
    {
        if (accountKey != CurrentKey) BindAccount();
        if (busy || accountKey == null || (confirmation != null && confirmation.activeSelf) || Time.unscaledTime < nextCheck) return;
        CheckOrder();
    }
    private void OnApplicationFocus(bool focused) { if (focused) nextCheck = 0; }
    public void BuyFiveGems()
    {
        if (cards != null && cards.Length > 0 && cards[0].Pack != null) SelectPack(cards[0]);
    }
    private void SelectPack(GemPackCard card)
    {
        if (busy || accountKey == null || accountKey != CurrentKey) return;
        if (HasPending) { if (card.Pack.id == PendingProduct && !needsReview && !processing) ResumePayment(); return; }
        if (!paymentsEnabled || confirmation == null) return;
        selected = card.Pack; cancelling = false;
        if (confirmationHeading != null) confirmationHeading.text = "CONFIRM PURCHASE";
        confirmationMessage.text = "Buy " + selected.gems + " Gems for PHP " + (selected.amount / 100m).ToString("N2", System.Globalization.CultureInfo.InvariantCulture) + "?\nPayment opens in the browser.";
        ShowConfirmation();
    }
    private void ShowConfirmation()
    {
        confirmation.transform.SetAsLastSibling(); confirmation.SetActive(true); RefreshCard();
        if (cancelButton != null) cancelButton.Select();
    }
    public void CancelPurchase() { if (confirmation != null) confirmation.SetActive(false); RefreshCard(); }
    public void ConfirmPurchase()
    {
        if (confirmation == null || !confirmation.activeInHierarchy || busy || accountKey == null || accountKey != CurrentKey) return;
        confirmation.SetActive(false);
        if (cancelling) CancelPayment();
        else if (paymentsEnabled && selected != null && !HasPending) CreateOrRecoverCheckout(true, false);
    }
    public void RequestCancellation()
    {
        if (busy || !HasPending || !orderVerified || !initialLoadComplete || needsReview || processing || confirmation == null) return;
        cancelling = true;
        if (confirmationHeading != null) confirmationHeading.text = "CANCEL PAYMENT?";
        confirmationMessage.text = "Close this unpaid checkout?\nIf payment has already started, we will verify it first.";
        ShowConfirmation();
    }
    private async void ResumePayment()
    {
        if (!PlayerPrefs.HasKey(accountKey)) { CreateOrRecoverCheckout(true, false); return; }
        int version = generation; string key = accountKey;
        SetBusy(true);
        try
        {
            var reply = await CommerceClient.CheckOrder(PlayerPrefs.GetString(key));
            if (!Current(version, key)) return;
            if (ApplyOrder(reply)) return;
            if (needsReview || processing) { Feedback("Payment is being verified. Please wait."); return; }
            string url = PlayerPrefs.GetString(key + "_url", "");
            if (!ValidCheckoutUrl(url)) throw new InvalidOperationException("The checkout link is unavailable. Please contact support.");
            OpenCheckout(url);
        }
        catch (Exception error) { if (Current(version, key)) Feedback(error.Message); }
        finally { if (Current(version, key)) { SetBusy(false); nextCheck = 0; } }
    }
    internal static bool ValidCheckoutUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var url) &&
        url.Scheme == "https" && url.Host == "checkout.paymongo.com" && url.IsDefaultPort && string.IsNullOrEmpty(url.UserInfo);
    private static void OpenCheckout(string url)
    {
#if UNITY_EDITOR
        if (ValidationOpenUrl != null) { ValidationOpenUrl(url); return; }
#endif
        Application.OpenURL(url);
    }
    private async void CreateOrRecoverCheckout(bool open, bool cancel)
    {
        int version = generation; string key = accountKey;
        if (busy || key == null) return;
        string request = PlayerPrefs.GetString(key + "_request", "");
        if (string.IsNullOrEmpty(request))
        {
            if (selected == null) return;
            request = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(key + "_request", request);
            PlayerPrefs.SetString(key + "_product", selected.id);
            PlayerPrefs.SetInt(key + "_gems", selected.gems); PlayerPrefs.SetInt(key + "_amount", selected.amount); PlayerPrefs.Save();
        }
        SetBusy(true); Feedback(cancel ? "Checking the checkout before cancellation..." : "Opening checkout...");
        bool cancelAfterRecovery = false;
        try
        {
            var reply = await CommerceClient.StartCheckout(request, PlayerPrefs.GetString(key + "_product", "gems_5"));
            if (string.IsNullOrEmpty(reply.orderId)) throw new InvalidOperationException("Invalid checkout response.");
            if (PlayerPrefs.GetString(key + "_request", "") != request) return;
            PlayerPrefs.SetString(key, reply.orderId);
            if (ValidCheckoutUrl(reply.checkoutUrl)) PlayerPrefs.SetString(key + "_url", reply.checkoutUrl);
            PlayerPrefs.Save();
            if (!Current(version, key)) return;
            if (ApplyOrder(reply)) return;
            if (cancel) { cancelAfterRecovery = true; return; }
            if (open && !processing && !needsReview && ValidCheckoutUrl(reply.checkoutUrl)) OpenCheckout(reply.checkoutUrl);
        }
        catch (Exception error) { if (Current(version, key)) Feedback(error.Message); }
        finally
        {
            if (Current(version, key))
            {
                SetBusy(false); nextCheck = Time.unscaledTime + 5;
                if (cancelAfterRecovery) CancelPayment();
            }
        }
    }
    private async void CancelPayment()
    {
        if (busy || !HasPending) return;
        if (!PlayerPrefs.HasKey(accountKey)) { CreateOrRecoverCheckout(false, true); return; }
        int version = generation; string key = accountKey;
        SetBusy(true); Feedback("Cancelling checkout...");
        try
        {
            var reply = await CommerceClient.CancelOrder(PlayerPrefs.GetString(key));
            if (!Current(version, key)) return;
            if (!ApplyOrder(reply)) Feedback("Payment may be processing. Please wait for confirmation.");
        }
        catch (Exception error) { if (Current(version, key)) Feedback(error.Message); }
        finally { if (Current(version, key)) { SetBusy(false); nextCheck = 0; } }
    }
    private bool ApplyOrder(CommerceClient.Checkout reply)
    {
        orderVerified = reply.status == "pending" || reply.status == "processing" || reply.status == "needs_review" ||
            (string.IsNullOrEmpty(reply.status) && ValidCheckoutUrl(reply.checkoutUrl));
        if (reply.status == "paid" || reply.status == "expired")
        {
            foreach (string suffix in new[] { "", "_request", "_url", "_product", "_gems", "_amount" }) PlayerPrefs.DeleteKey(accountKey + suffix);
            PlayerPrefs.Save(); needsReview = processing = orderVerified = false;
            if (reply.status == "paid") { CommerceClient.NotifyChanged(); Feedback("Payment confirmed. Gems added."); }
            else Feedback(reply.expiryReason == "player_cancelled" ? "Payment cancelled. No gems were added." : "Checkout expired. Choose a gem pack to try again.");
            RefreshCard(); return true;
        }
        needsReview = reply.status == "needs_review";
        processing = reply.status == "processing";
        if (!string.IsNullOrEmpty(reply.productId))
        {
            PlayerPrefs.SetString(accountKey + "_product", reply.productId);
            PlayerPrefs.SetInt(accountKey + "_gems", reply.gems); PlayerPrefs.SetInt(accountKey + "_amount", reply.amount);
        }
        if (ValidCheckoutUrl(reply.checkoutUrl)) PlayerPrefs.SetString(accountKey + "_url", reply.checkoutUrl);
        PlayerPrefs.Save(); RenderCatalog(); RefreshCard(); return false;
    }
    private async void CheckOrder()
    {
        int version = generation; string key = accountKey;
        SetBusy(true); nextCheck = Time.unscaledTime + 8;
        try
        {
            string order = PlayerPrefs.GetString(key, "");
            if (!string.IsNullOrEmpty(order))
            {
                try
                {
                    var reply = await CommerceClient.CheckOrder(order);
                    if (!Current(version, key)) return;
                    ApplyOrder(reply);
                }
                catch (Exception error)
                {
                    if (!Current(version, key)) return;
                    // Retain the receipt, but still load pack previews if checking
                    // an older checkout fails or the provider is temporarily down.
                    Feedback(error.Message);
                }
            }
            var wallet = await CommerceClient.Wallet();
            if (!Current(version, key)) return;
            bool validCatalog = AcceptCatalog(wallet.packs);
            paymentsEnabled = wallet.paymentsEnabled && validCatalog;
            RenderCatalog();
            if (!validCatalog && !HasPending) Feedback("Gem packs are updating. Please try again shortly.");
        }
        catch (Exception error)
        {
            if (Current(version, key))
            {
                paymentsEnabled = false;
                if (!HasPending) Feedback(error is CommerceClient.ShopSetupException ? "Top-ups are not available yet." : error.Message);
            }
        }
        finally
        {
            if (Current(version, key))
            {
                initialLoadComplete = true; SetBusy(false); nextCheck = Time.unscaledTime + 8;
            }
        }
    }
    private bool AcceptCatalog(CommerceClient.Pack[] packs)
    {
        // A legacy/malformed response must not erase the scene's previews or
        // enable purchases with stale displayed prices. Await a valid catalog.
        if (packs == null || packs.Length == 0 || packs.Any(p => p == null || string.IsNullOrEmpty(p.id) ||
            p.gems <= 0 || p.amount < 100 || p.currency != "PHP") || packs.Select(p => p.id).Distinct().Count() != packs.Length) return false;
        catalog = packs; return true;
    }
    private void RenderCatalog()
    {
        if (catalog == null || catalog.Length == 0 || cards == null || cards.Length == 0) return;
        var packs = catalog.Where(p => p != null && !string.IsNullOrEmpty(p.id) && p.gems > 0 && p.amount >= 100 && p.currency == "PHP").ToList();
        if (HasPending && PlayerPrefs.GetInt(accountKey + "_gems", 0) > 0)
        {
            var receipt = new CommerceClient.Pack { id = PendingProduct, gems = PlayerPrefs.GetInt(accountKey + "_gems"), amount = PlayerPrefs.GetInt(accountKey + "_amount"), currency = "PHP" };
            int index = packs.FindIndex(p => p.id == receipt.id);
            if (index < 0) packs.Add(receipt); else packs[index] = receipt;
        }
        var views = new List<GemPackCard>(cards);
        while (views.Count < packs.Count) views.Add(Instantiate(cards[0], cards[0].transform.parent));
        cards = views.ToArray();
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].gameObject.SetActive(i < packs.Count);
            if (i < packs.Count) cards[i].Bind(packs[i], SelectPack, RequestCancellation);
        }
    }
    private void SetBusy(bool value) { busy = value; RefreshCard(); }
    private void RefreshCard()
    {
        bool pending = HasPending;
        bool available = !busy && accountKey != null && accountKey == CurrentKey && (confirmation == null || !confirmation.activeSelf) &&
            (!CommerceClient.Sandbox || Application.isEditor || Debug.isDebugBuild);
        if (cards == null) return;
        foreach (var card in cards)
        {
            if (card == null) continue;
            bool owns = pending && card.Pack != null && card.Pack.id == PendingProduct;
            bool verified = orderVerified && initialLoadComplete;
            card.Present(owns && verified, owns && processing, owns && needsReview,
                available && card.Pack != null && (owns ? !processing && !needsReview : !pending && paymentsEnabled),
                available && owns && verified && !needsReview && !processing, owns && !verified);
        }
    }
    private void Feedback(string message) { if (shop != null) shop.ShowTopUpFeedback(message); }
}
