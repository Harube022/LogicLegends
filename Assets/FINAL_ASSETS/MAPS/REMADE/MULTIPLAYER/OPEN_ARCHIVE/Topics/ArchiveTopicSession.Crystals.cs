using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    [Serializable] public sealed class ArchiveCrystalMember
    {
        public int actor;
        public string name;
        public bool excused, awaitingGrace;
        public double absentSince = -1;
    }
    [Serializable] public sealed class ArchiveCrystalState
    {
        public int topic, resetSerial;
        public bool started, closed, online, fullyRestored, adminBypass;
        public double restoredAt;
        public List<ArchiveCrystalMember> members = new List<ArchiveCrystalMember>();
    }
    /// <summary>A read model over accepted main-topic receipts. No independent puzzle or timer.</summary>
    public static class ArchiveCrystalProgress
    {
        public static int Total(int topic) => ArchiveTopicCatalog.RoundCount(topic) * (topic == 2 ? 2 : 1);
        public static IEnumerable<ArchiveSolvedMilestone> Receipts(ArchiveTeamState team,int topic) =>
            (team.solved ?? new List<ArchiveSolvedMilestone>()).Where(s=>s.topic==topic&&s.resetSerial==team.stations[topic].testResetSerial&&
                s.round>=0&&s.round<ArchiveTopicCatalog.RoundCount(topic)&&s.subphase>=0&&s.subphase<(topic==2?2:1))
                .GroupBy(s=>s.round+":"+s.subphase).Select(g=>g.First());
        public static int Solved(ArchiveTeamState team,int topic) => Receipts(team,topic).Count();
        public static int[] Contributors(ArchiveTeamState team,int topic) => Receipts(team,topic)
            .SelectMany(s=>s.crystalContributors??Array.Empty<int>()).Where(a=>a!=0).Distinct().ToArray();
        public static bool Contributed(ArchiveTeamState team,int topic,int actor) => Contributors(team,topic).Contains(actor);
        public static ArchiveCrystalMember[] Required(ArchiveTeamState team,ArchiveCrystalState crystal)
        {
            var contributors=Contributors(team,crystal.topic);
            return crystal.members.Where(m=>!m.excused||contributors.Contains(m.actor)).ToArray();
        }
        public static int ContributorCount(ArchiveTeamState team,ArchiveCrystalState crystal) =>
            crystal.members.Count(m=>Contributed(team,crystal.topic,m.actor));
        public static bool TeamReady(ArchiveTeamState team,ArchiveCrystalState crystal)
        {
            if(!crystal.started||!crystal.online||crystal.adminBypass||crystal.members.Count<2)return false;
            var contributors=Contributors(team,crystal.topic);
            return crystal.members.Count(m=>contributors.Contains(m.actor))>=2&&Required(team,crystal).All(m=>contributors.Contains(m.actor));
        }
        public static bool CanRestore(ArchiveTeamState team,ArchiveCrystalState crystal) => !crystal.adminBypass&&
            team.stations[crystal.topic].phase==2&&Solved(team,crystal.topic)==Total(crystal.topic)&&TeamReady(team,crystal);
        public static float Body(ArchiveTeamState team,int topic) => Mathf.Clamp01((float)Solved(team,topic)/Total(topic));
        public static float Team(ArchiveTeamState team,ArchiveCrystalState crystal)
        {
            if(!crystal.online||crystal.members.Count<2||crystal.adminBypass)return 0;
            var required=Required(team,crystal);
            return Mathf.Clamp01((float)required.Count(m=>Contributed(team,crystal.topic,m.actor))/Mathf.Max(2,required.Length));
        }
        public static float Fraction(ArchiveTeamState team,ArchiveCrystalState crystal) => crystal.fullyRestored?1:
            Mathf.Min(.999f,.8f*Body(team,crystal.topic)+.2f*Team(team,crystal));
        // Flooring prevents displaying 100% before the authoritative full-restoration receipt.
        public static int Percent(ArchiveTeamState team,ArchiveCrystalState crystal) => crystal.fullyRestored?100:
            Mathf.FloorToInt(Fraction(team,crystal)*100+.001f);

        /// <summary>Presence is supplied by the authoritative room/health owner, never client lobby flags.</summary>
        public static bool Presence(ArchiveCrystalMember member,bool present,bool eliminated,double now,double grace,bool closed)
        {
            if(closed&&!member.awaitingGrace)return false;
            bool excused=member.excused,waiting=member.awaitingGrace;double absent=member.absentSince;
            if(eliminated){member.excused=true;member.awaitingGrace=false;}
            else if(present)
            {
                if(!closed)member.excused=false;
                member.absentSince=-1;member.awaitingGrace=false;
            }
            else
            {
                if(member.absentSince<0)member.absentSince=now;
                member.awaitingGrace=true;
                if(now-member.absentSince>=grace){member.excused=true;member.awaitingGrace=false;}
            }
            return excused!=member.excused||waiting!=member.awaitingGrace||absent!=member.absentSince;
        }
    }
    public sealed partial class ArchiveTopicSession
    {
        [Header("Topic crystal restoration")]
        [Tooltip("Replace optional side puzzles with crystal restoration from accepted main-topic work.")]
        public bool crystalRestoration = true;
        float nextCrystalRosterTick;
        public bool CrystalsEnabled => crystalRestoration;
        public ArchiveCrystalState Crystal(int topic) => Ready&&topic>=0&&topic<10&&State.crystals!=null&&State.crystals.Count>topic?State.crystals[topic]:null;
        // These are eligibility receipts for future match scoring; no currency or leaderboard is written.
        public int FullyRestoredCrystals => !Ready||State.crystals==null?0:State.crystals.Count(c=>c.fullyRestored&&!c.adminBypass);
        public int[] CrystalBonusTopics => !Ready||State.crystals==null?Array.Empty<int>():State.crystals.Where(c=>c.fullyRestored&&!c.adminBypass).Select(c=>c.topic).ToArray();
        void NormalizeCrystals()
        {
            if(State.crystals==null)State.crystals=new List<ArchiveCrystalState>();
            while(State.crystals.Count<10)State.crystals.Add(new ArchiveCrystalState{topic=State.crystals.Count});
            for(int i=0;i<10;i++)
            {
                if(State.crystals[i]==null||State.crystals[i].resetSerial!=State.stations[i].testResetSerial)
                    State.crystals[i]=new ArchiveCrystalState{topic=i,resetSerial=State.stations[i].testResetSerial};
                var c=State.crystals[i];c.topic=i;
                if(c.members==null)c.members=new List<ArchiveCrystalMember>();
            }
        }
        void BeginTopicCrystal(int topic)
        {
            NormalizeCrystals();var c=Crystal(topic);if(c.started||c.adminBypass)return;
            c.started=true;c.online=PhotonNetwork.InRoom;
            var actors=c.online?PhotonNetwork.CurrentRoom.Players.Values.Where(p=>CrystalAlive(p.ActorNumber)).Select(p=>p.ActorNumber).OrderBy(a=>a).ToArray():new[]{-1};
            foreach(int actor in actors)c.members.Add(new ArchiveCrystalMember{actor=actor,name=ActorName(actor)});
        }
        bool CrystalAlive(int actor) => hunting==null||hunting.health==null||!hunting.health.HasAuthoritativeActorHealth||hunting.health.IsActorAlive(actor);
        bool RefreshCrystals()
        {
            if(!Ready||!IsAuthority||!crystalRestoration)return false;
            NormalizeCrystals();bool changed=false;
            foreach(var c in State.crystals)
            {
                var station=State.stations[c.topic];
                if(!c.started||c.fullyRestored||c.adminBypass)continue;
                foreach(var member in c.members)
                {
                    var peer=PhotonNetwork.InRoom?PhotonNetwork.CurrentRoom.GetPlayer(member.actor):null;
                    bool present=!c.online||peer!=null&&!peer.IsInactive;
                    bool eliminated=c.online&&!CrystalAlive(member.actor);
                    changed|=ArchiveCrystalProgress.Presence(member,present,eliminated,Clock,LobbyRules.RecoverySeatMilliseconds/1000d,c.closed);
                }
                if(!c.closed&&station.phase==2)
                {
                    c.closed=true;
                    // Only an absence already pending at completion may settle later.
                    // Leaving after completion cannot erase a missing participation rune.
                    foreach(var member in c.members)member.awaitingGrace=member.absentSince>=0&&!member.excused;
                    changed=true;
                }
                if(ArchiveCrystalProgress.CanRestore(State,c))
                {c.fullyRestored=true;c.restoredAt=Clock;changed=true;}
            }
            return changed;
        }
        void ResetTopicCrystal(int topic,bool adminComplete)
        {
            NormalizeCrystals();State.crystals[topic]=new ArchiveCrystalState{topic=topic,resetSerial=State.stations[topic].testResetSerial,adminBypass=adminComplete,closed=adminComplete};
        }
        public string CrystalStatus(int topic)
        {
            var c=Crystal(topic);if(c==null)return "Crystal waiting";
            if(c.adminBypass)return "Admin preview - no crystal bonus";
            if(c.fullyRestored)return "Fully restored - team bonus recorded";
            if(!c.started)return "Read the Book to begin restoration";
            if(!c.online||c.members.Count<2)return "Solo practice - crystal body can reach 80%";
            if(c.closed)return c.members.Any(m=>m.awaitingGrace)?"Topic complete - teammate reconnect allowance":"Topic complete - teamwork runes unfinished";
            var missing=ArchiveCrystalProgress.Required(State,c).Where(m=>!ArchiveCrystalProgress.Contributed(State,topic,m.actor)).Select(m=>m.name).ToArray();
            return missing.Length==0?"All team runes lit - finish the topic":"Accepted work needed from "+missing.Length+" teammate"+(missing.Length==1?"":"s");
        }
    }
}
