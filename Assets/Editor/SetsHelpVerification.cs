#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.Video;

public static class SetsHelpVerification
{
    const string Report="Library/SetsHelpVerification.txt";
    static readonly string[] Captions=SetsHelpClipCapture.Captions;
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static Touchscreen touchscreen;static int finger;
    static SetssssChallengeOne stage;
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Private).GetValue(owner);
    static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Private).SetValue(owner,value);
    static void Invoke(object owner,string name)=>owner.GetType().GetMethod(name,Private).Invoke(owner,null);
    public static IEnumerator Run()
    {
        File.WriteAllText(Report,"Sets counters, enlarged labels, surrounding scenery, four actual clips, decoded fades and mobile touch checks.\n");
        stage=UnityEngine.Object.FindFirstObjectByType<SetssssChallengeOne>();touchscreen=InputSystem.AddDevice<Touchscreen>();finger=8300;
        var hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="InteractButton");
        var book=Field<Transform>(stage,"bookStatue");Position(book.position+Vector3.back*2.6f);yield return new WaitForSecondsRealtime(.25f);yield return Tap(hand);
        Check(stage.Started&&stage.Hud.Challenge.text.Contains("Question 1 of 2"),"real book starts question one");
        int first=stage.SelectedEntryIndices[0];yield return Solve(hand);yield return new WaitForSecondsRealtime(1.4f);
        Check(stage.ChallengeNumber==1&&stage.QuestionIndex==1&&stage.Hud.Challenge.text.Contains("Question 2 of 2"),"first correct answer advances question counter only");
        Check(stage.SelectedEntryIndices[1]!=first,"second question is distinct");yield return Solve(hand);yield return null;
        Check(stage.ChallengeNumber==2&&stage.QuestionIndex==0&&stage.Hud.Challenge.text.Contains("Question 1 of 2"),"second answer advances challenge and resets question counter");
        Log("Two distinct questions solved through actual hand pickup, natural drop and Submit. Counter 1 → 2 → next challenge 1.");
        stage.Introductions.Cancel();
        // Preview each introduction in an isolated test setup. No Admin clear, rewards or saved progress.
        for(int challenge=1;challenge<=5;challenge++)
        {
            stage.Introductions.ResetPlaythrough();Set(stage,"challengeNumber",challenge);Set(stage,"conditionIndex",0);Set(stage,"started",false);
            Invoke(stage,"ClearQuestionSelection");Invoke(stage,"RefreshIntroductionPreview");
            yield return Until(()=>stage.Introductions.IsOpen,6,"challenge introduction opens "+challenge);
            var intro=stage.Introductions;float time=stage.TimeRemaining;
            for(int page=0;page<intro.PageCount;page++)
            {
                foreach(int size in new[]{6,3})
                {
                    BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(1.5f);yield return new WaitForEndOfFrame();
                    var labels=GameObject.Find("Sets Diagram Region Labels").GetComponentsInChildren<TMP_Text>();
                    foreach(var label in labels)
                    {
                        Check(label.font==StageUITheme.HeadingFont&&label.fontSize>=15&&(label.fontStyle&FontStyles.Bold)!=0&&label.outlineWidth>=.1f,"large heavy outlined label "+label.text);
                        Check(Vector3.Dot(label.transform.forward,Camera.main.transform.forward)>.999f,"label faces gameplay camera");
                        Check(label.text!="A ∪ B","intersection never labeled as union");
                    }
                    Check(stage.TimeRemaining==time&&!stage.Started,"introduction preserves waiting state and countdown");
                    yield return Capture("Library/SetsIntro_C"+challenge+"_P"+page+"_"+Screen.width+"x"+Screen.height+".png");
                }
                int before=intro.PageIndex;
                for(int attempt=0;attempt<3&&intro.IsOpen&&intro.PageIndex==before;attempt++)
                {
                    yield return Until(()=>intro.NextButton.IsInteractable(),6,"camera framing enables Next");
                    yield return Tap(intro.NextButton);yield return new WaitForSecondsRealtime(.25f);
                }
                Check(!intro.IsOpen||intro.PageIndex!=before,"Next advances after camera framing settles");
            }
            Check(!intro.IsOpen&&!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.enabled,"introduction cleanup C"+challenge+" open="+intro.IsOpen+" blocked="+GameInput.Instance.GameplayInputBlocked+" player="+Player.LocalInstance.enabled+" page="+intro.PageIndex+" seen="+intro.HasSeen(challenge));
        }
        var border=GameObject.Find("StageBorderDecorations");Check(border!=null&&border.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled),"new scenery does not alter collisions");
        Check(border.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader.isSupported)),"existing scenery materials render correctly");
        Log("All five challenge introductions at landscape and portrait: heavy labels, correct intersection, camera/input/timer cleanup. Closed border has no gameplay colliders.");
        // The normal guide keeps its own pause snapshot while each recording is decoded.
        var help=UnityEngine.Object.FindFirstObjectByType<SetssssHelpUI>();var helpButton=Field<Button>(help,"helpButton");
        for(int size=0;size<3;size++)
        {
            BookMapMobileChecks.SetSize(new[]{6,12,3}[size]);yield return new WaitForSecondsRealtime(.4f);
            var safe=ScreenRect((RectTransform)stage.Hud.Hud.parent);var hud=ScreenRect(stage.Hud.Hud);
            Check(Contains(safe,hud),"compact HUD fits safe area");
            foreach(var label in stage.Hud.Hud.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate();Check(!label.isTextOverflowing,"HUD text fits: "+label.text);}
            float time=stage.TimeRemaining;yield return Tap(helpButton);yield return Until(()=>OpenPage()!=null,4,"Help opens");
            var guide=OpenPage();yield return Run(guide);Check(Mathf.Abs(stage.TimeRemaining-time)<.1f,"video playback consumes no countdown time");
            yield return Tap(guide.BeginButton);yield return null;
            Check(!guide.gameObject.activeInHierarchy&&guide.GameplayVideo.targetTexture==null&&!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.enabled,"Let's Begin restores controls without replay");
            Log(Screen.width+"x"+Screen.height+": HUD, four clips, exact captions, touch Pause/Replay/Begin, decoded two-second fades and input/time restore.");
        }
        RemoveTouchscreen();Log("ALL SETS PRESENTATION AND VIDEO CHECKS PASSED");
    }
    static IEnumerator Solve(Button hand)
    {
        int number=int.Parse(stage.ActiveCondition.setA.Concat(stage.ActiveCondition.setB).Single());
        var block=Field<GrabbableObject[]>(stage,"numberedBlocks").Single(b=>b.GetComponent<SetssssNumberedBlock>().Value==number);
        var pickup=block.transform.position+Vector3.back*1.8f;
        pickup.y=Field<Transform>(stage,"spawner").GetComponent<Renderer>().bounds.max.y+.12f;
        PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,pickup,Quaternion.identity);
        yield return new WaitForSecondsRealtime(.25f);yield return Tap(hand);
        Check(Player.LocalInstance.GetHeldObject()==block,"actual hand pickup");
        var region=Field<Bounds>(stage,stage.ActiveCondition.setA.Length>0?"aCircleBounds":"bCircleBounds");
        var point=region.center+Vector3.right*(stage.ActiveCondition.setA.Length>0?-4:4);Player.LocalInstance.transform.rotation=Quaternion.identity;
        var offset=Player.LocalInstance.GetHoldPoint().position-Player.LocalInstance.transform.position;
        var box=block.GetComponent<BoxCollider>();var meshOffset=Player.LocalInstance.GetHoldPoint().rotation*Vector3.Scale(box.center,block.transform.lossyScale);
        Position(point-offset-meshOffset);yield return new WaitForSecondsRealtime(.3f);yield return Tap(hand);
        Check(Player.LocalInstance.GetHeldObject()==null,"actual hand drops naturally");yield return new WaitForSecondsRealtime(.8f);
        yield return Until(()=>(bool)stage.GetType().GetMethod("BlocksAreSettled",Private).Invoke(stage,null),10,"physical blocks settle before Submit");
        yield return Tap(Field<Button>(stage,"submitButton"));
    }
    static void Position(Vector3 point){point.y=2.55f;PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,point,Quaternion.identity);}
    static IEnumerator Capture(string path){yield return new WaitForEndOfFrame();var t=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
    public static IEnumerator Run(StageHelpPage page, string scene = "Sets", string[] captions = null, string report = Report)
    {
        string[] expectedCaptions = captions ?? Captions;
        string evidenceKey = StageHelpVideoCatalog.ResourceKey(scene);
        bool ownsTouchscreen = touchscreen == null || !touchscreen.added;
        if (ownsTouchscreen) { touchscreen = InputSystem.AddDevice<Touchscreen>(); finger = 400; }
        Check(page != null && page.isActiveAndEnabled, "Help page is active");
        Check(page.VideoCount == 4 && StageHelpVideoCatalog.VideoCount(scene) == 4, "exactly four ordered " + scene + " recordings");
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
            Check(video.clip.name == StageHelpVideoCatalog.ClipResourceKey(scene, index), "ordered clip resource " + index);
            yield return null; Canvas.ForceUpdateCanvases();
            Check(caption.text == StageHelpVideoCatalog.CaptionAt(scene, video.time, index) && StageHelpVideoCatalog.AllCaptions(scene).Contains(expectedCaptions[index]), "exact requested caption " + (index + 1));
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
                ScreenCapture.CaptureScreenshot("Library/" + evidenceKey + "HelpPlaylist_" + Screen.width + "x" + Screen.height + ".png");
            }
            yield return CheckFade(video,index,report,evidenceKey);
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

    static IEnumerator CheckFade(VideoPlayer video,int index,string report = Report,string evidenceKey = "Sets")
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
            if(i==2)File.WriteAllBytes("Library/" + evidenceKey + "EncodedBlack_"+index+".png",pixels.EncodeToPNG());
            UnityEngine.Object.Destroy(pixels);RenderTexture.active=prior;
        }
        Check(luma[0]>.05f,"encoded scene remains visible when two-second fade begins");
        Check(luma[1]<luma[0]*.8f && luma[1]>luma[0]*.2f,"encoded fade is gradual at its midpoint");
        Check(luma[2]<.005f,"encoded clip reaches black before next recording");
        File.AppendAllText(report,"PASS Clip "+(index+1)+" encoded two-second fade luminance: "+string.Join(" / ",luma.Select(x=>x.ToString("F3")))+"\n");
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
    static void Check(bool valid, string detail) { if (!valid) throw new InvalidOperationException("Sets Help playlist: " + detail); }
    static void Log(string detail) => File.AppendAllText(Report, "PASS " + detail + "\n");
}

#endif
