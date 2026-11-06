#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Bakes cosmetic paths from the current scene. Does not move objects or modify physics.</summary>
public static class TruthTableGroundSetup
{
    public const string Folder="Assets/Art/TruthTableGround";
    const string Archive="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE";
    const float Step=1.5f;
    const int Resolution=1024;
    static Bounds bounds;
    static int width,height;
    static bool[] walkable,stone,accessible;
    static float[] heights;
    static Collider[] surfaces;
    static HashSet<Collider> surfaceSet;
    static readonly Collider[] nearby=new Collider[64];
    static float water;
    [Serializable] public class Route { public string name;public Vector2[] points; }
    [Serializable] public class GroundReport { public string floor,reference;public int surfacesChanged,maskResolution;public Vector4 worldRect;public Route[] routes;public bool transformAndCollidersPreserved; }
    public static GroundReport Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.name!="TruthTable")throw new InvalidOperationException("Open TruthTable in Edit Mode.");
        var root=GameObject.Find("Truth_Table");var floor=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="FLOOR (1)");
        if(!File.Exists("Library/TruthGroundBefore.unity"))EditorSceneManager.SaveScene(scene,"Library/TruthGroundBefore.unity",true);
        var floorRenderer=floor.GetComponent<MeshRenderer>();var floorCollider=floor.GetComponent<MeshCollider>();
        var ground=new List<MeshRenderer>{floorRenderer};
        ground.AddRange(root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Extension"&&r.sharedMaterials.Any(m=>m!=null&&m==floorRenderer.sharedMaterial)));
        var invariants=ground.Select(r=>Snapshot(r.transform)).ToArray();
        bounds=floorRenderer.bounds;water=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Ocean50x50W750H750").position.y;
        var allSurfaces=new HashSet<Collider>{floorCollider};
        foreach(var r in ground)foreach(var c in r.GetComponents<Collider>())allSurfaces.Add(c);
        foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="PATH1"||t.name.StartsWith("Pathway",StringComparison.Ordinal)))
            foreach(var c in t.GetComponentsInChildren<Collider>(true))if(c.enabled&&!c.isTrigger)allSurfaces.Add(c);
        surfaces=allSurfaces.ToArray();surfaceSet=allSurfaces;
        Physics.SyncTransforms();BuildGrid();
        var doors=UnityEngine.Object.FindFirstObjectByType<TruthTableDoorInteraction>();var serialized=new SerializedObject(doors);
        var book=(Transform)serialized.FindProperty("bookStatue").objectReferenceValue;Vector2 hub=XZ(book.position);MarkAccessible(hub);
        var routes=new List<Route>();
        var doorArray=serialized.FindProperty("doors");
        for(int i=0;i<doorArray.arraySize;i++)
        {
            var door=(Collider)doorArray.GetArrayElementAtIndex(i).objectReferenceValue;
            Vector3 approach=door.bounds.center-door.transform.forward*5;
            routes.Add(FindRoute("House "+(i+1),hub,XZ(approach)));
        }
        var table=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="TRUTIBOL");
        routes.Add(FindRoute("Truth Table approach",hub,XZ(table.position+Vector3.forward*8)));
        var spawn=GameObject.Find("Truth_Table_SpawnPoint");if(spawn!=null)routes.Add(FindRoute("Spawn approach",hub,XZ(spawn.transform.position)));
        BakeMask(routes,hub);
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/TruthTable_GrassAndDirt.mat");
        if(material==null){material=new Material(Shader.Find("LogicLegends/TruthTable/GrassAndPaths")){name="TruthTable_GrassAndDirt"};AssetDatabase.CreateAsset(material,Folder+"/TruthTable_GrassAndDirt.mat");}
        material.SetTexture("_GrassMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Archive+"/Art/Textures/Archive_GrassAlbedo.png"));
        material.SetTexture("_DetailAtlas",AssetDatabase.LoadAssetAtPath<Texture2D>(Archive+"/Art/Textures/Archive_SurfaceDetailAtlas.png"));
        material.SetTexture("_MacroMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Archive+"/Art/Textures/Archive_GroundVariation.png"));
        material.SetTexture("_PathMask",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/TruthTable_PathMask.png"));
        material.SetColor("_GrassColor",new Color(.48f,.64f,.29f));material.SetColor("_EarthColor",new Color(.5f,.38f,.24f));
        material.SetVector("_GrassReference",new Vector4(.073633f,.141184f,.026107f,1));
        material.SetVector("_WorldRect",new Vector4(bounds.min.x,bounds.min.z,bounds.size.x,bounds.size.z));material.SetFloat("_TileMeters",3.8f);material.SetFloat("_Variation",.65f);material.enableInstancing=true;
        foreach(var renderer in ground){renderer.sharedMaterial=material;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);EditorUtility.SetDirty(renderer);}
        bool preserved=ground.Select((r,i)=>Snapshot(r.transform)==invariants[i]).All(v=>v);
        if(!preserved)throw new InvalidOperationException("Ground transform/mesh/collision invariants changed.");
        var report=new GroundReport{floor=floor.name,reference="Archive_Meadow: original grass / detail / macro textures",surfacesChanged=ground.Count,maskResolution=Resolution,worldRect=new Vector4(bounds.min.x,bounds.min.z,bounds.size.x,bounds.size.z),routes=routes.ToArray(),transformAndCollidersPreserved=preserved};
        File.WriteAllText(Folder+"/TruthTableGroundRoutes.json",JsonUtility.ToJson(report,true));
        EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Library/TruthTableGroundSetup.json",JsonUtility.ToJson(report,true));return report;
    }
    static string Snapshot(Transform t)=>EditorJsonUtility.ToJson(t)+"|"+EditorJsonUtility.ToJson(t.GetComponent<MeshFilter>())+"|"+string.Join("|",t.GetComponents<Collider>().Select(c=>EditorJsonUtility.ToJson(c)));
    static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    static Vector2 Position(int index)=>new Vector2(bounds.min.x+(index%width)*Step,bounds.min.z+(index/width)*Step);
    static int Index(Vector2 p)=>Mathf.Clamp(Mathf.RoundToInt((p.y-bounds.min.z)/Step),0,height-1)*width+Mathf.Clamp(Mathf.RoundToInt((p.x-bounds.min.x)/Step),0,width-1);
    static bool Surface(Vector2 p,out float y,out bool bridge)
    {
        y=-10000;bridge=false;var ray=new Ray(new Vector3(p.x,bounds.max.y+15,p.y),Vector3.down);RaycastHit hit;
        foreach(var collider in surfaces)
        {
            var b=collider.bounds;if(p.x<b.min.x||p.x>b.max.x||p.y<b.min.z||p.y>b.max.z)continue;
            if(collider.Raycast(ray,out hit,80)&&hit.normal.y>.65f&&hit.point.y>y){y=hit.point.y;bridge=!collider.name.Contains("FLOOR")&&collider.name!="Extension";}
        }
        return y>water+.35f;
    }
    static void BuildGrid()
    {
        width=Mathf.CeilToInt(bounds.size.x/Step)+1;height=Mathf.CeilToInt(bounds.size.z/Step)+1;
        accessible=null;walkable=new bool[width*height];stone=new bool[walkable.Length];heights=new float[walkable.Length];
        for(int index=0;index<walkable.Length;index++)
        {
            Vector2 p=Position(index);float y;bool bridge;if(!Surface(p,out y,out bridge))continue;
            heights[index]=y;stone[index]=bridge;
            int count=Physics.OverlapCapsuleNonAlloc(new Vector3(p.x,y+.95f,p.y),new Vector3(p.x,y+1.65f,p.y),.48f,nearby,~0,QueryTriggerInteraction.Ignore);
            bool blocked=false;for(int j=0;j<count;j++)if(!surfaceSet.Contains(nearby[j])){blocked=true;break;}
            walkable[index]=!blocked;
        }
    }
    static int Nearest(Vector2 target)
    {
        int best=-1;float distance=float.MaxValue;
        for(int i=0;i<walkable.Length;i++)if(walkable[i]&&(accessible==null||accessible[i])){float d=(Position(i)-target).sqrMagnitude;if(d<distance){best=i;distance=d;}}
        if(best<0||distance>225)throw new InvalidOperationException("No accessible land near "+target);return best;
    }
    static void MarkAccessible(Vector2 hub)
    {
        // Closed rooms can be nearer than the porch. Select the exterior component
        // reachable from the book instead of ending a cosmetic trail inside a wall.
        int from=Nearest(hub);accessible=new bool[walkable.Length];var queue=new Queue<int>();queue.Enqueue(from);accessible[from]=true;
        while(queue.Count>0)
        {
            int at=queue.Dequeue(),x=at%width,z=at/width;
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                int nx=x+dx,nz=z+dz;if(nx<0||nx>=width||nz<0||nz>=height)continue;int n=nz*width+nx;
                if(!walkable[n]||accessible[n]||Mathf.Abs(heights[n]-heights[at])>1.3f)continue;
                if(dx!=0&&dz!=0&&(!walkable[z*width+nx]||!walkable[nz*width+x]))continue;
                accessible[n]=true;queue.Enqueue(n);
            }
        }
    }
    static Route FindRoute(string name,Vector2 start,Vector2 destination)
    {
        int from=Nearest(start),to=Nearest(destination);var scores=Enumerable.Repeat(float.PositiveInfinity,walkable.Length).ToArray();var previous=Enumerable.Repeat(-1,walkable.Length).ToArray();var closed=new bool[walkable.Length];
        var queue=new SortedSet<KeyValuePair<float,int>>(Comparer<KeyValuePair<float,int>>.Create((a,b)=>{int order=a.Key.CompareTo(b.Key);return order==0?a.Value.CompareTo(b.Value):order;}));
        scores[from]=0;queue.Add(new KeyValuePair<float,int>(0,from));
        while(queue.Count>0)
        {
            var next=queue.Min;queue.Remove(next);int current=next.Value;if(closed[current])continue;closed[current]=true;if(current==to)break;
            int x=current%width,z=current/width;
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                if(dx==0&&dz==0||x+dx<0||x+dx>=width||z+dz<0||z+dz>=height)continue;
                int n=(z+dz)*width+x+dx;if(!walkable[n]||closed[n]||Mathf.Abs(heights[n]-heights[current])>1.3f)continue;
                if(dx!=0&&dz!=0&&(!walkable[z*width+x+dx]||!walkable[(z+dz)*width+x]))continue;
                float travel=Step*(dx!=0&&dz!=0?1.414214f:1)*(stone[n]?.68f:1.02f)+Mathf.Abs(heights[n]-heights[current])*.4f;
                float cost=scores[current]+travel;if(cost>=scores[n])continue;scores[n]=cost;previous[n]=current;
                queue.Add(new KeyValuePair<float,int>(cost+Vector2.Distance(Position(n),Position(to))*.68f,n));
            }
        }
        if(!closed[to])throw new InvalidOperationException("Could not route "+name+" across existing walkable ground/bridges.");
        var raw=new List<Vector2>();for(int at=to;at>=0;at=previous[at]){raw.Add(Position(at));if(at==from)break;}raw.Reverse();
        var smooth=new List<Vector2>();int a=0;smooth.Add(raw[0]);
        while(a<raw.Count-1){int b=Mathf.Min(raw.Count-1,a+14);while(b>a+1&&!LineClear(raw[a],raw[b]))b--;smooth.Add(raw[b]);a=b;}
        return new Route{name=name,points=smooth.ToArray()};
    }
    static bool LineClear(Vector2 a,Vector2 b)
    {
        int steps=Mathf.CeilToInt(Vector2.Distance(a,b)/(.45f*Step));int last=Index(a);
        for(int s=1;s<=steps;s++){int index=Index(Vector2.Lerp(a,b,(float)s/steps));if(!walkable[index]||Mathf.Abs(heights[index]-heights[last])>1.3f)return false;last=index;}return true;
    }
    static void BakeMask(List<Route> routes,Vector2 hub)
    {
        var pixels=new float[Resolution*Resolution];int routeIndex=0;
        foreach(var route in routes)
        {
            float walked=0;
            for(int i=1;i<route.points.Length;i++)
            {
                var a=route.points[i-1];var b=route.points[i];float distance=Vector2.Distance(a,b);float offset=walked;walked+=distance;
                Paint(pixels,Vector2.Min(a,b)-Vector2.one*4,Vector2.Max(a,b)+Vector2.one*4,p=>{
                    float t=Mathf.Clamp01(Vector2.Dot(p-a,b-a)/Mathf.Max(.001f,(b-a).sqrMagnitude));
                    float r=1.75f*(1+.12f*Mathf.Sin((offset+t*distance)*.16f+routeIndex));
                    float rough=(Mathf.PerlinNoise(p.x*.19f,p.y*.19f)-.5f)*.8f+(Mathf.PerlinNoise(p.x*.71f,p.y*.71f)-.5f)*.18f;
                    float d=Vector2.Distance(p,Vector2.Lerp(a,b,t))+rough;
                    return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(r*.64f,r+.7f,d));
                });
            }
            routeIndex++;
        }
        Ellipse(pixels,hub,new Vector2(3.9f,3.1f),1);
        var random=new System.Random(72038);
        for(int i=0;i<28;i++)
        {
            int index=random.Next(walkable.Length);if(!walkable[index]){i--;continue;}
            Ellipse(pixels,Position(index),new Vector2(1.4f+(float)random.NextDouble()*2.6f,1+(float)random.NextDouble()*2),.45f+(float)random.NextDouble()*.45f);
        }
        var colors=new Color32[pixels.Length];for(int i=0;i<colors.Length;i++){byte value=(byte)Mathf.RoundToInt(Mathf.Clamp01(pixels[i])*255);colors[i]=new Color32(value,value,value,255);}
        var texture=new Texture2D(Resolution,Resolution,TextureFormat.RGBA32,false,true);texture.SetPixels32(colors);texture.Apply();string path=Folder+"/TruthTable_PathMask.png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=false;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Bilinear;importer.anisoLevel=2;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=Resolution;importer.SaveAndReimport();
    }
    static void Ellipse(float[] mask,Vector2 center,Vector2 radius,float opacity)
    {
        Paint(mask,center-radius*1.3f,center+radius*1.3f,p=>{Vector2 d=p-center;float distance=new Vector2(d.x/radius.x,d.y/radius.y).magnitude+(Mathf.PerlinNoise(p.x*.3f,p.y*.3f)-.5f)*.25f;return (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1.2f,distance)))*opacity;});
    }
    static void Paint(float[] mask,Vector2 min,Vector2 max,Func<Vector2,float> value)
    {
        int x0=Mathf.Clamp(Mathf.FloorToInt((min.x-bounds.min.x)/bounds.size.x*(Resolution-1)),0,Resolution-1),x1=Mathf.Clamp(Mathf.CeilToInt((max.x-bounds.min.x)/bounds.size.x*(Resolution-1)),0,Resolution-1);
        int z0=Mathf.Clamp(Mathf.FloorToInt((min.y-bounds.min.z)/bounds.size.z*(Resolution-1)),0,Resolution-1),z1=Mathf.Clamp(Mathf.CeilToInt((max.y-bounds.min.z)/bounds.size.z*(Resolution-1)),0,Resolution-1);
        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){var p=new Vector2(bounds.min.x+(float)x/(Resolution-1)*bounds.size.x,bounds.min.z+(float)z/(Resolution-1)*bounds.size.z);int index=z*Resolution+x;mask[index]=Mathf.Max(mask[index],value(p));}
    }
}
#endif
