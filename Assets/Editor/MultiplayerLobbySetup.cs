using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MultiplayerLobbySetup
{
    [MenuItem("Logic Legends/Multiplayer/Build Private Lobby UI")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before building lobby UI.");
        MultiplayerUISetup.ConfigureArtwork();
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Main Menu.unity") throw new System.InvalidOperationException("Open Main Menu first.");
        var menu = Object.FindFirstObjectByType<MainMenuManager>();
        var network = Object.FindFirstObjectByType<MultiplayerMenuManager>();
        var properties = new SerializedObject(menu);
        var play = (GameObject)properties.FindProperty("playMenuPanel").objectReferenceValue;
        var ui = network.GetComponent<MultiplayerLobbyUI>();
        if (ui == null) ui = network.gameObject.AddComponent<MultiplayerLobbyUI>();
        ui.Build(play.GetComponentInParent<Canvas>().transform, menu, play);
        network.LobbyUI = ui;
        EditorUtility.SetDirty(network); EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Private lobby UI saved. Play > Multiplayer > Host Game / Join Game.");
    }
}
