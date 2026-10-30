using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Cosmetic darts for accepted session shots. The existing authority owns hits, ammo and sedation.</summary>
    [DefaultExecutionOrder(10044), DisallowMultipleComponent]
    public sealed class ArchiveTranquilizerVfx : MonoBehaviour
    {
        [Range(35,90)] public float projectileSpeed=60;
        [Range(.5f,1.8f)] public float embeddedSeconds=1;
        const int PoolSize=4;
        const float FreshShotSeconds=.65f;
        sealed class ShotVisual
        {
            public GameObject root;
            public Transform dart;
            public MeshRenderer dartRenderer,muzzle,impact;
            public LineRenderer trail;
            public MaterialPropertyBlock muzzleProperties,impactProperties;
            public Vector3 origin,end,direction;
            public float began,duration;
            public int serial;
            public bool active,hit;
        }
        readonly ShotVisual[] pool=new ShotVisual[PoolSize];
        ArchiveRewardView view;
        ArchiveTopicSession session;
        ArchiveEquippedItems equipment;
        Mesh dartMesh,ringMesh,puffMesh;
        Material shaftMaterial,finMaterial,tipMaterial,energyMaterial;
        string match;
        int observedSerial=-1,nextSlot,lastSlot=-1;
        static readonly int BaseColorId=Shader.PropertyToID("_BaseColor"),ColorId=Shader.PropertyToID("_Color");
        public int VisibleProjectileCount
        {
            get{int count=0;foreach(var slot in pool)if(slot!=null&&slot.active&&slot.dartRenderer.enabled)count++;return count;}
        }
        public int FlyingProjectileCount
        {
            get{int count=0;foreach(var slot in pool)if(slot!=null&&slot.active&&Time.unscaledTime-slot.began<slot.duration)count++;return count;}
        }
        public int LastVisualSerial {get;private set;}=-1;
        public bool LastVisualHit {get;private set;}
        public Vector3 LastVisualOrigin {get;private set;}
        public Vector3 LastVisualEnd {get;private set;}
        public float LastTravelDuration {get;private set;}
        public Vector3 LastLaunchPosition=>LastVisualOrigin;
        public float LastFlightProgress=>lastSlot>=0&&pool[lastSlot]!=null?
            Mathf.Clamp01((Time.unscaledTime-pool[lastSlot].began)/Mathf.Max(.001f,pool[lastSlot].duration)):0;
        public bool LastProjectileInFlight=>lastSlot>=0&&pool[lastSlot]!=null&&pool[lastSlot].active&&LastFlightProgress<1;
        public int PoolCapacity=>PoolSize;
        public Vector3 LastProjectilePosition=>lastSlot>=0&&pool[lastSlot]!=null?pool[lastSlot].dart.position:Vector3.zero;
        public MeshRenderer LastProjectileRenderer=>lastSlot>=0&&pool[lastSlot]!=null?pool[lastSlot].dartRenderer:null;

        /// <returns>True only for a new, recent accepted shot; the caller may play its existing sound.</returns>
        public bool Present(ArchiveRewardView owner)
        {
            view=owner;session=owner!=null?owner.session:null;
            if(session==null||!session.Ready||session.State.survival==null){Clear();return false;}
            if(equipment==null)equipment=session.GetComponent<ArchiveEquippedItems>();
            var state=session.State.survival;
            if(match!=session.State.matchId)
            {
                Clear();match=session.State.matchId;observedSerial=-1;
                LastVisualSerial=-1;LastVisualHit=false;lastSlot=-1;
            }
            if(observedSerial==state.shotSerial)return false;
            bool forward=observedSerial<0||state.shotSerial>observedSerial;
            observedSerial=state.shotSerial;
            double age=ArchiveTopicSession.Clock-state.shotAt;
            // A late join or checkpoint restore never replays old fire, and host handoff
            // carrying the same serial cannot generate a duplicate projectile or sound.
            if(!forward||state.shotSerial<=0||age<-.15||age>FreshShotSeconds)return false;
            if(!Finite(state.shotOrigin)||!Finite(state.shotEnd))return false;
            if(dartMesh==null&&!BuildPool(owner.glowMaterial))return false;
            Vector3 origin=state.shotOrigin;
            if(equipment!=null&&equipment.TryGetMuzzle(state.shotActor,out var muzzle,out _)&&
                Vector3.SqrMagnitude(muzzle-state.shotOrigin)<36)origin=muzzle;
            Vector3 delta=state.shotEnd-origin;
            if(delta.sqrMagnitude<.0025f||delta.sqrMagnitude>1600)return false;
            var slot=pool[nextSlot];lastSlot=nextSlot;nextSlot=(nextSlot+1)%PoolSize;
            slot.origin=origin;slot.end=state.shotEnd;slot.direction=delta.normalized;
            slot.duration=Mathf.Clamp(delta.magnitude/Mathf.Max(1,projectileSpeed),.10f,.6f);
            slot.began=Time.unscaledTime;slot.serial=state.shotSerial;slot.hit=state.shotHit;slot.active=true;
            slot.root.SetActive(true);
            LastVisualSerial=slot.serial;LastVisualHit=slot.hit;LastVisualOrigin=origin;LastVisualEnd=slot.end;LastTravelDuration=slot.duration;
            UpdateSlot(slot,0);return true;
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&
            !float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);
        void LateUpdate()
        {
            if(view==null||!view.isActiveAndEnabled||session==null||!session.Ready||session.State.matchId!=match)
            {Clear();return;}
            foreach(var slot in pool)if(slot!=null&&slot.active)UpdateSlot(slot,Time.unscaledTime-slot.began);
        }
        void UpdateSlot(ShotVisual slot,float age)
        {
            if(age>slot.duration+embeddedSeconds)
            {slot.active=false;slot.root.SetActive(false);return;}
            float flight=Mathf.Clamp01(age/slot.duration);bool arrived=flight>=1;
            Vector3 position=Vector3.Lerp(slot.origin,slot.end,flight);
            slot.dart.SetPositionAndRotation(position,Quaternion.LookRotation(slot.direction));
            float fade=arrived?1-Mathf.SmoothStep(0,1,Mathf.Clamp01((age-slot.duration-.7f)/Mathf.Max(.1f,embeddedSeconds-.7f))):1;
            WorldScale(slot.dart,Vector3.one*Mathf.Max(.001f,fade));
            slot.dartRenderer.enabled=fade>.01f;
            slot.trail.enabled=!arrived&&age>0;
            if(slot.trail.enabled)
            {
                float tail=Mathf.Min(.95f,Vector3.Distance(slot.origin,position));
                slot.trail.SetPosition(0,position-slot.direction*tail);slot.trail.SetPosition(1,position);
            }
            float muzzleTime=age/.11f;
            slot.muzzle.enabled=muzzleTime<1;
            if(slot.muzzle.enabled)
            {
                slot.muzzle.transform.SetPositionAndRotation(slot.origin,Quaternion.LookRotation(slot.direction));
                WorldScale(slot.muzzle.transform,Vector3.one*Mathf.Lerp(.09f,.30f,muzzleTime));
                Tint(slot.muzzle,slot.muzzleProperties,new Color(1,.8f,.43f,(1-muzzleTime)*.65f));
            }
            float impactTime=(age-slot.duration)/.32f;
            slot.impact.enabled=arrived&&impactTime<1;
            if(slot.impact.enabled)
            {
                slot.impact.transform.SetPositionAndRotation(slot.end-slot.direction*.04f,Quaternion.LookRotation(-slot.direction));
                WorldScale(slot.impact.transform,Vector3.one*Mathf.Lerp(.10f,.48f,impactTime));
                Color color=slot.hit?new Color(.25f,1,.65f):new Color(1,.78f,.38f);
                color.a=(1-impactTime)*.7f;Tint(slot.impact,slot.impactProperties,color);
            }
        }
        bool BuildPool(Material source)
        {
            if(source==null||source.shader==null)return false;
            dartMesh=BuildDart();ringMesh=BuildRing();puffMesh=BuildPuff();
            shaftMaterial=CloneMaterial(source,"Dart_Ivory_Shaft",new Color(.92f,.98f,.93f));
            finMaterial=CloneMaterial(source,"Dart_Teal_Fins",new Color(.15f,.85f,.79f));
            tipMaterial=CloneMaterial(source,"Dart_Amber_Tip",new Color(1,.62f,.18f));
            energyMaterial=CloneMaterial(source,"Dart_Pressure_And_Impact",Color.white);
            energyMaterial.SetFloat("_Surface",1);energyMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);energyMaterial.SetFloat("_DstBlend",(float)BlendMode.One);
            energyMaterial.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);energyMaterial.SetFloat("_DstBlendAlpha",(float)BlendMode.One);
            energyMaterial.SetFloat("_ZWrite",0);energyMaterial.SetFloat("_Cull",(float)CullMode.Off);
            energyMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");energyMaterial.SetOverrideTag("RenderType","Transparent");energyMaterial.renderQueue=(int)RenderQueue.Transparent;
            for(int i=0;i<PoolSize;i++)
            {
                var slot=new ShotVisual();pool[i]=slot;
                slot.root=new GameObject("Accepted_Dart_Visual_"+i);slot.root.transform.SetParent(transform,false);
                slot.dartRenderer=MeshObject("Visible_3D_Dart",slot.root.transform,dartMesh);slot.dart=slot.dartRenderer.transform;
                slot.dartRenderer.sharedMaterials=new[]{shaftMaterial,finMaterial,tipMaterial};
                slot.muzzle=MeshObject("Muzzle_Pressure_Ring",slot.root.transform,ringMesh);slot.muzzle.sharedMaterial=energyMaterial;
                slot.impact=MeshObject("Dart_Impact_Puff",slot.root.transform,puffMesh);slot.impact.sharedMaterial=energyMaterial;
                slot.muzzleProperties=new MaterialPropertyBlock();slot.impactProperties=new MaterialPropertyBlock();
                var trailObject=new GameObject("Short_Travel_Trail",typeof(LineRenderer));trailObject.transform.SetParent(slot.root.transform,false);
                slot.trail=trailObject.GetComponent<LineRenderer>();slot.trail.useWorldSpace=true;slot.trail.sharedMaterial=energyMaterial;
                slot.trail.positionCount=2;slot.trail.startWidth=.035f;slot.trail.endWidth=.055f;
                slot.trail.startColor=new Color(.35f,.95f,.85f,0);slot.trail.endColor=new Color(.6f,1,.9f,.65f);
                slot.trail.shadowCastingMode=ShadowCastingMode.Off;slot.trail.receiveShadows=false;slot.trail.numCapVertices=0;
                slot.root.SetActive(false);
            }
            return true;
        }
        static Material CloneMaterial(Material source,string name,Color color)
        {
            var material=new Material(source){name=name,enableInstancing=true};
            material.SetColor(BaseColorId,color);if(material.HasProperty(ColorId))material.SetColor(ColorId,color);
            return material;
        }
        static MeshRenderer MeshObject(string name,Transform parent,Mesh mesh)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;
            renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }
        static void Tint(MeshRenderer renderer,MaterialPropertyBlock properties,Color color)
        {properties.SetColor(BaseColorId,color);properties.SetColor(ColorId,color);renderer.SetPropertyBlock(properties);}
        static void WorldScale(Transform target,Vector3 desired)
        {
            Vector3 parent=target.parent!=null?target.parent.lossyScale:Vector3.one;
            target.localScale=new Vector3(desired.x/Mathf.Max(.001f,Mathf.Abs(parent.x)),desired.y/Mathf.Max(.001f,Mathf.Abs(parent.y)),desired.z/Mathf.Max(.001f,Mathf.Abs(parent.z)));
        }
        static Mesh BuildDart()
        {
            // Twelve shaft side faces, a six-sided tip, and crossed low-poly fins. +Z is the tip.
            var vertices=new System.Collections.Generic.List<Vector3>();
            var shaft=new System.Collections.Generic.List<int>();var fins=new System.Collections.Generic.List<int>();var tip=new System.Collections.Generic.List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,System.Collections.Generic.List<int> indices)
            {int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n);indices.Add(n+2);indices.Add(n+3);}
            const int sides=6;const float radius=.043f;
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                var u=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);var v=new Vector3(Mathf.Cos(b),Mathf.Sin(b),0);
                Quad(u*radius+Vector3.back*.56f,v*radius+Vector3.back*.56f,v*radius+Vector3.back*.08f,u*radius+Vector3.back*.08f,shaft);
                int n=vertices.Count;vertices.Add(u*.067f+Vector3.back*.08f);vertices.Add(v*.067f+Vector3.back*.08f);vertices.Add(Vector3.forward*.09f);
                tip.Add(n);tip.Add(n+1);tip.Add(n+2);
            }
            for(int i=0;i<4;i++)
            {
                float angle=i*Mathf.PI*.5f;Vector3 outward=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                Vector3 a=Vector3.back*.61f,b=outward*.15f+Vector3.back*.59f,c=outward*.12f+Vector3.back*.40f,d=Vector3.back*.34f;
                Quad(a,b,c,d,fins);Quad(d,c,b,a,fins);
            }
            var mesh=new Mesh{name="Archive_Visible_Tranquilizer_Dart"};mesh.SetVertices(vertices);mesh.subMeshCount=3;
            mesh.SetTriangles(shaft,0);mesh.SetTriangles(fins,1);mesh.SetTriangles(tip,2);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh BuildRing()
        {
            const int segments=12;var vertices=new Vector3[(segments+1)*2];var uv=new Vector2[vertices.Length];var triangles=new int[segments*6];
            for(int i=0;i<=segments;i++)
            {
                float angle=i*Mathf.PI*2/segments;var outward=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                vertices[i*2]=outward*.7f;vertices[i*2+1]=outward;uv[i*2]=new Vector2(0,(float)i/segments);uv[i*2+1]=new Vector2(1,(float)i/segments);
                if(i==segments)continue;int n=i*6,v=i*2;triangles[n]=v;triangles[n+1]=v+2;triangles[n+2]=v+1;triangles[n+3]=v+1;triangles[n+4]=v+2;triangles[n+5]=v+3;
            }
            var mesh=new Mesh{name="Archive_Dart_Pressure_Ring"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh BuildPuff()
        {
            // Four crossed diamond wisps in one draw, expanded briefly at the accepted endpoint.
            var vertices=new Vector3[16];var triangles=new int[24];
            for(int i=0;i<4;i++)
            {
                var q=Quaternion.Euler(0,i*45,0);int v=i*4,n=i*6;
                vertices[v]=q*new Vector3(-.8f,0,0);vertices[v+1]=q*new Vector3(0,.8f,0);vertices[v+2]=q*new Vector3(.8f,0,0);vertices[v+3]=q*new Vector3(0,-.8f,0);
                triangles[n]=v;triangles[n+1]=v+1;triangles[n+2]=v+2;triangles[n+3]=v;triangles[n+4]=v+2;triangles[n+5]=v+3;
            }
            var mesh=new Mesh{name="Archive_Dart_Impact_Wisps"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public void Clear()
        {foreach(var slot in pool)if(slot!=null){slot.active=false;slot.root.SetActive(false);}}
        void OnDisable(){Clear();}
        void OnDestroy()
        {
            foreach(var slot in pool)if(slot!=null&&slot.root!=null)Destroy(slot.root);
            if(dartMesh!=null)Destroy(dartMesh);if(ringMesh!=null)Destroy(ringMesh);if(puffMesh!=null)Destroy(puffMesh);
            if(shaftMaterial!=null)Destroy(shaftMaterial);if(finMaterial!=null)Destroy(finMaterial);if(tipMaterial!=null)Destroy(tipMaterial);if(energyMaterial!=null)Destroy(energyMaterial);
        }
    }
}
