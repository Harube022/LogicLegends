#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.Induction;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Uses the recorder's offline isolation, real UI touches and live decoded video frames.</summary>
public static class InductionHelpVerification
{
    const string Report="Library/InductionHelpVerification.txt";
    static readonly string[] Captions=InductionHelpClipCapture.Captions;
    static Touchscreen touchscreen;
    static int finger;
    static InductionStageManager stage;
    public static IEnumerator Run()
    {
        File.WriteAllText(Report,"Induction UI, camera-facing mesh clearance, actual mobile UI touches and six-clip playback checks.\n");
        stage=InductionStageManager.Instance;touchscreen=InputSystem.AddDevice<Touchscreen>();finger=5200;
        var hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="InteractButton");
        Face(stage.book.position+Vector3.back*2.6f,stage.book.position);yield return null;yield return Tap(hand);
        Check(stage.State==InductionState.Active&&stage.timer.Running,"hand starts real C1");
        var correct=stage.Tablets.Single(t=>t.Data.id==stage.Challenge.answers["Base:1"]);
        yield return Collect(correct,hand);var slot=stage.slots.Single(s=>s.key=="Base:1");
        var bounds=stage.stations[0].GetComponent<Renderer>().bounds;
        Face(new Vector3(bounds.min.x-1.25f,29.12f,slot.transform.position.z),slot.transform.position);
        yield return null;yield return Tap(stage.ui.InteractButton);Check(slot.Tablet==correct&&stage.Held==null,"actual placement preserves ungraded answer");
        yield return Collect(stage.Tablets.First(t=>t!=correct),hand);
        Check(stage.CanSubmit&&stage.ui.ReturnButton.gameObject.activeSelf,"Submit and Return coexist after a partial/complete arrangement");
        yield return Labels();
        for(int size=0;size<3;size++)
        {
            BookMapMobileChecks.SetSize(new[]{6,12,3}[size]);yield return new WaitForSecondsRealtime(.5f);
            stage.ui.Refresh();Canvas.ForceUpdateCanvases();
            var safe=ScreenRect((RectTransform)stage.ui.Root.parent);var submit=ScreenRect((RectTransform)stage.ui.SubmitButton.transform);
            Check(Math.Abs(submit.center.x-safe.center.x)<1.5f&&submit.yMin>=safe.yMin+4&&submit.yMin-safe.yMin<70,"Submit is bottom-center within safe area");
            Check(Contains(safe,submit),"Submit fully fits safe area");
            foreach(var b in stage.uiHost.GetComponentsInChildren<Button>().Where(b=>b.name=="JumpButton"||b.name=="InteractButton"))
                Check(!submit.Overlaps(ScreenRect((RectTransform)b.transform)),"Submit clears mobile "+b.name);
            var actions=new[]{stage.ui.SubmitButton,stage.ui.ReturnButton,stage.ui.ViewBoardButton,stage.ui.InteractButton}.Where(b=>b.gameObject.activeInHierarchy).ToArray();
            foreach(var a in actions)foreach(var b in actions)if(a!=b)Check(!ScreenRect((RectTransform)a.transform).Overlaps(ScreenRect((RectTransform)b.transform)),"gameplay action buttons do not overlap");
            yield return Capture("Library/InductionActions_"+Screen.width+"x"+Screen.height+".png");
            var snapshot=Snapshot.Capture();yield return Tap(stage.help.HelpButton);
            yield return Until(()=>OpenPage()!=null,5,"Help opens");var page=OpenPage();snapshot.PausedBaseline();
            yield return Run(page);snapshot.CheckUnchanged(false);
            yield return Tap(page.BeginButton);yield return null;snapshot.CheckUnchanged(true);
            Check(!page.gameObject.activeInHierarchy&&page.GameplayVideo.targetTexture==null&&!stage.help.OnboardingPending&&!stage.onboarding.IsActive,"Let's Begin releases video and restores gameplay without replaying tutorial");
            Log(Screen.width+"x"+Screen.height+": bottom-center Submit, no action overlap, six actual clips and exact captions, automatic sequence, encoded fades, touch Pause/Replay/Let's Begin, unchanged time/held scroll/placements");
        }
        yield return Tap(stage.ui.ReturnButton);Check(stage.Held==null&&stage.Tablets.Count==stage.Challenge.choices.Length,"Return clears carry without duplicates");
        yield return Tap(stage.ui.SubmitButton);Check(stage.Correct==1&&stage.Mistakes==0&&stage.ChallengeIndex==1&&stage.State==InductionState.WaitingForBook,"repositioned Submit checks once and waits for next book activation");
        RemoveTouchscreen();Log("ALL INDUCTION UI AND SIX-CLIP CHECKS PASSED");
    }
    static IEnumerator Collect(InductionTablet tablet,Button hand)
    {
        Face(stage.scrollStand.position+Vector3.back*2.8f,stage.scrollStand.position);yield return null;yield return Tap(hand);
        Check(stage.BrowsingScrolls,"hand opens catalogue");
        var button=stage.ui.Collection.GetComponentsInChildren<Button>().Single(b=>b.name=="Scroll Choice "+tablet.Data.id);
        var scroll=button.GetComponentInParent<ScrollRect>();Canvas.ForceUpdateCanvases();
        scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(-((RectTransform)button.transform).anchoredPosition.y,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height)));
        yield return null;yield return Tap(button);yield return Tap(stage.ui.GetScrollButton);
        Check(stage.Held==tablet&&!stage.Reading,"Get This Scroll collects selected item once");
    }
    static void Face(Vector3 at,Vector3 toward)
    {
        at.y=29.12f;var d=toward-at;d.y=0;
        PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,at,Quaternion.LookRotation(d));
    }
    static IEnumerator Labels()
    {
        var camera=Camera.main;var brain=camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();bool enabled=brain.enabled;brain.enabled=false;
        Vector3 at=camera.transform.position;Quaternion rotation=camera.transform.rotation;float scale=Time.timeScale;Time.timeScale=0;
        foreach(var station in stage.stations)
        {
            var label=station.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Station Label");var box=station.GetComponent<Renderer>().bounds;
            Check(label.font==StageUITheme.HeadingFont&&label.fontSizeMin>=5&&(label.fontStyle&FontStyles.Bold)!=0&&label.outlineWidth>.1f,"heavy readable station label: "+label.text);
            foreach(float angle in new[]{0f,90f,180f,270f})foreach(float pitch in new[]{8f,55f,82f})
            {
                Vector3 direction=Quaternion.Euler(pitch,angle,angle==90?20:0)*Vector3.forward;
                camera.transform.SetPositionAndRotation(box.center-direction*15,Quaternion.LookRotation(direction));
                yield return null;yield return new WaitForEndOfFrame();label.ForceMeshUpdate();
                var corners=new Vector3[4];label.rectTransform.GetWorldCorners(corners);
                Check(corners.All(p=>p.y>box.max.y+.5f),"entire billboard remains above altar through orbit/pitch: "+label.text);
                Check(Vector3.Dot(label.transform.forward,camera.transform.forward)>.999f&&!label.isTextOverflowing,"full label faces live camera without clipping");
            }
        }
        camera.transform.SetPositionAndRotation(at,rotation);brain.enabled=enabled;Time.timeScale=scale;
        Log("All three station labels: 36 live camera angles including steep pitch; full text above mesh, correct facing, bold font and outline");
    }
    sealed class Snapshot
    {
        float time,scale;int correct,mistakes,challenge;InductionState state;InductionTablet held;InductionTablet[] placed;bool running;
        public static Snapshot Capture()=>new Snapshot{time=stage.timer.Remaining,scale=Time.timeScale,correct=stage.Correct,mistakes=stage.Mistakes,challenge=stage.ChallengeIndex,state=stage.State,held=stage.Held,placed=stage.slots.Select(s=>s.Tablet).ToArray(),running=stage.timer.Running};
        public void PausedBaseline(){time=stage.timer.Remaining;}
        public void CheckUnchanged(bool closed)
        {
            Check(Math.Abs(stage.timer.Remaining-time)<.1f&&stage.timer.Running==running&&stage.Correct==correct&&stage.Mistakes==mistakes&&stage.ChallengeIndex==challenge&&stage.State==state,"Help movies preserve actual timer and progress");
            Check(stage.Held==held&&stage.slots.Select(s=>s.Tablet).SequenceEqual(placed),"Help movies preserve collected and placed scrolls");
            if(closed)Check(Time.timeScale==scale&&Player.LocalInstance.enabled&&!GameInput.Instance.GameplayInputBlocked&&!stage.Reading,"Let's Begin restores previous input and timer state");
        }
    }
    static IEnumerator Capture(string path)
    {
        yield return new WaitForEndOfFrame();var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.Destroy(image);
    }
    public static IEnumerator Run(StageHelpPage page)
    {
        bool ownsTouchscreen = touchscreen == null || !touchscreen.added;
        if (ownsTouchscreen) { touchscreen = InputSystem.AddDevice<Touchscreen>(); finger = 400; }
        Check(page != null && page.isActiveAndEnabled, "Help page is active");
        Check(page.VideoCount == 6 && StageHelpVideoCatalog.VideoCount("Induction") == 6, "exactly six ordered Induction recordings");
        var video = page.GameplayVideo;
        Check(video.timeUpdateMode == VideoTimeUpdateMode.UnscaledGameTime, "recordings play while gameplay is paused");
        Button pause = page.GetComponentsInChildren<Button>().Single(b => b.name == "Pause Gameplay Video");
        Button replay = page.GetComponentsInChildren<Button>().Single(b => b.name == "Replay Gameplay Video");
        TMP_Text caption = page.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Action Caption");
        for (int index = 0; index < 6; index++)
        {
            int expected = index;
            yield return Until(() => page.CurrentVideoIndex == expected && video.isPrepared && video.frame > 0, 25, "clip " + (index + 1) + " prepares");
            Check(video.clip != null && video.clip.length >= 2 && video.clip.width >= 1280 && video.clip.height >= 720,
                "clip " + (index + 1) + " is an actual full-resolution recording with room for its two-second fade");
            Check(video.clip.name == StageHelpVideoCatalog.ClipResourceKey("Induction", index), "ordered clip resource " + index);
            yield return null; Canvas.ForceUpdateCanvases();
            Check(caption.text == Captions[index] && StageHelpVideoCatalog.CaptionAt("Induction", video.time, index) == Captions[index], "exact requested caption " + (index + 1));
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
            if (index == 5)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("Library/InductionHelpPlaylist_" + Screen.width + "x" + Screen.height + ".png");
            }
            yield return CheckFade(video,index);
            video.Play();
            int next = (index + 1) % 6;
            yield return Until(() => page.CurrentVideoIndex == next, 12, "clip " + (index + 1) + " automatically advances after its black ending");
        }
        yield return Until(() => video.isPrepared && video.frame > 0, 25, "playlist loops to first recording");
        Check(page.CurrentVideoIndex == 0, "sixth clip loops to first clip");
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
            if(i==2)File.WriteAllBytes("Library/InductionEncodedBlack_"+index+".png",pixels.EncodeToPNG());
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

    static StageHelpPage OpenPage() => UnityEngine.Object.FindObjectsByType<StageHelpPage>(FindObjectsSortMode.None).FirstOrDefault(p => p.isActiveAndEnabled);

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
    static void Check(bool valid, string detail) { if (!valid) throw new InvalidOperationException("Induction Help playlist: " + detail); }
    static void Log(string detail) => File.AppendAllText(Report, "PASS " + detail + "\n");
}

#endif
