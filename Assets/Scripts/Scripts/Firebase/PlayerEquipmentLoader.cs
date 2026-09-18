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
        else
        {
            // Editor Testing Fallback: Load default model so character is visible
            Debug.LogWarning("No active Firebase session detected. Applying default test equipment.");
            ApplyDefaultEquipment();
        }
    }

    private void LoadEquippedItemsFromCloud()
    {
        string userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;
        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        Debug.Log("Fetching equipped items from Firebase...");

        // dbRef.Child("users").Child(userId).Child("equipped").GetValueAsync().ContinueWithOnMainThread(task =>
        // {
        //     if (task.IsFaulted)
        //     {
        //         Debug.LogError("Failed to load equipment data.");
        //         return;
        //     }
        // Fetch the user node to inspect both equipped clothes and base_character
        dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Failed to load user data. Applying default fallback.");
                ApplyDefaultEquipment();
                return;
            }

            DataSnapshot snapshot = task.Result;

            // ---> THE FIX: Start with the default, and only overwrite it if Firebase has data!
            string equippedClothes = ""; 
            string equippedPet = "";

            if (snapshot.Exists)
            {
            //     if (snapshot.HasChild("clothes") && !string.IsNullOrEmpty(snapshot.Child("clothes").Value.ToString()))
            //     {
            //         equippedClothes = snapshot.Child("clothes").Value.ToString();
            //     }
                
            //     if (snapshot.HasChild("pets") && !string.IsNullOrEmpty(snapshot.Child("pets").Value.ToString()))
            //     {
            //         equippedPet = snapshot.Child("pets").Value.ToString();
            //     }
            // }
            // 1. Check if specific equipped clothing is saved
                // 1. Check nested path safely using Child() navigation
                if (snapshot.HasChild("equipped") && snapshot.Child("equipped").HasChild("clothes"))
                {
                    equippedClothes = snapshot.Child("equipped").Child("clothes").Value?.ToString();
                }

                // 2. Fallback to base_character if equipped clothes is missing or invalid for this prefab
                if (string.IsNullOrEmpty(equippedClothes) || !HasMatchingMesh(equippedClothes))
                {
                    if (snapshot.HasChild("base_character"))
                    {
                        string baseChar = snapshot.Child("base_character").Value?.ToString() ?? "";
                        if (baseChar.ToLower().Contains("female") && HasMatchingMesh("female_default"))
                        {
                            equippedClothes = "female_default";
                        }
                        else if (baseChar.ToLower().Contains("male") && HasMatchingMesh("male_default"))
                        {
                            equippedClothes = "male_default";
                        }
                    }
                }

                // 3. Ultimate fallback to defaultClothesID
                if (string.IsNullOrEmpty(equippedClothes) || !HasMatchingMesh(equippedClothes))
                {
                    equippedClothes = defaultClothesID;
                }

                // Check pet data safely
                if (snapshot.HasChild("equipped") && snapshot.Child("equipped").HasChild("pets"))
                {
                    equippedPet = snapshot.Child("equipped").Child("pets").Value?.ToString();
                }
            }
            else
            {
                equippedClothes = defaultClothesID;
            }
                // Send them to the dressing function
                ApplyEquipment(equippedClothes, clothesModels);
                ApplyEquipment(equippedPet, petModels);
            
        });
    }

    private bool HasMatchingMesh(string itemID)
    {
        if (clothesModels == null || string.IsNullOrEmpty(itemID)) return false;
        foreach (var entry in clothesModels)
        {
            if (entry != null && entry.itemID == itemID) return true;
        }
        return false;
    }

    private void ApplyDefaultEquipment()
    {
        ApplyEquipment(defaultClothesID, clothesModels);
        ApplyEquipment("", petModels);
    }

    private void ApplyEquipment(string equippedID, EquippableModel[] models)
    {
        // foreach (EquippableModel entry in models)
        // {
        //     if (entry.model != null)
        //     {
        //         // This single line does the magic: 
        //         // If the IDs match, it sets it to true (ON). If they don't, it sets it to false (OFF).
        //         entry.model.SetActive(entry.itemID == equippedID);
        //     }
        // }
        if (models == null || models.Length == 0) return;

        bool matchedAny = false;
        foreach (EquippableModel entry in models)
        {
            if (entry != null && entry.model != null)
            {
                bool matches = entry.itemID == equippedID;
                entry.model.SetActive(matches);
                if (matches) matchedAny = true;
            }
        }

        // Safety net: If no model matched, enable the first model so the character is visible
        if (!matchedAny && models[0].model != null)
        {
            Debug.LogWarning($"[PlayerEquipmentLoader] No mesh matched itemID '{equippedID}'. Enabling fallback mesh '{models[0].itemID}'.");
            models[0].model.SetActive(true);
        }
    }
}