using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>Opt-in isolated first-entry lifecycle and actual VideoPlayer decoding checks.</summary>
[InitializeOnLoad]
public static class ArchiveIntroPlaybackChecks
{
    const string Key="LogicLegends.ArchiveIntro.Check",Output="output/Cinematics/OpenArchiveTeaser/EditSource/";
    const string ClipPath="Assets/Assets/Video_Cutscene/LogicLegends_OpenArchive_Story.mp4";
    [Serializable]class Report
    {
        public bool passed,firstEntryShows,duplicateTapIgnored,completionPersists,returnEntryBypasses,secondAccountShows,skipPersists,skipCallbackOnce,errorOpensMenu,errorNotWatched,missingClipBypasses;
        public long decodedFrame;public ushort audioTracks;public string error,scope="Isolated Unity Play Mode. Synthetic account IDs only; no authentication or Photon connections.";
    }
    static Report report;static VideoClip clip;static MultiplayerMenuManager owner;static VideoPlayer video;static int callbacks,phase;static double began;static string uidA,uidB;
    static readonly BindingFlags Flags=BindingFlags.Static|BindingFlags.NonPublic;
    static ArchiveIntroPlaybackChecks(){EditorApplication.playModeStateChanged+=State;}
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Use Edit Mode.");
        if(SceneManager.GetActiveScene().isDirty)throw new Exception("Preserve unsaved scene before isolated test.");
        SessionState.SetString(Key+".restore",SceneManager.GetActiveScene().path);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene,"Assets/Cinematics/OpenArchiveTeaser/PlaybackChecks.unity");
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void Identity(string uid)
    {
        typeof(TrialBackendClient).GetField("ValidationTransport",Flags).SetValue(null,new Func<string,string,Task<string>>((a,b)=>Task.FromResult("{}")));
        typeof(TrialBackendClient).GetField("ValidationUid",Flags).SetValue(null,uid);
    }
    static ArchiveFirstMultiplayerIntro Intro()=>UnityEngine.Object.FindFirstObjectByType<ArchiveFirstMultiplayerIntro>();
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            report=new Report();began=EditorApplication.timeSinceStartup;phase=0;callbacks=0;
            uidA="archive-intro-check-"+Guid.NewGuid().ToString("N");uidB=uidA+"-other";
            Identity(uidA);clip=AssetDatabase.LoadAssetAtPath<VideoClip>(ClipPath);if(clip==null){Finish("Missing delivery clip.");return;}
            var camera=new GameObject("Playback Test Camera").AddComponent<Camera>();camera.gameObject.AddComponent<AudioListener>();
            var eventSystem=new GameObject("Playback Test Input",typeof(UnityEngine.EventSystems.EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            owner=new GameObject("Offline Menu Owner").AddComponent<MultiplayerMenuManager>();
            report.firstEntryShows=ArchiveFirstMultiplayerIntro.TryPlay(owner,clip,()=>callbacks++);
            report.duplicateTapIgnored=ArchiveFirstMultiplayerIntro.TryPlay(owner,clip,()=>callbacks+=100)&&UnityEngine.Object.FindObjectsByType<ArchiveFirstMultiplayerIntro>(FindObjectsSortMode.None).Length==1;
            video=Intro().GetComponent<VideoPlayer>();report.audioTracks=clip.audioTrackCount;
            EditorApplication.update+=Tick;
        }
        else if(state==PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update-=Tick;SessionState.SetBool(Key,false);
            var restore=SessionState.GetString(Key+".restore","");if(!string.IsNullOrEmpty(restore))EditorSceneManager.OpenScene(restore,OpenSceneMode.Single);
        }
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-began>80){Finish("Playback lifecycle check timed out.");return;}
            if(phase==0&&video!=null&&video.isPrepared&&video.frame>5)
            {
                // Decode the end and let the actual loopPointReached event complete the flow.
                report.decodedFrame=video.frame;phase=1;video.time=clip.length-1.1;video.Play();
            }
            else if(phase==1&&!ArchiveFirstMultiplayerIntro.IsShowing)
            {
                report.completionPersists=ArchiveFirstMultiplayerIntro.HasSeen(uidA)&&callbacks==1;
                report.returnEntryBypasses=!ArchiveFirstMultiplayerIntro.TryPlay(owner,clip,()=>callbacks+=100);
                Identity(uidB);report.secondAccountShows=ArchiveFirstMultiplayerIntro.TryPlay(owner,clip,()=>callbacks++);phase=2;
            }
            else if(phase==2&&Intro()!=null)
            {
                var intro=Intro();intro.GetComponentInChildren<UnityEngine.UI.Button>().onClick.Invoke();intro.Skip();report.skipCallbackOnce=callbacks==2;report.skipPersists=ArchiveFirstMultiplayerIntro.HasSeen(uidB);phase=3;
            }
            else if(phase==3)
            {
                PlayerPrefs.DeleteKey(ArchiveFirstMultiplayerIntro.KeyFor(uidB));ArchiveFirstMultiplayerIntro.TryPlay(owner,clip,()=>callbacks++);
                var intro=Intro();typeof(ArchiveFirstMultiplayerIntro).GetMethod("OnError",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(intro,new object[]{intro.GetComponent<VideoPlayer>(),"Injected decoder failure for offline test"});
                report.errorOpensMenu=callbacks==3&&!ArchiveFirstMultiplayerIntro.IsShowing;report.errorNotWatched=!ArchiveFirstMultiplayerIntro.HasSeen(uidB);
                report.missingClipBypasses=!ArchiveFirstMultiplayerIntro.TryPlay(owner,null,()=>callbacks++);Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
    static void Finish(string error)
    {
        EditorApplication.update-=Tick;report.error=error;
        report.passed=string.IsNullOrEmpty(error)&&report.firstEntryShows&&report.duplicateTapIgnored&&report.completionPersists&&report.returnEntryBypasses&&report.secondAccountShows&&report.skipPersists&&report.skipCallbackOnce&&report.errorOpensMenu&&report.errorNotWatched&&report.missingClipBypasses&&report.decodedFrame>5&&report.audioTracks==1;
        if(uidA!=null)PlayerPrefs.DeleteKey(ArchiveFirstMultiplayerIntro.KeyFor(uidA));if(uidB!=null)PlayerPrefs.DeleteKey(ArchiveFirstMultiplayerIntro.KeyFor(uidB));PlayerPrefs.Save();
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"first-entry-playmode-checks.json",JsonUtility.ToJson(report,true));EditorApplication.ExitPlaymode();
    }
}
