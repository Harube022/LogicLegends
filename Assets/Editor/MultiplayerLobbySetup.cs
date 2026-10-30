using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

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
        var canvas = play.GetComponentInParent<Canvas>().transform;
        var friends = menu.GetComponent<FriendsUI>();
        bool preserveSocial = friends != null && friends.HasInterface;
        if (preserveSocial) friends.PreserveLobbyEntry(canvas);
        ui.Build(canvas, menu, play);
        network.LobbyUI = ui;
        if (preserveSocial) friends.BindLobby(network, ui, canvas);
        else friends = FriendsUI.Build(menu, network, ui);
        ValidateWaitingLayout(ui);
        EditorUtility.SetDirty(network); EditorUtility.SetDirty(ui); EditorUtility.SetDirty(friends);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Private lobby UI saved. Play > Multiplayer > Host Game / Join Game.");
    }

    private static void ValidateWaitingLayout(MultiplayerLobbyUI ui)
    {
        var data = new SerializedObject(ui);
        var waiting = (GameObject)data.FindProperty("waitingPanel").objectReferenceValue;
        var inside = (RectTransform)waiting.transform.parent;
        RectTransform Field(string name) => ((Component)data.FindProperty(name).objectReferenceValue).transform as RectTransform;
        var status = Field("status"); var ready = Field("ready"); var start = Field("start");
        var leave = Field("leave"); var social = Field("socialEntry");
        var chat = (RectTransform)waiting.transform.Find("RoomChat");
        var invite = (RectTransform)waiting.transform.Find("InviteLabel");
        var code = Field("code"); var copy = Field("copy");
        var heading = Field("heading"); var roster = (RectTransform)waiting.transform.Find("PlayerRoster");
        var overlay = (GameObject)data.FindProperty("overlay").objectReferenceValue;
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            ui.ApplyViewLayout(true);
            // Validate the authored landscape canvas without changing the user's Game view.
            var reference = new GameObject("Lobby reference layout", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(reference, preview);
            reference.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)reference.transform).sizeDelta = new Vector2(1920,1080);
            var clone = Object.Instantiate(overlay, reference.transform, false);
            clone.SetActive(true);
            foreach (var safe in clone.GetComponentsInChildren<Crystal.SafeArea>(true))
            {
                safe.enabled = false;
                StageUITheme.Stretch((RectTransform)safe.transform, Vector2.zero, Vector2.one);
            }
            RectTransform Copy(RectTransform rect) => (RectTransform)clone.transform.Find(AnimationUtility.CalculateTransformPath(rect, overlay.transform));
            Copy((RectTransform)waiting.transform).gameObject.SetActive(true);
            inside = Copy(inside); status = Copy(status); ready = Copy(ready); start = Copy(start);
            leave = Copy(leave); social = Copy(social); chat = Copy(chat); invite = Copy(invite);
            code = Copy(code); copy = Copy(copy); heading = Copy(heading); roster = Copy(roster);
            Canvas.ForceUpdateCanvases();
            foreach (var rect in new[] { status, ready, start, leave, social, chat, invite, code, copy, heading, roster })
            {
                var bounds = Bounds(rect, inside);
                if (bounds.width <= 0 || bounds.height <= 0 || bounds.xMin < inside.rect.xMin - .5f ||
                    bounds.xMax > inside.rect.xMax + .5f || bounds.yMin < inside.rect.yMin - .5f || bounds.yMax > inside.rect.yMax + .5f)
                    throw new InvalidOperationException(rect.name + " crosses the lobby interior: " + bounds + " inside " + inside.rect);
            }
            NoOverlap(ready, start, inside); NoOverlap(ready, social, inside); NoOverlap(start, social, inside);
            NoOverlap(chat, status, inside); NoOverlap(status, leave, inside); NoOverlap(chat, leave, inside);
            NoOverlap(social, leave, inside); NoOverlap(social, status, inside); NoOverlap(social, chat, inside);
            NoOverlap(heading, roster, inside); NoOverlap(ready, roster, inside); NoOverlap(start, roster, inside);
            NoOverlap(invite, code, inside); NoOverlap(code, copy, inside); NoOverlap(copy, chat, inside);
            if (!heading.gameObject.activeSelf || !Field("eyebrow").gameObject.activeSelf || Bounds(heading, inside).yMin <= Bounds(roster, inside).yMax)
                throw new InvalidOperationException("Both headings must be visible above the roster.");
            var rosterBounds = Bounds(roster, inside); var chatBounds = Bounds(chat, inside);
            if (Mathf.Abs(rosterBounds.yMin-chatBounds.yMin)>.5f || Mathf.Abs(rosterBounds.yMax-chatBounds.yMax)>.5f)
                throw new InvalidOperationException("Roster and chat edges must align.");
            float buttonY = Bounds(ready, inside).center.y;
            if (new[] { start, social, leave }.Any(rect => Mathf.Abs(Bounds(rect, inside).center.y-buttonY)>.5f))
                throw new InvalidOperationException("All four footer buttons must align.");
            if (Bounds(status, inside).yMax >= Bounds(chat, inside).yMin || Bounds(leave, inside).yMax >= Bounds(status, inside).yMin)
                throw new InvalidOperationException("The footer must place status beneath chat and LEAVE beneath status.");
            File.WriteAllText("Temp/MultiplayerWaitingLayout.result", "PASS (1920 x 1080 authored reference)\nHeadings visible above the aligned roster/chat panels.\nREADY, START, SOCIAL and LEAVE share one row without overlap.");
        }
        finally { ui.ApplyViewLayout(false); EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static Rect Bounds(RectTransform rect, RectTransform relative)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector2 min = Vector2.one * float.PositiveInfinity, max = Vector2.one * float.NegativeInfinity;
        foreach (var corner in corners)
        { var point = (Vector2)relative.InverseTransformPoint(corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static void NoOverlap(RectTransform a, RectTransform b, RectTransform relative)
    {
        var first = Bounds(a, relative); var second = Bounds(b, relative);
        if (first.xMin < second.xMax - .5f && first.xMax > second.xMin + .5f && first.yMin < second.yMax - .5f && first.yMax > second.yMin + .5f)
            throw new InvalidOperationException(a.name + " overlaps " + b.name + ".");
    }
}
