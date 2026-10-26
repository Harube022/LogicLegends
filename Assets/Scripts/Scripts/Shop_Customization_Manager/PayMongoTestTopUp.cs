using System;
using Firebase.Auth;
using TMPro;
using UnityEngine;

/// <summary>Opens hosted checkout; the backend alone confirms and credits payments.</summary>
public class PayMongoTestTopUp : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Button buyButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private bool hideWalletBalanceStatus;
    private bool busy;
    private bool paymentsEnabled;
    private float nextCheck;
    private int generation;
    private string OrderKey => "CommerceOrder_" + CommerceClient.Settings.backendUrl + "_" + FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
    private void OnEnable()
    {
        generation++;
        busy = false;
        paymentsEnabled = false;
        if (buyButton != null) buyButton.interactable = false;
        SetStatus("Checking top-up availability...");
        nextCheck = 0;
    }
    private void OnDisable() { generation++; busy = false; }
    private bool Current(int version) => this != null && isActiveAndEnabled && version == generation;
    private void Update()
    {
        if (busy || !FirebaseManager.IsReady || CommerceClient.Settings == null || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 8;
        if (FirebaseAuth.DefaultInstance.CurrentUser != null) CheckOrder();
    }
    private void OnApplicationFocus(bool focused) { if (focused) nextCheck = 0; }
    public async void BuyFiveGems()
    {
        if (busy || !paymentsEnabled || !FirebaseManager.IsReady || CommerceClient.Settings == null) return;
        int version = generation;
        SetBusy(true);
        string key = OrderKey;
        string requestKey = key + "_request";
        string requestId = PlayerPrefs.GetString(requestKey, "");
        if (string.IsNullOrEmpty(requestId))
        {
            requestId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(requestKey, requestId);
            PlayerPrefs.Save();
        }
        SetStatus("Opening checkout...");
        try
        {
            var reply = await CommerceClient.StartCheckout(requestId);
            if (!Current(version)) return;
            if (string.IsNullOrEmpty(reply.orderId) || !Uri.TryCreate(reply.checkoutUrl, UriKind.Absolute, out var url) ||
                url.Scheme != "https" || url.Host != "checkout.paymongo.com") throw new InvalidOperationException("Invalid checkout response.");
            PlayerPrefs.SetString(key, reply.orderId);
            PlayerPrefs.Save();
            Application.OpenURL(reply.checkoutUrl);
            SetStatus("Complete payment, then return to the game.");
        }
        catch (Exception error) { if (Current(version)) SetStatus(error.Message); }
        finally { if (Current(version)) { SetBusy(false); nextCheck = Time.unscaledTime + 5; } }
    }
    private async void CheckOrder()
    {
        int version = generation;
        SetBusy(true);
        string key = OrderKey;
        try
        {
            string order = PlayerPrefs.GetString(key, "");
            if (!string.IsNullOrEmpty(order))
            {
                var reply = await CommerceClient.CheckOrder(order);
                if (!Current(version)) return;
                if (reply.status == "paid" || reply.status == "expired")
                {
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.DeleteKey(key + "_request");
                    PlayerPrefs.Save();
                    if (reply.status == "paid") CommerceClient.NotifyChanged();
                }
                else { SetStatus(reply.status == "needs_review" ? "Payment needs review. Please contact support." : "Payment pending. Gems will update after confirmation."); return; }
            }
            var wallet = await CommerceClient.Wallet();
            if (Current(version))
            {
                paymentsEnabled = wallet.paymentsEnabled;
                SetStatus(paymentsEnabled ? (wallet.sandbox ? "Sandbox gems: " : "Gems: ") + wallet.gems : "Top-ups are currently unavailable.");
            }
        }
        catch (Exception error) { if (Current(version)) { paymentsEnabled = false; SetStatus(error is CommerceClient.ShopSetupException ? "Top-ups are not available yet." : error.Message); } }
        finally
        {
            if (Current(version)) { SetBusy(false); nextCheck = Time.unscaledTime + 8; }
        }
    }
    private void SetBusy(bool value) { busy = value; if (buyButton != null) buyButton.interactable = !value && paymentsEnabled && (!CommerceClient.Sandbox || Application.isEditor || Debug.isDebugBuild); }
    private void SetStatus(string value)
    {
        if (hideWalletBalanceStatus && value != null &&
            (value.StartsWith("Gems:", StringComparison.Ordinal) ||
             value.StartsWith("Sandbox gems:", StringComparison.Ordinal))) value = "";
        if (statusText != null) statusText.text = value;
    }
}
