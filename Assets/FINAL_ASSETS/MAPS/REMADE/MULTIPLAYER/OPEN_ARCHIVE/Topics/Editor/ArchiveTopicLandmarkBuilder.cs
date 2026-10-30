using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Authored scenery meshes, kept separate from interaction objects and NavMesh geometry.</summary>
    public static class ArchiveTopicLandmarkBuilder
    {
        const string Folder=ArchiveTopicSetup.Folder+"/Art/EnvironmentPolish";

        sealed class Shape
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<int> indices=new List<int>();
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            public void Box(Vector3 center,Vector3 size,Quaternion rotation=default)
            {
                if(rotation.Equals(default(Quaternion)))rotation=Quaternion.identity;
                Vector3 P(float x,float y,float z)=>center+rotation*Vector3.Scale(size,new Vector3(x,y,z));
                Quad(P(-.5f,-.5f,.5f),P(.5f,-.5f,.5f),P(.5f,.5f,.5f),P(-.5f,.5f,.5f));
                Quad(P(.5f,-.5f,-.5f),P(-.5f,-.5f,-.5f),P(-.5f,.5f,-.5f),P(.5f,.5f,-.5f));
                Quad(P(-.5f,-.5f,-.5f),P(-.5f,-.5f,.5f),P(-.5f,.5f,.5f),P(-.5f,.5f,-.5f));
                Quad(P(.5f,-.5f,.5f),P(.5f,-.5f,-.5f),P(.5f,.5f,-.5f),P(.5f,.5f,.5f));
                Quad(P(-.5f,.5f,.5f),P(.5f,.5f,.5f),P(.5f,.5f,-.5f),P(-.5f,.5f,-.5f));
                Quad(P(-.5f,-.5f,-.5f),P(.5f,-.5f,-.5f),P(.5f,-.5f,.5f),P(-.5f,-.5f,.5f));
            }
            public void Bar(Vector3 a,Vector3 b,float width=.13f,float depth=.17f)
            {var d=b-a;if(d.sqrMagnitude<.0001f)return;Box((a+b)*.5f,new Vector3(width,d.magnitude,depth),Quaternion.FromToRotation(Vector3.up,d));}
            public void Gem(Vector3 center,float size=.28f)
            {
                var top=center+Vector3.up*size;var bottom=center-Vector3.up*size;
                var corners=new[]{center+Vector3.forward*size,center+Vector3.right*size,center-Vector3.forward*size,center-Vector3.right*size};
                for(int i=0;i<4;i++){Tri(top,corners[i],corners[(i+1)%4]);Tri(bottom,corners[(i+1)%4],corners[i]);}
            }
            void Tri(Vector3 a,Vector3 b,Vector3 c)
            {int n=vertices.Count;vertices.AddRange(new[]{a,b,c});indices.AddRange(new[]{n,n+1,n+2});}
            public void Ring(Vector3 center,float radius,float width=.11f,bool horizontal=false,int segments=36)
            {
                Vector3 P(float angle,float r,float z)=>horizontal?center+new Vector3(Mathf.Cos(angle)*r,z,-Mathf.Sin(angle)*r):center+new Vector3(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r,z);
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments,inner=radius-width;
                    Quad(P(a,radius,.08f),P(b,radius,.08f),P(b,inner,.08f),P(a,inner,.08f));
                    Quad(P(b,radius,-.08f),P(a,radius,-.08f),P(a,inner,-.08f),P(b,inner,-.08f));
                    Quad(P(a,radius,-.08f),P(b,radius,-.08f),P(b,radius,.08f),P(a,radius,.08f));
                    Quad(P(b,inner,-.08f),P(a,inner,-.08f),P(a,inner,.08f),P(b,inner,.08f));
                }
            }
            public void Cylinder(Vector3 center,float radius,float height,int segments=10)
            {
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                    Vector3 p=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),q=new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius),h=Vector3.up*height*.5f;
                    Quad(center+q-h,center+p-h,center+p+h,center+q+h);
                    Tri(center+h,center+q+h,center+p+h);Tri(center-h,center+p-h,center+q-h);
                }
            }
            public Mesh Save(string name)
            {
                string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}
                mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();
                var normals=mesh.normals;var uv=new Vector2[vertices.Count];
                for(int i=0;i<uv.Length;i++)
                {
                    var p=vertices[i];var n=normals[i];var a=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));
                    uv[i]=(a.y>=a.x&&a.y>=a.z?new Vector2(p.x,p.z):a.x>=a.z?new Vector2(p.z,p.y):new Vector2(p.x,p.y))*.4f;
                }
                mesh.uv=uv;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
            }
        }
        static Transform Part(Transform parent,string name,Shape shape,Material material,string meshName,bool shadows=false)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=shape.Save(meshName);
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=shadows;
            return go.transform;
        }
        public static ArchiveTopicLandmark Build(ArchiveTopicStation station,Transform parent,Material stone,Material trim,Material glow,TMP_FontAsset font)
        {
            if(station==null||parent==null)throw new ArgumentNullException("station/parent");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var existing=parent.Find("Topic_Landmark");if(existing!=null)UnityEngine.Object.DestroyImmediate(existing.gameObject);
            int topic=Mathf.Clamp(station.topic,0,9);
            var root=new GameObject("Topic_Landmark");root.transform.SetParent(parent,false);root.transform.localPosition=new Vector3(10,0,-5);
            var view=root.AddComponent<ArchiveTopicLandmark>();view.station=station;
            var foundation=new Shape();foundation.Cylinder(new Vector3(0,.16f,0),1.75f,.3f);foundation.Cylinder(new Vector3(0,.48f,0),1.35f,.4f);
            foundation.Box(new Vector3(0,.95f,0),new Vector3(2.3f,.65f,.95f));foundation.Box(new Vector3(0,1.34f,0),new Vector3(2.7f,.14f,1.25f));
            Part(root.transform,"Weathered_Stone_Pedestal",foundation,stone,"Landmark_Stone_Base",true);
            var bands=new Shape();bands.Cylinder(new Vector3(0,.36f,0),1.48f,.08f);bands.Box(new Vector3(0,1.22f,0),new Vector3(2.43f,.08f,1.07f));
            Part(root.transform,"Brass_Architectural_Bands",bands,trim,"Landmark_Brass_Bands");
            var symbols=new[]{new Shape(),new Shape(),new Shape()};
            BuildSymbol(topic,symbols);
            view.restoringFragments=new Transform[3];view.fragmentRenderers=new Renderer[3];view.assembledPositions=new Vector3[3];view.assembledRotations=new Quaternion[3];view.brokenPositions=new Vector3[3];view.brokenRotations=new Quaternion[3];
            for(int i=0;i<3;i++)
            {
                var fragment=Part(root.transform,"Restoring_Ornament_"+(i+1),symbols[i],glow,"Landmark_T"+(topic+1)+"_Ornament_"+i);
                view.restoringFragments[i]=fragment;view.fragmentRenderers[i]=fragment.GetComponent<Renderer>();
                view.assembledPositions[i]=Vector3.zero;view.assembledRotations[i]=Quaternion.identity;
                view.brokenPositions[i]=new Vector3((i-1)*.55f,-.65f-i*.18f,(i%2==0?1:-1)*.24f);
                view.brokenRotations[i]=Quaternion.Euler(6+i*4,0,(i-1)*17);
                fragment.localPosition=view.brokenPositions[i];fragment.localRotation=view.brokenRotations[i];
            }
            var channels=new Shape();channels.Ring(new Vector3(0,.11f,0),2.15f,.045f,true,40);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;var center=new Vector3(Mathf.Cos(a)*1.95f,.14f,Mathf.Sin(a)*1.95f);
                channels.Box(center,new Vector3(.09f,.025f,.24f),Quaternion.Euler(0,-a*Mathf.Rad2Deg,0));
            }
            channels.Bar(new Vector3(-2.10f,.14f,0),new Vector3(-3.25f,.14f,0),.035f,.035f);
            view.channels=Part(root.transform,"Engraved_Restoration_Channels",channels,glow,"Landmark_Restoration_Channels").GetComponent<Renderer>();
            var wave=new Shape();wave.Ring(new Vector3(0,.20f,0),1.65f,.035f,true,40);
            view.completionWave=Part(root.transform,"Topic_Completion_Ground_Wave",wave,glow,"Landmark_Completion_Wave");view.completionWave.gameObject.SetActive(false);
            var label=new GameObject("Topic_Number_And_Name").AddComponent<TextMeshPro>();label.transform.SetParent(root.transform,false);
            label.transform.localPosition=new Vector3(0,.96f,.49f);label.transform.localRotation=Quaternion.Euler(0,180,0);
            label.font=font!=null?font:station.font;label.text="T"+(topic+1)+"  "+ArchiveTopicCatalog.Names[topic];label.color=new Color(.95f,.89f,.70f);
            label.rectTransform.sizeDelta=new Vector2(2.1f,.53f);label.fontSize=3.1f;label.enableAutoSizing=true;label.fontSizeMin=1.8f;label.fontSizeMax=3.1f;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.richText=false;
            view.identity=label;
            EditorUtility.SetDirty(view);return view;
        }
        static Vector3 P(float x,float y,float z=0)=>new Vector3(x,y,z);
        static void BuildSymbol(int topic,Shape[] s)
        {
            switch(topic)
            {
                case 0: // Conjunction, disjunction and a central proposition gem.
                    s[0].Bar(P(-1.35f,2.3f),P(-.6f,4.45f),.22f,.3f);s[0].Bar(P(-.6f,4.45f),P(.15f,2.3f),.22f,.3f);
                    s[1].Bar(P(-.15f,4.25f,.1f),P(.6f,2.15f,.1f),.20f,.3f);s[1].Bar(P(.6f,2.15f,.1f),P(1.35f,4.25f,.1f),.20f,.3f);
                    s[2].Gem(P(0,4.9f),.36f);s[2].Bar(P(-.65f,1.75f),P(.65f,1.75f),.12f,.18f);break;
                case 1: // A blank truth lattice: an identity motif, never a live answer table.
                    for(int i=0;i<4;i++)s[i%3].Bar(P(-1.25f,2+i*.67f),P(1.25f,2+i*.67f),.12f,.2f);
                    for(int i=0;i<3;i++)s[(i+1)%3].Bar(P(-1.1f+i*1.1f,1.9f),P(-1.1f+i*1.1f,4.1f),.12f,.2f);
                    for(int i=0;i<3;i++)s[2].Gem(P(-1.1f+i*1.1f,4.5f),.19f);break;
                case 2: // Two premises flow toward a consequence.
                    s[0].Gem(P(-1.1f,2.1f),.36f);s[0].Gem(P(1.1f,2.1f),.36f);
                    s[1].Bar(P(-.95f,2.4f),P(0,3.6f),.15f);s[1].Bar(P(.95f,2.4f),P(0,3.6f),.15f);
                    s[2].Gem(P(0,4.25f),.48f);s[2].Bar(P(-.6f,3.65f),P(.6f,3.65f),.13f);break;
                case 3: // The proof scroll and a rising sequence of justified lines.
                    s[0].Bar(P(-1.15f,1.9f),P(-1.15f,4.4f),.2f,.24f);s[0].Bar(P(1.15f,1.9f),P(1.15f,4.4f),.2f,.24f);
                    s[1].Bar(P(-1.3f,4.4f),P(1.3f,4.4f),.24f,.30f);s[1].Bar(P(-1.3f,1.9f),P(1.3f,1.9f),.24f,.30f);
                    for(int i=0;i<4;i++)s[2].Bar(P(-.8f,2.3f+i*.48f),P(.3f+i*.13f,2.3f+i*.48f),.085f,.12f);break;
                case 4: // Divergent proof methods rejoin at one conclusion.
                    s[0].Bar(P(0,1.75f),P(-1.25f,2.85f),.19f);s[0].Bar(P(-1.25f,2.85f),P(-.7f,4.1f),.19f);
                    s[1].Bar(P(0,1.75f),P(1.25f,2.85f),.19f);s[1].Bar(P(1.25f,2.85f),P(.7f,4.1f),.19f);
                    s[2].Bar(P(-.7f,4.1f),P(0,4.6f),.19f);s[2].Bar(P(.7f,4.1f),P(0,4.6f),.19f);s[2].Gem(P(0,4.7f),.30f);break;
                case 5: // Induction's repeated step, represented by ascending pillars.
                    for(int i=0;i<6;i++){float x=-1.35f+i*.54f,h=1+i*.43f;s[i%3].Box(P(x,1.5f+h*.5f),P(.33f,h,.40f));s[(i+1)%3].Gem(P(x,1.68f+h),.16f);}
                    break;
                case 6: // Intersecting set rings are recognizable from either approach.
                    s[0].Ring(P(-.65f,3.1f),1.3f,.16f);s[1].Ring(P(.65f,3.1f,.03f),1.3f,.16f);
                    s[2].Gem(P(0,3.1f,.12f),.30f);s[2].Gem(P(-1.05f,3.1f,.08f),.15f);s[2].Gem(P(1.05f,3.1f,.08f),.15f);break;
                case 7: // A decorative domain/range pair and mapping connections.
                    for(int i=0;i<3;i++){float y=2.05f+i*.95f;s[0].Gem(P(-1.15f,y),.27f);s[1].Gem(P(1.15f,y),.27f);}
                    s[2].Bar(P(-.85f,2.05f),P(.85f,3),.10f);s[2].Bar(P(-.85f,3),P(.85f,3.95f),.10f);s[2].Bar(P(-.85f,3.95f),P(.85f,2.05f),.10f);break;
                case 8:
                    var nodes=new[]{P(-1.3f,2.2f),P(1.25f,2.1f),P(-.95f,4),P(.95f,4.25f),P(.1f,3.05f,.28f)};
                    for(int i=0;i<nodes.Length;i++)s[i%3].Gem(nodes[i],.28f);
                    var edges=new[]{0,2,2,3,3,1,1,0,0,4,4,3,2,4};for(int i=0;i<edges.Length;i+=2)s[(i/2)%3].Bar(nodes[edges[i]],nodes[edges[i+1]],.09f,.13f);break;
                default:
                    var tree=new[]{P(0,4.7f),P(-.85f,3.35f),P(.85f,3.35f),P(-1.4f,2.05f),P(-.3f,2.05f),P(.3f,2.05f),P(1.4f,2.05f)};
                    for(int i=0;i<tree.Length;i++)s[i%3].Gem(tree[i],i==0?.35f:.25f);
                    for(int i=1;i<tree.Length;i++)s[(i-1)%3].Bar(tree[(i-1)/2],tree[i],.14f,.18f);break;
            }
        }
    }
}
