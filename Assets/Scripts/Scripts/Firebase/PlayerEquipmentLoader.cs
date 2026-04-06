using UnityEngine;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;

public class PlayerEquipmentLoader : MonoBehaviour
{
    [System.Serializable]
    public class EquippableModel
    {
        public string itemID;      // e.g., "clothes_shirt"
        public GameObject model;   // The 3D mesh for this item
    }

    [Header("Equipment Categories")]
    [SerializeField] private EquippableModel[] clothesModels;
    [SerializeField] private EquippableModel[] petModels;

    // ---> NEW: Fallback Outfit <---
    [Header("Default Settings")]
    [Tooltip("If the player's database is empty, what should they wear?")]
    [SerializeField] private string defaultClothesID = "male_default";

    private void Start()
    {
        // 1. Double check that we are actually logged in
        if (FirebaseAuth.DefaultInstance != null && FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            // 2. Fetch the outfit from the cloud!
            LoadEquippedItemsFromCloud();
        }
    }

    private void LoadEquippedItemsFromCloud()
    {
        string userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;
        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        Debug.Log("Fetching equipped items from Firebase...");

        dbRef.Child("users").Child(userId).Child("equipped").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("Failed to load equipment data.");
                return;
            }

            // ---> THE FIX: Start with the default, and only overwrite it if Firebase has data!
            string equippedClothes = defaultClothesID; 
            string equippedPet = "";

            DataSnapshot snapshot = task.Result;
            if (snapshot.Exists)
            {
                if (snapshot.HasChild("clothes") && !string.IsNullOrEmpty(snapshot.Child("clothes").Value.ToString()))
                {
                    equippedClothes = snapshot.Child("clothes").Value.ToString();
                }
                
                if (snapshot.HasChild("pets") && !string.IsNullOrEmpty(snapshot.Child("pets").Value.ToString()))
                {
                    equippedPet = snapshot.Child("pets").Value.ToString();
                }
            }
                // Send them to the dressing function
                ApplyEquipment(equippedClothes, clothesModels);
                ApplyEquipment(equippedPet, petModels);
            
        });
    }

    private void ApplyEquipment(string equippedID, EquippableModel[] models)
    {
        foreach (EquippableModel entry in models)
        {
            if (entry.model != null)
            {
                // This single line does the magic: 
                // If the IDs match, it sets it to true (ON). If they don't, it sets it to false (OFF).
                entry.model.SetActive(entry.itemID == equippedID);
            }
        }
    }
}