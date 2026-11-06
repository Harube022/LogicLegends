#if UNITY_EDITOR
using UnityEngine;

/// <summary>Compatibility entry points for the rejected stride-solver checks.</summary>
public static class LatestSkinFootJitterChecks
{
    public const string Folder="Library/FootJitterAudit";
    public const string Report=LatestSkinSourceMotionChecks.Report;
    public static string Capture(string label)
    {return LatestSkinSourceMotionChecks.Static()+" Run BeginSourceMotion for final-frame comparisons; the procedural solver has been removed.";}
    public static string Compare(){return Capture("source");}
    public static LatestSkinFootMonitor Monitor(IntegratedSkinProfile skin)
    {var monitor=skin.gameObject.AddComponent<LatestSkinFootMonitor>();monitor.Initialize(skin);return monitor;}
}

public sealed class LatestSkinFootMonitor:MonoBehaviour
{
    SourcePoseMonitor source;
    public void Initialize(IntegratedSkinProfile skin)
    {source=skin.gameObject.AddComponent<SourcePoseMonitor>();source.Initialize(skin);}
    public void Verify(){source.Verify();}
    void OnDestroy(){if(source!=null)Destroy(source);}
}
#endif
