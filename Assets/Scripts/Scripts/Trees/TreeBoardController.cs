using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace LogicLegends.Trees
{
    public sealed class TreeBoardController : MonoBehaviour
    {
        public TreeNode nodePrefab;
        public LineRenderer edgePrefab;
        public List<TreeEdgeData> Edges { get; private set; } = new List<TreeEdgeData>();
        public string First { get; private set; }
        public string Second { get; private set; }
        public TreeChallengeData Challenge { get; private set; }
        public readonly List<TreeNode> Nodes = new List<TreeNode>();
        readonly List<LineRenderer> lines=new List<LineRenderer>();
        public void Build(TreeChallengeData data)
        {
            foreach(var node in GetComponentsInChildren<TreeNode>(true)) {node.gameObject.SetActive(false);Destroy(node.gameObject);}Nodes.Clear();
            foreach(var line in GetComponentsInChildren<LineRenderer>(true)){line.gameObject.SetActive(false);Destroy(line.gameObject);}lines.Clear();
            Challenge=data;First=Second=null;Edges=data.initial.Select(e=>new TreeEdgeData(e.source,e.destination){removable=e.removable}).ToList();
            foreach(var n in data.nodes) {var node=Instantiate(nodePrefab,transform);node.id=n.id;node.label.text=n.id;node.transform.localScale*=.58f;node.transform.localPosition=Position(n);Nodes.Add(node);}
            Draw();
        }
        // Same coordinates as the InformationBoard canvas. Keep the diagram below its question.
        public static Vector3 Position(TreeNodeData n)=>new Vector3(n.position.x*4.7f,(-110+n.position.y*330)*.0094f,-.07f);
        public void Select(string id)
        {
            if(!Nodes.Any(n=>n.id==id))return;
            if(!Challenge.Editable) {First=id;Second=null;}
            else if(First==null||Second!=null) {First=id;Second=null;}
            else if(First!=id)Second=id;
            RefreshSelection();
        }
        public void ClearSelection(){First=Second=null;RefreshSelection();}
        public void RefreshSelection(){foreach(var node in Nodes)node.SetSelected(node.id==First||node.id==Second,node.id==Challenge.target);}
        public bool Edit(bool remove,out string message)
        {
            message="Select two different nodes: parent first, then child.";
            if(!Challenge.Editable||First==null||Second==null)return false;
            var candidate=new TreeEdgeData(First,Second);
            var existing=Edges.FirstOrDefault(e=>e.Key(Challenge.Directed)==candidate.Key(Challenge.Directed));
            // Removal accepts either endpoint order, even on a directed branch.
            if(remove)existing=Edges.FirstOrDefault(e=>e.Key(false)==candidate.Key(false));
            if(remove)
            {
                if(existing==null||!existing.removable){message="No removable branch joins those nodes.";return false;}
                Edges.Remove(existing);message="Branch removed. Select the next pair.";
            }
            else
            {
                if(existing!=null){message="That connection already exists.";return false;}
                if(Challenge.type==TreeChallengeType.Spanning&&!Challenge.initial.Any(e=>e.Key(false)==candidate.Key(false))){message="Use only edges from the original graph.";return false;}
                if(Challenge.Directed&&(Second==Challenge.root||Edges.Any(e=>e.destination==Second))){message="The root has no parent; every other node has only one. Remove the old parent first.";return false;}
                var proposed=Edges.Concat(new[]{candidate}).ToList();
                if(Challenge.Directed&&!TreeAnswerValidator.Acyclic(Challenge,proposed)){message="That branch would create a cycle.";return false;}
                Edges.Add(candidate);message="Connected "+First+" to "+Second+". Select the next pair.";
            }
            ClearSelection();Draw();return true;
        }
        void Draw()
        {
            foreach(var line in lines){line.gameObject.SetActive(false);Destroy(line.gameObject);}lines.Clear();
            foreach(var edge in Edges)
            {
                var line=Instantiate(edgePrefab,transform);line.name=edge.source+" to "+edge.destination;line.useWorldSpace=false;line.positionCount=2;
                line.SetPosition(0,Position(Challenge.nodes.First(n=>n.id==edge.source))+Vector3.back*.08f);
                line.SetPosition(1,Position(Challenge.nodes.First(n=>n.id==edge.destination))+Vector3.back*.08f);lines.Add(line);
            }
            RefreshSelection();
        }
    }
}
