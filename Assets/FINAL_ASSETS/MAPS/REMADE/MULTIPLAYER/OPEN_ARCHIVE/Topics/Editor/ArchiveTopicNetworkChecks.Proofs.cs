using System;
using System.Collections;
using System.Linq;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>T4 scenarios reuse the existing private-room transport harness.</summary>
    public static partial class ArchiveTopicNetworkChecks
    {
        static IEnumerator ProofPlace(Peer peer,int item,int slot){
            var st=session.stations[3];var q=session.Question(3);yield return Move(peer,st,st.CurrentItemPosition(item,q,session.State.stations[3]));yield return Send(peer,3,ArchiveAction.Pick,item);yield return Move(peer,st,st.SlotPosition(slot,q));yield return Send(peer,3,ArchiveAction.Place,slot);
        }
        static IEnumerator ProofArrange(Peer peer,int[] answer){
            var q=session.Question(3);var state=session.State.stations[3];for(int i=0;i<answer.Length;i++)if(state.placements[i]!=answer[i]){if(q.regionPlacement)yield return ProofPlace(peer,i,answer[i]);else yield return ProofPlace(peer,answer[i],i);}
        }
        static IEnumerator ProofsFlow(Peer a,string room){
            var st=session.stations[3];Status="T4 shared scrolls and attribution";yield return Move(a,st,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(a,3,ArchiveAction.Start);var state=session.State.stations[3];double deadline=state.timerDeadline;
            Check(session.ActiveTopic==3&&state.timerRunning&&state.timerLimit==360&&state.timerActor==a.Actor,"Guest Book starts T4's authenticated shared six-minute timer");
            var q=session.Question(3);yield return ProofArrange(a,q.expected.Select(v=>1-v).ToArray());
            var b=Connect("Archive guest B QA");yield return Wait(()=>b.client.State==ClientState.ConnectedToMasterServer,"T4 guest B connected");b.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>b.client.InRoom&&b.state!=null&&b.state.stations[3].placements.Length==q.items.Length,"T4 late guest checkpoint");Spawn(b,Position(st,Vector3.zero));yield return Wait(()=>session.ActorPlayer(b.Actor)!=null,"T4 guest B avatar");
            Check(b.state.stations[3].placements.SequenceEqual(state.placements)&&Math.Abs(b.state.stations[3].timerDeadline-deadline)<.000001,"Late join restores actual scroll classifications and exact deadline");
            yield return Send(a,3,ArchiveAction.Submit,spoof:true);Check(session.State.mistakes.Count==1&&session.State.mistakes[0].actor==a.Actor&&state.round==0&&state.lastAnswerActor==a.Actor,"Wrong proof style charges its authenticated sender despite a forged actor field");
            int revision=session.State.revision;Packet(a,lastCommand);yield return .25f;Check(session.State.revision==revision&&session.State.mistakes.Count==1,"Replayed proof submission cannot charge another mistake");
            var fake=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));fake.revision+=1000;fake.stations[3].phase=2;b.client.OpRaiseEvent(ArchiveTopicSession.SnapshotEvent,new object[]{ArchiveTopicSession.Channel,JsonUtility.ToJson(fake)},new RaiseEventOptions{Receivers=ReceiverGroup.All},SendOptions.SendReliable);yield return .25f;
            Check(session.ActiveTopic==3&&a.state.stations[3].phase==1,"Guest snapshot cannot forge proof completion");
            yield return ProofArrange(b,q.expected);yield return Send(b,3,ArchiveAction.Submit);Check(state.round==1&&state.phase==0&&state.reveal.Contains("Formal")&&Math.Abs(state.timerDeadline-deadline)<.000001,"Teammate corrects the same scrolls and shares the proof-style result");
            yield return BlockedBook(b,2,"T4 remains reserved between challenges");yield return Move(b,st,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(b,3,ArchiveAction.Start);q=session.Question(3);
            Check(state.round==1&&state.placements.Length==4&&Math.Abs(state.timerDeadline-deadline)<.000001,"Next challenge keeps four original scrolls and the same running clock");
            yield return Move(a,st,st.CurrentItemPosition(0,q,state));yield return Send(a,3,ArchiveAction.Pick,0);yield return Move(b,st,st.CurrentItemPosition(0,q,state));revision=session.State.revision;Packet(b,Command(b,3,ArchiveAction.Pick,0));yield return .25f;
            Check(session.State.revision==revision&&state.carries.Count==1&&state.carries[0].actor==a.Actor,"Guest cannot steal a manuscript carried by a teammate");
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));yield return Wait(()=>!PhotonNetwork.IsMasterClient&&a.client.CurrentRoom.MasterClientId==a.Actor,"T4 live master handoff");a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(PhotonNetwork.LocalPlayer.ActorNumber));yield return Wait(()=>PhotonNetwork.IsMasterClient,"T4 Editor regains authority");yield return .2f;state=session.State.stations[3];
            Check(state.round==1&&state.carries.Count==1&&state.carries[0].actor==a.Actor&&Math.Abs(state.timerDeadline-deadline)<.000001,"Live master handoff retains solved work, carried manuscript and deadline");
            int former=a.Actor;a.client.Disconnect();yield return Wait(()=>PhotonNetwork.CurrentRoom.GetPlayer(former)==null,"T4 starter leaves");yield return .2f;
            Check(state.carries.Count==0&&session.ActiveTopic==3&&session.ActorName(former)=="Archive guest A QA"&&session.State.mistakes[0].actor==former,"Departure returns held scrolls while preserving reservation and mistake identity");
            yield return ProofPlace(b,0,q.expected[0]);var partial=state.placements.ToArray();int before=session.State.mistakes.Count;state.timerDeadline=ArchiveTopicSession.TimerClock-.01;yield return Wait(()=>state.timerExpired&&b.state.stations[3].timerExpired,"T4 timeout replicates",8);
            Check(state.round==1&&state.placements.SequenceEqual(partial)&&session.State.mistakes.Count==before+1&&session.State.mistakes.Last().actor==b.Actor,"Timeout retains classifications and attributes one mistake to the current activator");
            yield return BlockedBook(b,0,"Expired T4 cannot be exchanged for another topic");yield return Move(b,st,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(b,3,ArchiveAction.Start);
            Check(state.placements.SequenceEqual(partial)&&state.timerAttempt==2&&session.TimerRemaining(state)>355,"Guest retry keeps current manuscripts and starts a fresh attempt");
            Status="T4 capture restrictions";yield return Move(b,st,st.CurrentItemPosition(1,q,state));var capture=session.hunting.GetSnapshot();capture.state=(int)KhezuHuntState.Caught;capture.target=capture.caught=b.Actor;capture.captureSerial++;capture.huntSerial++;capture.capturePosition=session.ActorPlayer(b.Actor).transform.position;capture.captureAnchorValid=true;capture.captureStartedAt=KhezuHuntingController.EncounterClock-3;capture.encounterPhase=4;capture.awake=true;capture.pending=false;capture.animation="Idle";capture.animationSpeed=1;session.hunting.ApplySnapshot(capture,true);yield return .25f;
            Check(!session.CanAct(b.Actor)&&session.CanAct(PhotonNetwork.LocalPlayer.ActorNumber),"Only the captured player loses proof interaction authority");revision=session.State.revision;Packet(b,Command(b,3,ArchiveAction.Pick,1));yield return .25f;Check(session.State.revision==revision&&state.carries.Count==0,"Captured guest cannot pick a nearby unheld manuscript");session.hunting.Request(KhezuCommand.Sleep);yield return .2f;
            Status="All five T4 challenges over Photon";
            for(int r=state.round;r<5;r++){
                if(state.phase==0){yield return Move(b,st,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(b,3,ArchiveAction.Start);}
                q=session.Question(3);var answer=ArchiveTopicChecks.Solution(q).placements;
                if(r==4){int n=q.statementCount;int equal=Enumerable.Range(1,n-1).First(i=>q.proof.steps[answer[i]].justification==q.proof.steps[answer[0]].justification);int swap=answer[n];answer[n]=answer[n+equal];answer[n+equal]=swap;}
                yield return ProofArrange(b,answer);yield return Send(b,3,ArchiveAction.Submit);Check(state.round==r+1&&b.state.stations[3].round==r+1&&!string.IsNullOrEmpty(state.reveal),"Photon challenge "+(r+1)+" shares its accepted proof and progress");
            }
            Check(state.phase==2&&!state.timerRunning&&session.ActiveTopic==-1,"All five networked proof challenges freeze time and unlock the next topic");Check(session.State.stations.Where(s=>s.topic!=3).All(s=>s.round==0&&s.timerAttempt==0),"T4 network play preserves every other topic's progress and timer");Status="Completed T4 transport checks";
        }
    }
}
