using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public enum GameAudioChannel { Music, SoundFX }

/// <summary>Shared saved volume controls; mixer gains preserve each source's own volume and fades.</summary>
public sealed class AudioVolumeSettings : MonoBehaviour
{
    public const string MusicPreference = "LogicLegends.MusicVolume";
    public const string SoundFXPreference = "LogicLegends.SoundFXVolume";
    public static event Action Changed;
    private static AudioVolumeSettings instance;
    private static AudioMixer mixer;
    private static AudioMixerGroup musicGroup, soundFXGroup;
    private static bool loaded, dirty;
    private static float musicVolume = 1f, soundFXVolume = 1f;
    public static float MusicVolume { get { Load(); return musicVolume; } }
    public static float SoundFXVolume { get { Load(); return soundFXVolume; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        instance = null; mixer = null; musicGroup = null; soundFXGroup = null;
        loaded = false; dirty = false; Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Load();
        new GameObject("AudioVolumeSettings").AddComponent<AudioVolumeSettings>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += SceneLoaded;
    }

    private void Start() { Apply(); }
    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
                if (source.outputAudioMixerGroup == null) Route(source, GameAudioChannel.SoundFX);
    }

    private static void Load()
    {
        if (loaded) return;
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicPreference, 1f));
        soundFXVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SoundFXPreference, 1f));
        loaded = true;
        mixer = Resources.Load<AudioMixer>("Audio/LogicLegendsAudio");
        if (mixer != null)
        {
            musicGroup = Array.Find(mixer.FindMatchingGroups("Music"), group => group.name == "Music");
            soundFXGroup = Array.Find(mixer.FindMatchingGroups("SFX"), group => group.name == "SFX");
        }
    }

    public static void Route(AudioSource source, GameAudioChannel channel)
    {
        if (source == null) return;
        Load();
        source.outputAudioMixerGroup = channel == GameAudioChannel.Music ? musicGroup : soundFXGroup;
    }

    public static void SetMusicVolume(float value)
    {
        Load(); musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicPreference, musicVolume); UpdateSettings();
    }
    public static void SetSoundFXVolume(float value)
    {
        Load(); soundFXVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SoundFXPreference, soundFXVolume); UpdateSettings();
    }
    private static void UpdateSettings() { dirty = true; Apply(); Changed?.Invoke(); }
    private static void Apply()
    {
        Load();
        if (mixer == null) return;
        mixer.SetFloat("MusicVolume", ToDecibels(musicVolume));
        mixer.SetFloat("SFXVolume", ToDecibels(soundFXVolume));
    }
    public static float ToDecibels(float volume) => volume <= 0f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(volume));
    public static void Save()
    {
        if (!dirty) return;
        PlayerPrefs.Save(); dirty = false;
    }
    private void OnApplicationPause(bool paused) { if (paused) Save(); }
    private void OnApplicationQuit() { Save(); }
    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= SceneLoaded;
        Save(); instance = null;
    }
}