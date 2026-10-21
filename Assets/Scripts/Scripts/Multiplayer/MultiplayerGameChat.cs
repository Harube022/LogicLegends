using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Scene-owned party chat. Never creates a chat HUD in solo scenes.</summary>
public sealed class MultiplayerGameChat : MonoBehaviour
{
    private static readonly Vector2 PanelMin=new Vector2(.14f,.12f),PanelMax=new Vector2(.86f,.93f);
    [SerializeField] private Button toggle, close, backdrop;
    [SerializeField] private GameObject overlay;
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text unreadLabel, roomLabel;
    [SerializeField] private RoomChatPanel conversation;
    [SerializeField] private GameObject gameplayRoot;
    private MultiplayerLobbyService service;
    private GameInput blockedInput;
    private bool previousBlock, ownsBlock;
    private bool previousGameplayVisible, ownsVisibility;
    private int unread;
    private readonly List<Behaviour> suspendedLook=new List<Behaviour>();
    public bool IsOpen => overlay!=null&&overlay.activeSelf;
    public int UnreadCount => unread;
    public RoomChatPanel Conversation => conversation;

    private void Awake()
    {
        if(!MultiplayerSceneRoutes.IsGameplay(gameObject.scene)) { toggle.gameObject.SetActive(false);gameObject.SetActive(false);return; }
        overlay.SetActive(false);
        toggle.onClick.AddListener(Toggle);close.onClick.AddListener(Close);backdrop.onClick.AddListener(Close);
    }
    private void OnEnable()
    {
        service=MultiplayerLobbyService.Instance;
        if(service!=null) { service.ChatReceived+=Received;service.Changed+=Refresh; }
        Refresh();
    }
    private void OnDisable()
    {
        if(service!=null) { service.ChatReceived-=Received;service.Changed-=Refresh; }
        Close();
        // The edge button is a sibling under SafeArea, so it does not inherit this root's visibility.
        if(toggle!=null)toggle.gameObject.SetActive(false);
    }
    private void Update()
    {
        if(!IsOpen)return;
        if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame) { Close();return; }
        // Keep the composer above the native keyboard without scaling the existing touch HUD.
        float keyboard=0;
        if(TouchScreenKeyboard.visible)
        {
            var area=TouchScreenKeyboard.area;
            keyboard=area.height>0?Mathf.Clamp01(area.yMax/Mathf.Max(1,Screen.height)):.40f;
        }
        panel.anchorMin=new Vector2(PanelMin.x,Mathf.Max(PanelMin.y,keyboard+.015f));
        panel.anchorMax=PanelMax;
        MobileLookInput.ResetDelta();
    }
    private void Received(RoomChatMessage message)
    {
        if(!IsOpen&&!message.IsSystem&&message.Actor!=PhotonNetwork.LocalPlayer.ActorNumber)unread++;
        Refresh();
    }
    private void Refresh()
    {
        bool available=MultiplayerSceneRoutes.IsGameplay(gameObject.scene)&&service!=null&&service.InLobby;
        toggle.gameObject.SetActive(available&&!IsOpen);
        if(!available) { unread=0;Close(); }
        unreadLabel.transform.parent.gameObject.SetActive(unread>0);
        unreadLabel.text=unread>99?"99+":unread.ToString();
        unreadLabel.fontSize=unread>99?17:unread>9?21:25;
        if(available)roomLabel.text=service.Players.Length+" / 4 adventurers  ·  Private conversation";
    }
    public void Toggle() { if(IsOpen)Close();else Open(); }
    public void Open()
    {
        if(IsOpen||service==null||!service.InLobby||!MultiplayerSceneRoutes.IsGameplay(gameObject.scene)||Time.timeScale<=0)return;
        blockedInput=GameInput.Instance;
        if(blockedInput!=null) { previousBlock=blockedInput.GameplayInputBlocked;blockedInput.SetGameplayInputBlocked(true);ownsBlock=true; }
        MobileInputUI.FindJoystick()?.ResetJoystick();
        foreach(var look in FindObjectsByType<MobileLookInput>(FindObjectsSortMode.None))Suspend(look);
        if(Player.LocalInstance!=null)
            foreach(var look in Player.LocalInstance.GetComponentsInChildren<CameraTargetController>())Suspend(look);
        foreach(var look in FindObjectsByType<Unity.Cinemachine.CinemachineInputAxisController>(FindObjectsSortMode.None))Suspend(look);
        MobileLookInput.ResetDelta();
        if(gameplayRoot!=null) { previousGameplayVisible=gameplayRoot.activeSelf;ownsVisibility=true;gameplayRoot.SetActive(false); }
        unread=0;overlay.SetActive(true);Refresh();
        Canvas.ForceUpdateCanvases();conversation.scroll.verticalNormalizedPosition=0;
    }
    private void Suspend(Behaviour behaviour)
    {
        if(!behaviour.enabled)return;suspendedLook.Add(behaviour);behaviour.enabled=false;
    }
    public void Close()
    {
        if(overlay!=null)overlay.SetActive(false);
        if(conversation!=null&&conversation.input!=null)conversation.input.DeactivateInputField();
        if(EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null&&
            (EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform)||
             (toggle!=null&&EventSystem.current.currentSelectedGameObject.transform.IsChildOf(toggle.transform))))EventSystem.current.SetSelectedGameObject(null);
        if(ownsBlock&&blockedInput!=null) { blockedInput.SetMobileMovement(Vector2.zero);blockedInput.SetGameplayInputBlocked(previousBlock); }
        ownsBlock=false;blockedInput=null;
        foreach(var look in suspendedLook)if(look!=null)look.enabled=true;
        suspendedLook.Clear();MobileLookInput.ResetDelta();
        if(ownsVisibility&&gameplayRoot!=null)gameplayRoot.SetActive(previousGameplayVisible);
        ownsVisibility=false;
        if(toggle!=null)toggle.gameObject.SetActive(service!=null&&service.InLobby);
    }
    public static MultiplayerGameChat Build(Transform safeArea)
    {
        var old=safeArea.Find("MultiplayerChatHUD");if(old!=null)DestroyImmediate(old.gameObject);
        var canvas=safeArea.GetComponentInParent<Canvas>().transform;
        old=canvas.Find("MultiplayerChatHUD");if(old!=null)DestroyImmediate(old.gameObject);
        old=safeArea.Find("MultiplayerChatEdgeControls");if(old!=null)DestroyImmediate(old.gameObject);
        // Only controls placed near a screen edge follow the notch/home-bar safe area.
        var edge=MultiplayerUIArt.Rect("MultiplayerChatEdgeControls",safeArea);
        var root=MultiplayerUIArt.Rect("MultiplayerChatHUD",canvas);
        var hud=root.gameObject.AddComponent<MultiplayerGameChat>();
        hud.gameplayRoot=safeArea.Find("Gameplay_Interface")?.gameObject;
        hud.toggle=StageUITheme.Button("ChatToggle",edge,"CHAT",null);
        var toggleRect=(RectTransform)hud.toggle.transform;
        toggleRect.anchorMin=toggleRect.anchorMax=new Vector2(0,1);toggleRect.pivot=new Vector2(0,1);
        toggleRect.anchoredPosition=new Vector2(50,-185);MultiplayerUIArt.StandardAction(hud.toggle,220);
        if(MultiplayerSceneRoutes.IsOpenArchive(safeArea.gameObject.scene))
        {
            // Beside Settings, clear of the shared topic banner and development tools below.
            toggleRect.anchoredPosition=new Vector2(186,-32);
            MultiplayerUIArt.StandardAction(hud.toggle,132);
        }
        hud.toggle.navigation=new Navigation { mode=Navigation.Mode.None };
        var badgeRect=MultiplayerUIArt.Rect("UnreadBadge",toggleRect,1,1,1,1);
        badgeRect.sizeDelta=new Vector2(48,48);badgeRect.anchoredPosition=new Vector2(-4,0);
        var badge=badgeRect.gameObject.AddComponent<Image>();badge.sprite=Resources.LoadAll<Sprite>("StageUI/UnreadNotification")[0];
        badge.color=new Color(.82f,.16f,.14f,1f);
        badge.raycastTarget=false;
        hud.unreadLabel=MultiplayerUIArt.Label("Unread",badgeRect,"",0,0,1,1,25,false,new Color(1f,.96f,.86f,1f));
        hud.overlay=MultiplayerUIArt.Rect("ChatOverlay",root).gameObject;
        var dim=hud.overlay.AddComponent<Image>();dim.color=new Color(.01f,.02f,.025f,.32f);dim.pixelsPerUnitMultiplier=4.5f;
        hud.backdrop=hud.overlay.AddComponent<Button>();hud.backdrop.targetGraphic=dim;hud.backdrop.transition=Selectable.Transition.None;
        hud.panel=MultiplayerUIArt.Rect("ChatWoodFrame",hud.overlay.transform,PanelMin.x,PanelMin.y,PanelMax.x,PanelMax.y);
        var wood=hud.panel.gameObject.AddComponent<Image>();MultiplayerUIArt.GraphSurface(wood);
        var inside=MultiplayerUIArt.Rect("Interior",hud.panel);
        inside.offsetMin=new Vector2(32,32);inside.offsetMax=new Vector2(-32,-32);
        var title=MultiplayerUIArt.Label("ChatTitle",inside,"PARTY CHAT",0,1,1,1,40,false,StageUITheme.ReadingInk);
        title.rectTransform.offsetMin=new Vector2(0,-48);title.rectTransform.offsetMax=new Vector2(-244,0);
        title.alignment=TextAlignmentOptions.MidlineLeft;
        hud.roomLabel=MultiplayerUIArt.Label("RoomHeading",inside,"Private conversation",0,1,1,1,22,true,StageUITheme.ReadingInk);
        hud.roomLabel.rectTransform.offsetMin=new Vector2(0,-88);hud.roomLabel.rectTransform.offsetMax=new Vector2(-244,-52);
        hud.roomLabel.alignment=TextAlignmentOptions.MidlineLeft;
        hud.close=MultiplayerUIArt.Action("CloseChat",inside,"CLOSE",1,1,1,1,false,true);
        var closeRect=(RectTransform)hud.close.transform;closeRect.pivot=new Vector2(1,1);
        MultiplayerUIArt.StandardAction(hud.close,220);closeRect.anchoredPosition=Vector2.zero;
        var divider=MultiplayerUIArt.Rect("HeaderDivider",inside,0,1,1,1);
        divider.offsetMin=new Vector2(0,-112);divider.offsetMax=new Vector2(0,-110);
        var rule=divider.gameObject.AddComponent<Image>();rule.color=new Color(.32f,.20f,.12f,.25f);rule.raycastTarget=false;
        var surface=MultiplayerUIArt.Rect("ConversationPanel",inside);surface.offsetMax=new Vector2(0,-128);
        hud.conversation=MultiplayerUIArt.ChatView(surface,28,.98f,true);
        var conversationRect=(RectTransform)hud.conversation.transform;
        conversationRect.anchorMin=Vector2.zero;conversationRect.anchorMax=Vector2.one;
        conversationRect.offsetMin=conversationRect.offsetMax=Vector2.zero;
        hud.overlay.SetActive(false);badgeRect.gameObject.SetActive(false);
        return hud;
    }
}
