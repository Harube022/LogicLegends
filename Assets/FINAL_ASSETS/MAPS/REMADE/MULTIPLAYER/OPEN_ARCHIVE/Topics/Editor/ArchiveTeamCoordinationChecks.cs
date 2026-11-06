using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveTeamCoordinationChecks
    {
        [Serializable] sealed class Report {public bool passed;public List<string> checks=new List<string>(),errors=new List<string>();public string scope="Saved MultiplayerGameplay offline Play; real command validator, checkpoint serialization, roster, contextual objectives and receipt bounds. Clock/health/downed states use explicit Admin fixtures. No physical Android claim.";}
        public static string Status{get;private set;}="Idle";
        static void Check(Report r,bool pass,string label){r.checks.Add((pass?"PASS ":"FAIL ")+label);if(!pass)throw new InvalidOperationException(label);}
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Team Coordination (Offline Play)")]
        public static void Run()
        {
            var s=ArchiveTopicSession.Current;
            if(!Application.isPlaying||PhotonNetwork.InRoom||s==null||!s.Ready)throw new InvalidOperationException("Use saved MultiplayerGameplay offline Play.");
            var r=new Report();bool oldSuppression=s.suppressDefeatForVerification;
            bool oldThreat=s.worldProgression.suppressThreatsForVerification;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            ArchiveCommand Command(ArchiveTeamPingKind kind,int actor=-1,int target=0)
            {
                var record=s.State.actors.FirstOrDefault(a=>a.actor==actor);
                var cmd=new ArchiveCommand{topic=0,action=ArchiveAction.TeamPing,a=(int)kind,b=target,sequence=(record?.lastSequence??0)+1};
                typeof(ArchiveTopicSession).GetMethod("StampCommandContext",flags).Invoke(s,new object[]{cmd});return cmd;
            }
            void Cooldown(int actor=-1){var ping=s.State.teamPings.FirstOrDefault(p=>p.actor==actor);if(ping!=null)ping.createdAt=ArchiveTopicSession.Clock-ArchiveTopicSession.TeamPingCooldownSeconds-.1;}
            try
            {
                Status="Running";s.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);s.RequestAdminTest(ArchiveAdminTestAction.StartMatchClock);
                s.suppressDefeatForVerification=true;s.worldProgression.suppressThreatsForVerification=true;
                Check(r,s.RequestAdminTest(ArchiveAdminTestAction.SpawnTeammate),"Existing offline practice teammate is available for real roster/rescue checks");
                double deadline=s.State.matchTime.deadline;int mistakes=s.State.mistakes.Count,health=s.hunting.health.ActorHearts(-1);
                var roster=s.GetTeamRoster();Check(r,roster.Length==2&&roster.Count(p=>p.local)==1&&roster.All(p=>p.healthKnown&&p.hearts==p.maxHearts),"Roster uses existing health with one local actor and one teammate");
                Check(r,!s.CanTeamPing(ArchiveTeamPingKind.NeedMedkit,out _)&&!s.CanTeamPing(ArchiveTeamPingKind.ReviveTeammate,out _)&&!s.CanTeamPing(ArchiveTeamPingKind.GoToPortal,out _),"Impossible medkit, revival and unopened-portal requests are unavailable");
                var cmd=Command(ArchiveTeamPingKind.HelpHere);
                Check(r,s.Accept(-1,cmd)&&s.ActiveTeamPings.Length==1,"Actual host validator accepts a help signal at the authenticated actor position");
                var accepted=s.State.teamPings.Single();var capsule=Player.LocalInstance.GetComponent<CharacterController>();
                Check(r,accepted.actor==-1&&Mathf.Abs(accepted.position.y-capsule.bounds.min.y)<.01f&&Vector2.Distance(new Vector2(accepted.position.x,accepted.position.z),new Vector2(capsule.bounds.center.x,capsule.bounds.center.z))<.01f,"Help anchor is calculated from the real player capsule");
                var forged=new ArchiveTeamPing{actor=accepted.actor,sequence=accepted.sequence,kind=ArchiveTeamPingKind.HelpHere,position=accepted.position+Vector3.one*10000};
                Check(r,s.TryGetTeamPingPosition(forged,out var canonical)&&(canonical-accepted.position).sqrMagnitude<.001f,"A cloned receipt cannot replace the host-authenticated marker coordinates");
                forged.kind=ArchiveTeamPingKind.GoToPortal;forged.targetActor=-999;
                Check(r,s.TryGetTeamPingPosition(forged,out canonical)&&(canonical-accepted.position).sqrMagnitude<.001f,"A cloned receipt cannot change the host-authenticated ping kind or target");
                int revision=s.State.revision;Check(r,!s.Accept(-1,cmd)&&s.State.revision==revision,"Replaying an accepted ping cannot publish another receipt");
                Check(r,!s.RequestTeamPing(ArchiveTeamPingKind.HelpHere)&&s.TeamPingCooldownRemaining(-1)>3,"A second immediate signal is rejected by the shared four-second cooldown");
                Check(r,s.State.matchTime.deadline==deadline&&s.State.mistakes.Count==mistakes&&s.State.solved.Count==0&&s.hunting.health.ActorHearts(-1)==health,"Signals never edit academic progress, mistakes, hearts or the shared deadline");
                Cooldown();cmd=Command(ArchiveTeamPingKind.HelpHere);cmd.matchId="stale-match";Check(r,!s.Accept(-1,cmd),"Old match commands cannot place team signals");
                cmd=Command(ArchiveTeamPingKind.HelpHere);cmd.a=999;Check(r,!s.Accept(-1,cmd),"Undefined signal values are rejected without state mutation");
                Check(r,!s.Accept(-999,Command(ArchiveTeamPingKind.HelpHere,-999)),"An unregistered actor cannot spoof a team signal");
                accepted.createdAt=ArchiveTopicSession.Clock-13;Check(r,s.ActiveTeamPings.Length==0&&!s.TryGetTeamPingPosition(accepted,out _),"Expired signals disappear and cannot retain an objective marker");
                s.RequestAdminTest(ArchiveAdminTestAction.SetHearts,-1,2);
                Check(r,s.RequestTeamPing(ArchiveTeamPingKind.NeedMedkit)&&s.State.teamPings.Count==1&&s.State.teamPings[0].targetActor==-1,"An injured actor can request a medkit with one bounded receipt");
                s.RequestAdminTest(ArchiveAdminTestAction.SetHearts,-1,3);
                Check(r,!s.TryGetTeamPingPosition(s.State.teamPings[0],out _),"A resolved medkit request no longer redirects teammates");
                Cooldown();s.RequestAdminTest(ArchiveAdminTestAction.DownAtNest,-2);
                Check(r,s.GetTeamRoster().Single(p=>p.actor==-2).status=="Downed at nest","Downed-at-nest status is derived from the existing rescue state");
                cmd=Command(ArchiveTeamPingKind.ReviveTeammate,-1,-999);Check(r,!s.Accept(-1,cmd),"A nonexistent revival target cannot be invented by the client");
                Check(r,s.RequestTeamPing(ArchiveTeamPingKind.ReviveTeammate)&&s.TryGetTeamPingPosition(s.State.teamPings[0],out var anchor)&&(anchor-s.Downed(-2).anchor).sqrMagnitude<.001f,"Revival ping follows the actual downed teammate anchor");
                s.RequestAdminTest(ArchiveAdminTestAction.ReviveAtNest,-2);
                Check(r,!s.TryGetTeamPingPosition(s.State.teamPings[0],out _),"Successful revival immediately invalidates its rescue marker");
                Cooldown();s.RequestAdminTest(ArchiveAdminTestAction.DownAtNest,-2);
                s.RequestAdminTest(ArchiveAdminTestAction.GrantItem,-1,(int)ArchiveSurvivalItem.RevivalKit);s.State.teamPings.Clear();
                Check(r,s.TryGetTeamObjective(out var rescueObjective)&&rescueObjective.kind==ArchiveTeamObjectiveKind.Rescue&&rescueObjective.actor==-2,"A held Revival Kit guides the player to an eligible downed teammate");
                Check(r,s.RequestTeamPing(ArchiveTeamPingKind.ReviveTeammate),"A second valid downed fixture creates a rescue receipt for health-first restoration");
                s.RequestAdminTest(ArchiveAdminTestAction.SetHearts,-2,1);
                Check(r,s.Downed(-2)?.phase==2&&!s.TryGetTeamPingPosition(s.State.teamPings[0],out _),"Health restoration invalidates a rescue marker before its downed-state cleanup arrives");
                Cooldown();
                Check(r,!s.CanTeamPing(ArchiveTeamPingKind.ReviveTeammate,out _),"A pending downed record cannot make an already-living teammate eligible for a new revival signal");
                s.State.teamPings.Clear();
                Check(r,s.TryGetTeamObjective(out rescueObjective)&&rescueObjective.kind!=ArchiveTeamObjectiveKind.Rescue,"Default rescue guidance also ignores health-restored teammates awaiting downed cleanup");
                s.RequestAdminTest(ArchiveAdminTestAction.ReviveAtNest,-2);
                Cooldown();s.State.teamPings.Clear();s.State.stations[0].phase=1;
                Check(r,s.TryGetTeamObjective(out var objective)&&objective.kind==ArchiveTeamObjectiveKind.Topic&&objective.topic==0,"The reserved active topic supplies the default team objective");
                Cooldown();s.RequestTeamPing(ArchiveTeamPingKind.HelpHere);string json=JsonUtility.ToJson(s.State);
                var copy=JsonUtility.FromJson<ArchiveTeamState>(json);
                Check(r,copy.teamPings.Count==1&&copy.teamPings[0].actor==-1&&Math.Abs(copy.teamPings[0].createdAt-s.State.teamPings[0].createdAt)<.002,"The existing checkpoint retains ping ownership, cooldown and original timestamp");
                s.RequestAdminTest(ArchiveAdminTestAction.OpenPortal);s.State.teamPings.Clear();
                Check(r,s.TryGetTeamObjective(out objective)&&objective.kind==ArchiveTeamObjectiveKind.Portal,"The opened final portal replaces the normal topic objective");
                Check(r,s.RequestTeamPing(ArchiveTeamPingKind.GoToPortal),"Portal guidance becomes available only after completion");
                s.RequestAdminTest(ArchiveAdminTestAction.PreviewBarrier);
                Check(r,s.GetTeamRoster().Single(p=>p.actor==-1).status=="Captured"&&!s.TryGetTeamObjective(out _),"Captured status is shown and movement objectives hide for a captive");
                s.State.survival.defeated=true;s.State.survival.defeatReason=ArchiveTopicSession.MatchTimeoutReason;
                Check(r,!s.RequestTeamPing(ArchiveTeamPingKind.HelpHere)&&!s.TryGetTeamObjective(out _),"Terminal outcomes close new signals and objective guidance");
                r.passed=true;Status="Passed";
            }
            catch(Exception e){r.errors.Add(e.ToString());Status="Failed";}
            finally
            {
                s.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);s.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);
                s.suppressDefeatForVerification=oldSuppression;s.worldProgression.suppressThreatsForVerification=oldThreat;
                Directory.CreateDirectory("Temp/ArchiveTeamCoordination");File.WriteAllText("Temp/ArchiveTeamCoordination/Native.json",JsonUtility.ToJson(r,true));
            }
            Debug.Log("Team coordination native checks: "+Status+" ("+r.checks.Count+").");
        }
    }
}
