using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase.Auth;

/// <summary>Refreshes account-bound services across scene loads and account switches.</summary>
[DefaultExecutionOrder(-32000)]
public sealed class TrialSessionServices : MonoBehaviour
{
    FirebaseAuth auth;
    volatile bool authChanged;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        var go = new GameObject("Trial Account Services"); DontDestroyOnLoad(go);
        go.AddComponent<TrialSessionServices>();
    }
    void OnEnable() { SceneManager.sceneLoaded += SceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= SceneLoaded; if (auth != null) auth.StateChanged -= AuthChanged; }
    void AuthChanged(object sender, System.EventArgs args) { authChanged = true; }
    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        AdminAuthorization.Refresh(); TrialRewards.SceneLoaded();
        if (TrialRewards.CurrentTrial > 0)
        {
            var admin = FindFirstObjectByType<AdminDebugController>(FindObjectsInactive.Include);
            if (admin == null) new GameObject("Trial Admin").AddComponent<AdminDebugController>();
            else { admin.gameObject.SetActive(true); admin.enabled = true; }
        }
    }
    void Update()
    {
        if (FirebaseManager.IsReady && auth == null)
        { auth = FirebaseAuth.DefaultInstance; auth.StateChanged += AuthChanged; }
        if (authChanged) { authChanged = false; AdminAuthorization.Refresh(); }
        AdminAuthorization.Tick(); TrialRewards.Tick();
    }
}
