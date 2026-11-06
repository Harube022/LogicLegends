using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Finishes pending multiplayer receipts even after returning to the menu.</summary>
[DefaultExecutionOrder(-31000)]
public sealed class MultiplayerRewardPump : MonoBehaviour
{
    static MultiplayerRewardPump current;
    readonly HashSet<string> shown = new HashSet<string>();
    string noticeAccount;
    float menuSince = -1, nextNoticeCheck;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { current = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        if (current != null) return;
        var instance = new GameObject("Multiplayer Reward Sync");
        DontDestroyOnLoad(instance);
        current = instance.AddComponent<MultiplayerRewardPump>();
    }
    void Awake()
    {
        if (current != null && current != this) { Destroy(gameObject); return; }
        current = this;
    }
    void Update() { MultiplayerRewardOutbox.Tick(); UpdateNotice(); }
    void UpdateNotice()
    {
        if (SceneManager.GetActiveScene().name != "Main Menu") { menuSince = -1; return; }
        string uid = MultiplayerRewardBackendClient.UserId;
        if (uid != noticeAccount) { noticeAccount = uid; menuSince = -1; }
        if (string.IsNullOrEmpty(uid)) return;
        if (menuSince < 0) menuSince = Time.unscaledTime;
        if (Time.unscaledTime - menuSince < 2f || Time.unscaledTime < nextNoticeCheck) return;
        nextNoticeCheck = Time.unscaledTime + .5f;
        var service = MultiplayerLobbyService.Instance;
        if (service == null || service.Busy || service.Starting || service.IsRecovering ||
            FindFirstObjectByType<MultiplayerMenuManager>()?.LobbyUI?.IsOpen != true) return;
        var receipt = MultiplayerRewardOutbox.GetLatestReceipt();
        if (receipt == null || MultiplayerRewardBackendClient.UserId != uid) return;
        string key = uid + ":" + receipt.matchId;
        if (!shown.Add(key)) return;
        service.Notify("Last match · " + MultiplayerRewardOutbox.Status(receipt.matchId));
    }
    void OnDestroy() { if (current == this) current = null; }
}
