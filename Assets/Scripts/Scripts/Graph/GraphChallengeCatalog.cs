using System.Collections.Generic;
using UnityEngine;

namespace LogicLegends.Graph
{
    [CreateAssetMenu(menuName = "Logic Legends/Graph Challenge Catalog")]
    public sealed class GraphChallengeCatalog : ScriptableObject
    {
        public const int ChallengesPerRun = 5;
        public List<GraphChallengeData> challenges = new List<GraphChallengeData>();

        /// <summary>Shuffle the pool once and take five distinct challenges for this run.</summary>
        public List<int> SelectRun(System.Random random)
        {
            if (challenges.Count < ChallengesPerRun)
                throw new System.InvalidOperationException("Graph needs at least five challenges in its catalog.");
            var indices = new List<int>();
            for (int i = 0; i < challenges.Count; i++) indices.Add(i);
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int swap = indices[i]; indices[i] = indices[j]; indices[j] = swap;
            }
            indices.RemoveRange(ChallengesPerRun, indices.Count - ChallengesPerRun);
            return indices;
        }
    }
}
