using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors the centered GraphHUD username prompt in the saved Main Menu scene.</summary>
public static class UsernameSetupBuilder
{
    [MenuItem("Logic Legends/Accounts/Build In-Game Username Prompt")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before building the username prompt.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Open Main Menu before building the username prompt.");
        MultiplayerUISetup.ConfigureArtwork();
        var canvas = scene.GetRootGameObjects().Select(root => root.GetComponent<Canvas>())
            .FirstOrDefault(component => component != null);
        var auth = scene.GetRootGameObjects().Select(root => root.GetComponent<AuthManager>())
            .FirstOrDefault(component => component != null);
        if (canvas == null || auth == null) throw new InvalidOperationException("Main Menu canvas or AuthManager is missing.");

        var old = canvas.transform.Find("UsernameSetup");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var overlay = MultiplayerUIArt.Rect("UsernameSetup", canvas.transform);
        var dim = overlay.gameObject.AddComponent<Image>();
        dim.color = new Color(.015f, .025f, .04f, .72f);
        var card = MultiplayerUIArt.GraphCard("UsernameCard", overlay, .22f, .20f, .78f, .80f);
        MultiplayerUIArt.Label("Eyebrow", card, "MAKE YOUR MARK", .08f, .83f, .92f, .92f,
            22, false, StageUITheme.ReadingInk);
        MultiplayerUIArt.Label("Title", card, "CHOOSE YOUR NAME", .08f, .68f, .92f, .82f,
            42, false, StageUITheme.ReadingInk);
        MultiplayerUIArt.Label("Description", card, "You can change your username later",
            .08f, .59f, .92f, .66f, 25, true, StageUITheme.ReadingInk);
        MultiplayerUIArt.Label("Requirements", card,
            "3–20 characters: letters (Aa), numbers, or _.\nNo spaces or other symbols.",
            .08f, .46f, .92f, .57f, 21, true, StageUITheme.ReadingInk);
        var input = MultiplayerUIArt.Input("Username", card, "Username", .12f, .33f, .88f, .44f,
            32, 64);
        var message = MultiplayerUIArt.Label("Message", card, "Enter a username to continue.",
            .10f, .24f, .90f, .31f, 22, true, StageUITheme.ReadingInk);
        var submit = MultiplayerUIArt.Action("Continue", card, "CONTINUE", .5f, .13f, .5f, .13f, true);
        MultiplayerUIArt.StandardAction(submit);

        var ui = auth.GetComponent<UsernameSetupUI>();
        if (ui == null) ui = auth.gameObject.AddComponent<UsernameSetupUI>();
        ui.BindForEditor(overlay.gameObject, input, message, submit, auth);
        var serialized = new SerializedObject(auth);
        serialized.FindProperty("usernameSetup").objectReferenceValue = ui;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        overlay.gameObject.SetActive(false);
        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(auth);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Main Menu username prompt saved after character selection.");
    }
}
