#if UNITY_EDITOR
// Compatibility for older editor commands. The stage now uses sunset, never the retired mask.
public static class TruthTableNightSetup
{
    public static object Apply() => TruthTableSunsetSetup.Apply();
}
#endif
