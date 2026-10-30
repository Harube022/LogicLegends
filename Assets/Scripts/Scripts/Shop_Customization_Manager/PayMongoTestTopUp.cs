using System;
using TMPro;
using UnityEngine;

/// <summary>Explicit checkout confirmation and account-bound recovery for the existing five-gem pack.</summary>
public class PayMongoTestTopUp : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Button buyButton;
    [SerializeField] private TMP_Text buyLabel, pendingPayment, packName, packPrice;
    [SerializeField] private ShopManager shop;
    [SerializeField] private GameObject confirmation;
    [SerializeField] private TMP_Text confirmationMessage;
    [SerializeField] private UnityEngine.UI.Button confirmButton, cancelButton;
    [Header("Gem Card Layout")]
    [SerializeField] private RectTransform packFrame;
    [SerializeField] private float normalCardHeight = 350f, pendingCardHeight = 400f;
    private bool busy, paymentsEnabled, needsReview;
    private float nextCheck;
    private int generation;
    private string accountKey;
#if UNITY_EDITOR
    internal static Action<string> ValidationOpenUrl;
#endif
    private string CurrentKey => CommerceClient.Settings == null || string.IsNullOrEmpty(CommerceClient.UserId) ? null :
        "CommerceOrder_" + CommerceClient.Settings.backendUrl + "_" + CommerceClient.UserId;
    private bool HasPending => accountKey != null &&
        (PlayerPrefs.HasKey(accountKey) || PlayerPrefs.HasKey(accountKey + "_request"));

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
        generation++; busy = paymentsEnabled = needsReview = false;
        accountKey = CurrentKey; nextCheck = 0;
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

    // Keep the scene's existing BuyFiveGems binding. This action never creates a new checkout directly.
    public void BuyFiveGems()
    {
        if (busy || accountKey == null || accountKey != CurrentKey) return;
        if (HasPending) { if (!needsReview) ResumePayment(); return; }
        if (!paymentsEnabled || confirmation == null) return;
        confirmationMessage.text = "Buy " + packName.text + " for " + packPrice.text + "?\nPayment opens in the browser.";
        confirmation.transform.SetAsLastSibling();
        confirmation.SetActive(true);
        RefreshCard();
        if (cancelButton != null) cancelButton.Select();
    }
    public void CancelPurchase()
    {
        if (confirmation != null) confirmation.SetActive(false);
        RefreshCard();
    }
    public void ConfirmPurchase()
    {
        if (confirmation == null || !confirmation.activeInHierarchy || busy || !paymentsEnabled ||
            accountKey == null || accountKey != CurrentKey || HasPending) return;
        confirmation.SetActive(false);
        CreateOrRecoverCheckout();
    }
    private void ResumePayment()
    {
        string url = PlayerPrefs.GetString(accountKey + "_url", "");
        if (ValidCheckoutUrl(url)) { OpenCheckout(url); nextCheck = 0; }
        else if (PlayerPrefs.HasKey(accountKey + "_request")) CreateOrRecoverCheckout();
        else Feedback("The pending checkout link is unavailable. Please contact support.");
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
    private async void CreateOrRecoverCheckout()
    {
        int version = generation;
        string key = accountKey;
        if (busy || key == null) return;
        string request = PlayerPrefs.GetString(key + "_request", "");
        if (string.IsNullOrEmpty(request))
        {
            request = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(key + "_request", request); PlayerPrefs.Save();
        }
        SetBusy(true); Feedback("Opening checkout...");
        try
        {
            // Reuse the same request ID after a lost response; never silently create another charge.
            var reply = await CommerceClient.StartCheckout(request);
            if (string.IsNullOrEmpty(reply.orderId) || !ValidCheckoutUrl(reply.checkoutUrl))
                throw new InvalidOperationException("Invalid checkout response.");
            // Persist before checking panel visibility: closing a tab must not lose a successful response.
            if (PlayerPrefs.GetString(key + "_request", "") != request) return;
            PlayerPrefs.SetString(key, reply.orderId);
            PlayerPrefs.SetString(key + "_url", reply.checkoutUrl); PlayerPrefs.Save();
            if (!Current(version, key)) return;
            RefreshCard(); OpenCheckout(reply.checkoutUrl);
        }
        catch (Exception error) { if (Current(version, key)) Feedback(error.Message); }
        finally { if (Current(version, key)) { SetBusy(false); nextCheck = Time.unscaledTime + 5; } }
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
                var reply = await CommerceClient.CheckOrder(order);
                if (!Current(version, key)) return;
                if (reply.status == "paid" || reply.status == "expired")
                {
                    PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + "_request");
                    PlayerPrefs.DeleteKey(key + "_url"); PlayerPrefs.Save();
                    needsReview = false;
                    if (reply.status == "paid") { CommerceClient.NotifyChanged(); Feedback("Payment confirmed. Gems added."); }
                    else Feedback("Payment expired. A new checkout can be started.");
                }
                else { needsReview = reply.status == "needs_review"; RefreshCard(); return; }
            }
            // An uncertain checkout retains its request ID until an explicit Resume action recovers it.
            if (HasPending) return;
            var wallet = await CommerceClient.Wallet();
            if (Current(version, key)) paymentsEnabled = wallet.paymentsEnabled;
        }
        catch (Exception error)
        {
            if (Current(version, key))
            {
                paymentsEnabled = false;
                // Pending status stays on its own pack, even if the connection drops.
                if (!HasPending) Feedback(error is CommerceClient.ShopSetupException ? "Top-ups are not available yet." : error.Message);
            }
        }
        finally { if (Current(version, key)) { SetBusy(false); nextCheck = Time.unscaledTime + 8; } }
    }
    private void SetBusy(bool value) { busy = value; RefreshCard(); }
    private void RefreshCard()
    {
        bool pending = HasPending;
        if (buyLabel != null) buyLabel.text = pending ? "CONTINUE PAYMENT" : "BUY GEMS";
        if (packFrame != null)
        {
            float height = pending ? pendingCardHeight : normalCardHeight;
            packFrame.sizeDelta = new Vector2(packFrame.sizeDelta.x, height);
            // Keep the heading and top edge steady; reserve the footer only for pending payments.
            packFrame.anchoredPosition = new Vector2(packFrame.anchoredPosition.x, (pendingCardHeight - height) * .5f);
        }
        if (pendingPayment != null)
        {
            pendingPayment.gameObject.SetActive(pending);
            pendingPayment.text = needsReview ? "PAYMENT NEEDS REVIEW\nPlease contact support." : "PAYMENT PENDING\nContinue checkout to pay.";
        }
        if (buyButton != null) buyButton.interactable = !busy && !needsReview && accountKey != null &&
            accountKey == CurrentKey && (pending || paymentsEnabled) && (confirmation == null || !confirmation.activeSelf) &&
            (!CommerceClient.Sandbox || Application.isEditor || Debug.isDebugBuild);
    }
    private void Feedback(string message) { if (shop != null) shop.ShowTopUpFeedback(message); }
}
