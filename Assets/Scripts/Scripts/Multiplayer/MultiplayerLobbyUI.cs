using System;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Scene-authored lobby using the shared painted wood and GraphHUD artwork.</summary>
public sealed class MultiplayerLobbyUI : MonoBehaviour
{
    [SerializeField] private GameObject overlay, choicePanel, joinPanel, waitingPanel, playPanel;
    [SerializeField] private MainMenuManager mainMenu;
    [SerializeField] private TMP_Text status, code, count, startHint;
    [SerializeField] private TMP_Text eyebrow, heading;
    [SerializeField] private Button socialEntry;
    [SerializeField] private GameObject leaveConfirmation;
    [SerializeField] private Button confirmLeave, cancelLeave;
    [SerializeField] private TMP_Text[] slots;
    [SerializeField] private TMP_InputField inviteInput;
    [SerializeField] private Button host, joinChoice, join, copy, ready, start, leave, back, cancelJoin;
    private MultiplayerLobbyService service;
    private GameInput blockedInput;
    private bool wasBlocked, ownsBlock;
    private bool choosingJoin;
    public bool IsOpen => overlay != null && overlay.activeSelf;
    public TMP_InputField InviteInput => inviteInput;

    public void BindSocialEntry(Button button)
    {
        socialEntry = button;
        ApplyViewLayout(waitingPanel.activeSelf);
    }

    public void ApplyViewLayout(bool inRoom)
    {
        // The waiting-room title sits above its roster; Host / Join keeps the centered title.
        if (eyebrow != null)
        {
            eyebrow.gameObject.SetActive(true);
            eyebrow.rectTransform.anchorMin = new Vector2(0,.955f);
            eyebrow.rectTransform.anchorMax = inRoom ? new Vector2(.36f,1) : Vector2.one;
        }
        if (heading != null)
        {
            heading.gameObject.SetActive(true);
            heading.rectTransform.anchorMin = inRoom ? new Vector2(0,.86f) : new Vector2(0,.85f);
            heading.rectTransform.anchorMax = inRoom ? new Vector2(.36f,.955f) : new Vector2(1,.955f);
        }
        status.rectTransform.anchorMin = inRoom ? new Vector2(.39f,.155f) : new Vector2(.04f,.14f);
        status.rectTransform.anchorMax = inRoom ? new Vector2(1,.225f) : new Vector2(.96f,.20f);
        status.fontSizeMin = inRoom ? 20 : 22;
        status.fontSizeMax = inRoom ? 22 : 24;
        if (socialEntry == null) return;
        var rect = (RectTransform)socialEntry.transform;
        rect.anchorMin = rect.anchorMax = inRoom ? new Vector2(.49f,.082f) : Vector2.one;
        rect.pivot = inRoom ? new Vector2(.5f,.5f) : Vector2.one;
        rect.anchoredPosition = inRoom ? Vector2.zero : new Vector2(-5,-5);
    }

    private void Awake()
    {
        host.onClick.AddListener(Host);
        joinChoice.onClick.AddListener(ShowJoin);
        join.onClick.AddListener(Join);
        inviteInput.onSubmit.AddListener(SubmitJoin);
        copy.onClick.AddListener(CopyCode);
        ready.onClick.AddListener(ToggleReady);
        start.onClick.AddListener(StartGame);
        leave.onClick.AddListener(Leave);
        confirmLeave.onClick.AddListener(ConfirmLeave);
        cancelLeave.onClick.AddListener(CancelLeave);
        back.onClick.AddListener(Back);
        cancelJoin.onClick.AddListener(ShowChoices);
        overlay.SetActive(false);
    }
    private void OnDestroy()
    {
        if (service != null) service.Changed -= Refresh;
        RestoreInput();
    }
    public void Open()
    {
        if (service != null) service.Changed -= Refresh;
        service = MultiplayerLobbyService.EnsureExists();
        service.Changed += Refresh;
        choosingJoin = false;
        CancelLeave();
        playPanel.SetActive(false);
        overlay.SetActive(true); overlay.transform.SetAsLastSibling();
        if (GameInput.Instance != null && !ownsBlock)
        {
            blockedInput = GameInput.Instance; wasBlocked = blockedInput.GameplayInputBlocked;
            blockedInput.SetGameplayInputBlocked(true); ownsBlock = true;
        }
        Refresh();
    }
    private void Update()
    {
        if (!IsOpen || GameInput.Instance == null || ownsBlock) return;
        blockedInput = GameInput.Instance; wasBlocked = blockedInput.GameplayInputBlocked;
        blockedInput.SetGameplayInputBlocked(true); ownsBlock = true;
    }
    private void RestoreInput()
    {
        if (ownsBlock && blockedInput != null) blockedInput.SetGameplayInputBlocked(wasBlocked);
        ownsBlock = false;
    }
    private void Host() { service.HostGame(); }
    private void Join() { service.JoinGame(inviteInput.text); }
    private void SubmitJoin(string value) { Join(); }
    private void ToggleReady() { service.ToggleReady(); }
    private void StartGame() { service.StartGame(); }
    private void Leave()
    {
        if (service == null || !service.InLobby || service.Busy) return;
        leaveConfirmation.SetActive(true);
        leaveConfirmation.transform.SetAsLastSibling();
    }
    private void CancelLeave() { if (leaveConfirmation != null) leaveConfirmation.SetActive(false); }
    private void ConfirmLeave()
    {
        CancelLeave();
        if (service != null && service.InLobby && !service.Busy) service.LeaveLobby();
    }
    private void CopyCode()
    {
        if (!service.InLobby) return;
        GUIUtility.systemCopyBuffer = PhotonNetwork.CurrentRoom.Name;
        service.Notify("Invite code copied. Send it through Friends chat.");
    }
    public void ShowJoin() { choosingJoin = true; inviteInput.text = ""; Refresh(); inviteInput.Select(); }
    public void ShowChoices() { choosingJoin = false; Refresh(); }
    public void Back()
    {
        if (service.Busy || service.InLobby) return;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        overlay.SetActive(false); RestoreInput();
        mainMenu.ShowPlayMenu();
    }
    public void Refresh()
    {
        if (service == null || !IsOpen) return;
        bool inRoom = service.InLobby;
        choicePanel.SetActive(!inRoom && !choosingJoin);
        joinPanel.SetActive(!inRoom && choosingJoin);
        waitingPanel.SetActive(inRoom);
        status.text = service.Status;
        ApplyViewLayout(inRoom);
        host.interactable = joinChoice.interactable = join.interactable = !service.Busy;
        inviteInput.interactable = !service.Busy;
        back.gameObject.SetActive(!inRoom);
        back.interactable = cancelJoin.interactable = !service.Busy;
        if (!inRoom) { CancelLeave(); return; }
        code.text = PhotonNetwork.CurrentRoom.Name;
        var players = PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber).ToArray();
        count.text = players.Length + " / 4 PLAYERS";
        for (int i = 0; i < slots.Length; i++)
        {
            if (i >= players.Length) { slots[i].text = "OPEN SLOT\nWaiting for an adventurer..."; slots[i].color = new Color(.45f, .36f, .25f); continue; }
            var player = players[i];
            slots[i].text = LobbyRules.DisplayName(player) + "\n" + (player.IsMasterClient ? "HOST · " : "") +
                (player.IsLocal ? "YOU · " : "") + (player.IsInactive ? "RECONNECTING" : LobbyRules.IsReady(player) ? "READY" : "NOT READY");
            slots[i].color = StageUITheme.ReadingInk;
        }
        ready.interactable = !service.Starting && !service.Busy;
        ready.GetComponentInChildren<TMP_Text>().text = LobbyRules.IsReady(PhotonNetwork.LocalPlayer) ? "NOT READY" : "READY";
        start.gameObject.SetActive(PhotonNetwork.IsMasterClient);
        start.interactable = service.CanStart && !service.Busy;
        leave.interactable = !service.Busy;
        startHint.text = service.Starting ? "Starting together..." : service.WaitingForReconnect ? "Waiting for a teammate to reconnect. Their slot is reserved." : service.Players.Length < 2 ? "Invite at least one friend to start."
            : service.CanStart ? "Everyone is ready. The host can start!" : "Everyone must be ready. Only the host can start.";
    }

    public void Build(Transform canvas, MainMenuManager menu, GameObject playMenu)
    {
        mainMenu=menu;playPanel=playMenu;
        var old=canvas.Find("PrivateMultiplayerLobby");if(old!=null)DestroyImmediate(old.gameObject);
        overlay=MultiplayerUIArt.Rect("PrivateMultiplayerLobby",canvas).gameObject;
        var dim=overlay.AddComponent<Image>();dim.color=new Color(.015f,.025f,.04f,.64f);
        var safe=MultiplayerUIArt.Rect("SafeArea",overlay.transform);safe.gameObject.AddComponent<Crystal.SafeArea>();
        var frame=MultiplayerUIArt.Rect("WoodFrame",safe,.10f,.055f,.90f,.945f);
        var wood=frame.gameObject.AddComponent<Image>();wood.sprite=StageUITheme.Panel;wood.type=Image.Type.Sliced;wood.pixelsPerUnitMultiplier=1.7f;
        // All controls live inside the painted rim, including the footer/back action.
        var inside=MultiplayerUIArt.Rect("Interior",frame,.07f,.08f,.93f,.92f);
        eyebrow=MultiplayerUIArt.Label("Eyebrow",inside,"ADVENTURE TOGETHER",0,.955f,1,1,24,false,StageUITheme.Gold);
        heading=MultiplayerUIArt.Label("Heading",inside,"MULTIPLAYER",0,.85f,1,.955f,52,false,StageUITheme.Ink);
        status=MultiplayerUIArt.Label("Status",inside,"Host a private game or enter a code.",.04f,.14f,.96f,.20f,24,true,StageUITheme.Ink);
        choicePanel=MultiplayerUIArt.Rect("HostOrJoin",inside,0,.21f,1,.83f).gameObject;
        var hostCard=MultiplayerUIArt.GraphCard("HostCard",choicePanel.transform,0,.04f,.48f,1);
        MultiplayerUIArt.Label("CardEyebrow",hostCard,"CREATE A PRIVATE ROOM",.08f,.83f,.92f,.92f,22,false,StageUITheme.ReadingInk);
        MultiplayerUIArt.Label("Title",hostCard,"GATHER YOUR PARTY",.07f,.65f,.93f,.81f,38,false,StageUITheme.ReadingInk);
        MultiplayerUIArt.Label("Description",hostCard,"Start an adventure of your own.\nShare your invite code with\nup to three friends.",.09f,.36f,.91f,.60f,26,true,StageUITheme.ReadingInk);
        host=LobbyAction("HostGame",hostCard,"HOST GAME",.5f,.185f,true);
        var joinCard=MultiplayerUIArt.GraphCard("JoinCard",choicePanel.transform,.52f,.04f,1,1);
        MultiplayerUIArt.Label("CardEyebrow",joinCard,"HAVE AN INVITE CODE?",.08f,.83f,.92f,.92f,22,false,StageUITheme.ReadingInk);
        MultiplayerUIArt.Label("Title",joinCard,"JOIN AN ADVENTURE",.07f,.65f,.93f,.81f,38,false,StageUITheme.ReadingInk);
        MultiplayerUIArt.Label("Description",joinCard,"Your friends are waiting.\nEnter their six-character code\nand get ready together.",.09f,.36f,.91f,.60f,26,true,StageUITheme.ReadingInk);
        joinChoice=LobbyAction("JoinGame",joinCard,"JOIN GAME",.5f,.185f);
        joinPanel=MultiplayerUIArt.Rect("JoinByCode",inside,0,.21f,1,.83f).gameObject;
        var inviteCard=MultiplayerUIArt.GraphCard("InviteCard",joinPanel.transform,.12f,0,.88f,1);
        MultiplayerUIArt.Label("Title",inviteCard,"ENTER INVITE CODE",.08f,.72f,.92f,.88f,44,false,StageUITheme.ReadingInk);
        MultiplayerUIArt.Label("Instructions",inviteCard,"Enter the code your friend shared.",.08f,.52f,.92f,.64f,26,true,StageUITheme.ReadingInk);
        inviteInput=MultiplayerUIArt.Input("InviteCode",inviteCard,"Invite code",.13f,.31f,.87f,.48f,34,24);
        join=LobbyAction("JoinCode",inviteCard,"JOIN LOBBY",.5f,.185f,true);
        ((RectTransform)join.transform).anchoredPosition=new Vector2(-122,0);
        cancelJoin=LobbyAction("CancelJoin",inviteCard,"BACK",.5f,.185f,width:220);
        ((RectTransform)cancelJoin.transform).anchoredPosition=new Vector2(152,0);
        waitingPanel=MultiplayerUIArt.Rect("WaitingLobby",inside,0,.05f,1,.85f).gameObject;
        MultiplayerUIArt.Label("InviteLabel",waitingPanel.transform,"INVITE CODE",.40f,1.04f,.55f,1.17f,26,false,StageUITheme.Gold);
        code=MultiplayerUIArt.Label("Code",waitingPanel.transform,"------",.56f,1.04f,.73f,1.17f,44,false,StageUITheme.Ink);
        copy=LobbyAction("CopyCode",waitingPanel.transform,"COPY",.85f,1.105f,width:220);
        var roster=MultiplayerUIArt.GraphCard("PlayerRoster",waitingPanel.transform,0,.24f,.36f,1);
        count=MultiplayerUIArt.Label("PlayerCount",roster,"1 / 4 PLAYERS",.08f,.87f,.92f,.97f,30,false,StageUITheme.ReadingInk);
        MultiplayerUIArt.Label("RosterHeading",roster,"YOUR PARTY",.08f,.77f,.92f,.86f,30,false,StageUITheme.ReadingInk);
        slots=new TMP_Text[4];
        for(int i=0;i<4;i++)
        {
            float top=.74f-i*.15f;
            slots[i]=MultiplayerUIArt.Label("PlayerSlot"+(i+1),roster,"OPEN SLOT\nWaiting for a friend...",.09f,top-.14f,.91f,top,22,true,StageUITheme.ReadingInk);
            slots[i].alignment=TextAlignmentOptions.Left;
        }
        startHint=MultiplayerUIArt.Label("StartHint",roster,"Invite at least one friend to start.",.08f,.005f,.92f,.13f,19,true,StageUITheme.ReadingInk);
        var chat=MultiplayerUIArt.ChatSurface("RoomChat",waitingPanel.transform,.39f,.24f,1,1);
        MultiplayerUIArt.Label("ChatHeading",chat,"PARTY CHAT",.08f,.87f,.92f,.99f,30,false,StageUITheme.ReadingInk);
        MultiplayerUIArt.ChatView(chat,25,.85f,true,true);
        ready=LobbyAction("ToggleReady",waitingPanel.transform,"READY",.09f,.04f,true,width:220);
        start=LobbyAction("StartGame",waitingPanel.transform,"START GAME",.285f,.04f,true,width:220);
        leave=LobbyAction("LeaveLobby",waitingPanel.transform,"LEAVE LOBBY",1,.04f,false,true);
        var leaveRect=(RectTransform)leave.transform;
        leaveRect.pivot=new Vector2(1,.5f);leaveRect.anchoredPosition=new Vector2(-24,0);
        back=LobbyAction("BackToPlay",inside,"BACK TO PLAY",.5f,.07f,false,true);
        leaveConfirmation=MultiplayerUIArt.Rect("LeaveLobbyConfirmation",overlay.transform).gameObject;
        var confirmationDim=leaveConfirmation.AddComponent<Image>();confirmationDim.color=new Color(.01f,.02f,.03f,.72f);
        var confirmationCard=MultiplayerUIArt.GraphCard("ConfirmationCard",leaveConfirmation.transform,.5f,.5f,.5f,.5f);
        confirmationCard.sizeDelta=new Vector2(820,380);
        MultiplayerUIArt.Label("Title",confirmationCard,"LEAVE LOBBY?",.08f,.69f,.92f,.91f,42,false,StageUITheme.ReadingInk);
        MultiplayerUIArt.Label("Message",confirmationCard,"Are you sure you want to leave this lobby?",.10f,.42f,.90f,.66f,28,true,StageUITheme.ReadingInk);
        cancelLeave=LobbyAction("CancelLeave",confirmationCard,"CANCEL",.30f,.20f,true);
        confirmLeave=LobbyAction("ConfirmLeave",confirmationCard,"LEAVE",.70f,.20f,false,true);
        leaveConfirmation.SetActive(false);
        choicePanel.SetActive(true);joinPanel.SetActive(false);waitingPanel.SetActive(false);overlay.SetActive(false);
    }
    private static Button LobbyAction(string name,Transform parent,string label,float x,float y,bool green=false,bool red=false,float width=280)
    {
        var button=MultiplayerUIArt.Action(name,parent,label,x,y,x,y,green,red);
        MultiplayerUIArt.StandardAction(button,width);
        return button;
    }
}
