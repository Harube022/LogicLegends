using UnityEngine;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using Photon.Pun; // We need the Photon library!

// Notice this inherits from MonoBehaviourPun instead of MonoBehaviour
public class MultiplayerEquipmentLoader : MonoBehaviourPun 
{
    [System.Serializable]
    public class EquippableModel
    {
        public string itemID;      
        public GameObject model;   
    }

    [Header("Equipment Categories")]
    [SerializeField] private EquippableModel[] clothesModels;
    [SerializeField] private EquippableModel[] petModels;

    private void Start()
    {
        // CRITICAL: We only want YOUR local character to ask Firebase for YOUR clothes.
        if (photonView.IsMine)
        {
            if (FirebaseAuth.DefaultInstance != null && FirebaseAuth.DefaultInstance.CurrentUser != null)
            {
                LoadEquippedItemsFromCloud();
            }
        }
    }

    private void LoadEquippedItemsFromCloud()
    {
        string userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;
        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        dbRef.Child("users").Child(userId).Child("equipped").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted) return;

            DataSnapshot snapshot = task.Result;
            if (snapshot.Exists)
            {
                string equippedClothes = snapshot.HasChild("clothes") ? snapshot.Child("clothes").Value.ToString() : "";
                string equippedPet = snapshot.HasChild("pets") ? snapshot.Child("pets").Value.ToString() : "";

                // 1. Dress our local character on our own screen
                ApplyEquipment(equippedClothes, clothesModels);
                ApplyEquipment(equippedPet, petModels);

                // 2. Tell everyone else over the network what we are wearing!
                // "OthersBuffered" ensures that even players who join the game 5 minutes late will still receive this message and see your clothes!
                photonView.RPC("SyncEquipmentRPC", RpcTarget.OthersBuffered, equippedClothes, equippedPet);
            }
        });
    }

    // The [PunRPC] tag tells Photon that this method can be triggered across the internet
    [PunRPC]
    private void SyncEquipmentRPC(string clothesID, string petID)
    {
        // This code runs on EVERY OTHER player's computer, dressing YOUR character on THEIR screen!
        ApplyEquipment(clothesID, clothesModels);
        ApplyEquipment(petID, petModels);
    }

    private void ApplyEquipment(string equippedID, EquippableModel[] models)
    {
        foreach (EquippableModel entry in models)
        {
            if (entry.model != null)
            {
                entry.model.SetActive(entry.itemID == equippedID);
            }
        }
    }
}