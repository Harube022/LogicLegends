using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared trial chrome. Does not change stage state or HUD docking.</summary>
public static class StagePresentation
{
    static GameObject artwork;
    public static Sprite Art(string name)
    {
        if (artwork == null) artwork = Resources.Load<GameObject>("StageUI/GraphHUDArtwork");
        return artwork != null ? artwork.transform.Find(name).GetComponent<Image>().sprite : null;
    }
    public static void Frame(Image image, bool large = false)
    {
        image.sprite = Art(large ? "GraphMapFrame" : "GraphStatusFrame");
        image.type = Image.Type.Sliced; image.color = Color.white;
        if (image.sprite != null) image.pixelsPerUnitMultiplier = Mathf.Min(image.sprite.rect.width, image.sprite.rect.height) * .01f;
        image.raycastTarget = false;
        var inset = image.transform.Find("CalmContentInset");
        if (inset != null) inset.gameObject.SetActive(false);
        var outline = image.GetComponent<Outline>(); if (outline != null) outline.enabled = false;
    }
    public static void Hud(RectTransform hud, TMP_Text title, TMP_Text clock, TMP_Text progress)
    {
        Frame(hud.GetComponent<Image>());
        var skin = hud.GetComponent<TrialHudStyle>(); if (skin == null) skin = hud.gameObject.AddComponent<TrialHudStyle>();
        skin.Title = title; skin.Clock = clock; skin.Progress = progress; skin.Apply();
    }
    public static void Help(Button button, RectTransform settings = null)
    {
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
        { text.font = StageUITheme.HeadingFont; text.fontSharedMaterial = StageUITheme.HeadingFont.material; }
        StageUITheme.SkinButton(button); button.image.pixelsPerUnitMultiplier = 4.5f;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) { label.text = "HELP"; label.font = StageUITheme.HeadingFont; label.fontSize = 30; label.fontSizeMin = 28; label.fontSizeMax = 30; }
        var dock = button.GetComponent<StageHelpDock>(); if (dock == null) dock = button.gameObject.AddComponent<StageHelpDock>();
        dock.Settings = settings; dock.Layout();
    }
    public static Transform ResultHost(Transform host)
    {
        var canvas = StageFullScreenPanels.RootCanvas(host);
        return canvas != null ? canvas.transform : host;
    }
    public static void AdoptResult(GameObject panel, TMP_Text summary, Button primary, Button menu)
    {
        if (panel == null || summary == null) return;
        panel.transform.SetParent(ResultHost(panel.transform), false);
        StageUITheme.Stretch((RectTransform)panel.transform, Vector2.zero, Vector2.one);
        var shade = panel.GetComponent<Image>(); if (shade == null) shade = panel.AddComponent<Image>();
        shade.sprite = null; shade.color = new Color(.01f,.025f,.035f,.88f); shade.raycastTarget = true;
        var card = StageUITheme.Rect("FramedCard", panel.transform); Frame(card.gameObject.AddComponent<Image>(), true);
        summary.transform.SetParent(card, false);
        if (primary != null) { primary.transform.SetParent(card, false); ResultAction(primary, "WoodButtonGreen"); }
        if (menu != null) { menu.transform.SetParent(card, false); ResultAction(menu, "WoodButtonRed"); }
        var stage = StageUITheme.Text("ResultStage", card, TrialCatalog.ForScene(panel.scene.name, StageSelectionState.SelectedStage).Title, 28);
        var heading = StageUITheme.Text("ResultHeading", card, "STAGE COMPLETE", 64);
        var layout = panel.AddComponent<StageResultLayout>();
        layout.Card = card; layout.Stage = stage; layout.Heading = heading; layout.Summary = summary;
        layout.Primary = primary; layout.Menu = menu; layout.LegacySummary = true;
        ResultCanvas(panel); layout.Layout();
    }
    static void ResultAction(Button button, string art)
    {
        StageUITheme.SkinButton(button);
        var sprites = Resources.LoadAll<Sprite>("StageUI/" + art);
        if (sprites.Length > 0) button.image.sprite = sprites[0];
        button.image.pixelsPerUnitMultiplier = 4.5f;
    }
    public static void Completed(GameObject root, int trial, UnityEngine.Events.UnityAction next = null)
    {
        var view = root.GetComponent<StageResultLayout>();
        if (view != null) view.Complete(trial, next);
    }
    public static void NextTrial(int trial)
    {
        if (trial >= TrialCatalog.Count) return;
        var destination = TrialCatalog.Get(trial + 1);
        if (!Application.CanStreamedLevelBeLoaded(destination.Scene))
        { Debug.LogError("Next trial is missing from Build Settings: " + destination.Scene); return; }
        Time.timeScale = 1;
        GameInput.Instance?.SetGameplayInputBlocked(false);
        StageSelectionState.Select(destination.Number);
        if (TeleportManager.Instance != null) TeleportManager.Instance.LoadSceneWithFade(destination.Scene);
        else TeleportManager.Navigate(destination.Scene);
    }
    public static void ResultCanvas(GameObject root)
    {
        var canvas = root.GetComponent<Canvas>(); if (canvas == null) canvas = root.AddComponent<Canvas>();
        canvas.overrideSorting = true; canvas.sortingOrder = 900;
        if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();
    }
}
