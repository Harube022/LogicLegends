/// <summary>Explicit rollout switch. Reuses CommerceSettings; never introduces another wallet or account.</summary>
public static class SoloEconomyConnection
{
#if UNITY_EDITOR
    internal static bool? ValidationEnabled;
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetValidation() { ValidationEnabled = null; }
#endif
    public static bool Enabled
    {
        get
        {
#if UNITY_EDITOR
            if (ValidationEnabled.HasValue) return ValidationEnabled.Value;
            // Isolated checks use fake transports, never a live connection or settings write.
            if (TrialBackendClient.ValidationTransport != null || CommerceClient.ValidationRequest != null) return true;
#endif
            return CommerceClient.Settings != null && CommerceClient.Settings.soloEconomyConnected;
        }
    }
}
