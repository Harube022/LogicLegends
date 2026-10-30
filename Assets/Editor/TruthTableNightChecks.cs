#if UNITY_EDITOR
using System.Collections;
// Compatibility for existing editor check commands after the sunset replacement.
public static class TruthTableNightChecks
{
    public static IEnumerator Run() => TruthTableSunsetChecks.Run();
}
#endif
