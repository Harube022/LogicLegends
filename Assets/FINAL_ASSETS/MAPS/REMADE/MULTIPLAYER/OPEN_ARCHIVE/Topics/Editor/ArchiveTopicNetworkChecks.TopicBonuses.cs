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
        static bool topicBonusesOnly;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Distinct Topic Bonuses (Live Photon)")]
        public static void BeginTopicBonuses()=>StartChecks(false,topicBonuses:true);
        static ArchiveCommand BonusPacket(Peer peer,int topic,int action,int first=-1,int second=-1)
        {
            var c=session.Relay(topic);
            var cmd=Command(peer,topic,ArchiveAction.BonusInteract,action,first);
            cmd.c=second;cmd.matchId=session.State.matchId;cmd.captureSerial=c.serial;cmd.resetSerial=session.State.stations[topic].testResetSerial;cmd.workStamp=c.assignmentRevision.ToString();return cmd;
        }
        static IEnumerator BonusRemote(Peer peer,int topic,int action,Vector3 point,int first=-1,int second=-1)
        {
            yield return Move(peer,session.stations[topic],point);
            if(action==2&&session.BonusActivity(topic).mechanic==ArchiveBonusMechanic.GraphRoute)
                yield return BonusPeerWalk(peer,session.stations[topic],point,point);
            yield return Receipt(peer,BonusPacket(peer,topic,action,first,second),true,"T"+(topic+1)+" remote bonus action "+action+" / "+first);
        }
        static IEnumerator NativeTruthMilestone()
        {
            int topic=1;var station=session.stations[topic];var s=session.State.stations[topic];
            if(s.phase==0){ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(topic,ArchiveAction.Start);yield return .1f;}
            var q=session.Question(topic);int round=s.round;
            for(int slot=0;slot<4;slot++)
            {
                int item=q.expected[slot];ArchiveTopicPlayChecks.MoveTo(station,station.CurrentItemPosition(item,q,s));session.Request(topic,ArchiveAction.Pick,item);yield return .035f;
                ArchiveTopicPlayChecks.MoveTo(station,station.SlotPosition(slot,q));session.Request(topic,ArchiveAction.Place,slot);yield return .035f;
            }
            ArchiveTopicPlayChecks.MoveTo(station,Vector3.zero);session.Request(topic,ArchiveAction.Submit);yield return .1f;
            Check(s.round==round+1&&session.State.solved.Any(v=>v.topic==topic&&v.contributors.Contains(PhotonNetwork.LocalPlayer.ActorNumber)),"Actual native main Truth Table answer establishes the host's contribution");
        }
        static IEnumerator BonusPeerWalk(Peer peer,ArchiveTopicStation station,Vector3 from,Vector3 to)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(from,to)/.18f));
            for(int i=1;i<=steps;i++)
            {
                Vector3 position=Position(station,Vector3.Lerp(from,to,i/(float)steps));
                var player=session.ActorPlayer(peer.Actor);var cc=player.GetComponent<CharacterController>();
                var hits=Physics.RaycastAll(position+Vector3.up*5,Vector3.down,12,~(1<<7),QueryTriggerInteraction.Ignore).Where(h=>h.normal.y>.7f&&h.collider.GetComponentInParent<Player>()==null).OrderBy(h=>h.distance).ToArray();
                if(hits.Length>0)position.y=hits[0].point.y-(cc.center.y-cc.height*.5f)*Mathf.Abs(player.transform.lossyScale.y)+cc.skinWidth+.04f;
                peer.client.OpRaiseEvent(201,new object[]{PhotonNetwork.ServerTimestamp,null,new object[]{peer.viewId,false,null,position,station.transform.rotation,true,false,false,10f}},new RaiseEventOptions{Receivers=ReceiverGroup.Others},SendOptions.SendReliable);
                yield return .065f;
            }
            yield return .25f;
        }
        static IEnumerator SolveRemoteBonus(Peer peer,int topic,ArchiveCoopActivity a,ArchiveCoopActivityView view)
        {
            var c=session.Relay(topic);
            if(a.items.Length>0&&a.mechanic!=ArchiveBonusMechanic.InductionSequence)
            {
                for(int i=0;i<a.expected.Length;i++)
                {
                    int item=a.regionPlacement?i:a.expected[i],slot=a.regionPlacement?a.expected[i]:i;
                    if(c.bonusValues.Length>i&&c.bonusValues[i]==a.expected[i])continue;
                    yield return BonusRemote(peer,topic,0,view.CurrentItemPosition(item),item);yield return BonusRemote(peer,topic,1,view.SlotPosition(slot),slot);
                }
            }
            else if(a.mechanic==ArchiveBonusMechanic.InductionSequence)
                foreach(int item in a.expected)yield return BonusRemote(peer,topic,2,view.ItemPosition(item),item);
            else if(a.mechanic==ArchiveBonusMechanic.GraphRoute)
            {
                int[] route=ArchiveCoopActivityChecks.FindRoute(a);
                yield return BonusRemote(peer,topic,2,view.NodePosition(a.startNode),a.startNode);
                for(int i=1;i<route.Length;i++)yield return BonusPeerWalk(peer,view.station,view.NodePosition(route[i-1]),view.NodePosition(route[i]));
                Check(c.bonusRoute.SequenceEqual(route),"Remote graph runner traverses the actual directed junction sequence");
            }
            else
            {
                foreach(int link in c.bonusLinks.Except(a.requiredLinks).ToArray())
                {yield return BonusRemote(peer,topic,3,view.NodePosition(link/100),link/100);yield return BonusRemote(peer,topic,3,view.NodePosition(link%100),link%100);yield return BonusRemote(peer,topic,5,view.NodePosition(link%100),link/100,link%100);}
                foreach(int link in a.requiredLinks.Except(c.bonusLinks).ToArray())
                {yield return BonusRemote(peer,topic,3,view.NodePosition(link/100),link/100);yield return BonusRemote(peer,topic,3,view.NodePosition(link%100),link%100);yield return BonusRemote(peer,topic,4,view.NodePosition(link%100),link/100,link%100);}
            }
        }
        static IEnumerator TopicBonusesFlow(Peer a,string room)
        {
            report.scope="Live private Photon room: owned rendered host and independent Realtime support peer. T2 uses a genuine accepted native main-table answer. T8/T9 use labelled accepted-main-work checkpoint fixtures for isolated eligibility, not claimed played academic completions. Tests actual bonus transport, independent carries, physical replicated graph traversal, stale submissions, late join and authority transfer. Physical Android and remote rendered controls remain unverified.";
            session.hunting.enabled=false;
            Check(session.stations.Skip(1).All(s=>s.GetComponents<ArchiveCoopActivityView>().Length==1),"Saved T2–T10 each have one distinct bonus interaction view");
            foreach(int topic in new[]{1,7,8})
            {
                Status="Live distinct bonus T"+(topic+1);session.Initialize(314159+topic);
                var station=session.stations[topic];var view=station.GetComponent<ArchiveCoopActivityView>();var activity=ArchiveCoopActivity.Build(topic,session.State.seed);
                yield return Move(a,station,view.SubmitPosition);
                yield return Receipt(a,BonusPacket(a,topic,6),false,"Bonus cannot submit before main Book activation");
                ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(topic,ArchiveAction.Start);yield return .15f;
                Check(session.RelayAvailable(topic),"Real Book opens the correct distinct bonus");
                var c=session.Relay(topic);var main=session.State.stations[topic];double deadline=main.timerDeadline;
                if(topic==1)
                {
                    ArchiveTopicPlayChecks.MoveTo(station,view.ItemPosition(0));session.RequestBonus(topic,0,0);yield return .05f;
                    yield return BonusRemote(a,topic,0,view.ItemPosition(1),1);
                    Check(c.bonusCarries.Count==2&&c.bonusCarries.Select(v=>v.actor).Distinct().Count()==2,"Host and support can carry distinct bonus tiles simultaneously");
                    ArchiveTopicPlayChecks.MoveTo(station,view.SlotPosition(0));session.RequestBonus(topic,1,0);yield return .05f;
                    yield return BonusRemote(a,topic,1,view.SlotPosition(1),1);
                    Check(c.bonusCarries.Count==0,"Each player places only their own bonus tile");
                    yield return NativeTruthMilestone();
                }
                else
                {
                    // Explicit isolated eligibility checkpoint; never reported as played main content.
                    session.State.solved.Add(new ArchiveSolvedMilestone{topic=topic,round=-1,subphase=-1,contributors=new[]{PhotonNetwork.LocalPlayer.ActorNumber}});
                }
                string mainBefore=JsonUtility.ToJson(main);
                yield return SolveRemoteBonus(a,topic,activity,view);
                if(topic!=8)
                {
                    yield return Move(a,station,view.SubmitPosition);var stale=BonusPacket(a,topic,6);
                    if(topic==1)
                    {
                        int wrong=1-activity.expected[0];ArchiveTopicPlayChecks.MoveTo(station,view.ItemPosition(wrong));session.RequestBonus(topic,0,wrong);yield return .05f;
                        yield return Receipt(a,stale,false,"A bonus confirmation cannot use a pre-edit work revision");
                        ArchiveTopicPlayChecks.MoveTo(station,view.SlotPosition(0));session.RequestBonus(topic,1,0);yield return .05f;
                        yield return SolveRemoteBonus(a,topic,activity,view);
                    }
                }
                if(topic==8)
                {
                    Check(c.bonusRouteValid&&c.bonusAtNode,"Graph completion requires continuous supported walking to the final node");
                    yield return Receipt(a,BonusPacket(a,topic,6),true,"Runner submits at the reached goal node");
                }
                else yield return BonusRemote(a,topic,6,view.SubmitPosition);
                yield return .1f;
                Check(c.completed&&session.State.completedRelays.Count==1,"Transported distinct bonus completes once");
                Check(JsonUtility.ToJson(main)==mainBefore&&main.timerDeadline==deadline,"Optional teamwork never changes academic placements, rounds or exact deadline");
                Check(session.State.quality.Count(v=>v.key=="coop:"+topic)==2,"Existing ledger credits exactly the accepted main solver and bonus support");
                yield return Wait(()=>a.state!=null&&a.state.topicRelays.Count==10&&a.state.topicRelays[topic].completed,"Support receives completed mechanic snapshot",8);
                int credits=session.State.quality.Count;yield return Receipt(a,BonusPacket(a,topic,6),false,"A completed bonus rejects repeat reward requests");
                Check(session.State.quality.Count==credits,"No bonus credit can be farmed twice");
            }
            Status="Distinct bonus late join and host transfer";
            var b=Connect("Distinct bonus late guest QA");yield return Wait(()=>b.client.State==ClientState.ConnectedToMasterServer,"Late bonus client connected");
            b.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>b.client.InRoom&&b.state!=null&&b.state.topicRelays.Count==10,"Late bonus checkpoint received");
            Check(b.state.topicRelays[8].completed&&b.state.topicRelays[8].bonusRoute.SequenceEqual(session.Relay(8).bonusRoute),"Late join restores completed graph route rather than re-awarding it");
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));yield return Wait(()=>!PhotonNetwork.IsMasterClient,"Bonus authority leaves Editor");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(PhotonNetwork.LocalPlayer.ActorNumber));yield return Wait(()=>PhotonNetwork.IsMasterClient,"Bonus authority returns to Editor");yield return .2f;
            Check(session.Relay(8).completed&&session.State.completedRelays.Count==1,"Authority transfer preserves one earned completion and physical route");
            Status="Distinct bonus completion cutoff";session.Initialize(9137);var t=session.stations[7];var v=t.GetComponent<ArchiveCoopActivityView>();
            ArchiveTopicPlayChecks.MoveTo(t,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(7,ArchiveAction.Start);yield return .1f;
            yield return Move(a,t,v.SubmitPosition);var late=BonusPacket(a,7,6);
            Check(session.RequestTopicAdmin(7,session.State.stations[7].revision,true),"Authorized Admin closes isolated active-topic fixture");
            yield return Receipt(a,late,false,"In-flight bonus submit is rejected immediately after main completion");
            Check(session.Relay(7).closed&&!session.Relay(7).completed&&session.State.completedRelays.Count==0&&session.CanStartTopic(8),"Expired optional work grants no credit and never gates another topic");
            Check(session.State.mistakes.Count==0,"Bonus actions and rejected transport never charge monster mistakes");
            Status="Distinct topic bonuses passed";
        }
    }
}
