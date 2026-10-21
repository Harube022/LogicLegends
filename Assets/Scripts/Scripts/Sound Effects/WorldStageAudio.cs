using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One music player for PRELIM, Setssss, Rules of Inference, and Graph. Stage selection
/// can change inside PRELIM without a scene load, so the player follows both.
/// </summary>
public sealed class WorldStageAudio : MonoBehaviour
{
    public static WorldStageAudio Instance { get; private set; }
    public static bool HandlesActiveStage => Instance != null && Instance.stageNumber >= 0;

    private WorldStageAudioClips clips;
    private AudioSource musicSource;
    private AudioSource feedbackSource;
    private AudioSource completionSource;
    private int sceneHandle = -1;
    private int stageNumber = -1;
    private int lastTrack = -1;
    private double trackEndTime;
    private bool completionPlayed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null)
            new GameObject("World Stage Audio").AddComponent<WorldStageAudio>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        clips = Resources.Load<WorldStageAudioClips>("Audio/WorldStageAudioClips");
        if (clips == null) Debug.LogError("WorldStageAudioClips is missing from Resources/Audio.", this);
        musicSource = MakeSource("Music", GameAudioChannel.Music);
        feedbackSource = MakeSource("Answer Feedback", GameAudioChannel.SoundFX);
        completionSource = MakeSource("Stage Completion", GameAudioChannel.SoundFX);
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private AudioSource MakeSource(string name, GameAudioChannel channel)
    {
        var source = new GameObject(name).AddComponent<AudioSource>();
        source.transform.SetParent(transform, false);
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        AudioVolumeSettings.Route(source, channel);
        return source;
    }

    private static int ActiveStage(Scene scene)
    {
        if (scene.name == "PRELIM")
            return StageSelectionState.SelectedStage == 1 || StageSelectionState.SelectedStage == 2
                ? StageSelectionState.SelectedStage : -1;
        if (scene.name == "TruthTable") return 2;
        if (scene.name == "RulesOfInference") return 3;
        if (scene.name == "Setssss") return 4;
        if (scene.name == "Graph") return 9;
        if (scene.name == "Induction") return 6;
        return -1;
    }

    private void Update()
    {
        Scene scene = SceneManager.GetActiveScene();
        int nextStage = ActiveStage(scene);
        if (sceneHandle != scene.handle || stageNumber != nextStage)
        {
            musicSource.Stop();
            feedbackSource.Stop();
            completionSource.Stop();
            sceneHandle = scene.handle;
            stageNumber = nextStage;
            completionPlayed = false;
            trackEndTime = 0;
        }
        if (stageNumber < 0 || completionPlayed || clips == null ||
            clips.musicTracks == null || clips.musicTracks.Length == 0 ||
            Player.LocalInstance == null || !Player.LocalInstance.gameObject.activeInHierarchy)
            return;

        if (!musicSource.isPlaying && AudioSettings.dspTime >= trackEndTime)
            PlayNextTrack();
    }

    private void PlayNextTrack()
    {
        int available = 0;
        foreach (AudioClip clip in clips.musicTracks) if (clip != null) available++;
        if (available == 0) return;
        int selected = Random.Range(0, available);
        if (available > 1 && lastTrack >= 0)
        {
            // Select among all other tracks, never the one that just finished.
            selected = Random.Range(0, available - 1);
            if (selected >= lastTrack) selected++;
        }
        lastTrack = selected;
        int nonNullIndex = 0;
        foreach (AudioClip clip in clips.musicTracks)
        {
            if (clip == null) continue;
            if (nonNullIndex++ != selected) continue;
            musicSource.clip = clip;
            musicSource.volume = clips.musicVolume;
            musicSource.Play();
            trackEndTime = AudioSettings.dspTime + clip.length;
            break;
        }
    }

    public static bool PlayAnswerFeedback(bool correct)
    {
        if (Instance == null || Instance.stageNumber < 0 || Instance.clips == null) return false;
        AudioClip clip = correct ? Instance.clips.success : Instance.clips.error;
        if (clip == null) return false;
        Instance.feedbackSource.PlayOneShot(clip, Instance.clips.feedbackVolume);
        return true;
    }

    public static void PlayStageCompletion()
    {
        if (Instance == null || Instance.stageNumber < 0 || Instance.completionPlayed) return;
        Instance.completionPlayed = true;
        Instance.musicSource.Stop();
        if (Instance.clips != null && Instance.clips.stageComplete != null)
            Instance.completionSource.PlayOneShot(Instance.clips.stageComplete,
                Instance.clips.completionVolume);
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.handle == sceneHandle) musicSource.Stop();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Instance = null;
    }
}
