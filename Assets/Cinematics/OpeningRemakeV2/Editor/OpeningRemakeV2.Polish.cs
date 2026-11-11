using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static partial class OpeningRemakeV2
{
    static GameObject paperSet,schoolMagic;
    static Transform[] magicSeals;

    public static string UpgradeStoryPolish()
    {
        Bind();
        if(film.GetRootGameObjects().Any(r=>r.name=="Original F Grade Papers"))return "Polish already built.";
        paperSet=new GameObject("Original F Grade Papers");SceneManager.MoveGameObjectToScene(paperSet,film);
        var tests=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/MAPS/CINEMATIC/Tests.fbx");
        var flat=tests.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="Plane.050");
        var places=new[]{new Vector3(-639.8f,16.67f,405.0f),new Vector3(-650.1f,16.67f,405.6f),new Vector3(-629.9f,16.67f,405.5f)};
        for(int i=0;i<places.Length;i++)
        {
            var page=UnityEngine.Object.Instantiate(flat.gameObject,paperSet.transform);page.name="Original failed test "+(i+1);
            page.transform.localScale*=1.35f;page.transform.Rotate(Vector3.up,i*7,Space.World);
            var renderer=page.GetComponent<Renderer>();page.transform.position+=places[i]-renderer.bounds.center;
        }
        schoolMagic=new GameObject("Restored Student Summoning Circles");SceneManager.MoveGameObjectToScene(schoolMagic,film);
        var gold=MakeMaterial("Circle Warm Gold",new Color(3.7f,2.25f,.55f));
        var cyan=MakeMaterial("Circle Turquoise",new Color(.18f,2.5f,3.5f));
        var haze=MakeMaterial("Circle Ground Glow",new Color(.08f,.65f,.72f,.065f));
        haze.SetFloat("_Surface",1);haze.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);haze.SetFloat("_DstBlend",(float)BlendMode.One);
        haze.SetFloat("_ZWrite",0);haze.SetFloat("_Cull",0);haze.SetOverrideTag("RenderType","Transparent");haze.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");haze.renderQueue=3000;EditorUtility.SetDirty(haze);
        for(int i=0;i<6;i++)
        {
            var root=new GameObject("Student Circle "+i).transform;root.SetParent(schoolMagic.transform);root.position=SchoolHome(i)+Vector3.up*.045f;
            var outer=new GameObject("Rotating Inscription").transform;outer.SetParent(root,false);
            var core=new GameObject("Counter Rotating Core").transform;core.SetParent(root,false);
            PolishRing("Outer cyan contour",outer,cyan,1,.014f,96);
            PolishRing("Inner gold contour",outer,gold,.94f,.012f,96);
            PolishRing("Inscription guide",outer,cyan,.75f,.009f,72);
            for(int j=0;j<24;j++)
            {
                float a=j*Mathf.PI/12;var points=Enumerable.Range(0,5).Select(k=>new Vector3(Mathf.Cos(a+k*.013f),0,Mathf.Sin(a+k*.013f))*1.04f).ToArray();
                PolishLine("Broken outer arc "+j,outer,gold,points,.022f,false);
            }
            for(int j=0;j<12;j++)
            {
                float a=j*Mathf.PI/6;var glyph=new GameObject("Math glyph "+j).transform;glyph.SetParent(outer,false);
                glyph.localPosition=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.845f;glyph.localRotation=Quaternion.Euler(0,-j*30,0);glyph.localScale=Vector3.one*.30f;
                int stroke=0;foreach(var path in PolishRune(j%10))PolishLine("Rune stroke "+stroke++,glyph,j%2==0?gold:cyan,path.Select(p=>new Vector3(p.x,0,p.y)).ToArray(),.045f,false);
            }
            PolishRing("Core circle",core,cyan,.65f,.012f,72);PolishRing("Core inner circle",core,gold,.54f,.009f,64);
            for(int triangle=0;triangle<2;triangle++)
            {
                var points=Enumerable.Range(0,3).Select(j=>{float a=j*Mathf.PI*2/3+triangle*Mathf.PI;return new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.50f;}).ToArray();
                PolishLine("Proof triangle "+triangle,core,gold,points,.014f,true);
            }
            var disk=new GameObject("Soft floor glow").AddComponent<MeshFilter>();disk.transform.SetParent(root,false);
            var mesh=new Mesh();mesh.name="Summoning floor disk";var vertices=new List<Vector3>{Vector3.down*.008f};var tris=new List<int>();
            for(int j=0;j<=64;j++){float a=j*Mathf.PI/32;vertices.Add(new Vector3(Mathf.Cos(a),-.008f,Mathf.Sin(a))*.98f);if(j>0){tris.Add(0);tris.Add(j);tris.Add(j+1);}}
            mesh.SetVertices(vertices);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();
            Asset(mesh,Folder+"/SummoningFloorDisk.asset");disk.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/SummoningFloorDisk.asset");disk.gameObject.AddComponent<MeshRenderer>().sharedMaterial=haze;
            var motes=new GameObject("Rising sparks").transform;motes.SetParent(root,false);
            for(int j=0;j<9;j++)
            {
                var spark=GameObject.CreatePrimitive(PrimitiveType.Sphere);spark.name="Ascending mote "+j;spark.transform.SetParent(motes,false);UnityEngine.Object.DestroyImmediate(spark.GetComponent<Collider>());spark.GetComponent<Renderer>().sharedMaterial=j%2==0?gold:cyan;
            }
            var light=new GameObject("Seal local illumination").AddComponent<Light>();light.transform.SetParent(root,false);light.transform.localPosition=Vector3.up*.25f;light.type=LightType.Point;light.range=7;light.color=new Color(.2f,.85f,1);light.shadows=LightShadows.None;light.intensity=0;
        }
        foreach(var root in new[]{paperSet,schoolMagic})foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
        foreach(var light in schoolMagic.GetComponentsInChildren<Light>())light.cullingMask=1<<31;
        AssetDatabase.SaveAssets();BindSchoolPolish();ReframePaperLayout();Evaluate(0);EditorSceneManager.SaveScene(film);
        return "Restored three original Tests.fbx papers and six layered mathematical summoning circles.";
    }

    public static string ReframePaperLayout()
    {
        BindSchoolPolish();
        var room=film.GetRootGameObjects().Single(r=>r.name=="Cinematic Classroom");
        foreach(var r in room.GetComponentsInChildren<Renderer>(true))
        {
            if(r.name=="BézierCurve")r.transform.position+=new Vector3(-642,16.8f,406.7f)-r.bounds.center;
            if(r.name=="Plane.107")r.transform.position+=new Vector3(-642,16.97f,407)-r.bounds.center;
        }
        var tests=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/MAPS/CINEMATIC/Tests.fbx");
        var source=tests.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="Plane.050");
        var places=new[]{new Vector3(-639.5f,16.69f,405.2f),new Vector3(-650.1f,16.69f,405.6f),new Vector3(-629.9f,16.69f,405.5f)};
        for(int i=0;i<paperSet.transform.childCount;i++)
        {
            var page=paperSet.transform.GetChild(i);page.rotation=Quaternion.AngleAxis(180+i*7,Vector3.up)*source.transform.rotation;
            page.position+=places[i]-page.GetComponent<Renderer>().bounds.center;
        }
        Evaluate(0);EditorSceneManager.SaveScene(film);return "Placed the original F mark in clear view, with books beside the paper.";
    }

    static void BindSchoolPolish()
    {
        paperSet=film.GetRootGameObjects().FirstOrDefault(r=>r.name=="Original F Grade Papers");schoolMagic=film.GetRootGameObjects().FirstOrDefault(r=>r.name=="Restored Student Summoning Circles");
        magicSeals=schoolMagic==null?new Transform[0]:schoolMagic.transform.Cast<Transform>().OrderBy(t=>t.name).ToArray();
    }

    static void EvaluateSchoolPolish(bool school,bool summoning,float time)
    {
        if(paperSet==null||schoolMagic==null)return;
        paperSet.SetActive(school);schoolMagic.SetActive(school&&summoning);if(!school||!summoning)return;
        float formation=Smooth(Mathf.InverseLerp(100.4f,101.55f,time));
        for(int i=0;i<magicSeals.Length;i++)
        {
            var root=magicSeals[i];root.position=SchoolHome(i)+Vector3.up*.045f;root.localScale=Vector3.one*(2.65f*Mathf.Max(.001f,formation));
            root.Find("Rotating Inscription").localRotation=Quaternion.Euler(0,time*12+i*17,0);root.Find("Counter Rotating Core").localRotation=Quaternion.Euler(0,-time*17-i*17,0);
            var motes=root.Find("Rising sparks");
            for(int j=0;j<motes.childCount;j++)
            {
                float rise=Mathf.Repeat((time-100.4f)*.48f+j/9f,1),a=j*Mathf.PI*2/9+time*.28f;
                var mote=motes.GetChild(j);mote.localPosition=new Vector3(Mathf.Cos(a)*.88f,.06f+rise*1.1f,Mathf.Sin(a)*.88f);mote.localScale=new Vector3(.023f,.035f+.08f*rise,.023f)*Mathf.Sin(rise*Mathf.PI);
            }
            root.GetComponentInChildren<Light>().intensity=formation*(4.5f+.6f*Mathf.Sin(time*3+i));
        }
    }

    static void PolishRing(string name,Transform parent,Material material,float radius,float width,int count)
    {
        PolishLine(name,parent,material,Enumerable.Range(0,count).Select(i=>{float a=i*Mathf.PI*2/count;return new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;}).ToArray(),width,true);
    }
    static void PolishLine(string name,Transform parent,Material material,Vector3[] points,float width,bool loop)
    {
        var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);line.sharedMaterial=material;line.useWorldSpace=false;line.loop=loop;line.positionCount=points.Length;line.SetPositions(points);line.startWidth=line.endWidth=width;line.numCapVertices=2;line.numCornerVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
    }
    static IEnumerable<Vector2[]> PolishRune(int symbol)
    {
        if(symbol==0){yield return new[]{new Vector2(-.4f,.5f),new Vector2(0,-.5f),new Vector2(.4f,.5f)};yield return new[]{new Vector2(-.25f,0),new Vector2(.25f,0)};}
        else if(symbol==1){yield return new[]{new Vector2(-.35f,.5f),new Vector2(.35f,.5f),new Vector2(.35f,-.5f),new Vector2(-.35f,-.5f)};yield return new[]{new Vector2(-.25f,0),new Vector2(.35f,0)};}
        else if(symbol==2)yield return new[]{new Vector2(-.4f,-.4f),new Vector2(0,.5f),new Vector2(.4f,-.4f)};
        else if(symbol==3)yield return new[]{new Vector2(-.4f,.4f),new Vector2(0,-.5f),new Vector2(.4f,.4f)};
        else if(symbol==4){yield return Enumerable.Range(0,13).Select(j=>{float a=Mathf.PI/2+j*Mathf.PI/12;return new Vector2(Mathf.Cos(a)*.4f,Mathf.Sin(a)*.5f);}).ToArray();yield return new[]{new Vector2(-.4f,0),new Vector2(.3f,0)};}
        else if(symbol==5)yield return new[]{new Vector2(.4f,.5f),new Vector2(-.4f,.5f),new Vector2(.1f,0),new Vector2(-.4f,-.5f),new Vector2(.4f,-.5f)};
        else if(symbol==6||symbol==7)yield return Enumerable.Range(0,17).Select(j=>{float a=j*Mathf.PI/16;return new Vector2(Mathf.Cos(a)*.4f,Mathf.Sin(a)*.8f*(symbol==6?1:-1)-.2f);}).ToArray();
        else{yield return new[]{new Vector2(-.45f,0),new Vector2(.45f,0)};yield return new[]{new Vector2(.1f,.35f),new Vector2(.45f,0),new Vector2(.1f,-.35f)};if(symbol==9)yield return new[]{new Vector2(-.1f,.35f),new Vector2(-.45f,0),new Vector2(-.1f,-.35f)};}
    }
}
