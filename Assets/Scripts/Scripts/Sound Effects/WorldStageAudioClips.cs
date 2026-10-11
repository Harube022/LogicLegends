using UnityEngine;

/// <summary>Shared WORLDBGM references for the four learning stages.</summary>
[CreateAssetMenu(menuName = "Logic Legends/World Stage Audio Clips")]
public sealed class WorldStageAudioClips : ScriptableObject
{
    public AudioClip[] musicTracks;
    public AudioClip success;
    public AudioClip error;
    public AudioClip stageComplete;
    [Range(0f, 1f)] public float musicVolume = 0.35f;
    [Range(0f, 1f)] public float feedbackVolume = 0.7f;
    [Range(0f, 1f)] public float completionVolume = 0.85f;
}
