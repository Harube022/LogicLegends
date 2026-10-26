using UnityEngine;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using System;

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
    private const string LoadingText = "Loading Shop...";
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
    private bool commerceReady;
    private bool commercePaused;
    private string commerceNotice;

    [Header("Purchase Feedback Timing")]
    [SerializeField] private float purchaseProgressDelay = 0.2f;
    [SerializeField] private float purchaseFeedbackDuration = 1.5f;
    private int purchaseFeedbackVersion;
    private bool purchasing;
    private bool refreshingWallet;
    private float nextWalletCheck;
    private volatile bool walletDirty;
    private int walletUpdateVersion;
    private int connectingVersion = -1;
    private float nextConnectionCheck;

    private void OnEnable() { CommerceClient.Changed += OnCommerceChanged; RetryLoad(); }
    private void OnDisable()
    {
        CommerceClient.Changed -= OnCommerceChanged;
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

    private void OnCommerceChanged() { walletDirty = true; }
    private void Update()
    {
        if (!dataLoaded || purchasing || !IsCurrent(requestVersion)) return;
        if (!commerceReady)
        {
            if (!commercePaused && connectingVersion != requestVersion && Time.unscaledTime >= nextConnectionCheck)
                LoadCommerce(requestVersion);
            return;
        }
        if (refreshingWallet) return;
        if (!walletDirty && Time.unscaledTime < nextWalletCheck) return;
        walletDirty = false;
        nextWalletCheck = Time.unscaledTime + 8;
        RefreshWallet(requestVersion);
    }
    private void OnApplicationFocus(bool focused) { if (focused) walletDirty = true; }
    private async void RefreshWallet(int version)
    {
        refreshingWallet = true;
        int walletVersion = walletUpdateVersion;
        try
        {
            var account = await CommerceClient.Wallet();
            if (!IsCurrent(version) || walletVersion != walletUpdateVersion) return;
            if (account.sandbox)
            {
                var coins = await CommerceClient.WithTimeout(dbRef.Child("users").Child(userId).Child("coins").GetValueAsync());
                if (!IsCurrent(version) || walletVersion != walletUpdateVersion) return;
                if (coins.Value == null) currentCoins = 0;
                else if (!int.TryParse(coins.Value.ToString(), out currentCoins) || currentCoins < 0)
                    throw new InvalidOperationException("Couldn't read coin balance.");
            }
            ApplyAccount(account);
            commerceReady = true;
            commerceNotice = "";
            UpdateShopUI();
        }
        catch (Exception error) { if (IsCurrent(version)) CommerceUnavailable(error); }
        finally { refreshingWallet = false; }
    }
    private void ApplyAccount(CommerceClient.Account account)
    {
        currentGems = account.gems;
        // Sandbox commerce owns test gems only. Existing gameplay coins stay
        // sourced from the real Firebase account, including after purchases.
        if (!account.sandbox) currentCoins = account.coins;
        ownedItems.Clear();
        if (account.inventory != null) foreach (var item in account.inventory) ownedItems.Add(item);
    }
    private async void LoadCommerce(int version)
    {
        if (connectingVersion == version) return;
        connectingVersion = version;
        commerceNotice = "Connecting to the shop...";
        if (!revealing) Status(commerceNotice, false);
        try
        {
            var account = await CommerceClient.LoadAccount(playerBaseCharacter);
            if (!IsCurrent(version)) return;
            ApplyAccount(account);
            commerceReady = true;
            commercePaused = false;
            commerceNotice = "";
            nextWalletCheck = Time.unscaledTime + 8;
            UpdateShopUI();
            Status("", false);
            if (previewUI != null && previewUI.IsOpen && previewedItem != null)
                previewUI.Show(previewedItem, playerBaseCharacter, BalanceFor(previewedItem), ownedItems.Contains(previewedItem.ItemID));
            // Poll while this panel is open. Avoid adding/detaching native
            // database listeners on the project's current desktop Firebase SDK.
        }
        catch (Exception error) { if (IsCurrent(version)) CommerceUnavailable(error); }
        finally { if (connectingVersion == version) connectingVersion = -1; }
    }

    private void CommerceUnavailable(Exception error)
    {
        commerceReady = false;
        commercePaused = error is CommerceClient.ShopSetupException;
        commerceNotice = commercePaused ? "" : "Couldn't connect. Retry to reconnect.";
        nextConnectionCheck = Time.unscaledTime + 15;
        if (!revealing) Status(commerceNotice, !commercePaused);
        if (previewUI != null && previewUI.IsOpen)
            previewUI.ShowPurchasesUnavailable(commercePaused ? "Purchases aren't available yet. You can still preview outfits." : null);
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
        // Reconnect without hiding the already loaded outfit grid.
        if (dataLoaded && IsCurrent(requestVersion))
        {
            LoadCommerce(requestVersion);
            return;
        }
        walletUpdateVersion++;
        if (previewUI != null) previewUI.Close();
        previewedItem = null;
        StopAllCoroutines();
        int version = ++requestVersion;
        dataLoaded = false;
        commerceReady = false;
        commercePaused = false;
        commerceNotice = "Connecting to the shop...";
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
            Status("Please log in to load shop.", true);
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
                Status("Couldn't load the shop. Check the connection and retry.", true);
                return;
            }
            var snapshot = task.Result;
            playerBaseCharacter = snapshot.Child("base_character").Value?.ToString() ?? "";
            if (playerBaseCharacter != "Male_Character" && playerBaseCharacter != "Female_Character")
            {
                Status("Choose a character before opening the shop.", true);
                return;
            }
            if (!ReadBalance(snapshot, "coins", out currentCoins))
            {
                Status("Couldn't read coin balance. Please retry.", true);
                return;
            }
            // Show saved data before contacting the payment host. Reads never
            // authorize purchases; every purchase still needs the backend.
            if (!ReadBalance(snapshot, "gems", out currentGems)) currentGems = 0;
            foreach (var child in snapshot.Child("inventory").Children)
                if (child.Value is bool owned && owned) ownedItems.Add(child.Key);
            dataLoaded = true;
            revealing = true;
            UpdateShopUI();
            StartCoroutine(RevealLoadedContent(version));
            LoadCommerce(version);
        });
        deadline = Time.realtimeSinceStartup + 20f;
        while (!request.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        if (!request.IsCompleted && IsCurrent(version))
        {
            Status("Couldn't load the shop. Check the connection and retry.", true);
            requestVersion++; // A late reply must not replace a newer retry/error.
        }
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
        if (!commerceReady) Status(commerceNotice, !commercePaused);
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
            Status("The shop is unavailable. Please reopen the shop.", false);
            return;
        }
        previewedItem = itemToBuy;
        purchaseFeedbackVersion++; // Old result timers must not clear a different item's preview.
        Status("", false);
        previewUI.Show(itemToBuy, playerBaseCharacter, BalanceFor(itemToBuy), ownedItems.Contains(itemToBuy.ItemID));
        if (!commerceReady) previewUI.ShowPurchasesUnavailable(commercePaused ? "Purchases aren't available. You can still preview outfits." : null);
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
        if (!commerceReady) { if (!commercePaused) Status("Purchases are unavailable. Retry to reconnect.", true); return false; }
        return dataLoaded && IsPreviewCurrent && !revealing && !purchasing && previewUI != null && previewUI.IsOpen &&
            previewedItem != null && Fits(previewedItem) && previewedItem.Price >= 0 &&
            !ownedItems.Contains(previewedItem.ItemID);
    }

    public async void ConfirmPurchase()
    {
        // Only the second, explicit confirmation action can submit a purchase.
        if (!CanPurchasePreview() || !previewUI.IsConfirming) return;
        var itemToBuy = previewedItem;
        int balance = BalanceFor(itemToBuy);
        int version = requestVersion;
        int feedbackVersion = ++purchaseFeedbackVersion;
        if (balance < itemToBuy.Price)
        {
            previewUI.ShowResult("Not enough " + itemToBuy.CurrencyType.ToString().ToLowerInvariant() + ".", false, balance);
            return;
        }
        purchasing = true;
        walletUpdateVersion++;
        Status("", false);
        previewUI.ShowSaving();
        StartCoroutine(DelayedPurchaseFeedback(version, feedbackVersion));
        try
        {
            // Send only the ID: the server owns price, currency, character
            // eligibility, balance checking and the atomic ownership grant.
            var account = await CommerceClient.BuyItem(itemToBuy.ItemID);
            if (!IsCurrent(version)) return;
            purchasing = false;
            ApplyAccount(account);
            UpdateShopUI();
            previewUI.ShowResult(account.sandbox ? "Purchased in sandbox shop." : "Purchase successful. Equip it in Customization.", true, BalanceFor(itemToBuy), true);
            CommerceClient.NotifyChanged();
            StartCoroutine(ClearPurchaseFeedback(version, feedbackVersion));
        }
        catch (Exception error)
        {
            if (!IsCurrent(version)) return;
            purchasing = false;
            ShowPurchaseFailure(error.Message);
        }
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
            case ShopPurchaseDecision.MissingCharacter: return "Couldn't load the saved character. Retry to refresh the shop.";
            case ShopPurchaseDecision.CharacterChanged: return "The saved character changed. Retry to refresh the shop before buying.";
            case ShopPurchaseDecision.AlreadyOwned: return "You already own these clothes. Retry to refresh the shop.";
            case ShopPurchaseDecision.InvalidBalance: return "Couldn't read the balance. Retry to refresh the shop.";
            case ShopPurchaseDecision.InsufficientFunds: return "Not enough " + currency + ". Retry to refresh the balance.";
            default: return "Couldn't confirm the purchase. Retry to refresh the shop.";
        }
    }
}
