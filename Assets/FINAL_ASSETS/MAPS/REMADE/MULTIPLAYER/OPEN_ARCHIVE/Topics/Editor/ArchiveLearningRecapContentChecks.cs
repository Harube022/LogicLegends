using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Pure reading-model checks. No scene, room, account, progress, timer or reward is changed.</summary>
    public static class ArchiveLearningRecapContentChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public int checks,questionCases;
            public string scope="Pure detached learning recap over existing authoritative records and all ten source topic adapters. Native result/return UI and live transport are separate checks.";
            public List<string> errors=new List<string>();
        }
        static void Check(Report report,bool condition,string message)
        {report.checks++;if(!condition)throw new InvalidOperationException(message);}
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Validate Learning Recap Content")]
        public static void RunMenu()=>Run();
        public static Report Run()
        {
            var report=new Report();
            var catalog=AssetDatabase.LoadAssetAtPath<ArchiveTopicCatalog>(ArchiveTopicSetup.Folder+"/Archive_Topic_Catalog.asset");
            try
            {
                Check(report,catalog!=null,"The existing module-based topic catalog is available");
                var empty=ArchiveLearningRecap.Build(catalog,null,1,"\nTester\r\n");
                Check(report,empty.outcome=="unavailable"&&empty.reviews.Length==0&&empty.suggestions.Length==0&&empty.playerName=="Tester","Missing data has a truthful empty view and a single-line name");
                var team=Team();var view=ArchiveLearningRecap.Build(catalog,team,1,"Tester");
                Check(report,view.reviews.Length==0&&view.summary.Contains("0 / 0 correct confirmed submissions")&&view.summary.Contains("0 / 10 topics practiced"),"An observer receives no invented attempts or practice credit");
                Check(report,view.suggestions.Length==3&&view.suggestions.All(s=>s.reason.Contains("No personal practice record")),"Unpracticed suggestions are labelled as opportunities, not weaknesses");
                Check(report,view.suggestions.Select(s=>s.title).Distinct().Count()==3&&!view.summary.Contains("accuracy"),"Suggestions are distinct and no mastery or accuracy score is invented");
                team.actors.Add(new ArchiveActor{actor=1,answers=4,correctAnswers=2,hintsUsed=3,contributions=400});
                team.mistakes.Add(new ArchiveMistake{actor=1,topic=1,round=0,reason="answer"}); // Legacy metadata, no attempt context.
                team.mistakes.Add(new ArchiveMistake{actor=0,topic=1,round=0,reason="timeout"});
                team.mistakes.Add(new ArchiveMistake{actor=2,topic=7,round=0,reason="answer",reviewVersion=1,reviewFeedback="Other player's private review"});
                team.solved.Add(new ArchiveSolvedMilestone{topic=0,round=0,contributors=new[]{1},crystalContributors=new[]{1}});
                team.solved.Add(new ArchiveSolvedMilestone{topic=0,round=0,contributors=new[]{1},crystalContributors=new[]{1}}); // Duplicate receipt must not inflate counts.
                team.solved.Add(new ArchiveSolvedMilestone{topic=2,round=0,subphase=1,contributors=new[]{1},crystalContributors=new[]{2}});
                team.solved.Add(new ArchiveSolvedMilestone{topic=6,round=0,resetSerial=99,contributors=new[]{1},crystalContributors=new[]{1}}); // Old reset, no live credit.
                team.rewards.support.AddRange(new[]{
                    new ArchiveRewardSupportCredit{actor=1,target=2,kind="medkit"},new ArchiveRewardSupportCredit{actor=1,target=2,kind="medkit"},
                    new ArchiveRewardSupportCredit{actor=1,target=3,kind="revive"},new ArchiveRewardSupportCredit{actor=1,target=2,kind="give"},
                    new ArchiveRewardSupportCredit{actor=2,target=1,kind="battery"}});
                team.rewards.gifts.Add(new ArchiveRewardGift{donor=1,recipient=4,item=2}); // Not consumed; not support yet.
                string before=JsonUtility.ToJson(team);view=ArchiveLearningRecap.Build(catalog,team,1,"Tester");
                Check(report,view.summary.Contains("2 / 4 correct confirmed submissions")&&view.summary.Contains("3 hint steps requested"),"Existing answer and hint counters retain their actual submission/step meaning");
                Check(report,view.summary.Contains("1 personal incorrect submission\n")&&view.summary.Contains("1 shared team timeout\n"),"Shared timeout is not attributed as a personal error and singular counts read correctly");
                Check(report,view.summary.Contains("3 / 10 topics practiced")&&view.teamworkText.Contains("Accepted parts with your puzzle work: 1"),"Personal errors, confirmed participation and actual puzzle work are read without duplicate/reset credit");
                Check(report,view.teamworkText.Contains("Teammates healed: 1")&&view.teamworkText.Contains("Teammates revived: 1")&&view.teamworkText.Contains("Teammates recharged: 0")&&view.teamworkText.Contains("Teammates who used your shared items: 1"),"Support counts only the correct donor's distinct confirmed recipients, excluding pending gifts");
                Check(report,view.reviews.Length==0,"Legacy metadata and another actor's mistakes cannot become a fabricated personal detailed review");
                Check(report,view.suggestions[0].title.StartsWith("T2 ·")&&view.suggestions[0].reason.Contains("1 personal incorrect"),"Personal review suggestions lead with actual recorded errors");
                Check(report,before==JsonUtility.ToJson(team),"Building the view leaves every serialized gameplay and reward field unchanged");
                team.actors[0].hintsUsed=1;team.stations[0].phase=team.stations[2].phase=2;
                team.crystals.Add(new ArchiveCrystalState{topic=0,fullyRestored=true});
                team.crystals.Add(new ArchiveCrystalState{topic=2,fullyRestored=true,adminBypass=true});
                view=ArchiveLearningRecap.Build(catalog,team,1,"Tester");
                Check(report,view.summary.Contains("1 hint step requested")&&view.summary.Contains("Team: 2 / 10 topics completed · 1 crystal restored"),"Team completion and eligible crystal restoration derive from existing state, with singular hint/crystal copy");
                string frozen=JsonUtility.ToJson(view);team.actors[0].answers=99;team.mistakes.Clear();team.rewards.support.Clear();team.solved.Clear();
                Check(report,frozen==JsonUtility.ToJson(view),"The returned view is fully detached from later mutable match state");

                team=Team();team.exit.won=true;
                for(int topic=0;topic<10;topic++)for(int subphase=0;subphase<(topic==2?2:1);subphase++)
                {
                    team.mistakes.Clear();team.mistakes.Add(Mistake(topic,0,subphase,"Recorded feedback "+topic+":"+subphase));
                    before=JsonUtility.ToJson(team);view=ArchiveLearningRecap.Build(catalog,team,1,"Tester");
                    var question=ArchiveTopicQuestions.Build(catalog,topic,0,team.seed,subphase);
                    Check(report,view.outcome=="victory"&&view.reviews.Length==1,"T"+(topic+1)+" phase "+subphase+": one source review is available");
                    var review=view.reviews[0];
                    Check(report,review.prompt.StartsWith(question.prompt??"")&&review.feedback=="Recorded feedback "+topic+":"+subphase,"T"+(topic+1)+": exact prompt identity and recorded diagnosis are retained");
                    Check(report,review.reasoning==ArchiveLearningFeedback.Hint(topic,question,null,2)&&review.module.Contains(ArchiveLearningFeedback.Reference(topic)),"T"+(topic+1)+": reasoning and module reference reuse the existing course content");
                    Check(report,review.prompt.Length<=5000,"T"+(topic+1)+": reconstructed given context stays bounded for the mobile reading panel");
                    if(question.details.Any(detail=>!string.IsNullOrWhiteSpace(detail)))
                        Check(report,review.prompt.Contains("Given cards (supply order):")&&review.prompt.Contains(question.details.First(detail=>!string.IsNullOrWhiteSpace(detail))),"T"+(topic+1)+": the actual given card content is included for a useful review");
                    Check(report,before==JsonUtility.ToJson(team),"T"+(topic+1)+": source reconstruction is read-only");report.questionCases++;
                }
                team.mistakes.Clear();
                for(int topic=0;topic<5;topic++)team.mistakes.Add(Mistake(topic,0,0,"earlier "+topic));
                team.mistakes.Add(Mistake(4,0,0,"latest same problem"));
                team.mistakes.Add(new ArchiveMistake{actor=1,topic=4,round=0,subphase=0,reason="answer",reviewVersion=0,reviewFeedback="legacy cannot override"});
                team.mistakes.Add(new ArchiveMistake{actor=1,topic=9,round=999,reason="answer",reviewVersion=1,reviewFeedback="bad identity"});
                view=ArchiveLearningRecap.Build(catalog,team,1,"Tester");
                Check(report,view.reviews.Length==3&&view.reviews[0].feedback=="latest same problem"&&view.reviews.Select(r=>r.title).Distinct().Count()==3,"Only the latest three valid distinct personal problems appear; retries use their latest diagnosis");
                Check(report,view.reviews.All(r=>r.feedback!="legacy cannot override"&&r.feedback!="bad identity"),"Old or malformed identities cannot be presented as captured question context");
                team.rewards.adminUsed=true;view=ArchiveLearningRecap.Build(catalog,team,1,"Tester");
                Check(report,view.practice&&view.summary.StartsWith("ADMIN PRACTICE"),"Admin fixture is clearly labelled");
                team.rewards.adminUsed=false;team.rewards.online=false;view=ArchiveLearningRecap.Build(catalog,team,1,"Tester");
                Check(report,view.practice&&view.summary.StartsWith("OFFLINE PRACTICE"),"Offline fixture is clearly labelled");
                team.survival.defeated=true;view=ArchiveLearningRecap.Build(catalog,team,1,"Tester");
                Check(report,view.outcome=="defeat","Defeat is preserved rather than mislabelled as victory");
                team=Team();for(int topic=0;topic<10;topic++)team.solved.Add(new ArchiveSolvedMilestone{topic=topic,contributors=new[]{1},crystalContributors=new[]{1}});
                view=ArchiveLearningRecap.Build(catalog,team,1,"Tester");
                Check(report,view.suggestions.Length==0,"An all-practiced match without personal mistakes invents no weakness or mandatory remedial task");
                view=ArchiveLearningRecap.Build(null,team,1,"Tester");Check(report,view.reviews.Length==0&&!string.IsNullOrWhiteSpace(view.summary),"Missing catalog preserves truthful aggregate records without reconstructing prompts");
            }
            catch(Exception exception){report.errors.Add(exception.ToString());}
            report.passed=report.errors.Count==0;
            Directory.CreateDirectory("Temp/ArchiveLearningRecap");File.WriteAllText("Temp/ArchiveLearningRecap/Content.json",JsonUtility.ToJson(report,true));
            return report;
        }
        static ArchiveTeamState Team()=>new ArchiveTeamState
        {
            seed=7919,matchId="0123456789abcdef0123456789abcdef",
            stations=Enumerable.Range(0,10).Select(topic=>new ArchiveStationState{topic=topic}).ToArray(),
            rewards=new ArchiveMatchRewardState{online=true}
        };
        static ArchiveMistake Mistake(int topic,int round,int subphase,string feedback)=>new ArchiveMistake
        {actor=1,topic=topic,round=round,subphase=subphase,reviewVersion=1,reason="answer",reviewFeedback=feedback};
    }
}
