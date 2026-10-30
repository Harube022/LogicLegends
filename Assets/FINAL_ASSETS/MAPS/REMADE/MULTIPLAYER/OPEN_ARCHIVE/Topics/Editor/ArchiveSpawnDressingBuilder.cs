using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Paired garden-and-stela vignettes beside the existing spawn avenue. Scenery only.</summary>
    public static class ArchiveSpawnDressingBuilder
    {
        const string Folder=ArchiveTopicSetup.Folder+"/Art/EnvironmentPolish";
        public const string RootName="Spawn_Archive_Dressing";

        public static Transform Build(ArchiveTopicSession session,Transform parent,Material stone,Material trim,Material foliage,Material glow)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Author spawn dressing in stopped Edit Mode.");
            if(session==null||parent==null||stone==null||trim==null||foliage==null||glow==null)
                throw new ArgumentNullException("Existing scene and environment materials are required.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var old=parent.Find(RootName);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root=new GameObject(RootName).transform;root.SetParent(parent,false);

            var masonry=new Shape();var brass=new Shape();var leaves=new Shape();var ivory=new Shape();var violet=new Shape();var runes=new Shape();
            // A low chamfered planting kerb collects the architecture and foliage into
            // one deliberate bed. It does not add a new walkable platform or collider.
            masonry.BeveledPrism(new Vector3(0,.12f,.15f),new Vector3(5.4f,.24f,7.4f),.44f);
            masonry.BeveledPrism(new Vector3(0,.31f,.15f),new Vector3(4.98f,.15f,6.98f),.40f);
            // Weathered library stela, set behind its reading bench. The carved book
            // face points toward the avenue; no floating signs or new instructions.
            masonry.BeveledPrism(new Vector3(0,.67f,-1.68f),new Vector3(2.65f,.62f,1.25f),.20f);
            masonry.BeveledPrism(new Vector3(0,1.92f,-1.70f),new Vector3(2.05f,2.05f,.82f),.22f);
            masonry.BeveledPrism(new Vector3(0,3.04f,-1.7f),new Vector3(2.39f,.22f,1.05f),.19f);
            masonry.BeveledPrism(new Vector3(-.72f,3.24f,-1.7f),new Vector3(.71f,.18f,.87f),.12f);
            masonry.BeveledPrism(new Vector3(.55f,3.20f,-1.7f),new Vector3(1.24f,.10f,.83f),.13f);
            // Three pieces form a gently chamfered stone bench, rather than a row of
            // square boxes. All details share one stone mesh per vignette.
            foreach(float x in new[]{-1.18f,1.18f})masonry.BeveledPrism(new Vector3(x,.64f,1.54f),new Vector3(.63f,.67f,.95f),.12f);
            masonry.BeveledPrism(new Vector3(0,1.05f,1.54f),new Vector3(3.45f,.25f,1.28f),.21f);
            masonry.BeveledPrism(new Vector3(0,.48f,2.96f),new Vector3(3.65f,.18f,.44f),.15f);
            // Discontinuous brass joints read as inlays instead of another complete
            // coplanar ground layer. A restrained open-book relief gives archive identity.
            foreach(float sign in new[]{-1,1})
            {
                brass.Bar(new Vector3(sign*.97f,1.17f,1.54f),new Vector3(sign*1.56f,1.17f,1.54f),.035f,.035f);
                brass.Bar(new Vector3(sign*1.12f,.99f,-1.03f),new Vector3(sign*1.12f,1.08f,-1.03f),.07f,.04f);
            }
            float face=-1.267f;
            brass.Bar(new Vector3(-.73f,1.44f,face),new Vector3(-.73f,2.43f,face),.045f,.045f);
            brass.Bar(new Vector3(.73f,1.44f,face),new Vector3(.73f,2.43f,face),.045f,.045f);
            brass.Bar(new Vector3(0,1.55f,face+.035f),new Vector3(0,2.40f,face+.035f),.055f,.055f);
            for(int side=-1;side<=1;side+=2)
            {
                brass.Bar(new Vector3(0,2.40f,face+.035f),new Vector3(side*.73f,2.43f,face),.045f,.045f);
                brass.Bar(new Vector3(0,1.55f,face+.035f),new Vector3(side*.73f,1.44f,face),.045f,.045f);
                for(int row=0;row<4;row++)
                    brass.Bar(new Vector3(side*.16f,1.76f+row*.145f,face+.025f),new Vector3(side*.57f,1.71f+row*.16f,face+.025f),.025f,.025f);
            }
            // A modest geometric archive seal above the carved pages, with no light source.
            runes.Bar(new Vector3(-.35f,2.70f,face),new Vector3(0,2.91f,face),.035f,.04f);
            runes.Bar(new Vector3(0,2.91f,face),new Vector3(.35f,2.70f,face),.035f,.04f);
            runes.Bar(new Vector3(.35f,2.70f,face),new Vector3(0,2.49f,face),.035f,.04f);
            runes.Bar(new Vector3(0,2.49f,face),new Vector3(-.35f,2.70f,face),.035f,.04f);

            // Intentional plant masses: taller leaves at the rear flanks, lower
            // groundcover around the bench, and flower drifts at two front corners.
            var plantCenters=new[]{new Vector3(-1.85f,.4f,-2.1f),new Vector3(1.85f,.4f,-2.1f),
                new Vector3(-2.03f,.4f,-.24f),new Vector3(2.03f,.4f,-.24f),
                new Vector3(-1.93f,.4f,1.77f),new Vector3(1.93f,.4f,1.77f),
                new Vector3(-1.25f,.4f,2.76f),new Vector3(1.25f,.4f,2.76f)};
            for(int i=0;i<plantCenters.Length;i++)
            {
                float height=i<2?1.25f:i<4?.85f:.56f;
                for(int leaf=0;leaf<7;leaf++)
                {
                    float a=leaf*Mathf.PI*2/7+i*.31f;float length=height*(.70f+.22f*Mathf.Sin(i*3+leaf));
                    leaves.Leaf(plantCenters[i],new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),length,.25f+(i%2)*.04f);
                }
            }
            for(int bed=0;bed<2;bed++)for(int flower=0;flower<9;flower++)
            {
                float a=flower*2.399963f,spread=.22f+Mathf.Sqrt(flower)*.15f;
                var center=new Vector3((bed==0?-1.25f:1.25f)+Mathf.Cos(a)*spread,.46f,2.65f+Mathf.Sin(a)*spread*.6f);
                float stem=.33f+(flower%3)*.10f;
                leaves.Bar(center,center+Vector3.up*stem,.025f,.025f);
                Flower(bed==0?ivory:violet,center+Vector3.up*stem,.13f+(flower%2)*.035f,flower*.6f);
            }

            var bloom=Material("Archive_Spawn_Ivory_Blossom",new Color(.94f,.78f,.37f));
            var lavender=Material("Archive_Spawn_Violet_Blossom",new Color(.50f,.34f,.67f));
            var meshes=new[]{masonry.Save("Spawn_Garden_Masonry"),brass.Save("Spawn_Library_Brass_Inlay"),
                leaves.Save("Spawn_Garden_Leaf_Masses"),ivory.Save("Spawn_Garden_Ivory_Drift"),violet.Save("Spawn_Garden_Violet_Drift"),runes.Save("Spawn_Archive_Seal")};
            var materials=new[]{stone,trim,foliage,bloom,lavender,glow};
            var names=new[]{"Chamfered_Stone_And_Bench","Carved_Open_Book_Inlay","Layered_Garden_Foliage","Ivory_Flower_Drift","Violet_Flower_Drift","Archive_Seal"};
            foreach(int side in new[]{-1,1})
            {
                var cluster=new GameObject(side<0?"West_Spawn_Garden":"East_Spawn_Garden").transform;cluster.SetParent(root,false);
                // Existing banners are at x=17.5 and torches at x=16.25. The farther
                // bed leaves those props and the 25-unit walking avenue clear.
                var point=new Vector3(side*24,0,-203);
                var floors=Physics.RaycastAll(point+Vector3.up*2,Vector3.down,6,~(1<<7),QueryTriggerInteraction.Ignore)
                    .Where(h=>h.normal.y>.8f&&Mathf.Abs(h.point.y)<1).OrderBy(h=>h.distance).ToArray();
                if(floors.Length>0)point.y=floors[0].point.y+.025f;
                cluster.position=point;cluster.rotation=Quaternion.Euler(0,side<0?90:-90,0);
                cluster.localScale=Vector3.one*1.25f;
                for(int i=0;i<meshes.Length;i++)
                {
                    var part=new GameObject(names[i],typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(cluster,false);
                    part.GetComponent<MeshFilter>().sharedMesh=meshes[i];var renderer=part.GetComponent<MeshRenderer>();renderer.sharedMaterial=materials[i];
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
                    renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                }
            }
            return root;
        }
        static Material Material(string name,Color color)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.15f);material.SetFloat("_Surface",0);material.renderQueue=2000;
            EditorUtility.SetDirty(material);return material;
        }
        static void Flower(Shape shape,Vector3 center,float radius,float angle)
        {
            for(int petal=0;petal<5;petal++)
            {
                float a=angle+petal*Mathf.PI*2/5;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var s=Vector3.Cross(Vector3.up,d);
                shape.Quad(center,center+d*radius*.6f+s*radius*.43f+Vector3.up*.025f,center+d*radius,center+d*radius*.6f-s*radius*.43f+Vector3.up*.025f,true);
            }
        }
        sealed class Shape
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> triangles=new List<int>();
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,bool doubleSided=false)
            {
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
                if(doubleSided){n=vertices.Count;vertices.AddRange(new[]{d,c,b,a});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            }
            public void Bar(Vector3 a,Vector3 b,float width,float depth)
            {
                var delta=b-a;if(delta.sqrMagnitude<.000001f)return;var rotation=Quaternion.FromToRotation(Vector3.up,delta);
                var half=new Vector3(width,delta.magnitude,depth)*.5f;var center=(a+b)*.5f;
                Vector3 P(float x,float y,float z)=>center+rotation*Vector3.Scale(half,new Vector3(x,y,z));
                Quad(P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1));Quad(P(1,-1,-1),P(-1,-1,-1),P(-1,1,-1),P(1,1,-1));
                Quad(P(-1,-1,-1),P(-1,-1,1),P(-1,1,1),P(-1,1,-1));Quad(P(1,-1,1),P(1,-1,-1),P(1,1,-1),P(1,1,1));
                Quad(P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1));Quad(P(-1,-1,-1),P(1,-1,-1),P(1,-1,1),P(-1,-1,1));
            }
            public void BeveledPrism(Vector3 center,Vector3 size,float cut)
            {
                float x=size.x*.5f,z=size.z*.5f,c=Mathf.Min(cut,Mathf.Min(x,z)*.8f),h=size.y*.5f;
                var outline=new[]{new Vector3(-x+c,0,-z),new Vector3(x-c,0,-z),new Vector3(x,0,-z+c),new Vector3(x,0,z-c),
                    new Vector3(x-c,0,z),new Vector3(-x+c,0,z),new Vector3(-x,0,z-c),new Vector3(-x,0,-z+c)};
                var inner=outline.Select(p=>new Vector3(p.x*.96f,0,p.z*.96f)).ToArray();
                for(int i=0;i<8;i++)
                {
                    int j=(i+1)%8;
                    var a=center+outline[i]-Vector3.up*h;var b=center+outline[j]-Vector3.up*h;
                    var c1=center+inner[i]+Vector3.up*h;var d=center+inner[j]+Vector3.up*h;
                    Quad(b,a,c1,d);
                    Quad(center+Vector3.up*h,d,c1,center+Vector3.up*h);
                    Quad(center-Vector3.up*h,a,b,center-Vector3.up*h);
                }
            }
            public void Leaf(Vector3 origin,Vector3 direction,float length,float width)
            {
                var side=Vector3.Cross(Vector3.up,direction);var shoulder=origin+direction*length*.40f+Vector3.up*length*.64f;
                var tip=origin+direction*length*.82f+Vector3.up*length*.83f;
                Quad(origin,shoulder+side*width,tip,shoulder-side*width,true);
            }
            public Mesh Save(string name)
            {
                string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}
                mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();
                var normals=mesh.normals;var uv=new Vector2[vertices.Count];
                for(int i=0;i<vertices.Count;i++)
                {
                    var p=vertices[i];var n=normals[i];var a=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));
                    uv[i]=(a.y>=a.x&&a.y>=a.z?new Vector2(p.x,p.z):a.x>=a.z?new Vector2(p.z,p.y):new Vector2(p.x,p.y))*.4f;
                }
                mesh.uv=uv;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
            }
        }
    }
}
