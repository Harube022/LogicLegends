using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authors only the Main Menu shop preview; keeps the existing dressing-room prefabs connected.</summary>
public static class ShopPreviewSetup
{
    [MenuItem("Logic Legends/Shop/Validate Purchase Rules Without Spending Currency")]
    public static void ValidatePurchaseRules()
    {
        Expect(false, null, false, null, 10, ShopPurchaseDecision.WaitForServerData);
        Expect(false, "Female_Character", true, 100, 10, ShopPurchaseDecision.WaitForServerData);
        Expect(true, null, false, 100, 10, ShopPurchaseDecision.MissingCharacter);
        Expect(true, "Female_Character", false, 100, 10, ShopPurchaseDecision.CharacterChanged);
        Expect(true, "Male_Character", true, 100, 10, ShopPurchaseDecision.AlreadyOwned);
        Expect(true, "Male_Character", false, -1, 10, ShopPurchaseDecision.InvalidBalance);
        Expect(true, "Male_Character", false, "bad", 10, ShopPurchaseDecision.InvalidBalance);
        Expect(true, "Male_Character", false, 100, -1, ShopPurchaseDecision.InvalidBalance);
        Expect(true, "Male_Character", false, null, 10, ShopPurchaseDecision.InsufficientFunds);
        Expect(true, "Male_Character", false, 9, 10, ShopPurchaseDecision.InsufficientFunds);
        Expect(true, "Male_Character", false, 10L, 10, ShopPurchaseDecision.Ready);
        Expect(true, "Male_Character", false, "100", 10, ShopPurchaseDecision.Ready);
        Expect(true, "Male_Character", false, null, 0, ShopPurchaseDecision.Ready);
        int balance;
        if (ShopPurchaseRules.Evaluate(true,"Male_Character","Male_Character",false,100,10,out balance) !=
            ShopPurchaseDecision.Ready || balance != 100)
            throw new InvalidOperationException("Purchase validation changed the available balance.");
        Debug.Log("PASS: 14 purchase-rule checks; cold-cache, stale character, ownership, invalid balance and funds. No Firebase writes.");
    }

    private static void Expect(bool hasAccount, string character, bool owned, object balance, int price, ShopPurchaseDecision expected)
    {
        int available;
        var actual=ShopPurchaseRules.Evaluate(hasAccount,character,"Male_Character",owned,balance,price,out available);
        if(actual!=expected)throw new InvalidOperationException("Purchase rule expected "+expected+" but got "+actual);
    }

    [MenuItem("Logic Legends/Shop/Build Clothes Preview And Confirmation")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Open Main Menu outside Play Mode before authoring the shop preview.");
        var shop = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShopManager>(true)).Single();
        var custom = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CustomizationManager>(true)).Single();
        var canvas = shop.GetComponentInParent<Canvas>().transform;
        var source = new SerializedObject(custom);
        var old = canvas.Find("ShopItemPreview");
        bool updatingPreview = old != null;
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var ui = shop.GetComponent<ShopPreviewUI>();
        if (ui == null) ui = shop.gameObject.AddComponent<ShopPreviewUI>();
        var overlay = Rect("ShopItemPreview", canvas, Vector2.zero, Vector2.one);
        var dim = overlay.gameObject.AddComponent<Image>(); dim.color = new Color(.015f,.025f,.04f,.78f);
        var frame = Rect("PreviewCard", overlay, new Vector2(.5f,.5f), new Vector2(.5f,.5f));
        frame.sizeDelta = new Vector2(1320,820); Surface(frame, SpriteAt("Assets/Art/GraphHUD/GraphMapFrame.png"));
        frame.GetComponent<Image>().pixelsPerUnitMultiplier=6.5f;
        var header=Rect("HeaderPlaque",frame,new Vector2(.22f,.85f),new Vector2(.78f,.96f));
        Surface(header,SpriteAt("Assets/Art/GraphHUD/GraphStatusFrame.png"));
        var title = Label("Heading", header, "CLOTHES PREVIEW", .07f,.13f,.93f,.87f,40,false);
        title.alignment = TextAlignmentOptions.Center;
        title.characterSpacing=2;
        var portrait = Rect("CharacterFrame", frame, new Vector2(.05f,.105f), new Vector2(.45f,.825f));
        Surface(portrait, SpriteAt("Assets/Art/GraphHUD/GraphMapFrame.png"));
        var caption=Label("PreviewCaption",portrait,"OUTFIT PREVIEW",.07f,.025f,.93f,.09f,24,false);
        caption.alignment=TextAlignmentOptions.Center;caption.characterSpacing=2;
        var viewport = Rect("CharacterViewport",portrait,new Vector2(.055f,.105f),new Vector2(.945f,.95f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var imageRect = Rect("Character",viewport,Vector2.zero,Vector2.one);
        var raw = imageRect.gameObject.AddComponent<RawImage>(); raw.raycastTarget=false;
        raw.texture = custom.transform.Find("Character").GetComponent<RawImage>().texture;
        var aspect = imageRect.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio=raw.texture==null?1f:(float)raw.texture.width/raw.texture.height;
        var eyebrow=Label("OutfitLabel",frame,"SHOP OUTFIT",.51f,.77f,.94f,.815f,23,true);
        eyebrow.fontStyle=FontStyles.Bold;eyebrow.characterSpacing=3;eyebrow.color=new Color(.42f,.31f,.20f);
        var name = Label("ItemName",frame,"Clothes",.51f,.665f,.94f,.77f,54,false);
        name.color=new Color(.12f,.29f,.29f);
        var cost=Rect("PricePlaque",frame,new Vector2(.50f,.50f),new Vector2(.95f,.65f));
        Surface(cost,SpriteAt("Assets/Art/GraphHUD/GraphStatusFrame.png"));
        var priceCaption=Label("PriceLabel",cost,"PRICE",.065f,.64f,.90f,.90f,23,true);
        priceCaption.fontStyle=FontStyles.Bold;priceCaption.characterSpacing=2;
        var iconRect=Rect("CurrencyIcon",cost,new Vector2(.055f,.09f),new Vector2(.18f,.59f));
        var icon=iconRect.gameObject.AddComponent<Image>();icon.preserveAspect=true;icon.raycastTarget=false;
        var coin=SpriteAt("Assets/Art/ProfileCurrency/Coin.png");var gem=SpriteAt("Assets/Art/ProfileCurrency/Gem.png");icon.sprite=coin;
        var price = Label("Price",cost,"10",.23f,.04f,.64f,.62f,50,false);
        var unit=Label("CurrencyUnit",cost,"COINS",.67f,.10f,.94f,.54f,25,true);unit.fontStyle=FontStyles.Bold;
        var wallet=Rect("BalancePlaque",frame,new Vector2(.50f,.33f),new Vector2(.95f,.48f));
        Surface(wallet,SpriteAt("Assets/Art/GraphHUD/GraphStatusFrame.png"));
        var walletCaption=Label("BalanceLabel",wallet,"YOUR BALANCE",.065f,.68f,.94f,.92f,23,true);
        walletCaption.fontStyle=FontStyles.Bold;walletCaption.characterSpacing=2;
        var balance = Label("Balance",wallet,"0 COINS",.065f,.27f,.94f,.68f,34,false);
        var after=Label("AfterPurchase",wallet,"",.065f,.04f,.94f,.28f,22,true);
        after.color=new Color(.18f,.36f,.23f);after.gameObject.SetActive(false);
        var feedback=Rect("FeedbackPlaque",frame,new Vector2(.50f,.155f),new Vector2(.95f,.31f));
        Surface(feedback,SpriteAt("Assets/Art/GraphHUD/GraphObjectiveFrame.png"));
        var accentRect=Rect("StatusAccent",feedback,new Vector2(.028f,.15f),new Vector2(.037f,.85f));
        var accent=accentRect.gameObject.AddComponent<Image>();accent.color=new Color(.12f,.34f,.34f);accent.raycastTarget=false;
        var feedbackHeading=Label("FeedbackHeading",feedback,"TRY IT ON",.075f,.67f,.94f,.91f,24,false);
        feedbackHeading.color=accent.color;
        var message = Label("Feedback",feedback,"Preview only. Your equipped clothes stay unchanged.",.075f,.08f,.94f,.66f,24,true);
        message.enableAutoSizing=true;message.fontSizeMin=22;message.fontSizeMax=24;
        var secondary = Action("CancelOrClose",frame,"CLOSE",new Vector2(180,-330),false);
        var primary = Action("BuyOrConfirm",frame,"BUY OUTFIT",new Vector2(480,-330),true);
        var data = new SerializedObject(ui);
        Bind(data,"shop",shop); Bind(data,"overlay",overlay.gameObject); Bind(data,"card",frame);
        Bind(data,"characterImage",raw); Bind(data,"title",title); Bind(data,"itemName",name);
        Bind(data,"price",price); Bind(data,"balance",balance); Bind(data,"message",message);
        Bind(data,"currencyUnit",unit);Bind(data,"afterPurchase",after);Bind(data,"feedbackHeading",feedbackHeading);
        Bind(data,"currencyIcon",icon);Bind(data,"feedbackAccent",accent);Bind(data,"coinSprite",coin);Bind(data,"gemSprite",gem);
        Bind(data,"primary",primary); Bind(data,"secondary",secondary);
        Bind(data,"primaryLabel",primary.GetComponentInChildren<TMP_Text>());
        Bind(data,"secondaryLabel",secondary.GetComponentInChildren<TMP_Text>());
        Bind(data,"maleMannequin",source.FindProperty("maleMannequin").objectReferenceValue);
        Bind(data,"femaleMannequin",source.FindProperty("femaleMannequin").objectReferenceValue);
        var models=source.FindProperty("mannequinModels"); var targets=data.FindProperty("models"); targets.arraySize=models.arraySize;
        for(int i=0;i<models.arraySize;i++)
        {
            targets.GetArrayElementAtIndex(i).FindPropertyRelative("itemID").stringValue=models.GetArrayElementAtIndex(i).FindPropertyRelative("itemID").stringValue;
            targets.GetArrayElementAtIndex(i).FindPropertyRelative("model").objectReferenceValue=models.GetArrayElementAtIndex(i).FindPropertyRelative("model").objectReferenceValue;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        var manager=new SerializedObject(shop); Bind(manager,"previewUI",ui); manager.ApplyModifiedPropertiesWithoutUndo();
        var names=new System.Collections.Generic.Dictionary<string,string>
        {
            {"Common_Skin","Common Skin"},{"Elite_Skin","Elite Skin"},{"Rare_Skin","Rare Skin"},
            {"Epic_Skin","Epic Skin"},{"Special_Skin","Special Skin"},{"male_tuxedo","Tuxedo"},
            {"Common_Costume","Common Skin"},{"Elite_Costume","Elite Skin"},{"Rare_Costume","Rare Skin"},
            {"Epic_Costume","Epic Skin"},{"Special_Costume","Special Skin"},{"female_tuxedo","Tuxedo"}
        };
        var items=manager.FindProperty("allShopItems");
        for(int i=0;!updatingPreview&&i<items.arraySize;i++)
        {
            var item=(ShopItem)items.GetArrayElementAtIndex(i).objectReferenceValue; if(item==null)continue;
            var itemData=new SerializedObject(item);
            string label; if(!names.TryGetValue(item.ItemID,out label))label=item.ItemID.Replace('_',' ');
            itemData.FindProperty("displayName").stringValue=label;
            var button=(Button)itemData.FindProperty("buyButton").objectReferenceValue;
            if(button==null)continue;
            var art=Resources.LoadAll<Sprite>("StageUI/WoodButtonTeal")[0];
            itemData.FindProperty("availableSprite").objectReferenceValue=art;
            itemData.FindProperty("purchasedSprite").objectReferenceValue=art;
            itemData.ApplyModifiedPropertiesWithoutUndo();
            var buttonImage=button.GetComponent<Image>();buttonImage.sprite=art;buttonImage.type=Image.Type.Sliced;
            buttonImage.pixelsPerUnitMultiplier=4.5f;buttonImage.color=Color.white;
            ((RectTransform)button.transform).sizeDelta=new Vector2(220,88);
            var text=button.GetComponentInChildren<TMP_Text>(true);
            if(text==null)text=StageUITheme.Text("Label",button.transform,"PREVIEW",28,false);
            text.text="PREVIEW";text.font=StageUITheme.HeadingFont;text.fontSize=28;text.enableAutoSizing=false;
            text.color=new Color(1f,.95f,.83f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            StageUITheme.Stretch(text.rectTransform,Vector2.zero,Vector2.one);
            text.rectTransform.offsetMin=new Vector2(24,10);text.rectTransform.offsetMax=new Vector2(-24,-10);
            foreach(var existing in item.GetComponentsInChildren<TMP_Text>(true))
                if(existing!=text&&existing.name!="ItemPrice"&&!string.IsNullOrWhiteSpace(existing.text))existing.text=label;
        }
        overlay.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Main Menu shop preview and purchase confirmation saved. No gameplay scenes or prefab assets changed.");
    }

    private static void Bind(SerializedObject data,string field,UnityEngine.Object value) => data.FindProperty(field).objectReferenceValue=value;
    private static Sprite SpriteAt(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
        .OrderByDescending(sprite=>sprite.rect.width*sprite.rect.height).First();
    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var r=StageUITheme.Rect(name,parent);StageUITheme.Stretch(r,min,max);return r;
    }
    private static void Surface(RectTransform rect,Sprite sprite)
    {
        var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;
        image.pixelsPerUnitMultiplier=Mathf.Min(sprite.rect.width,sprite.rect.height)*.01f;image.raycastTarget=false;
    }
    private static TMP_Text Label(string name,Transform parent,string value,float x0,float y0,float x1,float y1,float size,bool reading)
    {
        // The shop preview uses IMPACT SDF for headings, values and body copy.
        var t=StageUITheme.Text(name,parent,value,size,false);t.font=StageUITheme.HeadingFont;
        t.color=StageUITheme.ReadingInk;t.richText=false;
        StageUITheme.Stretch(t.rectTransform,new Vector2(x0,y0),new Vector2(x1,y1));
        t.alignment=TextAlignmentOptions.MidlineLeft;t.textWrappingMode=TextWrappingModes.Normal;return t;
    }
    private static Button Action(string name,Transform parent,string label,Vector2 position,bool green)
    {
        var button=StageUITheme.Button(name,parent,label,null);
        var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
        rect.sizeDelta=new Vector2(280,88);rect.anchoredPosition=position;
        var image=button.GetComponent<Image>();image.sprite=Resources.LoadAll<Sprite>(green?"StageUI/WoodButtonGreen":"StageUI/WoodButtonRed")[0];
        image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4.5f;
        var text=button.GetComponentInChildren<TMP_Text>();text.font=StageUITheme.HeadingFont;text.fontSize=28;
        text.enableAutoSizing=false;text.color=new Color(1f,.95f,.83f);text.richText=false;
        text.rectTransform.offsetMin=new Vector2(24,10);text.rectTransform.offsetMax=new Vector2(-24,-10);
        button.navigation=new Navigation{mode=Navigation.Mode.None};return button;
    }
}
