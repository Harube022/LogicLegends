using UnityEngine;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;

public class CustomizationManager : MonoBehaviour
{
    [Header("UI Items")]
    [SerializeField] private CustomizationItem[] allUIItems; 

    // --- NEW: Slots for the base bodies in the dressing room ---
    [Header("Base Mannequins")]
    [SerializeField] private GameObject maleMannequin;
    [SerializeField] private GameObject femaleMannequin;
    

    [System.Serializable]
    public class EquippableModel
    {
        public string itemID;      
        public GameObject model;   
    }

    [Header("3D Mannequin Models")]
    [SerializeField] private EquippableModel[] mannequinModels; 

    private DatabaseReference dbRef;
    private string userId;
    private string playerBaseCharacter = "";

    private List<string> ownedItems = new List<string>();
    private string equippedClothes = "";
    private string equippedPet = "";

    private void OnEnable()
    {
        dbRef = FirebaseDatabase.DefaultInstance.RootReference;
        if (FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;
            LoadCustomizationData();
        }
    }

    private void LoadCustomizationData()
    {
        dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted) return;
            DataSnapshot snapshot = task.Result;

            if (snapshot.Exists)
            {
                // NEW: Load their chosen character!
                if (snapshot.HasChild("base_character"))
                {
                    playerBaseCharacter = snapshot.Child("base_character").Value.ToString();
                }

                ownedItems.Clear();
                if (snapshot.HasChild("inventory"))
                {
                    foreach (DataSnapshot item in snapshot.Child("inventory").Children)
                    {
                        ownedItems.Add(item.Key);
                    }
                }

                if (snapshot.HasChild("equipped"))
                {
                    if (snapshot.Child("equipped").HasChild("clothes"))
                        equippedClothes = snapshot.Child("equipped").Child("clothes").Value.ToString();
                    
                    if (snapshot.Child("equipped").HasChild("pets"))
                        equippedPet = snapshot.Child("equipped").Child("pets").Value.ToString();
                }

                // --- NEW: Auto-equip default skins for brand new players! ---
                if (string.IsNullOrEmpty(equippedClothes))
                {
                    equippedClothes = (playerBaseCharacter == "Female_Character") ? "female_default" : "male_default";
                }

                UpdateUIAndMannequin();
            }
        });
    }

    public void EquipItem(CustomizationItem itemToEquip)
    {
        // Notice we are using the new capitalized Getters here!
        if (itemToEquip.Type == CustomizationItem.ItemType.Clothes)
            equippedClothes = itemToEquip.ItemID;
        else if (itemToEquip.Type == CustomizationItem.ItemType.Pets)
            equippedPet = itemToEquip.ItemID;

        dbRef.Child("users").Child(userId).Child("equipped").Child(itemToEquip.Type.ToString().ToLower()).SetValueAsync(itemToEquip.ItemID);

        UpdateUIAndMannequin();
    }

    private void UpdateUIAndMannequin()
    {
        foreach (CustomizationItem item in allUIItems)
        {
            // --- NEW: Swap the 3D Base Body! ---
            if (playerBaseCharacter == "Female_Character")
            {
                if (maleMannequin != null) maleMannequin.SetActive(false);
                if (femaleMannequin != null) femaleMannequin.SetActive(true);
            }
            else 
            {
                // Default to Male
                if (maleMannequin != null) maleMannequin.SetActive(true);
                if (femaleMannequin != null) femaleMannequin.SetActive(false);
            }

            // 1. FILTER CHECK
            bool isCorrectGender = true;
            if (item.Target == CustomizationItem.TargetCharacter.MaleOnly && playerBaseCharacter != "Male_Character") isCorrectGender = false;
            if (item.Target == CustomizationItem.TargetCharacter.FemaleOnly && playerBaseCharacter != "Female_Character") isCorrectGender = false;

            // 2. If the item is marked as Default, treat it as always owned
            bool isOwned = item.IsDefault || ownedItems.Contains(item.ItemID);

            // 3. FINAL DECISION: Hide it if wrong gender OR not owned
            if (!isCorrectGender || !isOwned)
            {
                item.gameObject.SetActive(false);
                continue;
            }

            // If we made it this far, they own it and it fits their character!
            item.gameObject.SetActive(true);
            bool isEquipped = (item.ItemID == equippedClothes || item.ItemID == equippedPet);
            item.UpdateUI(isOwned, isEquipped);
        }

        foreach (EquippableModel entry in mannequinModels)
        {
            if (entry.itemID == equippedClothes || entry.itemID == equippedPet)
            {
                entry.model.SetActive(true); 
            }
            else
            {
                entry.model.SetActive(false); 
            }
        }
    }
}