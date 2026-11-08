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
    public void SelectCurrency(Currency currency) { selectedCurrency = currency; }
    public string Rarity => SoloEconomy.Price(itemID)?.rarity ?? "";
    [SerializeField] private TargetCharacter targetCharacter; // NEW: Tell Unity who can wear this
    public TargetCharacter Target => targetCharacter;

    [SerializeField] private ShopManager shopManager; // Reference to the main manager

    [Header("UI References")]
    [SerializeField] private Image buttonImage;      // The Image component of the Buy button
    [SerializeField] private Button buyButton;        // The Button component itself
    [SerializeField] private Sprite availableSprite;  // Your green "Buy" sprite
    [SerializeField] private Sprite purchasedSprite;  // Your grey "Buyed" sprite

    // The ShopManager will call this to set the button's look
    public void UpdateUI(bool isOwned)
    {
        var label = transform.Find("ItemPrice")?.GetComponent<TMPro.TMP_Text>();
        var quote = SoloEconomy.Price(itemID);
        if (label != null && quote != null)
        {
            label.text = quote.coins.ToString("N0") + " Coins\n" + quote.gems.ToString("N0") + " Gems";
            label.alignment = TMPro.TextAlignmentOptions.Center; label.enableAutoSizing = true; label.fontSizeMin = 22; label.fontSizeMax = 24;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            label.rectTransform.anchoredPosition = new Vector2(0,-67); label.rectTransform.sizeDelta = new Vector2(236,56);
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

    // Link this to your Buy button's OnClick() event in the Inspector!
    public void OnClickBuy()
    {
        shopManager.AttemptPurchase(this);
    }
}
