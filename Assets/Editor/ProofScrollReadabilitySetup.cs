#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class ProofScrollReadabilitySetup
{
    const string Folder="Assets/Resources/ProofScrollText";
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Resources","ProofScrollText");
        var font=StageUITheme.ReadingFont;
        var shader=Shader.Find("TextMeshPro/Mobile/Distance Field");
        if(font==null||shader==null)throw new InvalidOperationException("Existing reading font/mobile SDF shader missing.");
        var text=new Material(font.material){name="Proof Scroll Neon Reading",shader=shader};
        text.SetColor("_FaceColor",new Color(.68f,1f,.94f,1));text.SetFloat("_FaceDilate",.025f);
        text.SetColor("_OutlineColor",new Color(.012f,.045f,.055f,1));text.SetFloat("_OutlineWidth",.16f);text.SetFloat("_OutlineSoftness",0);
        text.SetColor("_UnderlayColor",new Color(.03f,.9f,.76f,.35f));text.SetFloat("_UnderlayDilate",.11f);text.SetFloat("_UnderlaySoftness",.2f);
        text.SetFloat("_UnderlayOffsetX",0);text.SetFloat("_UnderlayOffsetY",0);
        text.EnableKeyword("OUTLINE_ON");text.EnableKeyword("UNDERLAY_ON");
        Save(text,Folder+"/NeonReading.mat");
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FormalInformalProofs/Library_Parchment.mat");
        var paper=new Material(source){name="Proof Scroll Parchment"};
        foreach(var property in new[]{"_BaseColor","_Color"})if(paper.HasProperty(property)){var c=source.GetColor(property);paper.SetColor(property,new Color(c.r*.65f,c.g*.65f,c.b*.65f,c.a));}
        if(paper.HasProperty("_Smoothness"))paper.SetFloat("_Smoothness",.12f);
        Save(paper,Folder+"/Parchment.mat");
        // The pedestal prefab is shared with Direct&Indirect. Formal's scoped
        // OnEnable/SetContent hook applies this treatment to each scene instance.
        AssetDatabase.SaveAssets();
    }
    static void Save(Material value,string path)
    {
        var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(existing==null)AssetDatabase.CreateAsset(value,path);
        else{EditorUtility.CopySerialized(value,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(value);}
    }
}
#endif
