using System;
using System.Collections;
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
            if (mode == "fade") VerifyFade();
            else if (mode == "retention") VerifyRetention();
            else if (mode == "delivery") VerifyDelivery();
            else if (mode == "removal") VerifyRemoval();
            else if (mode == "notifications") VerifyNotifications();
            else Verify();
        }
        catch (Exception error) { File.WriteAllText(Result, "FAIL\n" + error); Debug.LogException(error); }
    }

    private static void VerifyFade()
    {
        var preview = EditorSceneManager.NewPreviewScene();
        GameObject root = null;
        IEnumerator routine = null;
        var checks = new List<string>();
        EditorApplication.CallbackFunction step = null;
        void Finish(Exception error)
        {
            EditorApplication.update -= step;
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            File.WriteAllLines(Result, error == null ? checks : new List<string> { "FAIL", error.ToString() });
        }
        IEnumerator Run()
        {
            root = new GameObject("Local Social fade check", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
            ((RectTransform)root.transform).sizeDelta = new Vector2(1920, 1080);
            var owner = root.AddComponent<FriendsUI>();
            var overlay = MultiplayerUIArt.Rect("Social", root.transform).gameObject;
            var surface = MultiplayerUIArt.Rect("Content", overlay.transform);
            var list = MultiplayerUIArt.Rect("ListPane", surface).gameObject;
            var chat = MultiplayerUIArt.Rect("ChatPane", surface).gameObject;
            var rows = MultiplayerUIArt.Rect("Rows", list.transform);
            var messages = MultiplayerUIArt.Rect("Messages", chat.transform);
            foreach (var pair in new[] { ("overlay", overlay), ("listPane", list), ("chatPane", chat) })
                typeof(FriendsUI).GetField(pair.Item1, Fields).SetValue(owner, pair.Item2);
            typeof(FriendsUI).GetField("listContent", Fields).SetValue(owner, rows);
            typeof(FriendsUI).GetField("messageContent", Fields).SetValue(owner, messages);
            typeof(FriendsUI).GetMethod("BeginContentLoad", Fields).Invoke(owner, new object[] { "Loading friends..." });
            var presentation = Get<MenuDataFeedback>(owner, "presentation");
            presentation.StopAllCoroutines();
            var listGroup = list.GetComponent<CanvasGroup>();
            var chatGroup = chat.GetComponent<CanvasGroup>();
            Check(listGroup != null && chatGroup != null && listGroup.alpha == 1 && chatGroup.alpha == 1 &&
                listGroup.interactable && chatGroup.interactable,
                "Social panes stay fully visible and interactive while data is pending", checks);
            var loadingLabel = Get<TMP_Text>(presentation, "message");
            Check(!Get<bool>(presentation, "automaticLoadingFeedback") && loadingLabel.text == string.Empty,
                "Ordinary tab loads do not schedule or show a Loading label", checks);
            var row = (RectTransform)typeof(FriendsUI).GetMethod("Row", Fields).Invoke(owner,
                new object[] { rows, "Local friend", 122f, .46f });
            Check(row.GetComponentInParent<CanvasGroup>() == listGroup,
                "Dynamically loaded friend rows inherit the actual fade group", checks);
            var rowButtonMethod = typeof(FriendsUI).GetMethod("RowButton", BindingFlags.NonPublic | BindingFlags.Static);
            var actions = new[] {
                (Button)rowButtonMethod.Invoke(null, new object[] { row, "CHAT", -252f, (UnityEngine.Events.UnityAction)(() => { }), false }),
                (Button)rowButtonMethod.Invoke(null, new object[] { row, "REMOVE", -12f, (UnityEngine.Events.UnityAction)(() => { }), true })
            };
            foreach (var button in actions) button.targetGraphic.CrossFadeColor(button.colors.disabledColor, 0f, true, true);
            typeof(MenuDataFeedback).GetMethod("SetContent", Fields).Invoke(presentation, new object[] { 0f, false });
            Check(actions.All(button => button.GetComponent<CanvasGroup>().ignoreParentGroups &&
                button.GetComponent<CanvasGroup>().alpha == 1f && button.targetGraphic.canvasRenderer.GetColor() == Color.white &&
                button.IsInteractable()),
                "CHAT and REMOVE stay opaque, untinted and usable while content loads", checks);
            var group = Get<CanvasGroup>(presentation, "feedbackGroup");
            var label = Get<TMP_Text>(presentation, "message");
            presentation.ShowMessage("Loading friends...");
            typeof(FriendsUI).GetMethod("RevealContent", Fields).Invoke(owner, new object[] { 0 });
            presentation.StopAllCoroutines();
            Check(group.alpha == 0 && label.text == string.Empty && listGroup.alpha == 0,
                "Loading text clears immediately on readiness while rows remain hidden for layout", checks);
            var revealMethod = typeof(MenuDataFeedback).GetMethod("Reveal", Fields);
            int version = Get<int>(presentation, "version");
            var oldReveal = (IEnumerator)revealMethod.Invoke(presentation, new object[] { version });
            oldReveal.MoveNext(); // layout frame
            oldReveal.MoveNext(); // first fade frame
            typeof(FriendsUI).GetMethod("BeginContentLoad", Fields).Invoke(owner, new object[] { "Loading chats..." });
            presentation.StopAllCoroutines();
            Check(!oldReveal.MoveNext() && listGroup.alpha == 1 && listGroup.interactable,
                "Switching tabs cancels the prior fade and restores full visibility without blocking input", checks);
            typeof(FriendsUI).GetMethod("RevealContent", Fields).Invoke(owner, new object[] { 0 });
            presentation.StopAllCoroutines();
            var reveal = (IEnumerator)revealMethod.Invoke(presentation, new object[] { Get<int>(presentation, "version") });
            bool sawPartialAlpha = false;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (reveal.MoveNext())
            {
                sawPartialAlpha |= listGroup.alpha > 0 && listGroup.alpha < 1;
                CheckDuringFade(listGroup, chatGroup, label);
                if (actions.Any(button => button.GetComponent<CanvasGroup>().alpha != 1f ||
                    button.targetGraphic.canvasRenderer.GetColor() != Color.white || !button.IsInteractable()))
                    throw new InvalidOperationException("Buttons became transparent or blocked during the row fade.");
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Social fade did not finish.");
                yield return null;
            }
            Check(sawPartialAlpha && listGroup.alpha == 1 && chatGroup.alpha == 1 && listGroup.interactable &&
                chatGroup.interactable && presentation.IsRevealed,
                "Only ready content fades in while Social input remains usable", checks);
            Check(actions.All(button => button.GetComponent<CanvasGroup>().alpha == 1f && button.IsInteractable() &&
                button.targetGraphic.canvasRenderer.GetColor() == Color.white),
                "Buttons remain fully visible and usable after loading finishes", checks);
            Check(surface.GetComponentsInChildren<MenuDataFeedback>(true).Length == 1 && list.GetComponents<CanvasGroup>().Length == 1 &&
                chat.GetComponents<CanvasGroup>().Length == 1,
                "Repeated tab loads reuse one presenter and one fade group per pane", checks);
            var other = MultiplayerUIArt.Rect("Other menu", root.transform);
            var otherGroup = other.gameObject.AddComponent<CanvasGroup>();
            var otherLabel = StageUITheme.Text("Feedback", other, "", 30);
            var otherFeedback = otherLabel.gameObject.AddComponent<CanvasGroup>();
            var otherPresentation = other.gameObject.AddComponent<MenuDataFeedback>();
            otherPresentation.Configure(new[] { otherGroup }, otherFeedback, otherLabel);
            Check(Get<float>(otherPresentation, "minimumLoadingDisplay") == .35f &&
                Get<float>(presentation, "minimumLoadingDisplay") == 0f && Get<bool>(otherPresentation, "automaticLoadingFeedback") &&
                Get<bool>(otherPresentation, "blockInputWhileLoading"),
                "Only Social skips routine loading text and input gates; other menus retain their timing", checks);
            var needsFeedback = typeof(FriendsUI).GetMethod("NeedsConnectionFeedback", BindingFlags.Static | BindingFlags.NonPublic);
            bool Needs(float elapsed, bool offline, bool? connected) => (bool)needsFeedback.Invoke(null, new object[] { elapsed, offline, connected });
            Check(!Needs(10f, false, true) && !Needs(.3f, false, false) && !Needs(10f, false, null) &&
                Needs(2f, false, false) && Needs(0f, true, true),
                "Feedback appears only for unavailable internet or sustained Firebase disconnection, not ordinary tab loads", checks);
            checks.Add("COMPLETE: local native CanvasGroup and fade lifecycle checks; no accounts accessed or scene layout saved.");
        }
        void CheckDuringFade(CanvasGroup list, CanvasGroup chat, TMP_Text label)
        {
            if (label.text.Length != 0 || !list.interactable || !chat.interactable)
                throw new InvalidOperationException("Routine loading text appeared or input was blocked during the fade.");
        }
        routine = Run();
        step = () =>
        {
            try { if (!routine.MoveNext()) Finish(null); }
            catch (Exception error) { Finish(error); }
        };
        EditorApplication.update += step;
    }

    private static void VerifyRetention()
    {
        const long now = 1800000000000, lifetime = 24L * 60 * 60 * 1000;
        var retained = typeof(FriendsRepository).GetMethod("RetainedMessages", BindingFlags.NonPublic | BindingFlags.Static);
        var unread = typeof(FriendsRepository).GetMethod("CountUnread", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { typeof(IEnumerable<FriendsRepository.Message>), typeof(string), typeof(string), typeof(long) }, null);
        var messages = new[] {
            new FriendsRepository.Message { Id = "001", Sender = "friend", SentAt = now - lifetime - 1 },
            new FriendsRepository.Message { Id = "002", Sender = "friend", SentAt = now - lifetime, InviteCode = "AB3D7K" },
            new FriendsRepository.Message { Id = "003", Sender = "friend", SentAt = now - lifetime + 1 },
            new FriendsRepository.Message { Id = "004", Sender = "me", SentAt = now },
            new FriendsRepository.Message { Id = "005", Sender = "friend", SentAt = 0 }
        };
        var checks = new List<string>();
        var visible = (List<FriendsRepository.Message>)retained.Invoke(null, new object[] { messages, now });
        Check(visible.Select(m => m.Id).SequenceEqual(new[] { "003", "004" }),
            "Expired messages, boundary-age invites and invalid timestamps are hidden", checks);
        Check((int)unread.Invoke(null, new object[] { messages, "friend", "", now }) == 1,
            "Expired messages and outgoing sends do not count as unread", checks);
        Check(((List<FriendsRepository.Message>)retained.Invoke(null, new object[] { messages, now + lifetime })).Count == 0,
            "Advancing time expires existing history without a Firebase snapshot", checks);
        VerifyDelivery();
        checks.Add("PASS Confirmed-send merge and latest-60 delivery regression checks");
        VerifyNotifications();
        checks.Add("PASS Existing notification count and badge regression checks");
        checks.Add("COMPLETE: local retention checks; no live data changed.");
        File.WriteAllLines(Result, checks);
    }

    private static void VerifyNotifications()
    {
        var method = typeof(FriendsRepository).GetMethod("CountUnread", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(IEnumerable<FriendsRepository.Message>), typeof(string), typeof(string) }, null);
        int Unread(IEnumerable<FriendsRepository.Message> messages, string seen)
            => (int)method.Invoke(null, new object[] { messages, "friend", seen });
        var old = new FriendsRepository.Message { Id = "001", Sender = "friend", Text = "Earlier message", SentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        var own = new FriendsRepository.Message { Id = "002", Sender = "me", Text = "My reply", SentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        var invite = new FriendsRepository.Message { Id = "003", Sender = "friend", Text = "Join my room", InviteCode = "AB3D7K", SentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        var incoming = new FriendsRepository.Message { Id = "004", Sender = "friend", Text = "New message", SentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
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
        var old = new FriendsRepository.Message { Id = "001", Sender = "friend", Text = "Earlier message", SentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        var sent = new FriendsRepository.Message { Id = "002", Sender = "me", Text = "New message", SentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        var invite = new FriendsRepository.Message { Id = "003", Sender = "me", Text = "Join my multiplayer room.", InviteCode = "AB3D7K", SentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        var local = new[] { sent, invite };
        var checks = new List<string>();
        var stale = Merge(new[] { old }, local);
        Check(stale.Select(m => m.Id).SequenceEqual(new[] { "001", "002", "003" }),
            "A stale snapshot retains the newly confirmed text and invite immediately", checks);
        var echo = Merge(new[] { old, sent, invite }, local);
        Check(echo.Count == 3 && echo.Last().InviteCode == "AB3D7K", "Firebase echoes do not duplicate the text or invite", checks);
        Check(Merge(Array.Empty<FriendsRepository.Message>(), Array.Empty<FriendsRepository.Message>()).Count == 0,
            "A fresh conversation does not retain another chat's messages", checks);
        var history = Enumerable.Range(0, 65).Select(i => new FriendsRepository.Message { Id = i.ToString("D3"), SentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
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
