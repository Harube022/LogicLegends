using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Short, original mono earcons. No runtime synthesis, extra mixers or gameplay events.</summary>
    public static class ArchiveIndicatorAudioSetup
    {
        public const string Folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/LightingAudio/Indicators";
        static readonly string[] Names = { "HeartLost", "CriticalHealth", "TeammateDown", "TeamPing", "HuntTargeted", "TopicStarted", "TopicComplete", "TimerWarning", "ConnectionLost", "ConnectionRestored", "IncorrectAnswer" };
        static readonly float[] Lengths = { .36f, .95f, .70f, .48f, .90f, .44f, 1.05f, .66f, .52f, .60f, .37f };
        [MenuItem("Tools/Logic Legends/Open Archive/Audio/Install Gameplay Indicator Sounds")]
        public static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.isDirty || scene.path != "Assets/Scenes/MultiplayerGameplay.unity") throw new InvalidOperationException("Use the saved MultiplayerGameplay scene in Edit Mode.");
            var audio = UnityEngine.Object.FindFirstObjectByType<ArchiveAudioLandscape>();
            if (audio == null) throw new InvalidOperationException("The existing archive audio presenter is required.");
            Directory.CreateDirectory(Folder);
            for (int i = 0; i < Names.Length; i++) Write(Folder + "/Archive_Cue_" + Names[i] + ".wav", i, Lengths[i]);
            AssetDatabase.Refresh();
            var clips = new AudioClip[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                string path = Folder + "/Archive_Cue_" + Names[i] + ".wav";
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                importer.forceToMono = true;
                var sample = importer.defaultSampleSettings; sample.loadType = AudioClipLoadType.DecompressOnLoad;
                sample.compressionFormat = AudioCompressionFormat.PCM; sample.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                sample.sampleRateOverride = 22050; importer.defaultSampleSettings = sample; importer.SaveAndReimport();
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clips[i] == null) throw new InvalidOperationException("Audio import failed: " + path);
            }
            Undo.RecordObject(audio, "Assign archive gameplay cues");
            audio.indicators = new ArchiveIndicatorSounds { heartLost = clips[0], criticalHealth = clips[1], teammateDown = clips[2], teamPing = clips[3],
                huntTargeted = clips[4], topicStarted = clips[5], topicComplete = clips[6], timerWarning = clips[7], connectionLost = clips[8], connectionRestored = clips[9],
                interfaceTap = audio.equip, incorrectAnswer = clips[10] };
            audio.indicatorVolume = .38f; EditorUtility.SetDirty(audio);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        static double Bell(double time, double frequency, double strength = 1)
        {
            if (time < 0) return 0;
            double attack = 1 - Math.Exp(-time * 240);
            return strength * attack * (Math.Sin(time * frequency * Math.PI * 2) * Math.Exp(-time * 7) +
                .26 * Math.Sin(time * frequency * 2.01 * Math.PI * 2) * Math.Exp(-time * 13) +
                .10 * Math.Sin(time * frequency * 3.98 * Math.PI * 2) * Math.Exp(-time * 22));
        }
        static double Pulse(double time, double frequency, double strength = 1)
        {
            if (time < 0) return 0;
            return strength * (1 - Math.Exp(-time * 300)) * Math.Exp(-time * 27) *
                (Math.Sin(time * frequency * Math.PI * 2) + .30 * Math.Sin(time * frequency * 2.02 * Math.PI * 2));
        }
        static double Sound(int kind, double t)
        {
            switch (kind)
            {
                case 0: return Pulse(t, 105, .75) + Pulse(t - .04, 72, .35) + Bell(t, 245, .13);
                case 1: return Pulse(t, 78) + Pulse(t - .16, 64, .72) + Pulse(t - .52, 78, .80) + Pulse(t - .68, 64, .55);
                case 2: return Bell(t, 440, .6) + Bell(t - .17, 330, .7) + Bell(t - .34, 220, .8);
                case 3: return Bell(t, 880, .70) + Bell(t - .13, 1174.66, .55);
                case 4: return Pulse(t, 82, .8) + Bell(t, 196, .40) + Bell(t - .23, 207.65, .3) + Pulse(t - .48, 82, .6);
                case 5: return Bell(t, 659.25, .60) + Bell(t - .11, 880, .45);
                case 6: return Bell(t, 523.25, .6) + Bell(t - .14, 659.25, .6) + Bell(t - .28, 783.99, .6) + Bell(t - .42, 1046.5, .38);
                case 7: return Bell(t, 440, .55) + Pulse(t, 120, .20) + Bell(t - .24, 440, .48) + Pulse(t - .24, 120, .18);
                case 8: return Bell(t, 523.25, .5) + Bell(t - .15, 349.23, .6);
                case 9: return Bell(t, 523.25, .5) + Bell(t - .15, 783.99, .6);
                default: return Bell(t, 369.99, .5) + Pulse(t, 185, .28) + Bell(t - .105, 261.63, .54) + Pulse(t - .105, 130.81, .32);
            }
        }
        static void Write(string path, int kind, float length)
        {
            const int rate = 22050; int count = Mathf.CeilToInt(length * rate);
            var samples = new double[count]; double peak = .001;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / rate;
                double fade = Math.Min(1, Math.Min(t / .006, (length - t) / .035));
                samples[i] = Sound(kind, t) * Math.Max(0, fade); peak = Math.Max(peak, Math.Abs(samples[i]));
            }
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                foreach (double value in samples) writer.Write((short)Math.Round(value / peak * .68 * short.MaxValue));
            }
        }
    }
}
