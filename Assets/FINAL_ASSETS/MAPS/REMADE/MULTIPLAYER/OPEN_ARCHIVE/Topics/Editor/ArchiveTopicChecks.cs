using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.Graph;
using LogicLegends.Trees;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveTopicChecks
    {
        [Serializable] public sealed class Report { public bool passed; public int correctAnswers,incorrectAnswers,questionBuilds; public List<string> failures=new List<string>(); }
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Validate All Topic Question Adapters")]
        public static void Run(){var report=Validate();Debug.Log("Open Archive topic adapters: "+(report.passed?"PASS":"FAIL")+", "+report.correctAnswers+" correct / "+report.incorrectAnswers+" incorrect checks.");}
        public static Report Validate()
        {
            var c=AssetDatabase.LoadAssetAtPath<ArchiveTopicCatalog>(ArchiveTopicSetup.Folder+"/Archive_Topic_Catalog.asset");var report=new Report();
            for(int seed=1;seed<=32;seed++)for(int topic=0;topic<10;topic++)for(int round=0;round<ArchiveTopicCatalog.RoundCount(topic);round++)
                for(int sub=0;sub<(topic==2?2:1);sub++)
                {
                    string key="seed "+seed+", T"+(topic+1)+", round "+round+", phase "+sub;
                    try
                    {
                        var q=ArchiveTopicQuestions.Build(c,topic,round,seed*7919,sub);report.questionBuilds++;
                        if(!q.itemOrder.OrderBy(i=>i).SequenceEqual(Enumerable.Range(0,q.items.Length)))throw new InvalidOperationException("Shuffled prop layout loses an answer item.");
                        if(topic==0&&c.propositions.Any(bank=>bank.title==q.title))throw new InvalidOperationException("Question heading reveals the connective answer.");
                        if(topic==2&&sub==1&&string.IsNullOrWhiteSpace(q.successText))throw new InvalidOperationException("Inference conclusion is missing from the correct-answer reveal.");
                        var state=Solution(q);state.topic=topic;state.round=round;state.subphase=sub;
                        if(!ArchiveTopicQuestions.Validate(q,state,out string message))throw new InvalidOperationException("Correct fixture rejected: "+message);
                        report.correctAnswers++;
                        var copy=JsonUtility.FromJson<ArchiveStationState>(JsonUtility.ToJson(state));
                        if(!ArchiveTopicQuestions.Validate(q,copy,out message))throw new InvalidOperationException("Serialized answer changed its meaning.");
                        MakeIncorrect(q,copy);
                        if(ArchiveTopicQuestions.Validate(q,copy,out message))throw new InvalidOperationException("Incorrect fixture accepted.");
                        report.incorrectAnswers++;
                        if(q.mapping!=null&&q.mapping.questionKind==MappingQuestionKind.SelectProperties&&((int)q.mapping.correctProperties&~14)!=0)throw new InvalidOperationException("Property answer cannot be entered with the three approved property controls.");
                        if(q.items.Length>64||q.nodes.Length>32||q.slots.Length>32)throw new InvalidOperationException("Activity exceeds the authored touch/prop budget.");
                    }
                    catch(Exception e){report.failures.Add(key+": "+e.Message);}
                }
            report.passed=report.failures.Count==0;
            Directory.CreateDirectory(ArchiveTopicSetup.Folder+"/Testing/Reports");File.WriteAllText(ArchiveTopicSetup.Folder+"/Testing/Reports/QuestionAdapters.json",JsonUtility.ToJson(report,true));return report;
        }
        public static ArchiveStationState Solution(ArchiveQuestion q)
        {
            var s=new ArchiveStationState{phase=1};ArchiveTopicQuestions.Prepare(q,s);
            switch(q.activity)
            {
                case ArchiveActivity.Choice:s.selection=q.correctChoice;break;
                case ArchiveActivity.Sort:case ArchiveActivity.Truth:case ArchiveActivity.Induction:s.placements=q.expected.ToArray();break;
                case ArchiveActivity.Premises:
                    var used=new HashSet<int>();
                    for(int i=0;i<q.expected.Length;i++){int choice=Enumerable.Range(0,q.items.Length).First(n=>!used.Contains(n)&&q.items[n]==q.items[q.expected[i]]);used.Add(choice);s.placements[i]=choice;}break;
                case ArchiveActivity.Order:
                    if(q.proof==null||q.proof.kind==ProofChallengeKind.MissingRule)s.placements=q.expected.ToArray();
                    else
                    {
                        var order=q.orders[0];for(int i=0;i<order.Length;i++)s.placements[i]=order[i];
                        if(q.proof.kind==ProofChallengeKind.Formalize)for(int i=0;i<order.Length;i++)s.placements[order.Length+i]=order.Length+order[i];
                    }break;
                case ArchiveActivity.Method:
                    s.selection=q.method;if(q.trial.acceptedOrders.Length>0)s.placements=q.trial.acceptedOrders[0].tabletIds.ToArray();break;
                case ArchiveActivity.Sets:
                    var taken=new HashSet<int>();
                    for(int region=0;region<q.regionValues.Length;region++)foreach(string value in q.regionValues[region])
                    {int i=Enumerable.Range(0,q.items.Length).First(n=>!taken.Contains(n)&&q.items[n]==value);taken.Add(i);s.placements[i]=region;}break;
                case ArchiveActivity.Mapping:
                    if(q.mapping.questionKind==MappingQuestionKind.ClassifyFunction)s.selection=q.mapping.correctClassificationIndex;
                    else if(q.mapping.questionKind==MappingQuestionKind.SelectProperties){s.flags=(int)q.mapping.correctProperties;if(s.flags==0)s.selection=3;}
                    else s.links=q.mapping.requiredPairs.Select(p=>Array.IndexOf(q.inputLabels,p.domain)*100+Array.IndexOf(q.outputLabels,p.codomain)).ToList();break;
                case ArchiveActivity.Graph:
                    if(q.graph.type==GraphChallengeType.Degree)s.selection=q.graph.Degree(q.graph.degreeVertex);
                    else
                    {
                        var route=new List<string>{q.graph.start};if(!FindRoute(q.graph,route))throw new InvalidOperationException("No accepted route in the original graph.");
                        s.route=route.Select(v=>Array.IndexOf(q.nodeIds,v)).ToList();s.routeAtVertex=true;
                    }break;
                case ArchiveActivity.Tree:
                    if(q.tree.type==TreeChallengeType.Build||q.tree.type==TreeChallengeType.Repair)s.links=q.tree.required.Select(e=>Array.IndexOf(q.nodeIds,e.source)*100+Array.IndexOf(q.nodeIds,e.destination)).ToList();
                    else if(q.tree.type==TreeChallengeType.Spanning)
                    {
                        var visited=new HashSet<string>{q.nodeIds[0]};s.links.Clear();
                        while(visited.Count<q.nodeIds.Length)
                        {
                            var edge=q.tree.initial.First(e=>visited.Contains(e.source)^visited.Contains(e.destination));
                            visited.Add(edge.source);visited.Add(edge.destination);s.links.Add(Array.IndexOf(q.nodeIds,edge.source)*100+Array.IndexOf(q.nodeIds,edge.destination));
                        }
                    }
                    if(q.tree.type==TreeChallengeType.Depth)s.flags=TreeAnswerValidator.Depth(q.tree,q.tree.initial,q.tree.target);
                    else if(q.tree.type==TreeChallengeType.Height)s.flags=TreeAnswerValidator.Height(q.tree,q.tree.initial);
                    else if(q.tree.type==TreeChallengeType.Identify)
                    {
                        for(int i=0;i<q.nodeIds.Length;i++){s.selection=i;if(ArchiveTopicQuestions.Validate(q,s,out _))break;}
                    }break;
            }
            return s;
        }
        static bool FindRoute(GraphChallengeData q,List<string> route)
        {
            if(route.Count>q.vertices.Count+1)return false;
            string at=route.Last();
            if(q.type==GraphChallengeType.Cycle&&route.Count>=4&&at==q.start)return true;
            if(q.type!=GraphChallengeType.Cycle&&route.Count>=2&&at==q.destination)return true;
            foreach(var vertex in q.vertices)
            {
                if(!q.Connected(at,vertex.id))continue;
                bool closing=q.type==GraphChallengeType.Cycle&&vertex.id==q.start&&route.Count>=3;
                if(!closing&&route.Contains(vertex.id))continue;
                route.Add(vertex.id);if(FindRoute(q,route))return true;route.RemoveAt(route.Count-1);
            }
            return false;
        }
        static void MakeIncorrect(ArchiveQuestion q,ArchiveStationState s)
        {
            switch(q.activity)
            {
                case ArchiveActivity.Choice:s.selection=(q.correctChoice+1)%q.items.Length;break;
                case ArchiveActivity.Mapping:
                    if(q.mapping.questionKind==MappingQuestionKind.ClassifyFunction)s.selection=1-q.mapping.correctClassificationIndex;
                    else if(q.mapping.questionKind==MappingQuestionKind.SelectProperties)s.flags^=2;
                    else s.links.Clear();break;
                case ArchiveActivity.Graph:s.route.Clear();s.selection=s.selection<0?-1:s.selection+1;break;
                case ArchiveActivity.Tree:
                    if(q.tree.type==TreeChallengeType.Identify)
                    {for(int i=0;i<q.nodeIds.Length;i++){s.selection=i;if(!ArchiveTopicQuestions.Validate(q,s,out _))return;}throw new InvalidOperationException("Identification has no invalid node.");}
                    else if(q.tree.type==TreeChallengeType.Depth||q.tree.type==TreeChallengeType.Height)s.flags++;
                    else s.links.Clear();break;
                case ArchiveActivity.Method:
                    if(q.trial.kind==TrialProofKind.IdentifyMethod)s.selection=(q.method+1)%3;
                    else if(s.placements.Length>0)s.placements[0]=-1;break;
                default:if(s.placements.Any(v=>v>=0))s.placements[Array.FindIndex(s.placements,v=>v>=0)]=-1;else throw new InvalidOperationException("Missing incorrect-answer fixture.");break;
            }
        }
    }
}
