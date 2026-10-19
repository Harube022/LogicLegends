using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Settles the existing achievement preview before revealing it; no progress or rewards are changed.</summary>
public sealed class MainMenuAchievementsReveal : MonoBehaviour
{
    [SerializeField] private MenuDataFeedback presentation;
    [SerializeField] private MainMenuAchievementsLayout layout;
    [SerializeField] private ScrollRect scroll;

    private void OnEnable()
    {
        presentation.BeginLoad("Loading achievements...");
        StartCoroutine(Prepare());
    }

    private IEnumerator Prepare()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        layout.RefreshLayout();
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1;
        yield return null;
        presentation.RevealWhenReady();
    }

    private void OnDisable() => StopAllCoroutines();
}
