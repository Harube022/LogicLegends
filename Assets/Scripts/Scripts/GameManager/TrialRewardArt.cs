using UnityEngine;

/// <summary>A reference to the existing currency artwork, without copying the source texture.</summary>
public sealed class TrialRewardArt : ScriptableObject
{
    public Sprite coin;
    public static Sprite Coin => Resources.Load<TrialRewardArt>("StageUI/TrialRewardArt")?.coin;
}
