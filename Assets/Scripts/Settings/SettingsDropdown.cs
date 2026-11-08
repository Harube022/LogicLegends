using TMPro;
using UnityEngine;

/// <summary>Keeps the native dropdown and its outside-tap blocker above the paused settings canvas.</summary>
public sealed class SettingsDropdown : TMP_Dropdown
{
    protected override GameObject CreateDropdownList(GameObject template)
    {
        var list = base.CreateDropdownList(template);
        int order = 30000;
        foreach (var parent in GetComponentsInParent<Canvas>()) order = Mathf.Max(order, parent.sortingOrder);
        var popupCanvas = list.GetComponent<Canvas>();
        if (popupCanvas != null) popupCanvas.sortingOrder = order + 2;
        return list;
    }
}
