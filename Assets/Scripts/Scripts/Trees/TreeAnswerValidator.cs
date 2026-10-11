using System.Collections.Generic;
using System.Linq;

namespace LogicLegends.Trees
{
    public static class TreeAnswerValidator
    {
        public static bool ValidEdges(TreeChallengeData c, IList<TreeEdgeData> edges)
        {
            var ids = new HashSet<string>(c.nodes.Select(n => n.id));
            return ids.Count == c.nodes.Count && ids.Count > 0 && edges.All(e => e != null && e.source != e.destination && ids.Contains(e.source) && ids.Contains(e.destination)) && edges.Select(e => e.Key(c.Directed)).Distinct().Count() == edges.Count;
        }
        public static bool Connected(TreeChallengeData c, IList<TreeEdgeData> edges)
        {
            if (!ValidEdges(c, edges)) return false;
            var visited = new HashSet<string> { c.nodes[0].id };
            var queue = new Queue<string>(visited);
            while(queue.Count > 0)
            {
                string id=queue.Dequeue();
                foreach(var edge in edges.Where(e=>e.source==id || e.destination==id))
                { string next=edge.source==id?edge.destination:edge.source; if(visited.Add(next))queue.Enqueue(next); }
            }
            return visited.Count==c.nodes.Count;
        }
        public static bool Acyclic(TreeChallengeData c, IList<TreeEdgeData> edges)
        {
            if(!ValidEdges(c,edges))return false;
            var parent=c.nodes.ToDictionary(n=>n.id,n=>n.id);
            System.Func<string,string> find=null; find=id=>parent[id]==id?id:find(parent[id]);
            foreach(var edge in edges) {string a=find(edge.source),b=find(edge.destination);if(a==b)return false;parent[a]=b;}
            return true;
        }
        public static bool Rooted(TreeChallengeData c, IList<TreeEdgeData> edges) => c.nodes.Any(n=>n.id==c.root) && Connected(c,edges) && Acyclic(c,edges) && edges.All(e=>e.destination!=c.root) && c.nodes.Where(n=>n.id!=c.root).All(n=>edges.Count(e=>e.destination==n.id)==1);
        public static int Depth(TreeChallengeData c, IList<TreeEdgeData> edges, string target)
        {
            if(!Rooted(c,edges))return -1;
            var depth=new Dictionary<string,int>{{c.root,0}};var queue=new Queue<string>();queue.Enqueue(c.root);
            while(queue.Count>0) {string id=queue.Dequeue();foreach(var e in edges.Where(e=>e.source==id)) {depth[e.destination]=depth[id]+1;queue.Enqueue(e.destination);}}
            return depth.TryGetValue(target,out int value)?value:-1;
        }
        public static int Height(TreeChallengeData c,IList<TreeEdgeData> edges) => Rooted(c,edges)?c.nodes.Max(n=>Depth(c,edges,n.id)):-1;
        public static bool Validate(TreeChallengeData c,IList<TreeEdgeData> edges,string selected,int? answer,out string reason)
        {
            reason="Check the objective and try again. Your work is kept.";
            if(!ValidEdges(c,edges)) {reason="The tree contains invalid or duplicate edges.";return false;}
            switch(c.type)
            {
                case TreeChallengeType.Identify:
                    if(!c.nodes.Any(n=>n.id==selected)) {reason="Select a node first.";return false;}
                    switch(c.identification)
                    {
                        case TreeIdentification.Parent:return edges.Any(e=>e.source==selected&&e.destination==c.target);
                        case TreeIdentification.Child:return edges.Any(e=>e.source==c.target&&e.destination==selected);
                        case TreeIdentification.Leaf:return !edges.Any(e=>e.source==selected);
                        default:return selected==c.root&&!edges.Any(e=>e.destination==selected);
                    }
                case TreeChallengeType.Depth:case TreeChallengeType.Height:
                    int expected=c.type==TreeChallengeType.Depth?Depth(c,edges,c.target):Height(c,edges);
                    reason="Count edges, starting with depth 0 at the root.";return expected>=0&&answer==expected;
                case TreeChallengeType.Spanning:
                    var original=new HashSet<string>(c.initial.Select(e=>e.Key(false)));
                    reason="Keep all vertices connected with exactly V - 1 edges and no cycle.";
                    return edges.Count==c.nodes.Count-1&&edges.All(e=>original.Contains(e.Key(false)))&&Connected(c,edges)&&Acyclic(c,edges);
                default:
                    reason="Match every required parent-child link, with no extra edges.";
                    return Rooted(c,edges)&&new HashSet<string>(c.required.Select(e=>e.Key(true))).SetEquals(edges.Select(e=>e.Key(true)));
            }
        }
    }
}
