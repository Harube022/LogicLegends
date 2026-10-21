using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Trees;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Adapts the solo board's edit rules. All mutable work remains in ArchiveTopicSession.</summary>
    public static class ArchiveTreeRules
    {
        public static bool NumberQuestion(ArchiveQuestion q)=>q.tree.type==TreeChallengeType.Depth||q.tree.type==TreeChallengeType.Height;
        public static Vector3 Node(ArchiveQuestion q,int i)=>new Vector3(q.nodes[i].x,0,q.nodes[i].y)+new Vector3(ArchiveTopicStation.GraphOffset.x,0,ArchiveTopicStation.GraphOffset.y);
        public static int Encode(ArchiveQuestion q,TreeEdgeData edge)=>Array.IndexOf(q.nodeIds,edge.source)*100+Array.IndexOf(q.nodeIds,edge.destination);
        public static bool Decode(ArchiveQuestion q,IEnumerable<int> links,out List<TreeEdgeData> edges){edges=new List<TreeEdgeData>();foreach(int link in links){if(link<0||link/100>=q.nodeIds.Length||link%100>=q.nodeIds.Length||link/100==link%100)return false;string a=q.nodeIds[link/100],b=q.nodeIds[link%100];var original=q.tree.initial.FirstOrDefault(e=>e.Key(false)==new TreeEdgeData(a,b).Key(false));edges.Add(new TreeEdgeData(a,b){removable=original==null||original.removable});}return TreeAnswerValidator.ValidEdges(q.tree,edges);}
        public static bool CanEdit(ArchiveQuestion q,ArchiveStationState s,int first,int second,bool remove,out string reason){
            reason="Select two different nodes: parent first, then child.";if(q.tree==null||!q.tree.Editable||first<0||second<0||first>=q.nodeIds.Length||second>=q.nodeIds.Length||first==second||!Decode(q,s.links,out var edges))return false;
            var candidate=new TreeEdgeData(q.nodeIds[first],q.nodeIds[second]);var existing=edges.FirstOrDefault(e=>e.Key(remove?false:q.tree.Directed)==candidate.Key(remove?false:q.tree.Directed));
            if(remove){reason="No removable branch joins those nodes.";return existing!=null&&existing.removable;}
            if(existing!=null){reason="That connection already exists.";return false;}
            if(q.tree.type==TreeChallengeType.Spanning&&!q.tree.initial.Any(e=>e.Key(false)==candidate.Key(false))){reason="Use only edges from the original graph.";return false;}
            if(q.tree.Directed&&(candidate.destination==q.tree.root||edges.Any(e=>e.destination==candidate.destination))){reason="The root has no parent; every other node has only one. Remove the old parent first.";return false;}
            if(q.tree.Directed&&!TreeAnswerValidator.Acyclic(q.tree,edges.Concat(new[]{candidate}).ToList())){reason="That branch would create a cycle.";return false;}
            reason="Connect "+candidate.source+" → "+candidate.destination+".";return true;
        }
        public static bool Edit(ArchiveQuestion q,ArchiveStationState s,int first,int second,bool remove,out string reason){if(!CanEdit(q,s,first,second,remove,out reason))return false;int link=first*100+second;if(remove){if(!s.links.Remove(link))s.links.Remove(second*100+first);reason="Branch removed. Select the next pair.";}else{s.links.Add(link);reason="Connected "+q.nodeIds[first]+" → "+q.nodeIds[second]+". Select the next pair.";}return true;}
        public static string Reveal(ArchiveQuestion q,ArchiveStationState s)=>q.title+"\n"+(NumberQuestion(q)?(q.tree.type==TreeChallengeType.Height?"Tree height":"Depth of "+q.tree.target)+" = "+s.flags:q.tree.type==TreeChallengeType.Identify?"Accepted node: "+q.nodeIds[s.selection]:"Accepted branches:\n"+string.Join("\n",s.links.Select(link=>q.nodeIds[link/100]+(q.tree.Directed?" → ":" — ")+q.nodeIds[link%100])))+"\n\n"+q.tree.lesson;
    }
}
