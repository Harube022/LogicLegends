using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

// Isolated decoder check: no AuthManager or Firebase scene objects are loaded.
[InitializeOnLoad]
public static class NewUserIntroPlaybackChecks
{
    const string Key="LogicLegends.V4IntroDecode",Root="output/Cinematics/OpeningRemakeV2/EditSource/NewUserIntegration/";
    const string Clip="Assets/Assets/Video_Cutscene/LogicLegends_Opening_V4.mp4";
    const string TestScene="Assets/Cinematics/OpeningRemakeV2/IntroPlaybackCheck.unity";
    [Serializable] class Restore {public string[] paths;public bool[] loaded,active;}
    [Serializable] class Report {public bool passed;public string scope="Isolated native Unity Play Mode decoding; no authentication or account writes",error;public long mapFrame,pillarFrame;public double mapTime,pillarTime;}
    static VideoPlayer video;static RenderTexture target;static double began;static int stage;static long lastFrame=-1;static Report report;
    static NewUserIntroPlaybackChecks(){EditorApplication.playModeStateChanged+=State;}

    public static string Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        for(int i=0;i<SceneManager.sceneCount;i++){var s=SceneManager.GetSceneAt(i);if(s.isDirty||string.IsNullOrEmpty(s.path))throw new InvalidOperationException("Preserve unsaved scenes before the isolated video check.");}
        var restore=new Restore{paths=setup.Select(s=>s.path).ToArray(),loaded=setup.Select(s=>s.isLoaded).ToArray(),active=setup.Select(s=>s.isActive).ToArray()};
        SessionState.SetString(Key+".restore",JsonUtility.ToJson(restore));
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.SaveScene(scene,TestScene);
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();return "Isolated native intro playback check starting.";
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            report=new Report();began=EditorApplication.timeSinceStartup;stage=0;lastFrame=-1;
            video=new GameObject("V4 intro decoder check").AddComponent<VideoPlayer>();video.playOnAwake=false;video.audioOutputMode=VideoAudioOutputMode.None;video.clip=AssetDatabase.LoadAssetAtPath<VideoClip>(Clip);
            target=new RenderTexture(960,540,0,RenderTextureFormat.ARGB32);target.Create();video.renderMode=VideoRenderMode.RenderTexture;video.targetTexture=target;video.sendFrameReadyEvents=true;
            video.frameReady+=(v,index)=>lastFrame=index;video.errorReceived+=(v,message)=>{report.error=message;Finish(false);};
            EditorApplication.update+=Tick;video.Prepare();
        }
        else if(state==PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update-=Tick;SessionState.SetBool(Key,false);
            var saved=JsonUtility.FromJson<Restore>(SessionState.GetString(Key+".restore",""));
            var setup=saved.paths.Select((p,i)=>new SceneSetup{path=p,isLoaded=saved.loaded[i],isActive=saved.active[i]}).ToArray();EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-began>45){report.error="Native Play Mode decoding timed out.";Finish(false);return;}
            if(stage==0&&video.isPrepared){stage=1;video.time=20;lastFrame=-1;video.Play();}
            else if(stage==1&&lastFrame>=600){video.Pause();report.mapFrame=lastFrame;report.mapTime=video.time;Capture("native-v4-map.png");stage=2;video.time=60;lastFrame=-1;video.Play();}
            else if(stage==2&&lastFrame>=1800){video.Pause();report.pillarFrame=lastFrame;report.pillarTime=video.time;Capture("native-v4-pillar.png");Finish(true);}
        }
        catch(Exception e){report.error=e.ToString();Finish(false);}
    }
    static void Capture(string file)
    {
        var old=RenderTexture.active;RenderTexture.active=target;var frame=new Texture2D(960,540,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,960,540),0,0);frame.Apply();File.WriteAllBytes(Root+file,frame.EncodeToPNG());UnityEngine.Object.Destroy(frame);RenderTexture.active=old;
    }
    static void Finish(bool passed)
    {
        EditorApplication.update-=Tick;report.passed=passed;Directory.CreateDirectory(Root);File.WriteAllText(Root+"video-playmode-decoding.json",JsonUtility.ToJson(report,true));
        if(video!=null)video.Stop();if(target!=null){target.Release();UnityEngine.Object.Destroy(target);}EditorApplication.ExitPlaymode();
    }
}
