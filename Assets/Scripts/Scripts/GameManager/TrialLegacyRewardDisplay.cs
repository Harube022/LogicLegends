using TMPro;
using UnityEngine;

/// <summary>Compatibility for scene-authored results that still expose a coin text field.</summary>
public sealed class TrialLegacyRewardDisplay : MonoBehaviour
{
    TMP_Text label;
    int trial;
    public static void Bind(TMP_Text label, int trial)
    {
        var view=label.GetComponent<TrialLegacyRewardDisplay>();
        if(view==null)view=label.gameObject.AddComponent<TrialLegacyRewardDisplay>();
        view.label=label;view.trial=trial;view.Refresh();
    }
    void OnEnable(){TrialRewards.Changed+=Refresh;Refresh();}
    void OnDisable(){TrialRewards.Changed-=Refresh;}
    void Refresh(){if(label!=null)label.text=TrialRewards.Status(trial);}
}
