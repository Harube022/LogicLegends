using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Graph;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        readonly Dictionary<string,ArchiveCoopActivity> bonusActivities=new Dictionary<string,ArchiveCoopActivity>();
        public ArchiveCarry BonusCarry(int actor,int topic)=>Relay(topic)?.bonusCarries?.FirstOrDefault(c=>c.actor==actor);
        static void RefreshBonusCarrySummary(ArchiveT1CoopState state){var carry=state.bonusCarries.FirstOrDefault();state.bonusHeld=carry?.item??-1;state.bonusHolder=carry?.actor??0;}
        public ArchiveCoopActivity BonusActivity(int topic)
        {
            if(!Ready||topic<1||topic>9)return null;
            string key=State.seed+":"+topic+":"+State.stations[topic].testResetSerial;
            if(!bonusActivities.TryGetValue(key,out var activity))
            {
                if(bonusActivities.Count>30)bonusActivities.Clear();
                bonusActivities[key]=activity=ArchiveCoopActivity.Build(topic,State.seed,State.stations[topic].testResetSerial);
            }
            return activity;
        }
        void PrepareBonusActivity(int topic)
        {
            var state=Relay(topic);var activity=BonusActivity(topic);if(state==null||activity==null)return;
            state.bonusValues=activity.initialValues.ToArray();state.bonusLinks=activity.initialLinks.ToList();state.bonusRoute.Clear();state.bonusCredits.Clear();
            state.bonusCarries.Clear();
            state.bonusHeld=-1;state.bonusHolder=0;state.bonusFirst=state.bonusSecond=-1;state.bonusFirstActor=0;
            state.bonusRouteActive=state.bonusRouteValid=state.bonusSampleValid=false;state.bonusRouteActor=0;state.bonusEdge=-1;
            state.feedback="Optional "+activity.title+" is open. "+activity.instructions;
        }
        public void RequestBonus(int topic,int action,int a=-1,int b=-1)
        {
            var relay=Relay(topic);if(relay==null||!BonusCanUse(LocalActor,topic,action,a,b))return;
            SendLearningCommand(new ArchiveCommand{topic=topic,action=ArchiveAction.BonusInteract,a=action,b=a,c=b,
                captureSerial=relay.serial,resetSerial=State.stations[topic].testResetSerial,workStamp=relay.assignmentRevision.ToString()});
        }
        public bool BonusCanUse(int actor,int topic,int action,int a=-1,int b=-1)
        {
            if(topic<1||topic>9||action<0||action>7||!RelayAvailable(topic)||!CanAct(actor))return false;
            var relay=Relay(topic);var activity=BonusActivity(topic);var view=stations[topic].GetComponent<ArchiveCoopActivityView>();
            if(activity==null||view==null)return false;
            bool NearPoint(Vector3 point,float distance=2.4f)=>Near(actor,topic,point,distance);
            bool HasIndex(int index,int count)=>index>=0&&index<count;
            bool carries=activity.mechanic==ArchiveBonusMechanic.TruthTiles||activity.mechanic==ArchiveBonusMechanic.InferenceChain||activity.mechanic==ArchiveBonusMechanic.ProofSort||activity.mechanic==ArchiveBonusMechanic.ProofRepair||activity.mechanic==ArchiveBonusMechanic.SetDelivery;
            switch(action)
            {
                case 0:
                    if(!carries||!HasIndex(a,activity.items.Length)||BonusCarry(actor,topic)!=null||!activity.reusableItems&&relay.bonusCarries.Any(c=>c.item==a))return false;
                    int slot=activity.regionPlacement?a:Array.IndexOf(relay.bonusValues,a);
                    Vector3 at=slot>=0&&slot<relay.bonusValues.Length&&relay.bonusValues[slot]>=0?view.SlotPosition(activity.regionPlacement?relay.bonusValues[slot]:slot):view.ItemPosition(a);
                    // Reusable tiles always remain available at their supply.
                    if(activity.reusableItems)at=view.ItemPosition(a);
                    return NearPoint(at);
                case 1:
                    if(!carries||BonusCarry(actor,topic)==null||!HasIndex(a,activity.slots.Length))return false;
                    return NearPoint(view.SlotPosition(a));
                case 2:
                    if(activity.mechanic==ArchiveBonusMechanic.InductionSequence)return HasIndex(a,activity.items.Length)&&NearPoint(view.ItemPosition(a));
                    if(activity.mechanic==ArchiveBonusMechanic.GraphRoute)return a==activity.startNode&&NearPoint(view.NodePosition(a),.85f)&&BonusSupported(actor)&&
                        (!relay.bonusRouteActive||relay.bonusRouteActor==actor||!CanAct(relay.bonusRouteActor));
                    return false;
                case 3:
                    return (activity.mechanic==ArchiveBonusMechanic.MappingLinks||activity.mechanic==ArchiveBonusMechanic.TreeRepair)&&HasIndex(a,activity.nodes.Length)&&NearPoint(view.NodePosition(a));
                case 4:case 5:
                    if((activity.mechanic!=ArchiveBonusMechanic.MappingLinks&&activity.mechanic!=ArchiveBonusMechanic.TreeRepair)||!HasIndex(a,activity.nodes.Length)||!HasIndex(b,activity.nodes.Length)||a==b||
                        relay.bonusFirst!=a||relay.bonusSecond!=b||relay.bonusFirstActor!=actor||!NearPoint(view.NodePosition(b)))return false;
                    if(activity.mechanic==ArchiveBonusMechanic.MappingLinks&&(a>=3||b<3))return false;
                    int edge=a*100+b;
                    return action==4?!relay.bonusLinks.Contains(edge)&&relay.bonusLinks.Count<12:relay.bonusLinks.Contains(edge);
                case 6:
                    if(relay.bonusCarries.Count>0||!NearPoint(view.SubmitPosition))return false;
                    if(activity.mechanic==ArchiveBonusMechanic.GraphRoute)return relay.bonusRouteActive&&relay.bonusRouteValid&&relay.bonusAtNode&&relay.bonusRouteActor==actor&&relay.bonusRoute.LastOrDefault()==activity.goalNode;
                    return true;
                case 7:return NearPoint(view.SubmitPosition)&&(!relay.bonusRouteActive||relay.bonusRouteActor==actor||!CanAct(relay.bonusRouteActor));
                default:return false;
            }
        }
        bool AcceptBonusInteraction(int actor,ArchiveCommand command)
        {
            int topic=command.topic;var relay=Relay(topic);
            if(topic<1||topic>9||relay==null||!RelayAvailable(topic)||command.matchId!=State.matchId||command.captureSerial!=relay.serial||
                command.resetSerial!=State.stations[topic].testResetSerial||!int.TryParse(command.workStamp,out int seenRevision)||seenRevision<0||seenRevision>relay.assignmentRevision||
                (command.a!=0&&seenRevision!=relay.assignmentRevision)||
                !FreshLearningSequence(actor,command)||!BonusCanUse(actor,topic,command.a,command.b,command.c))return false;
            var activity=BonusActivity(topic);RememberLearningSequence(actor,command);RefreshRelayPolicy(topic);
            int a=command.b,b=command.c,action=command.a;
            switch(action)
            {
                case 0:
                    relay.bonusCarries.Add(new ArchiveCarry{actor=actor,item=a});RefreshBonusCarrySummary(relay);
                    if(!activity.reusableItems)
                    {
                        int slot=activity.regionPlacement?a:Array.IndexOf(relay.bonusValues,a);
                        if(slot>=0&&slot<relay.bonusValues.Length)relay.bonusValues[slot]=-1;
                    }
                    relay.feedback="Carrying "+activity.items[a]+". Deliver it to a marked socket or tray.";
                    break;
                case 1:
                    int item=BonusCarry(actor,topic).item;
                    if(activity.regionPlacement)relay.bonusValues[item]=a;
                    else relay.bonusValues[a]=item;
                    CreditBonusWork(relay,actor,"p:"+(activity.regionPlacement?item:a)+":"+(activity.regionPlacement?a:item));
                    relay.bonusCarries.RemoveAll(c=>c.actor==actor);RefreshBonusCarrySummary(relay);relay.feedback="Placement shared. Your team may lift and correct it before checking.";
                    break;
                case 2:
                    if(activity.mechanic==ArchiveBonusMechanic.GraphRoute)
                    {
                        var trace=new ArchiveStationState();ArchiveGraphTraversal.Begin(trace,BonusGraph(topic),actor,BonusFeet(actor,topic),Clock);WriteBonusTrace(relay,trace);
                        relay.bonusRouteActive=true;relay.feedback="Route started. Walk the actual directed paths; jumping or teleporting does not record an edge.";
                    }
                    else
                    {
                        if(relay.bonusRoute.Count>=activity.items.Length)relay.bonusRoute.Clear();
                        relay.bonusRoute.Add(a);CreditBonusWork(relay,actor,"sequence:"+(relay.bonusRoute.Count-1));
                        relay.feedback="Sequence step "+relay.bonusRoute.Count+" recorded. Check the full reasoning when ready.";
                    }
                    break;
                case 3:
                    if(relay.bonusFirstActor!=actor||relay.bonusFirst<0||relay.bonusSecond>=0){relay.bonusFirst=a;relay.bonusSecond=-1;relay.bonusFirstActor=actor;}
                    else if(relay.bonusFirst!=a)relay.bonusSecond=a;
                    relay.feedback="Selected "+activity.nodes[relay.bonusFirst]+(relay.bonusSecond<0?". Choose the other endpoint.":" → "+activity.nodes[relay.bonusSecond]+". Connect or remove this link.");
                    break;
                case 4:case 5:
                    if(action==4)relay.bonusLinks.Add(a*100+b);else relay.bonusLinks.Remove(a*100+b);
                    CreditBonusWork(relay,actor,"edge:"+(a*100+b)+":"+(action==4?1:0));relay.bonusFirst=relay.bonusSecond=-1;relay.bonusFirstActor=0;
                    relay.feedback=action==4?"Connection shared. Check the whole relation or parent structure.":"Link removed. Other correct connections remain.";
                    break;
                case 6:
                    if(!activity.Validate(relay.bonusValues,relay.bonusLinks,relay.bonusRoute))
                    {
                        relay.attempts++;relay.explanation=activity.explanation;relay.feedback="Review this separate bonus example. Your work is kept; no monster mistake is added.";
                    }
                    else if(!BonusTeamworkReady(topic,actor))relay.feedback="Correct arrangement. A different teammate must contribute accepted main-topic work before the team bonus can be recorded. Check again while this topic is active.";
                    else CompleteBonusActivity(topic,actor);
                    break;
                case 7:
                    relay.serial++;PrepareBonusActivity(topic);relay.feedback="Optional work reset. Main-topic progress and its timer are kept.";
                    break;
            }
            relay.assignmentRevision++;Commit();return true;
        }
        static void CreditBonusWork(ArchiveT1CoopState relay,int actor,string key)
        {
            string prefix=key.Substring(0,key.LastIndexOf(':')+1);
            if(key.StartsWith("p:")||key.StartsWith("edge:"))relay.bonusCredits.RemoveAll(v=>v.key.StartsWith(prefix,StringComparison.Ordinal));else relay.bonusCredits.RemoveAll(v=>v.key==key);
            relay.bonusCredits.Add(new ArchiveWorkCredit{actor=actor,key=key});
            if(relay.bonusCredits.Count>32)relay.bonusCredits.RemoveAt(0);
        }
        bool BonusTeamworkReady(int topic,int submitter)
        {
            var relay=Relay(topic);var main=RelayMainContributors(topic);if(main.Length==0)return false;
            if(relay.reducedTeam)return true;
            var team=RelayTeamActors();
            return main.Any(m=>team.Contains(m)&&relay.bonusCredits.Where(c=>BonusCreditUsed(relay,c)).Select(c=>c.actor).Append(submitter).Any(a=>team.Contains(a)&&a!=m));
        }
        static bool BonusCreditUsed(ArchiveT1CoopState relay,ArchiveWorkCredit credit)
        {
            var parts=credit.key.Split(':');
            if(parts.Length==3&&int.TryParse(parts[1],out int index)&&int.TryParse(parts[2],out int value))
            {
                if(parts[0]=="p")return index>=0&&index<relay.bonusValues.Length&&relay.bonusValues[index]==value;
                if(parts[0]=="edge")return relay.bonusLinks.Contains(index)==(value==1);
            }
            return credit.key.StartsWith("sequence:",StringComparison.Ordinal)||credit.key.StartsWith("route:",StringComparison.Ordinal);
        }
        void CompleteBonusActivity(int topic,int actor)
        {
            var relay=Relay(topic);if(!RelayAvailable(topic)||relay.completed)return;
            relay.completed=relay.open=true;relay.openedAt=Clock;relay.explanation=BonusActivity(topic).explanation;relay.bonusRouteActive=false;
            relay.feedback=relay.reducedTeam?"Optional practice complete (one player).":"Optional team bonus complete! Main-topic progress continues.";
            if(!relay.reducedTeam)
            {
                var team=RelayTeamActors();
                foreach(int member in relay.bonusCredits.Where(c=>BonusCreditUsed(relay,c)).Select(v=>v.actor).Append(actor).Concat(RelayMainContributors(topic)).Where(team.Contains).Distinct())AddQualityCredit(member,"coop:"+topic);
            }
            if(!State.completedRelays.Any(c=>c.topic==topic))State.completedRelays.Add(JsonUtility.FromJson<ArchiveT1CoopState>(JsonUtility.ToJson(relay)));
        }
        bool BonusSupported(int actor)
        {
            var player=ActorPlayer(actor);var controller=player==null?null:player.GetComponent<CharacterController>();if(controller==null)return false;
            Vector3 feet=LogicLegends.OpenArchive.Water.OpenArchiveWater.Feet(controller);
            return Physics.RaycastAll(feet+Vector3.up*.6f,Vector3.down,1.2f,~(1<<7),QueryTriggerInteraction.Ignore).Any(h=>h.normal.y>.7f&&h.collider.GetComponentInParent<Player>()==null);
        }
        Vector3 BonusFeet(int actor,int topic)
        {
            var player=ActorPlayer(actor);var controller=player.GetComponent<CharacterController>();
            return stations[topic].transform.InverseTransformPoint(LogicLegends.OpenArchive.Water.OpenArchiveWater.Feet(controller));
        }
        ArchiveQuestion BonusGraph(int topic)
        {
            var activity=BonusActivity(topic);var view=stations[topic].GetComponent<ArchiveCoopActivityView>();
            var q=new ArchiveQuestion{nodeIds=Enumerable.Range(0,activity.nodes.Length).Select(i=>i.ToString()).ToArray(),graph=new GraphChallengeData{type=GraphChallengeType.DirectedPath,start=activity.startNode.ToString(),destination=activity.goalNode.ToString()}};
            q.nodes=Enumerable.Range(0,activity.nodes.Length).Select(i=>{var p=view.NodePosition(i);return new Vector2(p.x-ArchiveTopicStation.GraphOffset.x,p.z-ArchiveTopicStation.GraphOffset.y);}).ToArray();
            q.graph.vertices=q.nodeIds.Select((id,i)=>new GraphVertexData(id,q.nodes[i].x,q.nodes[i].y)).ToList();
            q.graph.edges=activity.initialLinks.Select(e=>new GraphEdgeData((e/100).ToString(),(e%100).ToString(),true)).ToList();return q;
        }
        static ArchiveStationState ReadBonusTrace(ArchiveT1CoopState relay)=>new ArchiveStationState{route=relay.bonusRoute,routeActor=relay.bonusRouteActor,continuous=relay.bonusRouteValid,routeAtVertex=relay.bonusAtNode,routeEdge=relay.bonusEdge,routeCrossedMiddle=relay.bonusCrossedMiddle,lastRoutePosition=relay.bonusLastPosition,routeSampleAt=relay.bonusLastAt,routeSampleValid=relay.bonusSampleValid};
        static void WriteBonusTrace(ArchiveT1CoopState relay,ArchiveStationState trace)
        {
            relay.bonusRoute=trace.route;relay.bonusRouteActor=trace.routeActor;relay.bonusRouteValid=trace.continuous;relay.bonusAtNode=trace.routeAtVertex;relay.bonusEdge=trace.routeEdge;
            relay.bonusCrossedMiddle=trace.routeCrossedMiddle;relay.bonusLastPosition=trace.lastRoutePosition;relay.bonusLastAt=trace.routeSampleAt;relay.bonusSampleValid=trace.routeSampleValid;
        }
        void TickBonusActivity(int topic)
        {
            var relay=Relay(topic);bool changed=RefreshRelayPolicy(topic);
            if(relay.bonusCarries.RemoveAll(c=>!CanAct(c.actor)||!Near(c.actor,topic,Vector3.zero,20))>0){RefreshBonusCarrySummary(relay);relay.feedback="Carrier unavailable. The item returned to its supply.";relay.assignmentRevision++;changed=true;}
            if(relay.bonusRouteActive)
            {
                var trace=ReadBonusTrace(relay);int count=trace.route.Count;bool valid=trace.continuous;
                if(!CanAct(relay.bonusRouteActor)||!BonusSupported(relay.bonusRouteActor))ArchiveGraphTraversal.Invalidate(trace,"Walking interrupted. Return to the start and retry.");
                else
                {
                    var player=ActorPlayer(relay.bonusRouteActor);float scale=Mathf.Max(.01f,stations[topic].transform.lossyScale.x);
                    ArchiveGraphTraversal.Sample(trace,BonusGraph(topic),BonusFeet(relay.bonusRouteActor,topic),Clock,.12f,player.moveSpeed*2.1f/scale);
                }
                WriteBonusTrace(relay,trace);
                if(!trace.continuous){relay.bonusRouteActive=false;relay.feedback=trace.routeReason;relay.assignmentRevision++;changed=true;}
                else if(count!=trace.route.Count){CreditBonusWork(relay,relay.bonusRouteActor,"route:"+trace.route.Count);relay.assignmentRevision++;changed=true;}
                if(Clock>=nextRelayCheckpoint[topic]){nextRelayCheckpoint[topic]=Clock+.5;changed=true;}
            }
            if(changed)Commit();
        }
    }
}
