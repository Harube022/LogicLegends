using UnityEngine;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class ShopManager : MonoBehaviour
{
    [Header("Currency UI")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text gemsText;
    [Header("All Shop Items")]
    [SerializeField] private ShopItem[] allShopItems;
    [Header("Loading Feedback")]
    [SerializeField] private TMP_Text loadingMessage;
    [SerializeField] private UnityEngine.UI.Button retryButton;
    [Header("Loading Transition")]
    [SerializeField] private UnityEngine.UI.ScrollRect itemsScroll;
    [SerializeField] private CanvasGroup[] revealGroups = new CanvasGroup[0];
    private const string LoadingText = "Loading your shop and balances...";
    private bool pendingLoadingFeedback;
    private float loadingMessageShownAt = -1f;
    private bool revealing;

    private DatabaseReference dbRef;
    private string userId;
    private string playerBaseCharacter = "";
    private int currentCoins;
    private int currentGems;
    private readonly HashSet<string> ownedItems = new HashSet<string>();
    private int requestVersion;
    private bool dataLoaded;

    [Header("Purchase Feedback Timing")]
    [SerializeField] private float purchaseProgressDelay = 0.2f;
    [SerializeField] private float purchaseFeedbackDuration = 1.5f;
    private int purchaseFeedbackVersion;
    private bool purchasing;

    private void OnEnable() { RetryLoad(); }
    private void OnDisable()
    {
        requestVersion++;
        StopAllCoroutines();
        dataLoaded = false;
        purchasing = false;
        revealing = false;
        HideItems();
        if (itemsScroll != null) itemsScroll.enabled = true;
    }

    private void HideItems()
    {
        SetReveal(0f, false);
        if (itemsScroll != null)
        {
            itemsScroll.StopMovement();
            itemsScroll.enabled = false;
        }
        foreach (var item in allShopItems)
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
        if (!isActiveAndEnabled || purchasing) return;
        StopAllCoroutines();
        int version = ++requestVersion;
        dataLoaded = false;
        revealing = false;
        playerBaseCharacter = "";
        ownedItems.Clear();
        currentCoins = currentGems = 0;
        HideItems();
        if (coinsText != null) coinsText.text = "...";
        if (gemsText != null) gemsText.text = "...";
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
            Status("Please log in to load your shop.", true);
            yield break;
        }
        userId = user.UserId;
        dbRef = FirebaseDatabase.DefaultInstance.RootReference;
        var request = dbRef.Child("users").Child(userId).GetValueAsync();
        request.ContinueWithOnMainThread(task =>
        {
            if (!IsCurrent(version)) return;
            if (task.IsCanceled || task.IsFaulted)
            {
                Status("Couldn't load your shop. Check your connection and retry.", true);
                return;
            }
            var snapshot = task.Result;
            playerBaseCharacter = snapshot.Child("base_character").Value?.ToString() ?? "";
            if (playerBaseCharacter != "Male_Character" && playerBaseCharacter != "Female_Character")
            {
                Status("Choose your character before opening the shop.", true);
                return;
            }
            // Missing balances display as zero. Loading never writes zeros over saved currency.
            if (!ReadBalance(snapshot, "coins", out currentCoins) ||
                !ReadBalance(snapshot, "gems", out currentGems))
            {
                Status("Couldn't read your balances. Please retry.", true);
                return;
            }
            ownedItems.Clear();
            foreach (var item in snapshot.Child("inventory").Children)
                if (item.Value != null && item.Value.ToString().ToLowerInvariant() == "true")
                    ownedItems.Add(item.Key);
            dataLoaded = true;
            revealing = true;
            UpdateShopUI();
            pendingLoadingFeedback = false;
            StartCoroutine(RevealLoadedContent(version));
        });
        deadline = Time.realtimeSinceStartup + 20f;
        while (!request.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        if (!request.IsCompleted && IsCurrent(version))
            Status("Your connection is taking longer. You can wait or retry.", true);
    }

    private bool IsCurrent(int version)
    {
        return this != null && isActiveAndEnabled && version == requestVersion &&
            FirebaseManager.IsReady && FirebaseAuth.DefaultInstance.CurrentUser != null &&
            FirebaseAuth.DefaultInstance.CurrentUser.UserId == userId;
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
        // Filter and rebuild at alpha zero so the shrinking grid never moves on screen.
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
    }

    private static bool ReadBalance(DataSnapshot snapshot, string key, out int balance)
    {
        balance = 0;
        var value = snapshot.Child(key).Value;
        return value == null || (int.TryParse(value.ToString(), out balance) && balance >= 0);
    }

    private bool Fits(ShopItem item)
    {
        return item.Target == ShopItem.TargetCharacter.Any ||
            (item.Target == ShopItem.TargetCharacter.MaleOnly && playerBaseCharacter == "Male_Character") ||
            (item.Target == ShopItem.TargetCharacter.FemaleOnly && playerBaseCharacter == "Female_Character");
    }

    public void AttemptPurchase(ShopItem itemToBuy)
    {
        if (!dataLoaded || revealing || purchasing || !IsCurrent(requestVersion) || itemToBuy == null ||
            !Fits(itemToBuy) || ownedItems.Contains(itemToBuy.ItemID) || itemToBuy.Price < 0) return;
        bool coins = itemToBuy.CurrencyType == ShopItem.Currency.Coins;
        int balance = coins ? currentCoins : currentGems;
        int version = requestVersion;
        int feedbackVersion = ++purchaseFeedbackVersion;
        if (balance < itemToBuy.Price)
        {
            Status("Not enough " + itemToBuy.CurrencyType.ToString().ToLowerInvariant() + ".", false);
            StartCoroutine(ClearPurchaseFeedback(version, feedbackVersion));
            return;
        }
        purchasing = true;
        Status("", false);
        StartCoroutine(DelayedPurchaseFeedback(version, feedbackVersion));
        // Save the inventory and the charged balance together; leave the other currency untouched.
        var updates = new Dictionary<string, object>
        {
            { coins ? "coins" : "gems", balance - itemToBuy.Price },
            { "inventory/" + itemToBuy.ItemID, true }
        };
        dbRef.Child("users").Child(userId).UpdateChildrenAsync(updates).ContinueWithOnMainThread(task =>
        {
            if (!IsCurrent(version)) return;
            purchasing = false;
            if (task.IsFaulted || task.IsCanceled)
            {
                dataLoaded = false;
                HideItems();
                Status("Couldn't save the purchase. Retry to reload your balances.", true);
                return;
            }
            if (coins) currentCoins = balance - itemToBuy.Price;
            else currentGems = balance - itemToBuy.Price;
            ownedItems.Add(itemToBuy.ItemID);
            UpdateShopUI();
            Status("Purchase successful.", false);
            StartCoroutine(ClearPurchaseFeedback(version, feedbackVersion));
        });
    }

    private IEnumerator DelayedPurchaseFeedback(int version, int feedbackVersion)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, purchaseProgressDelay));
        if (IsCurrent(version) && feedbackVersion == purchaseFeedbackVersion && purchasing)
            Status("Saving your purchase...", false);
    }

    private IEnumerator ClearPurchaseFeedback(int version, int feedbackVersion)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, purchaseFeedbackDuration));
        if (IsCurrent(version) && feedbackVersion == purchaseFeedbackVersion && !purchasing)
            Status("", false);
    }


    private void UpdateShopUI()
    {
        if (coinsText != null) coinsText.text = currentCoins.ToString();
        if (gemsText != null) gemsText.text = currentGems.ToString();
        foreach (var item in allShopItems)
        {
            if (item == null) continue;
            item.gameObject.SetActive(Fits(item));
            item.UpdateUI(ownedItems.Contains(item.ItemID));
        }
    }
}
