using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared presentation values from Propositional Logic's live UI.</summary>
public static class PrelimUIPresentation
{
    public const float CameraTransitionSeconds = .7f;

    public static void CompactHud(RectTransform hud, RectTransform safe, TMP_Text title, TMP_Text clock, TMP_Text progress)
    {
        hud.anchorMin = hud.anchorMax = hud.pivot = Vector2.one;
        hud.sizeDelta = new Vector2(500, 136); hud.anchoredPosition = new Vector2(-24, -24);
        hud.localScale = Vector3.one * Mathf.Min(1, (safe.rect.width - 48) / 500);
        title.font = clock.font = progress.font = StageUITheme.HeadingFont;
        title.color = clock.color = StageUITheme.Gold; progress.color = StageUITheme.Ink;
        title.enableAutoSizing = true; title.fontSizeMin = 22; title.fontSizeMax = 26;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        clock.enableAutoSizing = false; clock.fontSize = 40;
        progress.enableAutoSizing = true; progress.fontSizeMin = 22; progress.fontSizeMax = 24;
        StageUITheme.Stretch(title.rectTransform, new Vector2(.06f,.62f), new Vector2(.94f,.86f));
        StageUITheme.Stretch(clock.rectTransform, new Vector2(.06f,.18f), new Vector2(.31f,.57f));
        StageUITheme.Stretch(progress.rectTransform, new Vector2(.32f,.08f), new Vector2(.94f,.60f));
    }

    public static Button TutorialButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var r = StageUITheme.Rect(label + " Button", parent);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f,.5f);
        r.anchoredPosition = position; r.sizeDelta = new Vector2(250,92);
        r.gameObject.AddComponent<Image>().color = new Color(.12f,.48f,.43f);
        var button = r.gameObject.AddComponent<Button>(); button.onClick.AddListener(action);
        var text = StageUITheme.Text(label, r, label, 40, true);
        text.font = TMP_Settings.defaultFontAsset; text.color = Color.white;
        text.enableAutoSizing = true; text.fontSizeMin = 30; text.fontSizeMax = 40;
        StageUITheme.Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        text.rectTransform.offsetMin = Vector2.one * 8; text.rectTransform.offsetMax = -Vector2.one * 8;
        return button;
    }

    public static void Highlight(RectTransform target)
    {
        for (int i=0;i<4;i++)
        {
            var edge=StageUITheme.Rect("Highlight edge " + i,target);
            var image=edge.gameObject.AddComponent<Image>(); image.color=new Color(1,.86f,.22f,.95f); image.raycastTarget=false;
            if(i<2) { edge.anchorMin=new Vector2(0,i);edge.anchorMax=new Vector2(1,i);edge.sizeDelta=new Vector2(0,7); }
            else { edge.anchorMin=new Vector2(i-2,0);edge.anchorMax=new Vector2(i-2,1);edge.sizeDelta=new Vector2(7,0); }
            edge.anchoredPosition=Vector2.zero;
        }
    }
}
