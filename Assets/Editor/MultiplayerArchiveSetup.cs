using System;
using UnityEditor;

/// <summary>Connects the existing lobby to the completed scene without rebuilding its environment.</summary>
public static class MultiplayerArchiveSetup
{
    [MenuItem("Logic Legends/Multiplayer/Connect Lobby To Open Archive")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before saving the multiplayer route.");
        MultiplayerStageSetup.UpdateMainMenu();
        MultiplayerStageSetup.EnsureBuildScene();
        MultiplayerUISetup.BuildStageOnly();
    }
}
