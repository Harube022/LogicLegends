using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveMonsterBankSetup
    {
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Bake Monster Math Symbols")]
        public static void EnsureMathGlyphs()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Bake mathematical fonts outside Play Mode.");
            string root=ArchiveTopicSetup.Folder+"/Art/";
            var reading=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(root+"Archive_T7_Reading.asset");
            if(reading==null)throw new InvalidOperationException("The existing Archive reading font is missing.");
            string text=string.Concat(Enumerable.Range(0,ArchiveMonsterChallengeBank.Count).Select(i=>
            {
                var q=ArchiveMonsterChallengeBank.Get(i);
                return q.prompt+q.explanation+string.Join(" ",q.options)+string.Join(" ",q.hints);
            }));
            string missing=new string(text.Where(c=>!char.IsControl(c)&&!reading.HasCharacter(c,true,false)).Distinct().ToArray());
            if(missing.Length==0)return;
            string path=root+"Archive_Monster_Math_SDF.asset";
            var symbols=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            bool create=symbols==null;
            if(create)
            {
                var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/NotoSansMath-Regular.ttf");
                symbols=TMP_FontAsset.CreateFontAsset(source,64,9,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,256,256,AtlasPopulationMode.Dynamic,false);
                symbols.name="Archive_Monster_Math_SDF";
            }
            else symbols.atlasPopulationMode=AtlasPopulationMode.Dynamic;
            if(!symbols.TryAddCharacters(missing,out string unbaked)||!string.IsNullOrEmpty(unbaked))
                throw new InvalidOperationException("Cannot bake monster math glyphs: "+unbaked);
            symbols.atlasPopulationMode=AtlasPopulationMode.Static;
            if(create)
            {
                AssetDatabase.CreateAsset(symbols,path);AssetDatabase.AddObjectToAsset(symbols.material,symbols);
                foreach(var atlas in symbols.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,symbols);
            }
            if(!reading.fallbackFontAssetTable.Contains(symbols))reading.fallbackFontAssetTable.Add(symbols);
            EditorUtility.SetDirty(symbols);AssetDatabase.SaveAssetIfDirty(symbols);
            EditorUtility.SetDirty(reading);AssetDatabase.SaveAssetIfDirty(reading);
        }
    }
}
