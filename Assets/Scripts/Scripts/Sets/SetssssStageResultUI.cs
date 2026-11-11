using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Final result for the Setssss scene's five-challenge controller.</summary>
public sealed class SetssssStageResultUI : MonoBehaviour
{
    private static SetssssStageResultUI current;
    private bool navigating;
    public static void ShowFailure(UnityEngine.Events.UnityAction retry)
    {
        if (current != null) return;
        TrialTimeoutAudio.Play();
        var root = new GameObject("Sets Game Over", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        current = root.AddComponent<SetssssStageResultUI>();
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 950;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        var result = StageResultCard.Build(root.transform, current.ReturnToMenu);
        result.Stage.text = TrialCatalog.Get(7).Title; result.Heading.text = "TIME IS UP";
        result.Summary.text = "Try Again repeats this challenge.\nStart Over begins a fresh run.";
        result.Root.gameObject.SetActive(true);
        result.Root.GetComponent<StageResultLayout>().Fail(7, () => {
            retry?.Invoke();
            var old = current; current = null;
            if (old != null) Destroy(old.gameObject);
        });
    }

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

        var result = StageResultCard.Build(root.transform, current.ReturnToMenu);
        result.Stage.text = TrialCatalog.ForScene("Setssss").Title;
        result.Heading.text = "STAGE COMPLETE";
        result.Summary.text = "All five Sets challenges completed!";
        result.Primary.gameObject.SetActive(false); result.Extra.gameObject.SetActive(false);
        result.Root.gameObject.SetActive(true);
        result.Root.GetComponent<StageResultLayout>().Complete(7);
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
        TeleportManager.Navigate("Main Menu");
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }
}
