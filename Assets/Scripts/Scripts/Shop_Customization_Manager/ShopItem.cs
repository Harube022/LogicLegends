using UnityEngine;
using UnityEngine.UI;

public class ShopItem : MonoBehaviour
{

    // This creates a dropdown in the Unity Inspector!
    public enum Currency { Coins, Gems }

    [Header("Item Details")]
    [SerializeField] private string itemID;      
    public string ItemID => itemID;      // e.g., "shirt_red" or "pet_dog"
    [SerializeField] private int price;          
    public int Price => price;          // How many coins it costs
    [SerializeField] private Currency currencyType;
    public Currency CurrencyType => currencyType;
    [SerializeField] private ShopManager shopManager; // Reference to the main manager

    [Header("UI References")]
    [SerializeField] private Image buttonImage;      // The Image component of the Buy button
    [SerializeField] private Button buyButton;        // The Button component itself
    [SerializeField] private Sprite availableSprite;  // Your green "Buy" sprite
    [SerializeField] private Sprite purchasedSprite;  // Your grey "Buyed" sprite

    // The ShopManager will call this to set the button's look
    public void UpdateUI(bool isOwned)
    {
        if (isOwned)
        {
            buttonImage.sprite = purchasedSprite;
            buyButton.interactable = false; // Prevent buying again
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