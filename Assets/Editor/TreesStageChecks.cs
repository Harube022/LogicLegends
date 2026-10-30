using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Trees;
using UnityEditor;
using UnityEngine;
public static class TreesStageChecks
{
    [MenuItem("Tools/Logic Legends/Validate Trees Logic")]
    public static void Run()
    {
        int checks=0;Action<bool,string> check=(valid,name)=>{if(!valid)throw new Exception("Trees validation failed: "+name);checks++;};
        var c=TreeChallengeCatalog.Defaults();string reason;
        check(TreeAnswerValidator.Validate(c[0],c[0].initial,"A",null,out reason),"root A");
        check(!TreeAnswerValidator.Validate(c[0],c[0].initial,"B",null,out reason),"non-root rejected");
        check(!TreeAnswerValidator.Validate(c[0],c[0].initial,null,null,out reason),"missing pick rejected");
        c[0].identification=TreeIdentification.Parent;c[0].target="D";
        check(TreeAnswerValidator.Validate(c[0],c[0].initial,"B",null,out reason),"parent D is B");
        c[0].identification=TreeIdentification.Child;c[0].target="B";
        check(TreeAnswerValidator.Validate(c[0],c[0].initial,"E",null,out reason),"E child of B");
        c[0].identification=TreeIdentification.Leaf;
        foreach(string id in new[]{"D","E","F","G"})check(TreeAnswerValidator.Validate(c[0],c[0].initial,id,null,out reason),"leaf "+id);
        check(!TreeAnswerValidator.Validate(c[0],c[0].initial,"A",null,out reason),"root not leaf");
        check(TreeAnswerValidator.Validate(c[1],c[1].required,null,null,out reason),"exact build");
        check(!TreeAnswerValidator.Validate(c[1],new List<TreeEdgeData>(),null,null,out reason),"disconnected build rejected");
        check(!TreeAnswerValidator.Validate(c[2],c[2].initial,null,null,out reason),"broken tree rejected");
        check(!TreeAnswerValidator.Validate(c[2],c[2].initial.Concat(new[]{new TreeEdgeData("B","E")}).ToList(),null,null,out reason),"extra old parent rejected");
        check(TreeAnswerValidator.Validate(c[2],c[2].required,null,null,out reason),"repaired tree");
        check(TreeAnswerValidator.Depth(c[3],c[3].initial,"F")==3,"calculated F depth 3");
        check(TreeAnswerValidator.Depth(c[3],c[3].initial,"A")==0,"root depth 0");
        check(TreeAnswerValidator.Depth(c[3],c[3].initial,"B")==1,"B depth 1");
        check(TreeAnswerValidator.Height(c[3],c[3].initial)==3,"height 3");
        check(TreeAnswerValidator.Validate(c[3],c[3].initial,null,3,out reason),"numeric answer");
        check(!TreeAnswerValidator.Validate(c[3],c[3].initial,null,2,out reason),"wrong depth");
        var invalid=c[1].required.Concat(new[]{new TreeEdgeData("D","A")}).ToList();check(!TreeAnswerValidator.Rooted(c[1],invalid),"cycle rejected");
        check(!TreeAnswerValidator.ValidEdges(c[1],new[]{new TreeEdgeData("A","A")}),"self edge");
        check(!TreeAnswerValidator.ValidEdges(c[1],new[]{new TreeEdgeData("A","Z")}),"missing node");
        check(!TreeAnswerValidator.ValidEdges(c[1],new[]{new TreeEdgeData("A","B"),new TreeEdgeData("A","B")}),"duplicate");
        var reversed=c[1].required.Select(e=>new TreeEdgeData(e.destination,e.source)).ToList();check(!TreeAnswerValidator.Validate(c[1],reversed,null,null,out reason),"direction respected");
        int validTrees=0;
        for(int mask=0;mask<32;mask++)
        {
            var edges=c[4].initial.Where((e,i)=>(mask&(1<<i))!=0).ToList();
            bool expected=edges.Count==3&&TreeAnswerValidator.Connected(c[4],edges);
            bool valid=TreeAnswerValidator.Validate(c[4],edges,null,null,out reason);check(valid==expected,"spanning subset "+mask);if(valid)validTrees++;
        }
        check(validTrees==8,"all eight spanning trees accepted");
        check(!TreeAnswerValidator.Validate(c[4],new[]{new TreeEdgeData("A","D"),new TreeEdgeData("A","B"),new TreeEdgeData("B","C")},null,null,out reason),"foreign spanning edge");
        var catalog=AssetDatabase.LoadAssetAtPath<TreeChallengeCatalog>("Assets/Prefabs/Trees/TreeChallenges.asset");
        check(catalog!=null&&catalog.challenges.Count==5,"saved five-round catalog");
        check(catalog.variantSets!=null&&catalog.variantSets.Count==5,"five variant sets");
        for(int round=0;round<5;round++)
        {
            var set=catalog.variantSets[round];
            check(set!=null&&set.variants!=null&&set.variants.Count==10,"ten variants in round "+round);
            var signatures=new HashSet<string>();
            for(int index=0;index<10;index++)
            {
                var v=set.variants[index];
                string label="round "+round+" variant "+index;
                check(v!=null&&v.type==catalog.challenges[round].type,label+" type");
                check(v.nodes.Select(n=>n.id).Distinct().Count()==v.nodes.Count,label+" unique nodes");
                check(v.nodes.All(n=>n.position.x>=-1&&n.position.x<=1&&n.position.y>=0&&n.position.y<=1),label+" diagram bounds");
                check(TreeAnswerValidator.ValidEdges(v,v.initial)&&TreeAnswerValidator.ValidEdges(v,v.required),label+" valid edges");
                string signature=v.identification+"|"+v.root+"|"+v.target+"|"+v.objective+"|"+
                    string.Join(",",v.initial.Select(e=>e.Key(v.Directed)).OrderBy(x=>x).ToArray())+"|"+
                    string.Join(",",v.required.Select(e=>e.Key(v.Directed)).OrderBy(x=>x).ToArray());
                check(signatures.Add(signature),label+" distinct problem");
                switch(round)
                {
                    case 0:
                        check(v.nodes.Count==(v.difficulty==2?9:7)&&v.initial.Count==v.nodes.Count-1&&TreeAnswerValidator.Rooted(v,v.initial),label+" seven-node terminology tree");
                        check(v.nodes.Any(n=>TreeAnswerValidator.Validate(v,v.initial,n.id,null,out reason)),label+" has a correct node");
                        check(v.nodes.Any(n=>!TreeAnswerValidator.Validate(v,v.initial,n.id,null,out reason)),label+" has an incorrect node");
                        break;
                    case 1:
                        check(v.nodes.Count==(v.difficulty==2?9:5)&&v.initial.Count==0&&v.required.Count==v.nodes.Count-1,label+" four connection task");
                        check(TreeAnswerValidator.Validate(v,v.required,null,null,out reason),label+" complete build accepted");
                        check(!TreeAnswerValidator.Validate(v,v.initial,null,null,out reason),label+" empty build rejected");
                        break;
                    case 2:
                        check(v.nodes.Count==(v.difficulty==2?9:5)&&v.initial.Count==v.nodes.Count-1&&v.required.Count==v.nodes.Count-1&&TreeAnswerValidator.Rooted(v,v.initial),label+" intact starting tree");
                        check(v.initial.Count(e=>!v.required.Any(r=>r.Key(true)==e.Key(true)))==(v.difficulty==2?2:1),label+" correct number of wrong branches");
                        check(TreeAnswerValidator.Validate(v,v.required,null,null,out reason),label+" repair accepted");
                        check(!TreeAnswerValidator.Validate(v,v.initial,null,null,out reason),label+" broken version rejected");
                        break;
                    case 3:
                        int depth=TreeAnswerValidator.Depth(v,v.initial,v.target);
                        check(v.nodes.Count==(v.difficulty==2?9:6)&&v.initial.Count==v.nodes.Count-1&&(v.difficulty==2?depth>=4:depth==2||depth==3),label+" comparable depth task");
                        check(TreeAnswerValidator.Validate(v,v.initial,null,depth,out reason),label+" correct depth accepted");
                        check(!TreeAnswerValidator.Validate(v,v.initial,null,depth-1,out reason),label+" wrong depth rejected");
                        break;
                    case 4:
                        check(v.nodes.Count==(v.difficulty==2?6:4)&&v.initial.Count==(v.difficulty==2?9:5)&&TreeAnswerValidator.Connected(v,v.initial),label+" five-edge graph");
                        int solutions=0;
                        for(int mask=0;mask<(1<<v.initial.Count);mask++)
                        {
                            var subset=v.initial.Where((e,i)=>(mask&(1<<i))!=0).ToList();
                            if(TreeAnswerValidator.Validate(v,subset,null,null,out reason))solutions++;
                        }
                        check(v.difficulty==2?solutions>8:solutions==8,label+" valid spanning alternatives");
                        break;
                }
            }
        }
        Debug.Log("TREES VALIDATORS PASS: "+checks+" assertions; all 50 saved variants checked.");
    }
}
