using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.Tests.Khezu;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Opt-in deterministic reward evidence checks; no account or wallet is credited.</summary>
    public static class ArchiveMatchRewardChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scope="Deterministic multiplayer reward policy/build fixtures plus optional fresh saved-scene Play checks for startup eligibility and explicit legacy admin exclusion. No backend payout, remote rendering or physical Android claim.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        public static string Status{get;private set;}="Idle";
        static Report report;
        static void Check(bool pass,string label)
        {
            report.checks.Add((pass?"PASS ":"FAIL ")+label);
            if(!pass)throw new InvalidOperationException(label);
        }
        static ArchiveMatchRewardReport Perfect()=>new ArchiveMatchRewardReport
        {
            version=1,actor=1,matchId="0123456789abcdef0123456789abcdef",outcome="victory",online=true,
            participants=new[]{1,2},completedTopics=Enumerable.Range(0,10).ToArray(),restoredTopics=Enumerable.Range(0,10).ToArray(),
            participationKeys=new[]{"work:0:0:0","work:0:1:0","work:0:2:0"}
        };
        static ArchiveMatchRewardBreakdown Coins(ArchiveMatchRewardReport value)=>ArchiveMatchRewardPolicy.Calculate(value);
        static void Excludes(Action<ArchiveMatchRewardReport> change,string label)
        {
            var value=Perfect();change(value);var result=Coins(value);
            Check(result.coins==0&&!string.IsNullOrEmpty(result.exclusionReason),label);
        }
        static ArchiveTeamState Team()
        {
            var team=new ArchiveTeamState
            {
                matchId="0123456789abcdef0123456789abcdef",
                stations=Enumerable.Range(0,10).Select(t=>new ArchiveStationState{topic=t,phase=2,round=ArchiveTopicCatalog.RoundCount(t)}).ToArray(),
                rewards=new ArchiveMatchRewardState{online=true,participants=new List<int>{1,2}},
                crystals=Enumerable.Range(0,10).Select(t=>new ArchiveCrystalState{topic=t,started=true,online=true,closed=true,fullyRestored=true}).ToList(),
                exit=new ArchiveExitState{opened=true,won=true,escaped=new List<ArchiveEscape>{new ArchiveEscape{actor=2}}}
            };
            for(int topic=0;topic<10;topic++)for(int round=0;round<ArchiveTopicCatalog.RoundCount(topic);round++)
                for(int sub=0;sub<(topic==2?2:1);sub++)
                    team.solved.Add(new ArchiveSolvedMilestone{topic=topic,round=round,subphase=sub,resetSerial=0,solver=2,
                        contributors=new[]{1,2},crystalContributors=new[]{1,2}});
            return team;
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Validate Multiplayer Rewards")]
        public static Report Validate()
        {
            report=new Report();Status="Running";
            try{Policy();Evidence();report.passed=true;Status="Passed";}
            catch(Exception e){report.errors.Add(e.ToString());Status="Failed";}
            Save();return report;
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Multiplayer Rewards (Fresh Offline Play)")]
        public static Report Run()
        {
            var session=ArchiveTopicSession.Current;
            if(!Application.isPlaying||PhotonNetwork.InRoom||session==null||!session.Ready)
                throw new InvalidOperationException("Use fresh saved MultiplayerGameplay offline Play before any Admin action.");
            report=new Report();Status="Running";
            try{Policy();Evidence();FreshScene(session);GiftProvenance(session);FrozenEvidence(session);report.passed=true;Status="Passed";}
            catch(Exception e){report.errors.Add(e.ToString());Status="Failed";}
            Save();return report;
        }

        static void Policy()
        {
            var value=Perfect();var result=Coins(value);
            Check(result.coins==1000&&result.baseCoins==600&&result.crystalCoins==250&&result.participationCoins==150&&result.consolationCoins==0,
                "A genuine fully cooperative victory has the exact 600 + 250 + 150 coin breakdown");
            Check(ArchiveMatchRewardPolicy.MaximumCoins==1000&&ArchiveMatchRewardPolicy.FullParticipationCredits==3,
                "The reward ceiling and full-participation requirement remain explicit");
            value.participationKeys=Array.Empty<string>();result=Coins(value);
            Check(result.coins==0&&result.tier==0,"An AFK member cannot collect a team victory without confirmed contributions");
            value=Perfect();value.participationKeys=new[]{"work:0:0:0"};result=Coins(value);
            Check(result.tier==1&&result.coins==333,"One meaningful contribution earns the rounded one-third reward, not the full team pot");
            value.participationKeys=new[]{"work:0:0:0","work:0:1:0"};result=Coins(value);
            Check(result.tier==2&&result.coins==666,"Two meaningful contributions earn the rounded two-thirds reward");
            value.participationKeys=new[]{"work:0:0:0","work:0:1:0","work:0:2:0","work:0:3:0"};result=Coins(value);
            Check(result.tier==3&&result.coins==1000,"Extra accepted work cannot exceed the full reward ceiling");
            value=Perfect();value.participationKeys=new[]{"work:0:0:0","work:0:0:0","work:0:0:0"};
            Check(Coins(value).tier==1&&Coins(value).coins==333,"Replaying the same work receipt cannot raise participation tier");
            value=Perfect();value.restoredTopics=Array.Empty<int>();result=Coins(value);
            Check(result.coins==750&&result.crystalCoins==0,"Finishing topics without restoring crystals cannot earn the crystal bonus");
            value.restoredTopics=new[]{0,1,2,3};result=Coins(value);
            Check(result.coins==850&&result.crystalCoins==100,"Each genuinely restored crystal adds exactly twenty-five coins at full participation");
            value=Perfect();value.participationKeys=new[]{"work:0:0:0"};value.restoredTopics=new[]{0};
            Check(Coins(value).coins==258,"Partial crystal shares use integer flooring without rounding above the entitlement");
            value=Perfect();value.outcome="defeat";result=Coins(value);
            Check(result.coins==150&&result.consolationCoins==150&&result.baseCoins==0&&result.crystalCoins==0&&result.participationCoins==0,
                "Even a ten-topic escape failure earns only the capped defeat consolation");
            value.completedTopics=new[]{0,1,2};value.restoredTopics=Array.Empty<int>();
            Check(Coins(value).coins==45,"Defeat consolation follows completed topic progress");
            value.participationKeys=new[]{"work:0:0:0"};
            Check(Coins(value).coins==15,"A low-contribution defeat receives only its proportional consolation");
            value.completedTopics=Array.Empty<int>();
            Check(Coins(value).coins==0,"Repeated failure without a completed topic cannot farm consolation coins");
            value=Perfect();value.participationKeys=new[]{"support:medkit:2","support:battery:2","support:revive:2"};
            Check(Coins(value).coins==1000,"Validated healing, power support and rescue can earn participation without requiring last-solver credit");
            value.participationKeys=new[]{"support:give:2","support:give:2","support:give:2"};
            Check(Coins(value).tier==1,"Passing items repeatedly to the same teammate cannot create unlimited gift credit");

            Excludes(r=>r.adminUsed=true,"Admin-assisted matches award no permanent currency");
            Excludes(r=>r.online=false,"Offline previews award no permanent currency");
            Excludes(r=>r.version=0,"Unknown reward evidence versions fail closed");
            Excludes(r=>r.matchId="other-match","Malformed match identifiers cannot create a payout");
            Excludes(r=>r.actor=0,"An anonymous or zero actor cannot receive a multiplayer reward");
            Excludes(r=>r.actor=3,"An actor absent from the match roster cannot collect its reward");
            Excludes(r=>r.participants=new[]{1},"A single-player room cannot masquerade as cooperative multiplayer");
            Excludes(r=>r.participants=new[]{1,1},"Duplicated actor identities cannot create a multiplayer roster");
            Excludes(r=>r.participants=new[]{1,2,3,4,5},"A roster larger than the existing four-player room is rejected");
            Excludes(r=>r.participants=null,"Missing participant evidence fails closed");
            Excludes(r=>r.completedTopics=new[]{0,0,1,2,3,4,5,6,7,8},"Duplicated topic IDs cannot manufacture ten-topic victory");
            Excludes(r=>r.completedTopics=Enumerable.Range(0,9).ToArray(),"Incomplete academic progress cannot claim team victory");
            Excludes(r=>r.completedTopics=null,"Missing completed-topic evidence fails closed");
            Excludes(r=>r.restoredTopics=new[]{0,0},"Duplicated crystal receipts cannot increase the restoration reward");
            Excludes(r=>r.restoredTopics=new[]{10},"Unknown crystal topic IDs cannot grant a bonus");
            Excludes(r=>{r.outcome="defeat";r.completedTopics=new[]{0};r.restoredTopics=new[]{1};},"A crystal cannot qualify before its topic is genuinely complete");
            Excludes(r=>r.restoredTopics=null,"Missing restoration evidence fails closed");
            Excludes(r=>r.outcome="active","An unfinished match cannot claim its projected reward");
            Excludes(r=>r.participationKeys=null,"Missing contribution evidence fails closed");
            Excludes(r=>r.participationKeys=new[]{"work:0:5:0"},"Out-of-range ordinary topic rounds cannot mint work credit");
            Excludes(r=>r.participationKeys=new[]{"work:1:9:0"},"Truth Table work remains bounded to its original nine rounds");
            Excludes(r=>r.participationKeys=new[]{"work:6:10:0"},"Sets work remains bounded to its original ten problems");
            Excludes(r=>r.participationKeys=new[]{"work:0:0:1"},"Only Rules of Inference can report a second subphase");
            Excludes(r=>r.participationKeys=new[]{"work:2:0:2"},"Rules of Inference cannot invent a third subphase");
            Excludes(r=>r.participationKeys=new[]{"work:00:0:0"},"Noncanonical strings cannot duplicate equivalent work keys");
            Excludes(r=>r.participationKeys=new[]{"support:medkit:1"},"Self-healing is not teammate support credit");
            Excludes(r=>r.participationKeys=new[]{"support:medkit:3"},"Support cannot target an actor outside this match");
            Excludes(r=>r.participationKeys=new[]{"support:unknown:2"},"Unknown support categories cannot mint participation");
            Excludes(r=>r.participationKeys=Enumerable.Repeat("work:0:0:0",129).ToArray(),"Oversized contribution packets fail closed");
            Check(ArchiveMatchRewardPolicy.ValidKey("work:1:8:0",1,new[]{1,2})&&
                ArchiveMatchRewardPolicy.ValidKey("work:6:9:0",1,new[]{1,2})&&
                ArchiveMatchRewardPolicy.ValidKey("work:2:4:1",1,new[]{1,2}),
                "All final Truth Table, Sets and Inference steps remain eligible under their real round counts");
        }

        static void Evidence()
        {
            var team=Team();var built=ArchiveMatchRewardPolicy.Build(team,1);
            Check(built.version==2&&built.completedTopics.Length==10&&built.correctAnswers.Length==64&&Coins(built).coins==1000&&Coins(built).leaderboardPoints==500,
                "A connected non-escapee earns 1000 coins and the lowest score earns 500 points");
            var escaped=Coins(ArchiveMatchRewardPolicy.Build(team,2));
            Check(escaped.coins==2000&&escaped.leaderboardPoints==1000&&escaped.correctAnswers==64,
                "The escaping MVP earns 2000 coins and 1000 points");
            team.survival.downed.Add(new ArchiveDownedActor{actor=1,phase=2});
            Check(Coins(ArchiveMatchRewardPolicy.Build(team,1)).coins==1000,
                "Becoming downed does not erase work already earned for the team");
            team.survival.defeated=true;
            Check(ArchiveMatchRewardPolicy.Build(team,1).outcome=="defeat"&&Coins(ArchiveMatchRewardPolicy.Build(team,2)).coins==2000,
                "A team defeat preserves an individual escape");
            team.rewards.disconnectedActors.Add(2);
            var reconnect=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(team));
            Check(Coins(ArchiveMatchRewardPolicy.Build(reconnect,2)).coins==0&&Coins(ArchiveMatchRewardPolicy.Build(reconnect,2)).leaderboardPoints==0&&
                Coins(ArchiveMatchRewardPolicy.Build(reconnect,1)).leaderboardPoints==1000,
                "Disconnect is sticky across checkpoints and the departed solver is excluded from ranking");
            team=Team();team.solved.Add(team.solved[0]);var count=ArchiveMatchRewardPolicy.Build(team,1).correctAnswers.Length;
            Check(count==Team().solved.Count,"Duplicate milestone receipts do not multiply correct-answer credit");
            foreach(var solved in team.solved)solved.crystalContributors=new[]{2};
            team.actors.Add(new ArchiveActor{actor=1,contributions=int.MaxValue,correctAnswers=int.MaxValue,qualityMilestones=int.MaxValue});
            built=ArchiveMatchRewardPolicy.Build(team,1);
            Check(ArchiveMatchRewardPolicy.ParticipationKeys(team,1).Length==0&&Coins(built).correctAnswers==0&&Coins(built).leaderboardPoints==500,
                "Only the recorded solver earns answer credit even when the HUD counters or crystal contributors differ");
            team.rewards.support.Add(new ArchiveRewardSupportCredit{actor=1,target=2,kind="revive"});
            team.rewards.support.Add(new ArchiveRewardSupportCredit{actor=1,target=2,kind="revive"});
            Check(ArchiveMatchRewardPolicy.ParticipationKeys(team,1).SequenceEqual(new[]{"support:revive:2"})&&
                Coins(ArchiveMatchRewardPolicy.Build(team,1)).correctAnswers==0,
                "A support action remains a team contribution without adding a correct answer");
            for(int i=0;i<32;i++)team.solved[i].solver=1;
            Check(team.rewards.participants.All(actor=>Coins(ArchiveMatchRewardPolicy.Build(team,actor)).leaderboardPoints==1000),
                "Equal correct-answer counts award both teammates 1000 points");
            team.rewards.participants.Add(3);
            for(int i=0;i<team.solved.Count;i++)team.solved[i].solver=i%3+1;
            Check(team.rewards.participants.All(actor=>Coins(ArchiveMatchRewardPolicy.Build(team,actor)).leaderboardPoints==1000),
                "A one-answer spread also awards all eligible teammates 1000 points");
            team.exit=new ArchiveExitState();team.survival.defeated=true;
            foreach(var station in team.stations)station.phase=0;
            team.solved.RemoveRange(6,team.solved.Count-6);
            int[] solvers={1,3,3,2,2,2};
            for(int i=0;i<solvers.Length;i++)team.solved[i].solver=solvers[i];
            Check(Coins(ArchiveMatchRewardPolicy.Build(team,3)).leaderboardPoints==750,
                "A teammate halfway between the top and bottom answer counts earns 750 points");
            team=Team();team.stations[0].testResetSerial=1;built=ArchiveMatchRewardPolicy.Build(team,1);
            Check(!built.completedTopics.Contains(0)&&!built.restoredTopics.Contains(0)&&built.correctAnswers.Length==59,
                "Old reset-era receipts cannot restore a new topic or preserve its old reward work");
            team=Team();team.crystals[0].adminBypass=true;
            Check(Coins(ArchiveMatchRewardPolicy.Build(team,1)).coins==0,
                "An admin crystal skip excludes the whole match rather than only one crystal bonus");
            team=Team();team.rewards.adminUsed=true;var roundTrip=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(team));
            Check(roundTrip.rewards.adminUsed&&Coins(ArchiveMatchRewardPolicy.Build(roundTrip,1)).coins==0,
                "The existing checkpoint preserves whole-match admin exclusion across authority restoration");
            team=Team();team.rewards.online=false;
            Check(Coins(ArchiveMatchRewardPolicy.Build(team,1)).coins==0,
                "A synthetic offline teammate roster cannot enable permanent rewards");
        }

        static void FreshScene(ArchiveTopicSession session)
        {
            Check(session.State.rewards!=null&&!session.State.rewards.adminUsed,
                "Fresh saved MultiplayerGameplay startup remains eligible until an explicit Admin action");
            _ = KhezuLegacyTestPermission.CanUse;
            Check(!session.State.rewards.adminUsed,"Reading development UI permission never excludes a legitimate match");
            var original=session.State;
            var setter=typeof(ArchiveTopicSession).GetProperty("State").GetSetMethod(true);
            try
            {
                setter.Invoke(session,new object[]{JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(original))});
                KhezuLegacyTestPermission.MarkActionUsed();
                Check(session.State.rewards.adminUsed,"An explicit legacy test action marks the whole match Admin-assisted");
                int revision=session.State.revision;KhezuLegacyTestPermission.MarkActionUsed();
                Check(session.State.rewards.adminUsed&&session.State.revision==revision,
                    "Repeated legacy test actions retain one sticky exclusion without repeatedly committing it");
                Check(JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State)).rewards.adminUsed,
                    "Explicit legacy test exclusion survives checkpoint serialization");
            }
            finally{setter.Invoke(session,new object[]{original});}
        }
        static void GiftProvenance(ArchiveTopicSession session)
        {
            var original=session.State;
            var setter=typeof(ArchiveTopicSession).GetProperty("State").GetSetMethod(true);
            void Call(string method,params object[] args)
            {
                var member=typeof(ArchiveTopicSession).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic);
                if(member==null)throw new MissingMethodException(typeof(ArchiveTopicSession).Name,method);
                try{member.Invoke(session,args);}catch(TargetInvocationException e){throw e.InnerException??e;}
            }
            try
            {
                var fixture=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(original));
                fixture.exit=new ArchiveExitState();fixture.survival=new ArchiveSurvivalState();
                fixture.rewards=new ArchiveMatchRewardState{online=true,participants=new List<int>{1,2,3}};
                setter.Invoke(session,new object[]{fixture});
                int medkit=(int)ArchiveSurvivalItem.Medkit,battery=(int)ArchiveSurvivalItem.Battery;
                Call("TrackRewardGift",1,2,medkit);
                Check(session.State.rewards.gifts.Count==1&&session.State.rewards.support.Count==0,
                    "Giving an unused supply records provenance but grants no immediate support credit");
                Call("TrackRewardGift",2,1,medkit);Call("UseRewardGift",1,medkit);
                Check(session.State.rewards.gifts.Count==0&&session.State.rewards.support.Count==0,
                    "Returning an unused gift and consuming it yourself cannot farm teammate support");
                Call("UseRewardGift",1,battery);Call("RecordRewardSupport",1,1,"battery");
                Check(session.State.rewards.support.Count==0,
                    "Ordinary own-item use and self-targeted support cannot create a reward receipt");
                Call("TrackRewardGift",1,2,medkit);Call("TrackRewardGift",2,3,medkit);
                Check(session.State.rewards.gifts.Count==1&&session.State.rewards.gifts[0].donor==1&&
                    session.State.rewards.gifts[0].recipient==3&&session.State.rewards.support.Count==0,
                    "Forwarding a gift preserves its original donor until a teammate actually uses it");
                Call("UseRewardGift",3,battery);
                Check(session.State.rewards.gifts.Count==1&&session.State.rewards.support.Count==0,
                    "Using another item cannot consume a pending gift or claim its support credit");
                var restored=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));
                Check(restored.rewards.gifts.Count==1&&restored.rewards.gifts[0].donor==1&&
                    restored.rewards.gifts[0].recipient==3&&restored.rewards.gifts[0].item==medkit,
                    "Gift provenance survives the existing checkpoint serialization");
                setter.Invoke(session,new object[]{restored});Call("UseRewardGift",3,medkit);
                var credits=session.State.rewards.support;
                Check(session.State.rewards.gifts.Count==0&&credits.Count==1&&credits[0].actor==1&&
                    credits[0].target==3&&credits[0].kind=="give"&&!credits.Any(c=>c.actor==2),
                    "Only the original donor earns gift credit when the final recipient uses a forwarded supply");
                Call("UseRewardGift",3,medkit);Call("RecordRewardSupport",1,3,"give");
                Check(credits.Count==1,"Reusing a consumed gift or replaying its receipt cannot award it twice");
                Call("RecordRewardSupport",2,3,"medkit");Call("RecordRewardSupport",2,3,"medkit");
                Check(credits.Count==2&&credits.Count(c=>c.actor==2&&c.target==3&&c.kind=="medkit")==1,
                    "Repeated effective support remains bounded to one actor-target-kind receipt per match");
            }
            finally{setter.Invoke(session,new object[]{original});}
        }
        static void FrozenEvidence(ArchiveTopicSession session)
        {
            var original=session.State;
            var setter=typeof(ArchiveTopicSession).GetProperty("State").GetSetMethod(true);
            var finalize=typeof(ArchiveTopicSession).GetMethod("FinalizeMatchRewards",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                var fixture=Team();setter.Invoke(session,new object[]{fixture});
                finalize.Invoke(session,null);
                var frozen=session.RewardReport(1);string evidence=JsonUtility.ToJson(frozen);
                Check(fixture.rewards.finalReports.Count==2&&Coins(frozen).coins==1000,
                    "Terminal finalization freezes one report for every recorded participant");
                fixture.solved.Clear();fixture.crystals.Clear();fixture.rewards.support.Clear();
                var disconnect=typeof(ArchiveTopicSession).GetMethod("RecordRewardDisconnect",BindingFlags.Instance|BindingFlags.NonPublic);
                disconnect.Invoke(session,new object[]{1});
                finalize.Invoke(session,null);
                Check(fixture.rewards.disconnectedActors.Count==0&&JsonUtility.ToJson(session.RewardReport(1))==evidence&&Coins(session.RewardReport(1)).coins==1000,
                    "Later cleanup and disconnects cannot change a frozen terminal reward");
                session.MarkRewardsAdminUsed();
                Check(!fixture.rewards.adminUsed&&!frozen.adminUsed&&JsonUtility.ToJson(session.RewardReport(1))==evidence,
                    "Post-terminal debug use cannot retroactively rewrite an already sealed payout report");
                var restored=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(fixture));
                setter.Invoke(session,new object[]{restored});
                Check(JsonUtility.ToJson(session.RewardReport(1))==evidence,
                    "The sealed terminal report survives checkpoint replay without rebuilding mutable progress");
                fixture=Team();fixture.rewards.adminUsed=true;setter.Invoke(session,new object[]{fixture});
                finalize.Invoke(session,null);
                Check(fixture.rewards.finalReports.Count==2&&fixture.rewards.finalReports.All(r=>r.adminUsed&&Coins(r).coins==0),
                    "Admin use recorded before finalization stays excluded for every participant's sealed report");
            }
            finally{setter.Invoke(session,new object[]{original});}
        }
        static void Save()
        {
            Directory.CreateDirectory("Temp/ArchiveMatchRewards");
            File.WriteAllText("Temp/ArchiveMatchRewards/Policy.json",JsonUtility.ToJson(report,true));
            Debug.Log("Multiplayer reward policy: "+Status+" ("+report.checks.Count+").");
        }
    }
}
