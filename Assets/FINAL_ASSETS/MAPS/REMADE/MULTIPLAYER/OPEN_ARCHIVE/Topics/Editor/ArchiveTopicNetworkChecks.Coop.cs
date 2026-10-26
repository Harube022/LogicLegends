using System.Collections;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;
namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveTopicNetworkChecks
    {
        static IEnumerator RelayWalk(Peer peer,ArchiveT1CoopTrial trial,Vector3 from,Vector3 to)
        {
            var station=trial.station;
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(from,to)/.4f));
            for(int i=1;i<=steps;i++)
            {
                Vector3 position=Position(station,trial.LanePoint(Vector3.Lerp(from,to,i/(float)steps)));
                var player=session.ActorPlayer(peer.Actor);var capsule=player.GetComponent<CharacterController>();
                if(Physics.Raycast(position+Vector3.up*5,Vector3.down,out var floor,12,~(1<<7),QueryTriggerInteraction.Ignore))
                    position.y=floor.point.y-(capsule.center.y-capsule.height*.5f)*Mathf.Abs(player.transform.lossyScale.y)+capsule.skinWidth+.04f;
                peer.client.OpRaiseEvent(201,new object[]{PhotonNetwork.ServerTimestamp,null,new object[]{peer.viewId,false,null,position,station.transform.rotation,true,false,false,12f}},
                    new RaiseEventOptions{Receivers=ReceiverGroup.Others},SendOptions.SendReliable);
                yield return .09f;
            }
            yield return .35f;
        }
        static IEnumerator SolveRelayAcademics()
        {
            var station=session.stations[0];
            for(int round=0;round<ArchiveTopicCatalog.RoundCount(0);round++)
            {
                ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
                session.Request(0,ArchiveAction.Start);yield return .1f;
                Check(session.State.stations[0].phase==1,"Native T1 Book starts academic challenge "+(round+1));
                var q=session.Question(0);
                ArchiveTopicPlayChecks.MoveTo(station,station.ItemPosition(q.correctChoice,q));
                session.Request(0,ArchiveAction.BeginChoice,q.correctChoice);
                int accepted=round+1;yield return Wait(()=>session.State.stations[0].round==accepted,"Real T1 answer "+accepted+" accepted",8);
                if(round<ArchiveTopicCatalog.RoundCount(0)-1)
                    Check(session.State.coop.round<0&&session.State.stations[0].phase==0,"Academic answer "+accepted+" does not create an extra relay");
            }
        }
        static void RelayAssignment(ArchiveT1CoopState relay,bool result,out bool p,out bool q)
        {
            p=q=false;
            foreach(bool a in new[]{false,true})foreach(bool b in new[]{false,true})
                if(ArchiveTopicSession.EvaluateConnective(relay.connective,a,b)==result){p=a;q=b;return;}
            throw new System.InvalidOperationException("No truth assignment for relay target.");
        }
        static ArchiveCommand RelaySet(Peer peer,int input,bool value)
        {
            var cmd=Command(peer,0,ArchiveAction.SetT1Input,input,value?1:0);cmd.captureSerial=session.State.coop.serial;return cmd;
        }
        static ArchiveCommand RelayConfirm(Peer peer,int input)
        {
            var cmd=Command(peer,0,ArchiveAction.ConfirmT1Input,input,session.State.coop.assignmentRevision);
            cmd.captureSerial=session.State.coop.serial;return cmd;
        }
        // Existing multi-topic suites call this only after their five actual T1 answers.
        static IEnumerator CompleteRequiredRelay(Peer first,Peer second)
        {
            if(session.Ready&&session.State.stations[0].phase==2)yield break; // Optional practice does not block the topic.
            if(first==null||second==null||first.Actor==second.Actor||!session.T1RelayPending)
                throw new System.InvalidOperationException("A pending relay and two distinct Photon peers are required.");
            var station=session.stations[0];var trial=station.GetComponent<ArchiveT1CoopTrial>();var c=session.State.coop;
            RelayAssignment(c,c.target,out bool p,out bool q);
            yield return Move(first,station,trial.SwitchPosition(0));
            yield return Move(second,station,trial.SwitchPosition(1));
            yield return Receipt(first,RelaySet(first,0,p),true,"First teammate sets required P input");
            yield return Receipt(second,RelaySet(second,1,q),true,"Second teammate sets required Q input");
            yield return Receipt(first,RelayConfirm(first,0),true,"First teammate confirms its own P input");
            yield return Receipt(second,RelayConfirm(second,1),true,"Second teammate confirms its own Q input");
            yield return Wait(()=>c.open,"Required shared relay gate opens",8);
            var entry=new Vector3(-7,0,12.9f);var exit=new Vector3(7,0,12.9f);
            yield return Move(first,station,trial.LanePoint(entry));yield return .3f;
            yield return RelayWalk(first,trial,entry,exit);
            yield return Wait(()=>c.completed&&session.State.stations[0].phase==2,"Required shared relay crossing completes T1",8);
        }
        static IEnumerator CoopFlow(Peer a)=>OptionalRelayFlow(a,PhotonNetwork.CurrentRoom.Name);
    }
}
