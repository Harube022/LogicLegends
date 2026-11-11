using UnityEngine;
using UnityEngine.Rendering;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Cinematics;

namespace LogicLegends.OpenArchive.Topics
{
    public enum ArchiveMonsterSignal { Hidden, Sleeping, Awakening }

    /// <summary>Presentation of existing replicated sleep/awakening state. Issues no gameplay commands.</summary>
    [DisallowMultipleComponent,DefaultExecutionOrder(12020)]
    public sealed class ArchiveMonsterStateSignals : MonoBehaviour
    {
        public KhezuHuntingController monster;
        public Transform headAnchor;
        public Material bubbleMaterial,alertMaterial;
        [Min(.1f)] public float bubbleSize=2.2f,alertSize=2.8f;
        [Min(1)] public float fullVisibilityDistance=50,hideDistance=80;
        [Min(20)] public float maximumAlertPixels=110;
        [Min(.5f)] public float breathingSeconds=2.6f;
        public Vector3 bubbleOffset=new Vector3(1.4f,3.4f,3.6f);
        public float alertHeight=5.5f;
        public Color sleepTint=new Color(.55f,1.0f,1.15f),earlyWarning=new Color(1.4f,.85f,.14f),urgentWarning=new Color(1.8f,.15f,.07f);
        public ArchiveMonsterSignal CurrentSignal {get;private set;}
        public int VisibleBubbleCount {get;private set;}
        public bool AlertVisible {get;private set;}
        Transform visuals;Renderer[] bubbles;Renderer alert;
        MaterialPropertyBlock properties;Transform activeHead;RuntimeAnimatorController controller;
        KhezuCinematicJumpscareController cinematic;Animator animator;
        float sleepBlend,alertBlend;
        static readonly int Tint=Shader.PropertyToID("_Tint"),Opacity=Shader.PropertyToID("_Opacity");

        public static ArchiveMonsterSignal SignalFor(KhezuHuntState state,int mistakes,bool tranquilized,bool initialized=true,bool cinematicCovered=false)
        {
            if(!initialized||cinematicCovered)return ArchiveMonsterSignal.Hidden;
            if(tranquilized||state==KhezuHuntState.Sleeping||state==KhezuHuntState.NestSleeping)return ArchiveMonsterSignal.Sleeping;
            if(state==KhezuHuntState.Waking&&mistakes>0)return ArchiveMonsterSignal.Awakening;
            return ArchiveMonsterSignal.Hidden;
        }
        void OnEnable()
        {
            if(monster==null)monster=GetComponent<KhezuHuntingController>();
            animator=GetComponent<Animator>();cinematic=GetComponent<KhezuCinematicJumpscareController>();
            activeHead=null;controller=null;sleepBlend=alertBlend=0;CurrentSignal=ArchiveMonsterSignal.Hidden;
            if(Application.isPlaying)EnsureVisuals();
        }
        void EnsureVisuals()
        {
            if(visuals!=null||bubbleMaterial==null||alertMaterial==null)return;
            visuals=new GameObject("Monster_State_Signals").transform;visuals.SetParent(transform,false);visuals.gameObject.layer=gameObject.layer;
            bubbles=new Renderer[3];for(int i=0;i<3;i++)bubbles[i]=Quad("Sleep_Bubble_"+i,bubbleMaterial);
            alert=Quad("Awakening_Exclamation",alertMaterial);properties=new MaterialPropertyBlock();visuals.gameObject.SetActive(false);
        }
        Renderer Quad(string name,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name=name;go.layer=gameObject.layer;go.transform.SetParent(visuals,false);
            var collider=go.GetComponent<Collider>();if(collider!=null){collider.enabled=false;if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);}
            var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return renderer;
        }
        void LateUpdate()
        {
            var camera=Camera.main;
            if(monster==null||camera==null){Hide();return;}
            var session=ArchiveTopicSession.Current;
            bool covered=cinematic!=null&&cinematic.IsPlaying||session!=null&&(session.NightEntranceActive||ArchiveMatchCinematic.Covers(session));
            var signal=SignalFor(monster.State,monster.Mistakes,monster.IsTranquilized,monster.Initialized,covered);
            float urgency=Mathf.Clamp01((monster.Mistakes-1)/3f);
            Present(camera,HeadPosition(),signal,urgency,(float)(KhezuHuntingController.EncounterClock%3600),Time.unscaledDeltaTime);
        }
        Vector3 HeadPosition()
        {
            if(headAnchor!=null&&headAnchor.gameObject.activeInHierarchy)return headAnchor.position;
            var current=animator==null?null:animator.runtimeAnimatorController;
            if(activeHead==null||!activeHead.gameObject.activeInHierarchy||current!=controller)
            {
                controller=current;activeHead=null;
                foreach(var bone in GetComponentsInChildren<Transform>(false))if(bone.name=="Head"){activeHead=bone;break;}
            }
            return activeHead!=null?activeHead.position:transform.position+Vector3.up*3;
        }
        public void Present(Camera camera,Vector3 head,ArchiveMonsterSignal signal,float urgency,float time,float deltaTime)
        {
            EnsureVisuals();CurrentSignal=signal;
            if(visuals==null||camera==null){Hide();return;}
            float distance=Vector3.Distance(camera.transform.position,head);
            float opacity=ArchiveWorldLabelLayout.DistanceOpacity(distance,fullVisibilityDistance,hideDistance);
            if(camera.WorldToViewportPoint(head).z<=0||opacity<=.005f){Hide();return;}
            sleepBlend=Mathf.MoveTowards(sleepBlend,signal==ArchiveMonsterSignal.Sleeping?1:0,Mathf.Max(0,deltaTime)*6);
            alertBlend=Mathf.MoveTowards(alertBlend,signal==ArchiveMonsterSignal.Awakening?1:0,Mathf.Max(0,deltaTime)*7);
            visuals.gameObject.SetActive(sleepBlend>.005f||alertBlend>.005f);
            float scale=Mathf.Clamp(Mathf.Abs(transform.lossyScale.x)/2.5f,.55f,1.8f);
            Vector3 face=head+transform.right*bubbleOffset.x*scale+Vector3.up*bubbleOffset.y*scale+transform.forward*bubbleOffset.z*scale;
            VisibleBubbleCount=0;
            for(int i=0;i<3;i++)
            {
                float cycle=Mathf.Repeat(time*.38f+i*.43f,1),breath=.77f+.23f*Mathf.Sin(time*Mathf.PI*2/Mathf.Max(.5f,breathingSeconds));
                float size=bubbleSize*scale*(i==0?breath:Mathf.Lerp(.18f,.38f,cycle));
                Vector3 position=i==0?face:face+camera.transform.right*(.3f+cycle*.7f)*scale+Vector3.up*(.6f+cycle*2.3f)*scale;
                float alpha=sleepBlend*opacity*(i==0?.85f:Mathf.Sin(cycle*Mathf.PI)*.58f);
                Draw(bubbles[i],camera,position,size,sleepTint,alpha);if(alpha>.01f)VisibleBubbleCount++;
            }
            float pulse=1+Mathf.Sin(time*Mathf.Lerp(5,11,urgency))*.07f;
            float worldLimit=2*Mathf.Max(1,distance)*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)/Mathf.Max(1,camera.pixelHeight)*maximumAlertPixels;
            float alertWorldSize=Mathf.Min(alertSize*scale*(1+urgency*.15f),worldLimit)*pulse;
            Vector3 alertPosition=head+Vector3.up*(alertHeight*scale+Mathf.Sin(time*3)*.12f*scale);
            float alertAlpha=alertBlend*opacity;Draw(alert,camera,alertPosition,alertWorldSize,Color.Lerp(earlyWarning,urgentWarning,urgency),alertAlpha);
            AlertVisible=alertAlpha>.01f;
        }
        void Draw(Renderer renderer,Camera camera,Vector3 position,float worldSize,Color tint,float alpha)
        {
            renderer.enabled=alpha>.005f;if(!renderer.enabled)return;
            var t=renderer.transform;t.position=position;t.rotation=camera.transform.rotation;
            var parentScale=t.parent.lossyScale;t.localScale=new Vector3(worldSize/Mathf.Max(.001f,Mathf.Abs(parentScale.x)),worldSize/Mathf.Max(.001f,Mathf.Abs(parentScale.y)),1);
            properties.Clear();properties.SetColor(Tint,tint);properties.SetFloat(Opacity,alpha);renderer.SetPropertyBlock(properties);
        }
        void Hide(){if(visuals!=null)visuals.gameObject.SetActive(false);sleepBlend=alertBlend=0;VisibleBubbleCount=0;AlertVisible=false;}
        void OnDisable(){Hide();CurrentSignal=ArchiveMonsterSignal.Hidden;}
    }
}
