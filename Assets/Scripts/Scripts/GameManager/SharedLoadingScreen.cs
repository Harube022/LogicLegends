using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reusable presentation only. The caller owns loading and scene lifetime.</summary>
[DisallowMultipleComponent]
public sealed class SharedLoadingScreen : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private RectTransform composition;
    [SerializeField] private RectTransform safeArea;
    [SerializeField] private RawImage background;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text tipLabel;
    [SerializeField] private TMP_Text headingLabel;
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button cancelButton;
    [SerializeField, Min(0.05f)] private float fadeSeconds = 0.2f;
    [SerializeField, Min(0f)] private float minimumSceneDisplaySeconds = 1.25f;
    [SerializeField, Min(0f)] private float completedSceneHoldSeconds = 0.5f;
    [SerializeField, Min(0.05f)] private float sceneFillSeconds = 0.4f;
    [SerializeField] private string[] tips = {
        "Read your objective before you begin.\nSmall clues can lead to big discoveries.",
        "Look at what you know before deciding what follows.",
        "A different approach can reveal a new path forward.",
        "Playing together? Share your ideas and listen to your team."
    };
    private bool indeterminate;
    private string status;
    private float shownAt;
    private int tipIndex;
    private Vector2 lastSize;
    private Rect lastSafeArea;
    private bool showingError;
    private bool smoothSceneProgress;
    private float displayedProgress;
    private System.Action retryAction, cancelAction;
    public bool IsVisible => canvas != null && canvas.enabled;
    public float Progress { get; private set; }

    private void Awake()
    {
        if (retryButton != null) retryButton.onClick.AddListener(Retry);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        HideImmediate();
    }

    public void Show(string message, float progress = -1f, bool aboveBlackFade = true)
    {
        canvas.sortingOrder = aboveBlackFade ? 32761 : 32759;
        canvas.enabled = true;
        visibility.alpha = 1f;
        visibility.blocksRaycasts = true;
        visibility.interactable = false;
        showingError = false;
        smoothSceneProgress = false;
        retryAction = cancelAction = null;
        if (errorPanel != null) errorPanel.SetActive(false);
        if (headingLabel != null) { headingLabel.text = "PREPARING YOUR ADVENTURE"; headingLabel.gameObject.SetActive(true); }
        statusLabel.gameObject.SetActive(true);
        progressFill.transform.parent.gameObject.SetActive(true);
        progressLabel.gameObject.SetActive(true);
        tipLabel.transform.parent.gameObject.SetActive(true);
        status = "Loading";
        statusLabel.text = "Loading...";
        tipIndex = 0;
        if (tips.Length > 0) tipLabel.text = tips[0];
        SetProgress(progress);
        Canvas.ForceUpdateCanvases();
        Fit();
        shownAt = Time.realtimeSinceStartup;
    }

    public void SetProgress(float value)
    {
        indeterminate = value < 0f;
        Progress = indeterminate ? -1f : Mathf.Clamp01(value);
        if (indeterminate) progressLabel.text = "";
        else if (!smoothSceneProgress) RenderProgress(Progress);
    }

    public void ShowSceneLoading()
    {
        Show("Loading", 0f);
        smoothSceneProgress = true;
    }

    private void RenderProgress(float value)
    {
        displayedProgress = value;
        progressFill.fillAmount = value;
        progressLabel.text = (value >= 1f ? 100 : Mathf.FloorToInt(value * 100f)) + "%";
    }

    public IEnumerator CompleteSceneLoading()
    {
        SetProgress(1f);
        while (IsVisible && displayedProgress < 1f) yield return null;
        // Render and hold the actual completed bar before covering the destination.
        Canvas.ForceUpdateCanvases();
        // Let one complete player/UI frame run before starting the hold. Unlike
        // WaitForEndOfFrame, this also advances while inspecting the Editor.
        yield return null;
        float until = Mathf.Max(shownAt + minimumSceneDisplaySeconds,
            Time.realtimeSinceStartup + completedSceneHoldSeconds);
        while (IsVisible && Time.realtimeSinceStartup < until) yield return null;
    }

    private void Update()
    {
        if (!IsVisible) return;
        Fit();
        if (showingError) return;
        float elapsed = Time.realtimeSinceStartup - shownAt;
        statusLabel.text = status + "...";
        // A breathing bar, without a fabricated numeric percentage, for unknown work.
        if (indeterminate) progressFill.fillAmount = 0.3f + 0.18f * Mathf.Sin(elapsed * 2f);
        else if (smoothSceneProgress)
            // An asset-loading stall must not consume the entire visible fill in one frame.
            RenderProgress(Mathf.MoveTowards(displayedProgress, Progress,
                Mathf.Min(Time.unscaledDeltaTime, 0.05f) / sceneFillSeconds));
        int next = tips.Length == 0 ? 0 : (int)(elapsed / 8f) % tips.Length;
        if (next != tipIndex) { tipIndex = next; tipLabel.text = tips[next]; }
    }

    private void Fit()
    {
        Rect area = Screen.safeArea;
        var size = ((RectTransform)canvas.transform).rect.size;
        if (area == lastSafeArea && size == lastSize) return;
        lastSafeArea = area;
        lastSize = size;
        // Device Simulator may expose device-pixel safe areas while Game View reports
        // its smaller rendering size. Normalize against the matching pixel space.
        float safeWidth = area.xMax > Screen.width + 1 ? Mathf.Max(Screen.width, Screen.currentResolution.width) : Screen.width;
        float safeHeight = area.yMax > Screen.height + 1 ? Mathf.Max(Screen.height, Screen.currentResolution.height) : Screen.height;
        safeArea.anchorMin = new Vector2(Mathf.Clamp01(area.xMin / Mathf.Max(1, safeWidth)), Mathf.Clamp01(area.yMin / Mathf.Max(1, safeHeight)));
        safeArea.anchorMax = new Vector2(Mathf.Clamp01(area.xMax / Mathf.Max(1, safeWidth)), Mathf.Clamp01(area.yMax / Mathf.Max(1, safeHeight)));
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        Canvas.ForceUpdateCanvases();
        var available = safeArea.rect.size;
        composition.localScale = Vector3.one * Mathf.Min(available.x / 1920f, available.y / 1080f);
        // CanvasBackgroundCover applies the same crop as Main Menu while keeping
        // this rectangle stretched exactly to the canvas.
    }

    public IEnumerator FadeOut()
    {
        float from = visibility.alpha;
        float elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            visibility.alpha = Mathf.Lerp(from, 0f, elapsed / fadeSeconds);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        HideImmediate();
    }

    public void HideImmediate()
    {
        retryAction = cancelAction = null;
        showingError = false;
        if (visibility != null) { visibility.alpha = 0f; visibility.blocksRaycasts = false; visibility.interactable = false; }
        if (canvas != null) canvas.enabled = false;
    }

    public void ShowConnectionError(System.Action retry, System.Action cancel)
    {
        Show("Loading");
        showingError = true;
        indeterminate = false;
        retryAction = retry;
        cancelAction = cancel;
        if (headingLabel != null) headingLabel.gameObject.SetActive(false);
        statusLabel.gameObject.SetActive(false);
        progressFill.transform.parent.gameObject.SetActive(false);
        progressLabel.gameObject.SetActive(false);
        tipLabel.transform.parent.gameObject.SetActive(false);
        if (errorPanel != null) errorPanel.SetActive(true);
        visibility.interactable = true;
        retryButton.interactable = cancelButton.interactable = true;
    }

    private void Retry()
    {
        var action = retryAction;
        if (!showingError || action == null) return;
        retryButton.interactable = cancelButton.interactable = false;
        Show("Loading");
        action();
    }

    private void Cancel()
    {
        var action = cancelAction;
        if (!showingError || action == null) return;
        showingError = false;
        retryButton.interactable = cancelButton.interactable = false;
        retryAction = cancelAction = null;
        action();
    }
}
