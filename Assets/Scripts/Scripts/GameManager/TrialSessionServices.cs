using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase.Auth;
using System;
using System.Threading.Tasks;

/// <summary>Refreshes account-bound services across scene loads and account switches.</summary>
[DefaultExecutionOrder(-32000)]
public sealed class TrialSessionServices : MonoBehaviour
{
    static string acceptedUid;
    static float nextCheck;
    static bool checking;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { acceptedUid = null; nextCheck = 0; checking = false; }
    internal static void AcceptAccount(string uid)
    {
#if UNITY_EDITOR
        if (TrialBackendClient.ValidationTransport != null) return;
#endif
        acceptedUid = uid;
        nextCheck = Time.realtimeSinceStartup + 15f;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        var go = new GameObject("Trial Account Services"); DontDestroyOnLoad(go);
        go.AddComponent<TrialSessionServices>();
    }
    void OnEnable() { SceneManager.sceneLoaded += SceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= SceneLoaded; }
    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        TrialRewards.SceneLoaded();
        if (TrialRewards.CurrentTrial > 0)
        {
            var admin = FindFirstObjectByType<AdminDebugController>(FindObjectsInactive.Include);
            if (admin == null) admin = new GameObject("Trial Admin").AddComponent<AdminDebugController>();
            else { admin.gameObject.SetActive(true); admin.enabled = true; }
            // Every solo trial enables the shared entry point. Authentication,
            // modal safety and the actions' backend checks still gate access.
            admin.AdminButtonOn();
        }
    }
    void Update()
    {
        string uid = TrialBackendClient.UserId;
        if (acceptedUid != null && acceptedUid != uid)
        {
            acceptedUid = null;
            AdminAuthorization.Refresh();
            if (SceneManager.GetActiveScene().name != "Main Menu")
                ReturnToLogin("Your account session changed. Please sign in again.");
        }
        if (Application.isFocused && acceptedUid != null && Time.realtimeSinceStartup >= nextCheck && !checking)
            CheckInBackground();
        AdminAuthorization.Tick();
        TrialRewards.Tick();
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused && acceptedUid != null && Time.realtimeSinceStartup + 90f >= nextCheck && !checking)
            CheckInBackground();
    }

    static async void CheckInBackground()
    {
        checking = true;
        nextCheck = Time.realtimeSinceStartup + 30f;
        try { await RequireAccess(); }
        catch (Exception) { }
        finally { checking = false; }
    }

    internal static async Task<TrialBackendClient.Access> RequireAccess()
    {
        string uid = TrialBackendClient.UserId;
        try
        {
            var access = await AdminAuthorization.Verify();
            AcceptAccount(access.uid);
            return access;
        }
        catch (TrialBackendClient.AccountException error) when (error.AccessDenied)
        {
            if (uid != null && uid == TrialBackendClient.UserId && FirebaseManager.IsReady)
            {
                acceptedUid = null;
                FirebaseAuth.DefaultInstance.SignOut();
                ReturnToLogin(error.Message);
            }
            throw;
        }
    }

    static void ReturnToLogin(string message)
    {
        AdminAuthorization.RememberDenial(message);
        if (Photon.Pun.PhotonNetwork.InRoom || Photon.Pun.PhotonNetwork.IsConnected)
            MultiplayerLobbyService.Instance?.LeaveLobby();
        if (SceneManager.GetActiveScene().name == "Main Menu") return;
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main Menu");
    }
}
