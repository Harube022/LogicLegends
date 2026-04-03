using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using System; // Required to use Actions (Callbacks)
using Photon.Pun;

public class TeleportManager : MonoBehaviour
{
    public static TeleportManager Instance { get; private set; }

    [Header("Fade Settings")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Camera Settings")]
    [Tooltip("Drag the object with your ThirdPersonCameraController script here.")]
    [SerializeField] private ThirdPersonCameraController cameraScript;

    private bool isTeleporting = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject); 
    }

    private void Start()
    {
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
        if (!isTeleporting)
        {
            StartCoroutine(SceneLoadSequence(sceneName));
        }
    }

    private IEnumerator SceneLoadSequence(string sceneName)
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

        // 2. Load the new Scene (Multiplayer safe!)
        if (PhotonNetwork.InRoom)
        {
            // Only the Master Client should trigger the network load
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.LoadLevel(sceneName);
            }
        }
        else
        {
            // Fallback for solo testing
            SceneManager.LoadScene(sceneName);
        }

    }

    // Added the Action parameter back here
    public void StartTeleport(GameObject player, Transform destination, Action onMidTeleport = null)
    {
        if (!isTeleporting)
        {
            StartCoroutine(TeleportSequence(player, destination, onMidTeleport));
        }
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