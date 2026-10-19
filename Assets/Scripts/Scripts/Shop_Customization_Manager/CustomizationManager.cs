using UnityEngine;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class CustomizationManager : MonoBehaviour
{
    [Header("UI Items")]
    [SerializeField] private CustomizationItem[] allUIItems;
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
    [Header("Loading Feedback")]
    [SerializeField] private TMP_Text loadingMessage;
    [SerializeField] private UnityEngine.UI.Button retryButton;
    [Header("Loading Transition")]
    [SerializeField] private UnityEngine.UI.ScrollRect itemsScroll;
    [SerializeField] private CanvasGroup[] revealGroups = new CanvasGroup[0];
    [SerializeField] private CanvasGroup scrollbarLoadingGate;
    [SerializeField] private CharacterInspectView inspection;
    private const string LoadingText = "Loading your character and equipped skin...";
    private bool pendingLoadingFeedback;
    private float loadingMessageShownAt = -1f;
    private bool revealing;

    private DatabaseReference dbRef;
    private string userId;
    private string playerBaseCharacter = "";
    private readonly HashSet<string> ownedItems = new HashSet<string>();
    private string equippedClothes = "";
    private string equippedPet = "";
    private int requestVersion;
    private bool dataLoaded;
    private bool usingSandboxInventory;

    [Header("Equipment Save Feedback")]
    [SerializeField] private float saveFeedbackDelay = 0.2f;
    [SerializeField] private float savedFeedbackDuration = 1.5f;
    private int saveFeedbackVersion;
    private bool saving;

    private void OnEnable() { RetryLoad(); }
    private void OnDisable()
    {
        requestVersion++;
        StopAllCoroutines();
        dataLoaded = false;
        usingSandboxInventory = false;
        saving = false;
        revealing = false;
        HideContent();
        if (itemsScroll != null) itemsScroll.enabled = true;
    }

    private void HideContent()
    {
        SetReveal(0f, false);
        if (inspection != null) inspection.Clear();
        SetScrollbarReveal(false);
        if (itemsScroll != null)
        {
            itemsScroll.StopMovement();
            itemsScroll.enabled = false;
        }
        if (maleMannequin != null) maleMannequin.SetActive(false);
        if (femaleMannequin != null) femaleMannequin.SetActive(false);
        foreach (var entry in mannequinModels)
            if (entry != null && entry.model != null) entry.model.SetActive(false);
        foreach (var item in allUIItems)
            if (item != null) item.gameObject.SetActive(false);
    }

    private void Status(string message, bool retry)
    {
        pendingLoadingFeedback = false;
        loadingMessageShownAt = message == LoadingText ? Time.realtimeSinceStartup : -1f;
        if (loadingMessage != null)
        {
            loadingMessage.text = message;
            var group = loadingMessage.GetComponentInParent<CanvasGroup>();
            if (group != null) group.alpha = string.IsNullOrEmpty(message) ? 0f : 1f;
        }
        if (retryButton != null) retryButton.gameObject.SetActive(retry);
    }

    public void RetryLoad()
    {
        if (!isActiveAndEnabled || saving) return;
        StopAllCoroutines();
        int version = ++requestVersion;
        dataLoaded = false;
        usingSandboxInventory = false;
        revealing = false;
        playerBaseCharacter = equippedClothes = equippedPet = "";
        ownedItems.Clear();
        HideContent();
        Status("", false);
        pendingLoadingFeedback = true;
        StartCoroutine(DelayedLoadingFeedback(version));
        StartCoroutine(LoadWhenReady(version));
    }

    private IEnumerator LoadWhenReady(int version)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!FirebaseManager.IsReady && Time.realtimeSinceStartup < deadline &&
               string.IsNullOrEmpty(FirebaseManager.InitializationError)) yield return null;
        if (!FirebaseManager.IsReady)
        {
            Status("Couldn't connect. Please try again.", true);
            yield break;
        }
        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null)
        {
            Status("Please log in to load your character.", true);
            yield break;
        }
        userId = user.UserId;
        dbRef = FirebaseDatabase.DefaultInstance.RootReference;
        var request = dbRef.Child("users").Child(userId).GetValueAsync();
        request.ContinueWithOnMainThread(task =>
        {
            if (!IsCurrent(version)) return;
            if (task.IsFaulted || task.IsCanceled)
            {
                Status("Couldn't load your character. Check your connection and retry.", true);
                return;
            }
            var snapshot = task.Result;
            playerBaseCharacter = snapshot.Child("base_character").Value?.ToString() ?? "";
            if (playerBaseCharacter != "Male_Character" && playerBaseCharacter != "Female_Character")
            {
                Status("Choose your character before customizing it.", true);
                return;
            }
            // Real inventory and owned equipment remain usable even when the
            // payment host is disabled or unreachable.
            foreach (var child in snapshot.Child("inventory").Children)
                if (child.Value is bool owned && owned) ownedItems.Add(child.Key);
            equippedClothes = snapshot.Child("equipped/clothes").Value?.ToString();
            if (string.IsNullOrEmpty(equippedClothes)) equippedClothes = playerBaseCharacter == "Female_Character" ? "female_default" : "male_default";
            equippedPet = snapshot.Child("equipped/pets").Value?.ToString() ?? "";
            if (!HasClothesModel(equippedClothes)) { Status("Your equipped skin couldn't be loaded. Please retry.", true); return; }
            dataLoaded = true;
            revealing = true;
            UpdateUIAndMannequin();
            StartCoroutine(RevealLoadedContent(version));
            if (CommerceClient.Sandbox) LoadCommerceInventory(version);
        });
        deadline = Time.realtimeSinceStartup + 20f;
        while (!request.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        if (!request.IsCompleted && IsCurrent(version))
        {
            Status("Couldn't load your saved character. Check your connection and retry.", true);
            requestVersion++;
        }
    }

    private bool IsCurrent(int version)
    {
        return this != null && isActiveAndEnabled && version == requestVersion &&
            FirebaseManager.IsReady && FirebaseAuth.DefaultInstance.CurrentUser != null &&
            FirebaseAuth.DefaultInstance.CurrentUser.UserId == userId;
    }

    private async void LoadCommerceInventory(int version)
    {
        int equipmentVersion = saveFeedbackVersion;
        try
        {
            var account = await CommerceClient.LoadAccount(playerBaseCharacter);
            if (!IsCurrent(version) || saving || saveFeedbackVersion != equipmentVersion) return;
            usingSandboxInventory = true;
            ownedItems.Clear();
            if (account.inventory != null) foreach (var id in account.inventory) ownedItems.Add(id);
            equippedClothes = account.equippedClothes;
            equippedPet = account.equippedPet;
            if (!HasClothesModel(equippedClothes)) { Status("Your equipped skin couldn't be loaded. Please retry.", true); return; }
            UpdateUIAndMannequin();
        }
        catch (System.Exception) { /* Saved Firebase equipment is already visible. */ }
    }

    private IEnumerator DelayedLoadingFeedback(int version)
    {
        yield return new WaitForSecondsRealtime(0.2f);
        if (version == requestVersion && pendingLoadingFeedback && !dataLoaded)
            Status(LoadingText, false);
    }

    private void SetReveal(float alpha, bool interactive)
    {
        foreach (var group in revealGroups)
        {
            if (group == null) continue;
            group.alpha = alpha;
            group.interactable = interactive;
            group.blocksRaycasts = interactive;
        }
    }

    private void SettleScroll()
    {
        Canvas.ForceUpdateCanvases();
        if (itemsScroll == null || itemsScroll.content == null) return;
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(itemsScroll.content);
        Canvas.ForceUpdateCanvases();
        itemsScroll.StopMovement();
        itemsScroll.verticalNormalizedPosition = 1f;
    }

    private IEnumerator RevealLoadedContent(int version)
    {
        // The RawImage preview and grid fade only after the chosen skin and layout are ready.
        SettleScroll();
        yield return null;
        if (!IsCurrent(version)) yield break;
        SettleScroll();
        if (loadingMessageShownAt >= 0f)
        {
            float remaining = 0.35f - (Time.realtimeSinceStartup - loadingMessageShownAt);
            if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);
        }
        if (!IsCurrent(version)) yield break;
        Status("", false);
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 0.22f)
        {
            if (!IsCurrent(version)) yield break;
            SetReveal(Mathf.SmoothStep(0f, 1f, (Time.realtimeSinceStartup - start) / 0.22f), false);
            yield return null;
        }
        SetReveal(1f, true);
        if (itemsScroll != null) itemsScroll.enabled = true;
        revealing = false;
        RefreshScrollbarVisibility();
    }

    private void LateUpdate() => RefreshScrollbarVisibility();

    private void RefreshScrollbarVisibility()
    {
        if (itemsScroll == null || itemsScroll.content == null) return;
        var viewport = itemsScroll.viewport != null ? itemsScroll.viewport : (RectTransform)itemsScroll.transform;
        SetScrollbarReveal(isActiveAndEnabled && dataLoaded && !revealing &&
            itemsScroll.content.rect.height > viewport.rect.height + 1f);
    }

    private void SetScrollbarReveal(bool visible)
    {
        if (scrollbarLoadingGate == null) return;
        scrollbarLoadingGate.alpha = visible ? 1f : 0f;
        scrollbarLoadingGate.interactable = visible;
        scrollbarLoadingGate.blocksRaycasts = visible;
    }

    private bool Fits(CustomizationItem item)
    {
        return item.Target == CustomizationItem.TargetCharacter.Any ||
            (item.Target == CustomizationItem.TargetCharacter.MaleOnly && playerBaseCharacter == "Male_Character") ||
            (item.Target == CustomizationItem.TargetCharacter.FemaleOnly && playerBaseCharacter == "Female_Character");
    }

    private bool HasClothesModel(string itemId)
    {
        var body = playerBaseCharacter == "Female_Character" ? femaleMannequin : maleMannequin;
        foreach (var entry in mannequinModels)
            if (entry != null && entry.model != null && entry.itemID == itemId && body != null &&
                entry.model.transform.IsChildOf(body.transform)) return true;
        return false;
    }

    public async void EquipItem(CustomizationItem itemToEquip)
    {
        if (!dataLoaded || revealing || saving || !IsCurrent(requestVersion) || itemToEquip == null || !Fits(itemToEquip) ||
            (!itemToEquip.IsDefault && !ownedItems.Contains(itemToEquip.ItemID))) return;
        if (itemToEquip.Type == CustomizationItem.ItemType.Clothes && !HasClothesModel(itemToEquip.ItemID)) return;
        saving = true;
        int version = requestVersion;
        int feedbackVersion = ++saveFeedbackVersion;
        Status("", false);
        StartCoroutine(DelayedSavingFeedback(version, feedbackVersion));
        try
        {
            if (!usingSandboxInventory)
            {
                string field = itemToEquip.Type == CustomizationItem.ItemType.Clothes ? "clothes" : "pets";
                await CommerceClient.WithTimeout(SaveOwnedEquipment(field, itemToEquip.ItemID));
                if (!IsCurrent(version)) return;
                if (field == "clothes") equippedClothes = itemToEquip.ItemID; else equippedPet = itemToEquip.ItemID;
                saving = false;
                UpdateUIAndMannequin();
                Status("Equipment saved.", false);
                StartCoroutine(ClearSavedFeedback(version, feedbackVersion));
                return;
            }
                var account = await CommerceClient.EquipItem(itemToEquip.ItemID);
                if (!IsCurrent(version)) return;
                saving = false;
                equippedClothes = account.equippedClothes;
                equippedPet = account.equippedPet;
                UpdateUIAndMannequin();
                Status(account.sandbox ? "Equipment saved in sandbox." : "Equipment saved.", false);
                StartCoroutine(ClearSavedFeedback(version, feedbackVersion));
        }
        catch (System.Exception error)
        {
            if (!IsCurrent(version)) return;
            saving = false;
            Status(error.Message, true);
        }
    }

    private async System.Threading.Tasks.Task<bool> SaveOwnedEquipment(string field, string itemId)
    {
        // The protected database rules check ownership and character, even if
        // a modified client skips the local UI checks.
        await dbRef.Child("users").Child(userId).Child("equipped").Child(field).SetValueAsync(itemId);
        return true;
    }

    private IEnumerator DelayedSavingFeedback(int version, int feedbackVersion)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, saveFeedbackDelay));
        if (IsCurrent(version) && feedbackVersion == saveFeedbackVersion && saving)
            Status("Saving your equipment...", false);
    }

    private IEnumerator ClearSavedFeedback(int version, int feedbackVersion)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, savedFeedbackDuration));
        if (IsCurrent(version) && feedbackVersion == saveFeedbackVersion && !saving)
            Status("", false);
    }


    private void UpdateUIAndMannequin()
    {
        var body = playerBaseCharacter == "Female_Character" ? femaleMannequin : maleMannequin;
        // Set the skin before activating the body, preventing a default outfit from flashing.
        foreach (var entry in mannequinModels)
            if (entry != null && entry.model != null)
                entry.model.SetActive(body != null && entry.model.transform.IsChildOf(body.transform) &&
                    (entry.itemID == equippedClothes || entry.itemID == equippedPet));
        if (maleMannequin != null) maleMannequin.SetActive(body == maleMannequin);
        if (femaleMannequin != null) femaleMannequin.SetActive(body == femaleMannequin);
        if (inspection != null)
            foreach (var entry in mannequinModels)
                if (entry != null && entry.model != null && entry.itemID == equippedClothes && body != null &&
                    entry.model.transform.IsChildOf(body.transform))
                { inspection.ShowCharacter(body.transform,entry.model.transform); break; }
        foreach (var item in allUIItems)
        {
            if (item == null) continue;
            bool owned = item.IsDefault || ownedItems.Contains(item.ItemID);
            item.gameObject.SetActive(owned && Fits(item));
            item.UpdateUI(owned, item.ItemID == equippedClothes || item.ItemID == equippedPet);
        }
    }
}
