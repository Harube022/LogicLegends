using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Read-only scenery over accepted topic progress. Never starts, solves or times a challenge.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveTopicLandmark : MonoBehaviour
    {
        public ArchiveTopicStation station;
        public Transform[] restoringFragments;
        public Renderer[] fragmentRenderers;
        public Vector3[] assembledPositions, brokenPositions;
        public Quaternion[] assembledRotations, brokenRotations;
        public Renderer channels;
        public Transform completionWave;
        public TMP_Text identity;
        [Min(.1f)] public float restorationSeconds=1.3f;
        [Range(0,4)] public float glowIntensity=1.15f;
        [Min(.1f)] public float animationSpeed=1;
        [Min(10)] public float animateWithin=105;
        [Min(20)] public float visibleWithin=250;
        [Min(.1f)] public float completionWaveSeconds=1.65f;
        public float RestorationProgress { get; private set; }
        public int ObservedCompletionCount { get; private set; }
        public float CurrentProgress => RestorationProgress;
        public float TargetProgress => target;
        public int CompletionBurstCount => ObservedCompletionCount;
        public bool CompletionActive => CompletionWaveVisible;
        public bool IsAnimatingNearby { get; private set; }
        public bool CompletionWaveVisible => completionWave!=null&&completionWave.gameObject.activeSelf;

        static readonly int Base=Shader.PropertyToID("_BaseColor"),Emission=Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        Renderer[] renderers;
        Renderer waveRenderer;
        string match;
        int reset=-1,revision=-1;
        bool observed,wasComplete;
        float nextRead,target,pulseAt=-100;

        void Awake()
        {
            block=new MaterialPropertyBlock();
            renderers=GetComponentsInChildren<Renderer>(true);
            if(completionWave!=null){waveRenderer=completionWave.GetComponent<Renderer>();completionWave.gameObject.SetActive(false);}
            if(station==null)station=GetComponentInParent<ArchiveTopicStation>();
        }
        void OnEnable(){observed=false;revision=-1;nextRead=0;pulseAt=-100;}
        Color Tint => ArchiveCrystalPillar.Palette[Mathf.Clamp(station==null?0:station.topic,0,9)];
        void Paint(Renderer renderer,float strength,float emission)
        {
            if(renderer==null)return;
            block.Clear();block.SetColor(Base,Tint*strength);block.SetColor(Emission,Tint*emission);renderer.SetPropertyBlock(block);
        }
        void LateUpdate()
        {
            var session=station==null?null:station.session;
            if(session==null||!session.Ready||session.State.stations==null||station.topic<0||station.topic>=session.State.stations.Length)return;
            bool sampled=false;
            if(Time.unscaledTime>=nextRead)
            {
                sampled=true;nextRead=Time.unscaledTime+.2f;
                var local=Player.LocalInstance;
                float distance=local==null?0:Vector3.SqrMagnitude(local.transform.position-transform.position);
                IsAnimatingNearby=distance<animateWithin*animateWithin;
                bool visible=distance<visibleWithin*visibleWithin;
                foreach(var renderer in renderers)if(renderer!=null)renderer.enabled=visible;
                if(identity!=null)identity.enabled=visible&&distance<animateWithin*animateWithin;
                var state=session.State.stations[station.topic];
                bool newContext=match!=session.State.matchId||reset!=state.testResetSerial;
                if(newContext){match=session.State.matchId;reset=state.testResetSerial;observed=false;revision=-1;pulseAt=-100;}
                if(revision!=session.State.revision)
                {
                    revision=session.State.revision;
                    // Only accepted answer receipts restore the scenery. In particular an
                    // admin-clear cannot manufacture academic work or a crystal team bonus.
                    target=ArchiveCrystalProgress.Body(session.State,station.topic);
                    bool complete=state.phase==2;
                    if(observed&&!wasComplete&&complete&&IsAnimatingNearby)
                    {pulseAt=Time.unscaledTime;ObservedCompletionCount++;}
                    if(!observed)RestorationProgress=target; // No celebratory replay for late joins or restores.
                    observed=true;wasComplete=complete;
                }
            }
            if(!observed)return;
            if(!IsAnimatingNearby&&!sampled)return;
            float speed=1-Mathf.Exp(-Time.unscaledDeltaTime*4*Mathf.Max(.1f,animationSpeed)/Mathf.Max(.1f,restorationSeconds));
            RestorationProgress=IsAnimatingNearby?Mathf.Lerp(RestorationProgress,target,speed):target;
            int count=restoringFragments==null?0:restoringFragments.Length;
            for(int i=0;i<count;i++)
            {
                if(restoringFragments[i]==null||i>=assembledPositions.Length||i>=brokenPositions.Length||i>=assembledRotations.Length||i>=brokenRotations.Length)continue;
                float part=Mathf.Clamp01(RestorationProgress*count-i);
                restoringFragments[i].localPosition=Vector3.Lerp(brokenPositions[i],assembledPositions[i],part);
                restoringFragments[i].localRotation=Quaternion.Slerp(brokenRotations[i],assembledRotations[i],part);
                if(i<fragmentRenderers.Length)Paint(fragmentRenderers[i],Mathf.Lerp(.20f,.78f,part),.015f+part*glowIntensity*.8f);
            }
            Paint(channels,Mathf.Lerp(.10f,.65f,RestorationProgress),glowIntensity*RestorationProgress*.65f);
            float phase=(Time.unscaledTime-pulseAt)/Mathf.Max(.1f,completionWaveSeconds);
            bool wave=IsAnimatingNearby&&phase>=0&&phase<1;
            if(completionWave!=null)
            {
                if(completionWave.gameObject.activeSelf!=wave)completionWave.gameObject.SetActive(wave);
                if(wave)
                {
                    float spread=Mathf.Lerp(.55f,2.6f,phase);completionWave.localScale=new Vector3(spread,1,spread);
                    Paint(waveRenderer,(1-phase)*.7f,glowIntensity*(1-phase)*2);
                }
            }
        }
    }
}
