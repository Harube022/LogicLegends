using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Updates only the lobby and scene-owned chat; never rebuilds or unpacks the stage environment.</summary>
[InitializeOnLoad]
public static class MultiplayerUISetup
{
    static MultiplayerUISetup() { EditorApplication.update+=Requested; }
    private static void Requested()
    {
        const string request="Temp/MultiplayerUISetup.request";
        if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        string mode;
        try { mode=File.ReadAllText(request).Trim();File.Delete(request); } catch(IOException) { return; }
        try
        {
            if(mode=="lobby")BuildLobbyOnly();else if(mode=="stage")BuildStageOnly();else Build();
            File.WriteAllText("Temp/MultiplayerUISetup.result","PASS\n"+(mode=="lobby"?"Main Menu lobby saved.":mode=="stage"?"MultiplayerStage chat saved; canvas prefab remains connected.":"Lobby and MultiplayerStage chat saved; shared prefabs remain connected."));
        }
        catch(Exception error) { File.WriteAllText("Temp/MultiplayerUISetup.result","FAIL\n"+error);Debug.LogException(error); }
    }
    [MenuItem("Logic Legends/Multiplayer/Build In-Game Chat UI Only")]
    public static void BuildStageOnly()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before rebuilding multiplayer UI.");
        ConfigureArtwork();
        var previous=SceneManager.GetActiveScene();
        var stage=SceneManager.GetSceneByPath(MultiplayerStageSetup.ScenePath);bool opened=!stage.isLoaded;
        if(opened)stage=EditorSceneManager.OpenScene(MultiplayerStageSetup.ScenePath,OpenSceneMode.Additive);
        try
        {
            var canvas=stage.GetRootGameObjects().First(g=>g.name=="Canvas 1");
            var safe=canvas.transform.Find("SafeArea");
            if(safe==null||!PrefabUtility.IsPartOfPrefabInstance(canvas))throw new InvalidOperationException("A connected stage canvas with a safe area is required.");
            MultiplayerGameChat.Build(safe);
            EditorSceneManager.MarkSceneDirty(stage);EditorSceneManager.SaveScene(stage);
        }
        finally
        {
            if(previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(opened)EditorSceneManager.CloseScene(stage,true);
        }
    }
    [MenuItem("Logic Legends/Multiplayer/Build Lobby UI Only")]
    public static void BuildLobbyOnly()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before rebuilding multiplayer UI.");
        ConfigureArtwork();
        var previous=SceneManager.GetActiveScene();
        var menu=SceneManager.GetSceneByPath("Assets/Scenes/Main Menu.unity");bool openedMenu=!menu.isLoaded;
        if(openedMenu)menu=EditorSceneManager.OpenScene("Assets/Scenes/Main Menu.unity",OpenSceneMode.Additive);
        try { SceneManager.SetActiveScene(menu);MultiplayerLobbySetup.Build(); }
        finally
        {
            if(previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(openedMenu)EditorSceneManager.CloseScene(menu,true);
        }
    }
    [MenuItem("Logic Legends/Multiplayer/Build Lobby And In-Game Chat UI")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before rebuilding multiplayer UI.");
        ConfigureArtwork();
        var previous=SceneManager.GetActiveScene();
        var menu=SceneManager.GetSceneByPath("Assets/Scenes/Main Menu.unity");bool openedMenu=!menu.isLoaded;
        if(openedMenu)menu=EditorSceneManager.OpenScene("Assets/Scenes/Main Menu.unity",OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(menu);MultiplayerLobbySetup.Build();
            var stage=SceneManager.GetSceneByPath(MultiplayerStageSetup.ScenePath);
            if(!stage.isLoaded)stage=EditorSceneManager.OpenScene(MultiplayerStageSetup.ScenePath,OpenSceneMode.Additive);
            var canvas=stage.GetRootGameObjects().First(g=>g.name=="Canvas 1");
            var safe=canvas.transform.Find("SafeArea");
            if(safe==null)throw new InvalidOperationException("The stage canvas has no safe area.");
            MultiplayerGameChat.Build(safe);
            if(!PrefabUtility.IsPartOfPrefabInstance(canvas))throw new InvalidOperationException("Stage canvas must remain a linked prefab instance.");
            EditorSceneManager.MarkSceneDirty(stage);EditorSceneManager.SaveScene(stage);
            Debug.Log("Multiplayer UI updated: padded lobby cards and MultiplayerStage-only party chat.");
        }
        finally
        {
            if(previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(openedMenu)EditorSceneManager.CloseScene(menu,true);
        }
    }
    public static void ConfigureArtwork()
    {
        MultiplayerUIArt.ChatFrame=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/GraphHUD/GraphMapFrame.png").OfType<Sprite>().First();
        MultiplayerUIArt.ChatField=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/GraphHUD/GraphStatusFrame.png").OfType<Sprite>().First();
    }
}
