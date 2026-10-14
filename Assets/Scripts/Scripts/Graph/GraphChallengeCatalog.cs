using System;
using System.Collections.Generic;
using UnityEngine;

namespace LogicLegends.Graph
{
    [Serializable]
    public sealed class GraphChallengeVariantSet
    {
        public List<GraphChallengeData> variants = new List<GraphChallengeData>();
    }

    [CreateAssetMenu(menuName = "Logic Legends/Graph Challenge Catalog")]
    public sealed class GraphChallengeCatalog : ScriptableObject
    {
        public const int ChallengesPerRun = 5;
        public List<GraphChallengeData> challenges = new List<GraphChallengeData>();
        public List<GraphChallengeVariantSet> variantSets = new List<GraphChallengeVariantSet>();

        /// <summary>Pick one problem for each fixed, progressively harder challenge type.</summary>
        public List<int> SelectRun(System.Random random, IReadOnlyList<int> previous = null)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (variantSets == null || variantSets.Count != ChallengesPerRun)
                throw new InvalidOperationException("Graph needs five ordered challenge variant sets.");
            var indices = new List<int>(ChallengesPerRun);
            for (int round = 0; round < ChallengesPerRun; round++)
            {
                var set = variantSets[round];
                if (set == null || set.variants == null || set.variants.Count != 10 || set.variants.Exists(v => v == null))
                    throw new InvalidOperationException("Every Graph challenge needs ten saved variants.");
                int last = previous != null && previous.Count > round ? previous[round] : -1;
                if (last < 0 || last >= set.variants.Count) last = -1;
                int pick = random.Next(set.variants.Count - (last >= 0 ? 1 : 0));
                if (last >= 0 && pick >= last) pick++;
                indices.Add(pick);
            }
            return indices;
        }
    }
}
