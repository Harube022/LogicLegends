using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A single, safe-area notification shared by both PRELIM stages.</summary>
public sealed class PrelimChallengeCompletionNotice : MonoBehaviour
{
    private RectTransform panel;
    private TextMeshProUGUI message;
    private Coroutine finalMessageRoutine;
    private bool useHostBounds;

    public static PrelimChallengeCompletionNotice GetOrCreate(RectTransform host, TMP_Text style, TextMeshProUGUI existingMessage = null)
    {
        if (host == null) return null;
        PrelimChallengeCompletionNotice notice = host.GetComponent<PrelimChallengeCompletionNotice>();
        if (notice == null) notice = host.gameObject.AddComponent<PrelimChallengeCompletionNotice>();
        notice.BuildIfNeeded(host, style, existingMessage);
        return notice;
    }

    public void ShowWaiting(int completedChallenge, int nextChallenge)
    {
        SetPresentation(false);
        Show(string.Format(
            "Challenge {0} completed! Interact with the book statue to start Challenge {1}.",
            completedChallenge, nextChallenge));
    }

    public void ShowFinal()
    {
        SetPresentation(false);
        Show("All challenges completed!");
        finalMessageRoutine = StartCoroutine(HideFinalAfterDelay());
    }

    public void ShowStart()
    {
        SetPresentation(true);
        Show("Start!");
        finalMessageRoutine = StartCoroutine(HideFinalAfterDelay(1.5f));
    }

    public void UseHostBounds()
    {
        useHostBounds = true;
        SetPresentation(false);
    }

    public void ShowFeedback(string text, Color color, float seconds)
    {
        SetPresentation(false);
        if (message != null) message.color = color;
        Show(text);
        finalMessageRoutine = StartCoroutine(HideFinalAfterDelay(seconds));
    }

    public void Hide()
    {
        if (finalMessageRoutine != null) StopCoroutine(finalMessageRoutine);
        finalMessageRoutine = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    private void Show(string text)
    {
        Hide();
        if (panel == null || message == null) return;
        message.text = text;
        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();
    }

    private IEnumerator HideFinalAfterDelay(float seconds = 3f)
    {
        yield return new WaitForSecondsRealtime(seconds);
        finalMessageRoutine = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    private void SetPresentation(bool compact)
    {
        if (panel == null || message == null) return;
        panel.anchorMin = useHostBounds ? Vector2.zero : compact ? new Vector2(0.35f, 0.42f) : new Vector2(0.15f, 0.37f);
        panel.anchorMax = useHostBounds ? Vector2.one : compact ? new Vector2(0.65f, 0.58f) : new Vector2(0.85f, 0.63f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        message.fontSizeMax = compact ? 72f : 44f;
        message.fontSizeMin = compact ? 38f : 24f;
        message.color = compact ? new Color(1f, 0.9f, 0.55f) : Color.white;
    }

    private void BuildIfNeeded(RectTransform host, TMP_Text style, TextMeshProUGUI existingMessage)
    {
        if (panel != null) return;
        GameObject background = new GameObject("PrelimChallengeCompletionNotice",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel = (RectTransform)background.transform;
        panel.SetParent(host, false);
        // Center the message inside the safe area, clear of the minimap and edge controls.
        panel.anchorMin = new Vector2(0.15f, 0.37f);
        panel.anchorMax = new Vector2(0.85f, 0.63f);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        Image image = background.GetComponent<Image>();
        image.color = new Color(0.025f, 0.11f, 0.16f, 0.92f);
        image.raycastTarget = false;

        if (existingMessage != null) message = existingMessage;
        else
        {
            GameObject label = new GameObject("Message", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            message = label.GetComponent<TextMeshProUGUI>();
        }
        message.transform.SetParent(panel, false);
        message.gameObject.SetActive(true);
        RectTransform labelRect = message.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(24f, 10f);
        labelRect.offsetMax = new Vector2(-24f, -10f);
        if (style != null) message.font = style.font;
        message.fontSize = 44f;
        message.enableAutoSizing = true;
        message.fontSizeMin = 24f;
        message.fontSizeMax = 44f;
        message.fontStyle = FontStyles.Bold;
        message.alignment = TextAlignmentOptions.Center;
        message.textWrappingMode = TextWrappingModes.Normal;
        message.color = Color.white;
        message.outlineColor = new Color32(0, 0, 0, 255);
        message.outlineWidth = 0.2f;
        message.raycastTarget = false;
        background.SetActive(false);
    }
}
