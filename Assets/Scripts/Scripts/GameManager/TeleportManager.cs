using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using System; // Required to use Actions (Callbacks)
using Photon.Pun;

public class TeleportManager : MonoBehaviourPun
{
    public static TeleportManager Instance { get; private set; }

    [Header("Fade Settings")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Camera Settings")]
    [Tooltip("Drag the object with your ThirdPersonCameraController script here.")]
    [SerializeField] private ThirdPersonCameraController cameraScript;

    private bool isTeleporting = false;
    [SerializeField, Min(0f)] private float blackHoldDuration = 0.15f;
    private PrelimTeleportTransition prelimTransition;
    public static bool UsesCoveredTransitions =>
        SceneManager.GetActiveScene().name == "PRELIM" || SceneManager.GetActiveScene().name == "RulesOfInference";
    public bool IsTeleporting => UsesCoveredTransitions && prelimTransition != null ? prelimTransition.IsTeleporting : isTeleporting;
    public bool IsTransitioning => UsesCoveredTransitions && prelimTransition != null ? prelimTransition.IsTransitioning : isTeleporting;
    public float FadeAlpha => UsesCoveredTransitions && prelimTransition != null ? prelimTransition.FadeAlpha :
        (fadeCanvasGroup != null ? fadeCanvasGroup.alpha : 0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterPrelimScenes()
    {
        SceneManager.sceneLoaded -= EnsurePrelimManager;
        SceneManager.sceneLoaded += EnsurePrelimManager;
    }

    private static void EnsurePrelimManager(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "PRELIM" || scene.name == "RulesOfInference") EnsureExists();
    }

    public static TeleportManager EnsureExists()
    {
        if (Instance != null) return Instance;
        var existing = UnityEngine.Object.FindFirstObjectByType<TeleportManager>();
        return existing != null ? existing : new GameObject("TeleportManager").AddComponent<TeleportManager>();
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
        if (Instance == this && UsesCoveredTransitions)
        {
            prelimTransition = gameObject.AddComponent<PrelimTeleportTransition>();
            prelimTransition.Initialize(fadeCanvasGroup, fadeDuration, blackHoldDuration, cameraScript);
        }
    }

    private void Start()
    {
        if (prelimTransition != null) { prelimTransition.RevealInitialSpawn(); return; }
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f; // Start completely black
            StartCoroutine(FadeInAtStart());
        }
    }

    private IEnumerator FadeInAtStart()
    {
        fadeCanvasGroup.blocksRaycasts = true;
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(1f - (elapsedTime / fadeDuration));
            yield return null;
        }
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    // ---> NEW: Call this from your "Next Stage" UI Button <---
    public void LoadSceneWithFade(string sceneName)
    {
        if (UsesCoveredTransitions && prelimTransition != null)
        {
            if (prelimTransition.IsTeleporting || string.IsNullOrEmpty(sceneName)) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("TeleportManager: scene is missing from the build scene list: " + sceneName);
                return;
            }
            if (PhotonNetwork.InRoom && photonView != null && photonView.ViewID != 0)
                photonView.RPC("RPC_NetworkFadeAndLoad", RpcTarget.All, sceneName);
            else
                prelimTransition.StartSceneTransition(sceneName, !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient);
            return;
        }
        if (!isTeleporting)
        {
           if (PhotonNetwork.InRoom && photonView != null)
            {
                // ---> NEW: Tell EVERYONE in the room to fade out <---
                photonView.RPC("RPC_NetworkFadeAndLoad", RpcTarget.All, sceneName);
            }
            else
            {
                // Solo offline fallback
                StartCoroutine(SceneLoadSequence(sceneName, true));
            }
        }
    }

    [PunRPC]
    private void RPC_NetworkFadeAndLoad(string sceneName)
    {
        if (UsesCoveredTransitions && prelimTransition != null)
        {
            prelimTransition.StartSceneTransition(sceneName, !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient);
            return;
        }
        // The master client gets permission to actually trigger the load.
        // Everyone else just runs the fade-to-black animation and waits to be pulled in!
        StartCoroutine(SceneLoadSequence(sceneName, PhotonNetwork.IsMasterClient));
    }

    private IEnumerator SceneLoadSequence(string sceneName, bool isAllowedToLoadScene)
    {
        isTeleporting = true;

        // 1. Fade to Black
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            float elapsedTime = 0f;
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        yield return new WaitForSeconds(0.2f);

        if (isAllowedToLoadScene)
        {
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LoadLevel(sceneName);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

    }

    // Added the Action parameter back here
    public void StartTeleport(GameObject player, Transform destination, Action onMidTeleport = null)
    {
        if (UsesCoveredTransitions && prelimTransition != null)
        {
            prelimTransition.StartTeleport(player, destination, onMidTeleport);
            return;
        }
        if (!isTeleporting)
        {
            StartCoroutine(TeleportSequence(player, destination, onMidTeleport));
        }
    }

    public void StartTeleport(GameObject player, Vector3 position, Quaternion rotation, Action onMidTeleport = null)
    {
        if (UsesCoveredTransitions && prelimTransition != null)
            prelimTransition.StartTeleport(player, position, rotation, onMidTeleport);
    }

    public IEnumerator TeleportAndWait(GameObject player, Transform destination, Action onMidTeleport = null)
    {
        if (UsesCoveredTransitions && prelimTransition != null)
            yield return prelimTransition.TeleportAndWait(player, destination, onMidTeleport);
    }

    private IEnumerator TeleportSequence(GameObject player, Transform destination, Action onMidTeleport)
    {
        isTeleporting = true;

        // --- 1. Fade to Black ---
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true; 
            float elapsedTime = 0f;
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
                yield return null; 
            }
            fadeCanvasGroup.alpha = 1f; 
        }

        // --- 2. EXECUTE THE ENVIRONMENT SWAP WHILE SCREEN IS BLACK ---
        onMidTeleport?.Invoke();

        // --- 3. Move Player (Pure Transform) ---
        Vector3 safePosition = destination.position + (Vector3.up * 0.2f);
        
        player.transform.position = safePosition;
        player.transform.rotation = destination.rotation;

        Physics.SyncTransforms();

        // Snap Camera Instantly
        try
        {
            if (cameraScript != null) cameraScript.WarpCamera(destination);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Camera Warp Failed: " + e.Message);
        }

        yield return new WaitForSeconds(0.1f);

        // --- 4. Fade to Clear ---
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = false; 

            float elapsedTime = 0f;
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Clamp01(1f - (elapsedTime / fadeDuration));
                yield return null;
            }
            fadeCanvasGroup.alpha = 0f; 
        }

        isTeleporting = false;
    }
}