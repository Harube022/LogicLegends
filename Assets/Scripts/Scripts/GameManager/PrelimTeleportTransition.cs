using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Fade-first teleports used exclusively by PRELIM and RulesOfInference.</summary>
public sealed class PrelimTeleportTransition : MonoBehaviour
{
    public static PrelimTeleportTransition Instance { get; private set; }
    [Header("Fade Settings")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField, Min(0.15f)] private float fadeDuration = 0.5f;
    [SerializeField, Min(0f)] private float blackHoldDuration = 0.15f;

    [Header("Camera Settings")]
    [Tooltip("Optional legacy camera. Cinemachine targets are discovered at runtime.")]
    [SerializeField] private ThirdPersonCameraController cameraScript;

    public bool IsTeleporting => isTeleporting;
    public bool IsTransitioning => isTeleporting || startupFading;
    public float FadeAlpha => fadeCanvasGroup != null ? fadeCanvasGroup.alpha : 0f;

    private bool isTeleporting, startupFading;
    private Coroutine startupFade;
    private Player frozenPlayer;
    private GameInput frozenInput;
    private bool playerWasEnabled, inputWasBlocked;
    private readonly List<Behaviour> frozenCameraInputs = new List<Behaviour>();

    public void Initialize(CanvasGroup group, float duration, float hold, ThirdPersonCameraController legacyCamera)
    {
        Instance = this;
        fadeCanvasGroup = group;
        fadeDuration = duration;
        blackHoldDuration = hold;
        cameraScript = legacyCamera;
        EnsureFadeSurface();
        startupFading = true;
        fadeCanvasGroup.alpha = 1f;
        fadeCanvasGroup.blocksRaycasts = true;
    }

    private void EnsureFadeSurface()
    {
        var overlay = fadeCanvasGroup != null && fadeCanvasGroup.transform.parent != null &&
            fadeCanvasGroup.transform.parent.name == "TeleportFadeCanvas"
            ? fadeCanvasGroup.transform.parent.gameObject : null;
        if (overlay == null)
        {
            overlay = new GameObject("TeleportFadeCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlay.transform.SetParent(transform, false);
        }
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        if (fadeCanvasGroup == null)
        {
            var surface = new GameObject("FadeScreen", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            fadeCanvasGroup = surface.GetComponent<CanvasGroup>();
        }
        fadeCanvasGroup.transform.SetParent(overlay.transform, false);
        fadeCanvasGroup.gameObject.SetActive(true);
        fadeCanvasGroup.ignoreParentGroups = true;
        var rect = fadeCanvasGroup.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
        }
        var image = fadeCanvasGroup.GetComponent<Image>();
        if (image == null) image = fadeCanvasGroup.gameObject.AddComponent<Image>();
        image.color = Color.black; image.sprite = null; image.material = null;
        image.type = Image.Type.Simple; image.raycastTarget = true; image.enabled = true;
    }

    public void RevealInitialSpawn()
    {
        if (!isTeleporting) startupFade = StartCoroutine(FadeInWhenReady());
    }

    private IEnumerator FadeInWhenReady()
    {
        // Firebase spawning and the initial randomized room placement may finish later.
        while (Player.LocalInstance == null) yield return null;
        yield return null;
        yield return null;
        if (Player.LocalInstance != null) TeleportCameraSync.ResetAfterMove(Player.LocalInstance.transform, Vector3.zero);
        yield return FadeTo(0f);
        fadeCanvasGroup.blocksRaycasts = false;
        startupFading = false;
        startupFade = null;
    }

    private void BeginTransition(GameObject player)
    {
        if (startupFade != null) { StopCoroutine(startupFade); startupFade = null; }
        startupFading = false;
        isTeleporting = true;
        fadeCanvasGroup.blocksRaycasts = true;
        frozenInput = GameInput.Instance;
        inputWasBlocked = frozenInput != null && frozenInput.GameplayInputBlocked;
        frozenInput?.SetGameplayInputBlocked(true);
        frozenPlayer = player != null ? player.GetComponent<Player>() : Player.LocalInstance;
        playerWasEnabled = frozenPlayer != null && frozenPlayer.enabled;
        frozenPlayer?.ToggleControl(false);
        foreach (var input in UnityEngine.Object.FindObjectsByType<Unity.Cinemachine.CinemachineInputAxisController>(FindObjectsSortMode.None)) FreezeCameraInput(input);
        foreach (var input in UnityEngine.Object.FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None)) FreezeCameraInput(input);
        foreach (var input in UnityEngine.Object.FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None)) FreezeCameraInput(input);
        if (frozenPlayer != null)
            foreach (var input in frozenPlayer.GetComponentsInChildren<CameraTargetController>()) FreezeCameraInput(input);
        MobileLookInput.ResetDelta();
    }

    private void FreezeCameraInput(Behaviour input)
    {
        if (input.enabled) { frozenCameraInputs.Add(input); input.enabled = false; }
    }

    private void ReleaseControl()
    {
        if (frozenInput != null) frozenInput.SetGameplayInputBlocked(inputWasBlocked);
        if (frozenPlayer != null) frozenPlayer.ToggleControl(playerWasEnabled);
        foreach (var input in frozenCameraInputs) if (input != null) input.enabled = true;
        frozenCameraInputs.Clear(); frozenPlayer = null; frozenInput = null;
        MobileLookInput.ResetDelta();
    }

    private IEnumerator FadeTo(float target)
    {
        float from = fadeCanvasGroup.alpha;
        float duration = Mathf.Max(0.15f, fadeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            fadeCanvasGroup.alpha = Mathf.Lerp(from, target, elapsed / duration);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        fadeCanvasGroup.alpha = target;
        Canvas.ForceUpdateCanvases();
        // Render one completely opaque frame before changing the world/player.
        yield return null;
    }

    public void StartTeleport(GameObject player, Transform destination, Action onMidTeleport = null)
    {
        if (isTeleporting || player == null || destination == null) return;
        var view = player.GetComponent<PhotonView>();
        if (PhotonNetwork.InRoom && view != null && !view.IsMine) return;
        BeginTransition(player);
        StartCoroutine(TeleportSequence(player,
            () => destination != null ? (Pose?)new Pose(destination.position, destination.rotation) : null,
            onMidTeleport));
    }

    public void StartTeleport(GameObject player, Vector3 position, Quaternion rotation, Action onMidTeleport = null)
    {
        if (isTeleporting || player == null) return;
        var view = player.GetComponent<PhotonView>();
        if (PhotonNetwork.InRoom && view != null && !view.IsMine) return;
        BeginTransition(player);
        StartCoroutine(TeleportSequence(player, () => new Pose(position, rotation), onMidTeleport));
    }

    public IEnumerator TeleportAndWait(GameObject player, Transform destination, Action onMidTeleport = null)
    {
        while (isTeleporting) yield return null;
        StartTeleport(player, destination, onMidTeleport);
        while (isTeleporting) yield return null;
    }

    private IEnumerator TeleportSequence(GameObject player, Func<Pose?> destination, Action onMidTeleport)
    {
        try
        {
            yield return FadeTo(1f);
            if (player != null && destination().HasValue)
            {
                onMidTeleport?.Invoke();
                Pose? pose = destination();
                if (player != null && pose.HasValue)
                {
                    MovePlayerImmediately(player, pose.Value.position, pose.Value.rotation);
                    if (cameraScript != null) cameraScript.WarpCamera(player.transform);
                }
            }
            // Let physics and Cinemachine's normal LateUpdate settle while covered.
            yield return null;
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, blackHoldDuration));
            yield return FadeTo(0f);
        }
        finally
        {
            fadeCanvasGroup.alpha = 0f; fadeCanvasGroup.blocksRaycasts = false;
            ReleaseControl(); isTeleporting = false;
        }
    }

    public static void MovePlayerImmediately(GameObject player, Vector3 position, Quaternion rotation)
    {
        var controller = player.GetComponent<CharacterController>();
        bool controllerEnabled = controller != null && controller.enabled;
        if (controllerEnabled) controller.enabled = false;
        Vector3 delta = position - player.transform.position;
        player.transform.SetPositionAndRotation(position, rotation);
        var body = player.GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        player.GetComponent<Player>()?.ResetMotionAfterTeleport();
        Physics.SyncTransforms();
        TeleportCameraSync.ResetAfterMove(player.transform, delta);
        if (controllerEnabled) controller.enabled = true;
    }

    public void StartSceneTransition(string sceneName, bool allowedToLoad)
    {
        if (isTeleporting) return;
        BeginTransition(null);
        StartCoroutine(SceneLoadSequence(sceneName, allowedToLoad));
    }

    private IEnumerator SceneLoadSequence(string sceneName, bool allowedToLoad)
    {
        yield return FadeTo(1f);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, blackHoldDuration));
        if (!allowedToLoad) yield break; // Remain covered until the master synchronizes the scene.
        if (PhotonNetwork.InRoom) PhotonNetwork.LoadLevel(sceneName);
        else
        {
            var loading = SceneManager.LoadSceneAsync(sceneName);
            if (loading != null) yield return loading;
        }
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        ReleaseControl(); Instance = null;
    }
}
