using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Finite, colliderless roadside VFX. The owner supplies every phase and timestamp.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchivePathDisturbanceView : MonoBehaviour
    {
        public int id;
        [Min(.5f)] public float halfWidth=1.75f;
        [Min(1)] public float halfLength=3.5f;
        public Material energyMaterial,shardMaterial;
        [Range(0,3)] public float glowIntensity=1.05f;
        public int CurrentPhase {get;private set;}
        public int CurrentKind {get;private set;}
        public bool Visible {get;private set;}
        public int ActiveRendererCount {get;private set;}
        public int CosmeticElementCount {get;private set;}
        public const int MaximumCosmeticElements=80;
        public int TriangleCount=>(seal?.TriangleCount??0)+(energy?.TriangleCount??0)+(solids?.TriangleCount??0);
        ArchiveFieldMeshBuffer seal,energy,solids;
        MaterialPropertyBlock block;
        readonly Vector3[] footprintCorners=new Vector3[4];
        float nextGeometry;
        int drawnPhase=-1,drawnKind=-1;
        static readonly int Tint=Shader.PropertyToID("_Tint"),Glow=Shader.PropertyToID("_Glow"),Base=Shader.PropertyToID("_BaseColor"),Emission=Shader.PropertyToID("_EmissionColor");
        static readonly Color Warning=new Color(1,.34f,.035f),Mist=new Color(.10f,.69f,.95f),Shards=new Color(.80f,.25f,1),Runes=new Color(1,.32f,.14f);
        void Initialize()
        {
            if(block!=null)return;block=new MaterialPropertyBlock();
            if(energyMaterial!=null){seal=new ArchiveFieldMeshBuffer(transform,"Disturbance_Warning_Footprint",energyMaterial);energy=new ArchiveFieldMeshBuffer(transform,"Disturbance_Contained_Energy",energyMaterial);}
            if(shardMaterial!=null)solids=new ArchiveFieldMeshBuffer(transform,"Disturbance_Floating_Shards",shardMaterial);
        }
        /// <param name="phase">0 hidden, 1 warning, 2 active, 3 dissolve.</param>
        /// <param name="kind">0 luminous mist, 1 floating shards, 2 unstable runes.</param>
        public void Present(int phase,int kind,float phase01,float clock,bool nearby,bool reducedMotion=false)
        {
            CurrentPhase=Mathf.Clamp(phase,0,3);CurrentKind=Mathf.Clamp(kind,0,2);
            if(!isActiveAndEnabled||!nearby||CurrentPhase==0||CurrentPhase==3&&phase01>=1){ClearMeshes();return;}
            Initialize();if(seal==null||energy==null){ClearMeshes();return;}
            Visible=true;
            if(Time.unscaledTime<nextGeometry&&drawnPhase==CurrentPhase&&drawnKind==CurrentKind)return;
            nextGeometry=Time.unscaledTime+1/15f;drawnPhase=CurrentPhase;drawnKind=CurrentKind;
            seal.Begin();energy.Begin();solids?.Begin();CosmeticElementCount=0;
            float t=Mathf.Clamp01(phase01),fade=CurrentPhase==3?1-Mathf.SmoothStep(0,1,t):1;
            float form=CurrentPhase==1?Mathf.SmoothStep(0,1,Mathf.Clamp01(t*5)):CurrentPhase==2?Mathf.SmoothStep(0,1,Mathf.Clamp01(t*12)):1;
            float pulse=reducedMotion?.65f:.65f+.35f*Mathf.Sin(clock*(CurrentPhase==1?4:1.8f));
            var tint=CurrentKind==0?Mist:CurrentKind==1?Shards:Runes;
            var border=CurrentPhase==1?Warning:Color.Lerp(tint,Warning,.20f);
            Footprint(border,fade*(CurrentPhase==1?.75f+pulse*.22f:.90f),CurrentPhase==1?Mathf.Max(.1f,t):1);
            if(CurrentPhase==1)WarningRunes(Warning,form*(.45f+pulse*.4f));
            else
            {
                float size=CurrentPhase==3?Mathf.Lerp(1,.08f,t):form;
                if(CurrentKind==0)DrawMist(tint,clock,fade,size,reducedMotion);
                else if(CurrentKind==1)DrawShards(tint,clock,fade,size,reducedMotion,CurrentPhase==3?t:0);
                else DrawRunes(tint,clock,fade,size,reducedMotion);
            }
            seal.Commit();energy.Commit();solids?.Commit(true);
            PaintEnergy(seal,glowIntensity);PaintEnergy(energy,glowIntensity);
            if(solids!=null&&solids.renderer.enabled)
            {
                block.Clear();block.SetColor(Base,tint*.32f);block.SetColor(Emission,tint*.22f*fade);solids.renderer.SetPropertyBlock(block);
            }
            ActiveRendererCount=(seal.renderer.enabled?1:0)+(energy.renderer.enabled?1:0)+(solids!=null&&solids.renderer.enabled?1:0);
        }
        void PaintEnergy(ArchiveFieldMeshBuffer buffer,float glow)
        {block.Clear();block.SetColor(Tint,Color.white);block.SetFloat(Glow,glow);buffer.renderer.SetPropertyBlock(block);}
        static Color Alpha(Color tint,float alpha){tint.a=Mathf.Clamp01(alpha);return tint;}
        void Footprint(Color tint,float alpha,float reveal)
        {
            float x=Mathf.Max(.5f,halfWidth-.08f),z=Mathf.Max(1,halfLength-.08f);
            var corners=footprintCorners;
            corners[0]=new Vector3(-x,.11f,-z);corners[1]=new Vector3(-x,.11f,z);
            corners[2]=new Vector3(x,.11f,z);corners[3]=new Vector3(x,.11f,-z);
            // Short, separated lines read as a contained roadside footprint, never
            // as a solid wall spanning the safe lane beside it.
            for(int edge=0;edge<4;edge++)for(int i=0;i<10;i++)
            {
                if((edge*10+i)/40f>reveal)continue;
                var a=Vector3.Lerp(corners[edge],corners[(edge+1)%4],i/10f);
                var b=Vector3.Lerp(corners[edge],corners[(edge+1)%4],(i+.65f)/10f);
                seal.Line(a,b,.09f,Alpha(tint,alpha));
            }
            foreach(var corner in corners)
            {
                var inward=new Vector3(-Mathf.Sign(corner.x),0,-Mathf.Sign(corner.z)).normalized;
                var p=corner+inward*.24f;var side=Vector3.Cross(Vector3.up,inward)*.10f;
                seal.Triangle(p-side,p+side,p+inward*.28f,Alpha(tint,alpha));
            }
            CosmeticElementCount+=44;
        }
        void WarningRunes(Color tint,float alpha)
        {
            for(int i=0;i<3;i++)
            {
                var p=new Vector3(0,.13f,(i-1)*halfLength*.55f);
                seal.Line(p+new Vector3(-.35f,0,-.20f),p+new Vector3(0,0,.25f),.085f,Alpha(tint,alpha));
                seal.Line(p+new Vector3(0,0,.25f),p+new Vector3(.35f,0,-.20f),.085f,Alpha(tint,alpha));
                seal.Ring(p,.62f,.05f,24,0,Alpha(tint,alpha*.80f),1,true);
            }
            CosmeticElementCount+=9;
        }
        void DrawMist(Color tint,float clock,float fade,float size,bool reduced)
        {
            int count=reduced?6:11;
            for(int i=0;i<count;i++)
            {
                float phase=Mathf.Repeat(clock*(reduced?.08f:.18f)+i*.381966f,1),a=i*2.399963f;
                float x=Mathf.Sin(a)*halfWidth*.67f,z=Mathf.Cos(a)*halfLength*.75f;
                float wave=reduced?0:Mathf.Sin(clock*.7f+i)*.14f;
                var p=new Vector3(x,.20f+phase*.55f,z);
                float width=(.16f+(i%3)*.035f)*size,height=(.65f+(i%4)*.14f)*size;
                var tangent=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                var tip=p+Vector3.up*height+tangent*wave;
                var clear=Alpha(tint,0);var bright=Alpha(tint,fade*Mathf.Sin(phase*Mathf.PI)*.65f);
                energy.Quad(p-tangent*width,p+tangent*width,tip+tangent*width*.12f,tip-tangent*width*.12f,clear,clear,bright,bright);
            }
            // Three ground eddies establish magical mist without full-area alpha cards.
            for(int i=0;i<3;i++)energy.Ring(new Vector3(0,.16f,(i-1)*halfLength*.52f),.54f*size,.035f,32,reduced?0:clock*.08f+i,Alpha(tint,fade*.55f),1,true);
            CosmeticElementCount+=count+3;
        }
        void DrawShards(Color tint,float clock,float fade,float size,bool reduced,float dissolve)
        {
            int count=reduced?6:9;
            for(int i=0;i<count;i++)
            {
                float a=i*2.399963f;
                var p=new Vector3(Mathf.Sin(a)*halfWidth*.64f,.20f+((i*7)%5)*.12f+dissolve*.45f,Mathf.Cos(a)*halfLength*.72f);
                if(!reduced)p.y+=Mathf.Sin(clock*1.25f+i)*.08f;
                float height=(.45f+(i%4)*.20f)*size;var rotation=Quaternion.Euler(10+(i%3)*8,reduced?i*31:clock*11+i*31,(i%2==0?1:-1)*12);
                solids?.Shard(p,.18f*size,height,rotation,Color.white);
                var tip=p+rotation*(Vector3.up*height);
                // The thin bright spine identifies the shard even against dark trees.
                energy.Line(p,tip,.035f*size,Alpha(tint,fade*.85f));
                energy.Ring(new Vector3(p.x,.14f,p.z),.28f*size,.025f,16,a,Alpha(tint,fade*.4f),1,true);
            }
            CosmeticElementCount+=count*3;
        }
        void DrawRunes(Color tint,float clock,float fade,float size,bool reduced)
        {
            for(int i=0;i<4;i++)
            {
                float z=(i-1.5f)*halfLength*.43f,turn=reduced?i*.3f:clock*.18f+i;
                var p=new Vector3(Mathf.Sin(i*2.4f)*halfWidth*.3f,.18f,z);
                energy.Ring(p,.64f*size,.040f,36,turn,Alpha(tint,fade*.7f),1,true);
                energy.Ring(p,.44f*size,.020f,24,-turn,Alpha(tint,fade*.45f));
                for(int line=0;line<3;line++)
                {
                    float a=turn+line*Mathf.PI*2/3;
                    var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    energy.Line(p+direction*.24f*size,p+direction*.54f*size,.045f,Alpha(tint,fade*.8f));
                }
                var tip=p+Vector3.up*((.55f+(reduced?0:Mathf.Sin(clock+i)*.12f))*size);
                energy.Shard(tip,.07f*size,.18f*size,Quaternion.Euler(0,turn*Mathf.Rad2Deg,0),Alpha(tint,fade*.8f));
            }
            CosmeticElementCount+=24;
        }
        void ClearMeshes()
        {Visible=false;ActiveRendererCount=CosmeticElementCount=0;seal?.Hide();energy?.Hide();solids?.Hide();drawnPhase=drawnKind=-1;}
        public void Clear(){CurrentPhase=0;ClearMeshes();}
        void OnDisable()=>Clear();
        void OnDestroy(){seal?.Dispose();energy?.Dispose();solids?.Dispose();seal=energy=solids=null;}
    }
}
