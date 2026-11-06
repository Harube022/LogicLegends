using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Read-only beacon presentation. Match state, interaction and clocks belong to the field-event owner.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveResonanceBeaconView : MonoBehaviour
    {
        public int id;
        public Renderer[] bodyRenderers=System.Array.Empty<Renderer>();
        public Transform crystal;
        public Renderer crystalRenderer;
        public TMP_Text status;
        public Material energyMaterial;
        public Color readyColor=new Color(.07f,.94f,.79f);
        public Color activeColor=new Color(1,.78f,.28f);
        [Range(0,3)] public float glowIntensity=1.15f;
        public int CurrentState {get;private set;}=-1;
        public bool Visible {get;private set;}
        public int ActiveRendererCount {get;private set;}
        public int CosmeticElementCount {get;private set;}
        public const int MaximumCosmeticElements=9;
        ArchiveFieldMeshBuffer rings;
        MaterialPropertyBlock block;
        Vector3 crystalHome;
        bool initialized;
        int lastSecond=-1,lastLabelState=-1;
        float nextGeometry;
        static readonly int Base=Shader.PropertyToID("_BaseColor"),Emission=Shader.PropertyToID("_EmissionColor"),Tint=Shader.PropertyToID("_Tint"),Glow=Shader.PropertyToID("_Glow");

        void Initialize()
        {
            if(initialized)return;
            block=new MaterialPropertyBlock();if(crystal!=null)crystalHome=crystal.localPosition;
            if(energyMaterial!=null)rings=new ArchiveFieldMeshBuffer(transform,"Beacon_Orbiting_Resonance",energyMaterial);
            initialized=true;
        }
        /// <param name="state">0 cooldown, 1 ready, 2 active.</param>
        public void Present(int state,float remainingSeconds,float readyFraction,float clock,bool nearby,bool reducedMotion=false)
        {
            Initialize();state=Mathf.Clamp(state,0,2);CurrentState=state;Visible=isActiveAndEnabled;
            ActiveRendererCount=0;CosmeticElementCount=0;
            foreach(var renderer in bodyRenderers)
                if(renderer!=null){renderer.enabled=Visible;if(Visible)ActiveRendererCount++;}
            bool detail=Visible&&nearby;
            if(crystalRenderer!=null){crystalRenderer.enabled=Visible;if(Visible)ActiveRendererCount++;}
            if(status!=null&&status.gameObject.activeSelf!=detail)status.gameObject.SetActive(detail);
            if(!Visible){rings?.Hide();return;}
            float fraction=Mathf.Clamp01(readyFraction),pulse=.5f+.5f*Mathf.Sin(clock*(state==2?2.5f:1.15f));
            var tint=state==2?activeColor:state==1?readyColor:Color.Lerp(new Color(.15f,.26f,.28f),readyColor,fraction*.45f);
            if(crystalRenderer!=null)
            {
                block.Clear();block.SetColor(Base,tint*(state==0?.48f:.75f));
                block.SetColor(Emission,tint*(state==0?.12f:glowIntensity*(.70f+pulse*.30f)));
                crystalRenderer.SetPropertyBlock(block);
            }
            if(crystal!=null)
            {
                crystal.localPosition=crystalHome+Vector3.up*(detail&&!reducedMotion?Mathf.Sin(clock*1.15f+id)*.055f:0);
                crystal.localRotation=Quaternion.Euler(0,detail&&!reducedMotion?clock*(state==2?19:9)%360:0,0);
            }
            if(status!=null&&detail)
            {
                int seconds=Mathf.CeilToInt(Mathf.Max(0,remainingSeconds));
                if(seconds!=lastSecond||state!=lastLabelState)
                {
                    lastSecond=seconds;lastLabelState=state;
                    status.text="RESONANCE BEACON\n"+(state==1?"READY  -  USE HAND":state==2?"RESONATING  "+seconds+"s":"RECHARGING  "+seconds+"s");
                }
                status.color=state==0?new Color(.76f,.85f,.82f):Color.Lerp(tint,Color.white,.55f);
                var camera=Camera.main;if(camera!=null)status.transform.rotation=camera.transform.rotation;
                ActiveRendererCount++;
            }
            if(!detail||rings==null){rings?.Hide();return;}
            int moteCount=state==2?6:state==1?3:0;
            if(reducedMotion)moteCount=state==2?3:0;
            CosmeticElementCount=3+moteCount;
            if(Time.unscaledTime<nextGeometry&&rings.renderer.enabled){ActiveRendererCount++;return;}
            nextGeometry=Time.unscaledTime+1/15f;
            rings.Begin();float turn=reducedMotion?0:clock*.15f;
            float strength=state==0?.28f:state==2?1:.72f;
            rings.Ring(new Vector3(0,2.65f,0),.87f,.07f,48,turn,new Color(tint.r,tint.g,tint.b,strength),1,true,Quaternion.Euler(18,0,0));
            rings.Ring(new Vector3(0,2.65f,0),1.08f,.05f,48,-turn*.7f,new Color(1,.78f,.34f,strength*.90f),1,true,Quaternion.Euler(-24,0,28));
            // A physical charge arc makes cooldown readable without relying on hue.
            rings.Ring(new Vector3(0,.47f,0),1.12f,.075f,48,-Mathf.PI*.5f,new Color(tint.r,tint.g,tint.b,.9f),state==0?fraction:1);
            for(int i=0;i<moteCount;i++)
            {
                float a=i*Mathf.PI*2/Mathf.Max(1,moteCount)+turn;
                var p=new Vector3(Mathf.Cos(a)*.92f,2.65f+Mathf.Sin(a*2)*.36f,Mathf.Sin(a)*.92f);
                rings.Shard(p,.045f,.10f,Quaternion.Euler(0,a*Mathf.Rad2Deg,20),new Color(tint.r,tint.g,tint.b,.8f));
            }
            rings.Commit();block.Clear();block.SetColor(Tint,Color.white);block.SetFloat(Glow,glowIntensity);rings.renderer.SetPropertyBlock(block);
            CosmeticElementCount=3+moteCount;if(rings.renderer.enabled)ActiveRendererCount++;
        }
        public void Clear()
        {
            Visible=false;ActiveRendererCount=CosmeticElementCount=0;rings?.Hide();
            if(status!=null)status.gameObject.SetActive(false);
            foreach(var renderer in bodyRenderers)if(renderer!=null)renderer.enabled=false;
            if(crystalRenderer!=null)crystalRenderer.enabled=false;
        }
        void OnDisable()=>Clear();
        void OnDestroy(){rings?.Dispose();rings=null;}
    }
}
