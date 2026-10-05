using UnityEngine;

/// <summary>Selects the stage before shared scene managers initialize.</summary>
[DefaultExecutionOrder(-1000)]
public sealed class StandaloneStageSelection : MonoBehaviour
{
    [SerializeField, Range(1, 3)] private int stageNumber = 3;

    private void Awake()
    {
        StageSelectionState.Select(stageNumber);
    }
}
