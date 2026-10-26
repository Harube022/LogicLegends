using System;
using System.Collections;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveTopicNetworkChecks
    {
        static bool fairnessOnly;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Cooperative Fairness (Live Photon)")]
        public static void BeginFairness() => StartChecks(false,fairness:true);
        static ArchiveCommand Intent(Peer peer,int topic,ArchiveAction action,int a=-1,int b=-1)
        {
            var cmd=Command(peer,topic,action,a,b);var s=session.State.stations[topic];
            cmd.contextVersion=1;cmd.matchId=session.State.matchId;cmd.round=s.round;cmd.subphase=s.subphase;cmd.resetSerial=s.testResetSerial;cmd.phase=s.phase;
            cmd.workStamp=ArchiveTopicSession.WorkStamp(s);
            cmd.expectedHeld=s.carries.FirstOrDefault(c=>c.actor==peer.Actor)?.item??-1;
            if(action==ArchiveAction.Place&&a>=0&&a<s.placements.Length)cmd.expectedSlot=s.placements[a];
            return cmd;
        }
        static IEnumerator Receipt(Peer peer,ArchiveCommand cmd,bool accepted,string label)
        {
            Packet(peer,cmd);
            yield return Wait(()=>peer.receipts.ContainsKey(cmd.sequence),label+" acknowledgement",8);
            Check(peer.receipts[cmd.sequence].accepted==accepted,label);
            if(accepted)report.acceptedRemoteCommands++;
        }
        static IEnumerator FairnessFlow(Peer a,string room)
        {
            Status="Two-player match pacing";
            Check(session.State.pacingPlayers==2&&session.PacingMultiplier==1.25f,"Two-player party receives 25% more topic time");
            Check(session.hunting.barrierTimeLimitSeconds==90&&session.hunting.counterWarningSeconds>=.9f,"Normal barrier is bounded to 90 seconds with a readable counter warning");
            var station=session.stations[5];
            yield return Move(a,station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
            yield return Receipt(a,Intent(a,5,ArchiveAction.Start),true,"Guest starts the native Book");
            var s=session.State.stations[5];var q=session.Question(5);
            Check(s.timerLimit==675&&s.timerRunning,"Nine-minute induction budget scales to 675 seconds for two players");
            Check(q.items.Length>=2&&!q.reusableItems,"Concurrency fixture uses two distinct original induction props");
            double deadline=s.timerDeadline;
            ArchiveTopicPlayChecks.MoveTo(station,station.CurrentItemPosition(0,q,s));
            yield return Move(a,station,station.CurrentItemPosition(1,q,s));
            var independent=Intent(a,5,ArchiveAction.Pick,1);
            session.Request(5,ArchiveAction.Pick,0);
            yield return Receipt(a,independent,true,"Independent guest pick survives the host's simultaneous edit");
            Check(s.carries.Count==2&&s.carries.Any(c=>c.actor==a.Actor&&c.item==1),"Both teammates retain their own original item");
            int revision=session.State.revision;
            Packet(a,independent);yield return .35f;
            Check(session.State.revision==revision&&s.carries.Count==2,"Replaying an acknowledged command never repeats its mutation");

            Status="Concurrent placement conflicts";
            yield return Move(a,station,station.SlotPosition(0,q));
            ArchiveTopicPlayChecks.MoveTo(station,station.SlotPosition(0,q));
            var conflict=Intent(a,5,ArchiveAction.Place,0);
            session.Request(5,ArchiveAction.Place,0);
            yield return Receipt(a,conflict,false,"An occupied slot cannot silently overwrite the host's placement");
            Check(s.placements[0]==0&&s.carries.Any(c=>c.actor==a.Actor&&c.item==1)&&session.State.mistakes.Count==0,"Conflict retains the guest's carried item without a mistake");

            Status="Stale confirmations and personal hints";
            var staleSubmit=Intent(a,5,ArchiveAction.Submit);
            ArchiveTopicPlayChecks.MoveTo(station,station.CurrentItemPosition(0,q,s));session.Request(5,ArchiveAction.Pick,0);
            yield return Receipt(a,staleSubmit,false,"Changed work requires a fresh confirmation rather than an unseen wrong answer");
            Check(session.State.mistakes.Count==0&&s.answerSerial==0,"Rejected stale submission has no academic or health penalty");
            var hint=Intent(a,5,ArchiveAction.RequestHint,s.round,s.subphase);hint.captureSerial=s.testResetSerial;
            session.Request(5,ArchiveAction.ReturnItem);
            yield return Receipt(a,hint,true,"Personal hint remains valid when a teammate edits the same question");
            Check(session.HintLevel(5,a.Actor)==1&&s.timerDeadline==deadline,"Free hints keep the exact shared deadline");
            var diagnosis=Intent(a,5,ArchiveAction.RequestHint,s.round,s.subphase);diagnosis.captureSerial=s.testResetSerial;
            yield return Receipt(a,diagnosis,true,"Guest explicitly requests a diagnostic hint");
            string text=session.LearningHintText(5,a.Actor,2);
            ArchiveTopicPlayChecks.MoveTo(station,station.CurrentItemPosition(0,q,s));session.Request(5,ArchiveAction.Pick,0);
            Check(text==session.LearningHintText(5,a.Actor,2),"Open hints are stable snapshots rather than a live answer oracle");

            Status="Team timeout attribution";
            int personal=session.State.actors.First(p=>p.actor==a.Actor).mistakes,pressure=session.hunting.Mistakes;
            s.timerDeadline=ArchiveTopicSession.TimerClock-.1;
            yield return Wait(()=>s.timerExpired,"Shared deadline expires exactly once",8);
            yield return .3f;
            Check(session.State.mistakes.Count==1&&session.State.mistakes[0].actor==0&&session.State.mistakes[0].reason=="timeout","Shared timeout is recorded as a team event");
            Check(session.State.actors.First(p=>p.actor==a.Actor).mistakes==personal&&session.hunting.Mistakes==pressure+1,"Team timeout keeps monster pressure without blaming the Book reader");
            Check(s.round==0&&s.subphase==0&&s.timerTimeouts==1,"Timeout keeps unfinished progress and cannot fire twice");
            var old=Intent(a,5,ArchiveAction.Pick,1);
            ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
            session.Request(5,ArchiveAction.AdminResetTopic);
            yield return Receipt(a,old,false,"A delayed pre-reset intent cannot edit the reset challenge");

            Status="Learning profile and checkpoint";
            var copy=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));
            Check(copy.pacingPlayers==2&&copy.matchId==session.State.matchId&&copy.actors.First(p=>p.actor==a.Actor).hintsUsed==2,"Pacing, match identity and hint participation survive checkpoints");
            Check(session.CooperationGuide(5).Contains("participation"),"Participation is distinguished from individual mastery");
            session.learningPace=true;session.Initialize(271828);yield return .2f;
            Check(session.PacingMultiplier==1.875f&&session.hunting.barrierTimeLimitSeconds==180,"Learning profile preserves the three-minute barrier and extra thinking time");
            session.learningPace=false;
            yield return Move(a,session.stations[0],ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
            var fresh=Intent(a,0,ArchiveAction.Start);
            var previous=JsonUtility.FromJson<ArchiveCommand>(JsonUtility.ToJson(fresh));previous.matchId=copy.matchId;
            yield return Receipt(a,previous,false,"A delayed packet from the previous match cannot act in the new match");
            Check(a.receipts[previous.sequence].matchId==copy.matchId,"A stale acknowledgement retains its original match identity");
            a.receipts.Remove(fresh.sequence);
            yield return Receipt(a,fresh,true,"Previous-match rejection cannot poison the same sequence in the fresh match");
            Check(a.receipts[fresh.sequence].matchId==session.State.matchId,"Fresh acknowledgement is explicitly bound to the current match");

            Status="Late join and party-size budgets";
            var b=Connect("Fairness late guest QA");yield return Wait(()=>b.client.State==ClientState.ConnectedToMasterServer,"Third peer connects");
            b.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>b.client.InRoom&&PhotonNetwork.CurrentRoom.PlayerCount==3,"Third peer joins");
            yield return Wait(()=>b.state!=null&&b.state.matchId==session.State.matchId,"Late peer receives pacing checkpoint");
            Check(session.State.pacingPlayers==2,"Late join cannot silently change existing match budgets");
            session.Initialize(161803);yield return .2f;
            Check(session.State.pacingPlayers==3&&Mathf.Abs(session.PacingMultiplier-1.1f)<.001f,"New three-player match receives ten percent extra time");
            Check(ArchiveTopicSession.TeamTimeMultiplier(4)==1,"Four-player profile uses the authored base budgets");
            Check(session.State.learningPace==false&&session.hunting.barrierTimeLimitSeconds==90,"Fresh Normal match restores bounded barrier pacing");
            var c=Connect("Fairness fourth player QA");yield return Wait(()=>c.client.State==ClientState.ConnectedToMasterServer,"Fourth peer connects");
            c.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>c.client.InRoom&&PhotonNetwork.CurrentRoom.PlayerCount==4,"Four-player party joins");
            session.Initialize(141421);yield return .2f;
            Check(session.State.pacingPlayers==4&&session.PacingMultiplier==1,"Actual four-player party receives the authored base budgets");
            station=session.stations[0];ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
            session.Request(0,ArchiveAction.Start);yield return .2f;
            string match=session.State.matchId;double savedDeadline=session.State.stations[0].timerDeadline;
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));
            yield return Wait(()=>!PhotonNetwork.IsMasterClient&&a.client.CurrentRoom.MasterClientId==a.Actor,"Temporary fairness authority change");
            // The rendered Editor is now a real guest. Deliberately withhold its first receipt
            // to exercise the production pending/retry path, then test sender and match checks.
            int packets=0;string firstPacket=null;bool stableRetry=true;ArchiveCommand received=null;
            Action<EventData> observe=ev=>
            {
                if(ev.Code!=ArchiveTopicSession.CommandEvent||ev.Sender!=PhotonNetwork.LocalPlayer.ActorNumber||
                    !(ev.CustomData is object[] data)||data.Length!=2||!Equals(data[0],ArchiveTopicSession.Channel))return;
                string json=(string)data[1];var intent=JsonUtility.FromJson<ArchiveCommand>(json);
                if(intent.action!=ArchiveAction.RequestHint)return;
                if(firstPacket==null)firstPacket=json;else stableRetry&=firstPacket==json;
                received=intent;packets++;
            };
            a.client.EventReceived+=observe;
            Check(session.RequestLearningHint(0)&&session.CommandPending,"Rendered guest visibly tracks an unacknowledged action");
            yield return Wait(()=>packets>=2,"Production guest retries the unacknowledged request",8);
            Check(stableRetry&&received!=null,"Retry preserves the exact sequence, question and intent");
            void Reply(Peer sender,string identity)
            {
                var acknowledgement=new ArchiveCommandReceipt{sequence=received.sequence,matchId=identity,accepted=true,message=""};
                sender.client.OpRaiseEvent(ArchiveTopicSession.CommandReceiptEvent,new object[]{ArchiveTopicSession.Channel,JsonUtility.ToJson(acknowledgement)},
                    new RaiseEventOptions{TargetActors=new[]{PhotonNetwork.LocalPlayer.ActorNumber}},SendOptions.SendReliable);
            }
            Reply(b,match);yield return .3f;
            Check(session.CommandPending,"A non-authoritative peer cannot forge a success acknowledgement");
            Reply(a,copy.matchId);yield return .3f;
            Check(session.CommandPending,"A receipt from a previous match cannot clear a current pending action");
            Reply(a,match);yield return Wait(()=>!session.CommandPending,"Authoritative current-match receipt reaches the guest",8);
            Check(!session.CommandPending,"Valid master acknowledgement clears the guest's pending UI");
            Check(session.State.mistakes.Count==0,"Transport delay and rejected acknowledgements add no academic mistake");
            a.client.EventReceived-=observe;
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(PhotonNetwork.LocalPlayer.ActorNumber));
            yield return Wait(()=>PhotonNetwork.IsMasterClient,"Editor regains fairness authority");yield return .3f;
            Check(session.State.matchId==match&&session.State.pacingPlayers==4&&session.State.stations[0].timerDeadline==savedDeadline,"Master handoff preserves match identity, pacing and the exact active deadline");
            yield return CoopFlow(a);
        }
    }
}
