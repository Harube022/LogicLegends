using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Opt-in lifecycle checks against the real offline countdown and local film owner.</summary>
    public static class ArchiveMatchCinematicChecks
    {
        const string Folder="Temp/ArchiveMatchCinematics";
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scope="Saved MultiplayerGameplay, offline Unity Editor Play: real readiness/countdown/active transitions, camera and hero-material restoration, interruption and movement-lock ownership. No remote Photon rendering, physical Android or victory/reward validation claim.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        sealed class CameraState
        {
            public Camera camera;
            public int mask;
            public CameraClearFlags clear;
            public Color background;
            public bool enabled;
        }
        static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
        static readonly Dictionary<SkinnedMeshRenderer,Material[]> materials=new Dictionary<SkinnedMeshRenderer,Material[]>();
        static readonly List<CameraState> cameras=new List<CameraState>();
        static readonly Dictionary<Animator,bool> rootMotion=new Dictionary<Animator,bool>();
        static ArchiveTopicSession session;
        static ArchiveMatchCinematic cinematic;
        static Player player;
        static Camera gameplay;
        static Behaviour brain;
        static GameObject foreignOwner;
        static Report report;
        static bool active,originalEnabled,oldThreats,oldDefeat,brainEnabled;
        static float timeScale;
        static double next;
        public static string Status {get;private set;}="Idle";
        public static bool Running=>active;

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Match Cinematic Lifecycle (Offline Play)")]
        public static void Begin()
        {
            session=ArchiveTopicSession.Current;player=Player.LocalInstance;
            cinematic=UnityEngine.Object.FindFirstObjectByType<ArchiveMatchCinematic>();
            if(active||!Application.isPlaying||PhotonNetwork.InRoom||session==null||!session.Ready||player==null||cinematic==null||
                SceneManager.GetActiveScene().path!="Assets/Scenes/MultiplayerGameplay.unity")
                throw new InvalidOperationException("Use fresh saved MultiplayerGameplay offline Play with the installed match cinematic.");
            originalEnabled=cinematic.enabled;oldThreats=session.worldProgression.suppressThreatsForVerification;
            oldDefeat=session.suppressDefeatForVerification;timeScale=Time.timeScale;
            // A naturally running opening may already cover the view when the command
            // arrives. Restore it before capturing the genuine baseline resources.
            cinematic.enabled=false;
            Directory.CreateDirectory(Folder);report=new Report();active=true;next=0;stack.Clear();stack.Push(Flow());
            Status="Preparing cinematic baseline";
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayState;Application.logMessageReceived+=Log;
        }
        static void Tick()
        {
            if(!active)return;EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if(EditorApplication.timeSinceStartup<next)return;
                if(!Application.isPlaying)throw new InvalidOperationException("Play Mode stopped during cinematic checks.");
                if(stack.Count==0){Finish(true);return;}
                var current=stack.Peek();if(!current.MoveNext()){stack.Pop();return;}
                if(current.Current is IEnumerator nested)stack.Push(nested);
                else next=EditorApplication.timeSinceStartup+(current.Current is float delay?delay:0);
            }
            catch(Exception exception){report.errors.Add(Status+": "+exception);Finish(false);}
        }
        static void PlayState(PlayModeStateChange state)
        {if(active&&state==PlayModeStateChange.ExitingPlayMode){report.errors.Add("Play Mode interrupted.");Finish(false);}}
        static void Log(string message,string trace,LogType type)
        {if(active&&report.errors.Count<12&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))report.errors.Add(message+"\n"+trace);}
        static void Check(bool pass,string message)
        {report.checks.Add((pass?"PASS ":"FAIL ")+message);if(!pass)throw new InvalidOperationException(message);}
        static IEnumerator Wait(Func<bool> ready,string message,float seconds=12)
        {
            double deadline=EditorApplication.timeSinceStartup+Mathf.Clamp(seconds,.1f,30);
            while(!ready())
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException(message);
                yield return .025f;
            }
        }
        static bool MaterialsRestored()=>materials.All(pair=>pair.Key==null||pair.Key.sharedMaterials.SequenceEqual(pair.Value));
        static bool CamerasRestored()=>cameras.All(s=>s.camera==null||s.camera.enabled==s.enabled&&s.camera.cullingMask==s.mask&&
            s.camera.clearFlags==s.clear&&s.camera.backgroundColor==s.background);
        static void Restored(string context,bool checkRootMotion=true)
        {
            Check(!cinematic.IsPlaying&&cinematic.LocksHeld==0&&cinematic.HeroOverridesCount==0,context+": film, owned locks and hero overrides are cleared");
            Check(MaterialsRestored(),context+": every original skinned shared-material array is restored");
            Check(CamerasRestored(),context+": original camera enable, mask, clear flags and background are restored");
            Check(brain==null||brain.enabled==brainEnabled,context+": the real Cinemachine brain retains its enabled state");
            Check(cinematic.FilmCamera==null||!cinematic.FilmCamera.enabled,context+": offscreen film camera stops rendering");
            if(checkRootMotion)Check(rootMotion.All(p=>p.Key==null||p.Key.applyRootMotion==p.Value),context+": original Animator root-motion settings are restored");
            Check(Mathf.Approximately(Time.timeScale,timeScale),context+": no shared Time.timeScale change");
        }
        static void WindowChecks()
        {
            Check(ArchiveMatchCinematic.WindowProgress(0,-1,5)<0,"An unset receipt cannot play a film");
            Check(ArchiveMatchCinematic.WindowProgress(2,3,5)<0,"A future receipt cannot play early");
            Check(ArchiveMatchCinematic.WindowProgress(10,10,5)==0,"The authoritative start is progress zero");
            Check(Mathf.Approximately(ArchiveMatchCinematic.WindowProgress(12.5,10,5),.5f),"Shared-clock midpoint maps to half progress");
            Check(ArchiveMatchCinematic.WindowProgress(15,10,5)<0,"The exact window endpoint is already finished");
            Check(ArchiveMatchCinematic.WindowProgress(200,10,5)<0,"A stale late-join receipt cannot replay a finished film");
            Check(ArchiveMatchCinematic.WindowProgress(10,10,0)<0&&ArchiveMatchCinematic.WindowProgress(10,10,-1)<0,"Zero and negative durations never create a film window");
        }
        static IEnumerator Flow()
        {
            yield return .10f;
            session.suppressDefeatForVerification=true;session.worldProgression.suppressThreatsForVerification=true;
            gameplay=Camera.main;Check(gameplay!=null&&gameplay.enabled,"The existing enabled MainCamera is available");
            materials.Clear();foreach(var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))materials[skin]=skin.sharedMaterials.ToArray();
            rootMotion.Clear();foreach(var animator in player.GetComponentsInChildren<Animator>(true))rootMotion[animator]=animator.applyRootMotion;
            cameras.Clear();foreach(var camera in Camera.allCameras.Where(c=>c.targetTexture==null&&c.gameObject.scene==session.gameObject.scene))
                cameras.Add(new CameraState{camera=camera,enabled=camera.enabled,mask=camera.cullingMask,clear=camera.clearFlags,background=camera.backgroundColor});
            brain=gameplay.GetComponents<MonoBehaviour>().FirstOrDefault(b=>b!=null&&b.GetType().Name=="CinemachineBrain");brainEnabled=brain!=null&&brain.enabled;
            Check(materials.Count>0&&materials.Values.Any(m=>m.Length>0),"The real existing explorer rig has captured source materials");
            Check(!player.IsMovementLocked,"The fixture begins without a foreign player movement lock");
            WindowChecks();

            Status="Automatic readiness and five-second summoning";
            session.Initialize(117531);cinematic.enabled=true;
            Check(session.MatchTimePhase==ArchiveMatchTimePhase.WaitingForPlayers||session.MatchTimePhase==ArchiveMatchTimePhase.Countdown,"Initialize uses the real readiness flow instead of an edited countdown timestamp");
            yield return Wait(()=>session.MatchTimePhase==ArchiveMatchTimePhase.Countdown,"The authority reaches Countdown automatically",23);
            double countdown=session.State.matchTime.countdownStartedAt;
            Check(countdown>=0&&session.State.matchTime.startedAt<0,"The stored countdown receipt exists before the gameplay deadline starts");
            yield return Wait(()=>cinematic.IsPlaying&&cinematic.ShotName=="Summoning"&&cinematic.LocksHeld>0&&cinematic.HeroOverridesCount>0,"The real countdown activates summoning and hero reveal",4);
            Check(player.IsMovementLocked&&!session.CanAct(session.LocalActor),"Topic actions and character movement are blocked during summoning");
            Check(gameplay.enabled&&gameplay.cullingMask==0,"The real MainCamera stays enabled while only its covered world rendering is suppressed");
            Check(brain==null||brain.enabled==brainEnabled,"The Cinemachine brain remains available throughout the summoning shot");
            Check(materials.Any(p=>p.Key!=null&&!p.Key.sharedMaterials.SequenceEqual(p.Value)),"The real existing hero rig receives temporary summon materials");
            Check(cinematic.FilmCamera!=null&&cinematic.FilmCamera.enabled&&cinematic.FilmCamera.targetTexture!=null,"The native film camera renders to its bounded texture");
            Check(cinematic.FilmCamera.targetTexture.width<=1280,"The cinematic rendering width is capped for mobile cost");
            yield return Wait(()=>cinematic.TeamRevealProgress>=.38f||session.MatchTimePhase!=ArchiveMatchTimePhase.Countdown,"The team reveal reaches its middle beat before the guardian showcase",4);
            Check(session.MatchTimePhase==ArchiveMatchTimePhase.Countdown&&cinematic.Progress<.90f,"The genuine midpoint is captured before gameplay starts");
            Check(!cinematic.GuardianIntroVisible&&cinematic.HeroOverridesCount>0,"Players appear first, ahead of the active guardian showcase");
            ScreenCapture.CaptureScreenshot(Folder+"/Summoning.png");yield return .12f;
            yield return Wait(()=>session.MatchTimePhase==ArchiveMatchTimePhase.Active,"Gameplay starts after the original five-second countdown",8);
            yield return .15f;
            Check(Math.Abs(session.State.matchTime.startedAt-(countdown+ArchiveTopicSession.MatchCountdownSeconds))<.00001,"The gameplay clock starts at countdownStartedAt + 5 without a cinematic timer extension");
            Restored("Normal completion");
            Check(!player.IsMovementLocked&&session.CanAct(session.LocalActor),"The existing player can act after the automatic camera handoff");
            Check(!Resources.FindObjectsOfTypeAll<Material>().Any(m=>m.name=="Archive_Summon_Temporary"),"Temporary hero reveal materials are destroyed after normal completion");
            yield return Wait(()=>File.Exists(Folder+"/Summoning.png"),"Native summoning screenshot is written",3);

            Status="Interrupted film and foreign-lock ownership";
            cinematic.PreviewSummoning();
            yield return Wait(()=>cinematic.PreviewPlaying&&cinematic.LocksHeld>0&&cinematic.HeroOverridesCount>0,"The existing offline preview activates",4);
            yield return 1f;
            foreignOwner=new GameObject("Archive_Cinematic_QA_Foreign_Lock");
            var anchor=player.transform.position;player.AcquireMovementLock(foreignOwner,"Cinematic lifecycle fixture foreign owner",anchor,150);
            cinematic.enabled=false;yield return .15f;
            Restored("Component interruption",false);
            Check(player.IsMovementLocked&&(player.MovementLockPosition-anchor).sqrMagnitude<.0001f,"Film cleanup preserves another owner's higher-priority movement lock");
            Check(rootMotion.All(p=>p.Key==null||!p.Key.applyRootMotion),"Another movement-lock owner retains root-motion suppression until its own release");
            player.ReleaseMovementLock(foreignOwner);UnityEngine.Object.Destroy(foreignOwner);foreignOwner=null;
            yield return .05f;Check(!player.IsMovementLocked,"Releasing the fixture's own lock restores movement without a leaked cinematic owner");
            Check(rootMotion.All(p=>p.Key==null||p.Key.applyRootMotion==p.Value),"Root-motion settings restore after the final foreign owner releases its lock");
            cinematic.enabled=true;yield return .15f;
            Check(!cinematic.IsPlaying&&!cinematic.PreviewPlaying,"Re-enabling during Active does not replay a consumed intro or interrupted preview");
            Check(!Resources.FindObjectsOfTypeAll<Material>().Any(m=>m.name=="Archive_Summon_Temporary"),"Interrupted reveal materials are destroyed");

            Status="Explicit preview cancellation";
            cinematic.PreviewSummoning();yield return Wait(()=>cinematic.PreviewPlaying&&cinematic.LocksHeld>0,"A subsequent offline preview is reusable",4);
            cinematic.CancelPreview();yield return .15f;
            Restored("Explicit cancellation");
            Check(!player.IsMovementLocked&&session.CanAct(session.LocalActor),"Cancelling an offline preview returns the existing player to gameplay");
            Status="Completed";
        }
        static void Finish(bool success)
        {
            if(!active)return;active=false;
            EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayState;Application.logMessageReceived-=Log;
            try
            {
                if(cinematic!=null){cinematic.CancelPreview();cinematic.enabled=false;cinematic.enabled=originalEnabled;}
                if(player!=null&&foreignOwner!=null)player.ReleaseMovementLock(foreignOwner);
                if(foreignOwner!=null)UnityEngine.Object.Destroy(foreignOwner);foreignOwner=null;
                if(session!=null){session.suppressDefeatForVerification=oldDefeat;if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=oldThreats;}
            }
            catch(Exception exception){report.errors.Add("Cleanup: "+exception);}
            report.passed=success&&report.errors.Count==0;
            File.WriteAllText(Folder+"/Play.json",JsonUtility.ToJson(report,true));
            Status=report.passed?"Passed":"Failed";
            Debug.Log("Match cinematic lifecycle checks "+Status+" — "+report.checks.Count+" checks. "+Folder+"/Play.json");
            stack.Clear();
        }
    }
}
