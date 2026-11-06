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
    /// <summary>Real catalog cases verify that pre-submit hints are not an answer-checking oracle.</summary>
    public static class ArchiveReasoningHintChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scope="Pure Editor content checks against all ten existing topic adapters. Valid solutions, empty/null/malformed work and absent answer keys must receive the same level-2 scaffold for the same visible task kind. Existing validators are exercised before/after hint presentation only; no scene, account, room, timer or gameplay state is changed. Native UI/transport and physical Android are separate checks.";
            public int checks,questionCases,truthFeedbackCases;
            public int[] casesPerTopic=new int[10];
            public List<string> errors=new List<string>();
        }
        public static string Status {get;private set;}="Idle";
        static void Check(Report report,bool condition,string message)
        {report.checks++;if(!condition)throw new InvalidOperationException(message);}

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Validate Reasoning Hint Scaffolds")]
        public static void RunMenu()=>Run();
        public static Report Run()
        {
            var report=new Report();Status="Checking reasoning scaffolds";
            var catalog=AssetDatabase.LoadAssetAtPath<ArchiveTopicCatalog>(ArchiveTopicSetup.Folder+"/Archive_Topic_Catalog.asset");
            if(catalog==null){report.errors.Add("The existing topic catalog is required.");return Save(report);}
            for(int seed=1;seed<=4;seed++)for(int topic=0;topic<10;topic++)
                for(int round=0;round<ArchiveTopicCatalog.RoundCount(topic);round++)for(int subphase=0;subphase<(topic==2?2:1);subphase++)
                {
                    string identity="T"+(topic+1)+"/round"+round+"/phase"+subphase+"/seed"+seed;
                    try
                    {
                        var question=ArchiveTopicQuestions.Build(catalog,topic,round,seed*7919,subphase);
                        var solution=ArchiveTopicChecks.Solution(question);solution.topic=topic;solution.round=round;solution.subphase=subphase;
                        Check(report,ArchiveTopicQuestions.Validate(question,solution,out _),identity+": the real adapter accepts its source solution before hints");
                        string stateBefore=JsonUtility.ToJson(solution);
                        int choice=question.correctChoice,method=question.method;var expected=question.expected;
                        var expectedCopy=expected.ToArray();var items=question.items;var itemCopy=items.ToArray();
                        string scaffold=ArchiveLearningFeedback.Hint(topic,question,solution,2);
                        Check(report,scaffold.StartsWith("REASON IT THROUGH\n")&&scaffold.Length>110,identity+": scaffold provides a reasoning step");
                        Check(report,scaffold==ArchiveLearningFeedback.Hint(topic,question,null,2),identity+": absent work cannot turn a hint into validation feedback");
                        var empty=new ArchiveStationState{topic=topic,phase=1,placements=Array.Empty<int>()};
                        Check(report,scaffold==ArchiveLearningFeedback.Hint(topic,question,empty,2),identity+": empty and accepted work receive the same prompt");
                        var malformed=new ArchiveStationState{topic=topic,phase=1,selection=int.MaxValue,flags=int.MinValue,placements=null,links=null,route=null};
                        Check(report,scaffold==ArchiveLearningFeedback.Hint(topic,question,malformed,2),identity+": malformed live selections are never inspected by the scaffold");
                        for(int level=0;level<=3;level++)
                            Check(report,!string.IsNullOrWhiteSpace(ArchiveLearningFeedback.Hint(topic,question,solution,level)),identity+": existing hint level "+level+" is available");
                        Check(report,stateBefore==JsonUtility.ToJson(solution)&&ReferenceEquals(question.expected,expected)&&expected.SequenceEqual(expectedCopy)&&
                            ReferenceEquals(question.items,items)&&items.SequenceEqual(itemCopy)&&question.correctChoice==choice&&question.method==method,
                            identity+": explanations preserve source keys and all serialized team work");
                        Check(report,ArchiveTopicQuestions.Validate(question,solution,out _),identity+": source validator still accepts the unchanged solution after all hint levels");
                        string example=ArchiveLearningFeedback.Hint(topic,question,solution,3);

                        // Assigning fields on this fresh question cannot mutate the
                        // catalog's nested graph/proof/set objects. Nested assets stay untouched.
                        question.expected=null;question.correctChoice=int.MaxValue;question.method=int.MinValue;
                        question.items=null;question.details=null;question.slots=null;question.regionValues=null;
                        question.inputLabels=null;question.outputLabels=null;question.nodeIds=null;question.orders=null;
                        question.successText="UNIQUE LIVE ANSWER SENTINEL";question.prompt="UNIQUE LIVE QUESTION SENTINEL";
                        Check(report,scaffold==ArchiveLearningFeedback.Hint(topic,question,malformed,2),identity+": null answer arrays and poisoned keys cannot disclose answers or call the validator");
                        Check(report,example==ArchiveLearningFeedback.Hint(topic,question,malformed,3)&&!example.Contains("SENTINEL"),
                            identity+": the final worked example stays independent of the live question and answer");
                        report.questionCases++;report.casesPerTopic[topic]++;
                    }
                    catch(Exception exception){report.errors.Add(identity+": "+exception.GetBaseException().Message);}
                }
            try
            {
                for(int topic=0;topic<10;topic++)
                {
                    Check(report,report.casesPerTopic[topic]>0,"T"+(topic+1)+": real catalog coverage is present");
                    Check(report,!string.IsNullOrWhiteSpace(ArchiveLearningFeedback.Hint(topic,null,null,2)),"T"+(topic+1)+": a missing question still receives a safe topic prompt");
                }
                foreach(var kind in Enum.GetValues(typeof(GraphChallengeType)).Cast<GraphChallengeType>())
                {
                    var q=new ArchiveQuestion{graph=new GraphChallengeData{type=kind},expected=null,items=null,slots=null};
                    Check(report,ArchiveLearningFeedback.Hint(8,q,null,2).Length>110,"Graph "+kind+": visible task kind receives a process prompt without answer data");
                }
                foreach(var kind in Enum.GetValues(typeof(TreeChallengeType)).Cast<TreeChallengeType>())
                {
                    var q=new ArchiveQuestion{tree=new TreeChallengeData{type=kind},expected=null,items=null,slots=null};
                    Check(report,ArchiveLearningFeedback.Hint(9,q,null,2).Length>110,"Tree "+kind+": visible task kind receives a process prompt without answer data");
                }
                for(int expected=0;expected<=1;expected++)
                {
                    var q=new ArchiveQuestion{activity=ArchiveActivity.Truth,prompt="P ∧ Q",items=new[]{"TRUE","FALSE"},slots=new[]{"ROW TT"},expected=new[]{expected}};
                    var s=new ArchiveStationState{topic=1,phase=1,placements=new[]{2}};string before=JsonUtility.ToJson(s);
                    string feedback=ArchiveLearningFeedback.Diagnose(q,s);
                    Check(report,feedback.Contains("ROW TT")&&feedback.Contains("intermediate")&&!feedback.Contains("TRUE")&&!feedback.Contains("FALSE"),
                        "Truth feedback for hidden result "+expected+": points back to the row and intermediate values without supplying the result");
                    Check(report,before==JsonUtility.ToJson(s)&&q.expected[0]==expected,"Truth diagnosis leaves the selected tile and answer key unchanged");
                    report.truthFeedbackCases++;
                }
            }
            catch(Exception exception){report.errors.Add(exception.GetBaseException().Message);}
            return Save(report);
        }
        static Report Save(Report report)
        {
            report.passed=report.errors.Count==0&&report.questionCases>0;Status=report.passed?"Passed":"Failed";
            Directory.CreateDirectory("Temp/ArchiveLearningScaffold");
            File.WriteAllText("Temp/ArchiveLearningScaffold/Content.json",JsonUtility.ToJson(report,true));
            Debug.Log("Reasoning hint checks: "+Status+" ("+report.questionCases+" catalog cases, "+report.checks+" checks).");
            return report;
        }
    }
}
