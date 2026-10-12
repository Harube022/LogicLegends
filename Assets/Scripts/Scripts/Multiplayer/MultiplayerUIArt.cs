using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Multiplayer composition using original menu artwork and the explicitly requested GraphHUD chat frames.</summary>
public static class MultiplayerUIArt
{
    // The editor assigns these source sprites; Images serialize them into the owning scenes.
    public static Sprite ChatFrame, ChatField;
    public static RectTransform Rect(string name, Transform parent, float x0=0, float y0=0, float x1=1, float y1=1)
    {
        var rect=StageUITheme.Rect(name,parent);
        StageUITheme.Stretch(rect,new Vector2(x0,y0),new Vector2(x1,y1)); return rect;
    }
    public static TMP_Text Label(string name,Transform parent,string value,float x0,float y0,float x1,float y1,float size,bool reading,Color color)
    {
        var text=StageUITheme.Text(name,parent,value,size,reading);
        StageUITheme.Stretch(text.rectTransform,new Vector2(x0,y0),new Vector2(x1,y1));
        text.color=color; text.richText=false; text.enableAutoSizing=true;
        text.fontSizeMin=size-2; text.fontSizeMax=size; return text;
    }
    public static RectTransform Paper(string name,Transform parent,float x0,float y0,float x1,float y1)
    {
        var rect=Rect(name,parent,x0,y0,x1,y1); var image=rect.gameObject.AddComponent<Image>();
        image.sprite=StageUITheme.Paper; image.type=Image.Type.Sliced;
        image.pixelsPerUnitMultiplier=2.5f; image.raycastTarget=false; return rect;
    }
    public static Button Action(string name,Transform parent,string title,float x0,float y0,float x1,float y1,bool green=false,bool red=false)
    {
        var button=StageUITheme.Button(name,parent,title,null);
        StageUITheme.Stretch((RectTransform)button.transform,new Vector2(x0,y0),new Vector2(x1,y1));
        if(green||red)button.GetComponent<Image>().sprite=Resources.LoadAll<Sprite>(green?"StageUI/WoodButtonGreen":"StageUI/WoodButtonRed")[0];
        var text=button.GetComponentInChildren<TMP_Text>(); text.fontSizeMin=24; text.fontSizeMax=32;
        text.richText=false; button.navigation=new Navigation { mode=Navigation.Mode.None }; return button;
    }
    public static void StandardAction(Button button,float width=280)
    {
        var rect=(RectTransform)button.transform;
        rect.sizeDelta=new Vector2(width,88);
        button.GetComponent<Image>().pixelsPerUnitMultiplier=4.5f;
        var text=button.GetComponentInChildren<TMP_Text>();
        text.rectTransform.offsetMin=new Vector2(24,10);text.rectTransform.offsetMax=new Vector2(-24,-10);
        text.fontSizeMin=22;text.fontSizeMax=30;text.textWrappingMode=TextWrappingModes.NoWrap;
    }
    public static TMP_InputField Input(string name,Transform parent,string hint,float x0,float y0,float x1,float y1,float size,int limit,bool dark=false)
    {
        var rect=Rect(name,parent,x0,y0,x1,y1); var image=rect.gameObject.AddComponent<Image>();
        image.sprite=ChatField; image.type=Image.Type.Sliced;
        image.pixelsPerUnitMultiplier=Mathf.Min(image.sprite.rect.width,image.sprite.rect.height)*.01f;
        var input=rect.gameObject.AddComponent<TMP_InputField>();
        var viewport=Rect("TextArea",rect); viewport.offsetMin=new Vector2(30,10);viewport.offsetMax=new Vector2(-30,-10);
        viewport.gameObject.AddComponent<RectMask2D>();
        var text=Label("Value",viewport,"",0,0,1,1,size,true,StageUITheme.ReadingInk);
        var placeholder=Label("Placeholder",viewport,hint,0,0,1,1,size,true,new Color(.44f,.37f,.27f));
        text.alignment=placeholder.alignment=TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing=placeholder.enableAutoSizing=false;
        input.targetGraphic=image;input.textViewport=viewport;input.textComponent=text;input.placeholder=placeholder;
        input.characterLimit=limit;input.richText=false;input.lineType=TMP_InputField.LineType.SingleLine;
        input.caretColor=StageUITheme.ReadingInk;input.selectionColor=new Color(.18f,.36f,.34f,.3f);
        return input;
    }
    public static RectTransform ChatSurface(string name,Transform parent,float x0,float y0,float x1,float y1)
    {
        var rect=GraphCard(name,parent,x0,y0,x1,y1);
        var inset=Rect("ChatInterior",rect);inset.offsetMin=new Vector2(30,30);inset.offsetMax=new Vector2(-30,-30);
        return inset;
    }
    public static RectTransform GraphCard(string name,Transform parent,float x0,float y0,float x1,float y1)
    {
        var rect=Rect(name,parent,x0,y0,x1,y1);GraphSurface(rect.gameObject.AddComponent<Image>());return rect;
    }
    public static void GraphSurface(Image image)
    {
        image.sprite=ChatFrame;image.type=Image.Type.Sliced;image.color=Color.white;
        image.pixelsPerUnitMultiplier=Mathf.Min(ChatFrame.rect.width,ChatFrame.rect.height)*.01f;
    }
    public static RoomChatPanel ChatView(Transform parent,float fontSize=26,float top=.80f,bool standardActions=false)
    {
        var root=Rect("Conversation",parent,.055f,.04f,.945f,top);
        var view=root.gameObject.AddComponent<RoomChatPanel>();
        var scrollRoot=Rect("ChatScroll",root);scrollRoot.offsetMin=new Vector2(0,108);scrollRoot.offsetMax=new Vector2(0,-4);
        var scroll=scrollRoot.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;
        scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=36;
        var viewport=Rect("Viewport",scrollRoot); viewport.gameObject.AddComponent<RectMask2D>();
        var hit=viewport.gameObject.AddComponent<Image>();hit.color=new Color(1,1,1,.001f);
        var list=Rect("MessageCards",viewport,0,1,1,1);list.pivot=new Vector2(.5f,1);
        var layout=list.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=10;
        layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
        var fit=list.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport;scroll.content=list;
        // A plain transcript is kept for accessibility/integration consumers; the cards are the visible view.
        view.history=Label("Transcript",root,"",0,.27f,1,1,fontSize,true,StageUITheme.ReadingInk);
        view.history.gameObject.SetActive(false);
        view.messageList=list;view.scroll=scroll;view.messageFontSize=fontSize;
        view.input=Input("ChatMessage",root,"Write to your party...",0,0,.77f,0,fontSize,LobbyRules.ChatLimit,true);
        var inputRect=(RectTransform)view.input.transform;inputRect.offsetMin=new Vector2(0,30);inputRect.offsetMax=new Vector2(0,94);
        view.send=Action("SendMessage",root,"SEND",.79f,0,1,0,true);
        var sendRect=(RectTransform)view.send.transform;sendRect.offsetMin=new Vector2(0,30);sendRect.offsetMax=new Vector2(0,94);
        if(standardActions)
        {
            // Fixed-size supporting action, with the message field taking the remaining width.
            sendRect.anchorMin=sendRect.anchorMax=new Vector2(1,0);sendRect.pivot=new Vector2(1,0);
            StandardAction(view.send,220);sendRect.anchoredPosition=new Vector2(0,30);
            inputRect.anchorMax=new Vector2(1,0);inputRect.offsetMax=new Vector2(-236,118);
            scrollRoot.offsetMin=new Vector2(0,132);
        }
        view.feedback=Label("ChatFeedback",root,"Only your party can read this chat.",0,0,.82f,0,20,true,StageUITheme.ReadingInk);
        view.feedback.rectTransform.offsetMax=new Vector2(0,26);
        view.feedback.alignment=TextAlignmentOptions.Left;
        view.counter=Label("CharacterCount",root,"0 / 200",.82f,0,1,0,20,true,StageUITheme.ReadingInk);
        view.counter.rectTransform.offsetMax=new Vector2(0,26);
        view.counter.alignment=TextAlignmentOptions.Right;
        return view;
    }
}
