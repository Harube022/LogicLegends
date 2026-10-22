using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation for the existing lobby service's bounded recovery state.</summary>
public sealed class MultiplayerRecoveryUI : MonoBehaviour
{
    GameObject surface;
    TMP_Text remaining, status;
    MultiplayerLobbyService service;
    public bool IsVisible => surface != null && surface.activeSelf;
    public void Show(MultiplayerLobbyService owner)
    {
        service = owner;
        if (surface == null) Build();
        surface.SetActive(true); Update();
    }
    public void Hide() { if (surface != null) surface.SetActive(false); }
    void Update()
    {
        if (!IsVisible || service == null) return;
        remaining.text = Mathf.CeilToInt(service.RecoveryRemaining) + " seconds remaining";
        status.text = service.Status + "\nYour place is reserved while we restore your connection.";
    }
    void Build()
    {
        surface = new GameObject("Multiplayer_Reconnecting", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        surface.transform.SetParent(transform, false);
        var canvas = surface.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32765;
        var scaler = surface.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var shade = StageUITheme.Rect("Backdrop", surface.transform); StageUITheme.Stretch(shade, Vector2.zero, Vector2.one);
        shade.gameObject.AddComponent<Image>().color = new Color(.015f, .03f, .04f, .78f);
        var panel = StageUITheme.Rect("Recovery_Panel", surface.transform);
        panel.anchorMin = new Vector2(.18f, .25f); panel.anchorMax = new Vector2(.82f, .75f); panel.offsetMin = panel.offsetMax = Vector2.zero;
        var image = panel.gameObject.AddComponent<Image>(); image.sprite = StageUITheme.Paper; image.type = Image.Type.Sliced;
        var title = StageUITheme.Text("Heading", panel, "RECONNECTING…", 48, false);
        StageUITheme.Stretch(title.rectTransform, new Vector2(.06f, .73f), new Vector2(.94f, .93f)); title.alignment = TextAlignmentOptions.Center;
        title.color = StageUITheme.ReadingInk;
        status = StageUITheme.Text("Status", panel, "", 30, true);
        StageUITheme.Stretch(status.rectTransform, new Vector2(.08f, .37f), new Vector2(.92f, .72f)); status.alignment = TextAlignmentOptions.Center;
        status.enableAutoSizing = true; status.fontSizeMin = 22; status.fontSizeMax = 30;
        status.color = StageUITheme.ReadingInk;
        remaining = StageUITheme.Text("Remaining", panel, "", 26, true);
        StageUITheme.Stretch(remaining.rectTransform, new Vector2(.1f, .23f), new Vector2(.9f, .37f)); remaining.alignment = TextAlignmentOptions.Center;
        remaining.color = StageUITheme.ReadingInk;
        var leave = StageUITheme.Button("Leave_Match", panel, "LEAVE MATCH", () => service.LeaveLobby());
        var rect = (RectTransform)leave.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .13f); rect.sizeDelta = new Vector2(300, 88);
    }
}
