using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Trees;
using UnityEditor;
using UnityEngine;

/// <summary>Retains the fifty-entry bank. The upper half adds a genuine structural difficulty step.</summary>
public static class TreesQuestionRevision
{
    public static void Apply()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<TreeChallengeCatalog>("Assets/Prefabs/Trees/TreeChallenges.asset");
        for(int challenge=0;challenge<5;challenge++)
        {
            var bank=catalog.variantSets[challenge].variants;
            for(int i=0;i<5;i++)bank[i].difficulty=1;
            foreach(var q in bank)q.lesson=q.lesson.Replace("CONFIRM","Submit").Replace("confirm","submit");
            for(int i=5;i<10;i++)bank[i]=Harder(challenge,i,bank[i]);
        }
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
    }
    static TreeChallengeData Harder(int challenge,int seed,TreeChallengeData source)
    {
        var q=new TreeChallengeData {title=source.title,lesson=source.lesson,type=source.type,difficulty=2};
        int count=challenge==4?6:9;
        string[] ids=Enumerable.Range(0,count).Select(i=>((char)('A'+(i+seed)%count)).ToString()).ToArray();
        int[][] parents={new[]{-1,0,0,1,1,2,3,5,6},new[]{-1,0,0,1,2,3,3,5,6},new[]{-1,0,0,1,1,3,4,5,7},new[]{-1,0,0,1,3,4,2,6,5},new[]{-1,0,0,1,2,4,3,6,7}};
        int[] parent=parents[seed-5];
        if(challenge==4)
        {
            for(int i=0;i<count;i++)q.nodes.Add(new TreeNodeData(ids[i],Mathf.Sin(i*Mathf.PI/3)*.82f,.5f+Mathf.Cos(i*Mathf.PI/3)*.48f));
            for(int i=0;i<count;i++)q.initial.Add(new TreeEdgeData(ids[i],ids[(i+1)%count]));
            for(int i=0;i<3;i++)q.initial.Add(new TreeEdgeData(ids[(i+seed)%count],ids[(i+seed+3)%count]));
            q.root=ids[0];q.objective="Keep all 6 vertices connected. Remove cycles; keep 5 edges.";
            return q;
        }
        // Leaves are evenly spaced; each parent is centered over its children.
        var children=Enumerable.Range(0,count).Select(i=>new List<int>()).ToArray();
        for(int i=1;i<count;i++)children[parent[i]].Add(i);
        int leaves=children.Count(c=>c.Count==0),next=0;
        int[] depths=new int[count];for(int i=1;i<count;i++)depths[i]=depths[parent[i]]+1;
        float[] x=new float[count];Func<int,float> place=null;
        place=i=>x[i]=children[i].Count==0?-.86f+1.72f*next++/(leaves-1):children[i].Average(c=>place(c));place(0);
        for(int i=0;i<count;i++)q.nodes.Add(new TreeNodeData(ids[i],x[i],1-depths[i]/(float)depths.Max()));
        q.root=ids[0];
        for(int i=1;i<count;i++)q.required.Add(new TreeEdgeData(ids[parent[i]],ids[i]));
        q.initial=q.required.Select(e=>new TreeEdgeData(e.source,e.destination)).ToList();
        switch(challenge)
        {
            case 0:
                q.identification=seed%2==0?TreeIdentification.Parent:TreeIdentification.Child;q.target=ids[seed%2==0?8:3];
                q.objective=q.identification==TreeIdentification.Parent?"Select the parent of "+q.target+".":"Select a child of "+q.target+".";break;
            case 1:
                q.initial.Clear();q.objective="Root "+q.root+". Connect "+string.Join(", ",q.required.Select(e=>e.source+" → "+e.destination))+".";break;
            case 2:
                q.initial[6].source=ids[parent[7]==0?2:0];q.initial[7].source=ids[parent[8]==0?1:0];
                q.objective="Repair "+ids[7]+" under "+ids[parent[7]]+" and "+ids[8]+" under "+ids[parent[8]]+". Keep other links.";break;
            default:
                q.target=ids[Enumerable.Range(0,count).OrderByDescending(i=>depths[i]).First()];
                q.objective="What is the depth of node "+q.target+"?";break;
        }
        return q;
    }
}
