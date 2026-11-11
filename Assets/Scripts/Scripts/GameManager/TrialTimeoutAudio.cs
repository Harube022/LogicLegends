using UnityEngine;

/// <summary>Plays the shared game-over cue for timed trial failures.</summary>
public static class TrialTimeoutAudio
{
    private const string ResourcePath = "Audio/chisper-8-bit-game-over-634762";
    private static AudioClip clip;

    public static bool Play(float volume = 1f)
    {
        if (clip == null) clip = Resources.Load<AudioClip>(ResourcePath);
        if (clip == null)
        {
            Debug.LogWarning("The trial timeout sound could not be loaded from Resources/" + ResourcePath + ".");
            return false;
        }

        var audioObject = new GameObject("TrialTimeoutAudio");
        var source = audioObject.AddComponent<AudioSource>();
        AudioVolumeSettings.Route(source, GameAudioChannel.SoundFX);
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.Play();
        Object.Destroy(audioObject, clip.length + .1f);
        return true;
    }
}
