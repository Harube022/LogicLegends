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
        static bool optionalRelaysOnly;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Optional Topic Relays (Live Photon)")]
        public static void BeginOptionalRelays()=>StartChecks(false,optional:true);
        static ArchiveCommand OptionalCommand(Peer peer,int topic,int input,bool confirm,bool value=false)
        {
            var c=session.Relay(topic);
            var cmd=Command(peer,topic,confirm?ArchiveAction.ConfirmT1Input:ArchiveAction.SetT1Input,input,confirm?c.assignmentRevision:value?1:0);
            cmd.matchId=session.State.matchId;cmd.captureSerial=c.serial;return cmd;
        }
        static IEnumerator OptionalHostAnswer()
        {
            var station=session.stations[0];var s=session.State.stations[0];
            if(s.phase==0){ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(0,ArchiveAction.Start);yield return .1f;}
            int round=s.round;var q=session.Question(0);
            ArchiveTopicPlayChecks.MoveTo(station,station.ItemPosition(q.correctChoice,q));session.Request(0,ArchiveAction.BeginChoice,q.correctChoice);
            yield return Wait(()=>s.round==round+1,"Native host rune answer "+round,8);
        }
        static IEnumerator OptionalInputs(Peer peer,int topic,bool p,bool q)
        {
            var station=session.stations[topic];var trial=station.GetComponent<ArchiveT1CoopTrial>();
            yield return Move(peer,station,trial.SwitchPosition(0));yield return Receipt(peer,OptionalCommand(peer,topic,0,false,p),true,"Support sets P at T"+(topic+1));
            yield return Move(peer,station,trial.SwitchPosition(1));yield return Receipt(peer,OptionalCommand(peer,topic,1,false,q),true,"Support sets Q at T"+(topic+1));
            yield return Move(peer,station,trial.SwitchPosition(0));yield return Receipt(peer,OptionalCommand(peer,topic,0,true),true,"Support confirms current P");
            yield return Move(peer,station,trial.SwitchPosition(1));yield return Receipt(peer,OptionalCommand(peer,topic,1,true),true,"Support confirms current Q");
        }
        static IEnumerator OptionalRelayFlow(Peer a,string room)
        {
            Status="Optional relay before and during main work";
            session.hunting.enabled=false;
            var station=session.stations[0];var trial=station.GetComponent<ArchiveT1CoopTrial>();
            Check(session.stations.All(s=>s.GetComponent<ArchiveT1CoopTrial>()!=null),"Saved scene contains one reusable optional activity at each of T1–T10");
            yield return Move(a,station,trial.SwitchPosition(0));
            yield return Receipt(a,OptionalCommand(a,0,0,false,true),false,"Optional input is rejected before the first Book");
            ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(0,ArchiveAction.Start);yield return .15f;
            var c=session.Relay(0);var s=session.State.stations[0];double deadline=s.timerDeadline;
            Check(session.RelayAvailable(0)&&session.ActiveTopic==0,"First Book opens the side activity while the main topic stays active");
            bool p=false,q=false;
            foreach(bool x in new[]{false,true})foreach(bool y in new[]{false,true})if(ArchiveTopicSession.EvaluateConnective(c.connective,x,y)!=c.target){p=x;q=y;}
            yield return OptionalInputs(a,0,p,q);
            Check(!c.open&&c.attempts==0,"One player cannot open a team relay without any main-topic contribution");
            yield return OptionalHostAnswer();yield return .25f;
            Check(c.attempts==1&&!c.open&&session.State.mistakes.Count==0,"Wrong optional practice is checked after real main progress and adds no monster mistake");
            Check(s.round==1&&s.timerDeadline==deadline,"Optional work never advances the main round or refills its clock");
            RelayAssignment(c,c.target,out p,out q);
            yield return Move(a,station,trial.SwitchPosition(0));yield return Receipt(a,OptionalCommand(a,0,0,false,p),true,"Support changes P");
            var stale=OptionalCommand(a,0,0,true);
            yield return Move(a,station,trial.SwitchPosition(1));yield return Receipt(a,OptionalCommand(a,0,1,false,!q),true,"Changing Q invalidates previous confirmations");
            yield return Move(a,station,trial.SwitchPosition(0));yield return Receipt(a,stale,false,"Confirmation bound to an old assignment is rejected");
            yield return OptionalInputs(a,0,p,q);yield return Wait(()=>c.open,"Correct support assignment opens bonus arch",8);
            Check(c.actorP==a.Actor&&c.actorQ==a.Actor&&!c.reducedTeam,"Two-player match supports one main solver and one support player handling both runes");
            Check(session.State.solved.Any(v=>v.topic==0&&v.contributors.Contains(PhotonNetwork.LocalPlayer.ActorNumber)),"Team eligibility comes from actual accepted main work");
            yield return Move(a,station,trial.LanePoint(new Vector3(7,0,12.9f)));yield return .2f;
            Check(!c.completed,"Teleporting to the bonus exit does not earn completion");
            yield return Move(a,station,trial.LanePoint(new Vector3(-7,0,12.9f)));yield return .3f;
            yield return RelayWalk(a,trial,new Vector3(-7,0,12.9f),new Vector3(7,0,12.9f));
            yield return Wait(()=>c.completed,"Continuous supported walk completes optional bonus",8);
            Check(s.round==1&&s.phase==0&&session.ActiveTopic==0&&session.State.completedRelays.Count==1,"Bonus completion leaves academic progress and topic reservation intact");
            Check(session.State.quality.Count(v=>v.key=="coop:0")==2,"Main solver and support receive one existing participation receipt each");
            yield return Wait(()=>a.state!=null&&a.state.topicRelays.Count==10&&a.state.topicRelays[0].completed,"Guest receives optional completion checkpoint",8);
            int creditCount=session.State.quality.Count;int revision=session.State.revision;
            yield return Move(a,station,trial.SwitchPosition(0));yield return Receipt(a,OptionalCommand(a,0,0,true),false,"Completed optional bonus cannot be farmed again");
            Check(session.State.quality.Count==creditCount&&session.State.revision==revision,"Rejected repeat adds no credit or state mutation");
            Status="Native main completion closes bonus and unlocks next topic";
            while(s.round<5)yield return OptionalHostAnswer();
            Check(s.phase==2&&c.closed&&!session.RelayAvailable(0)&&session.ActiveTopic==-1,"Five actual T1 answers complete immediately without a required relay phase");
            yield return Receipt(a,OptionalCommand(a,0,0,false,true),false,"Even a completed bonus rejects inputs after main topic completion");
            station=session.stations[6];trial=station.GetComponent<ArchiveT1CoopTrial>();
            ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(6,ArchiveAction.Start);yield return .15f;
            var sets=session.State.stations[6];var setsRelay=session.Relay(6);var setsView=station.GetComponent<ArchiveCoopActivityView>();
            Check(session.RelayAvailable(6)&&!session.RelayAvailable(0),"Only the currently active topic exposes its optional activity");
            yield return BonusRemote(a,6,0,setsView.ItemPosition(0),0);
            var delayed=BonusPacket(a,6,6);
            Check(session.RequestTopicAdmin(6,sets.revision,true),"Existing authorized Admin completes active Sets fixture");yield return .1f;
            Check(session.Relay(6).closed&&!session.Relay(6).completed&&!session.RelayAvailable(6),"Main completion discards unfinished optional eligibility without awarding a bonus");
            yield return Receipt(a,delayed,false,"An in-flight confirmation cannot finish a bonus after its main topic closes");
            Check(session.State.completedRelays.Count==1&&session.CanStartTopic(7),"Expired optional work neither invents completion nor blocks another topic");
            Status="Late join and authority transfer";
            var b=Connect("Optional relay late guest QA");yield return Wait(()=>b.client.State==ClientState.ConnectedToMasterServer,"Late relay peer connected");
            b.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>b.client.InRoom&&b.state!=null&&b.state.topicRelays.Count==10,"Late peer restores all relay records");
            Check(b.state.topicRelays[0].completed&&b.state.topicRelays[6].closed&&!b.state.topicRelays[6].completed,"Late join distinguishes earned, skipped and closed optional work");
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));yield return Wait(()=>!PhotonNetwork.IsMasterClient,"Optional checkpoint authority leaves Editor");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(PhotonNetwork.LocalPlayer.ActorNumber));yield return Wait(()=>PhotonNetwork.IsMasterClient,"Optional checkpoint authority returns");yield return .2f;
            Check(session.Relay(0).completed&&session.Relay(6).closed&&session.State.completedRelays.Count==1,"Master handoff preserves exact optional eligibility and one-time completion");
            Check(session.State.mistakes.Count==0,"Optional testing changes no academic mistakes, health or monster penalty");
            Status="Optional topic relays passed";
        }
    }
}
