using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    [Serializable] public sealed class ArchiveMonsterChallenge
    {
        public string id,module,prompt,explanation;
        public int topic,correctIndex;
        public int[] slides;
        public string[] options,hints;
    }
    /// <summary>One authored, module-referenced bank shared by both monster encounters.</summary>
    public static class ArchiveMonsterChallengeBank
    {
        public const int CurrentVersion=1,RequiredCount=50;
        [Serializable] sealed class Document{public int version;public ArchiveMonsterChallenge[] questions;}
        static Document document;
        public static int Count{get{EnsureLoaded();return document.questions.Length;}}
        public static ArchiveMonsterChallenge Get(int index)
        {
            EnsureLoaded();
            if(index<0||index>=document.questions.Length)throw new ArgumentOutOfRangeException(nameof(index));
            return document.questions[index];
        }
        static void EnsureLoaded()
        {
            if(document!=null)return;
            var asset=Resources.Load<TextAsset>("ArchiveMonsterChallenges");
            if(asset==null)throw new InvalidOperationException("The DM1 monster challenge bank is missing from Resources.");
            var data=JsonUtility.FromJson<Document>(asset.text);
            if(data==null||data.version!=CurrentVersion||data.questions==null||data.questions.Length!=RequiredCount)
                throw new InvalidOperationException("The DM1 monster bank must contain exactly 50 versioned questions.");
            var ids=new HashSet<string>();var prompts=new HashSet<string>();
            foreach(var q in data.questions)
            {
                if(q==null||string.IsNullOrWhiteSpace(q.id)||!ids.Add(q.id)||string.IsNullOrWhiteSpace(q.prompt)||!prompts.Add(q.prompt)||
                    q.topic<0||q.topic>=10||string.IsNullOrWhiteSpace(q.module)||q.slides==null||q.slides.Length==0||q.slides.Any(s=>s<1)||
                    q.options==null||q.options.Length!=4||q.options.Any(string.IsNullOrWhiteSpace)||q.options.Distinct().Count()!=4||q.correctIndex<0||q.correctIndex>=4||
                    string.IsNullOrWhiteSpace(q.explanation)||q.hints==null||q.hints.Length!=3||q.hints.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidOperationException("Malformed or duplicate DM1 monster question: "+(q?.id??"null"));
            }
            if(Enumerable.Range(0,10).Any(topic=>data.questions.Count(q=>q.topic==topic)!=5))
                throw new InvalidOperationException("The monster bank must contain five questions for each DM1 topic.");
            document=data;
        }
    }
}
