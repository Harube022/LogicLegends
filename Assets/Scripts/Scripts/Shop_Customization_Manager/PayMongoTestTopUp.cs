using System;
using System.Collections;
using System.Text;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Development-only checkout. Test gems never modify the real game wallet.</summary>
public class PayMongoTestTopUp : MonoBehaviour
{
    [SerializeField] private string backendUrl = "http://127.0.0.1:5001/demo-logic-legends/us-central1/payments";
    [SerializeField] private UnityEngine.UI.Button buyButton;
    [SerializeField] private TMP_Text statusText;
    private bool busy;
    private float nextCheck;
    private string OrderKey => "PayMongoTestOrder_" + (FirebaseAuth.DefaultInstance.CurrentUser?.UserId ?? "signed-out");

    [Serializable] private class CheckoutRequest { public string productId = "gems_5"; public string requestId; }
    [Serializable] private class Reply { public string orderId; public string checkoutUrl; public string status; public string error; public int gems; }

    private bool TestBuild => Application.isEditor || Debug.isDebugBuild;

    private void OnEnable()
    {
        if (buyButton != null) buyButton.interactable = TestBuild;
        SetStatus(TestBuild ? "Test mode - local gems only" : "Test top-ups are disabled in release builds.");
        nextCheck = 0;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        busy = false;
    }

    private void Update()
    {
        if (!TestBuild || busy || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 15;
        if (FirebaseAuth.DefaultInstance.CurrentUser != null) StartCoroutine(CheckOrder());
    }

    private void OnApplicationFocus(bool focused) { if (focused) nextCheck = 0; }

    public void BuyFiveGems()
    {
        if (!TestBuild || busy) return;
        if (FirebaseAuth.DefaultInstance.CurrentUser == null) { SetStatus("Sign in before testing a top-up."); return; }
        StartCoroutine(Buy());
    }

    private IEnumerator Buy()
    {
        SetBusy(true);
        SetStatus("Opening test checkout...");
        Reply reply = null;
        string orderKey = OrderKey;
        string requestKey = orderKey + "_request";
        string requestId = PlayerPrefs.GetString(requestKey, "");
        if (string.IsNullOrEmpty(requestId))
        {
            requestId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(requestKey, requestId);
            PlayerPrefs.Save();
        }
        var payload = JsonUtility.ToJson(new CheckoutRequest { requestId = requestId });
        yield return Request("/checkout", payload, value => reply = value);
        if (reply != null && !string.IsNullOrEmpty(reply.orderId) && Uri.TryCreate(reply.checkoutUrl, UriKind.Absolute, out var url)
            && url.Scheme == "https" && url.Host == "checkout.paymongo.com")
        {
            PlayerPrefs.SetString(orderKey, reply.orderId);
            PlayerPrefs.Save();
            SetStatus("Complete the test payment, then return here.");
            Application.OpenURL(reply.checkoutUrl);
        }
        SetBusy(false);
        nextCheck = Time.unscaledTime + 5;
    }

    private IEnumerator CheckOrder()
    {
        SetBusy(true);
        string key = OrderKey;
        string order = PlayerPrefs.GetString(key, "");
        if (!string.IsNullOrEmpty(order))
        {
            Reply reply = null;
            yield return Request("/orders/" + UnityWebRequest.EscapeURL(order), null, value => reply = value);
            if (reply != null && reply.status == "paid")
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.DeleteKey(key + "_request");
                PlayerPrefs.Save();
            }
            else if (reply != null) SetStatus("Payment pending. Test gems have not been granted.");
        }
        if (string.IsNullOrEmpty(PlayerPrefs.GetString(key, "")))
        {
            Reply wallet = null;
            yield return Request("/wallet", null, value => wallet = value);
            if (wallet != null) SetStatus("Local test gems: " + wallet.gems + " (separate from game gems)");
        }
        SetBusy(false);
    }

    private IEnumerator Request(string path, string json, Action<Reply> completed)
    {
        if (!Uri.TryCreate(backendUrl, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != "https" && !(Application.isEditor && endpoint.IsLoopback)))
        { SetStatus("Set a public HTTPS backend URL for APK testing."); yield break; }
        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null) { SetStatus("Sign in before testing a top-up."); yield break; }
        var tokenTask = user.TokenAsync(false);
        while (!tokenTask.IsCompleted) yield return null;
        if (tokenTask.IsFaulted || tokenTask.IsCanceled) { SetStatus("Could not verify login. Please sign in again."); yield break; }
        if (FirebaseAuth.DefaultInstance.CurrentUser?.UserId != user.UserId) yield break;
        using (var request = new UnityWebRequest(backendUrl.TrimEnd('/') + path, json == null ? "GET" : "POST"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 45;
            request.SetRequestHeader("Authorization", "Bearer " + tokenTask.Result);
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            yield return request.SendWebRequest();
            if (FirebaseAuth.DefaultInstance.CurrentUser?.UserId != user.UserId) yield break;
            Reply reply = null;
            try { reply = JsonUtility.FromJson<Reply>(request.downloadHandler.text); } catch (Exception) { }
            if (request.result != UnityWebRequest.Result.Success)
            { SetStatus(reply != null && !string.IsNullOrEmpty(reply.error) ? reply.error : "Local backend unavailable. Start the emulators and check the URL."); yield break; }
            completed(reply);
        }
    }

    private void SetBusy(bool value) { busy = value; if (buyButton != null) buyButton.interactable = TestBuild && !value; }
    private void SetStatus(string value) { if (statusText != null) statusText.text = value; }
}
