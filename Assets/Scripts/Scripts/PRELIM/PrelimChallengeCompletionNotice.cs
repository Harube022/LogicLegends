using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A single, safe-area notification shared by both PRELIM stages.</summary>
[DefaultExecutionOrder(1050)]
public sealed class PrelimChallengeCompletionNotice : MonoBehaviour
{
    private RectTransform panel;
    private TextMeshProUGUI message;
    private Coroutine finalMessageRoutine;
    private bool useHostBounds;
    private RectTransform followHud;
    private CanvasGroup visibility;
    private bool compactPresentation;
    private float hudTextMin=23, hudTextMax=26;
    public bool SuppressWaitingMessages { get; set; }
    private readonly Vector3[] hudCorners = new Vector3[4];

    // PRELIM's stage HUDs opt into this transparent dock.
    public void FollowHud(RectTransform hud)
    {
        if (hud == null || panel == null || followHud == hud) return;
        followHud = hud;
        panel.SetParent(hud.parent, false);
        panel.GetComponent<Image>().enabled = false;
        message.rectTransform.offsetMin = message.rectTransform.offsetMax = Vector2.zero;
        visibility = panel.GetComponent<CanvasGroup>();
        if (visibility == null) visibility = panel.gameObject.AddComponent<CanvasGroup>();
        visibility.blocksRaycasts = false; visibility.interactable = false;
        SetPresentation(compactPresentation);
    }
    public void SetHudTextSize(float minimum,float maximum)
    {
        if(hudTextMin==minimum&&hudTextMax==maximum)return;
        hudTextMin=minimum;hudTextMax=maximum;
        if(followHud!=null)SetPresentation(compactPresentation);
    }

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
        if(SuppressWaitingMessages){Hide();return;}
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
    public void ShowWaitingForBook(int completedChallenge)
    {
        if(SuppressWaitingMessages){Hide();return;}
        SetPresentation(false);
        Show("Challenge "+completedChallenge+" completed!\nInteract with the book statue to continue.");
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
        compactPresentation = compact;
        panel.anchorMin = useHostBounds ? Vector2.zero : compact ? new Vector2(0.35f, 0.42f) : new Vector2(0.15f, 0.37f);
        panel.anchorMax = useHostBounds ? Vector2.one : compact ? new Vector2(0.65f, 0.58f) : new Vector2(0.85f, 0.63f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        message.fontSizeMax = compact ? 72f : 44f;
        message.fontSizeMin = compact ? 38f : 24f;
        message.color = compact ? new Color(1f, 0.9f, 0.55f) : Color.white;
        if (followHud != null)
        {
            panel.pivot = new Vector2(.5f, .5f);
            panel.localScale = Vector3.one;
            message.fontSizeMin = compact ? 38f : hudTextMin; message.fontSizeMax = compact ? 72f : hudTextMax;
            message.fontSize = message.fontSizeMax;
            message.alignment = compact ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
            message.outlineColor = Color.black; message.outlineWidth = .25f;
            LayoutBelowHud();
        }
    }

    private void LateUpdate()
    {
        if (followHud == null || panel == null || !panel.gameObject.activeSelf) return;
        visibility.alpha = followHud.gameObject.activeInHierarchy ? 1f : 0f;
        LayoutBelowHud();
    }

    private void LayoutBelowHud()
    {
        if (followHud == null || compactPresentation) return;
        panel.anchorMin = panel.anchorMax = followHud.anchorMax;
        panel.pivot = Vector2.one; panel.localScale = followHud.localScale;
        float width = followHud.rect.width;
        panel.sizeDelta = new Vector2(width, message.GetPreferredValues(message.text, width, 0).y + 6f);
        followHud.GetWorldCorners(hudCorners);
        panel.position = hudCorners[3] - Vector3.up * (12f * followHud.lossyScale.y);
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
