using TMPro;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reusable waiting-room/gameplay chat view; only the room service owns transport.</summary>
public sealed class RoomChatPanel : MonoBehaviour
{
    public TMP_InputField input;
    public TMP_Text history;
    public Button send;
    public ScrollRect scroll;
    public RectTransform messageList;
    public TMP_Text feedback, counter;
    public float messageFontSize=26;
    private MultiplayerLobbyService service;
    private GameInput blockedInput;
    private bool previousBlock, ownsBlock;
    private int renderedSequence=-1, renderedCount=-1;
    private float nextSend;

    private void OnEnable()
    {
        service = MultiplayerLobbyService.Instance;
        if (service != null) service.Changed += Refresh;
        input.richText = false; history.richText = false;
        input.characterLimit = LobbyRules.ChatLimit;
        send.onClick.AddListener(Send);
        input.onSubmit.AddListener(Submit);
        input.onSelect.AddListener(SelectInput);
        input.onValueChanged.AddListener(Edited);
        Edited(input.text);
        Refresh();
    }
    private void OnDisable()
    {
        if (service != null) service.Changed -= Refresh;
        send.onClick.RemoveListener(Send);
        input.onSubmit.RemoveListener(Submit);
        input.onSelect.RemoveListener(SelectInput);
        input.onValueChanged.RemoveListener(Edited);
        RestoreInput();
    }
    private void Update()
    {
        if (input.isFocused) SelectInput(input.text);
        if (!input.isFocused) RestoreInput();
        send.interactable=service!=null&&service.InLobby&&Time.unscaledTime>=nextSend&&!string.IsNullOrWhiteSpace(input.text);
    }
    private void SelectInput(string value)
    {
        // The lobby modal already owns a block. Do not acquire/restore it a second time
        // when this child is disabled in a different order during scene destruction.
        if (ownsBlock || GameInput.Instance == null || GameInput.Instance.GameplayInputBlocked) return;
        blockedInput = GameInput.Instance; previousBlock = blockedInput.GameplayInputBlocked;
        ownsBlock = true; blockedInput.SetGameplayInputBlocked(true);
    }
    private void RestoreInput()
    {
        if (ownsBlock && blockedInput != null) blockedInput.SetGameplayInputBlocked(previousBlock);
        ownsBlock = false; blockedInput = null;
    }
    private void Submit(string value) { Send(); }
    private void Send()
    {
        if (service == null) return;
        if (service.SendChat(input.text))
        {
            input.text="";nextSend=Time.unscaledTime+LobbyRules.ChatCooldown;
            if(feedback!=null)feedback.text="Message sent to your party.";
        }
        else if(feedback!=null)feedback.text=service.Status;
    }
    private void Edited(string text)
    {
        if(counter!=null)counter.text=text.Length+" / "+LobbyRules.ChatLimit;
        if(feedback!=null)feedback.text="Only your party can read this chat.";
    }
    public void Refresh()
    {
        if (service == null) return;
        history.text = string.Join("\n", service.Messages);
        input.interactable = service.InLobby;
        if(messageList==null) { Canvas.ForceUpdateCanvases();if(scroll!=null)scroll.verticalNormalizedPosition=0;return; }
        var entries=service.ChatMessages;
        int sequence=entries.Count==0?0:entries[entries.Count-1].Sequence;
        if(sequence==renderedSequence&&entries.Count==renderedCount)return;
        bool atBottom=renderedSequence<0||scroll.verticalNormalizedPosition<.08f;
        float oldPosition=scroll.verticalNormalizedPosition;
        renderedSequence=sequence;renderedCount=entries.Count;
        for(int i=messageList.childCount-1;i>=0;i--)
        { var old=messageList.GetChild(i).gameObject;old.SetActive(false);Destroy(old); }
        foreach(var entry in entries) AddCard(entry);
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition=atBottom?0:oldPosition;
    }
    private void AddCard(RoomChatMessage entry)
    {
        var card=MultiplayerUIArt.Rect("Message "+entry.Sequence,messageList);
        bool mine=!entry.IsSystem&&entry.Actor==PhotonNetwork.LocalPlayer.ActorNumber;
        var background=card.gameObject.AddComponent<Image>();
        background.color=entry.IsSystem?new Color(.73f,.62f,.43f,.14f):mine?new Color(.15f,.40f,.33f,.16f):new Color(1,.97f,.86f,.64f);
        background.raycastTarget=false;
        var layout=card.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(14,14,8,10);
        layout.spacing=4;layout.childControlWidth=layout.childControlHeight=true;
        layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
        if(!entry.IsSystem)
        {
            var name=StageUITheme.Text("Sender",card,(mine?"YOU · ":"")+entry.Name,messageFontSize-2);
            name.richText=false;name.color=mine?new Color(.12f,.34f,.26f):StageUITheme.ReadingInk;
            name.alignment=mine?TextAlignmentOptions.Right:TextAlignmentOptions.Left;
            name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Ellipsis;
        }
        var body=StageUITheme.Text("Message",card,entry.Text,entry.IsSystem?messageFontSize-3:messageFontSize,true);
        body.richText=false;body.color=StageUITheme.ReadingInk;
        body.alignment=entry.IsSystem?TextAlignmentOptions.Center:mine?TextAlignmentOptions.TopRight:TextAlignmentOptions.TopLeft;
    }
}
