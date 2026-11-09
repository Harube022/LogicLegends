#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.OpenArchive.Exploration;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Topics;
using LogicLegends.OpenArchiveTesting;
using LogicLegends.Tests.Monsters;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>Opt-in native movement regression; never attached to a saved scene or included in a build.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class ArchiveSprintStaminaPlayChecks : MonoBehaviour
    {
        [Serializable] sealed class Check { public string name,detail; public bool passed; }
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope="Offline Unity Play, existing local Player, original mobile/keyboard input and CharacterController. No replacement movement; no remote-client or physical Android verification.";
            public List<Check> checks=new List<Check>();
        }
        readonly Report report=new Report();
        Player player; CharacterController capsule; ArchiveSprintStamina stamina;
        Vector3 center; bool drive; int jumps; float peakSpeed;
        void Record(string name,bool passed,string detail="")
        { report.checks.Add(new Check{name=name,passed=passed,detail=detail});Save(); }
        void Save() { Directory.CreateDirectory("Temp/ArchiveStamina");File.WriteAllText("Temp/ArchiveStamina/checks.json",JsonUtility.ToJson(report,true)); }
        public static void Begin()
        {
            if(!Application.isPlaying||PhotonNetwork.InRoom||FindFirstObjectByType<ArchiveSprintStaminaPlayChecks>()!=null)throw new InvalidOperationException("Use fresh offline MultiplayerGameplay Play Mode.");
            new GameObject("Archive_Stamina_Play_Checks").AddComponent<ArchiveSprintStaminaPlayChecks>();
        }
        void Input(Vector3 direction)
        {
            var f=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized;
            var r=Vector3.ProjectOnPlane(Camera.main.transform.right,Vector3.up).normalized;
            GameInput.Instance.SetMobileMovement(new Vector2(Vector3.Dot(direction,r),Vector3.Dot(direction,f)));
        }
        void Update()
        {
            if(!drive)return;
            var radial=Vector3.ProjectOnPlane(player.transform.position-center,Vector3.up);
            float angle=Mathf.Atan2(radial.z,radial.x)+.24f;
            var goal=center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*12;
            Input(Vector3.ProjectOnPlane(goal-player.transform.position,Vector3.up).normalized);
            peakSpeed=Mathf.Max(peakSpeed,Vector3.ProjectOnPlane(capsule.velocity,Vector3.up).magnitude);
        }
        void Place(Vector3 point)
        {
            Input(Vector3.zero);capsule.enabled=false;player.transform.position=point;capsule.enabled=true;
            player.ResetMotionAfterTeleport();Physics.SyncTransforms();
        }
        Vector3 FindLoop(KhezuHuntingController hunt,ArchiveTopicSession session)
        {
            var filter=new NavMeshQueryFilter{agentTypeID=hunt.Agent.agentTypeID,areaMask=hunt.Agent.areaMask};
            var mesh=NavMesh.CalculateTriangulation();var candidates=new List<Vector3>(session.stations.Select(s=>s.transform.position));
            for(int i=0;i+2<mesh.indices.Length;i+=3)
            {
                var a=mesh.vertices[mesh.indices[i]];var b=mesh.vertices[mesh.indices[i+1]];var c=mesh.vertices[mesh.indices[i+2]];
                if(Vector3.Cross(b-a,c-a).magnitude>40)candidates.Add((a+b+c)/3);
            }
            foreach(var candidate in candidates)
            {
                if(!NavMesh.SamplePosition(candidate,out var found,2,filter))continue;bool clear=true;
                for(int i=0;i<24;i++)
                {
                    float a=i*Mathf.PI/12;var p=found.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*13;
                    if(!NavMesh.SamplePosition(p,out var hit,.25f,filter)||Mathf.Abs(hit.position.y-found.position.y)>.25f||NavMesh.Raycast(found.position,hit.position,out _,filter)){clear=false;break;}
                }
                if(clear)return found.position;
            }
            throw new InvalidOperationException("No unobstructed existing sprint test loop.");
        }
        void Jumped(){jumps++;}
        void Keys(params Key[] keys)
        {if(Keyboard.current!=null){InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys));InputSystem.Update();}}
        IEnumerator Start()
        {
            yield return null;
            var session=ArchiveTopicSession.Current;
            while(!session.Ready||ArchiveMatchCinematic.Covers(session)||!session.CanAct(session.LocalActor))yield return null;
            player=Player.LocalInstance;capsule=player.GetComponent<CharacterController>();stamina=FindFirstObjectByType<ArchiveSprintStamina>();
            var exploration=FindFirstObjectByType<OpenArchiveExplorationSession>();var hunters=FindObjectsByType<KhezuHuntingController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var enabledHunters=hunters.Select(h=>h.enabled).ToArray();bool oldExplore=exploration.enabled,oldThreat=session.worldProgression.suppressThreatsForVerification,oldDefeat=session.suppressDefeatForVerification;
            var oldPosition=player.transform.position;var oldRotation=player.transform.rotation;bool completed=false;
            var step=typeof(ArchiveSprintStamina).GetMethod("Step",BindingFlags.Instance|BindingFlags.NonPublic);
            player.OnJumpStarted+=Jumped;
            try
            {
                foreach(var hunt in hunters){hunt.Request(KhezuCommand.Sleep);hunt.enabled=false;}
                exploration.enabled=false;session.worldProgression.suppressThreatsForVerification=true;session.suppressDefeatForVerification=true;
                center=FindLoop(session.hunting,session);
                float bottom=(capsule.center.y-capsule.height*.5f)*player.transform.lossyScale.y;
                Place(center+Vector3.right*12+Vector3.up*(-bottom+capsule.skinWidth));
                yield return null;
                Record("Saved stamina binds exactly the existing local player",stamina!=null&&stamina.BoundPlayer==player&&FindObjectsByType<ArchiveSprintStamina>(FindObjectsSortMode.None).Length==1);
                Record("Configured six-second sprint and delayed eight-second regeneration",stamina.capacity==100&&stamina.fullSprintSeconds==6&&stamina.fullRecoverySeconds==8&&stamina.recoveryDelaySeconds==1.5f&&Mathf.Abs(stamina.resumeFraction-.35f)<.001f);
                drive=true;float began=Time.time;
                while(!player.IsRunning()&&Time.time-began<4)yield return null;
                float sprintAt=Time.time;bool jumped=false;
                while(!stamina.Exhausted&&Time.time-sprintAt<7)
                {
                    if(!jumped&&Time.time-sprintAt>.5f){GameInput.Instance.MobileJump();jumped=true;}
                    yield return null;
                }
                float duration=Time.time-sprintAt;
                Record("Existing mobile movement still auto-sprints after its walk warmup",sprintAt-began>2.8f&&sprintAt-began<3.3f,(sprintAt-began).ToString("F3")+"s warmup");
                Record("Continuous sprint exhausts after six seconds",stamina.Exhausted&&duration>5.8f&&duration<6.25f,duration.ToString("F3")+"s");
                Record("Running jump works and cannot reset stamina",jumps>0&&stamina.Current<.01f,"jumps="+jumps);
                Record("Exhaustion removes sprint without disabling walking or jump",!player.IsRunning()&&player.enabled&&capsule.enabled&&!player.IsMovementLocked&&player.IsRunningSuppressed);
                float recoveryAt=Time.time;var walkingAt=player.transform.position;
                yield return new WaitForSeconds(.8f);
                var moved=Vector3.ProjectOnPlane(player.transform.position-walkingAt,Vector3.up).magnitude;
                Record("Exhausted player keeps walking at normal speed",moved>player.moveSpeed*.65f&&moved<player.moveSpeed*.9f,"distance in 0.8s="+moved);
                Record("Regeneration delay does not refill immediately",stamina.Current<.01f);
                bool restartBelowThreshold=false;
                while(stamina.Exhausted&&Time.time-recoveryAt<5.5f){restartBelowThreshold|=player.IsRunning();yield return null;}
                Record("No tiny-refill sprint oscillation below 35 percent",!restartBelowThreshold&&!stamina.Exhausted&&stamina.Fraction>=.35f,"recovered after "+(Time.time-recoveryAt).ToString("F3")+"s");
                drive=false;Input(Vector3.zero);yield return null;
                float frozen=stamina.Current;player.AcquireMovementLock(this,"Stamina QA capture",player.transform.position);
                yield return new WaitForSeconds(.35f);
                Record("Capture lock freezes stamina and hides its HUD",Mathf.Abs(stamina.Current-frozen)<.001f&&!stamina.HudVisible&&!player.IsRunning());
                player.ReleaseMovementLock(this);yield return null;
                GameInput.Instance.AcquireGameplayActionBlock(this);frozen=stamina.Current;
                yield return new WaitForSeconds(.3f);
                Record("Blocked gameplay input cannot farm regeneration",Mathf.Abs(stamina.Current-frozen)<.001f);
                GameInput.Instance.ReleaseGameplayActionBlock(this);
                Time.timeScale=0;frozen=stamina.Current;yield return new WaitForSecondsRealtime(.25f);
                Record("Pause freezes stamina",Mathf.Abs(stamina.Current-frozen)<.001f);Time.timeScale=1;
                yield return new WaitForSeconds(8.2f);
                Record("Resting fully recovers without exceeding capacity",Mathf.Abs(stamina.Current-stamina.capacity)<.001f);
                Place(center+Vector3.up*(-bottom+capsule.skinWidth));Keys(Key.W);yield return new WaitForSeconds(.1f);player.OnRun();
                yield return new WaitForSeconds(.4f);Keys();
                Record("Keyboard sprint shares the same stamina budget",stamina.Current<98&&stamina.Current>85,"remaining="+stamina.Current);
                float beforeReset=stamina.Current;player.ResetMotionAfterTeleport();
                Record("Motion reset cannot refill stamina",Mathf.Abs(stamina.Current-beforeReset)<.001f);
                yield return null;
                Record("HUD is readable and does not capture touches",stamina.HudVisible&&stamina.HudCard!=null&&!stamina.HudCard.GetComponentInParent<CanvasGroup>().blocksRaycasts);
                ScreenCapture.CaptureScreenshot("Temp/ArchiveStamina/stamina-hud.png");
                yield return null; // Capture the visible HUD before later disable/enable cleanup.
                var main=session.hunting;var selector=session.worldProgression.selector;var originalMonster=selector.Selected;
                var encounter=main.GetComponent<KhezuCaptureEncounterController>();var film=main.GetComponent<KhezuCinematicJumpscareController>();
                bool oldEncounter=encounter.enabled,oldFilm=film.enabled;
                try
                {
                    encounter.enabled=film.enabled=false;
                    foreach(var kind in new[]{DevelopmentMonster.Rajang,DevelopmentMonster.Khezu})
                    {
                        main.Request(KhezuCommand.Sleep);encounter.SynchronizeBarrierMovement();selector.Select(kind);
                        step.Invoke(stamina,new object[]{1000f,false});Place(center+Vector3.right*12+Vector3.up*(-bottom+capsule.skinWidth));
                        drive=true;float warm=Time.time;
                        while(!player.IsRunning()&&Time.time-warm<4)yield return null;
                        main.Agent.enabled=true;main.Agent.Warp(center);main.transform.rotation=Quaternion.identity;
                        var state=main.GetSnapshot();state.state=(int)KhezuHuntState.Ready;state.awake=true;state.pending=false;state.mistakes=state.stage=4;
                        state.target=state.caught=0;state.animation="Idle";state.normalizedTime=0;
                        main.ApplySnapshot(state,true);main.SetPlayerEligibility(-1,true);main.Request(KhezuCommand.StartHunting);main.enabled=true;
                        float chaseAt=Time.time,minStamina=stamina.Fraction;
                        while(main.State!=KhezuHuntState.Caught&&Time.time-chaseAt<14){minStamina=Mathf.Min(minStamina,stamina.Fraction);yield return null;}
                        drive=false;Input(Vector3.zero);
                        Record(kind+": actual input-driven circular sprint is catchable with stamina",main.State==KhezuHuntState.Caught,"chase="+(Time.time-chaseAt).ToString("F3")+"s; min stamina="+minStamina.ToString("P0"));
                        main.Request(KhezuCommand.Sleep);encounter.SynchronizeBarrierMovement();main.enabled=false;
                    }
                }
                finally {main.Request(KhezuCommand.Sleep);encounter.SynchronizeBarrierMovement();selector.Select(originalMonster);encounter.enabled=oldEncounter;film.enabled=oldFilm;}
                // Boundary probes use the same private budget operation after native input/timing checks.
                step.Invoke(stamina,new object[]{1000f,false});
                step.Invoke(stamina,new object[]{60f,true});
                step.Invoke(stamina,new object[]{1.5f,false});
                Record("Exact recovery delay boundary adds no charge",stamina.Current==0&&stamina.Exhausted);
                step.Invoke(stamina,new object[]{2.79f,false});Record("34.875 percent is still exhausted",stamina.Exhausted);
                step.Invoke(stamina,new object[]{.02f,false});Record("Crossing 35 percent releases exhaustion",!stamina.Exhausted);
                step.Invoke(stamina,new object[]{1000f,false});Record("Large recovery step remains bounded",stamina.Current==stamina.capacity);
                player.SetRunningSuppressed(this,true);stamina.enabled=false;
                Record("Disabling stamina preserves another owner's run restriction",player.IsRunningSuppressed);
                player.SetRunningSuppressed(this,false);stamina.enabled=true;
                Record("All ten topic instructions use mobile interaction wording",Enumerable.Range(0,10).All(t=>!session.Question(t).instructions.Contains(" / E")&&!session.Question(t).instructions.Contains("Press E")));
                completed=true;
            }
            finally
            {
                drive=false;Keys();Input(Vector3.zero);Time.timeScale=1;player.ReleaseMovementLock(this);GameInput.Instance.ReleaseGameplayActionBlock(this);player.SetRunningSuppressed(this,false);
                step.Invoke(stamina,new object[]{1000f,false});stamina.enabled=true;player.OnJumpStarted-=Jumped;
                Place(oldPosition);player.transform.rotation=oldRotation;exploration.enabled=oldExplore;
                for(int i=0;i<hunters.Length;i++)hunters[i].enabled=enabledHunters[i];
                session.worldProgression.suppressThreatsForVerification=oldThreat;session.suppressDefeatForVerification=oldDefeat;
                report.passed=completed&&report.checks.All(c=>c.passed);Save();Debug.Log("Archive stamina checks: "+report.checks.Count(c=>c.passed)+"/"+report.checks.Count);Destroy(gameObject);
            }
        }
    }
}
#endif
