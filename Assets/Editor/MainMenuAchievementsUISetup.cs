using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authors a visual-only achievement list. No progress or reward callbacks are installed.</summary>
public static class MainMenuAchievementsUISetup
{
    private const string Art = "Assets/Assets/Interface_Sprites/achievements_interface/";

    [MenuItem("Logic Legends/Main Menu/Build Achievement UI Only")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Open Main Menu outside Play Mode first.");
        var menu = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Single(t => t.name == "Achievements");
        var background = menu.Find("Backgorund") as RectTransform;
        if (background.Find("AchievementList") != null)
            throw new InvalidOperationException("The achievement UI already exists. Edit its scene objects directly.");
        // Keep the existing illustrated board and navigation; replace only the empty list.
        var legacy = background.Find("Scroll Menu");
        if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        background.localScale = Vector3.one;
        StageUITheme.Stretch(background, new Vector2(.16f,.04f), new Vector2(.84f,.96f));
        background.GetComponent<Image>().raycastTarget = false;
        background.GetComponent<Image>().preserveAspect = false;
        var sub = Label("Subtitle",background,"CONQUER TRIALS. EARN YOUR REWARDS.",
            new Vector2(.09f,.755f),new Vector2(.91f,.80f),24);
        sub.alignment = TextAlignmentOptions.Center;
        sub.color = new Color(.42f,.28f,.15f);

        var viewport = Rect("AchievementList",background,new Vector2(.09f,.105f),new Vector2(.91f,.745f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.gameObject.AddComponent<Image>();
        hit.color = Color.clear; hit.raycastTarget = true;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45; scroll.inertia = true; scroll.decelerationRate = .08f;
        scroll.viewport = viewport;
        var content = Rect("Content",viewport,new Vector2(0,1),new Vector2(1,1));
        content.pivot = new Vector2(.5f,1);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16; layout.padding = new RectOffset(4,4,4,4);
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;

        // Proposed rewards are editable scene labels only. All cards start unclaimed.
        Card(content,"FirstTrial","FIRST STEPS","Complete your first trial.","0 / 1 TRIAL",50,0);
        Card(content,"TruthTable","TRUTH SEEKER","Complete the Truth Table trial.","0 / 1 TRIAL",75,0);
        Card(content,"RulesOfInference","INFERENCE ADEPT","Complete the Rules of Inference trial.","0 / 1 TRIAL",100,0);
        Card(content,"TheAwakening","THE AWAKENING","Conquer Trials 1–3.","0 / 3 TRIALS",150,5);
        Card(content,"TheAscent","THE ASCENT","Conquer Trials 4–5.","0 / 2 TRIALS",200,5);
        Card(content,"TheReckoning","THE RECKONING","Conquer Trials 6–8.","0 / 3 TRIALS",250,10);
        Card(content,"TheLegendsRise","THE LEGEND'S RISE","Conquer Trials 9–10.","0 / 2 TRIALS",300,10);
        Card(content,"AllTrials","LOGIC LEGEND","Conquer all ten trials.","0 / 10 TRIALS",500,20);

        ApplyApprovedLayout();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.verticalNormalizedPosition=1;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Logic Legends/Main Menu/Apply Approved Achievement Background")]
    public static void ApplyApprovedLayout()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!="Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Open Main Menu outside Play Mode first.");
        const string panelPath="Assets/Art/Achievements/AchievementsPanel.png";
        AssetDatabase.ImportAsset(panelPath);
        var importer=(TextureImporter)AssetImporter.GetAtPath(panelPath);
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100;
        importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;
        importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        var menu=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true))
            .Single(t=>t.name=="Achievements");
        var background=(RectTransform)menu.Find("Backgorund");
        background.localScale=Vector3.one;
        StageUITheme.Stretch(background,new Vector2(.13f,.06f),new Vector2(.94f,.94f));
        var art=background.GetComponent<Image>();
        art.sprite=SpriteAt(panelPath);art.type=Image.Type.Simple;art.preserveAspect=false;art.raycastTarget=false;
        foreach(var name in new[]{"Subtitle","ScrollHint","Heading","ColumnHeaders","AchievementsScrollbar"})
        {
            var old=background.Find(name);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
        var heading=Label("Heading",background,"ACHIEVEMENTS",new Vector2(.30f,.855f),new Vector2(.70f,.955f),62);
        heading.alignment=TextAlignmentOptions.Center;heading.color=new Color(1,.94f,.78f);
        heading.outlineColor=new Color(.20f,.10f,.04f);heading.outlineWidth=.12f;
        var columns=Rect("ColumnHeaders",background,new Vector2(.065f,.775f),new Vector2(.88f,.825f));
        Label("Achievement",columns,"ACHIEVEMENT",new Vector2(.045f,0),new Vector2(.50f,1),25);
        Label("Reward",columns,"REWARD",new Vector2(.54f,0),new Vector2(.735f,1),25);
        var status=Label("Status",columns,"STATUS",new Vector2(.755f,0),new Vector2(.98f,1),25);
        status.alignment=TextAlignmentOptions.Center;
        foreach(var label in columns.GetComponentsInChildren<TMP_Text>())label.color=new Color(.43f,.28f,.15f);
        var divider=Rect("Divider",columns,new Vector2(0,0),new Vector2(1,0));
        divider.sizeDelta=new Vector2(0,2);
        var line=divider.gameObject.AddComponent<Image>();line.color=new Color(.43f,.28f,.15f,.55f);line.raycastTarget=false;

        var viewport=(RectTransform)background.Find("AchievementList");
        StageUITheme.Stretch(viewport,new Vector2(.065f,.14f),new Vector2(.88f,.76f));
        viewport.pivot=new Vector2(.5f,1);
        var scroll=viewport.GetComponent<ScrollRect>();
        var content=scroll.content;
        content.GetComponent<VerticalLayoutGroup>().spacing=10;
        foreach(Transform row in content)
        {
            var layout=row.GetComponent<LayoutElement>();layout.preferredHeight=layout.minHeight=138;
            SetText(row,"Title",new Vector2(.045f,.67f),new Vector2(.51f,.94f),28);
            SetText(row,"Objective",new Vector2(.045f,.32f),new Vector2(.51f,.67f),26);
            SetText(row,"Progress",new Vector2(.045f,.06f),new Vector2(.51f,.30f),20);
            var reward=row.Find("RewardPreview");
            StageUITheme.Stretch((RectTransform)reward,new Vector2(.54f,.07f),new Vector2(.735f,.93f));
            reward.Find("RewardLabel").gameObject.SetActive(false);
            bool gems=reward.Find("Gems")!=null;
            StageUITheme.Stretch((RectTransform)reward.Find("Coins"),new Vector2(0,gems?.47f:.17f),new Vector2(1,gems?.95f:.83f));
            if(gems)StageUITheme.Stretch((RectTransform)reward.Find("Gems"),new Vector2(0,.02f),new Vector2(1,.48f));
            foreach(var amount in reward.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name=="Amount"))amount.fontSize=26;
            var states=row.Find("ClaimStates");
            StageUITheme.Stretch((RectTransform)states,new Vector2(.755f,.08f),new Vector2(.97f,.92f));
            foreach(Transform state in states)
            {
                var button=(RectTransform)state.Find("Button");
                button.anchorMin=button.anchorMax=new Vector2(.5f,.62f);button.anchoredPosition=Vector2.zero;
                button.sizeDelta=new Vector2(220,64);
                SetText(state,"Status",new Vector2(-.04f,.01f),new Vector2(1.04f,.25f),20);
            }
        }

        // This is a native Scrollbar: dragging the thumb or track moves the existing ScrollRect.
        var track=Rect("AchievementsScrollbar",background,new Vector2(.915f,.14f),new Vector2(.915f,.76f));
        track.pivot=new Vector2(.5f,1);track.sizeDelta=new Vector2(36,0);
        var trackArt=track.gameObject.AddComponent<Image>();
        trackArt.color=Color.clear;
        var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
        var sliding=Rect("SlidingArea",track,Vector2.zero,Vector2.one);
        sliding.offsetMin=new Vector2(6,6);sliding.offsetMax=new Vector2(-6,-6);
        var handle=Rect("Handle",sliding,Vector2.zero,Vector2.one);
        var handleArt=handle.gameObject.AddComponent<Image>();
        bar.handleRect=handle;bar.targetGraphic=handleArt;bar.transition=Selectable.Transition.None;
        bar.navigation=new Navigation{mode=Navigation.Mode.None};
        scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
        scroll.verticalScrollbarSpacing=12;
        MenuScrollbarArtSetup.Style(bar,MenuScrollbarArtSetup.ImportSprites());
        var responsive=background.GetComponent<MainMenuAchievementsLayout>();
        if(responsive==null)responsive=background.gameObject.AddComponent<MainMenuAchievementsLayout>();
        var binding=new SerializedObject(responsive);
        binding.FindProperty("viewport").objectReferenceValue=viewport;
        binding.FindProperty("scroll").objectReferenceValue=scroll;
        binding.ApplyModifiedPropertiesWithoutUndo();
        Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        responsive.RefreshLayout();
        scroll.StopMovement();scroll.verticalNormalizedPosition=1;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    private static void SetText(Transform parent,string name,Vector2 min,Vector2 max,float size)
    {
        var text=parent.Find(name).GetComponent<TMP_Text>();StageUITheme.Stretch(text.rectTransform,min,max);
        text.font=StageUITheme.HeadingFont;text.fontSize=size;text.enableAutoSizing=false;
    }

    private static void Card(Transform parent,string id,string title,string goal,string progress,int coins,int gems)
    {
        var row = Rect(id+"_Achievement",parent,Vector2.zero,Vector2.one);
        var layout = row.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight=200;layout.minHeight=200;
        // Compensate for transparent padding in the supplied container sprite, without changing its asset.
        var surface=Rect("Container",row,new Vector2(-.045f,-.20f),new Vector2(1.055f,1.20f));
        var image=surface.gameObject.AddComponent<Image>();image.sprite=SpriteAt(Art+"achievements_container.png");
        image.type=Image.Type.Simple;image.raycastTarget=false;
        // Content columns leave space for long objectives without covering the reward or button.
        var titleText=Label("Title",row,title,new Vector2(.045f,.68f),new Vector2(.49f,.89f),30);
        titleText.color=new Color(.13f,.30f,.30f);
        Label("Objective",row,goal,new Vector2(.045f,.32f),new Vector2(.49f,.68f),28);
        var progressText=Label("Progress",row,progress,new Vector2(.045f,.10f),new Vector2(.49f,.29f),22);
        progressText.color=new Color(.43f,.32f,.23f);
        var reward=Rect("RewardPreview",row,new Vector2(.51f,.13f),new Vector2(.71f,.88f));
        var caption=Label("RewardLabel",reward,"REWARD",new Vector2(0,.69f),new Vector2(1,1),21);
        caption.color=new Color(.43f,.32f,.23f);
        Reward(reward,"Coins",coins,"Assets/Art/ProfileCurrency/Coin.png",gems>0?.33f:.15f,gems>0?.68f:.65f);
        if(gems>0)Reward(reward,"Gems",gems,"Assets/Art/ProfileCurrency/Gem.png",0,.33f);

        var states=Rect("ClaimStates",row,new Vector2(.72f,.10f),new Vector2(.98f,.90f));
        ClaimState(states,"NotYetClaimed",Art+"btn_not_claim.png","NOT YET CLAIMED",false,true);
        ClaimState(states,"Claim",Art+"btn_claim.png","READY TO CLAIM",true,false);
        ClaimState(states,"Claimed",Art+"btn_claimed.png","REWARD CLAIMED",false,false);
    }

    private static void Reward(Transform parent,string name,int amount,string iconPath,float y0,float y1)
    {
        var reward=Rect(name,parent,new Vector2(0,y0),new Vector2(1,y1));
        var icon=Rect("Icon",reward,new Vector2(0,.04f),new Vector2(.28f,.96f)).gameObject.AddComponent<Image>();
        icon.sprite=SpriteAt(iconPath);icon.preserveAspect=true;icon.raycastTarget=false;
        Label("Amount",reward,amount+" "+name.ToUpperInvariant(),new Vector2(.33f,0),new Vector2(1,1),24);
    }

    private static void ClaimState(Transform parent,string name,string spritePath,string status,bool interactable,bool visible)
    {
        var state=Rect(name,parent,Vector2.zero,Vector2.one);
        var buttonRect=Rect("Button",state,new Vector2(.5f,.58f),new Vector2(.5f,.58f));
        buttonRect.sizeDelta=new Vector2(280,88);
        var image=buttonRect.gameObject.AddComponent<Image>();image.sprite=SpriteAt(spritePath);
        image.type=Image.Type.Simple;image.preserveAspect=true;image.pixelsPerUnitMultiplier=4.5f;
        var button=buttonRect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        button.interactable=interactable;button.transition=Selectable.Transition.None;
        button.navigation=new Navigation{mode=Navigation.Mode.None};
        // Button text is already painted into the supplied sprites. No duplicate overlay label.
        var label=Label("Status",state,status,new Vector2(-.06f,.02f),new Vector2(1.06f,.29f),21);
        label.alignment=TextAlignmentOptions.Center;
        label.color=interactable?new Color(.15f,.38f,.20f):new Color(.43f,.32f,.23f);
        state.gameObject.SetActive(visible);
    }

    private static Sprite SpriteAt(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
        .OrderByDescending(s=>s.rect.width*s.rect.height).First();
    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var rect=StageUITheme.Rect(name,parent);rect.gameObject.layer=LayerMask.NameToLayer("UI");
        StageUITheme.Stretch(rect,min,max);return rect;
    }
    private static TMP_Text Label(string name,Transform parent,string value,Vector2 min,Vector2 max,float size)
    {
        var text=StageUITheme.Text(name,parent,value,size,false);text.gameObject.layer=LayerMask.NameToLayer("UI");
        StageUITheme.Stretch(text.rectTransform,min,max);text.color=StageUITheme.ReadingInk;
        text.alignment=TextAlignmentOptions.MidlineLeft;text.richText=false;text.enableAutoSizing=false;
        text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;return text;
    }
}
