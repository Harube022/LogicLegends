#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Scene-only geometry copies and collision guards; imported art is preserved.</summary>
public static class FunctionsRelationsWorldSetup
{
    public sealed class Part
    {
        public int material;
        public int[] triangles;
        public Bounds bounds;
    }
    public static Mesh CopyMesh(Mesh source)
    {
        using(var data=MeshUtility.AcquireReadOnlyMeshData(source))
        {
            var d=data[0]; var copy=new Mesh {name=source.name+" — scene copy",indexFormat=source.indexFormat};
            using(var v=new NativeArray<Vector3>(d.vertexCount,Allocator.Temp)) {d.GetVertices(v);copy.vertices=v.ToArray();}
            if(d.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal))using(var n=new NativeArray<Vector3>(d.vertexCount,Allocator.Temp)){d.GetNormals(n);copy.normals=n.ToArray();}
            if(d.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent))using(var t=new NativeArray<Vector4>(d.vertexCount,Allocator.Temp)){d.GetTangents(t);copy.tangents=t.ToArray();}
            if(d.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0))using(var uv=new NativeArray<Vector2>(d.vertexCount,Allocator.Temp)){d.GetUVs(0,uv);copy.uv=uv.ToArray();}
            if(d.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color))using(var c=new NativeArray<Color>(d.vertexCount,Allocator.Temp)){d.GetColors(c);copy.colors=c.ToArray();}
            copy.subMeshCount=d.subMeshCount;
            for(int s=0;s<d.subMeshCount;s++)using(var t=new NativeArray<int>(d.GetSubMesh(s).indexCount,Allocator.Temp)){d.GetIndices(t,s);copy.SetTriangles(t.ToArray(),s);}
            copy.RecalculateBounds();return copy;
        }
    }
    public static List<Part> Parts(Mesh mesh,Transform transform)
    {
        var parts=new List<Part>();var v=mesh.vertices;
        for(int s=0;s<mesh.subMeshCount;s++)
        {
            if(s==7||s==8)continue; // Decorative grass cards are never collision or repair targets.
            var tris=mesh.GetTriangles(s); var parent=Enumerable.Range(0,v.Length).ToArray();
            var positions=new Dictionary<Vector3Int,int>();
            foreach(int i in tris)
            {
                var p=Vector3Int.RoundToInt(v[i]*1000);
                if(positions.TryGetValue(p,out int other))Union(parent,i,other);else positions.Add(p,i);
            }
            for(int t=0;t<tris.Length;t+=3){Union(parent,tris[t],tris[t+1]);Union(parent,tris[t],tris[t+2]);}
            var groups=new Dictionary<int,List<int>>();
            foreach(int i in tris){int r=Root(parent,i);if(!groups.ContainsKey(r))groups.Add(r,new List<int>());groups[r].Add(i);}
            foreach(var group in groups.Values)
            {
                var b=new Bounds(transform.TransformPoint(v[group[0]]),Vector3.zero);
                foreach(int i in group)b.Encapsulate(transform.TransformPoint(v[i]));
                parts.Add(new Part{material=s,triangles=group.ToArray(),bounds=b});
            }
        }
        return parts;
    }
    static int Root(int[] parent,int a){while(parent[a]!=a){parent[a]=parent[parent[a]];a=parent[a];}return a;}
    static void Union(int[] p,int a,int b){p[Root(p,a)]=Root(p,b);}
    const string Folder="Assets/Art/FunctionsRelationsRepairs";
    const string Archive="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Prefabs/";
    public static void Apply()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.name!="Functions and Relations")throw new InvalidOperationException("Open the saved mapping scene in Edit Mode.");
        if(GameObject.Find("StageCollisionGuards")!=null)throw new InvalidOperationException("World repairs already exist; inspect before rebuilding.");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var stage=GameObject.Find("Stage").GetComponent<MeshFilter>();var renderer=stage.GetComponent<Renderer>();
        var source=CopyMesh(stage.sharedMesh);var original=source.vertices;var parts=Parts(source,stage.transform);
        var guards=new GameObject("StageCollisionGuards").transform;
        // The imported map combines terrain, trees and grass in one collider. Remove decorative support surfaces.
        var solid=Subset(source,Enumerable.Range(0,source.subMeshCount).Where(i=>i!=5&&i!=6&&i!=7&&i!=8&&i!=12).ToArray());
        AssetDatabase.CreateAsset(solid,Folder+"/WalkableStageCollision.asset");stage.GetComponent<MeshCollider>().sharedMesh=solid;
        int treeCount=0;
        foreach(var part in parts.Where(p=>p.material==5&&p.bounds.size.y>3))
        {var b=part.bounds;Guard(guards,"Tree Guard "+(++treeCount),new Vector3(b.center.x,24,b.center.z),new Vector3(b.size.x+.2f,52,b.size.z+.2f));}
        foreach(string name in new[]{"Domain","Range"})
        {
            var target=GameObject.Find(name);var body=target.transform.Find("Object").GetComponent<Renderer>().bounds;
            foreach(var c in target.GetComponentsInChildren<Collider>(true))if(!c.isTrigger)c.enabled=false;
            Guard(target.transform,"Station Climb Guard",new Vector3(body.center.x,20,body.center.z),new Vector3(2.8f,44,body.size.z+.12f));
        }
        foreach(var target in scene.GetRootGameObjects().Where(g=>g.name.StartsWith("Pedestals",StringComparison.Ordinal)))
        {var b=target.GetComponent<Renderer>().bounds;foreach(var c in target.GetComponents<Collider>())c.enabled=false;Guard(target.transform,"Altar Climb Guard",new Vector3(b.center.x,20,b.center.z),new Vector3(b.size.x+.05f,44,b.size.z+.05f));}
        Guard(guards,"West Boundary",new Vector3(-247,20,903.5f),new Vector3(2,50,109));
        Guard(guards,"East Boundary",new Vector3(-130,20,903.5f),new Vector3(2,50,109));
        Guard(guards,"South Boundary",new Vector3(-188.5f,20,850),new Vector3(119,50,2));
        Guard(guards,"North Boundary",new Vector3(-188.5f,20,957),new Vector3(119,50,2));
        // Ground only: raycast it to seat every path stone without sampling the floating path itself.
        var groundMesh=Subset(source,new[]{0});var probe=new GameObject("Ground Probe").AddComponent<MeshCollider>();
        probe.transform.SetPositionAndRotation(stage.transform.position,stage.transform.rotation);probe.transform.localScale=stage.transform.lossyScale;probe.sharedMesh=groundMesh;
        Physics.SyncTransforms();var vertices=source.vertices;int stones=0;float lowest=0,highest=0;
        foreach(var part in parts.Where(p=>p.material==12))
        {
            RaycastHit hit;if(!probe.Raycast(new Ray(part.bounds.center+Vector3.up*30,Vector3.down),out hit,100))throw new InvalidOperationException("No ground beneath path stone.");
            float shift=hit.point.y-part.bounds.min.y-.02f;
            var delta=stage.transform.InverseTransformVector(Vector3.up*shift);
            foreach(int i in part.triangles.Distinct())vertices[i]+=delta;
            stones++;lowest=Mathf.Min(lowest,shift);highest=Mathf.Max(highest,shift);
        }
        source.vertices=vertices;
        var path=Subset(source,new[]{12});AssetDatabase.CreateAsset(path,Folder+"/GroundedStonePath.asset");
        var pathObject=new GameObject("Grounded Stone Path",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
        pathObject.transform.SetPositionAndRotation(stage.transform.position,stage.transform.rotation);pathObject.transform.localScale=stage.transform.lossyScale;
        pathObject.GetComponent<MeshFilter>().sharedMesh=path;pathObject.GetComponent<Renderer>().sharedMaterial=renderer.sharedMaterials[12];pathObject.GetComponent<MeshCollider>().sharedMesh=path;
        var hidden=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Original Path Hidden"};
        hidden.SetFloat("_Surface",1);hidden.SetFloat("_SrcBlend",5);hidden.SetFloat("_DstBlend",10);hidden.SetFloat("_ZWrite",0);
        hidden.SetColor("_BaseColor",Color.clear);hidden.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");hidden.SetOverrideTag("RenderType","Transparent");hidden.renderQueue=3000;
        AssetDatabase.CreateAsset(hidden,Folder+"/OriginalPathHidden.mat");var mats=renderer.sharedMaterials;mats[12]=hidden;renderer.sharedMaterials=mats;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        UnityEngine.Object.DestroyImmediate(probe.gameObject);
        // The default environment stays intact; this builder now applies functional geometry only.
        UnityEngine.Object.DestroyImmediate(groundMesh);
        if(GameObject.Find("Range") is GameObject range)
        {var label=range.GetComponentsInChildren<TMPro.TMP_Text>(true).First(t=>t.name=="FR_PurposeLabel");label.text="RANGE";label.rectTransform.sizeDelta=new Vector2(11,2);label.fontSize=5;label.enableAutoSizing=true;label.fontSizeMin=3.5f;label.fontSizeMax=5;EditorUtility.SetDirty(label);}
        UnityEngine.Object.DestroyImmediate(source);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Library/FunctionsRelationsWorldSetup.txt","Trees guarded: "+treeCount+"\nPath stones seated: "+stones+"; offsets "+lowest+" to "+highest+"m\nFour continuous boundaries; vertical station/altar guards. Walkable terrain, steps and interactions retained.\nDefault environment preserved; no decorative additions.\n");
    }
    static void Guard(Transform parent,string name,Vector3 center,Vector3 size)
    {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.position=center;g.transform.rotation=Quaternion.identity;g.transform.localScale=Vector3.one;var box=g.AddComponent<BoxCollider>();box.size=Vector3.Scale(size,new Vector3(1/g.transform.lossyScale.x,1/g.transform.lossyScale.y,1/g.transform.lossyScale.z));}
    static Mesh Subset(Mesh source,int[] slots)
    {
        var remap=new Dictionary<int,int>();var ids=new List<int>();var triangles=new List<int>();
        foreach(int s in slots)foreach(int i in source.GetTriangles(s))
        {if(!remap.TryGetValue(i,out int at)){at=ids.Count;remap.Add(i,at);ids.Add(i);}triangles.Add(at);}
        var v=source.vertices;var n=source.normals;var uv=source.uv;var c=source.colors;var t=source.tangents;
        var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=ids.Select(i=>v[i]).ToArray();
        if(n.Length==v.Length)mesh.normals=ids.Select(i=>n[i]).ToArray();if(uv.Length==v.Length)mesh.uv=ids.Select(i=>uv[i]).ToArray();
        if(c.Length==v.Length)mesh.colors=ids.Select(i=>c[i]).ToArray();if(t.Length==v.Length)mesh.tangents=ids.Select(i=>t[i]).ToArray();
        mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
    }
    static void AddArt(Transform parent,string path,Vector3 bottom,Quaternion rotation,Vector3 size)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Archive+path+".prefab");if(asset==null)throw new InvalidOperationException(path);
        var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);g.transform.rotation=rotation;Fit(g.transform,bottom,size);
        foreach(var c in g.GetComponentsInChildren<Collider>(true))c.enabled=false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
    }
    static void Fit(Transform t,Vector3 bottom,Vector3 size)
    {var b=ShareProofChamberDecorations.BoundsOf(t);t.localScale=Vector3.Scale(t.localScale,new Vector3(size.x/b.size.x,size.y/b.size.y,size.z/b.size.z));b=ShareProofChamberDecorations.BoundsOf(t);t.position+=bottom-new Vector3(b.center.x,b.min.y,b.center.z);}
    public static string Inspect()
    {
        var stage=GameObject.Find("Stage").GetComponent<MeshFilter>();var copy=CopyMesh(stage.sharedMesh);
        var mats=stage.GetComponent<Renderer>().sharedMaterials;var parts=Parts(copy,stage.transform);
        var lines=new List<string>();
        for(int s=0;s<copy.subMeshCount;s++)
        {
            var items=parts.Where(p=>p.material==s).OrderByDescending(p=>p.bounds.size.sqrMagnitude).ToArray();
            lines.Add("MATERIAL "+s+" "+mats[s].name+" PARTS "+items.Length);
            foreach(var p in items.Take(25))lines.Add("  center="+p.bounds.center.ToString("F2")+" size="+p.bounds.size.ToString("F2")+" triangles="+p.triangles.Length/3);
        }
        UnityEngine.Object.DestroyImmediate(copy);File.WriteAllLines("Library/FunctionsRelationsWorldParts.txt",lines);return string.Join("\n",lines);
    }
}
#endif
