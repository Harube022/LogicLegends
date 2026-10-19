using UnityEngine;

namespace LogicLegends.OpenArchive.Cinematics.VFX
{
    /// <summary>Presentation only. Owns no player lock, outcome, health, hunting, or networking state.</summary>
    [DisallowMultipleComponent]
    public sealed class CrimsonMathBarrierVfx : MonoBehaviour
    {
        public const float AuthoredRadius=4.8f,AuthoredHeight=8;
        public CrimsonMathBarrierStyle style;
        public Transform wallRunes,diagrams,upperRing,lowerRing,sealRunes;
        public Renderer[] effectRenderers;
        public ParticleSystem sparks,fragments;
        public bool destroyAfterDissolve=true;
        public bool IsDissolving {get;private set;}
        public float Formation {get;private set;}
        public float Visibility {get;private set;}
        public float Breakup {get;private set;}
        public float Age {get;private set;}
        float dissolved;
        MaterialPropertyBlock properties;
        static readonly int Dark=Shader.PropertyToID("_DarkRed"),Crimson=Shader.PropertyToID("_Crimson"),Scarlet=Shader.PropertyToID("_Scarlet"),Highlights=Shader.PropertyToID("_RuneHighlights"),Glow=Shader.PropertyToID("_Glow"),Progress=Shader.PropertyToID("_Progress"),Fade=Shader.PropertyToID("_Fade"),Break=Shader.PropertyToID("_Break"),Clock=Shader.PropertyToID("_Clock"),Opacity=Shader.PropertyToID("_Opacity"),Inside=Shader.PropertyToID("_InsideOpacity");
        void OnEnable(){BeginFormation();}
        public void BeginFormation(float elapsed=0){
            if(style==null)return;
            IsDissolving=false;dissolved=0;Age=Mathf.Max(0,elapsed);Visibility=1;Breakup=0;
            Apply();
            if(Application.isPlaying){if(sparks!=null)sparks.Play();if(fragments!=null)fragments.Play();}
        }
        public void Dissolve(float elapsed=0){
            if(IsDissolving)return;
            IsDissolving=true;dissolved=Mathf.Max(0,elapsed);
            if(Application.isPlaying){
                if(sparks!=null)sparks.Stop(true,ParticleSystemStopBehavior.StopEmitting);
                if(fragments!=null){fragments.Stop(true,ParticleSystemStopBehavior.StopEmitting);fragments.Emit(6);}
            }
        }
        void Update(){
            if(style==null)return;
            float dt=Time.deltaTime*style.animationSpeed;Age+=dt;
            if(IsDissolving){dissolved+=dt;Breakup=Mathf.Clamp01(dissolved/style.dissolveSeconds);Visibility=1-Mathf.SmoothStep(0,1,Breakup);}
            Apply();
            if(IsDissolving && Breakup>=1 && destroyAfterDissolve)Destroy(gameObject);
        }
        public void Preview(float age,float dissolve=0){
            Age=Mathf.Max(0,age);IsDissolving=dissolve>0;Breakup=Mathf.Clamp01(dissolve);Visibility=1-Mathf.SmoothStep(0,1,Breakup);Apply();
        }
        void Apply(){
            if(style==null)return;
            Formation=Mathf.SmoothStep(0,1,Mathf.Clamp01(Age/style.formationSeconds));
            transform.localScale=new Vector3(style.radius/AuthoredRadius,style.height/AuthoredHeight,style.radius/AuthoredRadius);
            float angle=Age*style.runeDegreesPerSecond;
            Rotate(wallRunes,angle);Rotate(diagrams,-angle*.48f);Rotate(upperRing,-angle*1.8f);Rotate(lowerRing,angle*1.35f);Rotate(sealRunes,-angle*.7f);
            if(properties==null)properties=new MaterialPropertyBlock();
            properties.SetColor(Dark,style.darkRed);properties.SetColor(Crimson,style.crimson);properties.SetColor(Scarlet,style.scarlet);properties.SetColor(Highlights,style.runeHighlights);
            properties.SetFloat(Glow,style.glowIntensity);properties.SetFloat(Progress,Formation);properties.SetFloat(Fade,Visibility);properties.SetFloat(Break,Breakup);properties.SetFloat(Clock,Age);
            properties.SetFloat(Opacity,style.wallOpacity);properties.SetFloat(Inside,style.insideWallOpacity);
            properties.SetVector("_BarrierCenter",new Vector4(transform.position.x,transform.position.y,transform.position.z,style.radius));
            if(effectRenderers!=null)foreach(var r in effectRenderers)if(r!=null)r.SetPropertyBlock(properties);
            UpdateParticles(sparks,style.scarlet,style.animationSpeed);UpdateParticles(fragments,style.runeHighlights,style.animationSpeed);
        }
        static void Rotate(Transform t,float angle){if(t!=null)t.localRotation=Quaternion.Euler(0,angle,0);}
        static void UpdateParticles(ParticleSystem p,Color c,float speed){if(p!=null){var main=p.main;main.startColor=c;main.simulationSpeed=speed;}}
        void OnDrawGizmosSelected(){if(style==null)return;Gizmos.color=style.scarlet;Gizmos.DrawWireCube(transform.position+Vector3.up*style.height*.5f,new Vector3(style.radius*2,style.height,style.radius*2));}
    }
}
