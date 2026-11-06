using UnityEngine;

/// <summary>Fresh-run navigation is distinct from each stage's existing checkpoint retry.</summary>
public static class TrialRunFlow
{
    public static void StartOver(int trial)
    {
        TrialRewards.PrepareNewAttempt(trial);
        if (trial == 1) LevelTimerManager.ResetSession();
        if (trial == 2) TruthTableDoorInteraction.ClearSessionForRestart();
        Navigate(TrialCatalog.Get(trial).Scene, trial);
    }
    public static void Study()
    {
        // Use the established Garden entrance; do not mark dialogue or library objectives complete.
        Navigate("LogicGarden", 0);
    }
    static void Navigate(string scene, int trial)
    {
        if (!Application.CanStreamedLevelBeLoaded(scene)) { Debug.LogError("Missing scene: " + scene); return; }
        Time.timeScale = 1;
        GameInput.Instance?.SetGameplayInputBlocked(false);
        if (Player.LocalInstance != null) Player.LocalInstance.ToggleControl(true);
        if (trial > 0) StageSelectionState.Select(trial);
        TeleportManager.Navigate(scene);
    }
}
