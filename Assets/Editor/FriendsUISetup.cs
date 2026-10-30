using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Adds the Friends entry points and one shared panel to the saved Main Menu scene.</summary>
[InitializeOnLoad]
public static class FriendsUISetup
{
    private const string RequestPath = "Temp/FriendsUISetup.request";
    private const string ResultPath = "Temp/FriendsUISetup.result";

    static FriendsUISetup() { EditorApplication.update += CheckRequest; }

    private static void CheckRequest()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            return;
        }
        try
        {
            File.Delete(RequestPath);
            Build();
            File.WriteAllText(ResultPath, "PASS\nFriends buttons and panel saved to Main Menu.");
        }
        catch (Exception error)
        {
            File.WriteAllText(ResultPath, "FAIL\n" + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("Logic Legends/Friends/Build Friends Interface")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        MultiplayerUISetup.ConfigureArtwork();
        const string path = "Assets/Scenes/Main Menu.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        Scene previous = SceneManager.GetActiveScene();
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var menu = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MainMenuManager>(true)).Single();
            var multiplayer = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MultiplayerMenuManager>(true)).Single();
            var lobby = multiplayer.GetComponent<MultiplayerLobbyUI>();
            if (lobby == null) throw new InvalidOperationException("The private multiplayer lobby is missing.");
            var friends = menu.GetComponent<FriendsUI>();
            if (friends != null && friends.HasInterface) friends.UpdatePresentation();
            else friends = FriendsUI.Build(menu, multiplayer, lobby);
            EditorUtility.SetDirty(friends);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
