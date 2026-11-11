using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>One backend decision shared by login, scene entry and the existing Admin UI.</summary>
public static class AdminAuthorization
{
    static TrialBackendClient.Access access;
    static Task<TrialBackendClient.Access> pending;
    static string pendingUid;
    static int generation;
    static float expiresAt;
    internal static string DenialMessage { get; private set; }
    public static string ConnectionStatus { get; private set; } = "";
    public static string Normalize(string email) => (email ?? "").Trim().ToLowerInvariant();
    public static bool IsAuthorized => IsCurrent && access.admin;
    internal static bool IsCurrent => access != null && access.uid == TrialBackendClient.UserId &&
        Time.realtimeSinceStartup < expiresAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { Refresh(); DenialMessage = null; }

    public static void Refresh()
    {
        generation++;
        access = null;
        pending = null;
        pendingUid = null;
        expiresAt = 0;
        ConnectionStatus = "";
    }

    internal static void RememberDenial(string message) { Refresh(); DenialMessage = message; }
    internal static string TakeDenial()
    {
        string message = DenialMessage;
        DenialMessage = null;
        return message;
    }

    internal static Task<TrialBackendClient.Access> Verify()
    {
        string uid = TrialBackendClient.UserId;
        if (string.IsNullOrEmpty(uid))
            return Task.FromException<TrialBackendClient.Access>(new TrialBackendClient.AccountException(401, "Please sign in again."));
        if (pendingUid == uid && pending != null && !pending.IsCompleted) return pending;
        pendingUid = uid;
        pending = Check(uid, ++generation);
        return pending;
    }

    static async Task<TrialBackendClient.Access> Check(string uid, int version)
    {
        try
        {
            var result = await TrialBackendClient.LoadAccess();
            if (version != generation || uid != TrialBackendClient.UserId)
                throw new InvalidOperationException("Your account changed. Please retry.");
            if (result == null || result.uid != uid || result.policyVersion != 1
#if UNITY_EDITOR
                && TrialBackendClient.ValidationTransport == null
#endif
               ) throw new InvalidOperationException("The account service is not ready. Please retry.");
            access = result;
            expiresAt = Time.realtimeSinceStartup + 150f;
            ConnectionStatus = result.admin ? "Admin access confirmed." : "This account does not have verified admin access.";
            return result;
        }
        catch
        {
            if (version == generation)
            {
                access = null;
                ConnectionStatus = "Account access could not be confirmed.";
            }
            throw;
        }
    }

    // Retain the explicit opt-in offline test hook; the player uses the session service.
    public static async void Tick()
    {
#if UNITY_EDITOR
        if (TrialBackendClient.ValidationTransport == null || IsCurrent || pending?.IsCompleted == false) return;
        try { await Verify(); } catch (Exception) { }
#endif
        await Task.CompletedTask;
    }

    public static bool TryUseGameplayAdmin()
    {
        if (!IsAuthorized) return false;
        if (TrialRewards.AdminExclusionConfirmed) return true;
        _ = TrialRewards.PrepareAdminAction();
        return false;
    }
}
