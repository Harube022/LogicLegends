using UnityEngine.SceneManagement;

/// <summary>Shared lobby destination and scene-owned multiplayer presentation routing.</summary>
public static class MultiplayerSceneRoutes
{
    // Keep the lobby and verification tools on the actual saved scene in Build Settings.
    public const string GameplayPath = "Assets/Scenes/MultiplayerGameplay.unity";
    public const string GameplayName = "MultiplayerGameplay";
    public const string LegacyName = "MultiplayerStage";

    public static bool IsOpenArchive(Scene scene) => scene.path == GameplayPath;
    public static bool IsGameplay(Scene scene) => IsOpenArchive(scene) || scene.name == LegacyName;
}
