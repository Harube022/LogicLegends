using UnityEngine;

/// <summary>Sets the standalone scene's trial before the shared area and UI awaken.</summary>
[DefaultExecutionOrder(-1100)]
public sealed class TruthTableSceneSetup : MonoBehaviour
{
    private void Awake()
    {
        if (gameObject.scene.name == "TruthTable") StageSelectionState.Select(2);
    }
}
