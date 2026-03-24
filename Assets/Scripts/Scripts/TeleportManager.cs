using UnityEngine;
using System.Collections;

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
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); 
        }
    }

    public void StartTeleport(GameObject player, Transform destination)
    {
        if (!isTeleporting)
        {
            StartCoroutine(TeleportSequence(player, destination));
        }
    }

    private IEnumerator TeleportSequence(GameObject player, Transform destination)
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

        // 2. Move Player
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.position = destination.position;
        player.transform.rotation = destination.rotation;

        if (cc != null) cc.enabled = true;

        // 3. Snap Camera Instantly (Using our new method!)
        if (cameraScript != null)
        {
            cameraScript.WarpCamera(destination);
        }

        yield return new WaitForSeconds(0.1f);

        // 4. Fade to Clear
        if (fadeCanvasGroup != null)
        {
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

        isTeleporting = false;
    }
}