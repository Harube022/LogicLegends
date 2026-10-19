using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuCharacterInspectSetup
{
    [MenuItem("Logic Legends/Main Menu/Add Character Inspection")]
    public static void Apply()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Open Main Menu outside Play Mode first.");
        var roots=scene.GetRootGameObjects();
        var custom=roots.SelectMany(root=>root.GetComponentsInChildren<CustomizationManager>(true)).Single();
        var preview=roots.SelectMany(root=>root.GetComponentsInChildren<ShopPreviewUI>(true)).Single();
        var camera=roots.SelectMany(root=>root.GetComponentsInChildren<Camera>(true))
            .Single(c=>c.targetTexture!=null&&c.targetTexture.name=="CharacterPreviewTexture");
        var customImage=custom.transform.Find("Character").GetComponent<RawImage>();
        var pd=new SerializedObject(preview);
        var shopImage=(RawImage)pd.FindProperty("characterImage").objectReferenceValue;
        var shopInspect=BindView(shopImage,camera,true);
        var customInspect=BindView(customImage,camera,false);
        pd.FindProperty("inspection").objectReferenceValue=shopInspect;pd.ApplyModifiedPropertiesWithoutUndo();
        var cd=new SerializedObject(custom);
        var scroll=(ScrollRect)cd.FindProperty("itemsScroll").objectReferenceValue;
        if(scroll==null)scroll=custom.GetComponentInChildren<ScrollRect>(true);
        var gate=scroll.verticalScrollbar.GetComponent<CanvasGroup>();
        if(gate==null)gate=scroll.verticalScrollbar.gameObject.AddComponent<CanvasGroup>();
        gate.alpha=0;gate.interactable=false;gate.blocksRaycasts=false;
        cd.FindProperty("inspection").objectReferenceValue=customInspect;
        cd.FindProperty("scrollbarLoadingGate").objectReferenceValue=gate;
        cd.ApplyModifiedPropertiesWithoutUndo();
        var caption=shopImage.transform.parent.parent.Find("PreviewCaption").GetComponent<TMP_Text>();
        caption.text="DRAG TO ROTATE";
        var hint=customImage.transform.Find("RotateHint") as RectTransform;
        if(hint==null)
        {
            var row=new GameObject("RotateHint",typeof(RectTransform));
            hint=(RectTransform)row.transform;hint.SetParent(customImage.transform,false);
        }
        var oldText=hint.GetComponent<TMP_Text>();
        if(oldText!=null)UnityEngine.Object.DestroyImmediate(oldText);
        hint.anchorMin=hint.anchorMax=new Vector2(.5f,0);hint.pivot=new Vector2(.5f,0);
        hint.anchoredPosition=new Vector2(0,28);hint.sizeDelta=new Vector2(340,44);
        var layout=hint.GetComponent<HorizontalLayoutGroup>();
        if(layout==null)layout=hint.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment=TextAnchor.MiddleCenter;layout.spacing=10;layout.childControlWidth=false;
        layout.childControlHeight=false;layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;

        var iconTransform=hint.Find("RotationIcon") as RectTransform;
        if(iconTransform==null)
        {
            var iconObject=new GameObject("RotationIcon",typeof(RectTransform));
            iconTransform=(RectTransform)iconObject.transform;iconTransform.SetParent(hint,false);
        }
        iconTransform.sizeDelta=new Vector2(36,36);
        var icon=iconTransform.GetComponent<RotationHintIcon>();if(icon==null)icon=iconTransform.gameObject.AddComponent<RotationHintIcon>();
        icon.color=new Color(1f,.72f,.24f);icon.raycastTarget=false;
        var iconLayout=iconTransform.GetComponent<LayoutElement>();if(iconLayout==null)iconLayout=iconTransform.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth=36;iconLayout.preferredHeight=36;

        var labelTransform=hint.Find("RotateHintText") as RectTransform;
        if(labelTransform==null)
        {
            var labelObject=new GameObject("RotateHintText",typeof(RectTransform));
            labelTransform=(RectTransform)labelObject.transform;labelTransform.SetParent(hint,false);
        }
        labelTransform.sizeDelta=new Vector2(260,42);
        var text=labelTransform.GetComponent<TMP_Text>();if(text==null)text=labelTransform.gameObject.AddComponent<TextMeshProUGUI>();
        text.text="SWIPE TO ROTATE";text.font=StageUITheme.HeadingFont;text.fontSize=20;
        text.color=new Color(1,.95f,.83f);text.outlineColor=new Color(.10f,.06f,.03f);
        text.outlineWidth=.12f;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
        var labelLayout=labelTransform.GetComponent<LayoutElement>();if(labelLayout==null)labelLayout=labelTransform.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth=260;labelLayout.preferredHeight=42;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    private static CharacterInspectView BindView(RawImage image,Camera camera,bool fit)
    {
        image.raycastTarget=true;image.uvRect=new Rect(0,0,1,1);
        var view=image.GetComponent<CharacterInspectView>();if(view==null)view=image.gameObject.AddComponent<CharacterInspectView>();
        var data=new SerializedObject(view);data.FindProperty("previewCamera").objectReferenceValue=camera;
        data.FindProperty("fitCamera").boolValue=fit;
        data.FindProperty("framingPadding").floatValue=1.08f;
        data.ApplyModifiedPropertiesWithoutUndo();return view;
    }
}
