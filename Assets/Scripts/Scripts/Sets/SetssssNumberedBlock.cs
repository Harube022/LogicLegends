using TMPro;
using UnityEngine;

// The physical model is reusable; this value is the element represented by it.
[DisallowMultipleComponent]
public sealed class SetssssNumberedBlock : MonoBehaviour
{
    [SerializeField, Range(1, 99)] private int value = 1;
    [SerializeField] private TMP_Text numberLabel;

    public int Value => value;

    public void SetValue(int newValue)
    {
        value = Mathf.Clamp(newValue, 1, 99);
        RefreshLabel();
    }

    private void Awake()
    {
        RefreshLabel();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RefreshLabel();
    }
#endif

    private void RefreshLabel()
    {
        if (numberLabel == null) numberLabel = GetComponentInChildren<TMP_Text>(true);
        if (numberLabel == null) return;
        numberLabel.text = value.ToString();
        numberLabel.enableAutoSizing = true;
        numberLabel.fontSizeMin = 40f;
        numberLabel.fontSizeMax = 54f;
    }
}
