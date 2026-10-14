using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Applies the user's scrollbar reference to Main Menu lists only.</summary>
public static class MenuScrollbarArtSetup
{
    private const string Path="Assets/Art/MenuScrollbar/MenuScrollbar.png";

    public static Sprite[] ImportSprites()
    {
        AssetDatabase.ImportAsset(Path);
        var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(Path));
        var pixels=texture.GetPixels32();
        var rects=new Rect[2];
        for(int part=0;part<2;part++)
        {
            int x0=part*texture.width/2,x1=(part+1)*texture.width/2;
            int left=x1,bottom=texture.height,right=x0,top=0;
            for(int y=0;y<texture.height;y++)
                for(int x=x0;x<x1;x++)
                    if(pixels[y*texture.width+x].a>8)
                    {left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
            if(right<=left||top<=bottom)throw new InvalidOperationException("Scrollbar sheet must contain separate left track and right thumb.");
            int l=Mathf.Max(x0,left-2),b=Mathf.Max(0,bottom-2);
            rects[part]=new Rect(l,b,Mathf.Min(x1,right+3)-l,Mathf.Min(texture.height,top+3)-b);
        }
        UnityEngine.Object.DestroyImmediate(texture);
        var importer=(TextureImporter)AssetImporter.GetAtPath(Path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;
        importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var previous=provider.GetSpriteRects();
        var names=new[]{"MenuScrollbarTrack","MenuScrollbarThumb"};
        var sprites=new SpriteRect[2];
        for(int i=0;i<2;i++)
        {
            var old=previous.FirstOrDefault(s=>s.name==names[i]);float width=rects[i].width;
            sprites[i]=new SpriteRect{name=names[i],rect=rects[i],alignment=SpriteAlignment.Center,pivot=Vector2.one*.5f,
                border=new Vector4(width*.30f,width*.52f,width*.30f,width*.52f),spriteID=old==null?GUID.Generate():old.spriteID};
        }
        provider.SetSpriteRects(sprites);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            sprites.Select(s=>new SpriteNameFileIdPair(s.name,s.spriteID)));
        provider.Apply();importer.SaveAndReimport();
        var assets=AssetDatabase.LoadAllAssetsAtPath(Path).OfType<Sprite>().ToArray();
        return names.Select(name=>assets.Single(s=>s.name==name)).ToArray();
    }

    public static void Style(Scrollbar bar,Sprite[] sprites)
    {
        var legacy=bar.transform.Find("WoodenRail");
        if(legacy!=null)UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        var track=bar.GetComponent<Image>();
        track.sprite=sprites[0];track.color=Color.white;track.type=Image.Type.Sliced;track.pixelsPerUnitMultiplier=2.5f;
        track.raycastPadding=new Vector4(-6,-4,-6,-4);
        var handle=bar.handleRect.GetComponent<Image>();
        bar.handleRect.anchoredPosition=Vector2.zero;
        bar.handleRect.sizeDelta=Vector2.zero;
        var outline=handle.GetComponent<Outline>();if(outline!=null)UnityEngine.Object.DestroyImmediate(outline);
        handle.sprite=sprites[1];handle.color=Color.white;handle.type=Image.Type.Sliced;handle.pixelsPerUnitMultiplier=3;
        handle.raycastPadding=new Vector4(-4,-4,-4,-4);
        bar.targetGraphic=handle;bar.transition=Selectable.Transition.None;
        bar.navigation=new Navigation{mode=Navigation.Mode.None};
    }

    [MenuItem("Logic Legends/Main Menu/Apply Reference Scrollbars")]
    public static void Apply()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Open Main Menu outside Play Mode first.");
        var sprites=ImportSprites();
        var transforms=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var achievements=transforms.Single(t=>t.name=="Achievements");
        var achievementScroll=achievements.GetComponentsInChildren<ScrollRect>(true).Single();
        achievementScroll.verticalScrollbar.GetComponent<RectTransform>().sizeDelta=new Vector2(36,0);
        Style(achievementScroll.verticalScrollbar,sprites);

        var customization=transforms.Single(t=>t!=null&&t.name=="Customization_Menu");
        var scroll=customization.GetComponentsInChildren<ScrollRect>(true).Single();
        var viewport=(RectTransform)scroll.transform;
        var bar=scroll.verticalScrollbar;
        if(bar==null)
        {
            var rect=StageUITheme.Rect("ClothesScrollbar",scroll.transform.parent);rect.gameObject.layer=5;
            rect.gameObject.AddComponent<Image>();bar=rect.gameObject.AddComponent<Scrollbar>();
            bar.direction=Scrollbar.Direction.BottomToTop;
            var area=StageUITheme.Rect("SlidingArea",rect);area.gameObject.layer=5;
            StageUITheme.Stretch(area,Vector2.zero,Vector2.one);area.offsetMin=new Vector2(5,5);area.offsetMax=new Vector2(-5,-5);
            var handle=StageUITheme.Rect("Handle",area);handle.gameObject.layer=5;
            StageUITheme.Stretch(handle,Vector2.zero,Vector2.one);handle.gameObject.AddComponent<Image>();
            bar.handleRect=handle;
        }
        // Place the track inside the parchment's right margin; keep the two-column grid intact.
        var track=(RectTransform)bar.transform;track.anchorMin=track.anchorMax=viewport.anchorMin;
        track.pivot=Vector2.one*.5f;track.localScale=Vector3.one;
        track.sizeDelta=new Vector2(32,viewport.rect.height-32);
        track.anchoredPosition=viewport.anchoredPosition+new Vector2(viewport.rect.width*.5f-16,0);
        Style(bar,sprites);
        scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        ConfigureCustomizationCards(scroll);
        Canvas.ForceUpdateCanvases();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Logic Legends/Main Menu/Fit Customization Cards Beside Scrollbar")]
    public static void FitCustomizationCards()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Open Main Menu outside Play Mode first.");
        var custom=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true))
            .Single(t=>t.name=="Customization_Menu");
        ConfigureCustomizationCards(custom.GetComponentsInChildren<ScrollRect>(true).Single());
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureCustomizationCards(ScrollRect scroll)
    {
        var viewport=scroll.viewport!=null?scroll.viewport:(RectTransform)scroll.transform;
        var grid=scroll.content.GetComponent<GridLayoutGroup>();
        grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;
        grid.spacing=new Vector2(12,grid.spacing.y);
        grid.padding=new RectOffset(8,56,grid.padding.top,grid.padding.bottom);
        float cellWidth=(viewport.rect.width-grid.padding.horizontal-grid.spacing.x)*.5f;
        grid.cellSize=new Vector2(cellWidth,grid.cellSize.y);
        foreach(Transform item in scroll.content)
        {
            var surface=item.Find("Image") as RectTransform;
            if(surface!=null)surface.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Min(302.4f,cellWidth-22.5f));
        }
        Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
    }
}
