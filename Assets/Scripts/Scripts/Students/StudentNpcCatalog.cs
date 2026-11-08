using System;
using UnityEngine;

namespace LogicLegends.Students
{
    [CreateAssetMenu(menuName="Logic Legends/Student NPC configuration")]
    public sealed class StudentNpcCatalog : ScriptableObject
    {
        [Serializable] public sealed class Skin
        {
            public string id, displayName;
            public GameObject prefab;
        }
        public Skin[] skins = Array.Empty<Skin>();
        [Min(.1f)] public float walkSpeed = 3.2f;
        [Min(.1f)] public float runSpeed = 6.4f;
        [Min(5)] public float longTripDistance = 22;
        [Min(1)] public float slowNearDestination = 5;
        [Min(.1f)] public float acceleration = 4;
        [Range(.1f,1)] public float stepHeight = .7f;
        [Min(.2f)] public float radius = .75f;
        [Min(1)] public float height = 5.5f;
        [Min(1)] public float gridSpacing = 2.5f;
        [Min(256)] public int maximumCells = 10000;
        [Min(8)] public int buildCellsPerFrame = 12;
        [Min(1)] public float talkRange = 5;
        [Min(300)] public float dialogueMaxWidth = 800;
        [Min(1)] public float interactionClearance = 4;
        [Range(0,1)] public float hintChance = .75f;
        public Vector2 restSeconds = new Vector2(1.4f,4);
    }

    // Pure policy and ledger are independent of spawning, UI and target lifetime.
    public static class StudentNpcPolicy
    {
        public static int Population(int trial, int challenge, System.Random random)
        {
            if(trial==9 && challenge==1)return 0;
            if(trial==2 || trial==3)return random.Next(2,4);
            if(trial==1 || trial==9)return random.Next(1,3);
            return random.Next(0,3);
        }
        public static string PopulationKey(int trial,int challenge,string question)
            => trial==1 || trial==2 || trial==3 ? "trial" : trial==9 ? challenge+"/"+question : challenge.ToString();
    }
    public sealed class StudentHintLedger
    {
        readonly System.Collections.Generic.HashSet<string> used = new System.Collections.Generic.HashSet<string>();
        public bool Used(string identity,int challenge)=>used.Contains(challenge+":"+identity);
        public bool Consume(string identity,int challenge)=>used.Add(challenge+":"+identity);
    }
}
