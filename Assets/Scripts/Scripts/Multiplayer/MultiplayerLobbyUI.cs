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
    [SerializeField] private TMP_Text[] slots;
    [SerializeField] private TMP_InputField inviteInput;
    [SerializeField] private Button host, joinChoice, join, copy, ready, start, leave, back, cancelJoin;
    private MultiplayerLobbyService service;
    private GameInput blockedInput;
    private bool wasBlocked, ownsBlock;
    private bool choosingJoin;
    public bool IsOpen => overlay != null && overlay.activeSelf;
    public TMP_InputField InviteInput => inviteInput;

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
    private void Leave() { service.LeaveLobby(); }
    private void CopyCode()
    {
        if (!service.InLobby) return;
        GUIUtility.systemCopyBuffer = PhotonNetwork.CurrentRoom.Name;
        service.Notify("Invite code copied. Share it with your friends outside the game.");
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
        // The lobby uses the space formerly reserved for the menu's back button.
        status.rectTransform.anchorMin = new Vector2(.04f, inRoom ? 0 : .14f);
        status.rectTransform.anchorMax = new Vector2(.96f, inRoom ? .05f : .20f);
        status.fontSizeMin = inRoom ? 20 : 22;
        status.fontSizeMax = inRoom ? 22 : 24;
        host.interactable = joinChoice.interactable = join.interactable = !service.Busy;
        inviteInput.interactable = !service.Busy;
        back.gameObject.SetActive(!inRoom);
        back.interactable = cancelJoin.interactable = !service.Busy;
        if (!inRoom) return;
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
        MultiplayerUIArt.Label("Eyebrow",inside,"ADVENTURE TOGETHER",0,.955f,1,1,24,false,StageUITheme.Gold);
        MultiplayerUIArt.Label("Heading",inside,"MULTIPLAYER",0,.85f,1,.955f,52,false,StageUITheme.Ink);
        status=MultiplayerUIArt.Label("Status",inside,"Host a private game or enter a friend's code.",.04f,.14f,.96f,.20f,24,true,StageUITheme.Ink);
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
        count=MultiplayerUIArt.Label("PlayerCount",waitingPanel.transform,"1 / 4 PLAYERS",0,.86f,.36f,1,30,false,StageUITheme.Ink);
        MultiplayerUIArt.Label("InviteLabel",waitingPanel.transform,"INVITE CODE",.40f,.86f,.55f,1,26,false,StageUITheme.Gold);
        code=MultiplayerUIArt.Label("Code",waitingPanel.transform,"------",.56f,.86f,.73f,1,44,false,StageUITheme.Ink);
        copy=LobbyAction("CopyCode",waitingPanel.transform,"COPY",.85f,1,width:220);
        ((RectTransform)copy.transform).anchoredPosition=new Vector2(0,-44);
        var roster=MultiplayerUIArt.GraphCard("PlayerRoster",waitingPanel.transform,0,.20f,.36f,.84f);
        MultiplayerUIArt.Label("RosterHeading",roster,"YOUR PARTY",.08f,.83f,.92f,.96f,30,false,StageUITheme.ReadingInk);
        slots=new TMP_Text[4];
        for(int i=0;i<4;i++)
        {
            float top=.81f-i*.18f;
            slots[i]=MultiplayerUIArt.Label("PlayerSlot"+(i+1),roster,"OPEN SLOT\nWaiting for a friend...",.09f,top-.17f,.91f,top,22,true,StageUITheme.ReadingInk);
            slots[i].alignment=TextAlignmentOptions.Left;
        }
        var chat=MultiplayerUIArt.ChatSurface("RoomChat",waitingPanel.transform,.38f,.20f,1,.84f);
        MultiplayerUIArt.Label("ChatHeading",chat,"PARTY CHAT",.08f,.87f,.92f,.99f,30,false,StageUITheme.ReadingInk);
        MultiplayerUIArt.ChatView(chat,25,.85f,true);
        ready=LobbyAction("ToggleReady",waitingPanel.transform,"READY",.135f,.0775f,true);
        start=LobbyAction("StartGame",waitingPanel.transform,"START GAME",.47f,.0775f,true);
        leave=LobbyAction("LeaveLobby",waitingPanel.transform,"LEAVE LOBBY",.835f,.0775f,false,true);
        startHint=MultiplayerUIArt.Label("StartHint",waitingPanel.transform,"Invite at least one friend to start.",0,.15f,1,.20f,22,true,StageUITheme.Ink);
        back=LobbyAction("BackToPlay",inside,"BACK TO PLAY",.5f,.07f,false,true);
        choicePanel.SetActive(true);joinPanel.SetActive(false);waitingPanel.SetActive(false);overlay.SetActive(false);
    }
    private static Button LobbyAction(string name,Transform parent,string label,float x,float y,bool green=false,bool red=false,float width=280)
    {
        var button=MultiplayerUIArt.Action(name,parent,label,x,y,x,y,green,red);
        MultiplayerUIArt.StandardAction(button,width);
        return button;
    }
}
