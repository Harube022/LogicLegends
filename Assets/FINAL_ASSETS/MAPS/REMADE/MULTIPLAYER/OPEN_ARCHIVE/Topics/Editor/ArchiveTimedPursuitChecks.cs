using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.Tests.Monsters;
using Photon.Pun;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Opt-in, offline production-method fixtures plus real native deadline frames. Never saved into the scene.</summary>
    public static class ArchiveTimedPursuitChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope="Offline Unity Editor: both existing guardian profiles, 30/40-second boundary and checkpoint fixtures, paused sedation/distraction/capture ownership, actual Update on an unreachable target from a 29.6-second fixture to capture, and rendered victory/waiting/defeat UI. Test player placement and administrative result fixtures are excluded from rewards. Physical Android and independent remote clients are not claimed.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        const BindingFlags Hidden=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        const string Folder="Temp/ArchiveTimedPursuit";
        static Report report;
        static IEnumerator flow;
        static double next;
        public static string Status{get;private set;}="Idle";
        static object Call(object obj,string name,params object[] args)
        {
            try{return obj.GetType().GetMethod(name,Hidden).Invoke(obj,args);}
            catch(TargetInvocationException e){throw e.InnerException??e;}
        }
        static void Set(object obj,string name,object value)=>obj.GetType().GetProperty(name,Hidden).SetValue(obj,value);
        static void Field(object obj,string name,object value)=>obj.GetType().GetField(name,Hidden).SetValue(obj,value);
        static void Check(bool condition,string label)
        {report.checks.Add((condition?"PASS ":"FAIL ")+label);if(!condition)throw new InvalidOperationException(label);}
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Timed Pursuit and Result Screen (Offline Play)")]
        public static void Begin()
        {
            if(flow!=null||!Application.isPlaying||PhotonNetwork.InRoom||ArchiveTopicSession.Current?.Ready!=true)
                throw new InvalidOperationException("Use fresh offline MultiplayerGameplay Play Mode.");
            Directory.CreateDirectory(Folder);report=new Report();flow=Run();next=0;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayState;Status="Waiting for the existing match intro";
        }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(!Application.isPlaying)throw new InvalidOperationException("Play Mode ended during verification.");
                if(!flow.MoveNext()){Finish();return;}
                next=EditorApplication.timeSinceStartup+(flow.Current is float pause?pause:0);
            }
            catch(Exception e){report.errors.Add(Status+": "+e);Finish();}
        }
        static void PlayState(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode&&flow!=null){report.errors.Add("Interrupted");Finish();}}
        static void Finish()
        {
            var previous=flow;flow=null;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayState;
            try{(previous as IDisposable)?.Dispose();}catch(Exception e){report.errors.Add("Restore: "+e);}
            report.passed=report.errors.Count==0&&report.checks.Count>0;Status=report.passed?"Passed":"Failed";
            File.WriteAllText(Folder+"/Verification.json",JsonUtility.ToJson(report,true));
            Debug.Log("Timed pursuit / match result verification: "+Status+" ("+report.checks.Count+")");
        }
        static object Target(Player player)
        {
            var type=typeof(KhezuHuntingController).GetNestedType("Target",Hidden);var target=Activator.CreateInstance(type);
            type.GetField("transform").SetValue(target,player.transform);type.GetField("capsule").SetValue(target,player.GetComponent<CharacterController>());
            type.GetField("name").SetValue(target,"QA local runner");return target;
        }
        static void Resume(KhezuHuntingController hunt,float elapsed,int actor=-1)
        {
            var state=hunt.GetSnapshot();state.state=(int)KhezuHuntState.Hunting;state.awake=true;state.pending=false;
            state.mistakes=state.stage=4;state.target=actor;state.caught=0;state.pursuitSeconds=elapsed;
            state.animation="Idle";state.animationSpeed=1;state.counterPhase=state.personalityPhase=0;
            hunt.ApplySnapshot(state,true);
        }
        static void Place(Player player,Vector3 position)
        {var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=position;cc.enabled=true;player.ResetMotionAfterTeleport();Physics.SyncTransforms();}
        static IEnumerator Run()
        {
            var session=ArchiveTopicSession.Current;
            while(Player.LocalInstance==null||!session.hunting.Initialized||ArchiveMatchCinematic.Covers(session))yield return .1f;
            var player=Player.LocalInstance;var hunt=session.hunting;var selector=session.worldProgression.selector;
            var selected=selector.Selected;var original=hunt.GetSnapshot();var originalTeam=JsonUtility.ToJson(session.State);
            var position=player.transform.position;bool oldPlayer=player.enabled,oldSession=session.enabled,oldHunt=hunt.enabled;
            bool oldThreat=session.worldProgression.suppressThreatsForVerification,oldDefeat=session.suppressDefeatForVerification;
            var explore=UnityEngine.Object.FindFirstObjectByType<LogicLegends.OpenArchiveTesting.OpenArchiveExplorationSession>();bool oldExplore=explore!=null&&explore.enabled;
            int oldSize=SelectedSize();
            session.enabled=false;session.suppressDefeatForVerification=true;session.worldProgression.suppressThreatsForVerification=true;
            player.enabled=false;if(explore!=null)explore.enabled=false;hunt.enabled=false;
            try
            {
                Check(UnityEngine.Object.FindObjectsByType<Exploration.ArchiveSprintStamina>(FindObjectsSortMode.None).Length==0,"Saved scene and runtime spawner create no stamina component");
                var legacy=new GameObject("Legacy_Stamina_QA").AddComponent<Exploration.ArchiveSprintStamina>();legacy.Bind(player);legacy.enabled=true;
                Check(!legacy.enabled&&!player.IsRunningSuppressed&&!legacy.HudVisible,"Old serialized stamina cannot restrict sprint or show a HUD");UnityEngine.Object.Destroy(legacy.gameObject);
                var target=Target(player);
                var secondary=session.worldProgression.secondary;
                if(secondary!=null)
                {
                    bool wasActive=secondary.gameObject.activeSelf;secondary.gameObject.SetActive(true);yield return .15f;
                    var secondSnapshot=secondary.GetSnapshot();
                    try
                    {
                        // Daytime presentation hides the night guardian during the initialization yield.
                        // Reactivate only for this synchronous capture-ownership fixture.
                        secondary.gameObject.SetActive(true);
                        Check(KhezuHuntingController.ActiveMonsters.Contains(secondary),"Second guardian participates in shared capture ownership");
                        Resume(hunt,32);float before=hunt.PursuitElapsedSeconds;
                        Set(secondary,"State",KhezuHuntState.Caught);Set(secondary,"CaughtPlayerId",-999);
                        Call(hunt,"UpdateTimedPursuit",target,10f);
                        Check(hunt.PursuitElapsedSeconds==before,"Another guardian's active capture pauses this target's deadline");
                    }
                    finally
                    {
                        secondary.ApplySnapshot(secondSnapshot,true);secondary.gameObject.SetActive(wasActive);hunt.Request(KhezuCommand.Sleep);
                    }
                }
                foreach(var profile in new[]{DevelopmentMonster.Rajang,DevelopmentMonster.Khezu})
                {
                    Status=profile+": timer boundaries and interruption checks";
                    hunt.Request(KhezuCommand.Sleep);hunt.GetComponent<KhezuCinematicJumpscareController>().Cancel();
                    hunt.GetComponent<KhezuCaptureEncounterController>().SynchronizeBarrierMovement();
                    selector.Select(profile);Resume(hunt,0);
                    Check(selector.Selected==profile,profile+": the intended existing rig is active");
                    Call(hunt,"UpdateTimedPursuit",target,29.9f);
                    Check(!hunt.PursuitEnraged&&hunt.State!=KhezuHuntState.Caught,profile+": no deadline capture before 30 seconds");
                    Call(hunt,"UpdateTimedPursuit",target,.11f);Call(hunt,"ApplyTimedPursuitSpeed");float startSpeed=hunt.Agent.speed;
                    Check(hunt.PursuitEnraged&&hunt.PursuitSecondsRemaining>9.9f,profile+": warning starts at 30 seconds");
                    Call(hunt,"UpdateTimedPursuit",target,5f);Call(hunt,"ApplyTimedPursuitSpeed");float middleSpeed=hunt.Agent.speed;
                    Check(middleSpeed>startSpeed,profile+": chase speed increases through the pressure ramp");
                    var checkpoint=JsonUtility.FromJson<KhezuSnapshot>(JsonUtility.ToJson(hunt.GetSnapshot()));float retained=checkpoint.pursuitSeconds;
                    Resume(hunt,0);hunt.ApplySnapshot(checkpoint,false);hunt.AuthorityChanged();
                    Check(Mathf.Abs(hunt.PursuitElapsedSeconds-retained)<.01f,profile+": JSON checkpoint and authority change preserve earned pursuit time");
                    hunt.HoldTranquilized(true);Call(hunt,"UpdateTimedPursuit",target,8f);
                    Check(hunt.IsTranquilized&&Mathf.Abs(hunt.PursuitElapsedSeconds-retained)<.01f,profile+": tranquilizer pauses the timer without clearing it");hunt.HoldTranquilized(false);
                    Field(hunt,"beaconWasActive",true);Call(hunt,"UpdateTimedPursuit",target,5f);Field(hunt,"beaconWasActive",false);
                    Check(Mathf.Abs(hunt.PursuitElapsedSeconds-retained)<.01f,profile+": an active teammate distraction pauses the timer");
                    Resume(hunt,39.9f);Call(hunt,"ApplyTimedPursuitSpeed");Check(hunt.Agent.speed>middleSpeed,profile+": final pressure is faster than the middle ramp");
                    int captures=hunt.CaptureSerial,hearts=hunt.health.ActorHearts(-1);Vector3 monster=hunt.transform.position;
                    Call(hunt,"UpdateTimedPursuit",target,.2f);
                    Check(hunt.State==KhezuHuntState.Caught&&hunt.CaughtPlayerId==-1&&hunt.CaptureSerial==captures+1,profile+": 40 seconds enters the original capture exactly once");
                    Check(player.IsMovementLocked&&hunt.health.ActorHearts(-1)==hearts&&Vector3.Distance(monster,hunt.transform.position)<.001f,profile+": capture locks its player without damage or monster teleport");
                    Call(hunt,"UpdateTimedPursuit",target,5f);Check(hunt.CaptureSerial==captures+1,profile+": an already caught target cannot be captured again");
                    hunt.Request(KhezuCommand.Sleep);hunt.GetComponent<KhezuCaptureEncounterController>().SynchronizeBarrierMovement();
                    Check(hunt.PursuitElapsedSeconds==0,profile+": sleep/reset clears the old target timer");

                    // Native Update must also hit the limit when navigation cannot reach the runner.
                    Place(player,position+new Vector3(1500,0,1500));Resume(hunt,29.6f);hunt.enabled=true;
                    double began=EditorApplication.timeSinceStartup;float minimum=1000,maximum=0;
                    Status=profile+": native unreachable-target chase to 40 seconds";
                    while(hunt.State!=KhezuHuntState.Caught&&EditorApplication.timeSinceStartup-began<14)
                    {if(hunt.PursuitEnraged){minimum=Mathf.Min(minimum,hunt.Agent.speed);maximum=Mathf.Max(maximum,hunt.Agent.speed);}yield return .05f;}
                    report.checks.Add("Native chase observation: "+profile+", state="+hunt.State+", elapsed="+hunt.PursuitElapsedSeconds+", target="+hunt.TargetPlayerId+", locked="+player.IsMovementLocked);
                    Check(hunt.State==KhezuHuntState.Caught&&hunt.PursuitElapsedSeconds>=40,profile+": actual Update captures an unreachable target by its active deadline");
                    Check(maximum>minimum+2,profile+": native frames apply the rising chase speed");
                    hunt.enabled=false;hunt.Request(KhezuCommand.Sleep);hunt.GetComponent<KhezuCinematicJumpscareController>().Cancel();
                    hunt.GetComponent<KhezuCaptureEncounterController>().SynchronizeBarrierMovement();Place(player,position);yield return .1f;
                }
                Status="Rendered result UI and native review action";
                session.State.rewards.adminUsed=true;
                foreach(var station in session.State.stations)station.phase=2;
                for(int i=0;i<session.State.crystals.Count;i++)session.State.crystals[i].fullyRestored=i<4;
                session.State.exit.opened=session.State.exit.won=true;session.State.exit.wonAt=ArchiveTopicSession.Clock-30;
                session.State.exit.escaped.Clear();session.State.exit.escaped.Add(new ArchiveEscape{actor=-1,anchor=position,time=ArchiveTopicSession.Clock});
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(2340,1080),new Vector2Int(1280,720)})
                {
                    Resolution(size.x,size.y);yield return .45f;Canvas.ForceUpdateCanvases();
                    var ui=ArchiveExitUI.Current;Check(ui.ResultVisible&&ui.ResultMetricsText.StartsWith("10 / 10"),"Result metrics read the existing match state at "+size);
                    var seal=ui.ReviewButton.transform.parent.GetComponentInChildren<ArchiveResultEmblemGraphic>();
                    var mesh=seal.canvasRenderer.GetMesh();
                    Check(seal.GetComponent<CanvasRenderer>()!=null&&mesh!=null&&mesh.vertexCount>0,"Victory seal has rendered geometry at "+size);
                    foreach(var text in ui.ReviewButton.transform.parent.GetComponentsInChildren<TMP_Text>())
                    {text.ForceMeshUpdate();Check(!text.isTextOverflowing,"Result label fits at "+size+": "+text.name);}
                    Check(!ui.ReviewButton.GetComponentInParent<StageResultLayout>().enabled,"Only this multiplayer card disables the Solo layout");
                    ScreenCapture.CaptureScreenshot(Folder+"/Victory-"+size.x+"x"+size.y+".png");yield return .15f;
                }
                var exit=ArchiveExitUI.Current;
                Check(exit.ReviewButton.gameObject.activeSelf&&exit.ReturnButton.gameObject.activeSelf,"Both terminal result actions remain available");
                exit.ReviewButton.onClick.Invoke();yield return .1f;Check(ArchiveLearningRecapUI.IsVisible,"Review Learning still opens the existing detached recap");
                ArchiveLearningRecapUI.Current.CloseButton.onClick.Invoke();yield return .1f;
                session.State.exit.won=false;yield return .1f;
                Check(exit.ResultVisible&&!exit.ReturnButton.gameObject.activeSelf,"Escaped players wait for the team without an early party-return action");
                session.State.survival.defeated=true;session.State.survival.defeatReason=ArchiveTopicSession.MatchTimeoutReason;yield return .2f;
                Check(exit.ResultVisible&&exit.ReturnButton.gameObject.activeSelf,"Defeat keeps the return action and the same bounded layout");
                ScreenCapture.CaptureScreenshot(Folder+"/Defeat.png");yield return .15f;
            }
            finally
            {
                hunt.enabled=false;hunt.Request(KhezuCommand.Sleep);hunt.GetComponent<KhezuCaptureEncounterController>().SynchronizeBarrierMovement();
                Set(session,"State",JsonUtility.FromJson<ArchiveTeamState>(originalTeam));selector.Select(selected);hunt.ApplySnapshot(original,true);
                Place(player,position);player.enabled=oldPlayer;session.enabled=oldSession;hunt.enabled=oldHunt;
                session.suppressDefeatForVerification=oldDefeat;session.worldProgression.suppressThreatsForVerification=oldThreat;
                if(explore!=null)explore.enabled=oldExplore;SelectSize(oldSize);
            }
        }
        static int SelectedSize()
        {var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");return(int)type.GetProperty("selectedSizeIndex",Hidden).GetValue(EditorWindow.GetWindow(type));}
        static void SelectSize(int i)
        {var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");var window=EditorWindow.GetWindow(type);type.GetProperty("selectedSizeIndex",Hidden).SetValue(window,i);window.Repaint();}
        static void Resolution(int width,int height)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;var type=assembly.GetType("UnityEditor.GameViewSizes");
            var sizes=type.BaseType.GetProperty("instance",Hidden).GetValue(null);var group=type.GetProperty("currentGroup",Hidden).GetValue(sizes);
            int count=(int)group.GetType().GetMethod("GetTotalCount",Hidden).Invoke(group,null);var get=group.GetType().GetMethod("GetGameViewSize",Hidden);
            for(int i=0;i<count;i++)
            {var value=get.Invoke(group,new object[]{i});if((int)value.GetType().GetProperty("width",Hidden).GetValue(value)==width&&(int)value.GetType().GetProperty("height",Hidden).GetValue(value)==height){SelectSize(i);return;}}
            var ctor=assembly.GetType("UnityEditor.GameViewSize").GetConstructors().First(c=>c.GetParameters().Length==4);
            var size=ctor.Invoke(new object[]{Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"),1),width,height,"Pursuit QA "+width+"x"+height});
            group.GetType().GetMethod("AddCustomSize",Hidden).Invoke(group,new[]{size});SelectSize(count);
        }
    }
}
