using System;
using System.Collections.Generic;
using UnityEngine;

namespace LogicLegends.Trees
{
    public enum TreeChallengeType { Identify, Build, Repair, Depth, Height, Spanning }
    public enum TreeIdentification { Root, Parent, Child, Leaf }
    [Serializable] public class TreeNodeData
    {
        public string id;
        public Vector2 position;
        public TreeNodeData(string name, float x, float y) { id = name; position = new Vector2(x, y); }
    }
    [Serializable] public class TreeEdgeData
    {
        public string source, destination;
        public bool removable = true;
        public TreeEdgeData(string a, string b) { source = a; destination = b; }
        public string Key(bool directed) => directed || string.CompareOrdinal(source, destination) < 0 ? source + ":" + destination : destination + ":" + source;
    }
    [Serializable] public class TreeChallengeData
    {
        public string title, objective, lesson, root = "A", target;
        public TreeChallengeType type;
        public TreeIdentification identification;
        public List<TreeNodeData> nodes = new List<TreeNodeData>();
        public List<TreeEdgeData> initial = new List<TreeEdgeData>(), required = new List<TreeEdgeData>();
        public bool Editable => type == TreeChallengeType.Build || type == TreeChallengeType.Repair || type == TreeChallengeType.Spanning;
        public bool Directed => type != TreeChallengeType.Spanning;
    }
    [CreateAssetMenu(menuName = "Logic Legends/Trees Challenge Catalog")]
    public sealed class TreeChallengeCatalog : ScriptableObject
    {
        public List<TreeChallengeData> challenges = new List<TreeChallengeData>();
        public static List<TreeChallengeData> Defaults()
        {
            var five = new List<TreeNodeData> { N("A",0,1),N("B",-.5f,.5f),N("C",.5f,.5f),N("D",-.8f,0),N("E",-.2f,0) };
            var required = new List<TreeEdgeData> { E("A","B"),E("A","C"),E("B","D"),E("B","E") };
            return new List<TreeChallengeData> {
                new TreeChallengeData { title="Tree terminology", type=TreeChallengeType.Identify, identification=TreeIdentification.Root, objective="Identify the root node.", lesson="The root has no parent. A parent points to its children. A leaf has no children.\n\nTap one node, then tap CONFIRM on the board.", nodes=new List<TreeNodeData>{N("A",0,1),N("B",-.5f,.5f),N("C",.5f,.5f),N("D",-.8f,0),N("E",-.3f,0),N("F",.3f,0),N("G",.8f,0)}, initial=new List<TreeEdgeData>{E("A","B"),E("A","C"),E("B","D"),E("B","E"),E("C","F"),E("C","G")} },
                new TreeChallengeData { title="Build a tree", type=TreeChallengeType.Build, objective="Root A. Connect A to B and C; connect B to D and E.", lesson="Select the parent first, then the child. Tap CONNECT. Each non-root node needs exactly one parent.\n\nUse REMOVE to correct a connection. Tap CONFIRM when the complete structure is ready.", nodes=five, required=required },
                new TreeChallengeData { title="Repair the broken tree", type=TreeChallengeType.Repair, objective="Repair the tree so E is a child of B. Keep the other parent links.", lesson="Select the endpoints of an incorrect branch and tap REMOVE. Select the new parent and child, then CONNECT.\n\nA correction must remove the old parent link before adding the new one.", nodes=five, required=required, initial=new List<TreeEdgeData>{E("A","B"),E("A","C"),E("B","D"),E("C","E")} },
                new TreeChallengeData { title="Tree depth", type=TreeChallengeType.Depth, target="F", objective="What is the depth of node F?", lesson="Root depth and level are 0. Depth counts edges from the root to a node. Height is the longest root-to-leaf path, also counted in edges.\n\nChoose a number, then confirm.", nodes=new List<TreeNodeData>{N("A",0,1),N("B",-.5f,.67f),N("C",.5f,.67f),N("D",-.7f,.33f),N("E",-.1f,.33f),N("F",-.8f,0)},initial=new List<TreeEdgeData>{E("A","B"),E("A","C"),E("B","D"),E("B","E"),E("D","F")} },
                new TreeChallengeData { title="Spanning tree", type=TreeChallengeType.Spanning, objective="Remove edges to connect all four vertices without a cycle. Keep exactly 3 edges.", lesson="A spanning tree includes every original vertex, stays connected, and has no cycle. With V vertices it has V - 1 edges.\n\nSeveral answers are valid. Select branch endpoints and REMOVE until your tree is ready.",nodes=new List<TreeNodeData>{N("A",0,1),N("B",-.8f,.5f),N("C",.8f,.5f),N("D",0,0)},initial=new List<TreeEdgeData>{E("A","B"),E("A","C"),E("B","C"),E("B","D"),E("C","D")} }
            };
        }
        static TreeNodeData N(string id,float x,float y) => new TreeNodeData(id,x,y);
        static TreeEdgeData E(string a,string b) => new TreeEdgeData(a,b);
    }
}
