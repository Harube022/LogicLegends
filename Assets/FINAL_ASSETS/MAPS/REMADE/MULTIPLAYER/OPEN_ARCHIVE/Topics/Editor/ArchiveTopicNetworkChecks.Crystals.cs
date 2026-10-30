using System;
using System.Collections;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveTopicNetworkChecks
    {
        static bool crystalsOnly;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Crystal Teamwork (Live Photon)")]
        public static void BeginCrystals(){StartChecks(false,crystals:true);}
        static IEnumerator CrystalChoice(Peer peer,int round)
        {
            var station=session.stations[0];
            if(session.State.stations[0].phase==0)
            {ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(0,ArchiveAction.Start);yield return .1f;}
            var q=session.Question(0);
            if(peer==null){ArchiveTopicPlayChecks.MoveTo(station,station.ItemPosition(q.correctChoice,q));session.Request(0,ArchiveAction.BeginChoice,q.correctChoice);}
            else{yield return Move(peer,station,station.ItemPosition(q.correctChoice,q));yield return Send(peer,0,ArchiveAction.BeginChoice,q.correctChoice);}
            yield return Wait(()=>session.State.stations[0].round==round+1,"T1 correct rune finishes",8);
        }
        static IEnumerator CrystalsFlow(Peer a,string room)
        {
            report.scope="Live private Photon room: real main-topic answers from a rendered Editor host and independent Realtime peers. Validates single-solver cap, shared restoration, confirmation-only exclusion, wrong-answer rejection, late join, checkpoint and authority handoff. Physical Android/two rendered mobile clients are unverified.";
            session.hunting.enabled=false;
            Check(session.CrystalsEnabled&&session.stations.All(t=>t.GetComponentInChildren<ArchiveCrystalPillar>()!=null),"Saved scene enables all ten crystal pillars");
            Check(session.stations.All(t=>!t.GetComponent<ArchiveT1CoopTrial>().enabled)&&session.stations.Where(t=>t.topic>0).All(t=>!t.GetComponent<ArchiveCoopActivityView>().enabled),"Separate optional minigames are disabled");
            int host=PhotonNetwork.LocalPlayer.ActorNumber;
            Status="One-solver cap";
            for(int round=0;round<5;round++)yield return CrystalChoice(null,round);
            var c=session.Crystal(0);
            Check(session.State.stations[0].phase==2&&ArchiveCrystalProgress.Body(session.State,0)==1,"One player may finish every main challenge without a crystal gate");
            Check(!c.fullyRestored&&ArchiveCrystalProgress.Percent(session.State,c)==90&&session.FullyRestoredCrystals==0,"One of two contributors reaches 90 percent but earns no full-restoration receipt");
            Check(ArchiveCrystalProgress.Contributors(session.State,0).SequenceEqual(new[]{host}),"Only the real rune chooser has crystal work credit");
            Check(session.CanStartTopic(1),"Unfinished team runes never reserve the next topic");
            session.Initialize(271828);Status="Cooperative restoration";
            for(int round=0;round<5;round++)yield return CrystalChoice(round%2==0?null:a,round);
            c=session.Crystal(0);double restoredAt=c.restoredAt;
            Check(c.fullyRestored&&ArchiveCrystalProgress.Percent(session.State,c)==100&&session.FullyRestoredCrystals==1,"Two real rune solvers fully restore the pillar exactly once");
            var pillar=session.stations[0].GetComponentInChildren<ArchiveCrystalPillar>();
            ArchiveTopicPlayChecks.MoveTo(session.stations[0],new Vector3(-10,0,7));yield return .3f;
            Check(pillar.pulseRings.All(r=>r.gameObject.activeInHierarchy)&&pillar.motes.All(m=>m.gameObject.activeInHierarchy),"Actual full restoration enables the three pulse rings and lightweight fragments");
            var film=new GameObject("Crystal_QA_Camera").AddComponent<Camera>();film.CopyFrom(Camera.main);film.depth=100;film.tag="Untagged";
            Vector3 focus=pillar.transform.position+Vector3.up*6;var offset=session.stations[0].transform.forward*24+Vector3.up*10;
            film.transform.SetPositionAndRotation(focus+offset,Quaternion.LookRotation(-offset));
            yield return .15f;UnityEngine.ScreenCapture.CaptureScreenshot("Temp/ArchiveCrystals/Crystal_Full_ActualTeamwork.png");yield return .15f;UnityEngine.Object.Destroy(film.gameObject);
            yield return Wait(()=>a.state!=null&&a.state.crystals.Count==10&&a.state.crystals[0].fullyRestored,"Guest receives full crystal checkpoint");
            Check(a.state.crystals[0].members.Count==2&&ArchiveCrystalProgress.Contributors(a.state,0).OrderBy(x=>x).SequenceEqual(new[]{host,a.Actor}.OrderBy(x=>x)),"Host and guest agree on the frozen roster and actual contributors");
            var stale=new ArchiveCommand{topic=0,action=ArchiveAction.BonusInteract,a=6,matchId=session.State.matchId,sequence=int.MaxValue};
            Packet(a,stale);yield return .2f;
            Check(session.FullyRestoredCrystals==1&&c.restoredAt==restoredAt&&session.State.completedRelays.Count==0,"Legacy bonus commands cannot farm another restoration or bonus receipt");
            var b=Connect("Crystal late join QA");yield return Wait(()=>b.client.State==ClientState.ConnectedToMasterServer,"Late peer connects");
            b.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>b.client.InRoom&&PhotonNetwork.CurrentRoom.PlayerCount==3,"Late peer joins");
            yield return Wait(()=>b.state!=null&&b.state.crystals.Count==10,"Late peer reads crystal checkpoint");
            Check(b.state.crystals[0].fullyRestored&&b.state.crystals[0].members.Count==2&&b.state.crystals[0].restoredAt==restoredAt,"Late join sees earned restoration without changing its original roster");
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));yield return Wait(()=>!PhotonNetwork.IsMasterClient&&a.client.CurrentRoom.MasterClientId==a.Actor,"Peer takes authority");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(host));yield return Wait(()=>PhotonNetwork.IsMasterClient,"Rendered host regains authority");yield return .2f;
            Check(session.Crystal(0).fullyRestored&&session.Crystal(0).restoredAt==restoredAt&&session.CrystalBonusTopics.SequenceEqual(new[]{0}),"Host transfer preserves the single earned restoration receipt");
            session.Initialize(161803);Status="Meaningful work versus Submit";var truth=session.stations[1];
            ArchiveTopicPlayChecks.MoveTo(truth,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(1,ArchiveAction.Start);yield return .1f;
            Check(session.Crystal(1).members.Count==3,"A new topic snapshots the current eligible team");
            double deadline=session.State.stations[1].timerDeadline;var q2=session.Question(1);var solution=ArchiveTopicChecks.Solution(q2);
            for(int slot=0;slot<4;slot++)
            {
                int item=slot==0?1-solution.placements[slot]:solution.placements[slot];
                yield return Move(a,truth,truth.CurrentItemPosition(item,q2,session.State.stations[1]));yield return Send(a,1,ArchiveAction.Pick,item);
                yield return Move(a,truth,truth.SlotPosition(slot,q2));yield return Send(a,1,ArchiveAction.Place,slot);
            }
            ArchiveTopicPlayChecks.MoveTo(truth,Vector3.zero);session.Request(1,ArchiveAction.Submit);yield return .15f;
            Check(ArchiveCrystalProgress.Solved(session.State,1)==0&&ArchiveCrystalProgress.Contributors(session.State,1).Length==0,"A wrong answer restores no fragments and lights no contributor rune");
            int correct=solution.placements[0];yield return Move(a,truth,truth.CurrentItemPosition(correct,q2,session.State.stations[1]));yield return Send(a,1,ArchiveAction.Pick,correct);
            yield return Move(a,truth,truth.SlotPosition(0,q2));yield return Send(a,1,ArchiveAction.Place,0);yield return 1.55f;
            ArchiveTopicPlayChecks.MoveTo(truth,Vector3.zero);session.Request(1,ArchiveAction.Submit);yield return Wait(()=>session.State.stations[1].round==1,"Correct shared table accepted");
            Check(ArchiveCrystalProgress.Solved(session.State,1)==1&&ArchiveCrystalProgress.Contributors(session.State,1).SequenceEqual(new[]{a.Actor}),"Actual tile builder receives crystal credit; the Submit-only host does not");
            Check(Math.Abs(session.State.stations[1].timerDeadline-deadline)<.000001,"Crystal processing preserves the exact main-topic deadline");
            Check(session.RequestTopicAdmin(1,session.State.stations[1].revision,true),"Authorized Admin completes the active test topic");
            Check(session.Crystal(1).adminBypass&&!session.Crystal(1).fullyRestored&&session.FullyRestoredCrystals==0,"Admin completion cannot manufacture crystal bonus eligibility");
        }
    }
}
