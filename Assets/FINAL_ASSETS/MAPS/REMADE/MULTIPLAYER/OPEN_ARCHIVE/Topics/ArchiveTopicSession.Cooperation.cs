using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        public const int T1RelayPhase=3;
        // Retained for older editor tooling. Optional relays never reserve topic progress.
        public bool T1RelayPending=>false;
        public static bool T1RelayTargetForSeed(int seed,int resetSerial=0)=>(unchecked(seed*397^resetSerial*17)&1)!=0;
        void NormalizeCooperation()
        {
            if(State.quality==null)State.quality=new List<ArchiveQualityCredit>();
            if(State.solved==null)State.solved=new List<ArchiveSolvedMilestone>();
            if(State.coop==null)State.coop=new ArchiveT1CoopState();
            if(State.completedRelays==null)State.completedRelays=new List<ArchiveT1CoopState>();
            if(State.topicRelays==null)State.topicRelays=new List<ArchiveT1CoopState>();
            while(State.topicRelays.Count<10)State.topicRelays.Add(new ArchiveT1CoopState{topic=State.topicRelays.Count,policyVersion=3});
            for(int i=0;i<10;i++)
            {
                var relay=State.topicRelays[i];
                if(relay==null||relay.policyVersion<3)State.topicRelays[i]=relay=new ArchiveT1CoopState{topic=i,policyVersion=3};
                relay.topic=i;
                if(relay.traversals==null)relay.traversals=new List<ArchiveT1Traversal>();
                if(relay.crossed==null)relay.crossed=new List<int>();
                if(relay.bonusValues==null)relay.bonusValues=Array.Empty<int>();
                if(relay.bonusLinks==null)relay.bonusLinks=new List<int>();
                if(relay.bonusRoute==null)relay.bonusRoute=new List<int>();
                if(relay.bonusCredits==null)relay.bonusCredits=new List<ArchiveWorkCredit>();
                if(relay.bonusCarries==null)relay.bonusCarries=new List<ArchiveCarry>();
            }
            State.coop=State.topicRelays[0];
            foreach(var s in State.stations)if(s.workCredits==null)s.workCredits=new List<ArchiveWorkCredit>();
        }
        void RecordWorkCredit(int actor,ArchiveCommand cmd,ArchiveStationState s,ArchiveQuestion q,
            int previousSelection,int previousFlags,int placedItem,int previousPlacement)
        {
            if(s.workCredits==null)s.workCredits=new List<ArchiveWorkCredit>();
            var crystalOwners=s.workCredits.ToArray();
            string key=null;
            if(cmd.action==ArchiveAction.Place)
            {
                // The authoritative pre-command carry identifies the item, including legacy
                // test commands. Replacing a reusable T/F tile with the same value is no work.
                if(placedItem<0||previousPlacement==placedItem&&!q.regionPlacement)return;
                if(q.regionPlacement)
                {
                    key="p:"+placedItem+":"+cmd.a;
                }
                else key="p:"+cmd.a+":"+s.placements[cmd.a];
            }
            if(cmd.action==ArchiveAction.Connect)key="l:"+(cmd.a*100+cmd.b);
            if(cmd.action==ArchiveAction.Remove&&q.tree!=null&&q.tree.Editable)
            {
                // Spanning/repair questions can require only removals. Credit an original
                // branch that stays absent, never a manufactured add-then-remove cycle.
                int edge=Math.Min(cmd.a,cmd.b)*100+Math.Max(cmd.a,cmd.b);
                if(q.tree.initial.Any(e=>
                    Math.Min(Array.IndexOf(q.nodeIds,e.source),Array.IndexOf(q.nodeIds,e.destination))*100+
                    Math.Max(Array.IndexOf(q.nodeIds,e.source),Array.IndexOf(q.nodeIds,e.destination))==edge))key="r:"+edge;
            }
            if(cmd.action==ArchiveAction.Select&&(q.tree==null||!q.tree.Editable)&&
                (q.mapping==null||q.mapping.questionKind!=MappingQuestionKind.BuildExactRelation))
            {
                if(previousSelection==s.selection&&previousFlags==s.flags)return;
                if(q.mapping!=null&&q.mapping.questionKind==MappingQuestionKind.SelectProperties)
                {
                    // Ownership belongs to each surviving checked property, not the entire
                    // intermediate bit mask. Toggling a flag off removes its credit.
                    if(cmd.a==0){key="s:3:0";s.workCredits.RemoveAll(w=>w.key.StartsWith("f:",StringComparison.Ordinal)||w.key.StartsWith("s:",StringComparison.Ordinal));}
                    else{key="f:"+cmd.a;s.workCredits.RemoveAll(w=>w.key==key||w.key.StartsWith("s:",StringComparison.Ordinal));if((s.flags&cmd.a)==0)return;}
                }
                else
                {
                    s.workCredits.RemoveAll(w=>w.key.StartsWith("s:",StringComparison.Ordinal));
                    key="s:"+s.selection+":"+s.flags;
                }
            }
            if(key==null)return;
            s.workCredits.RemoveAll(w=>w.key==key);
            var original=crystalOwners.FirstOrDefault(w=>w.key==key);
            s.workCredits.Add(new ArchiveWorkCredit{actor=actor,key=key,crystalTracked=true,
                crystalActor=original==null?actor:original.crystalTracked?original.crystalActor:original.actor});
            if(s.workCredits.Count>256)s.workCredits.RemoveRange(0,s.workCredits.Count-256);
        }
        bool CreditStillUsed(ArchiveWorkCredit w,ArchiveStationState s)
        {
            var parts=w.key.Split(':');int a,b;
            if(parts.Length==3&&parts[0]=="p"&&int.TryParse(parts[1],out a)&&int.TryParse(parts[2],out b))return a>=0&&a<s.placements.Length&&s.placements[a]==b;
            if(parts.Length==2&&parts[0]=="l"&&int.TryParse(parts[1],out a))return s.links.Contains(a);
            if(parts.Length==2&&parts[0]=="r"&&int.TryParse(parts[1],out a))return !s.links.Any(link=>Math.Min(link/100,link%100)*100+Math.Max(link/100,link%100)==a);
            if(parts.Length==2&&parts[0]=="f"&&int.TryParse(parts[1],out a))return (s.flags&a)!=0;
            return w.key=="s:"+s.selection+":"+s.flags;
        }
        void RecordSolvedMilestone(int actor,ArchiveStationState s,ArchiveQuestion q)
        {
            NormalizeCooperation();
            string key=s.topic+":"+s.round+":"+s.subphase;
            if(State.solved.Any(v=>v.topic==s.topic&&v.round==s.round&&v.subphase==s.subphase&&v.resetSerial==s.testResetSerial))return;
            var contributors=s.workCredits.Where(w=>CreditStillUsed(w,s)).Select(w=>w.actor).Append(actor)
                .Concat(q.graph!=null&&s.routeActor!=0?new[]{s.routeActor}:Array.Empty<int>()).Distinct().ToArray();
            var crystalContributors=s.workCredits.Where(w=>CreditStillUsed(w,s)).Select(w=>w.crystalTracked?w.crystalActor:w.actor)
                .Concat(q.activity==ArchiveActivity.Choice?new[]{actor}:Array.Empty<int>())
                .Concat(q.graph!=null&&q.graph.type!=LogicLegends.Graph.GraphChallengeType.Degree&&s.routeActor!=0&&s.continuous?new[]{s.routeActor}:Array.Empty<int>())
                .Where(a=>a!=0).Distinct().ToArray();
            State.solved.Add(new ArchiveSolvedMilestone{topic=s.topic,round=s.round,subphase=s.subphase,resetSerial=s.testResetSerial,
                contributors=contributors,crystalContributors=crystalContributors});
            foreach(int contributor in contributors)AddQualityCredit(contributor,key);
            s.workCredits.Clear();
        }
        void AddQualityCredit(int actor,string key)
        {
            if(State.quality.Any(v=>v.actor==actor&&v.key==key))return;
            State.quality.Add(new ArchiveQualityCredit{actor=actor,key=key});
            var record=State.actors.FirstOrDefault(a=>a.actor==actor);
            if(record!=null)record.qualityMilestones++;
        }
        public static bool EvaluateConnective(string name,bool p,bool q)
        {
            string op=(name??"").ToLowerInvariant();
            if(op.Contains("biconditional"))return p==q;
            if(op.Contains("conditional"))return !p||q;
            if(op.Contains("exclusive"))return p!=q;
            if(op.Contains("conjunction"))return p&&q;
            if(op.Contains("negation"))return !p;
            return p||q;
        }
        static string Truth(bool value)=>value?"TRUE":"FALSE";
        public static string ExplainT1Connective(string name,bool p,bool q)
        {
            string op=(name??"").ToLowerInvariant();
            string rule=op.Contains("biconditional")?"Biconditional is TRUE when both inputs match.":
                op.Contains("conditional")?"Conditional is FALSE only when P is TRUE and Q is FALSE.":
                op.Contains("exclusive")?"Exclusive OR is TRUE when exactly one input is TRUE.":
                op.Contains("conjunction")?"AND is TRUE only when both inputs are TRUE.":
                op.Contains("negation")?"Negation reverses P.":"OR is TRUE when at least one input is TRUE.";
            return "P = "+Truth(p)+", Q = "+Truth(q)+". Result = "+Truth(EvaluateConnective(name,p,q))+". "+rule;
        }

        // Compatibility entry points delegate to the same per-topic relay implementation.
        void TickT1Coop()=>TickTopicRelays();
        bool AcceptT1Input(int actor,ArchiveCommand cmd)=>AcceptTopicRelayInput(actor,cmd);
        void ResetT1Coop(bool complete)=>ResetTopicRelay(0,complete);
        public string T1RelayModeLabel=>RelayModeLabel(0);
        public int[] T1AvailableActors()=>RelayAvailableActors(0);
        public bool T1CanSetInput(int actor,int input)=>RelayCanSetInput(actor,0,input);
        public bool T1CanConfirmInput(int actor,int input)=>RelayCanConfirmInput(actor,0,input);
        public void RequestT1Input(int input,bool value)=>RequestRelayInput(0,input,value);
        public void RequestT1Confirm(int input)=>RequestRelayConfirm(0,input);
    }
}
