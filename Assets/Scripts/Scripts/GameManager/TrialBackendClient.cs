using System;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Uses the existing authenticated account service and wallet, never a second currency balance.</summary>
public static class TrialBackendClient
{
    [Serializable] public sealed class Access { public bool admin; public string uid; }
    [Serializable] public sealed class Attempt
    {
        public string attemptId, status;
        public int trial, retries, reward, coins;
        public bool adminUsed;
    }
    [Serializable] public sealed class Request
    {
        public int trial;
        public string attemptId, operationId;
        public bool newAttempt;
    }
    [Serializable] sealed class Error { public string error; }
#if UNITY_EDITOR
    // Only the opt-in offline checks use this transport. It is absent from player builds.
    internal static Func<string, string, Task<string>> ValidationTransport;
    internal static string ValidationUid, ValidationEmail;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetValidation() { ValidationTransport = null; ValidationUid = ValidationEmail = null; }
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
    public static string Email
    {
        get
        {
#if UNITY_EDITOR
            if (ValidationTransport != null) return ValidationEmail;
#endif
            return FirebaseManager.IsReady ? FirebaseAuth.DefaultInstance.CurrentUser?.Email : null;
        }
    }
    public static Task<Access> LoadAccess() => Send<Access>("/trials/access", null);
    public static Task<Attempt> SendAttempt(string action, Request request) => Send<Attempt>("/trials/" + action, JsonUtility.ToJson(request));
    static async Task<T> Send<T>(string path, string json) where T : class
    {
#if UNITY_EDITOR
        if (ValidationTransport != null) return JsonUtility.FromJson<T>(await ValidationTransport(path, json));
#endif
        var settings = CommerceClient.Settings;
        if (settings == null || !Uri.TryCreate(settings.backendUrl, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != "https" && !(Application.isEditor && endpoint.IsLoopback)))
            throw new InvalidOperationException("The account connection is not configured.");
        if (!FirebaseManager.IsReady) throw new InvalidOperationException("Please wait for account services.");
        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null) throw new InvalidOperationException("Sign in to save your trial reward.");
        string uid = user.UserId;
        string token = await CommerceClient.WithTimeout(user.TokenAsync(false));
        if (UserId != uid) throw new InvalidOperationException("Your account changed.");
        using (var request = new UnityWebRequest(settings.backendUrl.TrimEnd('/') + path, json == null ? "GET" : "POST"))
        {
            request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 25;
            request.SetRequestHeader("Authorization", "Bearer " + token);
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (UserId != uid) throw new InvalidOperationException("Your account changed.");
            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode == 401 || request.responseCode == 403) AdminAuthorization.Refresh();
                Error error = null;
                try { error = JsonUtility.FromJson<Error>(request.downloadHandler.text); } catch (Exception) { }
                throw new InvalidOperationException(error != null && !string.IsNullOrEmpty(error.error) ? error.error : "Reward save pending. Check your connection.");
            }
            return JsonUtility.FromJson<T>(request.downloadHandler.text);
        }
    }
}
