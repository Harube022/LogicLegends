using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveAdaptiveMusicSetup
    {
        public const string Folder=ArchiveLightingAudioSetup.Folder+"/Music";
        static AudioClip Clip(string mood)=>AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Archive_Score_"+mood+".wav");
        public static bool AssignIfAvailable(ArchiveAudioLandscape audio)
        {
            var day=Clip("Daylight");var afternoon=Clip("Afternoon");var evening=Clip("Evening");var night=Clip("Night");var hunt=Clip("Hunt");
            if(audio==null||day==null||afternoon==null||evening==null||night==null||hunt==null)return false;
            audio.dayBed=day;audio.afternoonMusic=afternoon;audio.eveningMusic=evening;audio.nightBed=night;audio.huntMusic=hunt;
            audio.dayVolume=.26f;audio.nightVolume=.29f;audio.musicCrossfadeSeconds=3.5f;audio.huntCrossfadeSeconds=1.2f;audio.huntReleaseSeconds=2.5f;
            EditorUtility.SetDirty(audio);return true;
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Audio/Install Adaptive World Music")]
        public static void Install()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty||scene.path!="Assets/Scenes/MultiplayerGameplay.unity")throw new InvalidOperationException("Use the saved MultiplayerGameplay scene in Edit Mode.");
            foreach(var path in Directory.GetFiles(Folder,"*.wav"))
            {
                var asset=path.Replace('\\','/');AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
                var importer=(AudioImporter)AssetImporter.GetAtPath(asset);var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.68f;
                settings.sampleRateSetting=AudioSampleRateSetting.OverrideSampleRate;settings.sampleRateOverride=24000;
                importer.forceToMono=false;importer.loadInBackground=true;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            }
            var audio=UnityEngine.Object.FindFirstObjectByType<ArchiveAudioLandscape>();
            if(!AssignIfAvailable(audio))throw new InvalidOperationException("Five score clips and the existing audio presenter are required.");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
