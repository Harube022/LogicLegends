using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LogicLegends.OpenArchive.Topics
{
    [Serializable] public sealed class ArchiveLearningRecapData
    {
        public string matchId="",playerName="",outcome="",summary="",topicsText="",teamworkText="";
        public int actor;
        public bool practice;
        public ArchiveLearningRecapReview[] reviews=Array.Empty<ArchiveLearningRecapReview>();
        public ArchiveLearningRecapSuggestion[] suggestions=Array.Empty<ArchiveLearningRecapSuggestion>();
    }
    [Serializable] public sealed class ArchiveLearningRecapReview
    {
        public string title="",prompt="",feedback="",reasoning="",module="";
    }
    [Serializable] public sealed class ArchiveLearningRecapSuggestion
    {
        public string title="",reason="",concept="",module="";
    }

    /// <summary>A detached local reading model over the existing confirmed match records. It owns no score, timer, progress or outcome.</summary>
    public static class ArchiveLearningRecap
    {
        const int TopicCount=10,MaximumReviews=3;
        static bool Topic(int topic)=>topic>=0&&topic<TopicCount;
        static string Name(int topic)=>"T"+(topic+1)+" · "+ArchiveTopicCatalog.Names[topic];
        static string Identity(ArchiveMistake mistake)=>mistake.topic+":"+mistake.round+":"+mistake.subphase+":"+mistake.resetSerial;
        static bool PersonalAnswer(ArchiveMistake mistake,int actor)=>mistake!=null&&actor!=0&&mistake.actor==actor&&Topic(mistake.topic)&&mistake.reason=="answer";
        static bool CurrentReceipt(ArchiveTeamState team,ArchiveSolvedMilestone receipt)=>receipt!=null&&Topic(receipt.topic)&&
            team.stations!=null&&team.stations.Length>receipt.topic&&team.stations[receipt.topic]!=null&&
            receipt.resetSerial==team.stations[receipt.topic].testResetSerial&&receipt.round>=0&&receipt.round<ArchiveTopicCatalog.RoundCount(receipt.topic)&&
            receipt.subphase>=0&&receipt.subphase<(receipt.topic==2?2:1);

        public static ArchiveLearningRecapData Build(ArchiveTopicCatalog catalog,ArchiveTeamState team,int actor,string actorName)
        {
            var result=new ArchiveLearningRecapData {actor=actor,playerName=CleanName(actorName)};
            if(team==null)
            {
                result.outcome="unavailable";result.summary="No completed match records are available.";
                return result;
            }
            result.matchId=team.matchId??"";
            result.outcome=team.survival?.defeated==true?"defeat":team.exit?.won==true?"victory":"unfinished";
            result.practice=team.rewards==null||!team.rewards.online||team.rewards.adminUsed||(team.crystals?.Any(c=>c!=null&&c.adminBypass)??false);
            var record=team.actors?.FirstOrDefault(a=>a!=null&&a.actor==actor);
            var mistakes=(team.mistakes??new List<ArchiveMistake>()).Where(m=>m!=null).ToArray();
            var personal=mistakes.Where(m=>PersonalAnswer(m,actor)).ToArray();
            var solved=(team.solved??new List<ArchiveSolvedMilestone>()).Where(s=>CurrentReceipt(team,s))
                .GroupBy(s=>s.topic+":"+s.round+":"+s.subphase).Select(g=>g.First()).ToArray();
            var worked=solved.Where(s=>(s.crystalContributors??Array.Empty<int>()).Contains(actor)&&actor!=0).ToArray();
            var practiced=new HashSet<int>(solved.Where(s=>actor!=0&&(s.contributors??Array.Empty<int>()).Contains(actor)).Select(s=>s.topic));
            practiced.UnionWith(worked.Select(s=>s.topic));practiced.UnionWith(personal.Select(m=>m.topic));
            if(actor!=0&&team.stations!=null)
                foreach(var station in team.stations.Where(s=>s!=null&&Topic(s.topic)))
                    if(station.workCredits?.Any(w=>w!=null&&w.actor==actor)==true)practiced.Add(station.topic);
            if(actor!=0&&team.learningHints!=null)
                foreach(var hint in team.learningHints.Where(h=>h!=null&&h.actor==actor&&Topic(h.topic)&&h.level>0))practiced.Add(hint.topic);
            int attempts=Math.Max(0,record?.answers??0),correct=Math.Min(attempts,Math.Max(0,record?.correctAnswers??0));
            int hints=Math.Max(0,record?.hintsUsed??0),timeouts=mistakes.Count(m=>m.actor==0&&m.reason=="timeout");
            int completed=(team.stations??Array.Empty<ArchiveStationState>()).Count(s=>s!=null&&s.phase==2);
            int restored=(team.crystals??new List<ArchiveCrystalState>()).Count(c=>c!=null&&c.fullyRestored&&!c.adminBypass);
            string practice=result.practice?(team.rewards?.adminUsed==true||team.crystals?.Any(c=>c!=null&&c.adminBypass)==true?"ADMIN PRACTICE · ":"OFFLINE PRACTICE · "):"";
            result.summary=practice+practiced.Count+" / 10 topics practiced\n"+
                "Team: "+completed+" / 10 topics completed · "+restored+" crystal"+(restored==1?"":"s")+" restored\n"+
                correct+" / "+attempts+" correct confirmed submissions · "+personal.Length+" personal incorrect submission"+(personal.Length==1?"":"s")+"\n"+
                hints+" hint step"+(hints==1?"":"s")+" requested · "+timeouts+" shared team timeout"+(timeouts==1?"":"s")+"\n"+
                "These are match activity records, not a mastery grade. Hint use carries no review penalty.";
            result.topicsText=TopicsText(team,actor,solved,worked,practiced,personal);
            result.teamworkText=TeamworkText(team,actor,worked.Length,correct);
            result.reviews=Reviews(catalog,team,actor,mistakes);
            result.suggestions=Suggestions(personal,practiced);
            return result;
        }
        static string CleanName(string value)
        {
            if(string.IsNullOrWhiteSpace(value))return "Player";
            value=value.Replace('\r',' ').Replace('\n',' ').Trim();
            return value.Length>64?value.Substring(0,64):value;
        }
        static string TopicsText(ArchiveTeamState team,int actor,ArchiveSolvedMilestone[] solved,ArchiveSolvedMilestone[] worked,HashSet<int> practiced,ArchiveMistake[] personal)
        {
            var text=new StringBuilder();
            for(int topic=0;topic<TopicCount;topic++)
            {
                if(topic>0)text.Append("\n\n");
                int total=ArchiveCrystalProgress.Total(topic),completed=solved.Count(s=>s.topic==topic),own=worked.Count(s=>s.topic==topic);
                var station=team.stations!=null&&team.stations.Length>topic?team.stations[topic]:null;
                text.Append(Name(topic)).Append('\n');
                text.Append("Team: ").Append(completed).Append('/').Append(total).Append(" accepted parts");
                if(station?.phase==2)text.Append(" · topic complete");
                text.Append("\nYou: ").Append(practiced.Contains(topic)?"practiced":"no personal practice record");
                text.Append(" · accepted parts with your puzzle work: ").Append(own);
                int wrong=personal.Count(m=>m.topic==topic);
                if(wrong>0)text.Append(" · incorrect submissions: ").Append(wrong);
            }
            return text.ToString();
        }
        static string TeamworkText(ArchiveTeamState team,int actor,int worked,int correct)
        {
            var support=(team.rewards?.support??new List<ArchiveRewardSupportCredit>()).Where(s=>s!=null&&actor!=0&&s.actor==actor&&s.target!=0&&s.target!=actor).ToArray();
            Func<string,int> count=kind=>support.Where(s=>s.kind==kind).Select(s=>s.target).Distinct().Count();
            return "Accepted parts with your puzzle work: "+worked+"\nCorrect confirmed submissions: "+correct+
                "\nTeammates healed: "+count("medkit")+"\nTeammates recharged: "+count("battery")+
                "\nTeammates revived: "+count("revive")+"\nTeammates who used your shared items: "+count("give")+
                "\n\nSupport counts distinct teammates per activity, not every item use. Shared items appear here after the recipient uses them.";
        }
        static ArchiveLearningRecapReview[] Reviews(ArchiveTopicCatalog catalog,ArchiveTeamState team,int actor,ArchiveMistake[] mistakes)
        {
            if(catalog==null||actor==0)return Array.Empty<ArchiveLearningRecapReview>();
            var result=new List<ArchiveLearningRecapReview>();var identities=new HashSet<string>();
            // Append order is the authoritative chronology; Photon time can wrap.
            for(int i=mistakes.Length-1;i>=0&&result.Count<MaximumReviews;i--)
            {
                var mistake=mistakes[i];
                if(!PersonalAnswer(mistake,actor)||mistake.reviewVersion!=1||string.IsNullOrWhiteSpace(mistake.reviewFeedback)||
                    mistake.round<0||mistake.round>=ArchiveTopicCatalog.RoundCount(mistake.topic)||
                    mistake.subphase<0||mistake.subphase>=(mistake.topic==2?2:1)||!identities.Add(Identity(mistake)))continue;
                try
                {
                    var question=ArchiveTopicQuestions.Build(catalog,mistake.topic,mistake.round,team.seed,mistake.subphase);
                    result.Add(new ArchiveLearningRecapReview
                    {
                        title=Name(mistake.topic)+" · "+ProblemLabel(mistake.topic,mistake.round,mistake.subphase),
                        prompt=QuestionContext(question),feedback=mistake.reviewFeedback,
                        reasoning=ArchiveLearningFeedback.Hint(mistake.topic,question,null,2),
                        module=ArchiveLearningFeedback.Reference(mistake.topic)+(string.IsNullOrWhiteSpace(question.moduleReference)?"":"\nQuestion scope: "+question.moduleReference)
                    });
                }
                catch(ArgumentException) { }
                catch(InvalidOperationException) { }
                catch(NullReferenceException) { }
                catch(IndexOutOfRangeException) { }
            }
            return result.ToArray();
        }
        static string ProblemLabel(int topic,int round,int subphase)
        {
            if(topic==6)return "Challenge "+(round/2+1)+" · Problem "+(round%2+1);
            if(topic==1)return "Table "+(round+1);
            if(topic==2)return "Challenge "+(round+1)+(subphase==0?" · Premises":" · Rule");
            return "Challenge "+(round+1);
        }
        static string QuestionContext(ArchiveQuestion question)
        {
            var text=new StringBuilder(question.prompt??"");
            if(question.graph!=null)
            {
                text.Append("\nGiven vertices: ").Append(string.Join(", ",question.nodeIds));
                text.Append("\nGiven edges: ").Append(string.Join(", ",question.graph.edges.Select(e=>e.source+(e.directed?" → ":" — ")+e.destination)));
            }
            else if(question.tree!=null)
            {
                text.Append("\nGiven nodes: ").Append(string.Join(", ",question.nodeIds));
                text.Append("\nInitial branches: ").Append(string.Join(", ",question.tree.initial.Select(e=>e.source+(question.tree.Directed?" → ":" — ")+e.destination)));
            }
            else if(question.mapping!=null)
            {
                text.Append("\nDomain: {").Append(string.Join(", ",question.inputLabels)).Append("}");
                text.Append("\nCodomain: {").Append(string.Join(", ",question.outputLabels)).Append("}");
                text.Append("\nInitially shown pairs: ").Append(string.Join(", ",question.mapping.initialPairs.Select(p=>"("+p.domain+", "+p.codomain+")")));
            }
            // Supplied statements are part of a proof question, not its answer.
            // Preserve the actual shuffled supply order rather than exposing the
            // source arrays' original (sometimes solved) statement order.
            var order=question.itemOrder?.Length>0?question.itemOrder:Enumerable.Range(0,question.items?.Length??0).ToArray();
            bool heading=false;
            foreach(int index in order)
            {
                if(question.items==null||question.details==null||index<0||index>=question.items.Length||index>=question.details.Length||string.IsNullOrWhiteSpace(question.details[index]))continue;
                if(!heading){text.Append("\nGiven cards (supply order):");heading=true;}
                text.Append('\n').Append(question.items[index]).Append(": ").Append(question.details[index]);
            }
            const int maximum=5000;
            const string omitted="\n[More given material omitted here; revisit the course module for the complete context.]";
            return text.Length<=maximum?text.ToString():text.ToString(0,maximum-omitted.Length)+omitted;
        }
        static ArchiveLearningRecapSuggestion[] Suggestions(ArchiveMistake[] personal,HashSet<int> practiced)
        {
            var result=new List<ArchiveLearningRecapSuggestion>();var used=new HashSet<int>();
            foreach(var group in personal.GroupBy(m=>m.topic).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key))
            {
                int topic=group.Key;used.Add(topic);result.Add(Suggestion(topic,group.Count()+" personal incorrect submission"+(group.Count()==1?"":"s")+" recorded here. Review the reasoning before your next attempt."));
                if(result.Count==MaximumReviews)return result.ToArray();
            }
            for(int topic=0;topic<TopicCount&&result.Count<MaximumReviews;topic++)
                if(!used.Contains(topic)&&!practiced.Contains(topic))result.Add(Suggestion(topic,"No personal practice record for this topic in this match. Try its next challenge with a teammate."));
            return result.ToArray();
        }
        static ArchiveLearningRecapSuggestion Suggestion(int topic,string reason)=>new ArchiveLearningRecapSuggestion
        {title=Name(topic),reason=reason,concept=ArchiveLearningFeedback.Concept(topic),module=ArchiveLearningFeedback.Reference(topic)};
    }
}
