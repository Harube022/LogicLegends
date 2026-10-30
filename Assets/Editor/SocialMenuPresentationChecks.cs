using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Local layout checks; no sign-in, Photon rooms or messages are created.</summary>
[InitializeOnLoad]
public static class SocialMenuPresentationChecks
{
    private const string Request = "Temp/SocialMenuPresentationChecks.request";
    private const string Result = "Temp/SocialMenuPresentationChecks.result";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static SocialMenuPresentationChecks() { EditorApplication.update += Tick; }
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Fields).GetValue(owner);
    private static void Check(bool condition, string message, List<string> checks)
    { if (!condition) throw new InvalidOperationException(message); checks.Add("PASS " + message); }

    private static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string mode = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            if (mode == "delivery") VerifyDelivery();
            else if (mode == "removal") VerifyRemoval();
            else if (mode == "notifications") VerifyNotifications();
            else Verify();
        }
        catch (Exception error) { File.WriteAllText(Result, "FAIL\n" + error); Debug.LogException(error); }
    }

    private static void VerifyNotifications()
    {
        var method = typeof(FriendsRepository).GetMethod("CountUnread", BindingFlags.NonPublic | BindingFlags.Static);
        int Unread(IEnumerable<FriendsRepository.Message> messages, string seen)
            => (int)method.Invoke(null, new object[] { messages, "friend", seen });
        var old = new FriendsRepository.Message { Id = "001", Sender = "friend", Text = "Earlier message" };
        var own = new FriendsRepository.Message { Id = "002", Sender = "me", Text = "My reply" };
        var invite = new FriendsRepository.Message { Id = "003", Sender = "friend", Text = "Join my room", InviteCode = "AB3D7K" };
        var incoming = new FriendsRepository.Message { Id = "004", Sender = "friend", Text = "New message" };
        var checks = new List<string>();
        Check(Unread(new[] { old, own, invite }, "001") == 1, "Incoming invites count as unread; outgoing messages do not", checks);
        Check(Unread(new[] { old, own, invite, incoming }, "001") == 2, "A new message updates the unread total from the latest snapshot", checks);
        Check(Unread(new[] { old, own, invite, incoming }, "004") == 0, "Opening the conversation clears messages through its read marker", checks);
        Check(Unread(new[] { old, own, invite, incoming }, "") == 3, "An unread conversation counts all incoming messages", checks);
        Check(Unread(Array.Empty<FriendsRepository.Message>(), "004") == 0, "An empty conversation has no unread messages", checks);
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Local notification check", typeof(RectTransform));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
            var owner = root.AddComponent<FriendsUI>();
            foreach (string field in new[] { "mainBadge", "lobbyBadge", "requestsBadge", "chatsBadge" })
            {
                var badge = StageUITheme.Rect(field, root.transform);
                var label = StageUITheme.Text("Count", badge, "", 25, false);
                typeof(FriendsUI).GetField(field, Fields).SetValue(owner, label);
            }
            var counts = new FriendsRepository.Notifications { Requests = 2 };
            counts.UnreadByFriend["friend"] = 3;
            typeof(FriendsUI).GetField("notifications", Fields).SetValue(owner, counts);
            typeof(FriendsUI).GetMethod("ShowNotifications", Fields).Invoke(owner, null);
            Check(Get<TMP_Text>(owner, "requestsBadge").text == "2" && Get<TMP_Text>(owner, "chatsBadge").text == "3" &&
                Get<TMP_Text>(owner, "mainBadge").text == "5" && Get<TMP_Text>(owner, "lobbyBadge").text == "5",
                "Requests, Chats and both Social entry badges receive the updated counts", checks);
            counts.Requests = 0; counts.UnreadByFriend.Clear();
            typeof(FriendsUI).GetMethod("ShowNotifications", Fields).Invoke(owner, null);
            Check(new[] { "mainBadge", "lobbyBadge", "requestsBadge", "chatsBadge" }.All(field =>
                !Get<TMP_Text>(owner, field).transform.parent.gameObject.activeSelf), "All badges hide when nothing is unread", checks);
            checks.Add("COMPLETE: local notification checks; no live messages or requests sent.");
            File.WriteAllLines(Result, checks);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void VerifyRemoval()
    {
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Local removal confirmation check", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
            ((RectTransform)root.transform).sizeDelta = new Vector2(1920, 1080);
            var owner = root.AddComponent<FriendsUI>();
            var overlay = MultiplayerUIArt.Rect("Social", root.transform).gameObject;
            var surface = MultiplayerUIArt.Rect("Content", overlay.transform);
            var image = surface.gameObject.AddComponent<Image>();
            image.sprite = StageUITheme.Paper;
            var pane = MultiplayerUIArt.Rect("ListPane", surface).gameObject;
            typeof(FriendsUI).GetField("overlay", Fields).SetValue(owner, overlay);
            typeof(FriendsUI).GetField("listPane", Fields).SetValue(owner, pane);
            typeof(FriendsUI).GetMethod("EnsureRemoveConfirmation", Fields).Invoke(owner, null);
            var modal = Get<GameObject>(owner, "removeConfirmation");
            var buttons = modal.GetComponentsInChildren<Button>(true);
            var cancel = buttons.Single(b => b.name == "CancelRemove");
            var remove = buttons.Single(b => b.name == "ConfirmRemove");
            var checks = new List<string>();
            Check(!modal.activeSelf && modal.GetComponent<Image>().raycastTarget,
                "Confirmation starts hidden and blocks clicks behind it when open", checks);
            Check(cancel.GetComponentInChildren<TMP_Text>().text == "CANCEL" &&
                cancel.GetComponent<Image>().sprite == Resources.LoadAll<Sprite>("StageUI/WoodButtonGreen")[0] &&
                remove.GetComponentInChildren<TMP_Text>().text == "REMOVE" &&
                remove.GetComponent<Image>().sprite == Resources.LoadAll<Sprite>("StageUI/WoodButtonRed")[0],
                "Cancel is green and Remove is red", checks);
            Canvas.ForceUpdateCanvases();
            Check(!Overlaps((RectTransform)cancel.transform, (RectTransform)remove.transform),
                "Confirmation buttons do not overlap", checks);
            typeof(FriendsUI).GetField("friendToRemove", Fields).SetValue(owner,
                new FriendsRepository.Profile { Id = "local-test", Name = "Test Player" });
            typeof(FriendsUI).GetField("removalAccount", Fields).SetValue(owner, "local-account");
            modal.SetActive(true);
            cancel.onClick.Invoke();
            Check(!modal.activeSelf && Get<FriendsRepository.Profile>(owner, "friendToRemove") == null &&
                Get<string>(owner, "removalAccount") == null,
                "Cancel closes the dialog and clears the selected friend without a database call", checks);
            remove.onClick.Invoke();
            Check(!Get<bool>(owner, "removingFriend"),
                "A confirmation after cancellation cannot submit a removal", checks);
            checks.Add("COMPLETE: local confirmation checks; no live friendship or messages changed.");
            File.WriteAllLines(Result, checks);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void VerifyDelivery()
    {
        var merge = typeof(FriendsUI).GetMethod("MergeMessages", BindingFlags.NonPublic | BindingFlags.Static);
        List<FriendsRepository.Message> Merge(IEnumerable<FriendsRepository.Message> snapshot, IEnumerable<FriendsRepository.Message> local)
            => (List<FriendsRepository.Message>)merge.Invoke(null, new object[] { snapshot, local });
        var old = new FriendsRepository.Message { Id = "001", Sender = "friend", Text = "Earlier message" };
        var sent = new FriendsRepository.Message { Id = "002", Sender = "me", Text = "New message" };
        var invite = new FriendsRepository.Message { Id = "003", Sender = "me", Text = "Join my multiplayer room.", InviteCode = "AB3D7K" };
        var local = new[] { sent, invite };
        var checks = new List<string>();
        var stale = Merge(new[] { old }, local);
        Check(stale.Select(m => m.Id).SequenceEqual(new[] { "001", "002", "003" }),
            "A stale snapshot retains the newly confirmed text and invite immediately", checks);
        var echo = Merge(new[] { old, sent, invite }, local);
        Check(echo.Count == 3 && echo.Last().InviteCode == "AB3D7K", "Firebase echoes do not duplicate the text or invite", checks);
        Check(Merge(Array.Empty<FriendsRepository.Message>(), Array.Empty<FriendsRepository.Message>()).Count == 0,
            "A fresh conversation does not retain another chat's messages", checks);
        var history = Enumerable.Range(0, 65).Select(i => new FriendsRepository.Message { Id = i.ToString("D3") });
        var latest = Merge(history, Array.Empty<FriendsRepository.Message>());
        Check(latest.Count == 60 && latest.First().Id == "005" && latest.Last().Id == "064", "History keeps the latest 60 messages in order", checks);
        checks.Add("COMPLETE: local delivery regression checks; no live messages sent.");
        File.WriteAllLines(Result, checks);
    }

    [MenuItem("Logic Legends/Friends/Verify Social Lobby And Settings Presentation")]
    public static void Verify()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Main Menu.unity");
        if (!scene.isLoaded) throw new InvalidOperationException("Open Main Menu first.");
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        var social = all.Select(t => t.GetComponent<FriendsUI>()).First(c => c != null);
        var lobby = all.Select(t => t.GetComponent<MultiplayerLobbyUI>()).First(c => c != null);
        var checks = new List<string>();
        Canvas.ForceUpdateCanvases();
        var red = Resources.LoadAll<Sprite>("StageUI/WoodButtonRed")[0];
        foreach (string field in new[] { "close", "backToList" })
        {
            var button = Get<Button>(social, field);
            Check(button.GetComponentInChildren<TMP_Text>(true).text == "X" && button.GetComponent<Image>().sprite == red &&
                ((RectTransform)button.transform).sizeDelta == new Vector2(80, 80), "Social " + field + " is a red 80 x 80 X", checks);
        }
        foreach (string field in new[] { "addTab", "friendsTab", "requestsTab", "chatsTab" })
            Check(((RectTransform)Get<Button>(social, field).transform).sizeDelta == new Vector2(220, 88), field + " retains compact dimensions", checks);
        var ready = (RectTransform)Get<Button>(lobby, "ready").transform;
        var start = (RectTransform)Get<Button>(lobby, "start").transform;
        var leave = (RectTransform)Get<Button>(lobby, "leave").transform;
        var roomChat = all.Single(t => t.name == "RoomChat") as RectTransform;
        var roster = all.Single(t => t.name == "PlayerRoster") as RectTransform;
        Check(!Overlaps(ready, start) && !Overlaps(ready, leave) && !Overlaps(start, leave) &&
            !Overlaps(leave, roomChat) && !Overlaps(start, roomChat), "Lobby actions and chat do not overlap", checks);
        Check(ready.anchorMin.y == start.anchorMin.y && leave.anchorMin.y < ready.anchorMin.y,
            "READY and START share a row with LEAVE beneath", checks);
        Check(Get<TMP_Text>(lobby, "count").transform.parent == roster && roster.anchorMax.y > 1,
            "Roster and count occupy the upper-left area", checks);
        var room = roomChat.GetComponentInChildren<RoomChatPanel>(true);
        Check(room.input.transform.parent == room.send.transform.parent && !room.scroll.transform.IsChildOf(room.input.transform.parent),
            "Lobby composer moves independently of the chat scroll", checks);
        Check(Get<TMP_InputField>(social, "messageInput").transform.parent == Get<Button>(social, "send").transform.parent,
            "Social SEND stays grouped with its input", checks);
        var settings = all.Single(t => t.name == "Settings_Menu");
        var settingsUI = settings.Find("MainMenuSettingsUI");
        var settingsFrame = (RectTransform)settingsUI.Find("SettingsFrame");
        var settingsClose = settingsUI.Find("CloseSettings").GetComponent<Button>();
        Check(settings.parent.GetComponent<Canvas>() != null && Mathf.Approximately((settingsFrame.anchorMin.x + settingsFrame.anchorMax.x) * .5f, .5f),
            "Settings is centered directly on the Canvas", checks);
        Check(!settings.Find("back_btn").gameObject.activeSelf && settingsClose.GetComponent<Image>().sprite == red &&
            settingsClose.onClick.GetPersistentMethodName(0) == "ShowMainMenu" && PrefabUtility.IsPartOfPrefabInstance(settingsUI.gameObject),
            "Settings uses a connected red X and retains its prefab link", checks);

        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Local message layout check", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
            ((RectTransform)root.transform).sizeDelta = new Vector2(1000, 600);
            var owner = root.AddComponent<FriendsUI>();
            var content = StageUITheme.Rect("Messages", root.transform);
            content.sizeDelta = new Vector2(1000, 600);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            typeof(FriendsUI).GetField("messageContent", Fields).SetValue(owner, content);
            var card = (RectTransform)typeof(FriendsUI).GetMethod("MessageCard", Fields).Invoke(owner,
                new object[] { "A longer local preview message to check wrapping, sender padding and literal <b>markup</b>.", "YOU", true });
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var sender = card.Find("Sender").GetComponent<TMP_Text>();
            var body = card.Find("Message").GetComponent<TMP_Text>();
            Check(card.GetComponent<Image>().sprite == null && card.GetComponent<Image>().color == MultiplayerUIArt.MessageColor(true),
                "Direct messages use the party-chat color without a frame sprite", checks);
            Check(!Overlaps(sender.rectTransform, body.rectTransform) && !body.richText && sender.color.g > sender.color.r,
                "Sender and message have separate padded rows with green YOU and literal text", checks);
            var action = (Button)typeof(FriendsUI).GetMethod("RowButton", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                new object[] { card, "CHAT", -12f, (UnityEngine.Events.UnityAction)(() => { }), false });
            Check(action.GetComponent<Image>().sprite == Resources.LoadAll<Sprite>("StageUI/WoodButtonGreen")[0], "CHAT actions use the green button artwork", checks);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        checks.Add("COMPLETE " + checks.Count + " local presentation checks. Native Android keyboard not exercised.");
        File.WriteAllLines(Result, checks);
    }

    private static bool Overlaps(RectTransform a, RectTransform b)
    {
        var first = Bounds(a); var second = Bounds(b);
        return first.xMin < second.xMax - .5f && first.xMax > second.xMin + .5f &&
            first.yMin < second.yMax - .5f && first.yMax > second.yMin + .5f;
    }
    private static Rect Bounds(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners.Min(c => c.x), corners.Min(c => c.y), corners.Max(c => c.x), corners.Max(c => c.y));
    }
}
