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
    [Header("Clothes Preview And Confirmation")]
    [SerializeField] private ShopPreviewUI previewUI;
    private ShopItem previewedItem;
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
        if (previewUI != null) previewUI.Close();
        previewedItem = null;
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
        if (previewUI != null) previewUI.Close();
        previewedItem = null;
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
            !Fits(itemToBuy) || itemToBuy.Price < 0) return;
        if (previewUI == null)
        {
            Status("The clothes preview is unavailable. Please reopen the shop.", false);
            return;
        }
        previewedItem = itemToBuy;
        purchaseFeedbackVersion++; // Old result timers must not clear a different item's preview.
        Status("", false);
        previewUI.Show(itemToBuy, playerBaseCharacter, BalanceFor(itemToBuy), ownedItems.Contains(itemToBuy.ItemID));
    }

    // A failed purchase can keep its modal open for feedback/retry, even though
    // fresh shop data is required before another purchase is allowed.
    public bool IsPreviewCurrent => IsCurrent(requestVersion);
    public bool IsPurchasing => purchasing;
    private int BalanceFor(ShopItem item) => item.CurrencyType == ShopItem.Currency.Coins ? currentCoins : currentGems;

    public void RequestPurchaseConfirmation()
    {
        if (!CanPurchasePreview()) return;
        previewUI.ShowConfirmation(BalanceFor(previewedItem));
    }

    private bool CanPurchasePreview()
    {
        return dataLoaded && IsPreviewCurrent && !revealing && !purchasing && previewUI != null && previewUI.IsOpen &&
            previewedItem != null && Fits(previewedItem) && previewedItem.Price >= 0 &&
            !ownedItems.Contains(previewedItem.ItemID);
    }

    public void ConfirmPurchase()
    {
        // Only the second, explicit confirmation action can submit a purchase.
        if (!CanPurchasePreview() || !previewUI.IsConfirming) return;
        var itemToBuy = previewedItem;
        bool coins = itemToBuy.CurrencyType == ShopItem.Currency.Coins;
        int balance = BalanceFor(itemToBuy);
        int version = requestVersion;
        int feedbackVersion = ++purchaseFeedbackVersion;
        if (balance < itemToBuy.Price)
        {
            previewUI.ShowResult("Not enough " + itemToBuy.CurrencyType.ToString().ToLowerInvariant() + ".", false, balance);
            return;
        }
        purchasing = true;
        Status("", false);
        previewUI.ShowSaving();
        StartCoroutine(DelayedPurchaseFeedback(version, feedbackVersion));
        string itemId = itemToBuy.ItemID;
        int price = itemToBuy.Price;
        string expectedCharacter = playerBaseCharacter;
        string currency = coins ? "coins" : "gems";
        string rejection = null;
        // Recheck the server balance and ownership together, so stale previews or
        // purchases from another device cannot charge twice or overwrite currency.
        dbRef.Child("users").Child(userId).RunTransaction(data =>
        {
            rejection = null;
            int freshBalance;
            var decision = ShopPurchaseRules.Evaluate(data.Value != null,
                data.Child("base_character").Value?.ToString(), expectedCharacter,
                data.Child("inventory").Child(itemId).Value?.ToString().ToLowerInvariant() == "true",
                data.Child(currency).Value, price, out freshBalance);
            // Firebase may first invoke this callback with an empty local cache.
            // Submit the unchanged value so the server can supply the actual
            // account and retry; do not confuse a cache miss with a changed body.
            if (decision == ShopPurchaseDecision.WaitForServerData) return TransactionResult.Success(data);
            if (decision != ShopPurchaseDecision.Ready)
            {
                rejection = ShopPurchaseRules.Feedback(decision, currency);
                return TransactionResult.Abort();
            }
            data.Child(currency).Value = freshBalance - price;
            data.Child("inventory").Child(itemId).Value = true;
            return TransactionResult.Success(data);
        }).ContinueWithOnMainThread(task =>
        {
            if (!IsCurrent(version)) return;
            purchasing = false;
            if (task.IsFaulted || task.IsCanceled || rejection != null)
            {
                ShowPurchaseFailure(rejection ?? "Couldn't complete the purchase. Retry to refresh your shop.");
                return;
            }
            // An unchanged empty account is not a confirmed purchase.
            if (task.Result == null || !task.Result.Exists ||
                task.Result.Child("inventory").Child(itemId).Value?.ToString().ToLowerInvariant() != "true")
            {
                ShowPurchaseFailure("Couldn't confirm the purchase. Retry to refresh your shop.");
                return;
            }
            ReadBalance(task.Result, "coins", out currentCoins);
            ReadBalance(task.Result, "gems", out currentGems);
            ownedItems.Add(itemId);
            UpdateShopUI();
            previewUI.ShowResult("Purchase successful. Equip it in Customization.", true, BalanceFor(itemToBuy), true);
            StartCoroutine(ClearPurchaseFeedback(version, feedbackVersion));
        });
    }

    private void ShowPurchaseFailure(string message)
    {
        dataLoaded = false;
        HideItems();
        Status("", false);
        previewUI.ShowError(message);
    }

    private IEnumerator DelayedPurchaseFeedback(int version, int feedbackVersion)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, purchaseProgressDelay));
        if (IsCurrent(version) && feedbackVersion == purchaseFeedbackVersion && purchasing)
            previewUI.ShowProgress();
    }

    private IEnumerator ClearPurchaseFeedback(int version, int feedbackVersion)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, purchaseFeedbackDuration));
        if (IsCurrent(version) && feedbackVersion == purchaseFeedbackVersion && !purchasing)
            previewUI.ClearResultFeedback();
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

public enum ShopPurchaseDecision
{
    WaitForServerData, MissingCharacter, CharacterChanged, AlreadyOwned, InvalidBalance, InsufficientFunds, Ready
}

/// <summary>Pure transaction validation, also used to check cold-cache and stale-account regressions without database writes.</summary>
public static class ShopPurchaseRules
{
    public static ShopPurchaseDecision Evaluate(bool hasAccountData, string storedCharacter, string expectedCharacter,
        bool owned, object balanceValue, int price, out int balance)
    {
        balance = 0;
        if (!hasAccountData) return ShopPurchaseDecision.WaitForServerData;
        if (string.IsNullOrEmpty(storedCharacter)) return ShopPurchaseDecision.MissingCharacter;
        if (storedCharacter != expectedCharacter) return ShopPurchaseDecision.CharacterChanged;
        if (owned) return ShopPurchaseDecision.AlreadyOwned;
        if (price < 0 || (balanceValue != null && (!int.TryParse(balanceValue.ToString(), out balance) || balance < 0)))
            return ShopPurchaseDecision.InvalidBalance;
        return balance < price ? ShopPurchaseDecision.InsufficientFunds : ShopPurchaseDecision.Ready;
    }

    public static string Feedback(ShopPurchaseDecision decision, string currency)
    {
        switch (decision)
        {
            case ShopPurchaseDecision.MissingCharacter: return "Couldn't load your saved character. Retry to refresh your shop.";
            case ShopPurchaseDecision.CharacterChanged: return "Your saved character changed. Retry to refresh your shop before buying.";
            case ShopPurchaseDecision.AlreadyOwned: return "You already own these clothes. Retry to refresh your shop.";
            case ShopPurchaseDecision.InvalidBalance: return "Couldn't read your balance. Retry to refresh your shop.";
            case ShopPurchaseDecision.InsufficientFunds: return "Not enough " + currency + ". Retry to refresh your balance.";
            default: return "Couldn't confirm the purchase. Retry to refresh your shop.";
        }
    }
}
