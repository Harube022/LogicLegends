using System.Collections;
using TMPro;
using UnityEngine;

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
    public bool IsRevealed { get; private set; }

    public void BeginLoad(string loadingMessage)
    {
        StopAllCoroutines();
        feedbackTimer = null;
        loading = true;
        IsRevealed = false;
        loadingShownAt = -1;
        HideFeedback();
        SetContent(0, false);
        StartCoroutine(ShowLoading(++version, loadingMessage));
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
        foreach (var group in contentGroups)
        {
            if (group == null) continue;
            group.alpha = alpha;
            group.interactable = group.blocksRaycasts = interactive;
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
