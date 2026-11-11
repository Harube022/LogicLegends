using UnityEngine;
using UnityEngine.UI;

public class ShopItem : MonoBehaviour
{

    // This creates a dropdown in the Unity Inspector!
    public enum Currency { Coins, Gems }
    public enum TargetCharacter { Any, MaleOnly, FemaleOnly }

    [Header("Item Details")]
    [SerializeField] private string itemID;      
    public string ItemID => itemID;      // e.g., "shirt_red" or "pet_dog"
    [SerializeField] private string displayName;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? itemID.Replace('_', ' ') : displayName;
    [SerializeField] private int price;          
    public int Price => SoloEconomy.Price(itemID) is SoloEconomy.CosmeticPrice quote ? (CurrencyType == Currency.Coins ? quote.coins : quote.gems) : price;
    [SerializeField] private Currency currencyType;
    private Currency selectedCurrency = Currency.Coins;
    public Currency CurrencyType => SoloEconomy.Price(itemID) != null ? selectedCurrency : currencyType;
    public bool CanBuyWith(Currency currency)
    {
        var quote = SoloEconomy.Price(itemID);
        return quote == null || (currency == Currency.Coins ? quote.coins : quote.gems) > 0;
    }
    public void SelectCurrency(Currency currency)
    {
        if (CanBuyWith(currency)) selectedCurrency = currency;
    }
    public string Rarity => SoloEconomy.Price(itemID)?.rarity ?? "";
    [SerializeField] private TargetCharacter targetCharacter; // NEW: Tell Unity who can wear this
    public TargetCharacter Target => targetCharacter;

    [SerializeField] private ShopManager shopManager; // Reference to the main manager

    [Header("UI References")]
    [SerializeField] private Image buttonImage;      // The Image component of the Buy button
    [SerializeField] private Button buyButton;        // The Button component itself
    [SerializeField] private Sprite availableSprite;  // Your green "Buy" sprite
    [SerializeField] private Sprite purchasedSprite;  // Your grey "Buyed" sprite
    [SerializeField] private Image coinPriceIcon;
    [SerializeField] private Image gemPriceIcon;

    // The ShopManager will call this to set the button's look
    public void UpdateUI(bool isOwned)
    {
        var label = transform.Find("ItemPrice")?.GetComponent<TMPro.TMP_Text>();
        var quote = SoloEconomy.Price(itemID);
        if (label != null && quote != null)
        {
            if (!CanBuyWith(selectedCurrency))
                selectedCurrency = quote.coins > 0 ? Currency.Coins : Currency.Gems;
            var lines = new System.Collections.Generic.List<string>(2);
            if (quote.coins > 0) lines.Add(quote.coins.ToString("N0") + " Coins");
            if (quote.gems > 0) lines.Add(quote.gems.ToString("N0") + " Gems");
            label.text = string.Join("\n", lines);
            label.alignment = TMPro.TextAlignmentOptions.MidlineLeft; label.enableAutoSizing = true; label.fontSizeMin = 22; label.fontSizeMax = 24;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            label.rectTransform.anchoredPosition = new Vector2(20,-67); label.rectTransform.sizeDelta = new Vector2(172,56);
            label.ForceMeshUpdate(true);
            PlacePriceIcon(coinPriceIcon, label, quote.coins > 0 ? 0 : -1);
            PlacePriceIcon(gemPriceIcon, label, quote.gems > 0 ? (quote.coins > 0 ? 1 : 0) : -1);
            ((RectTransform)buyButton.transform).anchoredPosition = new Vector2(0,-144);
            var oldIcon = transform.Find("Coin_Img"); if (oldIcon != null) oldIcon.gameObject.SetActive(false);
        }
        if (isOwned)
        {
            buttonImage.sprite = purchasedSprite;
            buyButton.interactable = true; // Owned clothes can still be previewed; the manager prevents buying twice.
            // Optional: If you have text on the button, you can change it to "OWNED" here
        }
        else
        {
            buttonImage.sprite = availableSprite;
            buyButton.interactable = true;
        }
    }

    private static void PlacePriceIcon(Image icon, TMPro.TMP_Text label, int line)
    {
        if (icon == null) return;
        if (line < 0) { icon.gameObject.SetActive(false); return; }
        icon.gameObject.SetActive(true);
        var rect = icon.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        float y = line == 0 ? 14 : -14;
        if (label.textInfo.lineCount > line)
        {
            var info = label.textInfo.lineInfo[line];
            y = (info.ascender + info.descender) * .5f;
        }
        rect.anchoredPosition = new Vector2(-104, y);
        rect.sizeDelta = new Vector2(24, 24);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }

    // Link this to your Buy button's OnClick() event in the Inspector!
    public void OnClickBuy()
    {
        shopManager.AttemptPurchase(this);
    }
}
