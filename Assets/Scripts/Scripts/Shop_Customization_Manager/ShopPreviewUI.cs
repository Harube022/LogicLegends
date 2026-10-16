using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Local dressing-room preview. Only ShopManager's explicit confirmation writes a purchase.</summary>
public sealed class ShopPreviewUI : MonoBehaviour
{
    [SerializeField] private ShopManager shop;
    [SerializeField] private GameObject overlay;
    [SerializeField] private RectTransform card;
    [SerializeField] private RawImage characterImage;
    [SerializeField] private CharacterInspectView inspection;
    [SerializeField] private TMP_Text title, itemName, price, balance, message;
    [Header("Preview Presentation")]
    [SerializeField] private TMP_Text currencyUnit, afterPurchase, feedbackHeading;
    [SerializeField] private Image currencyIcon, feedbackAccent;
    [SerializeField] private Sprite coinSprite, gemSprite;
    private static readonly Color Neutral = new Color(.12f,.34f,.34f);
    private static readonly Color Positive = new Color(.15f,.36f,.22f);
    private static readonly Color Warning = new Color(.53f,.33f,.12f);
    private static readonly Color Error = new Color(.56f,.18f,.15f);
    [SerializeField] private Button primary, secondary;
    [SerializeField] private TMP_Text primaryLabel, secondaryLabel;
    [SerializeField] private GameObject maleMannequin, femaleMannequin;
    [SerializeField] private CustomizationManager.EquippableModel[] models;
    private readonly Dictionary<GameObject, bool> previousStates = new Dictionary<GameObject, bool>();
    private ShopItem item;
    private bool owned;
    private bool previewAvailable;
    private bool confirming;
    private bool refreshRequired;
    private int displayedBalance;
    private Coroutine reveal;
    public bool IsOpen => overlay != null && overlay.activeSelf;
    public bool IsConfirming => IsOpen && confirming;

    private void Awake()
    {
        primary.onClick.AddListener(Primary);
        secondary.onClick.AddListener(Secondary);
        overlay.SetActive(false);
    }

    private void OnDisable() => Close();
    private void OnDestroy() => Close();
    private void Update()
    {
        if (IsOpen && !shop.IsPreviewCurrent) Close();
    }
    private void OnRectTransformDimensionsChange() { if (IsOpen) FitCard(); }
    private void OnValidate() => ApplyPreviewZoom();
    private void ApplyPreviewZoom()
    {
        if (characterImage == null) return;
        // Camera fitting keeps the whole outfit visible; cropping the texture can cut off shoes.
        characterImage.uvRect = new Rect(0,0,1,1);
    }

    private void Remember(GameObject value)
    {
        if (value != null && !previousStates.ContainsKey(value)) previousStates.Add(value, value.activeSelf);
    }

    public void Show(ShopItem selected, string baseCharacter, int currentBalance, bool isOwned)
    {
        Close();
        item = selected;
        owned = isOwned;
        Remember(maleMannequin);
        Remember(femaleMannequin);
        var body = baseCharacter == "Female_Character" ? femaleMannequin : maleMannequin;
        foreach (var model in models) if (model != null) Remember(model.model);
        foreach (var model in models)
            if (model != null && model.model != null)
                model.model.SetActive(body != null && model.model.transform.IsChildOf(body.transform) && model.itemID == item.ItemID);
        previewAvailable = false;
        foreach (var model in models)
            if (model != null && model.model != null && model.itemID == item.ItemID && model.model.activeSelf &&
                body != null && model.model.transform.IsChildOf(body.transform)) previewAvailable = true;
        if (maleMannequin != null) maleMannequin.SetActive(previewAvailable && body == maleMannequin);
        if (femaleMannequin != null) femaleMannequin.SetActive(previewAvailable && body == femaleMannequin);
        characterImage.gameObject.SetActive(false);
        ApplyPreviewZoom();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        FitCard();
        if (inspection != null && previewAvailable)
            foreach (var model in models)
                if (model != null && model.model != null && model.model.activeSelf && model.itemID == item.ItemID &&
                    model.model.transform.IsChildOf(body.transform))
                { inspection.ShowCharacter(body.transform,model.model.transform); break; }
        itemName.text = item.DisplayName;
        price.text = currencyUnit == null ? "Price: " + item.Price.ToString("N0") + " " + item.CurrencyType : item.Price.ToString("N0");
        if (currencyUnit != null) currencyUnit.text = item.CurrencyType.ToString().ToUpperInvariant();
        if (currencyIcon != null) currencyIcon.sprite = item.CurrencyType == ShopItem.Currency.Coins ? coinSprite : gemSprite;
        ShowResult(previewAvailable ? (owned ? "You already own these clothes." : "Preview only. Your equipped clothes stay unchanged.") :
            "This outfit preview is unavailable. Please choose another item.", owned, currentBalance);
        if (!previewAvailable) SetFeedback("This outfit preview is unavailable. Please choose another item.", "PREVIEW UNAVAILABLE", Warning);
        if (previewAvailable) reveal = StartCoroutine(RevealCharacter());
    }

    private IEnumerator RevealCharacter()
    {
        // Let the shared camera render the chosen body/outfit before showing its texture.
        yield return new WaitForEndOfFrame();
        if (IsOpen) characterImage.gameObject.SetActive(true);
        reveal = null;
    }

    private void FitCard()
    {
        var area = overlay.transform as RectTransform;
        if (area == null) return;
        float scale = Mathf.Min(1f, (area.rect.width - 64f) / 1320f, (area.rect.height - 64f) / 820f);
        card.localScale = Vector3.one * Mathf.Max(.1f, scale);
    }

    public void ShowConfirmation(int currentBalance)
    {
        if (item == null || owned || !previewAvailable) return;
        displayedBalance = currentBalance;
        confirming = true;
        title.text = "CONFIRM PURCHASE";
        UpdateBalance(currentBalance, true);
        SetFeedback(currentBalance < item.Price ? "Not enough " + item.CurrencyType.ToString().ToLowerInvariant() + "." :
            "Buy " + item.DisplayName + " for " + item.Price.ToString("N0") + " " + item.CurrencyType + "?",
            currentBalance < item.Price ? "NOT ENOUGH CURRENCY" : "PLEASE CONFIRM", currentBalance < item.Price ? Warning : Neutral);
        primaryLabel.text = "CONFIRM";
        primary.interactable = currentBalance >= item.Price;
        secondaryLabel.text = "CANCEL";
        secondary.interactable = true;
    }

    public void ShowSaving()
    {
        primary.interactable = secondary.interactable = false;
        SetFeedback("", "PROCESSING", Warning);
    }

    public void ShowProgress()
    {
        if (IsOpen) SetFeedback("Saving your purchase...", "PROCESSING", Warning);
    }

    public void ShowError(string text)
    {
        confirming = false;
        refreshRequired = true;
        title.text = "PURCHASE ERROR";
        SetFeedback(text, "PLEASE TRY AGAIN", Error);
        primaryLabel.text = "RETRY";
        secondaryLabel.text = "CLOSE";
        primary.interactable = secondary.interactable = true;
    }

    public void ClearResultFeedback()
    {
        if (IsOpen && owned && !refreshRequired && !confirming)
            SetFeedback("You already own these clothes.", "OWNED", Positive);
    }

    public void ShowResult(string text, bool isOwned, int currentBalance, bool purchaseSucceeded = false)
    {
        displayedBalance = currentBalance;
        owned = isOwned;
        confirming = false;
        refreshRequired = false;
        title.text = "CLOTHES PREVIEW";
        UpdateBalance(currentBalance, false);
        SetFeedback(text, purchaseSucceeded ? "PURCHASE SUCCESSFUL" : owned ? "OWNED" : "TRY IT ON", owned ? Positive : Neutral);
        primaryLabel.text = owned ? "OWNED" : "BUY OUTFIT";
        primary.interactable = !owned && previewAvailable && currentBalance >= item.Price;
        if (!owned && previewAvailable && currentBalance < item.Price)
            SetFeedback("Not enough " + item.CurrencyType.ToString().ToLowerInvariant() + ".", "NOT ENOUGH CURRENCY", Warning);
        secondaryLabel.text = "CLOSE";
        secondary.interactable = true;
    }

    private void SetFeedback(string text, string heading, Color accent)
    {
        message.text = text;
        if (feedbackHeading != null) { feedbackHeading.text = heading; feedbackHeading.color = accent; }
        if (feedbackAccent != null) feedbackAccent.color = accent;
    }

    private void UpdateBalance(int currentBalance, bool showAfterPurchase)
    {
        balance.text = afterPurchase == null ? "Balance: " + currentBalance.ToString("N0") + " " + item.CurrencyType :
            currentBalance.ToString("N0") + " " + item.CurrencyType.ToString().ToUpperInvariant();
        string remaining = "After purchase: " + Mathf.Max(0,currentBalance-item.Price).ToString("N0") + " " + item.CurrencyType;
        if (afterPurchase != null)
        {
            afterPurchase.text = showAfterPurchase ? remaining : "";
            afterPurchase.gameObject.SetActive(showAfterPurchase);
        }
        else if (showAfterPurchase) balance.text += "\n" + remaining;
    }

    private void Primary()
    {
        if (refreshRequired) shop.RetryLoad();
        else if (confirming) shop.ConfirmPurchase();
        else shop.RequestPurchaseConfirmation();
    }

    private void Secondary()
    {
        if (shop.IsPurchasing) return;
        if (refreshRequired) shop.RetryLoad();
        else if (confirming)
        {
            ShowResult("Purchase cancelled. No currency was spent.", owned, displayedBalance);
            SetFeedback("Purchase cancelled. No currency was spent.", "CANCELLED", Neutral);
        }
        else Close();
    }

    public void Close()
    {
        if (reveal != null) { StopCoroutine(reveal); reveal = null; }
        if (inspection != null) inspection.Clear();
        if (overlay != null) overlay.SetActive(false);
        // Hide both bodies before restoring children so no outfit/gender can flash.
        if (previousStates.Count > 0)
        {
            if (maleMannequin != null) maleMannequin.SetActive(false);
            if (femaleMannequin != null) femaleMannequin.SetActive(false);
            foreach (var state in previousStates)
                if (state.Key != null && state.Key != maleMannequin && state.Key != femaleMannequin) state.Key.SetActive(state.Value);
            foreach (var body in new[] { maleMannequin, femaleMannequin })
                if (body != null && previousStates.TryGetValue(body, out bool active)) body.SetActive(active);
            previousStates.Clear();
        }
        confirming = false;
        refreshRequired = false;
        item = null;
    }
}
