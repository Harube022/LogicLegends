using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One Friends panel shared by the Main Menu and the pre-game multiplayer lobby.</summary>
public sealed class FriendsUI : MonoBehaviour
{
    private enum Section { Add, Friends, Requests, Chats }

    [SerializeField] private MultiplayerMenuManager multiplayerMenu;
    [SerializeField] private MultiplayerLobbyUI lobby;
    [SerializeField] private GameObject mainMenuPanel, overlay, listPane, chatPane, searchBar;
    [SerializeField] private Button mainButton, lobbyButton, close, copyId, search, send, invite, backToList;
    [SerializeField] private Button addTab, friendsTab, requestsTab, chatsTab;
    [SerializeField] private TMP_InputField idInput, messageInput;
    [SerializeField] private TMP_Text status, myId, chatTitle, emptyState;
    [SerializeField] private RectTransform listContent, messageContent;
    [SerializeField] private RectTransform peopleScroll;
    [SerializeField] private ScrollRect messageScroll;
    [SerializeField] private Sprite rowArt;
    [SerializeField] private TMP_Text mainBadge, lobbyBadge, requestsBadge, chatsBadge;

    private Section section = Section.Friends;
    private FriendsRepository.Profile conversation;
    private int viewVersion;
    private bool sending, accessDenied;
    private float pollAt;
    private string renderedConversationId, renderedLastMessageId;
    private int renderedMessageCount = -1;
    private string notificationAccount;
    private FriendsRepository.Notifications notifications;
    private IDisposable notificationSubscription;
    private float retryNotificationsAt;
    private int notificationVersion;
    private IDisposable messageSubscription;
    private List<FriendsRepository.Message> latestMessages = new List<FriendsRepository.Message>();
    private readonly Dictionary<string, FriendsRepository.Message> confirmedSends = new Dictionary<string, FriendsRepository.Message>();
    private GameObject removeConfirmation;
    private TMP_Text removeConfirmationText;
    private FriendsRepository.Profile friendToRemove;
    private string removalAccount;
    private bool removingFriend;
    private MenuDataFeedback presentation;
    private bool awaitingContent;

    private void Awake()
    {
        mainButton.onClick.AddListener(Open);
        lobbyButton.onClick.AddListener(Open);
        close.onClick.AddListener(Close);
        copyId.onClick.AddListener(() => { if (FriendsRepository.MyId != null) { GUIUtility.systemCopyBuffer = FriendsRepository.MyId; ShowStatus("Player ID copied."); } });
        search.onClick.AddListener(Search);
        idInput.onSubmit.AddListener(_ => Search());
        send.onClick.AddListener(Send);
        messageInput.onSubmit.AddListener(_ => Send());
        invite.onClick.AddListener(Invite);
        backToList.onClick.AddListener(() => Select(Section.Chats));
        addTab.onClick.AddListener(() => Select(Section.Add));
        friendsTab.onClick.AddListener(() => Select(Section.Friends));
        requestsTab.onClick.AddListener(() => Select(Section.Requests));
        chatsTab.onClick.AddListener(() => Select(Section.Chats));
        overlay.SetActive(false);
    }

    private void Update()
    {
        bool home = mainMenuPanel != null && mainMenuPanel.activeInHierarchy;
        if (mainButton.gameObject.activeSelf != home) mainButton.gameObject.SetActive(home);
        string account = FriendsRepository.MyId;
        if (notificationAccount != account)
        {
            StopNotifications();
            if (notificationAccount != null && overlay.activeSelf) Close();
            CancelRemove();
            notificationAccount = account;
            notifications = null;
            ShowNotifications();
            retryNotificationsAt = 0;
        }
        bool watchNotifications = account != null && (home || lobbyButton.gameObject.activeInHierarchy || overlay.activeSelf);
        if (!watchNotifications && notificationSubscription != null) StopNotifications();
        if (watchNotifications && notificationSubscription == null && Time.unscaledTime >= retryNotificationsAt) StartNotifications();
        if (!overlay.activeSelf) return;
        if (account == null) { Close(); return; }
        invite.gameObject.SetActive(conversation != null && CanInvite());
        if (accessDenied || awaitingContent || Time.unscaledTime < pollAt) return;
        pollAt = Time.unscaledTime + 8f;
        if (conversation == null && section != Section.Add) Reload();
    }

    public void Open()
    {
        accessDenied = false;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        myId.text = FriendsRepository.MyId == null ? "Sign in to use Social" : "YOUR PLAYER ID    " + FriendsRepository.MyId;
        Select(Section.Friends);
        retryNotificationsAt = 0;
    }

    public void Close()
    {
        CancelRemove();
        awaitingContent = false;
        viewVersion++;
        StopMessages();
        conversation = null;
        overlay.SetActive(false);
    }

    private void Select(Section selected)
    {
        CancelRemove();
        StopMessages();
        accessDenied = false;
        section = selected;
        conversation = null;
        chatPane.SetActive(false);
        listPane.SetActive(true);
        searchBar.SetActive(selected == Section.Add);
        if (peopleScroll != null) peopleScroll.anchorMax = new Vector2(.955f, selected == Section.Add ? .77f : .94f);
        var activeArt = Resources.LoadAll<Sprite>("StageUI/WoodButtonGreen");
        var tabs = new[] { addTab, friendsTab, requestsTab, chatsTab };
        for (int i = 0; i < tabs.Length; i++)
            tabs[i].GetComponent<Image>().sprite = i == (int)selected && activeArt.Length > 0
                ? activeArt[0] : StageUITheme.ButtonArt;
        ShowStatus(FriendsRepository.MyId == null ? "Sign in to use Social." : string.Empty);
        Reload(true);
    }

    private void BeginContentLoad(string message)
    {
        if (presentation == null)
        {
            // CanvasGroups only affect visibility/input; authored anchors and button sizes stay untouched.
            var surface = listPane.transform.parent;
            var listGroup = listPane.GetComponent<CanvasGroup>() ?? listPane.AddComponent<CanvasGroup>();
            var chatGroup = chatPane.GetComponent<CanvasGroup>() ?? chatPane.AddComponent<CanvasGroup>();
            var loading = MultiplayerUIArt.Label("SocialLoading", surface, "", .10f, .38f, .90f, .62f,
                30, true, StageUITheme.ReadingInk);
            loading.raycastTarget = false;
            var feedback = loading.gameObject.AddComponent<CanvasGroup>();
            presentation = surface.gameObject.AddComponent<MenuDataFeedback>();
            presentation.Configure(new[] { listGroup, chatGroup }, feedback, loading);
        }
        awaitingContent = true;
        pollAt = Time.unscaledTime + 8f;
        presentation.BeginLoad(message);
    }

    private void RevealContent(int version)
    {
        if (!Current(version) || !awaitingContent) return;
        awaitingContent = false;
        pollAt = Time.unscaledTime + 8f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(conversation == null ? listContent : messageContent);
        Canvas.ForceUpdateCanvases();
        presentation.RevealWhenReady();
    }

    private static string ReadKey(string account, string other) => "Social.LastRead." + account + "." + other;
    private string LastRead(string other) => PlayerPrefs.GetString(ReadKey(notificationAccount, other), string.Empty);

    private void MarkRead(string other, string lastId)
    {
        if (string.IsNullOrEmpty(notificationAccount) || string.IsNullOrEmpty(lastId)) return;
        if (LastRead(other) == lastId) return;
        PlayerPrefs.SetString(ReadKey(notificationAccount, other), lastId);
        PlayerPrefs.Save();
        if (notifications != null && notifications.UnreadByFriend.Remove(other)) ShowNotifications();
    }

    private void StartNotifications()
    {
        string account = notificationAccount;
        if (string.IsNullOrEmpty(account) || notificationSubscription != null) return;
        int version = ++notificationVersion;
        retryNotificationsAt = Time.unscaledTime + 15f;
        try
        {
            var subscription = FriendsRepository.WatchNotifications(
                other => PlayerPrefs.GetString(ReadKey(account, other), string.Empty), fresh =>
                {
                    if (this == null || notificationVersion != version || FriendsRepository.MyId != account) return;
                    if (conversation != null) fresh.UnreadByFriend.Remove(conversation.Id);
                    notifications = fresh;
                    ShowNotifications();
                }, error =>
                {
                    if (this == null || notificationVersion != version || FriendsRepository.MyId != account) return;
                    StopNotifications();
                    retryNotificationsAt = Time.unscaledTime + 15f;
                    Debug.LogWarning("Social notifications: " + error);
                });
            if (this != null && notificationVersion == version && FriendsRepository.MyId == account)
                notificationSubscription = subscription;
            else subscription.Dispose();
        }
        catch (Exception error) { Debug.LogWarning("Social notifications: " + error); }
    }

    private void StopNotifications()
    {
        notificationVersion++;
        notificationSubscription?.Dispose();
        notificationSubscription = null;
    }

    private void ShowNotifications()
    {
        int requests = notifications?.Requests ?? 0;
        int chats = notifications?.Chats ?? 0;
        Badge(requestsBadge, requests);
        Badge(chatsBadge, chats);
        Badge(mainBadge, requests + chats);
        Badge(lobbyBadge, requests + chats);
    }

    private static void Badge(TMP_Text label, int count)
    {
        if (label == null) return;
        label.text = count > 99 ? "99+" : count.ToString();
        label.transform.parent.gameObject.SetActive(count > 0);
    }

    private async void Reload(bool transition = false)
    {
        if (conversation != null) return;
        // A completed friend action may refresh a newly selected tab while its first load is pending.
        transition |= awaitingContent;
        int version = ++viewVersion;
        if (transition)
        {
            BeginContentLoad(section == Section.Requests ? "Loading requests..." : section == Section.Chats
                ? "Loading chats..." : section == Section.Add ? "Loading search..." : "Loading friends...");
            Clear(listContent);
        }
        Empty(string.Empty);
        try
        {
            if (FriendsRepository.MyId == null) { Clear(listContent); Empty("Sign in to use Social."); return; }
            if (section == Section.Add) { Clear(listContent); Empty("Enter a Player ID above to find someone."); return; }
            List<FriendsRepository.Profile> people = section == Section.Requests
                ? await FriendsRepository.LoadRequestsAsync() : await FriendsRepository.LoadFriendsAsync();
            if (!Current(version)) return;
            Clear(listContent);
            if (people.Count == 0) { Empty(section == Section.Requests ? "No pending friend requests." : section == Section.Chats ? "No conversations yet. Add a friend to start chatting." : "No friends yet. Search by Player ID to add someone."); return; }
            foreach (var person in people)
            {
                var row = Row(listContent, person.Name + "\nID  " + ShortId(person.Id), 122);
                if (section == Section.Requests)
                {
                    RowButton(row, "ACCEPT", -252, () => Accept(person));
                    RowButton(row, "DECLINE", -12, () => Decline(person));
                }
                else if (section == Section.Chats) RowButton(row, "CHAT", -12, () => OpenChat(person));
                else
                {
                    RowButton(row, "CHAT", -252, () => OpenChat(person));
                    RowButton(row, "REMOVE", -12, () => Remove(person), true);
                }
            }
        }
        catch (Exception error) { if (Current(version)) Failure("Couldn't load Social. Check the connection and try again.", error); }
        finally { if (transition) RevealContent(version); }
    }

    private async void Search()
    {
        if (awaitingContent || section != Section.Add || conversation != null) return;
        int version = ++viewVersion;
        string id = idInput.text.Trim();
        Clear(listContent);
        Empty(string.Empty);
        if (!FriendsRepository.ValidId(id)) { ShowStatus("Enter a valid Player ID."); return; }
        if (id == FriendsRepository.MyId) { ShowStatus("This is your own Player ID."); return; }
        BeginContentLoad("Searching...");
        ShowStatus(string.Empty);
        try
        {
            var person = await FriendsRepository.FindProfileAsync(id);
            if (!Current(version)) return;
            if (person == null) { Empty("No player with that ID has a username yet."); ShowStatus("Player not found."); return; }
            bool isFriend = await FriendsRepository.IsFriendAsync(id);
            bool pending = !isFriend && await FriendsRepository.RequestPendingAsync(id);
            if (!Current(version)) return;
            var row = Row(listContent, person.Name + "\nID  " + ShortId(id), 122);
            if (isFriend) RowButton(row, "CHAT", -12, () => OpenChat(person));
            else if (!pending) RowButton(row, "ADD", -12, () => Add(person));
            ShowStatus(isFriend ? "Already friends." : pending ? "Friend request already sent." : "Send a friend request?");
        }
        catch (Exception error) { if (Current(version)) Failure("Couldn't search for that player. Try again.", error); }
        finally { RevealContent(version); }
    }

    private async void Add(FriendsRepository.Profile person)
    {
        if (await Act(() => FriendsRepository.SendRequestAsync(person.Id), "Friend request sent.") && overlay.activeSelf && section == Section.Add) Search();
    }
    private async void Accept(FriendsRepository.Profile person)
    {
        if (await Act(() => FriendsRepository.AcceptRequestAsync(person.Id), "Friend added.") && overlay.activeSelf) Reload();
    }
    private async void Decline(FriendsRepository.Profile person)
    {
        if (await Act(() => FriendsRepository.DeclineRequestAsync(person.Id), "Request declined.") && overlay.activeSelf) Reload();
    }
    private void Remove(FriendsRepository.Profile person)
    {
        if (removingFriend) return;
        EnsureRemoveConfirmation();
        friendToRemove = person;
        removalAccount = FriendsRepository.MyId;
        removeConfirmationText.text = "Remove " + person.Name + " from your friends list?";
        removeConfirmation.SetActive(true);
        removeConfirmation.transform.SetAsLastSibling();
    }

    private void EnsureRemoveConfirmation()
    {
        if (removeConfirmation != null) return;
        removeConfirmation = MultiplayerUIArt.Rect("RemoveFriendConfirmation", overlay.transform).gameObject;
        var dim = removeConfirmation.AddComponent<Image>();
        dim.color = new Color(.01f, .02f, .03f, .72f);
        var card = MultiplayerUIArt.Rect("ConfirmationCard", removeConfirmation.transform, .5f, .5f, .5f, .5f);
        card.sizeDelta = new Vector2(820, 400);
        // Reuse the serialized Social surface so the same artwork is available in Android builds.
        var source = listPane.transform.parent.GetComponent<Image>();
        var surface = card.gameObject.AddComponent<Image>();
        surface.sprite = source.sprite; surface.type = source.type;
        surface.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        MultiplayerUIArt.Label("Title", card, "REMOVE FRIEND?", .08f, .72f, .92f, .92f, 42, false, StageUITheme.ReadingInk);
        removeConfirmationText = MultiplayerUIArt.Label("Message", card, "", .10f, .40f, .90f, .70f, 28, true, StageUITheme.ReadingInk);
        var cancel = MultiplayerUIArt.Action("CancelRemove", card, "CANCEL", .30f, .20f, .30f, .20f, green: true);
        var confirm = MultiplayerUIArt.Action("ConfirmRemove", card, "REMOVE", .70f, .20f, .70f, .20f, red: true);
        MultiplayerUIArt.StandardAction(cancel);
        MultiplayerUIArt.StandardAction(confirm);
        cancel.onClick.AddListener(CancelRemove);
        confirm.onClick.AddListener(ConfirmRemove);
        removeConfirmation.SetActive(false);
    }

    private void CancelRemove()
    {
        friendToRemove = null;
        removalAccount = null;
        if (removeConfirmation != null) removeConfirmation.SetActive(false);
    }

    private async void ConfirmRemove()
    {
        var person = friendToRemove;
        string account = removalAccount;
        CancelRemove();
        if (removingFriend || person == null || account == null || account != FriendsRepository.MyId) return;
        removingFriend = true;
        try
        {
            await FriendsRepository.RemoveFriendAsync(person.Id);
            if (this == null || FriendsRepository.MyId != account) return;
            if (overlay.activeSelf)
            {
                ShowStatus("Friend removed.");
                if (conversation == null && section == Section.Friends) Reload();
            }
        }
        catch (Exception error)
        {
            if (this != null && FriendsRepository.MyId == account && overlay.activeSelf)
                Failure("Couldn't remove that friend. Try again.", error);
        }
        finally { if (this != null) removingFriend = false; }
    }

    private async Task<bool> Act(Func<Task> action, string success)
    {
        try { await action(); if (this != null) ShowStatus(success); return true; }
        catch (Exception error) { Failure("Couldn't complete that action. Try again.", error); return false; }
    }

    private void OpenChat(FriendsRepository.Profile person)
    {
        CancelRemove();
        StopMessages();
        int version = ++viewVersion;
        section = Section.Chats;
        conversation = person;
        renderedConversationId = renderedLastMessageId = null;
        renderedMessageCount = -1;
        listPane.SetActive(false);
        chatPane.SetActive(true);
        chatTitle.text = person.Name;
        messageInput.text = string.Empty;
        Clear(messageContent);
        ShowStatus(string.Empty);
        BeginContentLoad("Loading chat...");
        try
        {
            messageSubscription = FriendsRepository.WatchMessages(person.Id, messages =>
            {
                if (!Current(version) || conversation == null || conversation.Id != person.Id) return;
                latestMessages = messages;
                foreach (var message in messages) confirmedSends.Remove(message.Id);
                RenderConversation();
                RevealContent(version);
            }, error =>
            {
                if (!Current(version)) return;
                Failure("Couldn't load messages. Try again.", error);
                RevealContent(version);
            });
        }
        catch (Exception error)
        {
            if (Current(version)) Failure("Couldn't load messages. Try again.", error);
            RevealContent(version);
        }
    }

    private void StopMessages()
    {
        messageSubscription?.Dispose(); messageSubscription = null;
        latestMessages.Clear(); confirmedSends.Clear();
    }
    private void OnDisable() => StopNotifications();
    private void OnDestroy() { StopMessages(); StopNotifications(); }

    private void RecordSent(string other, FriendsRepository.Message message)
    {
        if (this == null || !overlay.activeSelf || conversation == null || conversation.Id != other || message.Sender != FriendsRepository.MyId) return;
        confirmedSends[message.Id] = message;
        RenderConversation();
    }

    private void RenderConversation()
    {
        if (conversation == null) return;
        string other = conversation.Id;
            var messages = MergeMessages(latestMessages, confirmedSends.Values);
            string lastId = messages.Count == 0 ? null : messages[messages.Count - 1].Id;
            MarkRead(other, lastId);
            if (renderedConversationId == other && renderedMessageCount == messages.Count && renderedLastMessageId == lastId) return;
            Clear(messageContent);
            if (messages.Count == 0) MessageCard("No messages yet. Say hello!", null, false);
            foreach (var message in messages)
            {
                bool mine = message.Sender == FriendsRepository.MyId;
                string label = message.Text;
                if (!string.IsNullOrEmpty(message.InviteCode)) label += "\nINVITE CODE  " + message.InviteCode;
                var row = MessageCard(label, mine ? "YOU" : conversation.Name, mine);
                if (!mine && !string.IsNullOrEmpty(message.InviteCode))
                {
                    string code = message.InviteCode;
                    var actions = StageUITheme.Rect("InviteAction", row);
                    actions.gameObject.AddComponent<LayoutElement>().preferredHeight = 88;
                    RowButton(actions, "JOIN", 0, () => UseInvite(code));
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(messageContent);
            Canvas.ForceUpdateCanvases();
            messageScroll.verticalNormalizedPosition = 0;
            renderedConversationId = other; renderedLastMessageId = lastId; renderedMessageCount = messages.Count;
    }

    internal static List<FriendsRepository.Message> MergeMessages(IEnumerable<FriendsRepository.Message> snapshot,
        IEnumerable<FriendsRepository.Message> confirmed) => snapshot.Concat(confirmed).GroupBy(m => m.Id).Select(g => g.First())
        .OrderBy(m => m.Id, StringComparer.Ordinal).TakeLast(60).ToList();

    private async void Send()
    {
        if (sending || conversation == null) return;
        string other = conversation.Id;
        string text = messageInput.text;
        if (string.IsNullOrWhiteSpace(text)) { ShowStatus("Write a message first."); return; }
        sending = true; send.interactable = false;
        try
        {
            var message = await FriendsRepository.SendMessageAsync(other, text);
            RecordSent(other, message);
            if (this != null && conversation != null && conversation.Id == other)
            { messageInput.text = string.Empty; ShowStatus("Message sent."); }
        }
        catch (Exception error) { Failure("Couldn't send the message. Try again.", error); }
        finally { if (this != null) { sending = false; send.interactable = true; } }
    }

    private bool CanInvite()
    {
        var service = MultiplayerLobbyService.Instance;
        return service != null && service.InLobby && PhotonNetwork.IsMasterClient && !service.Starting && LobbyRules.IsValidCode(PhotonNetwork.CurrentRoom?.Name);
    }

    private void Invite()
    {
        if (conversation != null) SendInvite(conversation);
    }

    private async void SendInvite(FriendsRepository.Profile person)
    {
        if (sending) return;
        if (!CanInvite()) { ShowStatus("Host a waiting room before inviting friends."); return; }
        string code = PhotonNetwork.CurrentRoom.Name;
        sending = true; invite.interactable = send.interactable = false;
        try
        {
            var message = await FriendsRepository.SendMessageAsync(person.Id, "Join my multiplayer room.", code);
            RecordSent(person.Id, message);
            if (this != null) ShowStatus("Invite sent to " + person.Name + ".");
        }
        catch (Exception error) { if (this != null) Failure("Couldn't send the invite. Try again.", error); }
        finally { if (this != null) { sending = false; invite.interactable = send.interactable = true; } }
    }

    private void UseInvite(string code)
    {
        if (!LobbyRules.IsValidCode(code)) { ShowStatus("Invalid code."); return; }
        if (MultiplayerLobbyService.Instance != null && MultiplayerLobbyService.Instance.InLobby)
        { ShowStatus("Leave your current lobby before joining another."); return; }
        Close();
        multiplayerMenu.OnClickMultiplayerButton();
        lobby.ShowJoin();
        lobby.InviteInput.text = code;
        MultiplayerLobbyService.Instance.Notify("Invite code ready. Tap JOIN LOBBY.");
    }

    private bool Current(int version) => this != null && overlay.activeSelf && version == viewVersion;
    private void ShowStatus(string value) { if (status != null) status.text = value; }
    private void Failure(string value, Exception error)
    {
        string reason = error.GetBaseException().Message;
        Debug.LogWarning("Friends: " + error);
        if (this == null) return;
        if (reason.IndexOf("permission", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            accessDenied = true;
            Empty("Social is unavailable until account access is enabled.");
            ShowStatus("Social is unavailable right now. Please try again later.");
        }
        else ShowStatus(value);
    }
    private static string ShortId(string id) => id.Length <= 18 ? id : id.Substring(0, 8) + "…" + id.Substring(id.Length - 6);

    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }
    private void Empty(string value)
    {
        if (emptyState == null) return;
        emptyState.text = value;
        emptyState.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }
    private void AddNotice(string value, RectTransform parent = null) => Row(parent != null ? parent : listContent, value, 122);

    private RectTransform MessageCard(string body, string sender, bool mine)
    {
        var card = StageUITheme.Rect("DirectMessageCard", messageContent);
        var image = card.gameObject.AddComponent<Image>();
        image.color = MultiplayerUIArt.MessageColor(mine, sender == null);
        image.raycastTarget = false;
        var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 14); layout.spacing = 4;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        if (sender != null)
        {
            var name = StageUITheme.Text("Sender", card, sender, 27);
            name.richText = false;
            name.color = mine ? new Color(.08f, .45f, .20f) : StageUITheme.ReadingInk;
            name.alignment = mine ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
        }
        var text = StageUITheme.Text("Message", card, body, 27, true);
        text.richText = false; text.color = StageUITheme.ReadingInk;
        text.alignment = sender == null ? TextAlignmentOptions.Center : mine ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
        return card;
    }

    private RectTransform Row(Transform parent, string value, float height, float textRight = .46f)
    {
        var rect = StageUITheme.Rect("FriendRow", parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = rowArt != null ? rowArt : StageUITheme.Paper;
        image.type = Image.Type.Sliced;
        if (image.sprite != null)
            image.pixelsPerUnitMultiplier = Mathf.Min(image.sprite.rect.width, image.sprite.rect.height) * .01f;
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        image.raycastTarget = false;
        var label = MultiplayerUIArt.Label("RowText", rect, value, .045f, .08f, textRight, .92f, 27, true, StageUITheme.ReadingInk);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return rect;
    }

    private static Button RowButton(Transform row, string caption, float right, UnityEngine.Events.UnityAction action, bool red = false)
    {
        var button = MultiplayerUIArt.Action(caption + "Button", row, caption, 1, .5f, 1, .5f, green: caption == "CHAT", red: red);
        var rect = (RectTransform)button.transform;
        rect.pivot = new Vector2(1, .5f); rect.sizeDelta = new Vector2(220, 88);
        rect.anchoredPosition = new Vector2(right, 0);
        button.GetComponent<Image>().pixelsPerUnitMultiplier = 4.5f;
        var label = button.GetComponentInChildren<TMP_Text>();
        label.fontSizeMin = 22; label.fontSizeMax = 28;
        button.onClick.AddListener(action);
        return button;
    }

#if UNITY_EDITOR
    public bool HasInterface => overlay != null && mainButton != null && lobbyButton != null;
    private Vector2 savedLobbyPosition, savedLobbySize, savedLobbyMin, savedLobbyMax, savedLobbyPivot;
    private Vector3 savedLobbyScale;

    public void PreserveLobbyEntry(Transform canvas)
    {
        if (lobbyButton == null) return;
        var rect = (RectTransform)lobbyButton.transform;
        savedLobbyPosition = rect.anchoredPosition; savedLobbySize = rect.sizeDelta;
        savedLobbyMin = rect.anchorMin; savedLobbyMax = rect.anchorMax;
        savedLobbyPivot = rect.pivot; savedLobbyScale = rect.localScale;
        rect.SetParent(canvas, false);
    }

    public void BindLobby(MultiplayerMenuManager multiplayer, MultiplayerLobbyUI newLobby, Transform canvas)
    {
        multiplayerMenu = multiplayer; lobby = newLobby;
        var rect = (RectTransform)lobbyButton.transform;
        rect.SetParent(canvas.Find("PrivateMultiplayerLobby/SafeArea/WoodFrame/Interior"), false);
        rect.anchorMin = savedLobbyMin; rect.anchorMax = savedLobbyMax; rect.pivot = savedLobbyPivot;
        rect.anchoredPosition = savedLobbyPosition; rect.sizeDelta = savedLobbySize; rect.localScale = savedLobbyScale;
        newLobby.BindSocialEntry(lobbyButton);
    }

    public void UpdatePresentation()
    {
        // Update the requested controls in place so authored tab/button settings survive.
        MultiplayerUIArt.StyleCloseAction(close);
        StageUITheme.Stretch((RectTransform)close.transform, new Vector2(.95f,.94f), new Vector2(.95f,.94f));
        ((RectTransform)close.transform).sizeDelta = new Vector2(80,80);
        MultiplayerUIArt.StyleCloseAction(backToList);
        StageUITheme.Stretch((RectTransform)backToList.transform, new Vector2(.09f,.89f), new Vector2(.09f,.89f));
        ((RectTransform)backToList.transform).sizeDelta = new Vector2(80,80);
    }

    private static ScrollRect Scroll(string name, Transform parent, out RectTransform content, float bottom = .06f, float top = .79f)
    {
        var root = MultiplayerUIArt.Rect(name, parent, .045f, bottom, .955f, top);
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
        var viewport = MultiplayerUIArt.Rect("Viewport", root);
        viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, .001f);
        content = MultiplayerUIArt.Rect("Rows", viewport, 0, 1, 1, 1);
        content.pivot = new Vector2(.5f, 1);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 9; layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content;
        return scroll;
    }

    public static FriendsUI Build(MainMenuManager menu, MultiplayerMenuManager multiplayer, MultiplayerLobbyUI lobby)
    {
        var controller = menu.GetComponent<FriendsUI>();
        if (controller == null) controller = menu.gameObject.AddComponent<FriendsUI>();
        controller.multiplayerMenu = multiplayer; controller.lobby = lobby;
        controller.rowArt = MultiplayerUIArt.ChatField;
        var menuData = new UnityEditor.SerializedObject(menu);
        controller.mainMenuPanel = (GameObject)menuData.FindProperty("mainMenuPanel").objectReferenceValue;
        var launcher = UnityEngine.Object.FindFirstObjectByType<MainMenuProfileLauncher>(FindObjectsInactive.Include);
        var profileData = new UnityEditor.SerializedObject(launcher);
        var profileButton = (GameObject)profileData.FindProperty("profileButtonObject").objectReferenceValue;
        Transform profileParent = profileButton.transform.parent;
        var previous = profileParent.Find("FriendsMainButton"); if (previous != null) DestroyImmediate(previous.gameObject);
        controller.mainButton = MultiplayerUIArt.Action("FriendsMainButton", profileParent, "SOCIAL", 0, 1, 0, 1);
        var mainRect = (RectTransform)controller.mainButton.transform;
        mainRect.sizeDelta = new Vector2(220, 88); mainRect.anchoredPosition = new Vector2(305, -100);
        controller.mainButton.GetComponent<Image>().pixelsPerUnitMultiplier = 4.5f;
        controller.mainBadge = NotificationBadge(controller.mainButton);
        controller.mainButton.gameObject.SetActive(false);

        Transform canvas = controller.mainMenuPanel.GetComponentInParent<Canvas>().transform;
        Transform lobbyInside = canvas.Find("PrivateMultiplayerLobby/SafeArea/WoodFrame/Interior");
        if (lobbyInside == null) throw new InvalidOperationException("Build the multiplayer lobby before Friends.");
        previous = lobbyInside.Find("FriendsLobbyButton"); if (previous != null) DestroyImmediate(previous.gameObject);
        controller.lobbyButton = MultiplayerUIArt.Action("FriendsLobbyButton", lobbyInside, "SOCIAL", 1, 1, 1, 1);
        var lobbyRect = (RectTransform)controller.lobbyButton.transform;
        lobbyRect.pivot = new Vector2(1, 1); lobbyRect.sizeDelta = new Vector2(220, 88); lobbyRect.anchoredPosition = new Vector2(-5, -5);
        controller.lobbyButton.GetComponent<Image>().pixelsPerUnitMultiplier = 4.5f;
        controller.lobbyBadge = NotificationBadge(controller.lobbyButton);
        lobby.BindSocialEntry(controller.lobbyButton);

        previous = canvas.Find("FriendsOverlay"); if (previous != null) DestroyImmediate(previous.gameObject);
        controller.overlay = MultiplayerUIArt.Rect("FriendsOverlay", canvas).gameObject;
        var dim = controller.overlay.AddComponent<Image>(); dim.color = new Color(.01f, .02f, .035f, .72f);
        var safe = MultiplayerUIArt.Rect("SafeArea", controller.overlay.transform); safe.gameObject.AddComponent<Crystal.SafeArea>();
        var frame = MultiplayerUIArt.Rect("FriendsFrame", safe, .07f, .045f, .93f, .955f);
        var wood = frame.gameObject.AddComponent<Image>(); wood.sprite = StageUITheme.Panel; wood.type = Image.Type.Sliced; wood.pixelsPerUnitMultiplier = 1.7f;
        var inside = MultiplayerUIArt.Rect("Interior", frame, .06f, .07f, .94f, .93f);
        MultiplayerUIArt.Label("Title", inside, "SOCIAL", .28f, .89f, .72f, .99f, 52, false, StageUITheme.Ink);
        controller.close = MultiplayerUIArt.CloseAction("CloseFriends", inside, .95f, .94f);
        var idCard = MultiplayerUIArt.Rect("PlayerIdCard", inside, .045f, .80f, .78f, .88f);
        var idArt = idCard.gameObject.AddComponent<Image>(); idArt.sprite = MultiplayerUIArt.ChatField;
        idArt.type = Image.Type.Sliced;
        idArt.pixelsPerUnitMultiplier = Mathf.Min(idArt.sprite.rect.width, idArt.sprite.rect.height) * .01f;
        idArt.raycastTarget = false;
        controller.myId = MultiplayerUIArt.Label("MyId", idCard, "YOUR PLAYER ID", .04f, .10f, .96f, .90f, 27, true, StageUITheme.ReadingInk);
        controller.myId.alignment = TextAlignmentOptions.MidlineLeft;
        controller.copyId = HeaderButton("CopyPlayerId", inside, "COPY ID", .875f, .84f, 220);
        controller.addTab = TabButton("AddFriends", inside, "ADD", .16f);
        controller.friendsTab = TabButton("FriendList", inside, "FRIENDS", .385f);
        controller.requestsTab = TabButton("FriendRequests", inside, "REQUESTS", .615f);
        controller.chatsTab = TabButton("FriendChats", inside, "CHATS", .84f);
        controller.requestsBadge = NotificationBadge(controller.requestsTab);
        controller.chatsBadge = NotificationBadge(controller.chatsTab);
        var surface = MultiplayerUIArt.GraphCard("FriendsContent", inside, .045f, .105f, .955f, .66f);
        // Extend the cream content down into the wood frame's unused lower area.
        surface.offsetMin = new Vector2(0, -82);
        controller.listPane = MultiplayerUIArt.Rect("ListPane", surface).gameObject;
        controller.searchBar = MultiplayerUIArt.Rect("SearchBar", controller.listPane.transform, .055f, .80f, .945f, .95f).gameObject;
        controller.idInput = MultiplayerUIArt.Input("PlayerId", controller.searchBar.transform, "Enter Player ID", 0, 0, .72f, 1, 28, 128);
        controller.search = HeaderButton("SearchPlayer", controller.searchBar.transform, "SEARCH", .86f, .50f, 220);
        controller.peopleScroll = Scroll("PeopleScroll", controller.listPane.transform, out controller.listContent, .08f, .77f).GetComponent<RectTransform>();
        controller.emptyState = MultiplayerUIArt.Label("EmptyState", controller.listPane.transform, "", .10f, .28f, .90f, .65f, 32, true, StageUITheme.ReadingInk);
        controller.emptyState.raycastTarget = false;
        controller.chatPane = MultiplayerUIArt.Rect("ChatPane", surface).gameObject;
        controller.chatTitle = MultiplayerUIArt.Label("ChatTitle", controller.chatPane.transform, "CHAT", .30f, .82f, .70f, .96f, 36, false, StageUITheme.ReadingInk);
        controller.backToList = MultiplayerUIArt.CloseAction("BackToChats", controller.chatPane.transform, .09f, .89f);
        controller.messageScroll = Scroll("MessagesScroll", controller.chatPane.transform, out controller.messageContent, .26f, .79f);
        var composer = MultiplayerUIArt.Rect("SocialChatComposer", controller.chatPane.transform);
        controller.messageInput = MultiplayerUIArt.Input("DirectMessage", composer, "Enter Message...", .05f, .07f, .75f, .21f, 28, FriendsRepository.MessageLimit);
        controller.send = HeaderButton("SendDirectMessage", composer, "SEND", .86f, .14f, 220);
        controller.invite = HeaderButton("InviteFriend", controller.chatPane.transform, "SEND INVITE", .85f, .89f, 280);
        controller.status = MultiplayerUIArt.Label("FriendsStatus", surface, "", .055f, .005f, .945f, .07f, 24, true, StageUITheme.ReadingInk);
        controller.status.alignment = TextAlignmentOptions.MidlineLeft;
        controller.searchBar.SetActive(false); controller.chatPane.SetActive(false); controller.emptyState.gameObject.SetActive(false); controller.overlay.SetActive(false);
        return controller;
    }

    private static Button HeaderButton(string name, Transform parent, string caption, float x, float y, float width)
    {
        var button = MultiplayerUIArt.Action(name, parent, caption, x, y, x, y);
        var rect = (RectTransform)button.transform;
        rect.sizeDelta = new Vector2(width, 88);
        button.GetComponent<Image>().pixelsPerUnitMultiplier = 4.5f;
        var label = button.GetComponentInChildren<TMP_Text>();
        label.fontSizeMin = 22; label.fontSizeMax = 30;
        return button;
    }

    private static Button TabButton(string name, Transform parent, string caption, float x)
    {
        var button = MultiplayerUIArt.Action(name, parent, caption, x, .735f, x, .735f);
        var rect = (RectTransform)button.transform;
        rect.sizeDelta = new Vector2(220, 88);
        button.GetComponent<Image>().pixelsPerUnitMultiplier = 4.5f;
        var label = button.GetComponentInChildren<TMP_Text>();
        label.fontSizeMin = 22; label.fontSizeMax = 30;
        return button;
    }

    private static TMP_Text NotificationBadge(Button button)
    {
        var badge = MultiplayerUIArt.Rect("NotificationBadge", button.transform, 1, 1, 1, 1);
        badge.sizeDelta = new Vector2(50, 50);
        badge.anchoredPosition = new Vector2(-3, 3);
        var image = badge.gameObject.AddComponent<Image>();
        image.sprite = Resources.LoadAll<Sprite>("StageUI/UnreadNotification")[0];
        image.color = new Color(.82f, .16f, .14f, 1f);
        image.raycastTarget = false;
        var label = MultiplayerUIArt.Label("Count", badge, "", 0, 0, 1, 1, 25, false, new Color(1f, .96f, .86f, 1f));
        label.raycastTarget = false;
        badge.gameObject.SetActive(false);
        return label;
    }
#endif
}
