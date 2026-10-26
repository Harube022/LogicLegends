using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.Trees;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Exercises contribution receipts over the original adapters without changing scene assets or gameplay progress.</summary>
    public static class ArchiveParticipationChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scope="Editor ledger checks over original T4/T5/T8/T10 adapters and real tree edit rules. The active session state is restored in finally; no LP, currency, network or scene mutations.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly MethodInfo Credit=typeof(ArchiveTopicSession).GetMethod("RecordWorkCredit",Private);
        static readonly MethodInfo Still=typeof(ArchiveTopicSession).GetMethod("CreditStillUsed",Private);
        static readonly MethodInfo Solved=typeof(ArchiveTopicSession).GetMethod("RecordSolvedMilestone",Private);
        static readonly MethodInfo Select=typeof(ArchiveTopicSession).GetMethod("Select",Private);
        static readonly MethodInfo SetState=typeof(ArchiveTopicSession).GetProperty("State").GetSetMethod(true);
        static ArchiveTopicSession session;
        static ArchiveTopicCatalog catalog;
        static Report report;

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Validate Final-Work Participation")]
        public static Report Validate()
        {
            session=UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();
            if(session==null)throw new InvalidOperationException("Open the saved Open Archive topic scene first.");
            if(Photon.Pun.PhotonNetwork.InRoom)throw new InvalidOperationException("Run these isolated ledger checks outside a Photon room.");
            var previous=session.State;catalog=session.catalog;report=new Report();
            try
            {
                SpanningAndRepair();PropertyBits();ScalarAndPickupSpam();Placements();
            }
            catch(Exception e){report.errors.Add(e.GetBaseException().ToString());}
            finally{SetState.Invoke(session,new object[]{previous});}
            report.passed=report.errors.Count==0&&report.checks.Count>0;
            Directory.CreateDirectory("Temp/ArchiveCoop");File.WriteAllText("Temp/ArchiveCoop/Participation.json",JsonUtility.ToJson(report,true));
            Debug.Log("Final-work participation: "+(report.passed?"PASS":"FAIL")+" ("+report.checks.Count+").");
            return report;
        }
        static void Check(bool value,string message)
        {report.checks.Add((value?"PASS ":"FAIL ")+message);if(!value)throw new InvalidOperationException(message);}
        static ArchiveQuestion Find(int topic,Func<ArchiveQuestion,bool> predicate)
        {
            for(int seed=1;seed<=32;seed++)for(int round=0;round<ArchiveTopicCatalog.RoundCount(topic);round++)
            {var q=ArchiveTopicQuestions.Build(catalog,topic,round,seed*7919);if(predicate(q))return q;}
            throw new InvalidOperationException("Original question fixture is missing for T"+(topic+1));
        }
        static ArchiveStationState Fresh(int topic,ArchiveQuestion q)
        {
            var team=new ArchiveTeamState{stations=Enumerable.Range(0,10).Select(t=>new ArchiveStationState{topic=t}).ToArray()};
            for(int actor=1;actor<=4;actor++)team.actors.Add(new ArchiveActor{actor=actor,displayName="QA "+actor});
            var s=team.stations[topic];s.phase=1;ArchiveTopicQuestions.Prepare(q,s);SetState.Invoke(session,new object[]{team});return s;
        }
        static void Record(int actor,ArchiveCommand command,ArchiveStationState s,ArchiveQuestion q,
            int previousSelection=-1,int previousFlags=0,int placedItem=-1,int previousPlacement=-1)
        {Credit.Invoke(session,new object[]{actor,command,s,q,previousSelection,previousFlags,placedItem,previousPlacement});}
        static bool Used(ArchiveWorkCredit credit,ArchiveStationState state)=>(bool)Still.Invoke(session,new object[]{credit,state});
        static void Complete(int actor,ArchiveStationState s,ArchiveQuestion q)
        {Check(ArchiveTopicQuestions.Validate(q,s,out _),"Original validator accepts the final work");Solved.Invoke(session,new object[]{actor,s,q});}
        static void Toggle(int actor,int bit,ArchiveStationState s,ArchiveQuestion q)
        {
            int selection=s.selection,flags=s.flags;
            Check((bool)Select.Invoke(session,new object[]{q,s,bit}),"Original property toggle accepts bit "+bit);
            Record(actor,new ArchiveCommand{action=ArchiveAction.Select,a=bit},s,q,selection,flags);
        }
        static int Canonical(int edge)=>Math.Min(edge/100,edge%100)*100+Math.Max(edge/100,edge%100);

        static void SpanningAndRepair()
        {
            var q=Find(9,v=>v.tree.type==TreeChallengeType.Spanning);var s=Fresh(9,q);var answer=ArchiveTopicChecks.Solution(q);
            foreach(int edge in s.links.ToArray().Where(e=>!answer.links.Any(a=>Canonical(a)==Canonical(e))))
            {
                int a=edge/100,b=edge%100;
                Check(ArchiveTreeRules.Edit(q,s,b,a,true,out _),"Original spanning rule accepts reversed-endpoint removal");
                Record(1,new ArchiveCommand{action=ArchiveAction.Remove,a=b,b=a},s,q);
            }
            Check(s.workCredits.Count>0&&s.workCredits.All(w=>w.actor==1&&Used(w,s)),"Final removed original edges belong to the teammate who removed them");
            Complete(2,s,q);
            Check(session.State.solved.Single().contributors.OrderBy(v=>v).SequenceEqual(new[]{1,2}),"Spanning remover and distinct confirmer both receive participation");
            int quality=session.State.quality.Count;Solved.Invoke(session,new object[]{2,s,q});
            Check(session.State.quality.Count==quality,"Replayed accepted milestone cannot add participation again");

            s=Fresh(9,q);int removed=s.links[0];
            Check(ArchiveTreeRules.Edit(q,s,removed/100,removed%100,true,out _),"Original branch can be removed for re-add fixture");
            Record(1,new ArchiveCommand{action=ArchiveAction.Remove,a=removed/100,b=removed%100},s,q);
            var removal=s.workCredits.Single();
            Check(ArchiveTreeRules.Edit(q,s,removed%100,removed/100,false,out _),"Original branch can be re-added with reversed endpoints");
            Record(2,new ArchiveCommand{action=ArchiveAction.Connect,a=removed%100,b=removed/100},s,q);
            Check(!Used(removal,s),"Re-added original edge invalidates its removal receipt");
            Check(ArchiveTreeRules.Edit(q,s,removed/100,removed%100,true,out _),"Re-added original branch can be removed again");
            Record(3,new ArchiveCommand{action=ArchiveAction.Remove,a=removed/100,b=removed%100},s,q);
            Check(s.workCredits.Count(w=>w.key==removal.key)==1&&s.workCredits.First(w=>w.key==removal.key).actor==3,
                "A genuinely new final removal belongs only to its current remover");

            q=Find(9,v=>v.tree.type==TreeChallengeType.Repair);s=Fresh(9,q);answer=ArchiveTopicChecks.Solution(q);
            foreach(int edge in s.links.ToArray().Where(e=>!answer.links.Contains(e)))
            {Check(ArchiveTreeRules.Edit(q,s,edge/100,edge%100,true,out _),"Original repair removes the wrong parent");Record(1,new ArchiveCommand{action=ArchiveAction.Remove,a=edge/100,b=edge%100},s,q);}
            foreach(int edge in answer.links.Where(e=>!s.links.Contains(e)))
            {Check(ArchiveTreeRules.Edit(q,s,edge/100,edge%100,false,out _),"Original repair connects the correct parent");Record(2,new ArchiveCommand{action=ArchiveAction.Connect,a=edge/100,b=edge%100},s,q);}
            Complete(3,s,q);
            Check(session.State.solved.Single().contributors.OrderBy(v=>v).SequenceEqual(new[]{1,2,3}),"Repair credits remover, builder and confirmer");

            q=Find(9,v=>v.tree.type==TreeChallengeType.Build);s=Fresh(9,q);int branch=ArchiveTopicChecks.Solution(q).links[0];
            Check(ArchiveTreeRules.Edit(q,s,branch/100,branch%100,false,out _),"Original build accepts a new branch");Record(1,new ArchiveCommand{action=ArchiveAction.Connect,a=branch/100,b=branch%100},s,q);
            Check(ArchiveTreeRules.Edit(q,s,branch/100,branch%100,true,out _),"Original build accepts correcting a newly added branch");Record(2,new ArchiveCommand{action=ArchiveAction.Remove,a=branch/100,b=branch%100},s,q);
            Check(!s.workCredits.Any(w=>Used(w,s)),"Add-then-remove of a non-original branch creates no final-work receipt");
        }
        static void PropertyBits()
        {
            var q=Find(7,v=>v.mapping.questionKind==MappingQuestionKind.SelectProperties&&(int)v.mapping.correctProperties==14);var s=Fresh(7,q);
            Toggle(1,2,s,q);Toggle(2,4,s,q);Toggle(3,8,s,q);
            Check(s.workCredits.Where(w=>Used(w,s)).Select(w=>w.actor).OrderBy(v=>v).SequenceEqual(new[]{1,2,3}),"Independent surviving property bits retain all three owners");
            Complete(4,s,q);
            Check(session.State.solved.Single().contributors.OrderBy(v=>v).SequenceEqual(new[]{1,2,3,4}),"Accepted properties credit every useful selector and confirmer");
            s=Fresh(7,q);Toggle(1,2,s,q);Toggle(2,4,s,q);Toggle(3,2,s,q);
            Check(!s.workCredits.Any(w=>w.key=="f:2"),"Turning a property off removes that bit's receipt");
            Check(s.workCredits.Any(w=>w.key=="f:4"&&w.actor==2&&Used(w,s)),"Toggling another bit never takes ownership of an unchanged bit");
            Toggle(4,2,s,q);
            Check(s.workCredits.Single(w=>w.key=="f:2").actor==4,"Turning a property back on credits the new actual selector");
            Check(s.workCredits.Count==2&&session.State.quality.Count==0,"Intermediate toggles cannot directly increment accepted participation");
        }
        static void ScalarAndPickupSpam()
        {
            var q=Find(4,v=>v.trial.kind==TrialProofKind.IdentifyMethod);var s=Fresh(4,q);
            int before=s.selection;s.selection=q.method;
            Record(1,new ArchiveCommand{action=ArchiveAction.Select,a=q.method},s,q,before,s.flags);
            Record(2,new ArchiveCommand{action=ArchiveAction.Select,a=q.method},s,q,s.selection,s.flags);
            Check(s.workCredits.Count==1&&s.workCredits[0].actor==1,"Tapping an already-selected answer cannot steal its original selector's receipt");
            int original=s.selection;s.selection=(q.method+1)%3;
            Record(2,new ArchiveCommand{action=ArchiveAction.Select,a=s.selection},s,q,original,s.flags);
            Check(s.workCredits.Count==1&&s.workCredits[0].actor==2,"An actual changed choice records its new selector");
            before=s.selection;s.selection=q.method;
            Record(1,new ArchiveCommand{action=ArchiveAction.Select,a=q.method},s,q,before,s.flags);
            for(int i=0;i<100;i++)
            {Record(2,new ArchiveCommand{action=ArchiveAction.Pick,a=0},s,q,s.selection,s.flags);Record(2,new ArchiveCommand{action=ArchiveAction.ReturnItem},s,q,s.selection,s.flags);}
            Check(s.workCredits.Count==1&&s.workCredits[0].actor==1&&session.State.quality.Count==0,"Repeated pickup/return creates no quality receipt or accepted milestone");
            Complete(3,s,q);
            Check(session.State.solved.Single().contributors.OrderBy(v=>v).SequenceEqual(new[]{1,3}),"Final confirmation excludes no-op selectors and pickup spam");
        }
        static void Placements()
        {
            var q=Find(1,v=>v.reusableItems);var s=Fresh(1,q);int value=q.expected[0];s.placements[0]=value;
            Record(1,new ArchiveCommand{action=ArchiveAction.Place,a=0},s,q,s.selection,s.flags,value,-1);
            Record(2,new ArchiveCommand{action=ArchiveAction.Place,a=0},s,q,s.selection,s.flags,value,value);
            Check(s.workCredits.Single().actor==1,"Replacing a reusable truth tile with the same value does not steal placement credit");
            s.placements[0]=1-value;
            Check(!Used(s.workCredits.Single(),s),"A replaced truth value invalidates the old placement receipt");
            q=Find(3,v=>v.regionPlacement&&v.items.Length>=2);s=Fresh(3,q);int item=q.items.Length-1,region=q.expected[item];s.placements[item]=region;
            Record(1,new ArchiveCommand{action=ArchiveAction.Place,a=region,expectedHeld=-1},s,q,s.selection,s.flags,item,-1);
            Check(s.workCredits.Single().key=="p:"+item+":"+region&&Used(s.workCredits.Single(),s),"Region credit uses the actual authoritative carried item even for legacy commands");
        }
    }
}
