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
        // Character models are stored inactive in the prefab. Show the configured
        // fallback immediately so the player is visible while Firebase loads (and
        // also during offline/editor play).
        ApplyEquipment(defaultClothesID, clothesModels);

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
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Failed to load equipment data. Keeping the default outfit.");
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
        if (models == null || models.Length == 0) return;

        GameObject selectedModel = null;

        foreach (EquippableModel entry in models)
        {
            if (entry != null && entry.model != null && entry.itemID == equippedID)
            {
                selectedModel = entry.model;
                break;
            }
        }

        // Old or mistyped database IDs must not make the whole character invisible.
        if (selectedModel == null)
        {
            foreach (EquippableModel entry in models)
            {
                if (entry != null && entry.model != null && entry.itemID == defaultClothesID)
                {
                    selectedModel = entry.model;
                    break;
                }
            }
        }

        if (selectedModel == null)
        {
            foreach (EquippableModel entry in models)
            {
                if (entry != null && entry.model != null)
                {
                    selectedModel = entry.model;
                    break;
                }
            }
        }

        foreach (EquippableModel entry in models)
        {
            if (entry != null && entry.model != null)
            {
                entry.model.SetActive(entry.model == selectedModel);
            }
        }

        PlayerVisibilityController visibility = GetComponent<PlayerVisibilityController>();
        if (visibility != null) visibility.RefreshRenderers();
    }
}
