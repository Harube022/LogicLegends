using System;
using UnityEngine;

/// <summary>Device preferences shared by the Main Menu and the ten solo trials.</summary>
[Serializable]
public sealed class SoloSettingsData
{
    public int mobilePerformanceVersion = 1;
    public int graphicsQuality = 0, frameRate = 30, shadowQuality = 0, lightingQuality = 0;
    public bool fog, motionBlur, mute, leftHanded;
    public int resolution = 720, textureQuality, specialEffects;
    public bool bloom;

    public void SelectGraphicsPreset(int tier)
    {
        graphicsQuality = Mathf.Clamp(tier, 0, 2);
        resolution = graphicsQuality == 2 ? 1080 : 720;
        textureQuality = specialEffects = graphicsQuality;
        shadowQuality = graphicsQuality == 2 ? 3 : graphicsQuality;
        fog = bloom = graphicsQuality == 2;
    }
    public float horizontalSensitivity = 1, verticalSensitivity = 1, zoomSensitivity = 1;
    public float buttonSize = 1, buttonOpacity = .8f, textSize = 1;
    public float music = 1, soundFX = 1;
    public bool highContrast = true, colorIndependentCues = true;
    public SoloSettingsData Copy() => JsonUtility.FromJson<SoloSettingsData>(JsonUtility.ToJson(this));
    public void Validate()
    {
        graphicsQuality = Mathf.Clamp(graphicsQuality, 0, 2); lightingQuality = Mathf.Clamp(lightingQuality, 0, 2);
        shadowQuality = Mathf.Clamp(shadowQuality, 0, 3); frameRate = frameRate == 60 ? 60 : 30;
        resolution = resolution == 1080 ? 1080 : 720;
        textureQuality = Mathf.Clamp(textureQuality, 0, 2);
        specialEffects = Mathf.Clamp(specialEffects, 0, 2);
        horizontalSensitivity = Clamp(horizontalSensitivity, .25f, 2, 1); verticalSensitivity = Clamp(verticalSensitivity, .25f, 2, 1);
        zoomSensitivity = Clamp(zoomSensitivity, .25f, 2, 1); buttonSize = Clamp(buttonSize, .8f, 1.2f, 1);
        buttonOpacity = Clamp(buttonOpacity, .35f, 1, .8f); textSize = Clamp(textSize, .8f, 1.25f, 1);
        music = Clamp(music, 0, 1, 1); soundFX = Clamp(soundFX, 0, 1, 1);
    }
    static float Clamp(float value, float low, float high, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, low, high);
}

public static class SoloGameSettings
{
    public const string PreferenceKey = "LogicLegends.SoloSettings.v1";
    static SoloSettingsData current;
    public static event Action Changed;
    public static SoloSettingsData Current
    {
        get
        {
            if (current != null) return current;
            string saved = PlayerPrefs.GetString(PreferenceKey, "");
            try { current = JsonUtility.FromJson<SoloSettingsData>(saved); } catch (Exception) { }
            if (current == null)
            {
                current = new SoloSettingsData();
                current.music = AudioVolumeSettings.MusicVolume; current.soundFX = AudioVolumeSettings.SoundFXVolume;
            }
            else if (!saved.Contains("\"mobilePerformanceVersion\"") || current.mobilePerformanceVersion < 1)
            {
                // One-time graphics-only migration from the earlier expensive
                // defaults. Preserve the player's audio, controls and reading choices.
                current.mobilePerformanceVersion = 1;
                current.graphicsQuality = current.lightingQuality = current.shadowQuality = 0;
                current.frameRate = 30; current.motionBlur = false;
                if (Application.isPlaying)
                { PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(current)); PlayerPrefs.Save(); }
            }
            if (!saved.Contains("\"resolution\"") && !string.IsNullOrEmpty(saved))
            {
                var migrated = new SoloSettingsData();
                migrated.SelectGraphicsPreset(current.graphicsQuality);
                current.resolution = migrated.resolution;
                current.textureQuality = migrated.textureQuality;
                current.specialEffects = migrated.specialEffects;
                current.bloom = migrated.bloom;
            }
            current.Validate(); return current;
        }
    }
    public static bool IsTrial(string scene)
    {
        for (int trial = 1; trial <= TrialCatalog.Count; trial++) if (TrialCatalog.Get(trial).Scene == scene) return true;
        return false;
    }
    public static bool InTrial => IsTrial(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    public static Vector2 LookMultiplier => InTrial ? new Vector2(Current.horizontalSensitivity, Current.verticalSensitivity) : Vector2.one;
    public static float ZoomMultiplier => InTrial ? Current.zoomSensitivity : 1;
    public static bool LeftHanded => InTrial && Current.leftHanded;
    public static void Apply(SoloSettingsData value)
    {
        current = value.Copy(); current.Validate();
        PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(current));
        AudioVolumeSettings.SetMusicVolume(current.music); AudioVolumeSettings.SetSoundFXVolume(current.soundFX);
        AudioListener.volume = current.mute ? 0 : 1;
        AudioVolumeSettings.Save(); PlayerPrefs.Save(); Changed?.Invoke();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { current = null; Changed = null; }
}
