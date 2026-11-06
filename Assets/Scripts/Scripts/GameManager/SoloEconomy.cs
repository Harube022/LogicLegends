using System;
using System.Linq;
using UnityEngine;

/// <summary>Client display defaults mirror Backend/commerce/economy.json. The server owns payouts and prices.</summary>
public static class SoloEconomy
{
    [Serializable] public sealed class TrialPolicy { public int trial, baseReward, quizReward, quizBonusLimit; }
    [Serializable] public sealed class RarityPrice { public string name; public int coins, gems; }
    [Serializable] public sealed class Config
    {
        public int version, maxRetries, retryReductionPercent;
        public TrialPolicy[] trials;
        public RarityPrice[] rarities;
    }
    [Serializable] public sealed class CosmeticPrice { public string id, rarity; public int coins, gems; }
    static Config defaults, server;
    static CosmeticPrice[] prices;
    public static Config Current => server ?? (defaults ?? (defaults = JsonUtility.FromJson<Config>(Resources.Load<TextAsset>("SoloEconomy").text)));
    public static TrialPolicy Trial(int trial) => Current.trials.Single(p => p.trial == trial);
    public static int BaseAfterRetries(int trial, int retries)
    {
        if (retries < 0) throw new ArgumentOutOfRangeException(nameof(retries));
        return (int)((long)Trial(trial).baseReward * Math.Max(0, 100 - Math.Min(retries, Current.maxRetries) * Current.retryReductionPercent) / 100);
    }
    public static CosmeticPrice Price(string id)
    {
        var quoted = prices?.FirstOrDefault(p => p.id == id);
        if (quoted != null) return quoted;
        string rarity = id.Split('_')[0]; var tier = Current.rarities.FirstOrDefault(p => p.name == rarity);
        return tier == null ? null : new CosmeticPrice { id = id, rarity = rarity, coins = tier.coins, gems = tier.gems };
    }
    public static void ApplyServer(Config config, CosmeticPrice[] catalog)
    {
        if (config == null || config.version < 2 || config.trials == null || config.rarities == null || catalog == null)
            throw new InvalidOperationException("The shop needs the updated economy service. Purchases are unavailable until it reconnects.");
        server = config; prices = catalog;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { defaults = server = null; prices = null; }
}
