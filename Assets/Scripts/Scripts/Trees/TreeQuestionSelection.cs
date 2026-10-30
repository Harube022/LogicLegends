using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace LogicLegends.Trees
{
    /// <summary>One foundation question, then one structurally harder question. Picks stay fixed during retries.</summary>
    public static class TreeQuestionSelection
    {
        public static int[] SelectPair(IList<TreeChallengeData> bank, int previous=-1)
        {
            var easy=Enumerable.Range(0,bank.Count).Where(i=>bank[i].difficulty==1).ToList();
            var hard=Enumerable.Range(0,bank.Count).Where(i=>bank[i].difficulty==2).ToList();
            if(easy.Count==0||hard.Count==0)throw new InvalidOperationException("Trees requires both difficulty groups.");
            if(easy.Count>1)easy.Remove(previous);
            return new[]{easy[UnityEngine.Random.Range(0,easy.Count)],hard[UnityEngine.Random.Range(0,hard.Count)]};
        }
    }
}
