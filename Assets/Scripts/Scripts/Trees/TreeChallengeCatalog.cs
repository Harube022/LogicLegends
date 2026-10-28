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
    [Serializable] public class TreeChallengeVariantSet
    {
        public List<TreeChallengeData> variants = new List<TreeChallengeData>();
    }
    [CreateAssetMenu(menuName = "Logic Legends/Trees Challenge Catalog")]
    public sealed class TreeChallengeCatalog : ScriptableObject
    {
        public List<TreeChallengeData> challenges = new List<TreeChallengeData>();
        public List<TreeChallengeVariantSet> variantSets = new List<TreeChallengeVariantSet>();
        public static List<TreeChallengeData> Defaults()
        {
            var five = new List<TreeNodeData> { N("A",0,1),N("B",-.5f,.5f),N("C",.5f,.5f),N("D",-.8f,0),N("E",-.2f,0) };
            var required = new List<TreeEdgeData> { E("A","B"),E("A","C"),E("B","D"),E("B","E") };
            return new List<TreeChallengeData> {
                new TreeChallengeData { title="Tree terminology", type=TreeChallengeType.Identify, identification=TreeIdentification.Root, objective="Identify the root node.", lesson="The root has no parent. A parent points to its children. A leaf has no children.\n\nTap one node, then tap CONFIRM on the board.", nodes=new List<TreeNodeData>{N("A",0,1),N("B",-.5f,.5f),N("C",.5f,.5f),N("D",-.8f,0),N("E",-.3f,0),N("F",.3f,0),N("G",.8f,0)}, initial=new List<TreeEdgeData>{E("A","B"),E("A","C"),E("B","D"),E("B","E"),E("C","F"),E("C","G")} },
                new TreeChallengeData { title="Build a tree", type=TreeChallengeType.Build, objective="Root A. Connect A to B and C; connect B to D and E.", lesson="Select parent, then child. Tap CONNECT.\nEach non-root node needs one parent.\nREMOVE fixes a link. CONFIRM checks the whole tree.", nodes=five, required=required },
                new TreeChallengeData { title="Repair the broken tree", type=TreeChallengeType.Repair, objective="Repair the tree so E is a child of B. Keep the other parent links.", lesson="Select the wrong branch endpoints. Tap REMOVE.\nSelect the new parent and child. Tap CONNECT.\nRemove the old parent before adding the new one.", nodes=five, required=required, initial=new List<TreeEdgeData>{E("A","B"),E("A","C"),E("B","D"),E("C","E")} },
                new TreeChallengeData { title="Tree depth", type=TreeChallengeType.Depth, target="F", objective="What is the depth of node F?", lesson="Root depth and level are 0. Depth counts edges from the root to a node. Height is the longest root-to-leaf path, also counted in edges.\n\nChoose a number, then confirm.", nodes=new List<TreeNodeData>{N("A",0,1),N("B",-.5f,.67f),N("C",.5f,.67f),N("D",-.7f,.33f),N("E",-.1f,.33f),N("F",-.8f,0)},initial=new List<TreeEdgeData>{E("A","B"),E("A","C"),E("B","D"),E("B","E"),E("D","F")} },
                new TreeChallengeData { title="Spanning tree", type=TreeChallengeType.Spanning, objective="Remove edges to connect all four vertices without a cycle. Keep exactly 3 edges.", lesson="Keep every vertex connected, with no cycle.\nV vertices need V − 1 edges. Several answers are valid.\nSelect branch endpoints and REMOVE to form a spanning tree.",nodes=new List<TreeNodeData>{N("A",0,1),N("B",-.8f,.5f),N("C",.8f,.5f),N("D",0,0)},initial=new List<TreeEdgeData>{E("A","B"),E("A","C"),E("B","C"),E("B","D"),E("C","D")} }
            };
        }
        static TreeNodeData N(string id,float x,float y) => new TreeNodeData(id,x,y);
        static TreeEdgeData E(string a,string b) => new TreeEdgeData(a,b);
    }
}
