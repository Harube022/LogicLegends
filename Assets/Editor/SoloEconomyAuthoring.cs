#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>Keep player display defaults and server quiz validation aligned with the actual authored data.</summary>
public sealed class SoloEconomyAuthoring : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    [MenuItem("Logic Legends/Economy/Test Backend Connection (Read Only)")]
    public static async void TestConnection()
    {
        try {
            var connection = await CommerceClient.CheckEconomyConnection();
            string[] required = { "original-base-retries", "pending-quiz-bonuses", "dual-currency-cosmetics" };
            if (connection.version < 2 || connection.economy == null || connection.economy.trials.Length != 10 ||
                connection.catalog == null || connection.catalog.Length < 10 || connection.capabilities == null || required.Any(c => !connection.capabilities.Contains(c)))
                throw new InvalidOperationException("The configured backend does not support the complete solo economy v2 contract.");
            UnityEngine.Debug.Log("Solo Economy connection verified. No account, balance, attempt or inventory was changed. The programmer can now enable Solo Economy Connected in CommerceSettings.");
        } catch (Exception error) { UnityEngine.Debug.LogWarning("Solo Economy is not connected: " + error.Message + " Sign in and check CommerceSettings / the deployed backend. The connection switch was not changed."); }
    }
    public static async void AuditCurrentWallet()
    {
        if (TrialBackendClient.UserId == null) { File.WriteAllText("Library/SoloEconomyWalletAudit.json", "{\"authenticated\":false}"); return; }
        try {
            var account = await CommerceClient.Wallet();
            File.WriteAllText("Library/SoloEconomyWalletAudit.json", Newtonsoft.Json.JsonConvert.SerializeObject(new {
                authenticated = true, coins = account.coins, gems = account.gems, ownedCosmetics = account.inventory?.Length ?? 0,
                sandbox = account.sandbox, readOnly = true
            }, Newtonsoft.Json.Formatting.Indented));
        } catch (Exception) { File.WriteAllText("Library/SoloEconomyWalletAudit.json", "{\"authenticated\":true,\"walletAvailable\":false,\"readOnly\":true}"); }
    }
    [MenuItem("Logic Legends/Economy/Sync Policy and Export Quiz Answers")]
    public static void Sync()
    {
        File.Copy("Backend/commerce/economy.json", "Assets/Resources/SoloEconomy.json", true);
        File.WriteAllText("Backend/commerce/quiz-questions.json", Newtonsoft.Json.JsonConvert.SerializeObject(QuizData(), Newtonsoft.Json.Formatting.Indented));
        AssetDatabase.ImportAsset("Assets/Resources/SoloEconomy.json");
    }
    static object QuizData()
    {
        var bank = AssetDatabase.LoadAssetAtPath<TruthTableDoorQuestionBank>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:TruthTableDoorQuestionBank").Single()));
        return new {
            truthTable = bank.questions.Select((q,i) => new { id = "tt-"+i, prompt = q.prompt, choices = q.choices, correctChoice = q.correctChoice }),
            inference = LogicLegends.Inference.InferencePickupQuestions.Bank.Select(q => new { id = "roi-"+q.Id, prompt = q.Prompt, choices = q.Choices, correctChoice = 0 })
        };
    }
    public void OnPreprocessBuild(BuildReport report)
    {
        if (report != null && (report.summary.options & BuildOptions.Development) == 0 && !SoloEconomyConnection.Enabled)
            throw new BuildFailedException("Solo Economy is disconnected. Deploy the backend, run its read-only connection check, and enable Solo Economy Connected in CommerceSettings before a release build. Development builds can test locally.");
        if (report != null && (report.summary.options & BuildOptions.Development) == 0 &&
            (CommerceClient.Settings.sandbox || !Uri.TryCreate(CommerceClient.Settings.backendUrl,UriKind.Absolute,out var endpoint) || endpoint.Scheme != "https"))
            throw new BuildFailedException("A release economy requires the hosted HTTPS backend URL with Sandbox disabled.");
        string canonical = File.ReadAllText("Backend/commerce/economy.json"), client = File.ReadAllText("Assets/Resources/SoloEconomy.json");
        if (!Newtonsoft.Json.Linq.JToken.DeepEquals(Newtonsoft.Json.Linq.JToken.Parse(canonical), Newtonsoft.Json.Linq.JToken.Parse(client)))
            throw new BuildFailedException("Sync the Solo Economy policy before building.");
        if (!Newtonsoft.Json.Linq.JToken.DeepEquals(Newtonsoft.Json.Linq.JToken.FromObject(QuizData()), Newtonsoft.Json.Linq.JToken.Parse(File.ReadAllText("Backend/commerce/quiz-questions.json"))))
            throw new BuildFailedException("Export the current pickup quiz bank before building the game and deploying its reward service.");
    }
}
#endif
