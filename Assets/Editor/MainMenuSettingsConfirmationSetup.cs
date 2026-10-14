using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuSettingsConfirmationSetup
{
    [MenuItem("Logic Legends/Main Menu/Add Settings Confirmations")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Open Main Menu outside Play Mode first.");
        var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var settings = transforms.Single(t => t.name == "Settings_Menu");
        if (settings.GetComponent<MainMenuSettingsConfirmation>() != null)
            throw new InvalidOperationException("Settings confirmations already exist; edit the existing dialog.");
        var logout = settings.Find("LogOut_btn").GetComponent<Button>();
        var quit = settings.Find("exit_btn").GetComponent<Button>();
        var auth = transforms.Select(t => t.GetComponent<AuthManager>()).Single(c => c != null);
        var menu = transforms.Select(t => t.GetComponent<MainMenuManager>()).Single(c => c != null);
        var canvas = settings.GetComponentInParent<Canvas>().transform;
        var controller = settings.gameObject.AddComponent<MainMenuSettingsConfirmation>();
        var overlay = Rect("SettingsConfirmation", canvas, Vector2.zero, Vector2.one);
        var dimmer = overlay.gameObject.AddComponent<Image>();
        dimmer.color = new Color(.015f, .025f, .04f, .80f);
        dimmer.raycastTarget = true;
        var frame = Rect("ConfirmationCard", overlay, Vector2.one * .5f, Vector2.one * .5f);
        frame.sizeDelta = new Vector2(960, 520);
        Surface(frame, "Assets/Art/GraphHUD/GraphMapFrame.png", 6.5f);
        var plaque = Rect("HeadingPlaque", frame, new Vector2(.20f,.72f), new Vector2(.80f,.92f));
        Surface(plaque, "Assets/Art/GraphHUD/GraphStatusFrame.png", 5f);
        var heading = Label("Heading", plaque, "LOG OUT?", Vector2.one * .08f, Vector2.one * .92f, 44);
        heading.characterSpacing = 2;
        var message = Label("Message", frame, "Are you sure you want to log out?\nYou will need to sign in again.",
            new Vector2(.08f,.36f), new Vector2(.92f,.68f), 30);
        message.lineSpacing = 14;
        var cancel = Action("CancelButton", frame, "CANCEL", new Vector2(-160,-155), true);
        var confirm = Action("ConfirmButton", frame, "LOG OUT", new Vector2(160,-155), false);
        var data = new SerializedObject(controller);
        Bind(data,"overlay",overlay.gameObject); Bind(data,"card",frame);
        Bind(data,"heading",heading); Bind(data,"message",message);
        Bind(data,"confirmLabel",confirm.GetComponentInChildren<TMP_Text>());
        Bind(data,"cancelButton",cancel); Bind(data,"logoutButton",logout); Bind(data,"quitButton",quit);
        data.ApplyModifiedPropertiesWithoutUndo();
        // Move the original actions to the confirmed events; other scenes keep their own bindings.
        SetConfirmedAction(controller,"onLogoutConfirmed",auth,"OnClickLogout");
        SetConfirmedAction(controller,"onQuitConfirmed",menu,"QuitGame");
        RemoveAction(logout,auth,"OnClickLogout");
        RemoveAction(quit,menu,"QuitGame");
        UnityEventTools.AddPersistentListener(logout.onClick,controller.RequestLogout);
        UnityEventTools.AddPersistentListener(quit.onClick,controller.RequestQuit);
        UnityEventTools.AddPersistentListener(cancel.onClick,controller.Cancel);
        UnityEventTools.AddPersistentListener(confirm.onClick,controller.Confirm);
        overlay.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetConfirmedAction(MainMenuSettingsConfirmation controller,string field,UnityEngine.Object target,string method)
    {
        var data = new SerializedObject(controller);
        var calls = data.FindProperty(field).FindPropertyRelative("m_PersistentCalls.m_Calls");
        calls.arraySize = 1;
        var call = calls.GetArrayElementAtIndex(0);
        call.FindPropertyRelative("m_Target").objectReferenceValue = target;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
        call.FindPropertyRelative("m_MethodName").stringValue = method;
        call.FindPropertyRelative("m_Mode").enumValueIndex = (int)UnityEngine.Events.PersistentListenerMode.Void;
        call.FindPropertyRelative("m_CallState").enumValueIndex = (int)UnityEngine.Events.UnityEventCallState.RuntimeOnly;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RemoveAction(Button button,UnityEngine.Object target,string method)
    {
        for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)
            if(button.onClick.GetPersistentTarget(i)==target && button.onClick.GetPersistentMethodName(i)==method)
                UnityEventTools.RemovePersistentListener(button.onClick,i);
    }

    private static void Bind(SerializedObject data,string field,UnityEngine.Object value) => data.FindProperty(field).objectReferenceValue=value;
    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var rect=StageUITheme.Rect(name,parent); StageUITheme.Stretch(rect,min,max); return rect;
    }
    private static void Surface(RectTransform rect,string path,float multiplier)
    {
        var image=rect.gameObject.AddComponent<Image>();
        image.sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderByDescending(s=>s.rect.width*s.rect.height).First();
        image.type=Image.Type.Sliced; image.pixelsPerUnitMultiplier=multiplier; image.raycastTarget=false;
    }
    private static TMP_Text Label(string name,Transform parent,string value,Vector2 min,Vector2 max,float size)
    {
        var text=StageUITheme.Text(name,parent,value,size,false);
        StageUITheme.Stretch(text.rectTransform,min,max);
        text.color=StageUITheme.ReadingInk; text.richText=false; text.enableAutoSizing=false;
        text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false;
        return text;
    }
    private static Button Action(string name,Transform parent,string title,Vector2 position,bool green)
    {
        var button=StageUITheme.Button(name,parent,title,null);
        var rect=(RectTransform)button.transform; rect.anchorMin=rect.anchorMax=Vector2.one*.5f;
        rect.sizeDelta=new Vector2(280,88); rect.anchoredPosition=position;
        var image=button.GetComponent<Image>();
        image.sprite=Resources.LoadAll<Sprite>(green?"StageUI/WoodButtonGreen":"StageUI/WoodButtonRed")[0];
        image.type=Image.Type.Sliced; image.pixelsPerUnitMultiplier=4.5f;
        var label=button.GetComponentInChildren<TMP_Text>();
        label.font=StageUITheme.HeadingFont; label.fontSize=28; label.enableAutoSizing=false;
        label.color=new Color(1,.95f,.83f); label.richText=false;
        label.rectTransform.offsetMin=new Vector2(24,10); label.rectTransform.offsetMax=new Vector2(-24,-10);
        button.navigation=new Navigation { mode=Navigation.Mode.None };
        return button;
    }
}
