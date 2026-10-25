using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Captions belong to the recorded actions, rather than an unrelated fixed slide timer.</summary>
public static class StageHelpVideoCatalog
{
    [Serializable] public sealed class Cue { public float seconds; public string caption; }
    [Serializable] public sealed class ClipGuide { public Cue[] cues; }
    [Serializable] public sealed class Recording
    {
        public string scene;
        public string source;
        public Cue[] cues;
        // Optional ordered clips; older recordings remain a single video with timed captions.
        public string[] clips;
        // Optional captions timed to individual sections within a clip.
        public ClipGuide[] guides;
    }
    static readonly Dictionary<string, Recording> cache = new Dictionary<string, Recording>();
    public static string ResourceKey(string scene)
    {
        switch (scene)
        {
            case "PRELIM": return "PRELIM";
            case "TruthTable": case "Truth_Table": return "TruthTable";
            case "RulesOfInference": return "RulesOfInference";
            case "Formal vs. Informal Proofs": case "Formal vs Informal": return "FormalProofs";
            case "Direct&Indirect": return "DirectProofs";
            case "Induction": return "Induction";
            case "Setssss": return "Sets";
            case "Functions and Relations": case "Function and Relations": return "FunctionsRelations";
            case "Graph": return "Graph";
            case "Trees": return "Trees";
            default: return scene;
        }
    }
    static Recording ForScene(string scene)
    {
        string key = ResourceKey(scene);
        Recording record;
        if (!cache.TryGetValue(key, out record))
        {
            var json = Resources.Load<TextAsset>("StageHelpVideos/" + key);
            record = json != null ? JsonUtility.FromJson<Recording>(json.text) : new Recording();
            if (record == null) record = new Recording();
            cache[key] = record;
        }
        return record;
    }
    public static int VideoCount(string scene)
    {
        var record = ForScene(scene);
        return record.clips != null && record.clips.Length > 0 ? record.clips.Length : 1;
    }
    public static string ClipResourceKey(string scene, int index)
    {
        var record = ForScene(scene);
        if (record.clips == null || record.clips.Length == 0) return ResourceKey(scene);
        string clip = record.clips[Mathf.Clamp(index, 0, record.clips.Length - 1)];
        return string.IsNullOrWhiteSpace(clip) ? ResourceKey(scene) : clip;
    }
    public static string CaptionAt(string scene, double playbackSeconds, int videoIndex = 0)
    {
        var record = ForScene(scene);
        string caption = "Gameplay demonstration";
        if (record.clips != null && record.clips.Length > 0)
        {
            if (record.cues != null && videoIndex >= 0 && videoIndex < record.cues.Length && record.cues[videoIndex] != null)
                caption = record.cues[videoIndex].caption;
            if (record.guides != null && videoIndex >= 0 && videoIndex < record.guides.Length && record.guides[videoIndex]?.cues != null)
                foreach (var cue in record.guides[videoIndex].cues)
                    if (cue != null && playbackSeconds >= cue.seconds) caption = cue.caption;
            return caption;
        }
        if (record.cues != null) foreach (var cue in record.cues)
            if (cue != null && playbackSeconds >= cue.seconds) caption = cue.caption;
        return caption;
    }
    public static IEnumerable<string> AllCaptions(string scene)
    {
        var record = ForScene(scene);
        if (record.cues != null) foreach (var cue in record.cues)
            if (cue != null && !string.IsNullOrEmpty(cue.caption)) yield return cue.caption;
        if (record.guides != null) foreach (var guide in record.guides)
            if (guide?.cues != null) foreach (var cue in guide.cues)
                if (cue != null && !string.IsNullOrEmpty(cue.caption)) yield return cue.caption;
    }
}
