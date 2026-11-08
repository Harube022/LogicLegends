using System;
using System.Collections.Generic;
using UnityEngine;

namespace LogicLegends.Students
{
    // Skin IDs, not catalog ordering, bind the user's exact dialogue to a character.
    public static class StudentDialogueBank
    {
        [Serializable] public sealed class Character
        { public string id,emotion,personality;public string[] lines; }
        [Serializable] sealed class Document { public Character[] characters; }
        static Dictionary<string,Character> entries;
        public static IReadOnlyDictionary<string,Character> Entries {get{Load();return entries;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>entries=null;
        static void Load()
        {
            if(entries!=null)return;
            entries=new Dictionary<string,Character>(StringComparer.Ordinal);
            var asset=Resources.Load<TextAsset>("Students/StudentDialogueLines");
            if(asset==null){Debug.LogError("Student dialogue bank is missing.");return;}
            var document=JsonUtility.FromJson<Document>(asset.text);
            if(document?.characters==null)return;
            foreach(var character in document.characters)
                if(character!=null&&!string.IsNullOrEmpty(character.id))entries.Add(character.id,character);
        }
        public static string[] Lines(string identity)
        {Load();return entries.TryGetValue(identity,out var character)?character.lines:Array.Empty<string>();}
        public static int NextIndex(int count,int previous,System.Random random)
        {
            if(count<1)return -1;if(count==1)return 0;
            if(previous<0||previous>=count)return random.Next(count);
            int next=random.Next(count-1);return next>=previous?next+1:next;
        }
    }

    public static class StudentConversation
    {
        static readonly string[] lead={"Here is what I would check:","My study note for this task:","Let us focus on the current step:","One useful reminder:","Here is a way to get unstuck:","Try this next:","My checklist starts here:","This is the part I would revisit:","A small suggestion:","Let us break this down:","My tip for this question:","One thing to keep in mind:"};
        public static string Hint(int style,StudentTrialState state)
        {
            string concept=state.Action.Key.EndsWith("/book")?"":state.Concept;
            return lead[style%lead.Length]+"\n\n"+state.Action.Message+(string.IsNullOrEmpty(concept)?"":"\n\n"+concept);
        }
    }
}
