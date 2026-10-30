using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.Tests.Monsters;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Offline development fixtures for night wake and reused second-guardian rigs.</summary>
    public static class ArchiveMonsterAuditChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scope="Fresh offline Play Mode fixtures. Advances night presentation directly; does not claim natural chase collision or physical-device coverage.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static void Check(Report report,bool value,string name)
        {report.checks.Add((value?"PASS ":"FAIL ")+name);if(!value)throw new InvalidOperationException(name);}
        public static Report Run()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom)throw new InvalidOperationException("Use a fresh offline MultiplayerGameplay Play session.");
            var report=new Report();var session=ArchiveTopicSession.Current;
            var h=session.hunting;var world=session.worldProgression;var selected=world.selector.Selected;
            var tick=typeof(ArchiveWorldProgression).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                session.Initialize(791311);h.Request(KhezuCommand.Sleep);
                foreach(int stage in new[]{1,2,3})
                {
                    var partial=h.GetSnapshot();partial.state=(int)KhezuHuntState.Waking;partial.mistakes=partial.stage=stage;partial.awake=partial.pending=false;
                    h.ApplySnapshot(partial,true);h.AwakenForNight();var wake=h.GetSnapshot();
                    Check(report,wake.mistakes==4&&wake.stage==4&&wake.pending&&h.State==KhezuHuntState.Waking,"Nightfall promotes partial mistake stage "+stage+" to Full_Awaken");
                }
                var resting=h.GetSnapshot();resting.state=(int)KhezuHuntState.NestSleeping;resting.sleepEndsAt=KhezuHuntingController.EncounterClock+30;
                h.ApplySnapshot(resting,true);double deadline=h.NestSleepEndsAt;h.AwakenForNight();
                Check(report,h.State==KhezuHuntState.NestSleeping&&h.NestSleepEndsAt==deadline,"Nightfall preserves the earned thirty-second nest sleep");
                var interrupted=h.GetSnapshot();interrupted.state=(int)KhezuHuntState.Waking;interrupted.mistakes=interrupted.stage=1;interrupted.awake=interrupted.pending=false;
                h.ApplySnapshot(interrupted,true);h.HoldTranquilized(true);var anchor=h.transform.position;h.AwakenForNight();
                Check(report,h.IsTranquilized&&h.transform.position==anchor&&h.GetSnapshot().stage==4&&h.GetSnapshot().pending,"Nightfall queues the full wake without cancelling tranquilizer sleep or its position");
                h.HoldTranquilized(false);h.Request(KhezuCommand.Sleep);

                tick.Invoke(world,null);
                foreach(var primary in new[]{DevelopmentMonster.Khezu,DevelopmentMonster.Rajang})
                {
                    if(world.secondary.gameObject.activeSelf)world.secondary.gameObject.SetActive(false);
                    world.selector.Select(primary);h.Request(KhezuCommand.Sleep);
                    var w=session.State.worldTime;w.phase=3;w.previousPhase=0;w.changedAt=ArchiveTopicSession.Clock;w.arrivalStartedAt=ArchiveTopicSession.Clock;w.secondaryReleased=false;
                    tick.Invoke(world,null);
                    var secondary=world.secondary;var animator=secondary.GetComponent<Animator>();
                    var clips=(Dictionary<string,AnimationClip>)typeof(KhezuHuntingController).GetField("clips",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(secondary);
                    Check(report,animator.runtimeAnimatorController.animationClips.All(c=>clips.TryGetValue(c.name,out var cached)&&cached==c),"Reused "+world.SecondaryName+" guardian caches its actual rig clips and attack timing");
                    var bones=(List<Transform>)typeof(KhezuHuntingController).GetField("upperReachBones",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(secondary);
                    Check(report,bones.Count>0&&bones.All(b=>b!=null&&b.gameObject.activeInHierarchy),"Reused "+world.SecondaryName+" guardian uses active head/chest/arm reach bones");
                    w.phase=0;w.arrivalStartedAt=-1;w.secondaryReleased=false;tick.Invoke(world,null);
                }
                report.passed=true;
            }
            catch(Exception e){report.errors.Add(e.ToString());}
            finally
            {
                h.HoldTranquilized(false);h.Request(KhezuCommand.Sleep);
                session.Initialize(791312);tick.Invoke(world,null);
                world.selector.Select(selected);h.Request(KhezuCommand.Sleep);
                Directory.CreateDirectory("Temp/ArchiveMonsterAudit");File.WriteAllText("Temp/ArchiveMonsterAudit/Native.json",JsonUtility.ToJson(report,true));
            }
            return report;
        }
    }
}
