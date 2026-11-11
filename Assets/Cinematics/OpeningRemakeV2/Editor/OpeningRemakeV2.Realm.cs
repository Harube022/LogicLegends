using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static partial class OpeningRemakeV2
{
    static Transform realm;
    static Material realmOcean;
    static Transform[] realmLabels,realmHoops;
    static readonly Vector3 RealmOrigin=new Vector3(-2847,0,-1173);
    static readonly Vector2 RealmPillar=new Vector2(80,20),RealmGarden=new Vector2(190,153);
    static List<Vector2[]> realmCoasts=new List<Vector2[]>();
    static readonly string RealmFolder=Folder+"/Realm3D";

    sealed class RealmMesh
    {
        public List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();public List<Color> colors=new List<Color>();public List<int> triangles=new List<int>();
        public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color,Vector3? normal=null)
        {
            var n=normal??Vector3.Cross(b-a,c-a).normalized;int i=vertices.Count;vertices.AddRange(new[]{a,b,c});normals.AddRange(new[]{n,n,n});colors.AddRange(new[]{color,color,color});triangles.AddRange(new[]{i,i+1,i+2});
        }
        public void Top(Vector2 a,Vector2 b,Vector2 c,Vector2[] polygon)
        {
            if(Vector3.Cross(new Vector3(b.x-a.x,0,b.y-a.y),new Vector3(c.x-a.x,0,c.y-a.y)).y<0){var t=b;b=c;c=t;}
            int i=vertices.Count;
            foreach(var p in new[]{a,b,c}){float h=RealmHeight(p,polygon);vertices.Add(new Vector3(p.x,h,p.y));float dx=(RealmHeight(p+Vector2.right,polygon)-RealmHeight(p-Vector2.right,polygon))*.5f,dz=(RealmHeight(p+Vector2.up,polygon)-RealmHeight(p-Vector2.up,polygon))*.5f;normals.Add(new Vector3(-dx,1,-dz).normalized);colors.Add(RealmColor(p,h,polygon));}
            triangles.AddRange(new[]{i,i+1,i+2});
        }
        public Mesh Finish(string name){var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(vertices);m.SetNormals(normals);m.SetColors(colors);m.SetTriangles(triangles,0);m.RecalculateBounds();return m;}
    }

    public static string BuildRealm3D(bool rebuild=false)
    {
        Bind();if(realm!=null){if(!rebuild)return "3D realm already exists.";UnityEngine.Object.DestroyImmediate(realm.gameObject);realm=null;}
        if(!AssetDatabase.IsValidFolder(RealmFolder))AssetDatabase.CreateFolder(Folder,"Realm3D");
        realm=new GameObject("Logic Realm - Sculpted 3D World").transform;SceneManager.MoveGameObjectToScene(realm.gameObject,film);realm.SetPositionAndRotation(RealmOrigin,Quaternion.LookRotation(Vector3.up,Vector3.right));
        var terrainMaterial=new Material(Shader.Find("LogicLegends/Cinematics/RealmTerrain"));Asset(terrainMaterial,RealmFolder+"/Land.mat");terrainMaterial=AssetDatabase.LoadAssetAtPath<Material>(RealmFolder+"/Land.mat");
        realmOcean=new Material(Shader.Find("LogicLegends/Cinematics/RealmOcean"));Asset(realmOcean,RealmFolder+"/Ocean.mat");realmOcean=AssetDatabase.LoadAssetAtPath<Material>(RealmFolder+"/Ocean.mat");
        var gold=RealmLit("Antique Gold",new Color(.72f,.45f,.13f),.6f,.55f);var stone=RealmLit("Temple Stone",new Color(.48f,.52f,.48f),0,.35f);
        var glow=MakeMaterial("Realm Pillar Radiance",new Color(.25f,2.3f,3.5f));
        SetRealmCoasts();
        var top=new RealmMesh();var cliffs=new RealmMesh();
        foreach(var polygon in realmCoasts)
        {
            var indices=TriangulateRealm(polygon);
            // A common grid keeps shared mountain edges identical and prevents height-field cracks.
            float minX=Mathf.Floor(polygon.Min(p=>p.x)/6)*6,maxX=polygon.Max(p=>p.x),minZ=Mathf.Floor(polygon.Min(p=>p.y)/6)*6,maxZ=polygon.Max(p=>p.y);
            for(float x=minX;x<maxX;x+=6)for(float z=minZ;z<maxZ;z+=6)
            {
                var a=new Vector2(x,z);var b=a+Vector2.right*6;var c=a+Vector2.up*6;var d=a+new Vector2(6,6);
                RealmGridTriangle(top,new[]{a,b,c},polygon,indices);RealmGridTriangle(top,new[]{b,d,c},polygon,indices);
            }
            var center=polygon.Aggregate(Vector2.zero,(sum,p)=>sum+p)/polygon.Length;
            for(int k=0;k<polygon.Length;k++)
            {
                var a=polygon[k];var b=polygon[(k+1)%polygon.Length];int n=Mathf.CeilToInt(Vector2.Distance(a,b)/8);
                for(int j=0;j<n;j++)
                {
                    var p=Vector2.Lerp(a,b,j/(float)n);var q=Vector2.Lerp(a,b,(j+1)/(float)n);
                    var v0=new Vector3(p.x,RealmHeight(p,polygon),p.y);var v1=new Vector3(q.x,RealmHeight(q,polygon),q.y);
                    var v2=new Vector3(q.x,-12,q.y);var v3=new Vector3(p.x,-12,p.y);
                    var nrm=new Vector3((p.x+q.x)*.5f-center.x,0,(p.y+q.y)*.5f-center.y).normalized;
                    var color=Color.Lerp(new Color(.24f,.28f,.29f),new Color(.43f,.43f,.36f),Mathf.PerlinNoise(p.x*.07f+20,p.y*.07f+70));
                    cliffs.Triangle(v0,v1,v2,color,nrm);cliffs.Triangle(v0,v2,v3,color,nrm);
                    cliffs.Triangle(v2,v1,v0,color,nrm);cliffs.Triangle(v3,v2,v0,color,nrm);
                }
            }
        }
        RealmMeshObject("Raised continents",top.Finish("Sculpted coast and mountain terrain"),terrainMaterial);
        RealmMeshObject("Layered coastline cliffs",cliffs.Finish("Coastal rock faces"),terrainMaterial);
        var water=new RealmMesh();int grid=128;float half=6000;
        for(int x=0;x<grid;x++)for(int z=0;z<grid;z++)
        {
            var a=new Vector3(-half+x*half*2/grid,-8,-half+z*half*2/grid);var b=a+new Vector3(half*2/grid,0,0);var c=a+new Vector3(0,0,half*2/grid);var d=b+new Vector3(0,0,half*2/grid);
            water.Triangle(a,c,b,Color.white,Vector3.up);water.Triangle(b,c,d,Color.white,Vector3.up);
        }
        RealmMeshObject("Animated three dimensional ocean",water.Finish("Ocean waves"),realmOcean);
        var trees=new RealmMesh();var random=new System.Random(3107);int count=0;
        for(int attempt=0;attempt<2600&&count<460;attempt++)
        {
            var p=new Vector2(-330+(float)random.NextDouble()*700,-235+(float)random.NextDouble()*490);var polygon=realmCoasts.FirstOrDefault(poly=>InsideRealm(p,poly));if(polygon==null)continue;
            float h=RealmHeight(p,polygon);if(h>43||RealmDesert(p)||RealmDistance(p,polygon)<14||Vector2.Distance(p,RealmPillar)<43||Vector2.Distance(p,RealmGarden)<35)continue;
            float size=.65f+(float)random.NextDouble()*.8f;
            RealmCone(trees,new Vector3(p.x,h,p.y),.7f*size,.3f*size,6*size,new Color(.25f,.19f,.12f),6);
            var col=Color.Lerp(new Color(.055f,.18f,.12f),new Color(.25f,.40f,.18f),(float)random.NextDouble());
            RealmCone(trees,new Vector3(p.x,h+4*size,p.y),4.7f*size,0,9*size,col,8);RealmCone(trees,new Vector3(p.x,h+8*size,p.y),3.6f*size,0,8*size,col*1.1f,8);count++;
        }
        RealmMeshObject("Forests across the living realm",trees.Finish("Miniature pine forests"),terrainMaterial);
        var shore=MakeMaterial("Realm Shore Foam",new Color(.35f,.75f,.75f));
        for(int p=0;p<realmCoasts.Count;p++)
        {
            var line=new GameObject("Shoreline foam "+p).AddComponent<LineRenderer>();line.transform.SetParent(realm,false);line.sharedMaterial=shore;line.useWorldSpace=false;line.loop=true;line.startWidth=line.endWidth=1.1f;line.positionCount=realmCoasts[p].Length;
            line.SetPositions(realmCoasts[p].Select(v=>new Vector3(v.x,-7.5f,v.y)).ToArray());line.shadowCastingMode=ShadowCastingMode.Off;
        }
        BuildRealmPillar(gold,stone,glow);BuildRealmGarden(gold,stone);
        RealmLabel("THE LOGIC REALM",new Vector3(-10,22,-205),140,400);
        RealmLabel("LOGIC GARDEN",new Vector3(193,62,199),88,210);
        RealmLabel("PILLAR OF LOGIC",new Vector3(80,75,-30),76,190);
        foreach(var t in realm.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
        foreach(var light in realm.GetComponentsInChildren<Light>(true))light.cullingMask=1<<31;
        foreach(var sprite in worldMap.GetComponentsInChildren<SpriteRenderer>(true))sprite.enabled=false;
        AssetDatabase.SaveAssets();Bind();Evaluate(0);EditorSceneManager.SaveScene(film);
        return "Built real 3D terrain, cliffs, ocean, forests and landmarks. Old map sprite disabled. Trees="+count+", vertices="+top.vertices.Count;
    }

    static void SetRealmCoasts()
    {
        realmCoasts=new List<Vector2[]>{
            Poly(-310,-45,-280,-78,-252,-120,-207,-137,-170,-119,-133,-112,-96,-77,-79,-32,-55,13,-27,45,-18,79,-48,107,-83,112,-105,153,-143,184,-177,187,-204,161,-230,145,-259,103,-244,66,-207,51,-194,17,-216,-12,-249,-36,-279,-54),
            Poly(18,22,39,73,83,106,127,115,143,148,181,177,219,165,232,139,269,119,304,87,322,51,300,20,319,-20,287,-51,254,-58,219,-87,187,-77,161,-97,123,-107,95,-93,62,-110,32,-88,6,-57,-5,-22),
            Poly(25,-107,60,-103,104,-120,143,-116,177,-135,199,-161,182,-184,152,-196,130,-220,97,-230,84,-208,54,-204,30,-180,8,-151),
            Poly(-238,153,-260,179,-243,210,-209,215,-188,193,-199,168),
            Poly(-303,101,-321,116,-306,143,-277,137,-270,116),
            Poly(-160,200,-171,228,-148,240,-131,223),
            Poly(310,184,325,210,353,207,365,184,344,162,320,164),
            Poly(-263,-224,-282,-209,-300,-226,-291,-246,-268,-247)};
    }
    static Vector2[] Poly(params float[] xy){var p=new Vector2[xy.Length/2];for(int i=0;i<p.Length;i++)p[i]=new Vector2(xy[i*2],xy[i*2+1]);return p;}
    static void RealmGridTriangle(RealmMesh mesh,Vector2[] triangle,Vector2[] polygon,List<int> ears)
    {
        if(triangle.All(p=>InsideRealm(p,polygon))){mesh.Top(triangle[0],triangle[1],triangle[2],polygon);return;}
        float minX=triangle.Min(p=>p.x),maxX=triangle.Max(p=>p.x),minY=triangle.Min(p=>p.y),maxY=triangle.Max(p=>p.y);
        for(int t=0;t<ears.Count;t+=3)
        {
            var clip=new[]{polygon[ears[t]],polygon[ears[t+1]],polygon[ears[t+2]]};
            if(clip.Max(p=>p.x)<minX||clip.Min(p=>p.x)>maxX||clip.Max(p=>p.y)<minY||clip.Min(p=>p.y)>maxY)continue;
            var points=triangle.ToList();
            for(int e=0;e<3&&points.Count>0;e++)
            {
                var a=clip[e];var b=clip[(e+1)%3];var input=points;points=new List<Vector2>();var prev=input[input.Count-1];float old=Cross2(b-a,prev-a);
                foreach(var p in input){float side=Cross2(b-a,p-a);if((side>=0)!=(old>=0))points.Add(Vector2.Lerp(prev,p,old/(old-side)));if(side>=0)points.Add(p);prev=p;old=side;}
            }
            for(int n=1;n+1<points.Count;n++)if(Mathf.Abs(Cross2(points[n]-points[0],points[n+1]-points[0]))>.0001f)mesh.Top(points[0],points[n],points[n+1],polygon);
        }
    }
    static float Cross2(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
    static List<int> TriangulateRealm(Vector2[] p)
    {
        var left=Enumerable.Range(0,p.Length).ToList();float area=0;for(int i=0;i<p.Length;i++)area+=Cross2(p[i],p[(i+1)%p.Length]);if(area<0)left.Reverse();var result=new List<int>();int guard=0;
        while(left.Count>3&&guard++<p.Length*p.Length)
        {
            bool cut=false;for(int i=0;i<left.Count;i++)
            {
                int a=left[(i+left.Count-1)%left.Count],b=left[i],c=left[(i+1)%left.Count];if(Cross2(p[b]-p[a],p[c]-p[b])<=.001f)continue;
                bool contains=left.Any(k=>k!=a&&k!=b&&k!=c&&Cross2(p[b]-p[a],p[k]-p[a])>=0&&Cross2(p[c]-p[b],p[k]-p[b])>=0&&Cross2(p[a]-p[c],p[k]-p[c])>=0);
                if(contains)continue;result.AddRange(new[]{a,b,c});left.RemoveAt(i);cut=true;break;
            }
            if(!cut)throw new Exception("Could not triangulate a realm coastline.");
        }
        result.AddRange(left);return result;
    }
    static bool InsideRealm(Vector2 p,Vector2[] polygon){bool inside=false;for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)if((polygon[i].y>p.y)!=(polygon[j].y>p.y)&&p.x<(polygon[j].x-polygon[i].x)*(p.y-polygon[i].y)/(polygon[j].y-polygon[i].y)+polygon[i].x)inside=!inside;return inside;}
    static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b){var d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude));}
    static float RealmDistance(Vector2 p,Vector2[] polygon){float d=1000;for(int i=0;i<polygon.Length;i++)d=Mathf.Min(d,SegmentDistance(p,polygon[i],polygon[(i+1)%polygon.Length]));return d;}
    static float Ridge(Vector2 p,Vector2 a,Vector2 b,float width,float height){float d=SegmentDistance(p,a,b);return height*Mathf.Exp(-d*d/(width*width))*(.62f+.38f*Mathf.PerlinNoise(p.x*.065f+50,p.y*.065f+50));}
    static float RealmHeight(Vector2 p,Vector2[] polygon)
    {
        float coast=Smooth(Mathf.Clamp01(RealmDistance(p,polygon)/22));
        float ridge=Ridge(p,new Vector2(-215,-68),new Vector2(-101,151),21,72)+Ridge(p,new Vector2(50,82),new Vector2(227,39),21,65)+Ridge(p,new Vector2(178,9),new Vector2(262,-34),15,38);
        float h=5+coast*(7+Mathf.PerlinNoise(p.x*.027f+60,p.y*.027f+70)*10+ridge);
        float nearPillar=1-Smooth(Mathf.InverseLerp(22,47,Vector2.Distance(p,RealmPillar)));h=Mathf.Lerp(h,14,nearPillar);
        float nearGarden=1-Smooth(Mathf.InverseLerp(25,46,Vector2.Distance(p,RealmGarden)));return Mathf.Lerp(h,17,nearGarden);
    }
    static bool RealmDesert(Vector2 p){return p.x>33&&p.x<280&&p.y>53&&p.y<126;}
    static Color RealmColor(Vector2 p,float h,Vector2[] polygon)
    {
        float noise=Mathf.PerlinNoise(p.x*.05f+100,p.y*.05f+100);
        Color green=Color.Lerp(new Color(.19f,.30f,.12f),new Color(.39f,.48f,.22f),noise);
        if(RealmDesert(p))green=Color.Lerp(new Color(.56f,.40f,.20f),new Color(.75f,.61f,.34f),noise);
        if(h>36)green=Color.Lerp(green,new Color(.40f,.38f,.34f),Mathf.InverseLerp(36,57,h));
        if(h>65)green=Color.Lerp(green,new Color(.78f,.82f,.79f),Mathf.InverseLerp(65,83,h));
        return Color.Lerp(new Color(.65f,.58f,.38f),green,Smooth(Mathf.Clamp01(RealmDistance(p,polygon)/11)));
    }
    static GameObject RealmMeshObject(string name,Mesh mesh,Material material)
    {
        string path=RealmFolder+"/"+name.Replace(" ","")+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        // CopySerialized alone leaves stale GPU index buffers when topology changes.
        if(existing==null)AssetDatabase.CreateAsset(mesh,path);
        else{existing.Clear();existing.indexFormat=mesh.indexFormat;existing.vertices=mesh.vertices;existing.normals=mesh.normals;existing.colors=mesh.colors;existing.triangles=mesh.triangles;existing.RecalculateBounds();EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(mesh);}
        var go=new GameObject(name);go.transform.SetParent(realm,false);go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;
    }
    static Material RealmLit(string name,Color color,float metal,float smooth){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);string p=RealmFolder+"/"+name.Replace(" ","")+".mat";Asset(m,p);return AssetDatabase.LoadAssetAtPath<Material>(p);}
    static void RealmCone(RealmMesh mesh,Vector3 at,float bottom,float top,float height,Color color,int sides)
    {
        for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;var p=at+new Vector3(Mathf.Cos(a)*bottom,0,Mathf.Sin(a)*bottom);var q=at+new Vector3(Mathf.Cos(b)*bottom,0,Mathf.Sin(b)*bottom);var r=at+new Vector3(Mathf.Cos(a)*top,height,Mathf.Sin(a)*top);var s=at+new Vector3(Mathf.Cos(b)*top,height,Mathf.Sin(b)*top);mesh.Triangle(p,r,q,color);mesh.Triangle(q,r,s,color);}
    }
    static GameObject RealmPrimitive(string name,PrimitiveType primitive,Vector3 at,Vector3 scale,Material mat,Transform parent=null)
    {
        var go=GameObject.CreatePrimitive(primitive);go.name=name;go.transform.SetParent(parent??realm,false);go.transform.localPosition=at;go.transform.localScale=scale;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;return go;
    }
    static void BuildRealmPillar(Material gold,Material stone,Material glow)
    {
        Vector3 p=new Vector3(RealmPillar.x,14,RealmPillar.y);
        for(int i=0;i<3;i++)RealmPrimitive("Pillar sanctuary tier "+i,PrimitiveType.Cylinder,p+Vector3.up*(1.5f+i*3),new Vector3(38-i*8,1.5f,38-i*8),stone);
        var source=pillar.GetComponent<MeshFilter>().sharedMesh;float scale=48/pillar.bounds.size.y;var mesh=UnityEngine.Object.Instantiate(source);mesh.name="Actual Pillar crystal miniature";
        mesh.vertices=source.vertices.Select(v=>(pillar.transform.TransformPoint(v)-pillar.bounds.center)*scale).ToArray();mesh.normals=source.normals.Select(n=>pillar.transform.TransformDirection(n)).ToArray();mesh.RecalculateBounds();
        var crystal=RealmMeshObject("Original Pillar crystal miniature",mesh,pillar.sharedMaterial);crystal.transform.localPosition=p+Vector3.up*35;
        for(int i=0;i<2;i++)
        {
            var ring=new RealmMesh();int around=48,tube=8;float y=22+i*18;
            for(int a=0;a<around;a++)for(int b=0;b<tube;b++)
            {
                var v=new Vector3[4];int[] aa={a,a+1,a,a+1},bb={b,b,b+1,b+1};
                for(int k=0;k<4;k++){float u=aa[k]*Mathf.PI*2/around,w=bb[k]*Mathf.PI*2/tube;v[k]=new Vector3(Mathf.Cos(u)*(11+Mathf.Cos(w)*.8f),Mathf.Sin(w)*.8f,Mathf.Sin(u)*(11+Mathf.Cos(w)*.8f));}
                ring.Triangle(v[0],v[2],v[1],Color.white);ring.Triangle(v[1],v[2],v[3],Color.white);
            }
            RealmMeshObject("Pillar golden hoop "+i,ring.Finish("Golden torus"),gold).transform.localPosition=p+Vector3.up*y;
        }
        RealmPrimitive("Pillar beacon",PrimitiveType.Cylinder,p+Vector3.up*74,new Vector3(.65f,36,.65f),glow);
        var light=new GameObject("Pillar miniature light").AddComponent<Light>();light.transform.SetParent(realm,false);light.transform.localPosition=p+Vector3.up*38;light.type=LightType.Point;light.range=115;light.intensity=9;light.color=new Color(.15f,.7f,1);light.shadows=LightShadows.None;
    }
    static void BuildRealmGarden(Material gold,Material stone)
    {
        var p=new Vector3(RealmGarden.x,17,RealmGarden.y);RealmPrimitive("Logic Garden island sanctuary",PrimitiveType.Cylinder,p+Vector3.up*1.5f,new Vector3(60,1.5f,60),stone);
        RealmPrimitive("Logic Garden inner court",PrimitiveType.Cylinder,p+Vector3.up*3,new Vector3(43,1,43),gold);
        for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var at=p+new Vector3(Mathf.Cos(a)*23,10,Mathf.Sin(a)*23);RealmPrimitive("Garden column "+i,PrimitiveType.Cylinder,at,new Vector3(3.4f,8,3.4f),stone);RealmPrimitive("Garden column crown "+i,PrimitiveType.Cylinder,at+Vector3.up*8.4f,new Vector3(5,1,5),gold);}
        var foliage=MakeMaterial("Realm Garden Crystal",new Color(.17f,1.35f,1.7f));RealmPrimitive("Garden knowledge crystal",PrimitiveType.Sphere,p+Vector3.up*12,new Vector3(9,17,9),foliage);
    }
    static void RealmLabel(string words,Vector3 position,float size,float width)
    {
        var t=new GameObject("World label "+words).AddComponent<TextMeshPro>();t.transform.SetParent(realm,false);t.transform.localPosition=position;t.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");t.fontSize=size;t.fontStyle=FontStyles.Bold;t.color=new Color(1,.89f,.6f);t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(width,35);t.text=words;
    }
    static void BindRealm3D()
    {
        var root=film.GetRootGameObjects().FirstOrDefault(r=>r.name=="Logic Realm - Sculpted 3D World");realm=root==null?null:root.transform;if(realm==null)return;
        realmOcean=AssetDatabase.LoadAssetAtPath<Material>(RealmFolder+"/Ocean.mat");realmLabels=realm.GetComponentsInChildren<TMP_Text>(true).Select(t=>t.transform).ToArray();realmHoops=realm.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Pillar golden hoop")).ToArray();
        foreach(var sprite in worldMap.GetComponentsInChildren<SpriteRenderer>(true))sprite.enabled=false;
    }
    static void EvaluateRealm3D(bool map,float time)
    {
        if(realm==null)return;realm.gameObject.SetActive(map);if(!map)return;
        foreach(var label in realmLabels)
        {
            label.rotation=Quaternion.LookRotation(label.position-camera.transform.position,camera.transform.up);
            if(label.name.EndsWith("THE LOGIC REALM")){var text=label.GetComponent<TMP_Text>();var color=text.color;color.a=1-Smooth(Mathf.InverseLerp(20,22,time));text.color=color;}
        }
        foreach(var hoop in realmHoops)hoop.localRotation=Quaternion.Euler(0,time*9,0);
        realmOcean.SetFloat("_MapTime",time);sun.transform.rotation=realm.rotation*Quaternion.Euler(48,-48,0);sun.intensity=1.2f;
        RenderSettings.ambientSkyColor=new Color(.45f,.59f,.69f);RenderSettings.ambientEquatorColor=new Color(.25f,.35f,.37f);RenderSettings.ambientGroundColor=new Color(.15f,.18f,.18f);grade.postExposure.Override(.08f);
    }
}
