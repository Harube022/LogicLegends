using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveCrystalChecks
    {
        [Serializable] public sealed class Report
        {public bool passed;public int checks;public string scope;public List<string> errors=new List<string>();}
        static void Require(bool pass,string name,Report report){report.checks++;if(!pass)throw new InvalidOperationException(name);}
        static ArchiveTeamState Fixture(int topic,int players)
        {
            var team=new ArchiveTeamState{stations=Enumerable.Range(0,10).Select(i=>new ArchiveStationState{topic=i}).ToArray()};
            team.crystals=Enumerable.Range(0,10).Select(i=>new ArchiveCrystalState{topic=i,started=i==topic,online=true}).ToList();
            team.crystals[topic].members=Enumerable.Range(1,players).Select(a=>new ArchiveCrystalMember{actor=a,name="Player "+a}).ToList();return team;
        }
        static void Receipts(ArchiveTeamState team,int topic,Func<int,int> owner)
        {
            int subphases=topic==2?2:1;
            for(int i=0;i<ArchiveCrystalProgress.Total(topic);i++)team.solved.Add(new ArchiveSolvedMilestone{topic=topic,round=i/subphases,subphase=i%subphases,resetSerial=team.stations[topic].testResetSerial,crystalContributors=new[]{owner(i)}});
            team.stations[topic].phase=2;
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Validate Crystal Restoration Rules")]
        public static Report Validate()
        {
            var report=new Report{scope="Deterministic rule fixtures, including roster grace, normalized topic steps, no confirmation-only credit, reset filtering and checkpoint round-trip. Not a played multiplayer match."};
            try
            {
                for(int topic=0;topic<10;topic++)for(int count=2;count<=4;count++)
                {
                    var team=Fixture(topic,count);var c=team.crystals[topic];
                    Require(ArchiveCrystalProgress.Percent(team,c)==0,"Initial crystal must be broken",report);
                    Receipts(team,topic,_=>1);
                    Require(ArchiveCrystalProgress.Body(team,topic)==1&&!ArchiveCrystalProgress.CanRestore(team,c),"One solver may finish the topic but cannot fully restore",report);
                    Require(ArchiveCrystalProgress.Percent(team,c)<100,"Single solver never displays 100 percent",report);
                    team.solved.Clear();Receipts(team,topic,i=>i%count+1);
                    Require(ArchiveCrystalProgress.CanRestore(team,c),"All eligible teammates' real work permits full restoration",report);
                    team.stations[topic].phase=1;Require(!ArchiveCrystalProgress.CanRestore(team,c),"Cooperation cannot bypass topic completion",report);team.stations[topic].phase=2;
                    int accepted=ArchiveCrystalProgress.Solved(team,topic);team.solved.Add(team.solved[0]);
                    Require(ArchiveCrystalProgress.Solved(team,topic)==accepted,"Duplicate receipt cannot restore another piece",report);
                    c.adminBypass=true;Require(!ArchiveCrystalProgress.CanRestore(team,c),"Admin completion never qualifies",report);c.adminBypass=false;
                    var restored=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(team));
                    Require(ArchiveCrystalProgress.CanRestore(restored,restored.crystals[topic]),"Checkpoint preserves meaningful contributors",report);
                    team.stations[topic].testResetSerial++;
                    Require(ArchiveCrystalProgress.Solved(team,topic)==0&&ArchiveCrystalProgress.Contributors(team,topic).Length==0,"Old reset receipts cannot restore the new pillar",report);
                }
                var solo=Fixture(0,1);solo.crystals[0].online=false;Receipts(solo,0,_=>1);
                Require(ArchiveCrystalProgress.Percent(solo,solo.crystals[0])==80&&!ArchiveCrystalProgress.CanRestore(solo,solo.crystals[0]),"Solo practice keeps body progress without team bonus",report);
                var confirm=Fixture(0,2);Receipts(confirm,0,_=>1);foreach(var s in confirm.solved)s.contributors=new[]{1,2};
                Require(!ArchiveCrystalProgress.TeamReady(confirm,confirm.crystals[0]),"Ordinary confirmer participation does not light a crystal rune",report);
                var member=new ArchiveCrystalMember{actor=2};
                ArchiveCrystalProgress.Presence(member,false,false,10,45,false);
                ArchiveCrystalProgress.Presence(member,false,false,54.9,45,false);Require(!member.excused&&member.awaitingGrace,"Disconnect retains the rune requirement during grace",report);
                ArchiveCrystalProgress.Presence(member,true,false,54.95,45,false);Require(!member.excused&&member.absentSince<0,"Rejoining retains identity and required rune",report);
                ArchiveCrystalProgress.Presence(member,false,false,60,45,false);ArchiveCrystalProgress.Presence(member,false,false,105,45,false);
                Require(member.excused&&!member.awaitingGrace,"Expired recovery seat releases an unavailable member",report);
                member=new ArchiveCrystalMember{actor=2};ArchiveCrystalProgress.Presence(member,false,false,1,45,true);
                Require(!member.excused&&member.absentSince<0,"Leaving after topic completion cannot manufacture a bonus",report);
                var dead=Fixture(0,3);Receipts(dead,0,i=>i%2+1);dead.crystals[0].members[2].excused=true;
                Require(ArchiveCrystalProgress.TeamReady(dead,dead.crystals[0]),"Two genuine contributors may qualify after a third is eliminated",report);
                dead.crystals[0].members[1].excused=true;
                Require(ArchiveCrystalProgress.TeamReady(dead,dead.crystals[0]),"An earned contribution survives that player's later elimination",report);
                var lone=Fixture(0,3);Receipts(lone,0,_=>1);lone.crystals[0].members[1].excused=lone.crystals[0].members[2].excused=true;
                Require(!ArchiveCrystalProgress.TeamReady(lone,lone.crystals[0]),"Disconnects cannot turn one solver into a cooperative completion",report);
                Require(ArchiveCrystalPillar.Palette.Distinct().Count()==10,"All ten pillars have distinct palette entries",report);
                report.passed=true;
            }
            catch(Exception e){report.errors.Add(e.ToString());}
            Directory.CreateDirectory("Temp/ArchiveCrystals");File.WriteAllText("Temp/ArchiveCrystals/Rules.json",JsonUtility.ToJson(report,true));return report;
        }
    }
}
