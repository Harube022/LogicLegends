using UnityEngine.SceneManagement;

/// <summary>Shared lobby destination and scene-owned multiplayer presentation routing.</summary>
public static class MultiplayerSceneRoutes
{
    // Use the full path: there are older MultiplayerGameplay scenes elsewhere in Assets.
    public const string GameplayPath = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Testing/MultiplayerGameplay.unity";
    public const string GameplayName = "MultiplayerGameplay";
    public const string LegacyName = "MultiplayerStage";

    public static bool IsOpenArchive(Scene scene) => scene.path == GameplayPath;
    public static bool IsGameplay(Scene scene) => IsOpenArchive(scene) || scene.name == LegacyName;
}
