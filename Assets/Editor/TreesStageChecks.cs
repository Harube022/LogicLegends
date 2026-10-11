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
        Debug.Log("TREES VALIDATORS PASS: "+checks+" assertions; all 8 valid spanning trees accepted.");
    }
}
