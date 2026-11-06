using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>
    /// Local presentation only. Four pooled summoning seals/ribbons become the
    /// portal's final release; all motion is derived from the shared film progress.
    /// </summary>
    public sealed class ArchiveMatchCinematicFx : IDisposable
    {
        const int MaximumHeroes=4;
        readonly GameObject root;
        readonly Transform[] seals=new Transform[MaximumHeroes],ribbons=new Transform[MaximumHeroes];
        readonly Renderer[] sealRenderers=new Renderer[MaximumHeroes],ribbonRenderers=new Renderer[MaximumHeroes];
        readonly MeshFilter[] sealFilters=new MeshFilter[MaximumHeroes],ribbonFilters=new MeshFilter[MaximumHeroes];
        readonly Mesh sealMesh,ribbonMesh,releaseMesh;
        readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        bool disposed;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),Tint=Shader.PropertyToID("_Tint"),
            ColorProperty=Shader.PropertyToID("_Color"),Emission=Shader.PropertyToID("_EmissionColor");

        public ArchiveMatchCinematicFx(Transform owner,Material glowMaterial)
        {
            if(owner==null)throw new ArgumentNullException(nameof(owner));
            if(glowMaterial==null)throw new ArgumentNullException(nameof(glowMaterial));
            root=new GameObject("Match_Cinematic_Local_Energy"){hideFlags=HideFlags.DontSave};
            root.transform.SetParent(owner,false);
            // The map itself is scaled; progress-controlled dimensions are in
            // world metres regardless of which presentation transform owns us.
            var ownerScale=owner.lossyScale;
            root.transform.localScale=new Vector3(Inverse(ownerScale.x),Inverse(ownerScale.y),Inverse(ownerScale.z));
            sealMesh=MakeSeal();ribbonMesh=MakeRibbons();releaseMesh=MakeRelease();
            for(int i=0;i<MaximumHeroes;i++)
            {
                seals[i]=Part("Hero_"+(i+1)+"_Summoning_Seal",sealMesh,glowMaterial,out sealRenderers[i],out sealFilters[i]);
                ribbons[i]=Part("Hero_"+(i+1)+"_Rising_Energy",ribbonMesh,glowMaterial,out ribbonRenderers[i],out ribbonFilters[i]);
            }
            Hide();
        }
        Transform Part(string name,Mesh mesh,Material material,out Renderer renderer,out MeshFilter filter)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);
            filter=go.GetComponent<MeshFilter>();filter.sharedMesh=mesh;
            renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return go.transform;
        }
        static void Visible(Transform transform,bool visible)
        {if(transform.gameObject.activeSelf!=visible)transform.gameObject.SetActive(visible);}
        static float Inverse(float scale)=>Mathf.Abs(scale)>.0001f?1/scale:1;
        void Paint(Renderer renderer,Color tint,float alpha)
        {
            tint.a*=Mathf.Clamp01(alpha);block.Clear();block.SetColor(BaseColor,tint);block.SetColor(Tint,tint);
            block.SetColor(ColorProperty,tint);block.SetColor(Emission,new Color(tint.r,tint.g,tint.b,1)*alpha);
            renderer.SetPropertyBlock(block);
        }
        static float Envelope(float progress,float inEnd,float outStart)
            =>Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,inEnd,progress))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(outStart,1,progress)));

        /// <summary>Teal/gold floor seals charge, then narrow ribbons reveal each real hero.</summary>
        public void ShowSummon(IReadOnlyList<Vector3> feet,float progress,Color tint)
        {
            if(disposed)return;
            progress=Mathf.Clamp01(progress);int count=Mathf.Min(MaximumHeroes,feet==null?0:feet.Count);
            for(int i=0;i<MaximumHeroes;i++)
            {
                float start=.035f+i*.045f;
                float local=Mathf.InverseLerp(start,1,progress);
                float strength=Envelope(local,.17f,.77f);
                bool visible=i<count&&progress>start&&strength>.001f;
                Visible(seals[i],visible);Visible(ribbons[i],visible);if(!visible)continue;
                sealFilters[i].sharedMesh=sealMesh;ribbonFilters[i].sharedMesh=ribbonMesh;
                var position=feet[i]+Vector3.up*.065f;
                float build=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.03f,.36f,local));
                float converge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.79f,local));
                float radius=Mathf.Lerp(.30f,1.55f,build);
                seals[i].SetPositionAndRotation(position,Quaternion.Euler(0,local*100+i*37,0));
                seals[i].localScale=new Vector3(radius,1,radius);
                ribbons[i].SetPositionAndRotation(position+Vector3.up*(converge*.36f),Quaternion.Euler(0,-local*115+i*41,0));
                float width=Mathf.Lerp(1.36f,.82f,converge);
                ribbons[i].localScale=new Vector3(width,Mathf.Lerp(.18f,4.8f,build),width);
                // The smooth single swell avoids repeated flashes during a close shot.
                Paint(sealRenderers[i],Color.Lerp(tint,Color.white,.15f),strength*.85f);
                Paint(ribbonRenderers[i],tint,strength*(.42f+.24f*converge));
            }
        }

        /// <summary>A warm portal release: one expanding ground wave and rising mathematical shards.</summary>
        public void ShowVictory(Vector3 portalCenter,Quaternion portalRotation,float progress,Color tint)
        {
            if(disposed)return;
            progress=Mathf.Clamp01(progress);
            float strength=Envelope(progress,.20f,.82f);
            for(int i=0;i<MaximumHeroes;i++){Visible(seals[i],i<2&&strength>.001f);Visible(ribbons[i],i<2&&strength>.001f);}
            if(strength<=.001f)return;
            sealFilters[0].sharedMesh=sealMesh;sealFilters[1].sharedMesh=sealMesh;
            ribbonFilters[0].sharedMesh=releaseMesh;ribbonFilters[1].sharedMesh=ribbonMesh;
            float open=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.70f,progress));
            float wave=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,.93f,progress));
            seals[0].SetPositionAndRotation(portalCenter+Vector3.up*.07f,Quaternion.Euler(0,progress*65,0));
            float groundRadius=Mathf.Lerp(2.1f,10.5f,wave);seals[0].localScale=new Vector3(groundRadius,1,groundRadius);
            var focus=portalCenter+Vector3.up*4.6f;
            // A floor seal's +Y normal becomes the portal plane's forward normal.
            seals[1].SetPositionAndRotation(focus,portalRotation*Quaternion.Euler(90,0,progress*48));
            float halo=Mathf.Lerp(2.3f,5.0f,open);seals[1].localScale=new Vector3(halo,1,halo);
            ribbons[0].SetPositionAndRotation(focus+Vector3.up*(progress*2.8f),portalRotation*Quaternion.Euler(0,0,-progress*18));
            float spread=Mathf.Lerp(1.8f,6.4f,open);ribbons[0].localScale=new Vector3(spread,spread,spread);
            ribbons[1].SetPositionAndRotation(portalCenter+Vector3.up*(progress*1.2f),Quaternion.Euler(0,-progress*100,0));
            ribbons[1].localScale=new Vector3(Mathf.Lerp(2.6f,1.1f,open),Mathf.Lerp(1.4f,10.0f,open),Mathf.Lerp(2.6f,1.1f,open));
            Paint(sealRenderers[0],tint,strength*.48f*(1-wave*.65f));
            Paint(sealRenderers[1],Color.Lerp(tint,Color.white,.28f),strength*.70f);
            Paint(ribbonRenderers[0],Color.Lerp(tint,Color.white,.18f),strength*.72f);
            Paint(ribbonRenderers[1],tint,strength*.45f);
        }
        public void Hide()
        {
            if(disposed)return;
            for(int i=0;i<MaximumHeroes;i++){Visible(seals[i],false);Visible(ribbons[i],false);}
        }
        public void Dispose()
        {
            if(disposed)return;Hide();disposed=true;
            Destroy(root);Destroy(sealMesh);Destroy(ribbonMesh);Destroy(releaseMesh);
        }
        static void Destroy(UnityEngine.Object target)
        {if(target!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(target);else UnityEngine.Object.DestroyImmediate(target);}}

        static Mesh MakeSeal()
        {
            var shape=new Shape();shape.Ring(1,.032f,64);shape.Ring(.82f,.016f,56);shape.Ring(.54f,.018f,40);
            for(int i=0;i<20;i++)
            {
                float angle=i*Mathf.PI/10;var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                shape.Line(radial*.88f,radial*(i%5==0?.98f:.94f),.019f,Vector3.up);
            }
            // Four readable geometric connectives/graph marks, built without a font atlas.
            for(int quadrant=0;quadrant<4;quadrant++)
            {
                float angle=quadrant*Mathf.PI*.5f;var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var tangent=Vector3.Cross(Vector3.up,radial);var center=radial*.67f;
                shape.Line(center-tangent*.095f-radial*.07f,center+radial*.09f,.017f,Vector3.up);
                shape.Line(center+radial*.09f,center+tangent*.095f-radial*.07f,.017f,Vector3.up);
            }
            shape.Line(new Vector3(-.26f,0,.18f),new Vector3(.26f,0,.18f),.018f,Vector3.up);
            shape.Line(new Vector3(.26f,0,.18f),new Vector3(-.02f,0,0),.018f,Vector3.up);
            shape.Line(new Vector3(-.02f,0,0),new Vector3(.26f,0,-.18f),.018f,Vector3.up);
            shape.Line(new Vector3(.26f,0,-.18f),new Vector3(-.26f,0,-.18f),.018f,Vector3.up);
            return shape.Save("Archive_Match_Summoning_Seal");
        }
        static Mesh MakeRibbons()
        {
            var shape=new Shape();const int strands=6,segments=9;
            for(int strand=0;strand<strands;strand++)
            {
                float start=strand*Mathf.PI*2/strands;
                for(int segment=0;segment<segments;segment++)
                {
                    float a=segment/(float)segments,b=(segment+1)/(float)segments;
                    Vector3 Point(float t)=>new Vector3(Mathf.Cos(start+t*1.4f)*(.92f-t*.30f),t,Mathf.Sin(start+t*1.4f)*(.92f-t*.30f));
                    var tangent=new Vector3(-Mathf.Sin(start+a*1.4f),0,Mathf.Cos(start+a*1.4f));
                    float width=segment%3==0?.025f:.013f;
                    shape.Quad(Point(a)-tangent*width,Point(b)-tangent*width,Point(b)+tangent*width,Point(a)+tangent*width,true);
                }
                for(int shard=0;shard<3;shard++)
                {
                    float angle=start+shard*.23f;var center=new Vector3(Mathf.Cos(angle)*(1+shard*.16f),.14f+shard*.29f,Mathf.Sin(angle)*(1+shard*.16f));
                    shape.Diamond(center,new Vector3(-Mathf.Sin(angle),0,Mathf.Cos(angle))*.035f,Vector3.up*.032f);
                }
            }
            return shape.Save("Archive_Match_Rising_Ribbons");
        }
        static Mesh MakeRelease()
        {
            var shape=new Shape();
            for(int i=0;i<28;i++)
            {
                float angle=i*2.399963f;float radius=.55f+(i%5)*.115f;
                var radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);var tangent=new Vector3(-radial.y,radial.x,0);
                var center=radial*radius+Vector3.forward*((i%3-1)*.12f);
                shape.Diamond(center,tangent*(.018f+(i%3)*.006f),radial*(.045f+(i%4)*.018f));
                if(i%3==0)shape.Line(radial*.30f,radial*.53f,.009f,Vector3.forward);
            }
            return shape.Save("Archive_Match_Victory_Fragments");
        }
        sealed class Shape
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> triangles=new List<int>();
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,bool doubleSided=false)
            {Triangle(a,b,c);Triangle(a,c,d);if(doubleSided){Triangle(c,b,a);Triangle(d,c,a);}}
            public void Ring(float radius,float thickness,int segments)
            {
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                    var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var q=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                    Quad(p*(radius-thickness),q*(radius-thickness),q*(radius+thickness),p*(radius+thickness),true);
                }
            }
            public void Line(Vector3 a,Vector3 b,float width,Vector3 normal)
            {
                var direction=b-a;if(direction.sqrMagnitude<.000001f)return;
                var side=Vector3.Cross(normal,direction.normalized)*width*.5f;Quad(a-side,b-side,b+side,a+side,true);
            }
            public void Diamond(Vector3 center,Vector3 side,Vector3 length)
                =>Quad(center-length,center+side,center+length,center-side,true);
            public Mesh Save(string name)
            {
                var mesh=new Mesh{name=name,hideFlags=HideFlags.DontSave};
                mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var colors=new Color[vertices.Count];var uv=new Vector2[vertices.Count];
                for(int i=0;i<vertices.Count;i++){colors[i]=Color.white;uv[i]=new Vector2(vertices[i].x,vertices[i].y+vertices[i].z);}
                mesh.colors=colors;mesh.uv=uv;return mesh;
            }
        }
    }
}
