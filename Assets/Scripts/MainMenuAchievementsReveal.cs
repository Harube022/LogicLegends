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
                string reward = claimed.coins.ToString("N0") + " coins" + (claimed.gems > 0 ? " and " + claimed.gems.ToString("N0") + " gems" : string.Empty);
                presentation.ShowTransient("Achievement claimed! You received " + reward + ".");
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
        foreach (var entry in handlers) if (entry.Key != null) entry.Key.onClick.RemoveListener(entry.Value);
        handlers.Clear();
        claiming.Clear();
    }
}
