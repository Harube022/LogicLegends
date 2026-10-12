using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>The shared PRELIM result card, with navigation supplied by its owning stage.</summary>
public sealed class StageResultCard
{
    public RectTransform Root, Card;
    public TMP_Text Stage, Heading, Summary;
    public Button Primary, Menu, Extra;

    public static StageResultCard Build(Transform host, UnityAction menuAction)
    {
        var view = new StageResultCard();
        view.Root = StageUITheme.Rect("StageResultPanel", host);
        StageUITheme.Stretch(view.Root, Vector2.zero, Vector2.one);
        view.Root.gameObject.AddComponent<Image>().color = new Color(.01f,.035f,.045f,.88f);
        view.Card = StageUITheme.Rect("FramedCard", view.Root);
        StageUITheme.Stretch(view.Card, new Vector2(.14f,.12f), new Vector2(.86f,.88f));
        StageUITheme.Surface(view.Card.gameObject.AddComponent<Image>());
        view.Stage = StageUITheme.Text("ResultStage", view.Card, "", 27);
        StageUITheme.Stretch(view.Stage.rectTransform, new Vector2(.08f,.77f), new Vector2(.92f,.86f));
        view.Heading = StageUITheme.Text("ResultHeading", view.Card, "", 72);
        StageUITheme.Stretch(view.Heading.rectTransform, new Vector2(.08f,.57f), new Vector2(.92f,.76f));
        view.Heading.enableAutoSizing = true;
        view.Heading.fontSizeMin = 30; view.Heading.fontSizeMax = 72;
        view.Heading.alignment = TextAlignmentOptions.Center;
        view.Summary = StageUITheme.Text("ResultSummary", view.Card, "", 31, true);
        StageUITheme.Stretch(view.Summary.rectTransform, new Vector2(.1f,.34f), new Vector2(.9f,.56f));
        view.Primary = StageUITheme.Button("NextOrRetryButton", view.Card, "NEXT STAGE", null);
        view.Menu = StageUITheme.Button("MainMenuButton", view.Card, "MAIN MENU", menuAction);
        view.Extra = StageUITheme.Button("CheckpointRetryButton", view.Card, "TRY AGAIN", null);
        StageUITheme.Stretch((RectTransform)view.Primary.transform, new Vector2(.12f,.17f), new Vector2(.48f,.29f));
        StageUITheme.Stretch((RectTransform)view.Menu.transform, new Vector2(.52f,.17f), new Vector2(.88f,.29f));
        StageUITheme.Stretch((RectTransform)view.Extra.transform, new Vector2(.25f,.04f), new Vector2(.75f,.15f));
        view.Root.gameObject.SetActive(false);
        return view;
    }
}
