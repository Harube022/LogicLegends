using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics.VFX;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using Object=UnityEngine.Object;

namespace LogicLegends.OpenArchive.Editor
{
    public static class CrimsonMathBarrierBuilder
    {
        public const string Folder="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Cinematics/VFX/CrimsonBarrier";
        public const string PrefabPath=Folder+"/Crimson_Mathematical_Prison.prefab";
        public const string Reports="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/CrimsonBarrier/Reports";
        public const string Symbols="∀∃∈∉∪∩⊆∧∨¬→↔∑";
        const float R=CrimsonMathBarrierVfx.AuthoredRadius,H=CrimsonMathBarrierVfx.AuthoredHeight;
        static readonly Color Bright=new Color(1,.8f,.8f,.88f),Dim=new Color(.7f,.2f,.2f,.55f);
        static TMP_FontAsset font;
        static TextMeshPro label;
        sealed class Geometry
        {
            public readonly List<Vector3> v=new List<Vector3>(),n=new List<Vector3>();
            public readonly List<Vector2> uv=new List<Vector2>(),seed=new List<Vector2>();
            public readonly List<Color> colors=new List<Color>();
            public readonly List<int> triangles=new List<int>();
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal,Color color,float height=0,float random=.5f){
                int i=v.Count;v.AddRange(new[]{a,b,c,d});n.AddRange(new[]{normal,normal,normal,normal});
                uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
                for(int k=0;k<4;k++){seed.Add(new Vector2(random,height));colors.Add(color);}
                triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
            }
            public void Line(Vector3 a,Vector3 b,float width,Color color,Vector3 normal,float height=0){
                var side=Vector3.Cross(b-a,normal).normalized*width*.5f;
                Quad(a-side,b-side,b+side,a+side,normal,color,height,Mathf.Repeat(v.Count*.017f,1));
            }
            public void Circle(Func<float,Vector3> point,int segments,float width,Color color,Vector3 normal,float reveal=0){for(int i=0;i<segments;i++)Line(point(i*Mathf.PI*2/segments),point((i+1)*Mathf.PI*2/segments),width,color,normal,reveal);}
            public Mesh Save(string name){
                string path=Folder+"/Meshes/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool fresh=mesh==null;if(fresh)mesh=new Mesh{name=name};mesh.Clear();
                mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetUVs(1,seed);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
                // Shader breakup stays within three metres of the authored surfaces.
                var bounds=mesh.bounds;bounds.Expand(6);mesh.bounds=bounds;
                if(fresh)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);return mesh;
            }
        }
        static Vector3 Polar(float a,float r,float y=0)=>new Vector3(Mathf.Sin(a)*r,y,Mathf.Cos(a)*r);
        static Vector3 Tangent(float a)=>new Vector3(Mathf.Cos(a),0,-Mathf.Sin(a));
        static Vector3 Panel(float x,float y,float a,float height)=>Polar(a+x/R,R+.025f,height+y);
        static void MathLabel(Geometry g,string text,float angle,float height,float letterHeight,bool floor=false){
            label.text=text;label.ForceMeshUpdate();var source=label.mesh;var vertices=source.vertices;var uvs=source.uv;var indices=source.triangles;
            if(vertices.Length==0)throw new InvalidOperationException("Empty math label: "+text);
            var bounds=source.bounds;float scale=letterHeight/Mathf.Max(.001f,bounds.size.y);
            int sides=floor?1:2;
            for(int side=0;side<sides;side++){
                int first=g.v.Count;float sign=side==0?-1:1;float random=Mathf.Repeat(first*.0173f,1);
                foreach(var vertex in vertices){
                    var p=(vertex-bounds.center)*scale;Vector3 point,normal;
                    if(floor){normal=Vector3.up;point=Polar(angle,R*.86f,.06f)+Tangent(angle)*p.x+Polar(angle,1)*p.y;}
                    else{float a=angle+sign*p.x/R;normal=Polar(a,side==0?1:-1);point=Polar(a,R+(side==0?.04f:.02f),height+p.y);}
                    g.v.Add(point);g.n.Add(normal);g.seed.Add(new Vector2(random,floor?0:Mathf.Clamp01(height/H)*.85f));
                    g.colors.Add(new Color(1,side==0?1:0,1,side==0?.92f:.58f));
                }
                g.uv.AddRange(uvs);
                for(int t=0;t<indices.Length;t+=3){
                    int a=indices[t],b=indices[t+1],c=indices[t+2];
                    var generatedNormal=Vector3.Cross(g.v[first+b]-g.v[first+a],g.v[first+c]-g.v[first+a]);
                    if(Vector3.Dot(generatedNormal,g.n[first+a])<0){int swap=b;b=c;c=swap;}
                    g.triangles.Add(first+a);g.triangles.Add(first+b);g.triangles.Add(first+c);
                }
            }
        }
        static void DiagramLine(Geometry g,float angle,float height,Vector2 a,Vector2 b,float width=.025f){g.Line(Panel(a.x,a.y,angle,height),Panel(b.x,b.y,angle,height),width,Dim,Polar(angle,1),height/H*.85f);}
        static void DiagramCircle(Geometry g,float angle,float height,Vector2 center,float x,float y,float width=.025f){g.Circle(t=>Panel(center.x+Mathf.Cos(t)*x,center.y+Mathf.Sin(t)*y,angle,height),32,width,Bright,Polar(angle,1),height/H*.85f);}
        static void Arrow(Geometry g,float a,float h,Vector2 from,Vector2 to){DiagramLine(g,a,h,from,to);var back=(from-to).normalized*.18f;var side=new Vector2(-back.y,back.x)*.55f;DiagramLine(g,a,h,to,to+back+side);DiagramLine(g,a,h,to,to+back-side);}
        static Geometry Wall(){
            var g=new Geometry();for(int i=0;i<64;i++){
                float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;
                int first=g.v.Count;g.Quad(Polar(a,R),Polar(b,R),Polar(b,R,H),Polar(a,R,H),Polar((a+b)*.5f,1),Color.white);
                g.uv[first]=new Vector2(i/64f,0);g.uv[first+1]=new Vector2((i+1)/64f,0);g.uv[first+2]=new Vector2((i+1)/64f,1);g.uv[first+3]=new Vector2(i/64f,1);
            }return g;
        }
        static Geometry Ring(float y){
            var g=new Geometry();foreach(float radius in new[]{R,R-.16f,R+.16f})g.Circle(a=>Polar(a,radius,y),96,radius==R?.045f:.018f,Bright,Vector3.up,y/H*.85f);
            for(int i=0;i<96;i++){
                float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96;
                g.Quad(Polar(a,R,y-.045f),Polar(b,R,y-.045f),Polar(b,R,y+.045f),Polar(a,R,y+.045f),Polar((a+b)*.5f,1),Bright,y/H*.85f);
                g.Quad(Polar(a,R+.015f,y-.13f),Polar(b,R+.015f,y-.13f),Polar(b,R+.015f,y+.13f),Polar(a,R+.015f,y+.13f),Polar((a+b)*.5f,1),new Color(1,.3f,.3f,.13f),y/H*.85f);
            }
            for(int i=0;i<52;i++){float a=i*Mathf.PI*2/52;g.Line(Polar(a,R-.2f,y),Polar(a,R+(i%4==0?.28f:.12f),y),.025f,Dim,Vector3.up,y/H*.85f);}
            return g;
        }
        static Geometry GroundSeal(){
            var g=new Geometry();foreach(float r in new[]{R*.97f,R*.94f,R*.73f,R*.69f,R*.36f})g.Circle(a=>Polar(a,r,.04f),96,r>R*.9f?.03f:.022f,Bright,Vector3.up);
            for(int i=0;i<26;i++){float a=i*Mathf.PI*2/26;g.Line(Polar(a,R*.69f,.04f),Polar(a,R*.73f,.04f),.024f,Dim,Vector3.up);}
            for(int i=0;i<13;i++){float a=i*Mathf.PI*2/13;g.Line(Polar(a,R*.36f,.04f),Polar(a+.09f,R*.66f,.04f),.018f,Dim,Vector3.up);}
            foreach(float offset in new[]{-.62f,.62f})g.Circle(a=>new Vector3(Mathf.Cos(a)*1.18f+offset,.045f,Mathf.Sin(a)*1.18f),64,.035f,Bright,Vector3.up);
            return g;
        }
        static Geometry Diagrams(out Geometry letters){
            var g=new Geometry();letters=new Geometry();float h=4.25f;
            for(int panel=0;panel<5;panel++){
                float a=(panel*72+28)*Mathf.Deg2Rad;
                if(panel==0){
                    DiagramCircle(g,a,h,new Vector2(-.5f,0),.87f,.85f);DiagramCircle(g,a,h,new Vector2(.5f,0),.87f,.85f);
                    MathLabel(letters,"A ∪ B",a,h+1.2f,.43f);MathLabel(letters,"A",a-.2f,h,.31f);MathLabel(letters,"B",a+.2f,h,.31f);
                }else if(panel==1){
                    var nodes=new[]{new Vector2(-1,-.65f),new Vector2(-1,.7f),new Vector2(.95f,.7f),new Vector2(1,-.65f),Vector2.zero};
                    foreach(var edge in new[]{new Vector2Int(0,1),new Vector2Int(1,2),new Vector2Int(2,3),new Vector2Int(3,0),new Vector2Int(0,4),new Vector2Int(1,4),new Vector2Int(3,4)})DiagramLine(g,a,h,nodes[edge.x],nodes[edge.y]);
                    foreach(var node in nodes)DiagramCircle(g,a,h,node,.09f,.09f,.05f);MathLabel(letters,"G=(V,E)",a,h+1.2f,.36f);
                }else if(panel==2){
                    for(int row=0;row<6;row++)DiagramLine(g,a,h,new Vector2(-1.6f,1.05f-row*.42f),new Vector2(1.6f,1.05f-row*.42f),.016f);
                    foreach(float x in new[]{-1.6f,-.55f,.3f,1.6f})DiagramLine(g,a,h,new Vector2(x,-1.05f),new Vector2(x,1.05f),.016f);
                    string[] rows={"P   Q   P∧Q","T   T     T","T   F     F","F   T     F","F   F     F"};
                    for(int row=0;row<rows.Length;row++)MathLabel(letters,rows[row],a,h+.84f-row*.42f,.23f);
                }else if(panel==3){
                    MathLabel(letters,"A={1,2,3}",a,h+.65f,.45f);MathLabel(letters,"A ⊆ B",a,h-.1f,.44f);MathLabel(letters,"∀x∈A",a,h-.85f,.42f);
                    DiagramLine(g,a,h,new Vector2(-1.4f,1.15f),new Vector2(1.4f,1.15f));
                }else{
                    DiagramCircle(g,a,h,new Vector2(-1.05f,0),.44f,1);DiagramCircle(g,a,h,new Vector2(1.05f,0),.44f,1);
                    for(int n=0;n<3;n++){
                        var from=new Vector2(-1.05f,.55f-n*.55f);var to=new Vector2(1.05f,.55f-((n+1)%3)*.55f);
                        DiagramCircle(g,a,h,from,.055f,.055f,.04f);DiagramCircle(g,a,h,to,.055f,.055f,.04f);Arrow(g,a,h,from+Vector2.right*.1f,to-Vector2.right*.1f);
                    }
                    MathLabel(letters,"f:A→B",a,h+1.25f,.4f);
                }
            }return g;
        }
        static TMP_FontAsset Font(){
            string path=Folder+"/Fonts/Crimson_NotoSansMath_SDF.asset";var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(asset!=null)return asset;
            string characters=Symbols+"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789{}(),= :";
            asset=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/NotoSansMath-Regular.ttf"),64,9,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,false);
            asset.name="Crimson_NotoSansMath_SDF";string missing;asset.TryAddCharacters(characters,out missing);
            if(!string.IsNullOrEmpty(missing))throw new InvalidOperationException("Missing required math glyphs: "+missing);
            asset.atlasPopulationMode=AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset,path);AssetDatabase.AddObjectToAsset(asset.material,asset);foreach(var atlas in asset.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,asset);EditorUtility.SetDirty(asset);return asset;
        }
        static Material Material(string name,string shader){
            string path=Folder+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            var found=Shader.Find(shader);if(found==null)throw new InvalidOperationException("Missing shader: "+shader);
            if(mat==null){mat=new Material(found){name=name};AssetDatabase.CreateAsset(mat,path);}else mat.shader=found;
            EditorUtility.SetDirty(mat);return mat;
        }
        static Transform Part(GameObject root,string name,Geometry g,Material mat){
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=g.Save(name);
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;return go.transform;
        }
        static ParticleSystem Particles(GameObject root,string name,Material material,bool shards){
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);var p=go.AddComponent<ParticleSystem>();p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=p.main;main.duration=3;main.loop=true;main.playOnAwake=false;main.maxParticles=shards?8:24;main.startLifetime=new ParticleSystem.MinMaxCurve(shards?1.4f:1.1f,shards?2.5f:1.9f);main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(shards?.1f:.055f,shards?.2f:.14f);main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            var emission=p.emission;emission.rateOverTime=shards?1.5f:10;
            var shape=p.shape;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=R;shape.radiusThickness=.06f;shape.rotation=new Vector3(90,0,0);
            var velocity=p.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;velocity.x=new ParticleSystem.MinMaxCurve(0,0);velocity.z=new ParticleSystem.MinMaxCurve(0,0);velocity.y=new ParticleSystem.MinMaxCurve(shards?.45f:1.1f,shards?1.1f:2.7f);
            var color=p.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.75f,.2f),new GradientAlphaKey(0,1)});color.color=gradient;
            var size=p.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
            var renderer=p.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=.05f;
            if(shards){var mesh=new Geometry();mesh.Quad(new Vector3(-.35f,0,0),new Vector3(0,-1,0),new Vector3(.35f,0,0),new Vector3(0,1,0),Vector3.forward,Color.white);renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=mesh.Save("Glowing_Fragment");}
            return p;
        }
        [MenuItem("Tools/Logic Legends/Crimson Barrier/Build Reusable VFX Assets")]
        public static void Build(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before authoring assets.");
            foreach(string folder in new[]{"Fonts","Meshes","Materials"})Directory.CreateDirectory(Folder+"/"+folder);Directory.CreateDirectory(Reports);AssetDatabase.Refresh();
            font=Font();AssetDatabase.SaveAssets();
            var textObject=new GameObject("Temporary_Math_Glyph_Authoring");label=textObject.AddComponent<TextMeshPro>();label.font=font;label.fontSharedMaterial=font.material;label.fontSize=10;label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.NoWrap;
            GameObject root=null;
            try{
                string stylePath=Folder+"/Crimson_Barrier_Style.asset";var style=AssetDatabase.LoadAssetAtPath<CrimsonMathBarrierStyle>(stylePath);if(style==null){style=ScriptableObject.CreateInstance<CrimsonMathBarrierStyle>();AssetDatabase.CreateAsset(style,stylePath);}
                var wall=Material("Crimson_Transparent_Veil","LogicLegends/CrimsonMath/Veil");var lines=Material("Crimson_Energy_Lines","LogicLegends/CrimsonMath/Energy");var glyphs=Material("Crimson_Math_Glyphs","LogicLegends/CrimsonMath/Energy");glyphs.SetTexture("_MainTex",font.atlasTexture);glyphs.SetFloat("_Glyph",1);glyphs.SetFloat("_Cull",2);
                var sparks=Material("Crimson_Soft_Sparks","LogicLegends/CrimsonMath/Energy");sparks.SetFloat("_Particle",1);
                string texturePath=Folder+"/Materials/Soft_Spark.asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if(texture==null){texture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Soft_Spark",wrapMode=TextureWrapMode.Clamp};var pixels=new Color[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2));}texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,texturePath);}sparks.SetTexture("_MainTex",texture);
                root=new GameObject("Crimson_Mathematical_Prison");var vfx=root.AddComponent<CrimsonMathBarrierVfx>();vfx.style=style;
                Part(root,"Crimson_Cylindrical_Veil",Wall(),wall);
                vfx.lowerRing=Part(root,"Lower_Rotating_Energy_Rings",Ring(.18f),lines);vfx.upperRing=Part(root,"Upper_Rotating_Energy_Rings",Ring(H-.15f),lines);
                Part(root,"Concentric_Mathematical_Seal",GroundSeal(),lines);
                var wallLetters=new Geometry();var sealLetters=new Geometry();
                for(int i=0;i<Symbols.Length;i++){
                    float a=i*Mathf.PI*2/Symbols.Length;MathLabel(wallLetters,Symbols[i].ToString(),a,1.25f,.72f);MathLabel(wallLetters,Symbols[(i+5)%Symbols.Length].ToString(),a+.11f,6.85f,.82f);MathLabel(sealLetters,Symbols[i].ToString(),a,0,.55f,true);
                }
                vfx.wallRunes=Part(root,"Rotating_Discrete_Math_Symbols",wallLetters,glyphs);vfx.sealRunes=Part(root,"Rotating_Ground_Runes",sealLetters,glyphs);
                Geometry diagramLetters;var diagramGeometry=Diagrams(out diagramLetters);vfx.diagrams=Part(root,"Mathematical_Diagrams",diagramGeometry,lines);var texts=Part(root,"Diagram_Notation",diagramLetters,glyphs);texts.SetParent(vfx.diagrams,false);
                var streaks=new Geometry();for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24;float y=.5f+Mathf.Repeat(i*.618f,1)*5.5f;streaks.Line(Polar(a,R+.008f,y),Polar(a,R+.008f,y+.45f+(i%3)*.32f),.018f,new Color(1,.3f,.3f,.38f),Polar(a,1),y/H);}
                var streakMaterial=Material("Crimson_Rising_Streaks","LogicLegends/CrimsonMath/Energy");streakMaterial.SetFloat("_Streak",1);Part(root,"Vertical_Energy_Streaks",streaks,streakMaterial);
                vfx.sparks=Particles(root,"Rising_Crimson_Sparks",sparks,false);vfx.fragments=Particles(root,"Occasional_Glowing_Fragments",lines,true);
                vfx.effectRenderers=root.GetComponentsInChildren<Renderer>(true);vfx.Preview(style.formationSeconds+1);PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();
                var meshes=root.GetComponentsInChildren<MeshFilter>().Select(m=>m.sharedMesh).ToArray();int triangles=meshes.Sum(m=>m.triangles.Length/3);
                File.WriteAllText(Reports+"/Assets.txt","Symbols: "+Symbols+"\nFont: Noto Sans Math; all required glyphs baked into a static 1024² SDF atlas.\nRenderers: "+vfx.effectRenderers.Length+"\nMesh triangles: "+triangles+"\nParticle budget: 24 sparks + 8 fragments.\nNo colliders, rigidbodies, lights, canvas, input or gameplay systems in VFX prefab.\n");
            }finally{if(root!=null)Object.DestroyImmediate(root);Object.DestroyImmediate(textObject);label=null;}
            AssetDatabase.Refresh();
        }
        public static void AttachToExistingEncounter(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            var encounter=Object.FindFirstObjectByType<Cinematics.KhezuCaptureEncounterController>();if(encounter==null)throw new InvalidOperationException("Open the existing cinematic encounter scene.");
            var component=encounter.GetComponent<KhezuBarrierVfxPresentation>();if(component==null)component=encounter.gameObject.AddComponent<KhezuBarrierVfxPresentation>();
            component.encounter=encounter;component.hunting=encounter.GetComponent<Hunting.KhezuHuntingController>();component.barrierPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<CrimsonMathBarrierVfx>();
            EditorUtility.SetDirty(component);EditorSceneManager.MarkSceneDirty(encounter.gameObject.scene);EditorSceneManager.SaveScene(encounter.gameObject.scene);
        }
    }
}
