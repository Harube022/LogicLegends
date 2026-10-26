using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Extends the saved optional side decks; does not rebuild topic or environment assets.</summary>
    public static class ArchiveCoopActivitySetup
    {
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Install Topic Specific Optional Mechanics")]
        public static void Install() => InstallAll();
        public static void InstallAll(bool save = true)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before editing the optional activity setup.");
            EnsureMathGlyphs();
            var session = UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();
            if (session == null || session.stations == null || session.stations.Length != 10) throw new InvalidOperationException("Open the saved MultiplayerGameplay scene.");
            const string art = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/";
            Material Mat(string path) => AssetDatabase.LoadAssetAtPath<Material>(art + path);
            foreach (var station in session.stations.Where(s => s.topic > 0))
            {
                var legacy = station.GetComponent<ArchiveT1CoopTrial>();
                if (legacy == null) throw new InvalidOperationException("Existing side deck is missing at T" + (station.topic + 1));
                legacy.enabled = false;
                foreach (var child in station.GetComponentsInChildren<Transform>(true))
                {
                    string name = child.name;
                    if (name == "Coop_P_Rune" || name == "Coop_Q_Rune" || name.StartsWith("Coop_Rune_Gold_", StringComparison.Ordinal) || name.StartsWith("Coop_Input_", StringComparison.Ordinal) ||
                        name.StartsWith("Coop_Gate_", StringComparison.Ordinal) || name == "Coop_Logic_Gate" || name.StartsWith("Coop_Pulse_Rune_", StringComparison.Ordinal)) child.gameObject.SetActive(false);
                }
                var view = station.GetComponent<ArchiveCoopActivityView>(); if (view == null) view = station.gameObject.AddComponent<ArchiveCoopActivityView>();
                view.station = station; view.stoneMaterial = Mat("Materials/Archive_Topic_Stone.mat"); view.brassMaterial = Mat("Materials/Archive_Topic_BrassInlay.mat");
                view.tealMaterial = Mat("Coop_Teal.mat"); view.amberMaterial = Mat("Coop_Amber.mat"); view.redMaterial = Mat("Coop_Pulse.mat"); view.sign = legacy.sign; view.backSign = legacy.backSign;
                var a = ArchiveCoopActivity.Build(station.topic, 0);
                string text = "T" + (station.topic + 1) + " · OPTIONAL\n" + a.title + "\nActivate the main Book first.\nCloses when this topic completes.";
                if (view.sign != null) view.sign.text = text; if (view.backSign != null) view.backSign.text = text;
                EditorUtility.SetDirty(view); EditorUtility.SetDirty(legacy); EditorUtility.SetDirty(station);
            }
            var scene = session.gameObject.scene; UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            if (save) { UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); }
        }
        public static void EnsureMathGlyphs()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Bake fonts outside Play Mode.");
            const string root="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/";
            var reading=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(root+"Archive_T7_Reading.asset");
            if (reading==null || reading.HasCharacter((char)0x2205,true,false)) return;
            string path=root+"Archive_EmptySet_SDF.asset";
            var symbols=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if(symbols==null)
            {
                var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/NotoSansMath-Regular.ttf");
                symbols=TMP_FontAsset.CreateFontAsset(source,64,9,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,128,128,AtlasPopulationMode.Dynamic,false);
                symbols.name="Archive_EmptySet_SDF";
                if(!symbols.TryAddCharacters("\u2205",out string missing)||!string.IsNullOrEmpty(missing))throw new InvalidOperationException("Cannot bake the empty-set notation.");
                symbols.atlasPopulationMode=AtlasPopulationMode.Static;
                AssetDatabase.CreateAsset(symbols,path);AssetDatabase.AddObjectToAsset(symbols.material,symbols);
                foreach(var atlas in symbols.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,symbols);
                EditorUtility.SetDirty(symbols);AssetDatabase.SaveAssetIfDirty(symbols);
            }
            reading.fallbackFontAssetTable.Add(symbols);EditorUtility.SetDirty(reading);AssetDatabase.SaveAssetIfDirty(reading);
        }
    }
}
