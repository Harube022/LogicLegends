using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using TMPro;

public class ShopManager : MonoBehaviour
{
    [Header("Currency UI")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text gemsText;
    
    [Header("All Shop Items")]
    [SerializeField] private ShopItem[] allShopItems; // Drag all your item slots here

    private DatabaseReference dbRef;
    private string userId;
    
    private int currentCoins = 0;
    private int currentGems = 0;
    private List<string> ownedItems = new List<string>();

    private void Start()
    {
        // 1. Get database connection
        dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        // 2. Make sure someone is actually logged in!
        if (FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;
            LoadPlayerData();
        }
    }

    // --- FIREBASE LOADING ---
    private void LoadPlayerData()
    {
        dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted) return;

            DataSnapshot snapshot = task.Result;

            if (snapshot.Exists)
            {
                // Load Coins
                if (snapshot.HasChild("coins"))
                {
                    currentCoins = int.Parse(snapshot.Child("coins").Value.ToString());
                }
                else 
                {
                    currentCoins = 500; // Retroactive coin grant
                    SaveCurrencyToDatabase();
                }

                // Load Gems
                if (snapshot.HasChild("gems"))
                {
                    currentGems = int.Parse(snapshot.Child("gems").Value.ToString());
                }
                else
                {
                    currentGems = 50; // Retroactive gem grant for older accounts!
                    SaveCurrencyToDatabase();
                }

                // Load Inventory
                ownedItems.Clear();
                if (snapshot.HasChild("inventory"))
                {
                    foreach (DataSnapshot item in snapshot.Child("inventory").Children)
                    {
                        ownedItems.Add(item.Key); 
                    }
                }
            }
            else
            {
                // First time playing! Give them some starter coins!
                currentCoins = 500; 
                currentGems = 50;
                SaveCurrencyToDatabase();
            }

            UpdateShopUI();
        });
    }

    // --- PURCHASE LOGIC ---
    public void AttemptPurchase(ShopItem itemToBuy)
    {

        bool canAfford = false;

        // 1. Check which currency the item costs, and see if they have enough
        if (itemToBuy.currencyType == ShopItem.Currency.Coins && currentCoins >= itemToBuy.price)
        {
            currentCoins -= itemToBuy.price;
            canAfford = true;
        }
        else if (itemToBuy.currencyType == ShopItem.Currency.Gems && currentGems >= itemToBuy.price)
        {
            currentGems -= itemToBuy.price;
            canAfford = true;
        }

        if (canAfford)
        {
            ownedItems.Add(itemToBuy.itemID);

            SaveCurrencyToDatabase(); // Saves both coins and gems
            dbRef.Child("users").Child(userId).Child("inventory").Child(itemToBuy.itemID).SetValueAsync(true);

            UpdateShopUI();
            Debug.Log($"Successfully purchased {itemToBuy.itemID}!");
        }
        else
        {
            Debug.LogWarning($"Not enough {itemToBuy.currencyType}!");
        }
    }

    // --- UI & SAVING ---
    private void UpdateShopUI()
    {
        coinsText.text = currentCoins.ToString();
        gemsText.text = currentGems.ToString();

        // Tell every item in the shop to update its button sprite
        foreach (ShopItem item in allShopItems)
        {
            bool isOwned = ownedItems.Contains(item.itemID);
            item.UpdateUI(isOwned);
        }
    }

    private void SaveCurrencyToDatabase()
    {
        // Save both to Firebase at the same time
        dbRef.Child("users").Child(userId).Child("coins").SetValueAsync(currentCoins);
        dbRef.Child("users").Child(userId).Child("gems").SetValueAsync(currentGems);
    }
}