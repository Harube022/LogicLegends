using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class PasswordVisibilityToggle : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite passwordHiddenSprite;
    [SerializeField] private Sprite passwordShownSprite;
    [SerializeField] private bool passwordVisible;

    public bool IsPasswordVisible => passwordVisible;

    private void Awake()
    {
        ResolveReferences();
        SetPasswordVisible(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
        UpdateIcon();
    }
#endif

    public void Configure(
        TMP_InputField targetInputField,
        Image targetIconImage,
        Sprite hiddenSprite,
        Sprite shownSprite)
    {
        inputField = targetInputField;
        iconImage = targetIconImage;
        passwordHiddenSprite = hiddenSprite;
        passwordShownSprite = shownSprite;
        SetPasswordVisible(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            SetPasswordVisible(!passwordVisible);
    }

    public void SetPasswordVisible(bool visible)
    {
        ResolveReferences();
        passwordVisible = visible;

        if (inputField != null)
        {
            int anchor = inputField.selectionAnchorPosition;
            int focus = inputField.selectionFocusPosition;

            inputField.contentType = visible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;
            inputField.inputType = visible
                ? TMP_InputField.InputType.Standard
                : TMP_InputField.InputType.Password;
            inputField.ForceLabelUpdate();

            if (Application.isPlaying)
            {
                inputField.selectionAnchorPosition = Mathf.Clamp(anchor, 0, inputField.text.Length);
                inputField.selectionFocusPosition = Mathf.Clamp(focus, 0, inputField.text.Length);
            }
        }

        UpdateIcon();
    }

    private void ResolveReferences()
    {
        if (inputField == null)
            inputField = GetComponentInParent<TMP_InputField>();

        if (iconImage == null)
            iconImage = GetComponent<Image>();
    }

    private void UpdateIcon()
    {
        if (iconImage == null)
            return;

        iconImage.sprite = passwordVisible ? passwordShownSprite : passwordHiddenSprite;
        // iconImage.preserveAspect = true;
        iconImage.raycastTarget = true;
    }
}
