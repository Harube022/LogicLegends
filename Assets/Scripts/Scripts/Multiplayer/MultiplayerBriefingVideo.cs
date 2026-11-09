using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Local video presentation for the lobby guide; never touches party, readiness or match state.</summary>
public sealed class MultiplayerBriefingVideo : MonoBehaviour
{
    public static readonly string[] ClipKeys = { "Multiplayer_Topics", "Multiplayer_Teamwork", "Multiplayer_Guardians", "Multiplayer_RescueEscape" };
    VideoPlayer player;
    RawImage image;
    TMP_Text caption, status;
    Button playback, replay;
    RenderTexture texture;
    VideoClip expected;
    int page;
    bool wantsPlayback, visible;
    public VideoPlayer Player => player;
    public Button PlaybackButton => playback;
    public Button ReplayButton => replay;
    public string Caption => caption != null ? caption.text : "";
    public bool HasRecording => expected != null;

    public void Build(RectTransform host)
    {
        if (player != null) return;
        var surface = host.gameObject.AddComponent<Image>(); surface.color = new Color(.05f, .20f, .21f, .09f); surface.raycastTarget = false;
        var heading = MultiplayerUIArt.Label("Watch_Label", host, "WATCH HOW IT WORKS", .04f, .88f, .96f, .99f, 23, false, new Color(.04f, .32f, .33f));
        heading.alignment = TextAlignmentOptions.MidlineLeft;
        var screen = MultiplayerUIArt.Rect("Video_Frame", host, .035f, .34f, .965f, .87f);
        screen.gameObject.AddComponent<Image>().color = new Color(.015f, .03f, .04f);
        var picture = MultiplayerUIArt.Rect("Recorded_Gameplay", screen);
        image = picture.gameObject.AddComponent<RawImage>(); image.color = Color.white; image.raycastTarget = false;
        var aspect = picture.gameObject.AddComponent<AspectRatioFitter>(); aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 16f / 9f;
        status = MultiplayerUIArt.Label("Video_Status", screen, "Loading demonstration…", .06f, .25f, .94f, .75f, 24, true, Color.white);
        status.alignment = TextAlignmentOptions.Center;
        caption = MultiplayerUIArt.Label("Video_Action_Caption", host, "", .045f, .155f, .955f, .33f, 24, true, StageUITheme.ReadingInk);
        caption.alignment = TextAlignmentOptions.Center;
        playback = MultiplayerUIArt.Action("Pause_Demonstration", host, "Pause", .04f, .025f, .47f, .145f);
        replay = MultiplayerUIArt.Action("Replay_Demonstration", host, "Replay", .53f, .025f, .96f, .145f);
        playback.onClick.AddListener(Toggle); replay.onClick.AddListener(Replay);
        player = picture.gameObject.AddComponent<VideoPlayer>(); player.playOnAwake = false; player.waitForFirstFrame = true;
        player.audioOutputMode = VideoAudioOutputMode.None; player.isLooping = true; player.skipOnDrop = true;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime; player.renderMode = VideoRenderMode.RenderTexture;
        player.prepareCompleted += Prepared; player.errorReceived += Failed;
    }
    public void Show(int index)
    {
        if (player == null) return;
        page = Mathf.Clamp(index, 0, ClipKeys.Length - 1); visible = wantsPlayback = true;
        player.Stop(); expected = Resources.Load<VideoClip>("StageHelpVideos/" + ClipKeys[page]); player.clip = expected;
        if (texture == null)
        {
            texture = new RenderTexture(960, 540, 0) { name = "Multiplayer How To Play" }; texture.Create();
        }
        var previous = RenderTexture.active; RenderTexture.active = texture; GL.Clear(true, true, Color.black); RenderTexture.active = previous;
        player.targetTexture = texture; image.texture = texture;
        caption.text = StageHelpVideoCatalog.CaptionAt(ClipKeys[page], 0);
        status.gameObject.SetActive(true); status.text = expected != null ? "Loading demonstration…" : "Recording unavailable. You can still read the guide.";
        playback.interactable = replay.interactable = expected != null; PlaybackLabel();
        if (expected != null) player.Prepare();
    }
    void Prepared(VideoPlayer ready)
    {
        if (!visible || !isActiveAndEnabled || ready.clip != expected) return;
        status.gameObject.SetActive(false); if (wantsPlayback) ready.Play(); PlaybackLabel();
    }
    void Failed(VideoPlayer source, string message)
    {
        if (!visible) return;
        wantsPlayback = false; status.gameObject.SetActive(true); status.text = "Tap Replay to reload the demonstration."; PlaybackLabel();
    }
    void Update()
    {
        if (visible && player != null && player.isPrepared)
            caption.text = StageHelpVideoCatalog.CaptionAt(ClipKeys[page], player.time);
    }
    void Toggle()
    {
        if (!visible || expected == null) return;
        wantsPlayback = !wantsPlayback;
        if (player.isPrepared) { if (wantsPlayback) player.Play(); else player.Pause(); }
        PlaybackLabel();
    }
    void Replay() { if (visible) Show(page); }
    void PlaybackLabel() { playback.GetComponentInChildren<TMP_Text>().text = wantsPlayback ? "Pause" : "Play"; }
    public void Hide()
    {
        visible = wantsPlayback = false;
        if (player != null) { player.Stop(); player.clip = null; player.targetTexture = null; }
        if (image != null) image.texture = null;
        if (texture != null) { texture.Release(); Destroy(texture); texture = null; }
        expected = null;
    }
    void OnDisable() => Hide();
    void OnDestroy() { Hide(); if (player != null) { player.prepareCompleted -= Prepared; player.errorReceived -= Failed; } }
}
