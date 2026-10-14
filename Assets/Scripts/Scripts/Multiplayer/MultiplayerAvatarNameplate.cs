using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows a Photon player's chosen username above the multiplayer avatar.</summary>
[RequireComponent(typeof(PhotonView))]
public sealed class MultiplayerAvatarNameplate : MonoBehaviourPun
{
    private Canvas canvas;
    private TMP_Text label;
    private string shownName;

    private void Awake()
    {
        var anchor = new GameObject("Multiplayer Nameplate", typeof(RectTransform), typeof(Canvas));
        anchor.transform.SetParent(transform, false);
        var controller = GetComponent<CharacterController>();
        float top = controller != null ? controller.center.y + controller.height * .5f : 4.5f;
        anchor.transform.localPosition = Vector3.up * (top + .45f);
        anchor.transform.localScale = Vector3.one * .006f;
        var rect = (RectTransform)anchor.transform;
        rect.sizeDelta = new Vector2(320, 64);
        canvas = anchor.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        var textObject = new GameObject("Username", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(anchor.transform, false);
        var textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        label = textObject.GetComponent<TMP_Text>();
        label.font = StageUITheme.ReadingFont;
        label.fontSize = 36;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = 25;
        label.fontSizeMax = 36;
        label.color = StageUITheme.Ink;
        label.outlineColor = new Color(.07f, .04f, .025f, 1f);
        label.outlineWidth = .18f;
        label.richText = false;
        label.raycastTarget = false;
    }

    private void LateUpdate()
    {
        bool visible = PhotonNetwork.InRoom && photonView.Owner != null;
        if (canvas.enabled != visible) canvas.enabled = visible;
        if (!visible) return;
        string currentName = LobbyRules.DisplayName(photonView.Owner);
        if (shownName != currentName)
        {
            shownName = currentName;
            label.text = currentName;
        }
        var camera = Camera.main;
        if (camera != null) canvas.transform.rotation = camera.transform.rotation;
    }
}
