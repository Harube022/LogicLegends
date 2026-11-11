using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>Loads server-verified achievement progress and handles one-time reward claims.</summary>
public sealed class MainMenuAchievementsReveal : MonoBehaviour
{
    [SerializeField] private MenuDataFeedback presentation;
    [SerializeField] private MainMenuAchievementsLayout layout;
    [SerializeField] private ScrollRect scroll;
    private readonly Dictionary<Button, UnityAction> handlers = new Dictionary<Button, UnityAction>();
    private readonly HashSet<string> claiming = new HashSet<string>();
    private int loadVersion;
    private GameObject rewardPopup;
    private TMP_Text rewardHeading, rewardSubtitle, coinAmount, gemAmount;
    private GameObject gemRewardLine;

    private static readonly Dictionary<string, string> RowNames = new Dictionary<string, string>
    {
        {"first_steps", "FirstTrial_Achievement"}, {"truth_seeker", "TruthTable_Achievement"},
        {"inference_adept", "RulesOfInference_Achievement"}, {"the_awakening", "TheAwakening_Achievement"},
        {"the_ascent", "TheAscent_Achievement"}, {"the_reckoning", "TheReckoning_Achievement"},
        {"the_legends_rise", "TheLegendsRise_Achievement"}, {"logic_legend", "AllTrials_Achievement"}
    };

    private void OnEnable()
    {
        int version = ++loadVersion;
        presentation.BeginLoad("Loading achievements...");
        _ = Load(version);
    }

    private async Task Load(int version)
    {
        try
        {
            var data = await TrialBackendClient.LoadAchievements();
            if (version != loadVersion || !isActiveAndEnabled) return;
            Apply(data);
            StartCoroutine(Prepare());
        }
        catch (Exception error)
        {
            if (version == loadVersion && isActiveAndEnabled) presentation.FailLoad(error.Message);
        }
    }

    private void Apply(TrialBackendClient.AchievementList data)
    {
        if (data?.items == null) throw new InvalidOperationException("Achievement data is unavailable. Please retry.");
        foreach (var item in data.items)
        {
            if (item == null || !RowNames.TryGetValue(item.id, out string rowName)) continue;
            var row = scroll.content.Find(rowName);
            if (row == null) continue;
            SetText(row.Find("Title"), item.title);
            SetText(row.Find("Objective"), item.objective);
            SetText(row.Find("Progress"), item.progress + " / " + item.target + " " + item.unit);
            SetReward(row.Find("RewardPreview"), "Coins", item.coins);
            SetReward(row.Find("RewardPreview"), "Gems", item.gems);

            var states = row.Find("ClaimStates");
            var notYet = states.Find("NotYetClaimed");
            var claimState = states.Find("Claim");
            var claimedState = states.Find("Claimed");
            bool claimed = item.claimed;
            bool ready = item.eligible && !claimed;
            if (notYet != null) notYet.gameObject.SetActive(!ready && !claimed);
            if (claimState != null) claimState.gameObject.SetActive(!claimed && (ready || claiming.Contains(item.id)));
            if (claimedState != null) claimedState.gameObject.SetActive(claimed);
            var button = claimState != null ? claimState.Find("Button")?.GetComponent<Button>() : null;
            if (button != null)
            {
                button.interactable = ready && !claiming.Contains(item.id);
                Bind(button, item.id);
            }
        }
    }

    private static void SetText(Transform target, string value)
    {
        var text = target != null ? target.GetComponent<TMP_Text>() : null;
        if (text != null) text.text = value ?? string.Empty;
    }

    private static void SetReward(Transform preview, string currency, int amount)
    {
        var value = preview != null ? preview.Find(currency) : null;
        if (value == null) return;
        value.gameObject.SetActive(amount > 0);
        var label = value.Find("Amount")?.GetComponent<TMP_Text>();
        if (label != null) label.text = amount.ToString("N0") + " " + currency.ToUpperInvariant();
    }

    private void Bind(Button button, string id)
    {
        if (handlers.TryGetValue(button, out var previous)) button.onClick.RemoveListener(previous);
        UnityAction handler = () => _ = Claim(id);
        handlers[button] = handler;
        button.onClick.AddListener(handler);
    }

    private async Task Claim(string id)
    {
        if (claiming.Contains(id) || !isActiveAndEnabled) return;
        int version = loadVersion;
        claiming.Add(id);
        try
        {
            var row = scroll.content.Find(RowNames[id]);
            var button = row?.Find("ClaimStates/Claim/Button")?.GetComponent<Button>();
            if (button != null) button.interactable = false;
            presentation.ShowMessage("Claiming achievement reward...");
            var data = await TrialBackendClient.ClaimAchievement(id);
            if (version != loadVersion || !isActiveAndEnabled) return;
            Apply(data);
            CommerceClient.NotifyChanged();
            var claimed = Array.Find(data.items, item => item.id == id);
            if (claimed != null)
            {
                presentation.HideFeedback();
                ShowRewardPopup(claimed);
            }
            layout.RefreshLayout();
        }
        catch (Exception error)
        {
            claiming.Remove(id);
            if (version == loadVersion && isActiveAndEnabled)
            {
                try { Apply(await TrialBackendClient.LoadAchievements()); }
                catch (Exception) { }
                presentation.ShowTransient(error.Message);
            }
        }
        finally { if (version == loadVersion) claiming.Remove(id); }
    }

    private void ShowRewardPopup(TrialBackendClient.Achievement achievement)
    {
        EnsureRewardPopup();
        rewardHeading.text = "REWARD CLAIMED!";
        rewardSubtitle.text = achievement.title;
        coinAmount.text = "+" + achievement.coins.ToString("N0") + " COINS";
        gemAmount.text = "+" + achievement.gems.ToString("N0") + " GEMS";
        gemRewardLine.SetActive(achievement.gems > 0);
        rewardPopup.transform.SetAsLastSibling();
        rewardPopup.SetActive(true);
    }

    private void EnsureRewardPopup()
    {
        if (rewardPopup != null) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) throw new InvalidOperationException("The achievements canvas is unavailable.");

        var overlayRect = StageUITheme.Rect("AchievementRewardPopup", canvas.transform);
        StageUITheme.Stretch(overlayRect, Vector2.zero, Vector2.one);
        rewardPopup = overlayRect.gameObject;
        var shade = rewardPopup.AddComponent<Image>();
        shade.color = new Color(.015f, .025f, .04f, .78f);
        shade.raycastTarget = true;

        var card = StageUITheme.Rect("RewardCard", overlayRect);
        card.anchorMin = card.anchorMax = card.pivot = Vector2.one * .5f;
        card.sizeDelta = new Vector2(760, 500);
        var canvasRect = canvas.transform as RectTransform;
        if (canvasRect != null)
        {
            float scale = Mathf.Min(1f, (canvasRect.rect.width - 40f) / card.sizeDelta.x,
                (canvasRect.rect.height - 40f) / card.sizeDelta.y);
            card.localScale = Vector3.one * Mathf.Max(.1f, scale);
        }
        var cardImage = card.gameObject.AddComponent<Image>();
        StagePresentation.Frame(cardImage, true);

        rewardHeading = PopupText("Heading", card, "REWARD CLAIMED!", new Vector2(.12f,.74f), new Vector2(.88f,.91f), 44, false);
        rewardHeading.color = StageUITheme.ReadingInk;
        rewardSubtitle = PopupText("Achievement", card, "FIRST STEPS", new Vector2(.12f,.62f), new Vector2(.88f,.74f), 32, false);
        rewardSubtitle.color = StageUITheme.ReadingInk;
        var message = PopupText("Message", card, "You earned:", new Vector2(.15f,.49f), new Vector2(.85f,.60f), 26, true);
        message.color = StageUITheme.ReadingInk;

        var coinSprite = scroll.content.Find("FirstTrial_Achievement/RewardPreview/Coins/Icon")?.GetComponent<Image>()?.sprite;
        var gemSprite = scroll.content.Find("TheAwakening_Achievement/RewardPreview/Gems/Icon")?.GetComponent<Image>()?.sprite;
        AddRewardLine(card, "CoinsReward", coinSprite, new Vector2(.25f,.35f), new Vector2(.75f,.47f), out coinAmount);
        AddRewardLine(card, "GemsReward", gemSprite, new Vector2(.25f,.21f), new Vector2(.75f,.33f), out gemAmount, out gemRewardLine);

        var close = MultiplayerUIArt.CloseAction("CloseRewardPopup", card, .92f, .90f);
        close.onClick.AddListener(CloseRewardPopup);
        rewardPopup.SetActive(false);
    }

    private static TMP_Text PopupText(string name, Transform parent, string value, Vector2 min, Vector2 max, float size, bool reading)
    {
        var label = StageUITheme.Text(name, parent, value, size, reading);
        StageUITheme.Stretch(label.rectTransform, min, max);
        label.alignment = TextAlignmentOptions.Center;
        label.richText = false;
        label.raycastTarget = false;
        return label;
    }

    private static void AddRewardLine(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max,
        out TMP_Text amount, out GameObject lineObject)
    {
        var line = StageUITheme.Rect(name, parent);
        StageUITheme.Stretch(line, min, max);
        lineObject = line.gameObject;

        var iconRect = StageUITheme.Rect("Icon", line);
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(.12f,.5f);
        iconRect.sizeDelta = new Vector2(64,64);
        var icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        amount = PopupText("Amount", line, string.Empty, new Vector2(.25f,.05f), new Vector2(.98f,.95f), 32, false);
        amount.color = StageUITheme.ReadingInk;
        amount.enableAutoSizing = true;
        amount.fontSizeMin = 26;
        amount.fontSizeMax = 32;
    }

    private static void AddRewardLine(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max,
        out TMP_Text amount)
    {
        AddRewardLine(parent, name, sprite, min, max, out amount, out _);
    }

    private void CloseRewardPopup()
    {
        if (rewardPopup != null) rewardPopup.SetActive(false);
    }

    private IEnumerator Prepare()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        layout.RefreshLayout();
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1;
        yield return null;
        presentation.RevealWhenReady();
    }

    private void OnDisable()
    {
        ++loadVersion;
        StopAllCoroutines();
        CloseRewardPopup();
        foreach (var entry in handlers) if (entry.Key != null) entry.Key.onClick.RemoveListener(entry.Value);
        handlers.Clear();
        claiming.Clear();
    }
}
