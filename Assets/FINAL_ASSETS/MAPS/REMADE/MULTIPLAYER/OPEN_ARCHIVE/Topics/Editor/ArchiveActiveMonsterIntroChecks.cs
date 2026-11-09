using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.Tests.Monsters;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Opt-in checks against both real selected rigs and the existing readiness/countdown owner.</summary>
    public static class ArchiveActiveMonsterIntroChecks
    {
        const string Folder="Temp/ArchiveMatchCinematics";
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scope="Offline Unity Editor Play in saved MultiplayerGameplay. Both selected species, delayed scene preparation, team-then-guardian countdown and offline preview, source-rig/state preservation, player handoff, and interruption/foreign-lock ownership. No Android, remote Photon rendering, or real multi-device test claim.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static readonly Stack<IEnumerator> flow=new Stack<IEnumerator>();
        static readonly Dictionary<Renderer,bool> sourceVisibility=new Dictionary<Renderer,bool>();
        static readonly Dictionary<SkinnedMeshRenderer,Material[]> playerMaterials=new Dictionary<SkinnedMeshRenderer,Material[]>();
        static ArchiveTopicSession session;
        static ArchiveMatchCinematic cinematic;
        static DevelopmentMonsterSelection selector;
        static KhezuHuntingController monster;
        static Animator sourceAnimator;
        static Player player;
        static Camera gameplay;
        static Report report;
        static GameObject foreignOwner;
        static DevelopmentMonster originalSpecies;
        static bool active,originalFilmEnabled,oldThreats,oldDefeat;
        static int cameraMask;
        static CameraClearFlags cameraClear;
        static Vector3 position,scale;
        static Quaternion rotation;
        static RuntimeAnimatorController controller;
        static float animatorSpeed,timeScale;
        static double next;
        public static string Status {get;private set;}="Idle";
        public static bool Running=>active;

        [MenuItem("Tools/Logic Legends/Open Archive/Cinematics/Test Active Monster Intro (Offline Play)")]
        public static void Begin()
        {
            session=ArchiveTopicSession.Current;player=Player.LocalInstance;
            cinematic=session==null?null:session.GetComponent<ArchiveMatchCinematic>();
            selector=session?.worldProgression?.selector;monster=session?.hunting;
            if(active||!Application.isPlaying||PhotonNetwork.InRoom||session==null||!session.Ready||player==null||cinematic==null||
                selector==null||monster==null||!monster.Initialized||SceneManager.GetActiveScene().path!="Assets/Scenes/MultiplayerGameplay.unity")
                throw new InvalidOperationException("Use fresh saved MultiplayerGameplay offline Play with the selected guardian and match cinematic ready.");
            if(!selector.CanSwap)throw new InvalidOperationException("The offline guardian selector must be available: "+selector.SwapBlockedReason);
            originalFilmEnabled=cinematic.enabled;originalSpecies=selector.Selected;
            oldThreats=session.worldProgression.suppressThreatsForVerification;oldDefeat=session.suppressDefeatForVerification;timeScale=Time.timeScale;
            cinematic.enabled=false;
            report=new Report();Directory.CreateDirectory(Folder);flow.Clear();flow.Push(Run());active=true;next=0;
            Status="Preparing guardian intro fixtures";
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayState;Application.logMessageReceived+=Log;
        }
        static void Tick()
        {
            if(!active)return;EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if(EditorApplication.timeSinceStartup<next)return;
                if(!Application.isPlaying)throw new InvalidOperationException("Play Mode ended during guardian intro verification.");
                if(flow.Count==0){Finish(true);return;}
                var current=flow.Peek();if(!current.MoveNext()){flow.Pop();return;}
                if(current.Current is IEnumerator nested)flow.Push(nested);
                else next=EditorApplication.timeSinceStartup+(current.Current is float delay?delay:0);
            }
            catch(Exception exception){report.errors.Add(Status+": "+exception);Finish(false);}
        }
        static void PlayState(PlayModeStateChange state)
        {if(active&&state==PlayModeStateChange.ExitingPlayMode){report.errors.Add("Play Mode interrupted.");Finish(false);}}
        static void Log(string message,string trace,LogType type)
        {if(active&&report.errors.Count<12&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))report.errors.Add(message+"\n"+trace);}
        static void Check(bool pass,string text)
        {report.checks.Add((pass?"PASS ":"FAIL ")+text);if(!pass)throw new InvalidOperationException(text);}
        static IEnumerator Wait(Func<bool> predicate,string text,float timeout=12)
        {
            double end=EditorApplication.timeSinceStartup+Mathf.Clamp(timeout,.1f,30);
            while(!predicate())
            {if(EditorApplication.timeSinceStartup>end)throw new TimeoutException(text);yield return .02f;}
        }
        static string Normalize(string value)=>new string((value??"").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        static bool VisibilityRestored()=>sourceVisibility.All(pair=>pair.Key==null||pair.Key.forceRenderingOff==pair.Value);
        static bool MaterialsRestored()=>playerMaterials.All(pair=>pair.Key==null||pair.Key.sharedMaterials.SequenceEqual(pair.Value));
        static bool GuardianGraphsStopped()
        {
            // Unity 6.3 no longer exposes the global GetAllGraphs enumerator.
            // Inspect only the resource owned by this cinematic instead.
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var owner=typeof(ArchiveMatchCinematic).GetField("guardianIntro",flags).GetValue(cinematic);
            if(owner==null)return true;
            var graph=(PlayableGraph)typeof(ArchiveActiveMonsterIntro).GetField("graph",flags).GetValue(owner);
            return !graph.IsValid()||!graph.IsPlaying();
        }
        static void SourceBaseline()
        {
            sourceVisibility.Clear();foreach(var renderer in monster.GetComponentsInChildren<Renderer>(true))sourceVisibility[renderer]=renderer.forceRenderingOff;
            position=monster.transform.position;rotation=monster.transform.rotation;scale=monster.transform.localScale;
            sourceAnimator=monster.GetComponent<Animator>();controller=sourceAnimator.runtimeAnimatorController;animatorSpeed=sourceAnimator.speed;
        }
        static void SourceUnchanged(string context)
        {
            Check(monster.State==KhezuHuntState.Sleeping&&monster.Mistakes==0&&monster.TargetPlayerId==0,context+": live guardian remains asleep with zero mistakes and no hunted target");
            Check((monster.transform.position-position).sqrMagnitude<.0001f&&Quaternion.Angle(monster.transform.rotation,rotation)<.01f&&
                (monster.transform.localScale-scale).sqrMagnitude<.0001f,context+": actual guardian position, rotation and scale remain unchanged");
            Check(sourceAnimator.runtimeAnimatorController==controller&&Mathf.Approximately(sourceAnimator.speed,animatorSpeed),
                context+": actual Animator controller and speed remain owned by the live encounter");
        }
        static IEnumerator Run()
        {
            yield return .10f;
            session.worldProgression.suppressThreatsForVerification=true;session.suppressDefeatForVerification=true;
            gameplay=Camera.main;Check(gameplay!=null&&gameplay.enabled,"Existing MainCamera is enabled before the fixtures");
            cameraMask=gameplay.cullingMask;cameraClear=gameplay.clearFlags;
            playerMaterials.Clear();foreach(var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))playerMaterials[skin]=skin.sharedMaterials.ToArray();
            Check(!player.IsMovementLocked,"Fixture begins without another movement-lock owner");
            Check(ArchiveTopicSession.MatchGuardianIntroSeconds==3&&ArchiveTopicSession.MatchCountdownSeconds==5,
                "Existing preparation and authoritative five-second countdown durations are unchanged");
            Check(ArchiveMatchCinematic.GuardianProgress(0,true)<0&&ArchiveMatchCinematic.GuardianProgress(.59f,true)<0&&
                Mathf.Approximately(ArchiveMatchCinematic.GuardianProgress(.6f,true),0)&&
                Mathf.Approximately(ArchiveMatchCinematic.GuardianProgress(.8f,true),.5f)&&
                ArchiveMatchCinematic.GuardianProgress(1,true)<0&&ArchiveMatchCinematic.GuardianProgress(.8f,false)<0,
                "Guardian beat follows the team during the last two seconds, with an exclusive end boundary");
            Check(Mathf.Approximately(ArchiveMatchCinematic.SummoningProgress(0,true),0)&&
                Mathf.Approximately(ArchiveMatchCinematic.SummoningProgress(.3f,true),.5f)&&
                Mathf.Approximately(ArchiveMatchCinematic.SummoningProgress(.6f,true),1)&&
                Mathf.Approximately(ArchiveMatchCinematic.SummoningProgress(1,true),1)&&
                Mathf.Approximately(ArchiveMatchCinematic.SummoningProgress(.2f,false),.2f),
                "Team reveal owns the first three seconds; disabling guardian presentation retains the original reveal mapping");
            yield return Species(DevelopmentMonster.Khezu,117531);
            yield return Species(DevelopmentMonster.Rajang,117532);

            Status="Guardian interruption and foreign lock ownership";
            cinematic.enabled=false;selector.Select(originalSpecies);yield return .05f;SourceBaseline();
            session.Initialize(117533);cinematic.enabled=true;
            yield return Wait(()=>cinematic.GuardianIntroVisible,"Guardian reveal starts for interruption fixture",7);
            yield return Wait(()=>ArchiveMatchCinematic.GuardianProgress(cinematic.Progress,true)>=.2f,"Guardian reveal reaches interruption beat",2);
            foreignOwner=new GameObject("Archive_Guardian_Intro_QA_Foreign_Lock");var anchor=player.transform.position;
            player.AcquireMovementLock(foreignOwner,"Guardian intro QA foreign lock",anchor,200);
            cinematic.enabled=false;yield return .12f;
            Check(!cinematic.GuardianIntroVisible&&!cinematic.IsPlaying&&cinematic.LocksHeld==0,
                "Disabling the film hides its guardian proxy and releases only its own cinematic locks");
            Check(GuardianGraphsStopped(),"Interrupted guardian reveal leaves no selected-guardian graph playing");
            Check(VisibilityRestored(),"Interrupted guardian reveal restores all original source-renderer force-off flags");
            SourceUnchanged("Interrupted guardian reveal");
            Check(gameplay.enabled&&gameplay.cullingMask==cameraMask&&gameplay.clearFlags==cameraClear,
                "Interrupted guardian reveal restores original gameplay camera rendering");
            Check(player.IsMovementLocked&&(player.MovementLockPosition-anchor).sqrMagnitude<.0001f,
                "Guardian cleanup preserves another owner's higher-priority player lock");
            player.ReleaseMovementLock(foreignOwner);UnityEngine.Object.Destroy(foreignOwner);foreignOwner=null;
            yield return .05f;Check(!player.IsMovementLocked,"Releasing the fixture's own lock leaves no leaked guardian lock");
            yield return Wait(()=>session.MatchTimePhase==ArchiveMatchTimePhase.Active,"Existing authoritative preparation/countdown still completes with the film disabled",12);
            cinematic.enabled=true;yield return .10f;
            Check(!cinematic.GuardianIntroVisible&&!cinematic.IsPlaying,"Re-enabling after Active never replays the old guardian prelude");
            Check(MaterialsRestored(),"All original explorer material arrays remain restored");
            Check(Mathf.Approximately(Time.timeScale,timeScale),"Guardian presentation never changes shared Time.timeScale");
        }
        static IEnumerator Species(DevelopmentMonster species,int seed)
        {
            Status=species+" guardian reveal";
            cinematic.enabled=false;Check(selector.CanSwap,species+": existing offline selector permits this fixture");
            selector.Select(species);yield return .08f;
            Check(selector.Selected==species,species+": existing selector applied the requested actual rig");SourceBaseline();
            session.Initialize(seed);cinematic.enabled=true;
            // A client whose preparation lasts longer than the old three-second
            // creation-time window must still see its selected guardian.
            session.State.matchTime.createdAt=ArchiveTopicSession.Clock-10;
            Check(session.MatchTimePhase==ArchiveMatchTimePhase.WaitingForPlayers&&session.State.matchTime.startedAt<0,
                species+": guardian reveal uses the existing unstarted preparation checkpoint");
            yield return Wait(()=>cinematic.ShotName=="Summoning"&&cinematic.HeroOverridesCount>0,
                "Players are summoned before the guardian appears",4);
            Check(!cinematic.GuardianIntroVisible&&cinematic.Progress<.6f&&MaterialsRestored()==false,
                species+": countdown opens with the existing player reveal and no guardian proxy");
            yield return Wait(()=>cinematic.GuardianIntroVisible,"The selected guardian proxy becomes visible after delayed preparation",7);
            yield return Wait(()=>ArchiveMatchCinematic.GuardianProgress(cinematic.Progress,true)>=.45f,"The selected guardian reaches the reveal midpoint",2);
            double countdown=session.State.matchTime.countdownStartedAt;
            double elapsed=ArchiveTopicSession.Clock-countdown;
            Check(elapsed>=ArchiveTopicSession.MatchCountdownSeconds-ArchiveMatchCinematic.GuardianShowcaseSeconds&&
                elapsed<ArchiveTopicSession.MatchCountdownSeconds&&session.MatchTimePhase==ArchiveMatchTimePhase.Countdown&&session.State.matchTime.startedAt<0,
                species+": delayed loading does not consume the guardian beat or shared gameplay time");
            Check(cinematic.HeroOverridesCount==0&&MaterialsRestored(),species+": team summon materials restore before the guardian showcase");
            Check(cinematic.GuardianName==ArchiveMonsterNames.ForPrimalAxiom(species==DevelopmentMonster.Rajang),
                species+": title matches the selected primary guardian");
            Check(Normalize(cinematic.GuardianAnimation)=="fullawaken",species+": the existing Full_Awaken clip drives the visual-only rig");
            Check(cinematic.GuardianRendererCount>0&&cinematic.GuardianBounds.size.sqrMagnitude>1,
                species+": existing visible model supplies meaningful cinematic bounds");
            Check(gameplay.enabled&&gameplay.cullingMask==0&&cinematic.FilmCamera!=null&&cinematic.FilmCamera.enabled,
                species+": existing full-screen film owns rendering while the real MainCamera stays enabled");
            Check(player.IsMovementLocked&&!session.CanAct(session.LocalActor),species+": player movement and academic actions wait for preparation to finish");
            SourceUnchanged(species+" midpoint");
            ScreenCapture.CaptureScreenshot(Folder+"/Guardian-"+species+".png");yield return .10f;
            yield return Wait(()=>session.MatchTimePhase==ArchiveMatchTimePhase.Active,"Guardian showcase hands off to gameplay after the team reveal",4);
            Check(countdown>=session.State.matchTime.createdAt+ArchiveTopicSession.MatchGuardianIntroSeconds-.02,
                species+": original preparation floor still applies");
            yield return .12f;
            Check(!cinematic.GuardianIntroVisible&&VisibilityRestored(),species+": gameplay restores the live sleeping guardian's renderer flags");
            Check(GuardianGraphsStopped(),species+": cached guardian graph stops after its showcase");
            SourceUnchanged(species+" after reveal");
            yield return Wait(()=>session.MatchTimePhase==ArchiveMatchTimePhase.Active,"Unchanged five-second team countdown reaches Active",8);
            yield return .15f;
            Check(Math.Abs(session.State.matchTime.startedAt-countdown-ArchiveTopicSession.MatchCountdownSeconds)<.00001,
                species+": original shared gameplay start remains countdownStartedAt + five seconds");
            Check(!cinematic.IsPlaying&&!cinematic.GuardianIntroVisible&&cinematic.LocksHeld==0&&cinematic.HeroOverridesCount==0,
                species+": entire intro clears proxy, hero overrides and owned movement locks before gameplay");
            Check(VisibilityRestored()&&MaterialsRestored(),species+": live guardian and explorer presentation resources are restored");
            Check(gameplay.enabled&&gameplay.cullingMask==cameraMask&&gameplay.clearFlags==cameraClear&&!player.IsMovementLocked,
                species+": normal gameplay camera and character control return");
            SourceUnchanged(species+" Active");
            cinematic.PreviewSummoning();
            yield return Wait(()=>cinematic.PreviewPlaying&&cinematic.HeroOverridesCount>0,"Offline preview begins with players",2);
            Check(!cinematic.GuardianIntroVisible,species+": Admin preview also shows players before the monster");
            cinematic.CancelPreview();yield return .10f;
            Check(!cinematic.IsPlaying&&!player.IsMovementLocked&&MaterialsRestored(),species+": cancelling the first player beat restores gameplay");
            cinematic.PreviewSummoning();
            yield return Wait(()=>cinematic.PreviewPlaying&&cinematic.GuardianIntroVisible,"Offline preview then includes the selected active guardian",4);
            Check(cinematic.GuardianName==ArchiveMonsterNames.ForPrimalAxiom(species==DevelopmentMonster.Rajang),
                species+": preview title matches the same selected guardian");
            cinematic.CancelPreview();yield return .10f;
            Check(!cinematic.IsPlaying&&!cinematic.GuardianIntroVisible&&!player.IsMovementLocked&&VisibilityRestored()&&MaterialsRestored(),
                species+": cancelling during the guardian beat restores gameplay, models and locks");
            SourceUnchanged(species+" preview cancellation");
        }
        static void Finish(bool success)
        {
            if(!active)return;active=false;
            EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayState;Application.logMessageReceived-=Log;
            try
            {
                if(cinematic!=null)cinematic.enabled=false;
                if(player!=null&&foreignOwner!=null)player.ReleaseMovementLock(foreignOwner);
                if(foreignOwner!=null)UnityEngine.Object.Destroy(foreignOwner);foreignOwner=null;
                if(Application.isPlaying&&selector!=null&&selector.CanSwap)selector.Select(originalSpecies);
                if(session!=null){session.suppressDefeatForVerification=oldDefeat;if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=oldThreats;}
                if(cinematic!=null)cinematic.enabled=originalFilmEnabled;
            }
            catch(Exception exception){report.errors.Add("Cleanup: "+exception);}
            report.passed=success&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";
            File.WriteAllText(Folder+"/ActiveMonster.json",JsonUtility.ToJson(report,true));
            Debug.Log("Active guardian intro checks "+Status+" — "+report.checks.Count+" checks. "+Folder+"/ActiveMonster.json");
            flow.Clear();
        }
    }
}
