using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Each row removes that exact editable pair; preset links remain visible and locked.</summary>
[DefaultExecutionOrder(1050)]
public sealed class FunctionsRelationsConnectionsView : MonoBehaviour
{
    public RectTransform Panel { get; private set; }
    public readonly Dictionary<MappingPair, UnityEngine.UI.Button> RemoveButtons = new Dictionary<MappingPair, UnityEngine.UI.Button>();
    FunctionsRelationsStageManager stage;
    RectTransform content;
    TMP_Text range;
    string signature;

    public void Initialize(FunctionsRelationsStageManager owner, RectTransform rangePanel)
    {
        stage = owner;
        Panel = StageUITheme.Rect("Current Connections", rangePanel);
        StageUITheme.Stretch(Panel,new Vector2(.51f,.19f),new Vector2(.94f,.71f));
        var title = StageUITheme.Text("Connections Heading", Panel, "CONNECTIONS", 30);
        title.color = StageUITheme.Gold;
        StageUITheme.Stretch(title.rectTransform, new Vector2(.05f,.86f), new Vector2(.95f,.98f));
        range = StageUITheme.Text("Outputs Actually Used", Panel, "Range = { }", 26);
        range.color = StageUITheme.Ink; range.font = StageUITheme.ReadingFont;
        StageUITheme.Stretch(range.rectTransform, new Vector2(.05f,.73f), new Vector2(.95f,.86f));
        var scroll = StageUITheme.Rect("Connection List", Panel).gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        StageUITheme.Stretch((RectTransform)scroll.transform, new Vector2(.05f,.06f), new Vector2(.95f,.72f));
        var viewport = StageUITheme.Rect("Viewport", scroll.transform);
        StageUITheme.Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
        viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
        content = StageUITheme.Rect("Content", viewport);
        content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 8; layout.childControlWidth = true; layout.childForceExpandWidth = true;
        layout.childControlHeight = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32;
    }

    void LateUpdate()
    {
        if (stage == null || Panel == null || stage.Mapping == null) return;
        Panel.gameObject.SetActive(stage.Tutorial == null);
        var pairs = stage.Mapping.Pairs.OrderBy(p => p.domain).ThenBy(p => p.codomain).ToArray();
        string next = string.Join(";", pairs.Select(p => p.ToString() + stage.Mapping.IsLocked(p)));
        if (signature == next) return;
        signature = next;
        foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        RemoveButtons.Clear();
        range.text = "Used = { " + string.Join(", ", pairs.Select(p => p.codomain).Distinct().OrderBy(v => v)) + " }";
        foreach (var pair in pairs)
        {
            var row = StageUITheme.Rect("Connection " + pair, content);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 64;
            var text = StageUITheme.Text("Pair", row, pair.ToString(), 32);
            text.font = StageUITheme.ReadingFont; text.color = StageUITheme.Ink;
            StageUITheme.Stretch(text.rectTransform, new Vector2(0,.08f), new Vector2(.49f,.92f));
            if (stage.Mapping.IsLocked(pair))
            {
                var locked = StageUITheme.Text("Fixed", row, "Fixed", 25);
                locked.color = StageUITheme.Gold;
                StageUITheme.Stretch(locked.rectTransform, new Vector2(.52f,.08f), new Vector2(1,.92f));
            }
            else
            {
                MappingPair exact = pair;
                var button = StageUITheme.Button("Remove " + pair, row, "Remove", () => stage.RemoveConnection(exact));
                StageUITheme.Stretch((RectTransform)button.transform, new Vector2(.50f,0), Vector2.one);
                var caption = button.GetComponentInChildren<TMP_Text>(); caption.fontSizeMin = 25; caption.fontSizeMax = 29;
                RemoveButtons.Add(pair, button);
            }
        }
    }
}
