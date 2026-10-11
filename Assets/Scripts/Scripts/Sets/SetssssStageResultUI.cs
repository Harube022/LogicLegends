using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Final result for the Setssss scene's five-challenge controller.</summary>
public sealed class SetssssStageResultUI : MonoBehaviour
{
    private static SetssssStageResultUI current;
    private bool navigating;

    public static void Show()
    {
        if (current != null) return;
        var root = new GameObject("Setssss Stage Complete", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        current = root.AddComponent<SetssssStageResultUI>();
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform safeArea = StageUITheme.Rect("SafeArea", root.transform);
        StageUITheme.Stretch(safeArea, Vector2.zero, Vector2.one);
        safeArea.gameObject.AddComponent<Crystal.SafeArea>();
        RectTransform shade = StageUITheme.Rect("ModalShade", safeArea);
        StageUITheme.Stretch(shade, Vector2.zero, Vector2.one);
        shade.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.035f, 0.045f, 0.9f);
        RectTransform card = StageUITheme.Rect("FramedCard", shade);
        StageUITheme.Stretch(card, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));
        StageUITheme.Surface(card.gameObject.AddComponent<Image>());
        TMP_Text title = StageUITheme.Text("ResultTitle", card, "STAGE COMPLETE", 68);
        StageUITheme.Stretch(title.rectTransform, new Vector2(0.08f, 0.63f), new Vector2(0.92f, 0.83f));
        title.enableAutoSizing = true;
        title.fontSizeMin = 38f;
        TMP_Text body = StageUITheme.Text("ResultSummary", card,
            "All five Sets challenges completed!", 36, true);
        StageUITheme.Stretch(body.rectTransform, new Vector2(0.1f, 0.40f), new Vector2(0.9f, 0.60f));
        body.enableAutoSizing = true;
        body.fontSizeMin = 25f;
        Button menu = StageUITheme.Button("MainMenuButton", card, "GO TO MENU", current.ReturnToMenu);
        StageUITheme.Stretch((RectTransform)menu.transform,
            new Vector2(0.30f, 0.15f), new Vector2(0.70f, 0.31f));
        GameInput.Instance?.SetGameplayInputBlocked(true);
        if (Player.LocalInstance != null) Player.LocalInstance.ToggleControl(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        WorldStageAudio.PlayStageCompletion();
    }

    private void ReturnToMenu()
    {
        if (navigating) return;
        navigating = true;
        Time.timeScale = 1f;
        GameInput.Instance?.SetGameplayInputBlocked(false);
        if (Photon.Pun.PhotonNetwork.InRoom) Photon.Pun.PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene("Main Menu");
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }
}
