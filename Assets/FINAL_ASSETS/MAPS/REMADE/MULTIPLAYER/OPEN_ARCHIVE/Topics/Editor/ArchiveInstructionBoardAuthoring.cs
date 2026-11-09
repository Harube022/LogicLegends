using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Saved, opaque scenery for the existing instruction boards. No gameplay state or collision.</summary>
    public static class ArchiveInstructionBoardAuthoring
    {
        public const string Folder=ArchiveTopicSetup.Folder+"/Art/InstructionBoards";
        static Mesh body,header,paper,posts,brass,inset;
        static Material timber,metal,parchment,teal,stone;

        [MenuItem("Tools/Logic Legends/Open Archive/Environment/Style All Instruction Boards")]
        public static void Install()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=ArchiveTopicSetup.ScenePath)
                throw new InvalidOperationException("Open stopped MultiplayerGameplay before styling its boards.");
            var stations=UnityEngine.Object.FindObjectsByType<ArchiveTopicStation>(FindObjectsSortMode.None).OrderBy(s=>s.topic).ToArray();
            if(stations.Length!=10||stations.Any(s=>s.board==null||s.title==null))
                throw new InvalidOperationException("All ten existing instruction boards are required.");
            // Preflight before touching any authored board.
            foreach(var station in stations)
                foreach(string name in new[]{"Question_Board_Frame","Question_Board_Face","Topic_Header_Backplate"})
                    if(station.transform.Find(name)?.GetComponent<MeshFilter>()==null)
                        throw new InvalidOperationException("Missing original board part: "+station.name+"/"+name);
            Prepare();
            Undo.SetCurrentGroupName("Style archive instruction boards");
            foreach(var station in stations)Apply(station);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("Styled all ten original instruction boards. Topic flow, small waymarks, collision and completion ownership preserved.");
        }

        static void Prepare()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var source=AssetDatabase.LoadAssetAtPath<Material>(ArchiveTopicSetup.Folder+"/Art/Materials/Archive_Topic_Wood.mat");
            timber=Material("Archive_Board_DarkTimber",source.shader,new Color(.28f,.15f,.07f));
            timber.CopyPropertiesFromMaterial(source);timber.SetColor("_Tint",new Color(.29f,.17f,.09f));
            timber.SetVector("_WoodRepeat",new Vector4(1.8f,1.1f,0,0));EditorUtility.SetDirty(timber);
            var lit=Shader.Find("Universal Render Pipeline/Lit");var unlit=Shader.Find("Universal Render Pipeline/Unlit");
            if(lit==null||unlit==null)throw new InvalidOperationException("Existing URP shaders are required.");
            metal=Material("Archive_Board_AntiqueBrass",lit,new Color(.82f,.59f,.27f));
            metal.SetFloat("_Metallic",.16f);metal.SetFloat("_Smoothness",.32f);
            metal.EnableKeyword("_EMISSION");metal.SetColor("_EmissionColor",new Color(.065f,.04f,.012f));
            teal=Material("Archive_Board_Enamel",unlit,new Color(.025f,.105f,.105f));
            parchment=Material("Archive_Board_Parchment",unlit,new Color(.93f,.89f,.76f));
            parchment.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/StageUI/ReadingPaper.png"));
            stone=AssetDatabase.LoadAssetAtPath<Material>(ArchiveTopicSetup.Folder+"/Art/Materials/Archive_Topic_Stone.mat");

            var g=new Shape();
            g.Bevel(new Vector3(0,2.5f,-10.26f),new Vector3(7.65f,3.85f,.5f),.22f,.065f);
            // Carved lower apron and angled mounting brackets add a real silhouette.
            g.Bevel(new Vector3(0,.64f,-10.29f),new Vector3(6.65f,.46f,.42f),.18f,.06f);
            for(int side=-1;side<=1;side+=2)
                g.Bar(new Vector3(side*3.5f,3.95f,-10.38f),new Vector3(side*3.9f,4.5f,-10.38f),.24f,.36f);
            body=Save("Archive_Instruction_Carved_Frame",g,new Vector3(0,2.5f,-10.26f));
            g=new Shape();g.Bevel(new Vector3(0,4.92f,-10.24f),new Vector3(8.7f,1.34f,.54f),.37f,.09f);
            g.Bevel(new Vector3(0,5.68f,-10.24f),new Vector3(1.35f,1.18f,.5f),.36f,.09f);
            header=Save("Archive_Instruction_Crowned_Header",g,new Vector3(0,4.92f,-10.24f));
            g=new Shape();g.Bevel(new Vector3(0,2.5f,-9.99f),new Vector3(6.92f,3.15f,.12f),.12f,.025f);
            paper=Save("Archive_Instruction_Parchment",g,new Vector3(0,2.5f,-9.99f),true);
            g=new Shape();g.Bevel(Vector3.zero,new Vector3(7.86f,.98f,.12f),.26f,.025f);
            inset=Save("Archive_Instruction_Enamel_Inset",g);
            g=new Shape();
            g.Bevel(new Vector3(0,.16f,0),new Vector3(.84f,.32f,.8f),.11f,.035f);
            g.Bevel(new Vector3(0,.43f,0),new Vector3(.64f,.28f,.63f),.08f,.03f);
            g.Bevel(new Vector3(0,2.24f,0),new Vector3(.42f,3.4f,.48f),.055f,.025f);
            g.Bevel(new Vector3(0,4.02f,0),new Vector3(.64f,.28f,.65f),.08f,.035f);
            posts=Save("Archive_Instruction_Stone_Post",g);
            g=new Shape();
            // Raised inner border, with generous blank space around the reading face.
            Border(g,3.55f,.86f,4.15f,-9.875f,.055f);
            Border(g,3.43f,.98f,4.03f,-9.87f,.018f);
            // Header edge is a chamfered brass outline, not another rectangle.
            Outline(g,new Vector3(0,4.92f,-9.905f),8.26f,1.12f,.31f,.045f);
            Outline(g,new Vector3(0,5.69f,-9.91f),1.15f,.99f,.29f,.045f);
            for(int side=-1;side<=1;side+=2)
            {
                foreach(float y in new[]{.48f,3.8f})
                    g.Bevel(new Vector3(side*3.52f,y,-10.25f),new Vector3(.64f,.13f,.68f),.055f,.015f);
                foreach(float y in new[]{1.1f,3.9f})
                {
                    float up=y<2?1:-1;
                    g.Bar(new Vector3(side*3.25f,y,-9.84f),new Vector3(side*3.25f,y+up*.23f,-9.84f),.04f,.035f);
                    g.Bar(new Vector3(side*3.25f,y,-9.84f),new Vector3(side*3.02f,y,-9.84f),.04f,.035f);
                }
                // Fluted metal end caps and single faceted brass fasteners.
                g.Bevel(new Vector3(side*4.05f,4.92f,-9.88f),new Vector3(.17f,.41f,.13f),.055f,.02f);
                foreach(float y in new[]{1.18f,3.83f})g.Gem(new Vector3(side*3.49f,y,-9.81f),.067f,.045f);
            }
            // Open-book crest: embossed cover, page corners and a central spine.
            float z=-9.81f;
            g.Bar(new Vector3(0,5.5f,z),new Vector3(-.36f,5.61f,z),.04f,.04f);
            g.Bar(new Vector3(-.36f,5.61f,z),new Vector3(-.36f,5.94f,z),.04f,.04f);
            g.Bar(new Vector3(-.36f,5.94f,z),new Vector3(0,5.82f,z),.04f,.04f);
            g.Bar(new Vector3(0,5.82f,z),new Vector3(.36f,5.94f,z),.04f,.04f);
            g.Bar(new Vector3(.36f,5.94f,z),new Vector3(.36f,5.61f,z),.04f,.04f);
            g.Bar(new Vector3(.36f,5.61f,z),new Vector3(0,5.5f,z),.04f,.04f);
            g.Bar(new Vector3(0,5.5f,z),new Vector3(0,5.82f,z),.035f,.04f);
            // Bottom seal ties the two supporting posts together visually.
            Outline(g,new Vector3(0,.71f,-9.82f),1.14f,.9f,.23f,.048f);
            brass=Save("Archive_Instruction_Brass_Details",g,new Vector3(0,3,-9.9f));
        }

        static void Apply(ArchiveTopicStation station)
        {
            var root=station.transform;
            Part(root,"Question_Board_Frame",body,timber,new Vector3(0,2.5f,-10.26f));
            Part(root,"Question_Board_Face",paper,parchment,new Vector3(0,2.5f,-9.99f));
            Part(root,"Topic_Header_Backplate",header,timber,new Vector3(0,4.92f,-10.24f));
            var supports=root.Cast<Transform>().Where(t=>t.name=="Board_Support").OrderBy(t=>t.localPosition.x).ToArray();
            if(supports.Length!=2)throw new InvalidOperationException("Expected two existing board supports.");
            for(int i=0;i<2;i++)Configure(supports[i],posts,stone,new Vector3((i==0?-1:1)*3.52f,0,-10.34f));
            Part(root,"Question_Board_Brass_Details",brass,metal,new Vector3(0,3,-9.9f));
            Part(root,"Question_Board_Enamel",inset,teal,new Vector3(0,4.92f,-9.88f));
            var tint=ArchiveCrystalPillar.Palette[station.topic];
            var gemMaterial=Material("Archive_Board_Accent_T"+(station.topic+1),metal.shader,tint*.73f);
            gemMaterial.EnableKeyword("_EMISSION");gemMaterial.SetColor("_EmissionColor",tint*.52f);
            gemMaterial.SetFloat("_Smoothness",.28f);
            var glyph=new Shape();
            // Reuse the saved 3D topic glyph instead of relying on missing font symbols.
            var source=AssetDatabase.LoadAssetAtPath<Mesh>(ArchiveTopicSetup.Folder+"/Art/EnvironmentPolish/Waymark_Glyph_T"+(station.topic+1)+".asset");
            if(source==null)throw new InvalidOperationException("The saved topic glyph is missing.");
            var vs=source.vertices;var ix=source.triangles;
            for(int i=0;i<ix.Length;i+=3)
            {
                var a=vs[ix[i]];var b=vs[ix[i+1]];var c=vs[ix[i+2]];
                if(a.y<2||b.y<2||c.y<2||a.z>0||b.z>0||c.z>0)continue;
                Vector3 P(Vector3 p)=>new Vector3((p.x-.85f)*.68f,.71f+(p.y-2.85f)*.68f,-9.76f+(p.z+.19f)*.68f);
                glyph.Face(P(a),P(b),P(c));
            }
            for(int side=-1;side<=1;side+=2)glyph.Gem(new Vector3(side*3.81f,4.92f,-9.72f),.14f,.085f);
            var emblemPivot=new Vector3(0,3,-9.76f);
            Part(root,"Question_Board_Topic_Emblem",Save("Archive_Instruction_Emblem_T"+(station.topic+1),glyph,emblemPivot),gemMaterial,emblemPivot);
            Undo.RecordObjects(new UnityEngine.Object[]{station.title,station.title.transform,station.board,station.board.transform},"Style board typography");
            station.title.font=StageUITheme.HeadingFont;station.title.fontSharedMaterial=StageUITheme.HeadingFont.material;
            station.title.transform.localPosition=new Vector3(0,4.92f,-9.76f);
            station.title.transform.localRotation=Quaternion.Euler(0,180,0);station.title.transform.localScale=Vector3.one;
            station.title.rectTransform.sizeDelta=new Vector2(7.1f,.83f);
            station.title.enableAutoSizing=true;station.title.fontSize=5.4f;station.title.fontSizeMax=5.4f;station.title.fontSizeMin=2.8f;
            station.title.color=new Color(1,.94f,.77f);station.title.alignment=TextAlignmentOptions.Center;
            station.title.textWrappingMode=TextWrappingModes.NoWrap;station.title.overflowMode=TextOverflowModes.Truncate;
            station.title.richText=false;station.title.raycastTarget=false;
            station.board.transform.localPosition=new Vector3(0,2.5f,-9.82f);
            station.board.transform.localRotation=Quaternion.Euler(0,180,0);station.board.transform.localScale=Vector3.one;
            station.board.rectTransform.sizeDelta=new Vector2(6.12f,2.48f);
            station.board.color=new Color(.15f,.105f,.065f);station.board.alignment=TextAlignmentOptions.Center;
            station.board.margin=new Vector4(.08f,.06f,.08f,.06f);
            station.board.enableAutoSizing=true;station.board.fontSizeMin=2.6f;station.board.fontSizeMax=6.4f;station.board.fontSize=6.4f;
            station.board.overflowMode=TextOverflowModes.Overflow;
            EditorUtility.SetDirty(station.title);EditorUtility.SetDirty(station.board);
            station.title.ForceMeshUpdate();station.board.ForceMeshUpdate();
        }

        static Material Material(string name,Shader shader,Color color)
        {
            var path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader){name=name};AssetDatabase.CreateAsset(material,path);}
            material.enableInstancing=true;material.SetColor("_BaseColor",color);material.SetFloat("_Surface",0);material.renderQueue=2000;
            EditorUtility.SetDirty(material);return material;
        }
        static void Part(Transform parent,string name,Mesh mesh,Material material,Vector3 position)
        {
            var part=parent.Find(name);
            if(part==null){part=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;Undo.RegisterCreatedObjectUndo(part.gameObject,"Add board detail");part.SetParent(parent,false);}
            Configure(part,mesh,material,position);
        }
        static void Configure(Transform part,Mesh mesh,Material material,Vector3 position)
        {
            var filter=part.GetComponent<MeshFilter>();var renderer=part.GetComponent<MeshRenderer>();
            Undo.RecordObjects(new UnityEngine.Object[]{part,filter,renderer},"Style original instruction board");
            part.localPosition=position;part.localRotation=Quaternion.identity;part.localScale=Vector3.one;
            filter.sharedMesh=mesh;renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            EditorUtility.SetDirty(filter);EditorUtility.SetDirty(renderer);
        }
        static void Border(Shape g,float x,float low,float high,float z,float width)
        {
            g.Bar(new Vector3(-x,low,z),new Vector3(x,low,z),width,.06f);
            g.Bar(new Vector3(-x,high,z),new Vector3(x,high,z),width,.06f);
            g.Bar(new Vector3(-x,low,z),new Vector3(-x,high,z),width,.06f);
            g.Bar(new Vector3(x,low,z),new Vector3(x,high,z),width,.06f);
        }
        static void Outline(Shape g,Vector3 p,float width,float height,float cut,float weight)
        {
            var points=Shape.Polygon(width,height,cut);
            for(int i=0;i<points.Length;i++)g.Bar(p+(Vector3)points[i],p+(Vector3)points[(i+1)%points.Length],weight,.055f);
        }
        static Mesh Save(string name,Shape shape,Vector3 pivot=default,bool paperUV=false)
        {
            string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}
            // Keep every pivot on the board, so the existing completion shrink
            // stays in place instead of drawing baked geometry toward the arena origin.
            mesh.Clear();mesh.SetVertices(shape.vertices.Select(p=>p-pivot).ToArray());mesh.SetTriangles(shape.indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var uv=shape.vertices.Select(p=>paperUV?new Vector2(.3f+(p.x/6.92f+.5f)*.4f,.25f+((p.y-2.5f)/3.15f+.5f)*.5f):new Vector2(p.x,p.y)*.45f).ToArray();
            mesh.uv=uv;EditorUtility.SetDirty(mesh);return mesh;
        }
        sealed class Shape
        {
            public readonly List<Vector3> vertices=new List<Vector3>();public readonly List<int> indices=new List<int>();
            public void Face(Vector3 a,Vector3 b,Vector3 c){int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(n);indices.Add(n+1);indices.Add(n+2);}
            public static Vector2[] Polygon(float width,float height,float cut)
            {
                float x=width*.5f,y=height*.5f;cut=Mathf.Min(cut,Mathf.Min(x,y)*.8f);
                return new[]{new Vector2(-x+cut,-y),new Vector2(x-cut,-y),new Vector2(x,-y+cut),new Vector2(x,y-cut),new Vector2(x-cut,y),new Vector2(-x+cut,y),new Vector2(-x,y-cut),new Vector2(-x,-y+cut)};
            }
            public void Bevel(Vector3 center,Vector3 size,float cut,float bevel)
            {
                var outside=Polygon(size.x,size.y,cut);var front=Polygon(size.x-bevel*2,size.y-bevel*2,Mathf.Max(.01f,cut-bevel*.5f));
                for(int i=0;i<8;i++)
                {
                    int j=(i+1)%8;
                    var a=center+(Vector3)outside[i]-Vector3.forward*size.z*.5f;
                    var b=center+(Vector3)outside[j]-Vector3.forward*size.z*.5f;
                    var c=center+(Vector3)outside[i]+Vector3.forward*(size.z*.5f-bevel);
                    var d=center+(Vector3)outside[j]+Vector3.forward*(size.z*.5f-bevel);
                    var e=center+(Vector3)front[i]+Vector3.forward*size.z*.5f;
                    var f=center+(Vector3)front[j]+Vector3.forward*size.z*.5f;
                    Face(a,b,c);Face(b,d,c);Face(c,d,e);Face(d,f,e);
                    Face(center+Vector3.forward*size.z*.5f,e,f);Face(center-Vector3.forward*size.z*.5f,b,a);
                }
            }
            public void Bar(Vector3 a,Vector3 b,float width,float depth)
            {
                var delta=b-a;if(delta.sqrMagnitude<.00001f)return;
                var side=Vector3.Cross(delta.normalized,Vector3.forward)*width*.5f;
                var z=Vector3.forward*depth*.5f;
                var p=new[]{a-side-z,b-side-z,b+side-z,a+side-z,a-side+z,b-side+z,b+side+z,a+side+z};
                int[] ix={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
                for(int i=0;i<ix.Length;i+=3)Face(p[ix[i]],p[ix[i+1]],p[ix[i+2]]);
            }
            public void Gem(Vector3 center,float radius,float depth)
            {
                var r=new[]{Vector3.up,Vector3.right,Vector3.down,Vector3.left};
                for(int i=0;i<4;i++)
                {Face(center+r[(i+1)%4]*radius,center+r[i]*radius,center+Vector3.forward*depth);Face(center+r[i]*radius,center+r[(i+1)%4]*radius,center-Vector3.forward*depth);}
            }
        }
    }
}
