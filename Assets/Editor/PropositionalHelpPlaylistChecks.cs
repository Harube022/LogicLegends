#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Opt-in, offline touch checks for Propositional Logic's four recorded Help actions.</summary>
public static class PropositionalHelpPlaylistChecks
{
    const string Flag = "PropositionalHelp.PlaylistQA";
    const string Report = "Library/PropositionalHelpPlaylistChecks.txt";
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Captions = {
        "Approach the Book Statue and tap the hand button to begin.",
        "Read the question and use the hint board to guide your answer.",
        "Compare the four door answers with the question.",
        "Walk to your chosen door and stand on its selection pad to submit your answer."
    };
    static Touchscreen touchscreen;
    static int finger;

    [MenuItem("Logic Legends/Help/Check Propositional Four-Clip Playback")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Exit Play Mode and save the current scene before checking the Help playlist.");
        SessionState.SetString(Flag + ".Scene", SceneManager.GetActiveScene().path);
        SessionState.SetInt(Flag + ".Size", BookMapMobileChecks.GetSize());
        SessionState.SetInt(Flag + ".SafeArea", (int)Crystal.SafeArea.Sim);
        SessionState.SetBool(Flag + ".Persistence", StageCompleteManager.SuppressPersistenceForValidation);
        SessionState.SetInt(Flag + ".Topic", LevelTimerManager.savedTopicIndex);
        SessionState.SetFloat(Flag + ".Time", LevelTimerManager.savedRemainingTime);
        SessionState.SetBool(Flag + ".Retry", LevelTimerManager.isRespawningFromFail);
        SessionState.SetInt(Flag + ".RetryCount", LevelTimerManager.TryAgainCount);
        SessionState.SetBool(Flag, true);
        File.WriteAllText(Report, "Offline Propositional Logic Help playlist checks; Input System touch input.\n");
        EditorSceneManager.OpenScene("Assets/Scenes/PRELIM.unity");
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.playModeStateChanged -= Restore;
        EditorApplication.playModeStateChanged += Restore;
    }

    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false);
        SceneManager.sceneLoaded -= Loaded;
        foreach (string field in new[] { "ValidationTransport", "ValidationUid", "ValidationEmail" })
            typeof(TrialBackendClient).GetField(field, Statics)?.SetValue(null, null);
        StageCompleteManager.SuppressPersistenceForValidation = SessionState.GetBool(Flag + ".Persistence", false);
        LevelTimerManager.savedTopicIndex = SessionState.GetInt(Flag + ".Topic", 0);
        LevelTimerManager.savedRemainingTime = SessionState.GetFloat(Flag + ".Time", -1);
        LevelTimerManager.isRespawningFromFail = SessionState.GetBool(Flag + ".Retry", false);
        typeof(LevelTimerManager).GetField("tryAgainCount", Statics)?.SetValue(null, SessionState.GetInt(Flag + ".RetryCount", 0));
        BookMapMobileChecks.SetSize(SessionState.GetInt(Flag + ".Size", 6));
        Crystal.SafeArea.Sim = (Crystal.SafeArea.SimDevice)SessionState.GetInt(Flag + ".SafeArea", 0);
        string original = SessionState.GetString(Flag + ".Scene", "Assets/Scenes/Main Menu.unity");
        if (!string.IsNullOrEmpty(original)) EditorApplication.delayCall += () => EditorSceneManager.OpenScene(original);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        typeof(TrialBackendClient).GetField("ValidationTransport", Statics)?.SetValue(null,
            (Func<string, string, System.Threading.Tasks.Task<string>>)((route, body) => System.Threading.Tasks.Task.FromResult("{}")));
        typeof(TrialBackendClient).GetField("ValidationUid", Statics)?.SetValue(null, null);
        typeof(TrialBackendClient).GetField("ValidationEmail", Statics)?.SetValue(null, null);
        StageCompleteManager.SuppressPersistenceForValidation = true;
        StageSelectionState.SelectForEditorSpawn(1);
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if ((component.GetType().Namespace ?? "").StartsWith("Photon.Pun.Demo.Cockpit")) component.enabled = false;
        if (Player.LocalInstance == null)
        {
            var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
            avatar.GetComponent<MultiplayerAvatarSync>().enabled = false;
            avatar.GetComponent<PlayerEquipmentLoader>().enabled = false;
            foreach (var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true)) skin.gameObject.SetActive(skin.name == "UNIFORM_MALE");
            avatar.GetComponent<CharacterController>().enabled = true;
            var spawner = UnityEngine.Object.FindFirstObjectByType<LevelSpawner>();
            var spawn = Read(spawner, "spawnPoint") as Transform;
            if (spawn != null) avatar.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        }
        if (UnityEngine.Object.FindFirstObjectByType<PropositionalHelpPlaylistQaRunner>() == null)
        {
            var runner = new GameObject("Offline Propositional Help Playlist QA").AddComponent<PropositionalHelpPlaylistQaRunner>();
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.StartCoroutine(Guard(RunFixture()));
        }
    }

    static IEnumerator Guard(IEnumerator work)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(work);
        while (stack.Count > 0)
        {
            object next;
            try
            {
                var item = stack.Peek();
                if (!item.MoveNext()) { stack.Pop(); continue; }
                next = item.Current;
            }
            catch (Exception error)
            {
                File.AppendAllText(Report, "FAIL " + error + "\n"); Debug.LogException(error);
                RemoveTouchscreen(); EditorApplication.isPlaying = false; yield break;
            }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        Log("ALL PROPOSITIONAL HELP PLAYLIST CHECKS PASSED");
        RemoveTouchscreen(); SceneManager.sceneLoaded -= Loaded; EditorApplication.isPlaying = false;
    }

    static IEnumerator RunFixture()
    {
        Application.runInBackground = true;
        touchscreen = InputSystem.AddDevice<Touchscreen>(); finger = 400;
        BookMapMobileChecks.SetSize(6);
        yield return Until(() => Player.LocalInstance != null && GameInput.Instance != null &&
            (TeleportManager.Instance == null || !TeleportManager.Instance.IsTransitioning), 30, "player and arrival fade ready");
        yield return Until(() => OpenPage() != null, 25, "automatic first Help opens");
        yield return StageHelpPlayChecks.CompleteInitialGuide("PRELIM");
        yield return Until(() => OpenPage() == null, 4, "initial Help closes without starting a challenge");
        Check(Player.LocalInstance.enabled && !GameInput.Instance.GameplayInputBlocked, "initial Close restores controls");
        var help = UnityEngine.Object.FindFirstObjectByType<PropositionalLogicTutorial>();
        Button helpButton = (Button)Read(help, "helpButton");
        foreach (int size in new[] { 6, 3 })
        {
            BookMapMobileChecks.SetSize(size); yield return new WaitForSecondsRealtime(.4f);
            var snapshot = Snapshot.Capture();
            yield return Tap(helpButton);
            yield return Until(() => OpenPage() != null, 4, "Help reopens");
            var page = OpenPage();
            yield return Run(page);
            snapshot.CheckUnchanged(false);
            yield return Tap(page.BeginButton); yield return null;
            Check(!page.gameObject.activeInHierarchy && page.GameplayVideo.targetTexture == null, "Close releases playlist and render texture");
            snapshot.CheckUnchanged(true);
            Log(Screen.width + "x" + Screen.height + ": four-clip automatic sequence, exact captions, touch Pause/Replay/Close, timer/progress/input preservation");
        }
    }

    /// <summary>Can also be run by another prepared offline fixture with an already-open page.</summary>
    public static IEnumerator Run(StageHelpPage page)
    {
        bool ownsTouchscreen = touchscreen == null || !touchscreen.added;
        if (ownsTouchscreen) { touchscreen = InputSystem.AddDevice<Touchscreen>(); finger = 400; }
        Check(page != null && page.isActiveAndEnabled, "Help page is active");
        Check(page.VideoCount == 4 && StageHelpVideoCatalog.VideoCount("PRELIM") == 4, "exactly four ordered Propositional recordings");
        var video = page.GameplayVideo;
        Check(video.timeUpdateMode == VideoTimeUpdateMode.UnscaledGameTime, "recordings play while gameplay is paused");
        Button pause = page.GetComponentsInChildren<Button>().Single(b => b.name == "Pause Gameplay Video");
        Button replay = page.GetComponentsInChildren<Button>().Single(b => b.name == "Replay Gameplay Video");
        TMP_Text caption = page.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Action Caption");
        for (int index = 0; index < 4; index++)
        {
            int expected = index;
            yield return Until(() => page.CurrentVideoIndex == expected && video.isPrepared && video.frame > 0, 25, "clip " + (index + 1) + " prepares");
            Check(video.clip != null && video.clip.length >= 2 && video.clip.width >= 1280 && video.clip.height >= 720,
                "clip " + (index + 1) + " is an actual full-resolution recording with room for its two-second fade");
            Check(video.clip.name == StageHelpVideoCatalog.ClipResourceKey("PRELIM", index), "ordered clip resource " + index);
            yield return null; Canvas.ForceUpdateCanvases();
            Check(caption.text == Captions[index] && StageHelpVideoCatalog.CaptionAt("PRELIM", video.time, index) == Captions[index], "exact requested caption " + (index + 1));
            caption.ForceMeshUpdate();
            Check(caption.fontSize >= 26 && !caption.isTextOverflowing, "caption remains readable without clipping " + (index + 1));
            CheckLayout(page, caption);
            if (index == 0)
            {
                yield return Tap(pause); Check(!video.isPlaying, "actual touch pauses clip");
                double still = video.time; yield return new WaitForSecondsRealtime(.3f);
                Check(Math.Abs(video.time - still) < .12, "paused footage remains still");
                yield return Tap(pause); Check(video.isPlaying, "actual touch resumes clip");
            }
            if (index == 3)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("Library/PropositionalHelpPlaylist_" + Screen.width + "x" + Screen.height + ".png");
            }
            yield return CheckFade(video,index);
            video.Play();
            int next = (index + 1) % 4;
            yield return Until(() => page.CurrentVideoIndex == next, 12, "clip " + (index + 1) + " automatically advances after its black ending");
        }
        yield return Until(() => video.isPrepared && video.frame > 0, 25, "playlist loops to first recording");
        Check(page.CurrentVideoIndex == 0, "fourth clip loops to first clip");
        // Replay must reset the entire playlist, even when another clip is active.
        video.time = Math.Max(0, video.clip.length - .2);
        yield return Until(() => page.CurrentVideoIndex == 1 && video.isPrepared && video.frame > 0, 25, "second recording ready for Replay");
        yield return Tap(replay);
        yield return Until(() => page.CurrentVideoIndex == 0 && video.isPrepared && video.isPlaying, 25, "Replay resets playlist to clip one");
        Check(video.time < 1.5, "Replay starts first clip from beginning");
        if (ownsTouchscreen) RemoveTouchscreen();
    }

    static IEnumerator CheckFade(VideoPlayer video,int index)
    {
        video.Pause();
        double duration=video.clip.length;
        float[] luma=new float[3];
        double[] positions={duration-2.05,duration-1.05,duration-.005};
        for(int i=0;i<positions.Length;i++)
        {
            bool ready=false;
            VideoPlayer.EventHandler sought=v=>ready=true;
            video.seekCompleted+=sought;
            if(i==2)video.frame=(long)video.frameCount-1;
            else video.time=Math.Max(0,positions[i]);
            yield return Until(()=>ready,8,"fade seek completes");
            video.seekCompleted-=sought;
            yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
            var prior=RenderTexture.active;RenderTexture.active=video.targetTexture;
            var pixels=new Texture2D(video.targetTexture.width,video.targetTexture.height,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,pixels.width,pixels.height),0,0);pixels.Apply();
            var samples=pixels.GetPixels32();double sum=0;int count=0;
            for(int j=0;j<samples.Length;j+=31){sum+=(samples[j].r+samples[j].g+samples[j].b)/(255.0*3);count++;}
            luma[i]=(float)(sum/count);
            if(i==2)File.WriteAllBytes("Library/PropositionalEncodedBlack_"+index+".png",pixels.EncodeToPNG());
            UnityEngine.Object.Destroy(pixels);RenderTexture.active=prior;
        }
        Check(luma[0]>.05f,"encoded scene remains visible when two-second fade begins");
        Check(luma[1]<luma[0]*.8f && luma[1]>luma[0]*.2f,"encoded fade is gradual at its midpoint");
        Check(luma[2]<.005f,"encoded clip reaches black before next recording");
        Log("Clip "+(index+1)+" encoded two-second fade luminance: "+string.Join(" / ",luma.Select(x=>x.ToString("F3"))));
    }

    static void CheckLayout(StageHelpPage page, TMP_Text caption)
    {
        Rect screen = new Rect(0, 0, Screen.width, Screen.height);
        foreach (RectTransform rect in new[] { (RectTransform)page.BeginButton.transform, caption.rectTransform,
            page.transform.Find("Shared Scrollable Help/Gameplay Demonstration") as RectTransform })
            Check(rect != null && Contains(screen, ScreenRect(rect)), "Help controls/caption fit safe screen bounds: " + rect?.name);
        Rect reading = ScreenRect(page.ReadingScroll.viewport);
        Rect media = ScreenRect(page.transform.Find("Shared Scrollable Help/Gameplay Demonstration") as RectTransform);
        Check(!reading.Overlaps(media), "text and footage panels do not overlap");
        Check(GameInput.Instance.GameplayInputBlocked && !Player.LocalInstance.enabled, "Help keeps normal gameplay blocked during video playback");
    }

    sealed class Snapshot
    {
        float time, scale;
        bool running, tutorialPaused, playerEnabled, inputBlocked, started;
        int topic;
        object question;
        int[] doors;
        public static Snapshot Capture()
        {
            var timer = UnityEngine.Object.FindFirstObjectByType<LevelTimerManager>();
            var quiz = UnityEngine.Object.FindFirstObjectByType<QuizManager>();
            return new Snapshot { time = timer.RemainingTime, running = timer.IsTimerRunning,
                tutorialPaused = (bool)Read(timer, "isTutorialPaused"), scale = Time.timeScale,
                playerEnabled = Player.LocalInstance.enabled, inputBlocked = GameInput.Instance.GameplayInputBlocked,
                started = (bool)Read(quiz, "currentChallengeStarted"), topic = (int)Read(quiz, "currentTopicIndex"),
                question = Read(quiz, "currentQuestion"), doors = ((int[])Read(quiz, "currentDoorOptionOrder")).ToArray() };
        }
        public void CheckUnchanged(bool closed)
        {
            var timer = UnityEngine.Object.FindFirstObjectByType<LevelTimerManager>();
            var quiz = UnityEngine.Object.FindFirstObjectByType<QuizManager>();
            Check(Math.Abs(timer.RemainingTime - time) < .02f && timer.IsTimerRunning == running, "recorded actions do not run or consume the real countdown");
            Check((int)Read(quiz, "currentTopicIndex") == topic && (bool)Read(quiz, "currentChallengeStarted") == started &&
                ReferenceEquals(Read(quiz, "currentQuestion"), question) && doors.SequenceEqual((int[])Read(quiz, "currentDoorOptionOrder")),
                "recordings do not start, answer, reshuffle or advance the real question");
            if (closed) Check(Math.Abs(Time.timeScale - scale) < .001f && Player.LocalInstance.enabled == playerEnabled &&
                GameInput.Instance.GameplayInputBlocked == inputBlocked && (bool)Read(timer, "isTutorialPaused") == tutorialPaused,
                "Close restores previous timer, player and input state");
        }
    }

    static StageHelpPage OpenPage() => UnityEngine.Object.FindObjectsByType<StageHelpPage>(FindObjectsSortMode.None).FirstOrDefault(p => p.isActiveAndEnabled);
    static object Read(object target, string name) => target?.GetType().GetProperty(name, Members)?.GetValue(target) ?? target?.GetType().GetField(name, Members)?.GetValue(target);
    static IEnumerator Until(Func<bool> condition, float seconds, string detail)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < until) yield return null;
        Check(condition(), detail);
    }
    static IEnumerator Tap(Button button)
    {
        Check(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), "touch target available: " + button?.name);
        yield return null; Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
        Vector2 point = ScreenRect((RectTransform)button.transform).center;
        var hits = new List<RaycastResult>();
        Check(EventSystem.current != null, "EventSystem exists");
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "touch raycast reaches " + button.name);
        int id = ++finger;
        InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Began, position = point });
        yield return null; yield return null;
        InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = point });
        yield return null; yield return null;
    }
    static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var canvas = rect.GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var points = corners.Select(c => RectTransformUtility.WorldToScreenPoint(camera, c)).ToArray();
        return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
    }
    static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.yMin >= outer.yMin - 1 && inner.xMax <= outer.xMax + 1 && inner.yMax <= outer.yMax + 1;
    static void RemoveTouchscreen() { if (touchscreen != null && touchscreen.added) InputSystem.RemoveDevice(touchscreen); touchscreen = null; }
    static void Check(bool valid, string detail) { if (!valid) throw new InvalidOperationException("Propositional Help playlist: " + detail); }
    static void Log(string detail) => File.AppendAllText(Report, "PASS " + detail + "\n");
}
public sealed class PropositionalHelpPlaylistQaRunner : MonoBehaviour { }
#endif
