using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local presentation only. The existing encounter supplies the entrance clock.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveGuardianEntranceVfx : MonoBehaviour
    {
        [Range(.6f,1.6f)] public float radiusScale=1;
        [Range(.2f,3)] public float glowIntensity=1.7f;
        public bool Visible {get;private set;}
        public int ActiveRendererCount=>Visible?3:0;
        public int CosmeticElementCount {get;private set;}
        public int GroundSampleCount {get;private set;}
        public int GroundSurfaceCount=>groundSurfaces.Count;
        public const int MaximumCosmeticElements=46;
        const int GroundGrid=9;
        readonly float[] groundHeights=new float[GroundGrid*GroundGrid];
        readonly List<Collider> groundSurfaces=new List<Collider>(24);
        Vector3 sampledCenter;
        float sampledExtent,sampledRadius,lastPresentedTime=-1;
        int sampledAnchor;
        bool groundReady;
        Transform pool;
        Material energy,dust;
        Buffer seal,streaks,fragments;
        static readonly int Tint=Shader.PropertyToID("_Tint"),Glow=Shader.PropertyToID("_Glow");

        /// <summary>Creates three reusable meshes and two local materials once per active lifetime.</summary>
        public void Initialize()
        {
            if(pool!=null)return;
            var energyAsset=Resources.Load<Material>("ArchiveGuardianEntranceEnergy");
            var dustAsset=Resources.Load<Material>("ArchiveGuardianEntranceDust");
            if(energyAsset==null||dustAsset==null)
            {
                Debug.LogError("Guardian entrance VFX resources are missing.",this);return;
            }
            energy=new Material(energyAsset){name="Guardian_Entrance_Energy_Runtime",hideFlags=HideFlags.DontSave};
            dust=new Material(dustAsset){name="Guardian_Entrance_Dust_Runtime",hideFlags=HideFlags.DontSave};
            pool=new GameObject("Guardian_Entrance_Cosmetic_Pool").transform;
            pool.SetParent(transform,false);pool.gameObject.hideFlags=HideFlags.DontSave;
            seal=new Buffer(pool,"Formation_Seal_And_Shockwave",energy,ProjectGround);
            streaks=new Buffer(pool,"Rising_Energy_Wisps",energy);
            fragments=new Buffer(pool,"Ground_Dust_And_Fragments",dust);
            pool.gameObject.SetActive(false);
        }

        /// <summary>Use the existing entrance elapsed/duration; t=0 and t=1 are clear.</summary>
        public void Present(Transform anchor,Bounds monsterBounds,Color tint,float normalizedTime,bool reducedMotion)
        {
            if(!isActiveAndEnabled||anchor==null||normalizedTime<=0||normalizedTime>=1)
            {Clear();return;}
            Initialize();if(pool==null)return;
            // Only this cosmetic pool moves. The anchor, monster, rig and navigation
            // agent remain entirely under their existing owners.
            pool.SetPositionAndRotation(anchor.position,Quaternion.identity);
            var s=transform.lossyScale;
            pool.localScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(s.x)),1/Mathf.Max(.001f,Mathf.Abs(s.y)),1/Mathf.Max(.001f,Mathf.Abs(s.z)));
            float radius=Mathf.Clamp(Mathf.Max(monsterBounds.size.x,monsterBounds.size.z)*.60f,3,10.5f)*radiusScale;
            float height=Mathf.Clamp(monsterBounds.size.y,3,15);
            float t=Mathf.Clamp01(normalizedTime),formation=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.015f,.28f,t));
            if(!groundReady||sampledAnchor!=anchor.GetInstanceID()||Mathf.Abs(sampledRadius-radius)>.1f||t<lastPresentedTime-.05f||
                (new Vector2(anchor.position.x-sampledCenter.x,anchor.position.z-sampledCenter.z)).sqrMagnitude>radius*radius*.25f)
                SampleGround(anchor,radius);
            lastPresentedTime=t;
            float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1,t)),strength=formation*fade;
            energy.SetColor(Tint,new Color(tint.r,tint.g,tint.b,1));energy.SetFloat(Glow,glowIntensity);
            dust.SetColor(Tint,Color.Lerp(new Color(.28f,.24f,.19f),tint*.36f,.3f));dust.SetFloat(Glow,1);
            seal.Begin();streaks.Begin();fragments.Begin();
            float turn=reducedMotion?0:t*.32f;
            DrawSeal(radius,formation,strength,turn);
            DrawShockwave(radius,t,reducedMotion);
            DrawWisps(radius,height,t,strength,reducedMotion);
            DrawFragments(radius,height,t,reducedMotion);
            seal.Commit();streaks.Commit();fragments.Commit();
            pool.gameObject.SetActive(true);Visible=true;
        }

        void DrawSeal(float radius,float formation,float strength,float turn)
        {
            const int segments=72;
            Ring(seal,radius,.025f*radius,segments,turn,strength*.62f,formation,false,.005f);
            Ring(seal,radius*.77f,.015f*radius,segments,-turn*.65f,strength*.48f,formation,true,.005f);
            Ring(seal,radius*.30f,.015f*radius,48,0,strength*.45f,formation,false,.005f);
            for(int i=0;i<12;i++)
            {
                if(i/12f>formation)continue;
                float a=i*Mathf.PI/6+turn;var radial=Radial(a);var tangent=Radial(a+Mathf.PI*.5f);
                var p=radial*radius*.88f;
                // Simple geometric conjunction/disjunction runes avoid font fallback
                // and retain readable silhouettes in the wide entrance camera.
                float sign=i%2==0?1:-1;
                Line(seal,p-tangent*radius*.052f-radial*radius*.04f*sign,p+radial*radius*.045f*sign,radius*.012f,strength*.66f);
                Line(seal,p+radial*radius*.045f*sign,p+tangent*radius*.052f-radial*radius*.04f*sign,radius*.012f,strength*.66f);
                if(i%2==0)Line(seal,radial*radius*.32f,radial*radius*.66f,radius*.010f,strength*.33f);
            }
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3+turn;var p=Radial(a)*radius*.60f;
                RingAt(seal,p,radius*.047f,radius*.010f,12,strength*.60f);
            }
        }
        void DrawShockwave(float radius,float t,bool reduced)
        {
            if(t<.43f||t>.87f)return;
            float q=Mathf.InverseLerp(.43f,.87f,t);
            float r=radius*Mathf.Lerp(reduced?.94f:.52f,reduced?1.12f:1.88f,1-(1-q)*(1-q));
            float alpha=Mathf.Sin(q*Mathf.PI)*(reduced?.22f:.65f);
            Ring(seal,r,radius*(reduced?.018f:.037f),72,0,alpha,1,true,.035f);
        }
        void DrawWisps(float radius,float height,float t,float strength,bool reduced)
        {
            int count=reduced?8:18;CosmeticElementCount=count;
            for(int i=0;i<count;i++)
            {
                float phase=Mathf.Repeat(t*1.6f+i*.618034f,1);
                float a=i*2.399963f+(reduced?0:t*.18f);
                float r=radius*(.68f+Hash(i+7)*.30f);
                float rise=height*(reduced?.22f:.62f),y=phase*rise;
                float alpha=Mathf.Sin(phase*Mathf.PI)*strength*(reduced?.32f:.55f);
                float length=height*(.07f+Hash(i+30)*.11f)*(reduced?.60f:1);
                var basePoint=Radial(a)*r+Vector3.up*y;
                basePoint.y+=GroundLocal(basePoint);
                Vector3 tangent=Radial(a+Mathf.PI*.5f),lean=Radial(a+.5f)*radius*.035f;
                float width=radius*(.012f+Hash(i+50)*.008f);
                // A tapered, single ribbon per wisp; no crossed billboard stack.
                var tip=basePoint+Vector3.up*length+lean;
                streaks.Quad(basePoint-tangent*width,basePoint+tangent*width,tip+tangent*width*.12f,tip-tangent*width*.12f,
                    new Color(1,1,1,alpha*.06f),new Color(1,1,1,alpha*.06f),new Color(1,1,1,alpha),new Color(1,1,1,alpha));
            }
        }
        void DrawFragments(float radius,float height,float t,bool reduced)
        {
            if(t<.47f||t>.94f)return;
            int count=reduced?10:28;CosmeticElementCount+=count;
            for(int i=0;i<count;i++)
            {
                float delay=Hash(i+76)*.06f,q=Mathf.InverseLerp(.47f+delay,.94f,t);
                if(q<=0||q>=1)continue;
                float a=i*2.399963f,travel=radius*(reduced?.06f:.26f)*q;
                float y=Mathf.Sin(q*Mathf.PI)*Mathf.Min(height*.12f,1.25f)*(reduced?.4f:1);
                var p=Radial(a)*(radius*(.67f+Hash(i+10)*.27f)+travel)+Vector3.up*(.025f+y);
                p.y+=GroundLocal(p);
                float size=radius*(.016f+Hash(i+100)*.013f)*Mathf.Pow(1-q,1.3f);
                var tangent=Radial(a+1.57f)*size;var radial=Radial(a)*size;
                var top=p+Vector3.up*size*1.5f;var bottom=p-Vector3.up*size*.35f;
                fragments.Triangle(p+tangent,p-tangent,top,Color.white);
                fragments.Triangle(p-tangent,p+radial,top,new Color(.77f,.77f,.77f,1));
                fragments.Triangle(p+radial,p+tangent,top,new Color(.63f,.63f,.63f,1));
                fragments.Triangle(p-tangent,p+tangent,bottom,new Color(.5f,.5f,.5f,1));
            }
        }
        static Vector3 Radial(float a)=>new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
        static float Hash(int i)=>Mathf.Repeat(Mathf.Sin(i*127.1f+311.7f)*43758.5453f,1);
        static bool GroundSurface(Transform item)
        {
            for(var t=item;t!=null;t=t.parent)
                if(t.name=="Nest_StoneFloor_30m"||t.name=="Island_Grassland"||t.name=="Island_Grassland_WithRiverHoles"||t.name=="Smooth_Walkable_Collision"||t.name=="Nest_Four_Wide_Landings")return true;
            return false;
        }
        void SampleGround(Transform anchor,float radius)
        {
            sampledCenter=anchor.position;sampledRadius=radius;sampledExtent=radius*2;sampledAnchor=anchor.GetInstanceID();
            GroundSampleCount=0;groundSurfaces.Clear();
            foreach(var collider in FindObjectsByType<Collider>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(!collider.isTrigger&&collider.enabled&&GroundSurface(collider.transform))groundSurfaces.Add(collider);
            // Collider.Raycast addresses only the named environmental surfaces;
            // monster, player, chain, prop and pooled FX geometry never participate.
            for(int z=0;z<GroundGrid;z++)for(int x=0;x<GroundGrid;x++)
            {
                float px=sampledCenter.x+Mathf.Lerp(-sampledExtent,sampledExtent,x/(float)(GroundGrid-1));
                float pz=sampledCenter.z+Mathf.Lerp(-sampledExtent,sampledExtent,z/(float)(GroundGrid-1));
                var ray=new Ray(new Vector3(px,sampledCenter.y+30,pz),Vector3.down);
                float nearest=float.PositiveInfinity,y=sampledCenter.y;
                foreach(var surface in groundSurfaces)
                    if(surface!=null&&surface.Raycast(ray,out var hit,70)&&hit.normal.y>.45f&&hit.distance<nearest)
                    {nearest=hit.distance;y=hit.point.y;}
                groundHeights[z*GroundGrid+x]=y;GroundSampleCount++;
            }
            groundReady=true;
        }
        float GroundLocal(Vector3 local)
        {
            if(!groundReady||pool==null)return .10f;
            float gx=Mathf.Clamp01((pool.position.x+local.x-sampledCenter.x+sampledExtent)/(sampledExtent*2))*(GroundGrid-1);
            float gz=Mathf.Clamp01((pool.position.z+local.z-sampledCenter.z+sampledExtent)/(sampledExtent*2))*(GroundGrid-1);
            int x=Mathf.Min(Mathf.FloorToInt(gx),GroundGrid-2),z=Mathf.Min(Mathf.FloorToInt(gz),GroundGrid-2);
            float a=Mathf.Lerp(groundHeights[z*GroundGrid+x],groundHeights[z*GroundGrid+x+1],gx-x);
            float b=Mathf.Lerp(groundHeights[(z+1)*GroundGrid+x],groundHeights[(z+1)*GroundGrid+x+1],gx-x);
            return Mathf.Lerp(a,b,gz-z)-pool.position.y+.10f;
        }
        Vector3 ProjectGround(Vector3 local){local.y+=GroundLocal(local);return local;}
        static void Line(Buffer mesh,Vector3 a,Vector3 b,float width,float alpha)
        {
            var direction=b-a;if(direction.sqrMagnitude<.00001f)return;
            var side=Vector3.Cross(Vector3.up,direction.normalized)*width*.5f;
            mesh.Quad(a-side,a+side,b+side,b-side,new Color(1,1,1,alpha));
        }
        static void RingAt(Buffer mesh,Vector3 center,float radius,float width,int segments,float alpha)
        {
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                mesh.Quad(center+Radial(a)*(radius-width*.5f),center+Radial(b)*(radius-width*.5f),center+Radial(b)*(radius+width*.5f),center+Radial(a)*(radius+width*.5f),new Color(1,1,1,alpha));
            }
        }
        static void Ring(Buffer mesh,float radius,float width,int segments,float turn,float alpha,float formation,bool broken,float lift)
        {
            for(int i=0;i<segments;i++)
            {
                if(i/(float)segments>formation||broken&&(i%12>=9))continue;
                float a=i*Mathf.PI*2/segments+turn,b=(i+1)*Mathf.PI*2/segments+turn;
                var y=Vector3.up*lift;
                mesh.Quad(Radial(a)*(radius-width*.5f)+y,Radial(b)*(radius-width*.5f)+y,Radial(b)*(radius+width*.5f)+y,Radial(a)*(radius+width*.5f)+y,new Color(1,1,1,alpha));
            }
        }
        public void Clear()
        {Visible=false;CosmeticElementCount=0;groundReady=false;lastPresentedTime=-1;if(pool!=null)pool.gameObject.SetActive(false);}
        public void TearDown()
        {
            Clear();seal?.Dispose();streaks?.Dispose();fragments?.Dispose();seal=streaks=fragments=null;
            if(pool!=null)DestroyOwned(pool.gameObject);if(energy!=null)DestroyOwned(energy);if(dust!=null)DestroyOwned(dust);
            pool=null;energy=dust=null;
        }
        void OnDisable()=>TearDown();
        void OnDestroy()=>TearDown();
        static void DestroyOwned(UnityEngine.Object item){if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
        sealed class Buffer
        {
            readonly List<Vector3> vertices=new List<Vector3>(2200);
            readonly List<Color> colors=new List<Color>(2200);
            readonly List<int> indices=new List<int>(3300);
            readonly Mesh mesh;
            readonly Func<Vector3,Vector3> projection;
            public Buffer(Transform parent,string name,Material material,Func<Vector3,Vector3> projection=null)
            {
                this.projection=projection;
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
                mesh=new Mesh{name=name+"_PooledMesh",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
            }
            public void Begin(){vertices.Clear();colors.Clear();indices.Clear();}
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
            {
                if(projection!=null){a=projection(a);b=projection(b);c=projection(c);}
                int index=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);colors.Add(color);colors.Add(color);colors.Add(color);
                indices.Add(index);indices.Add(index+1);indices.Add(index+2);
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)=>Quad(a,b,c,d,color,color,color,color);
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color ca,Color cb,Color cc,Color cd)
            {
                if(projection!=null){a=projection(a);b=projection(b);c=projection(c);d=projection(d);}
                int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);colors.Add(ca);colors.Add(cb);colors.Add(cc);colors.Add(cd);
                indices.Add(i);indices.Add(i+1);indices.Add(i+2);indices.Add(i);indices.Add(i+2);indices.Add(i+3);
            }
            public void Commit(){mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();}
            public void Dispose(){if(mesh!=null)DestroyOwned(mesh);}
        }
    }
}
