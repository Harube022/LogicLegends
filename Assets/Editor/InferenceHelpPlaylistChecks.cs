#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using LogicLegends.Inference;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Opt-in, offline touch checks for Rules of Inference's five recorded Help actions.</summary>
public static class InferenceHelpPlaylistChecks
{
    const string Flag = "InferenceHelp.PlaylistQA";
    const string Report = "Library/InferenceHelpPlaylistChecks.txt";
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Captions = InferenceHelpClipCapture.Captions;
    static Touchscreen touchscreen;
    static int finger;

    [MenuItem("Logic Legends/Help/Check Rules of Inference Five-Clip Playback")]
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
        File.WriteAllText(Report, "Offline Rules of Inference Help playlist checks; Input System touch input.\n");
        EditorSceneManager.OpenScene("Assets/Scenes/RulesOfInference.unity");
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
        StageSelectionState.SelectForEditorSpawn(3);
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
        if (UnityEngine.Object.FindFirstObjectByType<InferenceHelpPlaylistQaRunner>() == null)
        {
            var runner = new GameObject("Offline Rules of Inference Help Playlist QA").AddComponent<InferenceHelpPlaylistQaRunner>();
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
        Log("ALL INFERENCE HELP PLAYLIST CHECKS PASSED");
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
        yield return StageHelpPlayChecks.CompleteInitialGuide("RulesOfInference");
        yield return Until(() => OpenPage() == null, 4, "initial Help closes without starting a challenge");
        Check(Player.LocalInstance.enabled && !GameInput.Instance.GameplayInputBlocked, "initial Close restores controls");
        var help = StageJourneyUI.Instance;
        Button helpButton = (Button)Read(help, "inferenceHelp");
        foreach (int size in new[] { 6, 12, 3 })
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
            Log(Screen.width + "x" + Screen.height + ": five-clip automatic sequence, exact captions, touch Pause/Replay/Close, timer/progress/input preservation");
        }
    }

    /// <summary>Can also be run by another prepared offline fixture with an already-open page.</summary>
    public static IEnumerator Run(StageHelpPage page)
    {
        bool ownsTouchscreen = touchscreen == null || !touchscreen.added;
        if (ownsTouchscreen) { touchscreen = InputSystem.AddDevice<Touchscreen>(); finger = 400; }
        Check(page != null && page.isActiveAndEnabled, "Help page is active");
        Check(page.VideoCount == 5 && StageHelpVideoCatalog.VideoCount("RulesOfInference") == 5, "exactly five ordered Rules of Inference recordings");
        var video = page.GameplayVideo;
        Check(video.timeUpdateMode == VideoTimeUpdateMode.UnscaledGameTime, "recordings play while gameplay is paused");
        Button pause = page.GetComponentsInChildren<Button>().Single(b => b.name == "Pause Gameplay Video");
        Button replay = page.GetComponentsInChildren<Button>().Single(b => b.name == "Replay Gameplay Video");
        TMP_Text caption = page.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Action Caption");
        for (int index = 0; index < 5; index++)
        {
            int expected = index;
            yield return Until(() => page.CurrentVideoIndex == expected && video.isPrepared && video.frame > 0, 25, "clip " + (index + 1) + " prepares");
            Check(video.clip != null && video.clip.length >= 2 && video.clip.width >= 1280 && video.clip.height >= 720,
                "clip " + (index + 1) + " is an actual full-resolution recording with room for its two-second fade");
            Check(video.clip.name == StageHelpVideoCatalog.ClipResourceKey("RulesOfInference", index), "ordered clip resource " + index);
            yield return null; Canvas.ForceUpdateCanvases();
            Check(caption.text == Captions[index] && StageHelpVideoCatalog.CaptionAt("RulesOfInference", video.time, index) == Captions[index], "exact requested caption " + (index + 1));
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
            if (index == 4)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("Library/InferenceHelpPlaylist_" + Screen.width + "x" + Screen.height + ".png");
            }
            if(index==2)yield return CheckBoardCaptions(page,caption);
            yield return CheckFade(video,index);
            video.Play();
            int next = (index + 1) % 5;
            yield return Until(() => page.CurrentVideoIndex == next, 12, "clip " + (index + 1) + " automatically advances after its black ending");
        }
        yield return Until(() => video.isPrepared && video.frame > 0, 25, "playlist loops to first recording");
        Check(page.CurrentVideoIndex == 0, "fifth clip loops to first clip");
        // Replay must reset the entire playlist, even when another clip is active.
        video.time = Math.Max(0, video.clip.length - .2);
        yield return Until(() => page.CurrentVideoIndex == 1 && video.isPrepared && video.frame > 0, 25, "second recording ready for Replay");
        yield return Tap(replay);
        yield return Until(() => page.CurrentVideoIndex == 0 && video.isPrepared && video.isPlaying, 25, "Replay resets playlist to clip one");
        Check(video.time < 1.5, "Replay starts first clip from beginning");
        if (ownsTouchscreen) RemoveTouchscreen();
    }

    static IEnumerator CheckBoardCaptions(StageHelpPage page,TMP_Text caption)
    {
        var manifest=JsonUtility.FromJson<StageHelpVideoCatalog.Recording>(Resources.Load<TextAsset>("StageHelpVideos/RulesOfInference").text);
        Check(manifest.guides!=null&&manifest.guides[2].cues.Length>=4,"board explanation has separate timed section captions");
        var video=page.GameplayVideo;video.Pause();
        foreach(var cue in manifest.guides[2].cues)
        {
            bool ready=false;VideoPlayer.EventHandler sought=v=>ready=true;video.seekCompleted+=sought;
            video.time=cue.seconds+.08;yield return Until(()=>ready,8,"board caption seek completes");video.seekCompleted-=sought;
            yield return null;yield return new WaitForEndOfFrame();caption.ForceMeshUpdate();
            Check(caption.text==cue.caption&&caption.fontSize>=26&&!caption.isTextOverflowing,"timed board explanation fits: "+cue.caption);
            CheckLayout(page,caption);
        }
        Log(Screen.width+"x"+Screen.height+": all premise, blanks, check, crystal and conclusion captions match their recorded section highlights");
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
            if(i==0)File.WriteAllBytes("Library/InferenceEncodedView_"+index+".png",pixels.EncodeToPNG());
            if(i==2)File.WriteAllBytes("Library/InferenceEncodedBlack_"+index+".png",pixels.EncodeToPNG());
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
        float time,scale;
        bool playerEnabled,inputBlocked,tutorial,started,completed,quizOpen;
        int rounds,crystals;
        InferenceQuestion question;
        InferencePuzzleStage stage;
        GrabbableObject held;
        string[] blanks;
        public static Snapshot Capture()
        {
            var owner=UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>();
            return new Snapshot {time=StageJourneyUI.Instance.InferenceRemainingSeconds,scale=Time.timeScale,
                playerEnabled=Player.LocalInstance.enabled,inputBlocked=GameInput.Instance.GameplayInputBlocked,
                tutorial=(bool)Read(StageJourneyUI.Instance,"tutorialOpen"),started=owner.HasStarted,completed=owner.PuzzleCompleted,
                rounds=owner.SolvedRounds,question=owner.Question,stage=owner.Stage,crystals=owner.Crystals.Count,
                held=Player.LocalInstance.GetHeldObject(),blanks=owner.dragPuzzle.Zones.Select(z=>z.Value).ToArray(),quizOpen=owner.GetComponent<InferenceCrystalQuiz>().IsOpen};
        }
        public void CheckUnchanged(bool closed)
        {
            var owner=UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>();
            Check(Math.Abs(StageJourneyUI.Instance.InferenceRemainingSeconds-time)<.02f,"recorded actions do not consume the real countdown");
            Check(owner.HasStarted==started&&owner.PuzzleCompleted==completed&&owner.SolvedRounds==rounds&&ReferenceEquals(owner.Question,question)&&owner.Stage==stage&&owner.Crystals.Count==crystals&&
                Player.LocalInstance.GetHeldObject()==held&&blanks.SequenceEqual(owner.dragPuzzle.Zones.Select(z=>z.Value))&&owner.GetComponent<InferenceCrystalQuiz>().IsOpen==quizOpen,
                "recordings do not start challenges, fill real blanks, collect crystals, open quizzes or change progress");
            if(closed)Check(Math.Abs(Time.timeScale-scale)<.001f&&Player.LocalInstance.enabled==playerEnabled&&GameInput.Instance.GameplayInputBlocked==inputBlocked&&(bool)Read(StageJourneyUI.Instance,"tutorialOpen")==tutorial,
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
    static void Check(bool valid, string detail) { if (!valid) throw new InvalidOperationException("Rules of Inference Help playlist: " + detail); }
    static void Log(string detail) => File.AppendAllText(Report, "PASS " + detail + "\n");
}
public sealed class InferenceHelpPlaylistQaRunner : MonoBehaviour { }
#endif
