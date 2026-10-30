using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace LogicLegends.Trees
{
    /// <summary>Spread a diagram without modifying its nodes or connections.</summary>
    public static class TreeDiagramLayout
    {
        public static Dictionary<string,Vector3> Calculate(TreeChallengeData question)
        {
            var points=question.nodes.ToDictionary(n=>n.id,n=>TreeBoardController.Position(n));
            foreach(var row in question.nodes.GroupBy(n=>Mathf.RoundToInt(n.position.y*1000)))
            {
                var ordered=row.OrderBy(n=>n.position.x).ToArray();
                for(int i=1;i<ordered.Length;i++)
                {var p=points[ordered[i].id];p.x=Mathf.Max(p.x,points[ordered[i-1].id].x+1.35f);points[ordered[i].id]=p;}
                float over=Mathf.Max(0,points[ordered[ordered.Length-1].id].x-5.45f);
                foreach(var node in ordered){var p=points[node.id];p.x-=over;points[node.id]=p;}
            }
            return points;
        }
    }
}
