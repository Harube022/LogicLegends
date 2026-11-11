using System;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

internal static class CommunityBackendClient
{
    internal const string Site = "https://logiclegends.netlify.app";
    [Serializable] internal sealed class Submission
    {
        public string kind, category, subject, description, targetUid, reason;
    }
    [Serializable] sealed class Reply { public bool submitted; public string id, error; }

    internal static async Task Submit(Submission submission)
    {
        if (!FirebaseManager.IsReady) throw new InvalidOperationException("Sign in to submit.");
        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null) throw new InvalidOperationException("Sign in to submit.");
        string token = await CommerceClient.WithTimeout(user.TokenAsync(false));
        if (TrialBackendClient.UserId != user.UserId) throw new InvalidOperationException("Your account changed.");
        using var request = new UnityWebRequest(Site + "/.netlify/functions/education?action=game-feedback", "POST");
        request.redirectLimit = 0;
        request.timeout = 25;
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(submission)));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Authorization", "Bearer " + token);
        request.SetRequestHeader("Content-Type", "application/json");
        var operation = request.SendWebRequest();
        while (!operation.isDone) await Task.Yield();
        if (TrialBackendClient.UserId != user.UserId) throw new InvalidOperationException("Your account changed.");
        Reply reply = null;
        try { reply = JsonUtility.FromJson<Reply>(request.downloadHandler.text); } catch (ArgumentException) { }
        if (request.result != UnityWebRequest.Result.Success || reply?.submitted != true || string.IsNullOrEmpty(reply.id))
            throw new InvalidOperationException(request.responseCode >= 400 && request.responseCode < 500 && !string.IsNullOrEmpty(reply?.error)
                ? reply.error : "Couldn't confirm submission. Your draft is still here. Check your connection and try again.");
    }
}
