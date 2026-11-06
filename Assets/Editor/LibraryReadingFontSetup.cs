#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Bakes the library's missing mathematical glyphs without editing the game's shared font.</summary>
public static class LibraryReadingFontSetup
{
    public static void Bake()
    {
        const string folder="Assets/Resources/StudyLibrary/";
        if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(folder+"LibraryReadingFont.asset")!=null)return;
        var original=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        string text=string.Join("\n",new[]{"PRELIM","MIDTERMS","PRE-FINALS","FINALS"}.SelectMany(StudyLibraryCatalog.GetShelf)
            .SelectMany(b=>b.lessonBlocks!=null ? b.lessonBlocks.Select(p=>(p.heading??"")+"\n"+(p.text??"")) : b.lessonPages.Select(p=>p.text??"")));
        var printable=new System.Text.StringBuilder();
        for(int i=0;i<text.Length;i++)
        {
            var category=System.Globalization.CharUnicodeInfo.GetUnicodeCategory(text,i);
            int count=char.IsSurrogatePair(text,i)?2:1;
            if(category!=System.Globalization.UnicodeCategory.Control&&category!=System.Globalization.UnicodeCategory.Format)printable.Append(text,i,count);
            i+=count-1;
        }
        uint[] missing;original.HasCharacters(printable.ToString(),out missing,true,false);
        var math=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/NotoSansMath-Regular.ttf"),
            (int)original.faceInfo.pointSize,original.atlasPadding,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,false);
        math.name="Library Math Fallback";
        UnityEngine.TextCore.LowLevel.FontEngine.LoadFontFace(math.sourceFontFile,(int)original.faceInfo.pointSize);
        var supported=missing.Distinct().Where(c=>{uint index;return UnityEngine.TextCore.LowLevel.FontEngine.TryGetGlyphIndex(c,out index)&&index!=0;}).ToArray();
        // This TMP version's uint overload expects UTF-16 units, then combines
        // surrogate pairs internally. Passing UTF-32 values truncates math letters.
        var units=string.Concat(supported.Select(c=>char.ConvertFromUtf32((int)c))).Select(c=>(uint)c).ToArray();
        uint[] unavailable;
        math.TryAddCharacters(units,out unavailable);
        if(unavailable!=null&&unavailable.Length>0)throw new InvalidOperationException("Math font lacks: "+string.Join(",",unavailable.Select(c=>"U+"+c.ToString("X"))));
        math.atlasPopulationMode=AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(math,folder+"LibraryMathFallback.asset");
        AssetDatabase.AddObjectToAsset(math.material,math);foreach(var atlas in math.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,math);
        var reading=UnityEngine.Object.Instantiate(original);reading.name="Library Reading Font";
        reading.material=UnityEngine.Object.Instantiate(original.material);reading.material.name="Library Reading Font Material";
        reading.fallbackFontAssetTable=new List<TMP_FontAsset>(original.fallbackFontAssetTable){math};
        AssetDatabase.CreateAsset(reading,folder+"LibraryReadingFont.asset");AssetDatabase.AddObjectToAsset(reading.material,reading);
        EditorUtility.SetDirty(reading);EditorUtility.SetDirty(math);
        AssetDatabase.SaveAssetIfDirty(reading);AssetDatabase.SaveAssetIfDirty(math);
        Debug.Log("Library math fallback baked: "+supported.Length+" glyphs in one 1024 atlas. Existing source symbols without a matching project glyph: "+string.Join(",",missing.Distinct().Except(supported).Select(c=>"U+"+c.ToString("X"))));
    }
}
#endif
