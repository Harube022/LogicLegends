using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Shared, single-page field guide. The stage still owns modal input and timer state.</summary>
[DefaultExecutionOrder(10000)]
public sealed class StageHelpPage : MonoBehaviour
{
    RectTransform host, page, reading, media, videoRect, footer;
    TMP_Text title, body, caption, status, hint;
    ScrollRect scroll;
    Button beginButton, playbackButton, replayButton;
    VideoPlayer player;
    RawImage video;
    RenderTexture texture;
    string sceneKey;
    Action closeAction, beginAction;
    bool exiting;
    bool wantsPlayback = true, preparingVideo;
    int currentVideoIndex;
    Vector2 lastSize;
    readonly Vector3[] parentCorners = new Vector3[4];
    public ScrollRect ReadingScroll => scroll;
    public VideoPlayer GameplayVideo => player;
    // Kept for Editor tooling compiled against the old guide. No Close/X is created.
    public Button CloseButton => null;
    public Button BeginButton => beginButton;
    public int CurrentVideoIndex => currentVideoIndex;
    public int VideoCount => StageHelpVideoCatalog.VideoCount(sceneKey);

    public static StageHelpPage Install(RectTransform existingCard, string scene, Action close, Action begin = null, bool onboardingPending = false)
    {
        var view = existingCard.GetComponent<StageHelpPage>();
        if (view == null)
        {
            view = existingCard.gameObject.AddComponent<StageHelpPage>();
            view.host = existingCard;
            view.sceneKey = scene;
            view.Build();
        }
        view.closeAction = close;
        // The stage owns restoring its modal snapshot before beginning practice.
        // Reopened Help always restores play, even when a tutorial callback exists.
        view.beginAction = onboardingPending && begin != null ? begin : close;
        view.exiting = false;
        view.beginButton.gameObject.SetActive(true);
        view.HideLegacyChildren();
        view.lastSize = Vector2.zero;
        view.Layout();
        view.scroll.StopMovement();
        view.scroll.verticalNormalizedPosition = 1;
        return view;
    }

    void Build()
    {
        var frame = host.GetComponent<Image>();
        if (frame != null) { frame.sprite = StageUITheme.Panel; frame.type = Image.Type.Sliced; frame.color = Color.white; frame.pixelsPerUnitMultiplier = 2.2f; }
        page = StageUITheme.Rect("Shared Scrollable Help", host);
        StageUITheme.Stretch(page, Vector2.zero, Vector2.one);
        title = Label("Guide Title", page, StageHelpContent.TitleForScene(sceneKey), 40, StageUITheme.Gold, true);

        reading = StageUITheme.Rect("Parchment Guide", page);
        Paper(reading);
        var scrolling = StageUITheme.Rect("Guide Scroll", reading);
        StageUITheme.Stretch(scrolling, Vector2.zero, Vector2.one);
        scrolling.offsetMin = new Vector2(26, 48); scrolling.offsetMax = new Vector2(-24, -52);
        scroll = scrolling.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45;
        var viewport = StageUITheme.Rect("Viewport", scrolling);
        StageUITheme.Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.offsetMax = new Vector2(-28, 0);
        var hit = viewport.gameObject.AddComponent<Image>(); hit.color = Color.white;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var content = StageUITheme.Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true; layout.childControlWidth = true;
        layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
        layout.padding = new RectOffset(0, 12, 0, 20);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        body = Label("Guide Sections", content, StageHelpContent.ForScene(sceneKey), 32, StageUITheme.ReadingInk);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.richText = true; body.lineSpacing = 4; body.paragraphSpacing = 8;
        scroll.viewport = viewport; scroll.content = content;
        var barRect = StageUITheme.Rect("Reading Scrollbar", scrolling);
        barRect.anchorMin = new Vector2(1, 0); barRect.anchorMax = Vector2.one;
        barRect.pivot = new Vector2(1, .5f); barRect.sizeDelta = new Vector2(22, 0); barRect.anchoredPosition = Vector2.zero;
        barRect.gameObject.AddComponent<Image>().color = new Color(.25f, .14f, .08f, .12f);
        var handle = StageUITheme.Rect("Thumb", barRect); StageUITheme.Stretch(handle, Vector2.zero, Vector2.one);
        var handleArt = handle.gameObject.AddComponent<Image>(); handleArt.color = new Color(.36f, .24f, .11f, .65f);
        var bar = barRect.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle; bar.targetGraphic = handleArt; bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        hint = Label("Reading Hint", reading, "Swipe or scroll to read", 23, StageUITheme.ReadingInk);
        hint.alignment = TextAlignmentOptions.Center;

        media = StageUITheme.Rect("Gameplay Demonstration", page);
        Paper(media);
        videoRect = StageUITheme.Rect("Recorded Gameplay", media);
        video = videoRect.gameObject.AddComponent<RawImage>(); video.color = Color.black; video.raycastTarget = false;
        videoRect.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        videoRect.GetComponent<AspectRatioFitter>().aspectRatio = 16f / 9f;
        var videoHost = StageUITheme.Rect("Video Frame", media);
        videoRect.SetParent(videoHost, false); StageUITheme.Stretch(videoRect, Vector2.zero, Vector2.one);
        status = Label("Playback Status", videoHost, "Loading gameplay…", 28, Color.white);
        StageUITheme.Stretch(status.rectTransform, new Vector2(.05f, .25f), new Vector2(.95f, .75f));
        status.alignment = TextAlignmentOptions.Center;
        caption = Label("Action Caption", media, "", 28, StageUITheme.ReadingInk);
        caption.alignment = TextAlignmentOptions.Center;
        playbackButton = ActionButton("Pause Gameplay Video", media, "Pause", TogglePlayback);
        replayButton = ActionButton("Replay Gameplay Video", media, "Replay", Replay);
        player = video.gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false; player.isLooping = false; player.waitForFirstFrame = true;
        player.skipOnDrop = true; player.audioOutputMode = VideoAudioOutputMode.None;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.prepareCompleted += Prepared; player.errorReceived += PlaybackError;
        player.loopPointReached += ClipFinished;
        footer = StageUITheme.Rect("Guide Actions", page);
        beginButton = ActionButton("BeginButton", footer, "Let’s Begin", ExitGuide);
        Layout(); StartVideo();
    }

    void ExitGuide()
    {
        if (exiting) return;
        exiting = true; // A second touch must not create another tutorial or overwrite a modal snapshot.
        (beginAction ?? closeAction)?.Invoke();
    }

    void HideLegacyChildren()
    {
        // Keep serialized references alive; only the obsolete multi-page presentation is retired.
        foreach (Transform child in host) if (child != page && child.gameObject.activeSelf) child.gameObject.SetActive(false);
    }
    void OnEnable() { if (page != null) { lastSize = Vector2.zero; scroll.verticalNormalizedPosition = 1; StartVideo(); } }
    void LateUpdate()
    {
        if (page == null) return;
        HideLegacyChildren(); Layout();
        if (player != null && player.isPrepared)
            caption.text = StageHelpVideoCatalog.CaptionAt(sceneKey, player.time, currentVideoIndex);
    }
    void Layout()
    {
        if (host == null || page == null) return;
        FitSafeArea();
        host.localScale = Vector3.one;
        Vector2 size = host.rect.size;
        if (size == lastSize) return;
        lastSize = size;
        bool stacked = size.x < size.y * 1.2f || size.x < 1000;
        float inset = Mathf.Clamp(size.x * .025f, 18, 40);
        float header = 94, bottom = 96;
        SetRect(title.rectTransform, inset, size.y - header, size.x - 2 * inset, 72);
        SetRect(footer, inset, 16, size.x - 2 * inset, bottom - 18);
        SetRect((RectTransform)beginButton.transform, Mathf.Max(0, footer.rect.width - 280), 0, 280, 76);
        float available = size.y - header - bottom;
        // Reserve enough space for the longest action caption without shrinking mobile text.
        float leftWidth = (size.x - inset * 2 - 24) * .54f;
        float captionWidth = stacked ? size.x - 2 * inset : size.x - 2 * inset - leftWidth - 24;
        caption.fontSize = stacked ? 26 : 28;
        float captionHeight = 84;
        foreach (string actionCaption in StageHelpVideoCatalog.AllCaptions(sceneKey))
            captionHeight = Mathf.Max(captionHeight, caption.GetPreferredValues(
                actionCaption, Mathf.Max(1, captionWidth - 40), 0).y + 16);
        float mediaFooter = captionHeight + 96;
        if (stacked)
        {
            float videoHeight = Mathf.Min(available * .56f, (size.x - 2 * inset) * 9f / 16f + mediaFooter);
            SetRect(media, inset, bottom, size.x - 2 * inset, videoHeight);
            SetRect(reading, inset, bottom + videoHeight + 16, size.x - 2 * inset, available - videoHeight - 16);
        }
        else
        {
            SetRect(reading, inset, bottom, leftWidth, available);
            SetRect(media, inset + leftWidth + 24, bottom, size.x - 2 * inset - leftWidth - 24, available);
        }
        var frame = (RectTransform)videoRect.parent;
        float mediaWidth = media.rect.width, mediaHeight = media.rect.height;
        float clipHeight = Mathf.Min(mediaWidth * 9f / 16f - 24, mediaHeight - mediaFooter);
        SetRect(frame, 16, mediaHeight - clipHeight - 18, mediaWidth - 32, Mathf.Max(80, clipHeight));
        SetRect(caption.rectTransform, 20, 78, mediaWidth - 40, captionHeight);
        SetRect((RectTransform)playbackButton.transform, mediaWidth * .5f - 160, 14, 150, 54);
        SetRect((RectTransform)replayButton.transform, mediaWidth * .5f + 10, 14, 150, 54);
        SetRect(hint.rectTransform, 26, 8, reading.rect.width - 52, 28);
        body.fontSize = stacked ? 31 : 32;
        title.fontSize = stacked ? 36 : 40;
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
    }
    void FitSafeArea()
    {
        var parent = host.parent as RectTransform;
        if (parent == null) return;
        var canvas = host.GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        parent.GetWorldCorners(parentCorners);
        Vector2 lower = RectTransformUtility.WorldToScreenPoint(camera, parentCorners[0]);
        Vector2 upper = RectTransformUtility.WorldToScreenPoint(camera, parentCorners[2]);
        Vector2 dimensions = upper - lower;
        var safe = Screen.safeArea;
        if (safe.width <= 0 || safe.height <= 0 || safe.xMax > Screen.width + 1 || safe.yMax > Screen.height + 1)
            safe = new Rect(0, 0, Screen.width, Screen.height);
        Vector2 min = new Vector2(Mathf.Clamp01((safe.xMin - lower.x) / Mathf.Max(1, dimensions.x)), Mathf.Clamp01((safe.yMin - lower.y) / Mathf.Max(1, dimensions.y)));
        Vector2 max = new Vector2(Mathf.Clamp01((safe.xMax - lower.x) / Mathf.Max(1, dimensions.x)), Mathf.Clamp01((safe.yMax - lower.y) / Mathf.Max(1, dimensions.y)));
        Vector2 padding = Vector2.Scale(max - min, new Vector2(.025f, .035f));
        StageUITheme.Stretch(host, min + padding, max - padding);
    }
    static void SetRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(Mathf.Max(1, width), Mathf.Max(1, height));
    }
    static void Paper(RectTransform rect)
    {
        var image = rect.gameObject.AddComponent<Image>(); image.sprite = StageUITheme.Paper;
        image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 2.5f; image.raycastTarget = false;
    }
    static TMP_Text Label(string name, Transform parent, string text, float size, Color color, bool heading = false)
    {
        var label = StageUITheme.Text(name, parent, text, size);
        label.font = heading ? StageUITheme.HeadingFont : StageUITheme.ReadingFont;
        label.fontSize = size; label.enableAutoSizing = false; label.color = color;
        label.raycastTarget = false; label.outlineWidth = 0;
        return label;
    }
    static Button ActionButton(string name, Transform parent, string text, UnityEngine.Events.UnityAction action)
    {
        var button = StageUITheme.Button(name, parent, text, action);
        button.GetComponent<Image>().pixelsPerUnitMultiplier = 4.5f;
        return button;
    }
    void StartVideo()
    {
        if (player == null || !isActiveAndEnabled) return;
        wantsPlayback = true;
        if (texture == null)
        {
            texture = new RenderTexture(960, 540, 0) { name = "Help Gameplay Preview" };
            texture.Create(); video.texture = texture; player.targetTexture = texture;
        }
        LoadVideo(0, true);
    }
    void LoadVideo(int index, bool showLoading)
    {
        player.Stop();
        currentVideoIndex = Mathf.Clamp(index, 0, VideoCount - 1);
        player.isLooping = VideoCount == 1;
        player.clip = Resources.Load<VideoClip>("StageHelpVideos/" + StageHelpVideoCatalog.ClipResourceKey(sceneKey, currentVideoIndex));
        caption.text = StageHelpVideoCatalog.CaptionAt(sceneKey, 0, currentVideoIndex);
        preparingVideo = false;
        if (player.clip == null)
        {
            status.gameObject.SetActive(true); status.text = "Gameplay recording unavailable";
            SetPlaybackLabel("Play"); return;
        }
        // The preceding clip ends on black. Keep that frame during playlist preparation.
        status.gameObject.SetActive(showLoading); status.text = "Loading gameplay…";
        preparingVideo = true;
        SetPlaybackLabel(wantsPlayback ? "Pause" : "Play");
        player.Prepare();
    }
    void Prepared(VideoPlayer ready)
    {
        if (!isActiveAndEnabled) return;
        preparingVideo = false; status.gameObject.SetActive(false); video.color = Color.white;
        if (wantsPlayback) ready.Play();
        SetPlaybackLabel(wantsPlayback ? "Pause" : "Play");
    }
    void ClipFinished(VideoPlayer finished)
    {
        if (!isActiveAndEnabled || VideoCount == 1) return;
        LoadVideo((currentVideoIndex + 1) % VideoCount, false);
    }
    void PlaybackError(VideoPlayer source, string message)
    {
        preparingVideo = false; status.gameObject.SetActive(true); status.text = "Unable to play recording. Tap Replay.";
        SetPlaybackLabel("Play");
    }
    void TogglePlayback()
    {
        if (preparingVideo)
        {
            wantsPlayback = !wantsPlayback;
            SetPlaybackLabel(wantsPlayback ? "Pause" : "Play"); return;
        }
        if (!player.isPrepared) { StartVideo(); return; }
        wantsPlayback = !wantsPlayback;
        if (wantsPlayback) player.Play(); else player.Pause();
        SetPlaybackLabel(wantsPlayback ? "Pause" : "Play");
    }
    void Replay()
    {
        wantsPlayback = true;
        if (currentVideoIndex == 0 && player.isPrepared && !preparingVideo)
        {
            player.time = 0; player.Play(); SetPlaybackLabel("Pause");
        }
        else StartVideo();
    }
    void SetPlaybackLabel(string text) { playbackButton.GetComponentInChildren<TMP_Text>().text = text; }
    void OnDisable()
    {
        preparingVideo = false; wantsPlayback = false;
        if (player != null) { player.Stop(); player.targetTexture = null; }
        if (video != null) video.texture = null;
        if (texture != null) { texture.Release(); Destroy(texture); texture = null; }
    }
}
