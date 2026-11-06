using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Hides unfinished menu data and presents scoped authentication-style feedback.</summary>
[DisallowMultipleComponent]
public sealed class MenuDataFeedback : MonoBehaviour
{
    [SerializeField] private CanvasGroup[] contentGroups;
    [SerializeField] private CanvasGroup feedbackGroup;
    [SerializeField] private TMP_Text message;
    [Header("Loading Transition")]
    [SerializeField, Min(0)] private float loadingFeedbackDelay = .2f;
    [SerializeField, Min(0)] private float minimumLoadingDisplay = .35f;
    [SerializeField, Min(0)] private float revealDuration = .22f;
    [Header("Feedback Timing")]
    [SerializeField, Min(.1f)] private float feedbackDuration = 2.5f;

    private int version;
    private bool loading;
    private float loadingShownAt = -1;
    private Coroutine feedbackTimer;
    private bool keepButtonsOpaque;
    private bool blockInputWhileLoading = true;
    private bool automaticLoadingFeedback = true;
    public bool IsRevealed { get; private set; }

    public void Configure(CanvasGroup[] groups, CanvasGroup feedback, TMP_Text label, float minimumLoadingSeconds = .35f,
        bool keepButtonsOpaque = false, bool blockInputWhileLoading = true, bool automaticLoadingFeedback = true)
    {
        contentGroups = groups;
        feedbackGroup = feedback;
        message = label;
        minimumLoadingDisplay = Mathf.Max(0f, minimumLoadingSeconds);
        this.keepButtonsOpaque = keepButtonsOpaque;
        this.blockInputWhileLoading = blockInputWhileLoading;
        this.automaticLoadingFeedback = automaticLoadingFeedback;
    }

    public void BeginLoad(string loadingMessage)
    {
        StopAllCoroutines();
        feedbackTimer = null;
        loading = true;
        IsRevealed = false;
        loadingShownAt = -1;
        HideFeedback();
        SetContent(blockInputWhileLoading ? 0f : 1f, false);
        ++version;
        if (automaticLoadingFeedback) StartCoroutine(ShowLoading(version, loadingMessage));
    }

    private IEnumerator ShowLoading(int request, string text)
    {
        yield return new WaitForSecondsRealtime(loadingFeedbackDelay);
        if (request != version || !loading) yield break;
        ShowMessage(text);
        loadingShownAt = Time.realtimeSinceStartup;
    }

    public void RevealWhenReady()
    {
        loading = false;
        if (minimumLoadingDisplay <= 0f) HideFeedback();
        if (!blockInputWhileLoading) SetContent(0f, true);
        StartCoroutine(Reveal(version));
    }

    private IEnumerator Reveal(int request)
    {
        // Give text, layout and render textures a frame before showing them.
        yield return null;
        if (loadingShownAt >= 0)
        {
            float remaining = minimumLoadingDisplay - (Time.realtimeSinceStartup - loadingShownAt);
            if (remaining > 0) yield return new WaitForSecondsRealtime(remaining);
        }
        if (request != version) yield break;
        HideFeedback();
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < revealDuration)
        {
            if (request != version) yield break;
            SetContent(Mathf.SmoothStep(0, 1, (Time.realtimeSinceStartup - start) / revealDuration), false);
            yield return null;
        }
        SetContent(1, true);
        IsRevealed = true;
    }

    public void FailLoad(string text)
    {
        ++version;
        StopAllCoroutines();
        feedbackTimer = null;
        loading = IsRevealed = false;
        SetContent(0, false);
        ShowMessage(text);
    }

    public void ShowMessage(string text)
    {
        if (feedbackTimer != null) StopCoroutine(feedbackTimer);
        feedbackTimer = null;
        message.text = text;
        feedbackGroup.alpha = string.IsNullOrEmpty(text) ? 0 : 1;
        feedbackGroup.interactable = feedbackGroup.blocksRaycasts = false;
    }

    public void ShowTransient(string text)
    {
        ShowMessage(text);
        feedbackTimer = StartCoroutine(ClearFeedback());
    }

    private IEnumerator ClearFeedback()
    {
        yield return new WaitForSecondsRealtime(feedbackDuration);
        feedbackTimer = null;
        ShowMessage(string.Empty);
    }

    public void HideFeedback() => ShowMessage(string.Empty);

    private void SetContent(float alpha, bool interactive)
    {
        interactive |= !blockInputWhileLoading;
        foreach (var group in contentGroups)
        {
            if (group == null) continue;
            group.alpha = alpha;
            group.interactable = group.blocksRaycasts = interactive;
            if (!keepButtonsOpaque) continue;
            foreach (var button in group.GetComponentsInChildren<Button>(true))
            {
                var visibility = button.GetComponent<CanvasGroup>();
                if (visibility == null) visibility = button.gameObject.AddComponent<CanvasGroup>();
                visibility.ignoreParentGroups = true;
                visibility.alpha = 1f;
                // Social bypasses opacity and input gating; other menus can
                // retain their existing loading input policy.
                visibility.interactable = visibility.blocksRaycasts = interactive;
                // A Button added under a disabled CanvasGroup can retain its
                // initial disabled tint even after its transition is set to None.
                if (button.transition == Selectable.Transition.None && button.targetGraphic != null)
                    button.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
            }
        }
    }

    private void OnDisable()
    {
        ++version;
        StopAllCoroutines();
        feedbackTimer = null;
        loading = IsRevealed = false;
        SetContent(0, false);
        HideFeedback();
    }
}
