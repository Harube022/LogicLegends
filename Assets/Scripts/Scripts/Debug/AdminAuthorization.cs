using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Authenticated identity plus a short-lived backend decision. UI toggles cannot grant access.</summary>
public static class AdminAuthorization
{
    static string observedUid, observedEmail, verifiedUid, verifiedEmail;
    static bool verified, loading;
    static float refreshAt, expiresAt;
    static int version;
    public static string ConnectionStatus { get; private set; } = "";
    public static string Normalize(string email) => (email ?? "").Trim().ToLowerInvariant();
    public static bool IsAllowedEmail(string email)
    {
        switch (Normalize(email))
        {
            case "sngbdsa137@gmail.com":
            case "russelbarrameda593@gmail.com":
            case "famatigabarrameda@gmail.com": 
            case "gravata.joshuaharvey@ncst.edu.ph":
            case "cayangyang.jhayfel@ncst.edu.ph": 
            return true;
            default: return false;
        }
    }
    public static bool IsAuthorized => verified && Time.realtimeSinceStartup < expiresAt &&
        !string.IsNullOrEmpty(TrialBackendClient.UserId) && TrialBackendClient.UserId == verifiedUid &&
        Normalize(TrialBackendClient.Email) == verifiedEmail && IsAllowedEmail(TrialBackendClient.Email);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        observedUid = observedEmail = verifiedUid = verifiedEmail = null;
        verified = loading = false; refreshAt = expiresAt = 0; version++;
        ConnectionStatus = "";
    }
    public static void Refresh()
    {
        verified = false; verifiedUid = verifiedEmail = null;
        refreshAt = expiresAt = 0; loading = false; version++;
        ConnectionStatus = "";
    }
    public static void Tick()
    {
        string uid = TrialBackendClient.UserId, email = Normalize(TrialBackendClient.Email);
        if (uid != observedUid || email != observedEmail)
        { Refresh(); observedUid = uid; observedEmail = email; }
        if (!loading && uid != null && IsAllowedEmail(email) && Time.realtimeSinceStartup >= refreshAt)
            _ = Verify(uid, email, version);
    }
    static async Task Verify(string uid, string email, int generation)
    {
        loading = true; refreshAt = Time.realtimeSinceStartup + 15;
        ConnectionStatus = "Checking admin access...";
        try
        {
            var access = await TrialBackendClient.LoadAccess();
            if (generation != version || TrialBackendClient.UserId != uid || Normalize(TrialBackendClient.Email) != email) return;
            verified = access != null && access.admin && access.uid == uid;
            verifiedUid = uid; verifiedEmail = email;
            ConnectionStatus = verified ? "Admin access confirmed." : "This account does not have verified admin access.";
            expiresAt = Time.realtimeSinceStartup + 90; refreshAt = Time.realtimeSinceStartup + 60;
        }
        catch (Exception)
        {
            if (generation != version) return;
            verified = false;
            refreshAt = Time.realtimeSinceStartup + 15;
            ConnectionStatus = "Admin access could not be confirmed. Check the backend connection or sign in again. Retrying in 15 seconds.";
            Debug.LogWarning(ConnectionStatus);
        }
        finally { if (generation == version) loading = false; }
    }
    public static bool TryUseGameplayAdmin()
    {
        if (!IsAuthorized) return false;
        if (TrialRewards.AdminExclusionConfirmed) return true;
        _ = TrialRewards.PrepareAdminAction();
        return false; // No clear/skip until its exclusion has been saved by the backend.
    }
}
