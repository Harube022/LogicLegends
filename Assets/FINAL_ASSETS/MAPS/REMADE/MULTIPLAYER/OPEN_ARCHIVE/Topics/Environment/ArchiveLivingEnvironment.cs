using System;
using System.Collections.Generic;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local decorative presentation only. Existing colliders, LODs, footsteps and shared match state remain authoritative.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveLivingEnvironment : MonoBehaviour
    {
        public ArchiveTopicSession session;
        public MeshFilter[] foliageMeshes=Array.Empty<MeshFilter>();
        [Tooltip("Unique original/readable source pairs. Originals stay on every tree until its nearby leaf animation is active.")]
        public Mesh[] originalFoliageSources=Array.Empty<Mesh>(),readableFoliageSources=Array.Empty<Mesh>();
        public Vector3[] groveCenters=Array.Empty<Vector3>();
        public Transform[] motes=Array.Empty<Transform>();
        public Material leafMaterial,fireflyMaterial;
        public AudioSource[] riverVoices=Array.Empty<AudioSource>(),birdVoices=Array.Empty<AudioSource>();
        [Header("Nearby foliage only - bark vertices never move")]
        [Range(0,8)] public int maximumAnimatedCanopies=8;
        [Range(12,100)] public float foliageDistance=62;
        [Range(0,.12f)] public float swayStrength=.055f;
        [Range(.1f,2)] public float windSpeed=.75f;
        [Range(2,10)] public float foliageUpdatesPerSecond=10;
        [Header("Opaque decorative motes - no realtime lights")]
        [Range(0,24)] public int maximumVisibleMotes=18;
        [Range(8,70)] public float moteDistance=42;
        [Range(.1f,2)] public float moteSpeed=.65f;
        [Header("Localized existing ambience")]
        [Range(0,.25f)] public float riverVolume=.12f;
        [Range(0,.15f)] public float birdVolume=.055f;
        [Range(12,70)] public float audioDistance=44;
        public int AnimatedCanopyCount {get;private set;}
        public int VisibleMoteCount {get;private set;}
        public int ActiveAmbientVoiceCount {get;private set;}
        public int CachedLeafMeshCount=>sourceCache.Count;
        public bool IsNight=>session!=null&&session.Ready&&session.State.worldTime!=null&&session.State.worldTime.phase==3;

        sealed class Source
        {
            public Mesh mesh;public Vector3[] vertices;public int[] leaves;
        }
        sealed class Canopy
        {
            public MeshFilter filter;public Renderer renderer;public Source source;
            public Mesh original,animated;public Vector3[] work;
        }
        readonly Dictionary<Mesh,Source> sourceCache=new Dictionary<Mesh,Source>();
        readonly List<Canopy> canopies=new List<Canopy>(),nearby=new List<Canopy>();
        readonly List<int> nearGroves=new List<int>();
        Renderer[] moteRenderers;int[] moteGroves;
        float nextScan,nextWind;Vector3 observer;bool initialized;

        void Awake()
        {
            var readable=new Dictionary<Mesh,Mesh>();
            for(int i=0;i<Mathf.Min(originalFoliageSources.Length,readableFoliageSources.Length);i++)
                if(originalFoliageSources[i]!=null&&readableFoliageSources[i]!=null)readable[originalFoliageSources[i]]=readableFoliageSources[i];
            foreach(var filter in foliageMeshes)
            {
                if(filter==null||filter.sharedMesh==null)continue;
                var renderer=filter.GetComponent<Renderer>();if(renderer==null||renderer.isPartOfStaticBatch)continue;
                var original=filter.sharedMesh;var mesh=original;
                if(!mesh.isReadable&&!readable.TryGetValue(original,out mesh))continue;
                if(mesh==null||!mesh.isReadable)continue;
                if(!sourceCache.TryGetValue(original,out var source))
                {
                    var tags=new List<Vector4>();mesh.GetUVs(2,tags);
                    if(tags.Count!=mesh.vertexCount)continue;
                    var leafIndices=new List<int>();for(int i=0;i<tags.Count;i++)if(tags[i].w>.5f)leafIndices.Add(i);
                    if(leafIndices.Count==0)continue;
                    source=new Source{mesh=mesh,vertices=mesh.vertices,leaves=leafIndices.ToArray()};sourceCache.Add(original,source);
                }
                canopies.Add(new Canopy{filter=filter,renderer=renderer,source=source,original=original});
            }
            moteRenderers=new Renderer[motes.Length];moteGroves=new int[motes.Length];
            for(int i=0;i<motes.Length;i++){moteGroves[i]=-1;if(motes[i]!=null){moteRenderers[i]=motes[i].GetComponent<Renderer>();motes[i].gameObject.SetActive(false);}}
            foreach(var source in riverVoices)Route(source);foreach(var source in birdVoices)Route(source);
            initialized=true;
        }
        static void Route(AudioSource source){if(source!=null){source.playOnAwake=false;source.Stop();source.volume=0;AudioVolumeSettings.Route(source,GameAudioChannel.SoundFX);}}
        void LateUpdate()
        {
            if(!initialized)return;
            var player=Player.LocalInstance;
            if(session==null||!session.Ready||player==null||Time.timeScale<=0)
            {ReleaseAll();HideMotes();StopAudio();return;}
            observer=player.transform.position;
            float now=Time.unscaledTime;
            if(now>=nextScan){nextScan=now+.75f;SelectNearby();}
            float clock=(float)(ArchiveTopicSession.Clock%4096);
            if(now>=nextWind){nextWind=now+1/Mathf.Clamp(foliageUpdatesPerSecond,2,10);AnimateCanopies(clock);}
            AnimateMotes(clock,IsNight);
            RefreshAudio(IsNight);
        }
        void SelectNearby()
        {
            nearby.Clear();float radius=foliageDistance*foliageDistance;
            foreach(var canopy in canopies)
                if(canopy.filter!=null&&canopy.renderer!=null&&canopy.renderer.enabled&&canopy.renderer.isVisible&&
                    canopy.filter.gameObject.activeInHierarchy&&(canopy.renderer.bounds.center-observer).sqrMagnitude<radius)nearby.Add(canopy);
            nearby.Sort((a,b)=>(a.renderer.bounds.center-observer).sqrMagnitude.CompareTo((b.renderer.bounds.center-observer).sqrMagnitude));
            int keep=Mathf.Clamp(maximumAnimatedCanopies,0,8);
            if(nearby.Count>keep)nearby.RemoveRange(keep,nearby.Count-keep);
            foreach(var canopy in canopies)if(canopy.animated!=null&&!nearby.Contains(canopy))Release(canopy);
            foreach(var canopy in nearby)
            {
                if(canopy.animated!=null)continue;
                canopy.animated=Instantiate(canopy.source.mesh);canopy.animated.name=canopy.source.mesh.name+"_Local_Leaf_Wind";
                canopy.animated.MarkDynamic();var bounds=canopy.animated.bounds;bounds.Expand(.3f);canopy.animated.bounds=bounds;
                canopy.work=(Vector3[])canopy.source.vertices.Clone();canopy.filter.sharedMesh=canopy.animated;
            }
            AnimatedCanopyCount=nearby.Count;
            nearGroves.Clear();radius=moteDistance*moteDistance;
            for(int i=0;i<groveCenters.Length;i++)if(Horizontal(groveCenters[i]-observer)<radius)nearGroves.Add(i);
            nearGroves.Sort((a,b)=>Horizontal(groveCenters[a]-observer).CompareTo(Horizontal(groveCenters[b]-observer)));
            if(nearGroves.Count>8)nearGroves.RemoveRange(8,nearGroves.Count-8);
            for(int i=0;i<moteGroves.Length;i++)
            {
                int old=moteGroves[i];
                // Keep a mote rooted in its grove while visible; moving the camera never drags it.
                if(old>=0&&Horizontal(groveCenters[old]-observer)<radius)continue;
                moteGroves[i]=nearGroves.Count==0?-1:nearGroves[i%nearGroves.Count];
            }
        }
        static float Horizontal(Vector3 value)=>value.x*value.x+value.z*value.z;
        void AnimateCanopies(float time)
        {
            foreach(var canopy in nearby)
            {
                if(canopy.animated==null||canopy.filter==null)continue;
                float phase=canopy.filter.transform.position.x*.07f+canopy.filter.transform.position.z*.09f;
                foreach(int i in canopy.source.leaves)
                {
                    var p=canopy.source.vertices[i];float ripple=time*windSpeed+phase+p.y*.5f;
                    float strength=swayStrength*(.65f+.35f*Mathf.Sin(time*.31f+phase));
                    canopy.work[i]=p+new Vector3(Mathf.Sin(ripple)*strength,0,Mathf.Cos(ripple*.83f)*strength*.6f);
                }
                canopy.animated.vertices=canopy.work;
            }
        }
        void AnimateMotes(float time,bool night)
        {
            VisibleMoteCount=0;int budget=Mathf.Min(Mathf.Clamp(maximumVisibleMotes,0,24),night?18:12);
            bool quiet=session.NightEntranceActive;
            for(int i=0;i<motes.Length;i++)
            {
                var mote=motes[i];if(mote==null)continue;
                int grove=moteGroves[i];bool visible=!quiet&&i<budget&&grove>=0;
                if(mote.gameObject.activeSelf!=visible)mote.gameObject.SetActive(visible);if(!visible)continue;
                float phase=i*2.39996f;float speed=time*moteSpeed;var anchor=groveCenters[grove];
                if(night)
                {
                    mote.position=new Vector3(anchor.x+Mathf.Sin(speed*.7f+phase)*4,Mathf.Max(.8f,anchor.y-3)+Mathf.Sin(speed*1.1f+phase)*.8f,anchor.z+Mathf.Cos(speed*.61f+phase)*4);
                    mote.localScale=Vector3.one*(.11f+(.5f+.5f*Mathf.Sin(speed*2+phase))*.07f);
                }
                else if(i%3==0)
                {
                    float falling=Mathf.Repeat(speed*.25f+i*.21f,1);
                    mote.position=anchor+new Vector3(Mathf.Sin(speed*.4f+phase)*3,-falling*4,Mathf.Cos(speed*.35f+phase)*3);
                    mote.localScale=new Vector3(.4f,.24f,.24f);
                }
                else
                {
                    mote.position=anchor+new Vector3(Mathf.Sin(speed*.6f+phase)*4,-2+Mathf.Sin(speed+phase),Mathf.Cos(speed*.73f+phase)*4);
                    mote.localScale=new Vector3(.42f,.16f+Mathf.Abs(Mathf.Sin(speed*9+phase))*.2f,.28f);
                }
                mote.rotation=Quaternion.Euler(20+Mathf.Sin(speed+phase)*25,time*(night?18:32)+i*53,Mathf.Sin(speed*.7f+phase)*30);
                if(moteRenderers[i]!=null){var material=night?fireflyMaterial:leafMaterial;if(moteRenderers[i].sharedMaterial!=material)moteRenderers[i].sharedMaterial=material;}
                VisibleMoteCount++;
            }
        }
        void RefreshAudio(bool night)
        {
            ActiveAmbientVoiceCount=0;float duck=session.NightEntranceActive ? .12f : 1;
            if(session.worldProgression!=null)duck*=session.worldProgression.NightAmbienceDuck;
            RefreshVoices(riverVoices,riverVolume*duck,1);
            RefreshVoices(birdVoices,night?0:birdVolume*duck,1);
        }
        void RefreshVoices(AudioSource[] sources,float volume,int maximum)
        {
            AudioSource nearest=null;float best=audioDistance*audioDistance;
            foreach(var source in sources)if(source!=null&&source.clip!=null){float d=(source.transform.position-observer).sqrMagnitude;if(d<best){best=d;nearest=source;}}
            foreach(var source in sources)
            {
                if(source==null)continue;bool play=source==nearest&&volume>.001f&&maximum>0;
                if(play)
                {
                    source.volume=volume;
                    if(!source.isPlaying){source.time=Mathf.Repeat((float)(ArchiveTopicSession.Clock%4096),Mathf.Max(.1f,source.clip.length-.05f));source.Play();}
                    ActiveAmbientVoiceCount++;
                }
                else if(source.isPlaying)source.Stop();
            }
        }
        void Release(Canopy canopy)
        {
            if(canopy.animated==null)return;
            if(canopy.filter!=null&&canopy.filter.sharedMesh==canopy.animated)canopy.filter.sharedMesh=canopy.original;
            Destroy(canopy.animated);canopy.animated=null;canopy.work=null;
        }
        void ReleaseAll(){foreach(var canopy in canopies)Release(canopy);nearby.Clear();AnimatedCanopyCount=0;}
        void HideMotes(){foreach(var mote in motes)if(mote!=null)mote.gameObject.SetActive(false);VisibleMoteCount=0;}
        void StopAudio(){foreach(var source in riverVoices)if(source!=null)source.Stop();foreach(var source in birdVoices)if(source!=null)source.Stop();ActiveAmbientVoiceCount=0;}
        void OnDisable(){ReleaseAll();HideMotes();StopAudio();nextScan=0;}
        void OnDestroy(){ReleaseAll();}
    }
}
