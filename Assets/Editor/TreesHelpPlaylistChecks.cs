#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.Video;
using LogicLegends.Trees;

/// <summary>Opt-in offline real-touch checks for both actual Trees Help clips.</summary>
public static class TreesHelpPlaylistChecks
{
    const string Report="Library/TreesHelpPlaylistChecks.txt";
    static readonly string[] Captions=TreesHelpClipCapture.Captions;
    static Touchscreen touchscreen;static int finger;
    public static IEnumerator Run()
    {
        File.WriteAllText(Report,"Offline Trees Help: real Input System touches, two recordings, encoded fades, mobile layout and preserved state.\n");
        var stage=UnityEngine.Object.FindFirstObjectByType<TreeStageManager>();
        touchscreen=InputSystem.AddDevice<Touchscreen>();finger=4200;
        foreach(int size in new[]{6,12,2})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.4f);
            float time=stage.timer.Remaining;var picks=Enumerable.Range(0,10).Select(i=>stage.SelectedVariant(i/2,i%2)).ToArray();int correct=stage.Correct;
            stage.SetLesson(true);yield return null;
            var page=UnityEngine.Object.FindFirstObjectByType<StageHelpPage>();
            yield return VerifyPage(page);
            yield return Tap(page.BeginButton);yield return null;
            Check(!stage.LessonOpen&&stage.LocalPlayer.enabled&&!GameInput.Instance.GameplayInputBlocked,"Help exit restores controls and leaves book usable");
            Check(stage.timer.Remaining==time&&!stage.timer.Running&&stage.Correct==correct&&Enumerable.Range(0,10).Select(i=>stage.SelectedVariant(i/2,i%2)).SequenceEqual(picks),"video playback preserves real question, time, unchanged tree and progress");
            Log(Screen.width+"x"+Screen.height+": both clips, captions, fades, Pause, Replay and exit passed");
        }
        RemoveTouchscreen();Log("ALL TREES HELP PLAYLIST CHECKS PASSED");
    }
    public static IEnumerator VerifyPage(StageHelpPage page)
    {
        bool ownsTouchscreen = touchscreen == null || !touchscreen.added;
        if (ownsTouchscreen) { touchscreen = InputSystem.AddDevice<Touchscreen>(); finger = 400; }
        Check(page != null && page.isActiveAndEnabled, "Help page is active");
        Check(page.VideoCount == 2 && StageHelpVideoCatalog.VideoCount("Trees") == 2, "exactly two ordered Trees recordings");
        var video = page.GameplayVideo;
        Check(video.timeUpdateMode == VideoTimeUpdateMode.UnscaledGameTime, "recordings play while gameplay is paused");
        Button pause = page.GetComponentsInChildren<Button>().Single(b => b.name == "Pause Gameplay Video");
        Button replay = page.GetComponentsInChildren<Button>().Single(b => b.name == "Replay Gameplay Video");
        TMP_Text caption = page.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Action Caption");
        for (int index = 0; index < 2; index++)
        {
            int expected = index;
            yield return Until(() => page.CurrentVideoIndex == expected && video.isPrepared && video.frame > 0, 25, "clip " + (index + 1) + " prepares");
            Check(video.clip != null && video.clip.length >= 2 && video.clip.width >= 1280 && video.clip.height >= 720,
                "clip " + (index + 1) + " is an actual full-resolution recording with room for its two-second fade");
            Check(video.clip.name == StageHelpVideoCatalog.ClipResourceKey("Trees", index), "ordered clip resource " + index);
            yield return null; Canvas.ForceUpdateCanvases();
            Check(caption.text == Captions[index] && StageHelpVideoCatalog.CaptionAt("Trees", video.time, index) == Captions[index], "exact requested caption " + (index + 1));
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
            if (index == 1)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("Library/TreesHelpPlaylist_" + Screen.width + "x" + Screen.height + ".png");
            }
            yield return CheckFade(video,index);
            video.Play();
            int next = (index + 1) % 2;
            yield return Until(() => page.CurrentVideoIndex == next, 12, "clip " + (index + 1) + " automatically advances after its black ending");
        }
        yield return Until(() => video.isPrepared && video.frame > 0, 25, "playlist loops to first recording");
        Check(page.CurrentVideoIndex == 0, "second clip loops to first clip");
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
            if(i==2)File.WriteAllBytes("Library/TreesEncodedBlack_"+index+".png",pixels.EncodeToPNG());
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
    static void Check(bool valid, string detail) { if (!valid) throw new InvalidOperationException("Trees Help playlist: " + detail); }
    static void Log(string detail) => File.AppendAllText(Report, "PASS " + detail + "\n");
}
#endif
