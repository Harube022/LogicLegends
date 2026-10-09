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

    public static PrelimChallengeCompletionNotice GetOrCreate(RectTransform host, TMP_Text style)
    {
        if (host == null) return null;
        PrelimChallengeCompletionNotice notice = host.GetComponent<PrelimChallengeCompletionNotice>();
        if (notice == null) notice = host.gameObject.AddComponent<PrelimChallengeCompletionNotice>();
        notice.BuildIfNeeded(host, style);
        return notice;
    }

    public void ShowWaiting(int completedChallenge, int nextChallenge)
    {
        Show(string.Format(
            "Challenge {0} completed! Interact with the book statue to start Challenge {1}.",
            completedChallenge, nextChallenge));
    }

    public void ShowFinal()
    {
        Show("All challenges completed!");
        finalMessageRoutine = StartCoroutine(HideFinalAfterDelay());
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

    private IEnumerator HideFinalAfterDelay()
    {
        yield return new WaitForSecondsRealtime(3f);
        finalMessageRoutine = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    private void BuildIfNeeded(RectTransform host, TMP_Text style)
    {
        if (panel != null) return;
        GameObject background = new GameObject("PrelimChallengeCompletionNotice",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel = (RectTransform)background.transform;
        panel.SetParent(host, false);
        // The minimap and timer occupy the upper-right corner; controls occupy the bottom.
        panel.anchorMin = new Vector2(0.10f, 0.62f);
        panel.anchorMax = new Vector2(0.70f, 0.82f);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        Image image = background.GetComponent<Image>();
        image.color = new Color(0.025f, 0.11f, 0.16f, 0.92f);
        image.raycastTarget = false;

        GameObject label = new GameObject("Message", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        message = label.GetComponent<TextMeshProUGUI>();
        message.transform.SetParent(panel, false);
        RectTransform labelRect = message.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 8f);
        labelRect.offsetMax = new Vector2(-18f, -8f);
        if (style != null) message.font = style.font;
        message.fontSize = 42f;
        message.enableAutoSizing = true;
        message.fontSizeMin = 25f;
        message.fontSizeMax = 42f;
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
