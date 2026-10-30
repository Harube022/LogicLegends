using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Displays checkpointed academic work. Never awards progress or changes a challenge.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveCrystalPillar : MonoBehaviour
    {
        public ArchiveTopicStation station;
        public Transform[] shards, runes, pulseRings, motes;
        public Vector3[] restPositions;
        public Renderer[] shardRenderers, runeRenderers, pulseRenderers, moteRenderers;
        public Transform progressFill;
        public TMP_Text status;
        public AudioSource completionAudio;
        [Min(.1f)] public float restorationSeconds=1.15f;
        [Min(.1f)] public float pulseSpeed=1;
        [Range(0,5)] public float glowIntensity=1.6f;
        public static readonly string[] ColorNames={"Turquoise","Sapphire","Amethyst","Gold","Ruby","Indigo","Emerald","Rose","Amber","Ice"};
        public static readonly Color[] Palette={new Color(.04f,.85f,.76f),new Color(.12f,.45f,1),new Color(.70f,.22f,1),
            new Color(1,.83f,.14f),new Color(1,.12f,.25f),new Color(.36f,.30f,1),new Color(.12f,.92f,.32f),
            new Color(1,.28f,.69f),new Color(1,.43f,.08f),new Color(.55f,.90f,1)};
        Vector3[] assembled,broken;Quaternion[] assembledRotation,brokenRotation;
        MaterialPropertyBlock block;
        int shownRevision=-1;string context="";bool observedFull,observedOnce;float nextStatus;
        float cachedBody;int[] cachedContributors=Array.Empty<int>();
        static readonly int Base=Shader.PropertyToID("_BaseColor"),Emission=Shader.PropertyToID("_EmissionColor");
        public Color Tint => Palette[Mathf.Clamp(station==null?0:station.topic,0,9)];
        void Awake()
        {
            block=new MaterialPropertyBlock();
            if(station==null)station=GetComponentInParent<ArchiveTopicStation>();
            assembled=restPositions!=null&&restPositions.Length==shards.Length?restPositions:shards.Select(t=>t.localPosition).ToArray();assembledRotation=shards.Select(t=>Quaternion.identity).ToArray();
            broken=new Vector3[shards.Length];brokenRotation=new Quaternion[shards.Length];
            for(int i=0;i<shards.Length;i++)
            {
                broken[i]=BrokenPosition(i,station==null?0:station.topic);
                brokenRotation[i]=Quaternion.Euler(25+(i%3)*19,i*41,32-i*13);
                shards[i].localPosition=broken[i];shards[i].localRotation=brokenRotation[i];
            }
        }
        public static Vector3 BrokenPosition(int i,int topic)
        {float angle=i*2.39996f+topic*.6f;return new Vector3(Mathf.Cos(angle)*(1.05f+i*.065f),.55f+(i%3)*.16f,Mathf.Sin(angle)*(1.05f+i*.065f));}
        void Paint(Renderer renderer,Color color,float glow)
        {if(renderer==null)return;block.Clear();block.SetColor(Base,color);block.SetColor(Emission,color*glow);renderer.SetPropertyBlock(block);}
        void LateUpdate()
        {
            var session=station==null?null:station.session;var c=session?.Crystal(station.topic);
            if(c==null||!session.CrystalsEnabled)return;
            string key=session.State.matchId+":"+c.resetSerial;
            if(context!=key){context=key;observedOnce=false;shownRevision=-1;}
            if(observedOnce&&!observedFull&&c.fullyRestored&&completionAudio!=null&&Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,transform.position)<35)completionAudio.Play();
            observedOnce=true;observedFull=c.fullyRestored;
            if(shownRevision!=session.State.revision&&Time.unscaledTime>=nextStatus)
            {
                nextStatus=Time.unscaledTime+.2f;shownRevision=session.State.revision;
                cachedBody=ArchiveCrystalProgress.Body(session.State,station.topic);cachedContributors=ArchiveCrystalProgress.Contributors(session.State,station.topic);
                int percent=ArchiveCrystalProgress.Percent(session.State,c),required=ArchiveCrystalProgress.Required(session.State,c).Length;
                if(status!=null)status.text="T"+(station.topic+1)+" - "+ColorNames[station.topic]+" Crystal\n"+percent+"% RESTORED\n"+
                    "Solved "+ArchiveCrystalProgress.Solved(session.State,station.topic)+" / "+ArchiveCrystalProgress.Total(station.topic)+
                    (c.online?" - Contributors "+ArchiveCrystalProgress.ContributorCount(session.State,c)+" / "+Mathf.Max(2,required):" - Solo practice");
                if(progressFill!=null){float width=3.2f*percent/100f;progressFill.localScale=new Vector3(Mathf.Max(.001f,width),.075f,.06f);progressFill.localPosition=new Vector3(-1.6f+width*.5f,4.34f,0);Paint(progressFill.GetComponent<Renderer>(),Tint,1);}
            }
            float body=cachedBody;
            float blend=1-Mathf.Exp(-Time.unscaledDeltaTime*4/Mathf.Max(.1f,restorationSeconds));
            float wave=.5f+.5f*Mathf.Sin((float)ArchiveTopicSession.Clock*pulseSpeed*2.4f);
            bool near=Player.LocalInstance!=null&&Vector3.SqrMagnitude(Player.LocalInstance.transform.position-transform.position)<4900;
            if(status!=null&&status.gameObject.activeSelf!=near)status.gameObject.SetActive(near);
            for(int i=0;i<shards.Length;i++)
            {
                float part=Mathf.Clamp01(body*shards.Length-i);
                var target=Vector3.Lerp(broken[i],assembled[i],part);
                if(c.fullyRestored)target.y+=Mathf.Sin((float)ArchiveTopicSession.Clock*pulseSpeed*2.4f)*.015f;
                shards[i].localPosition=Vector3.Lerp(shards[i].localPosition,target,blend);
                shards[i].localRotation=Quaternion.Slerp(shards[i].localRotation,Quaternion.Slerp(brokenRotation[i],assembledRotation[i],part),blend);
                Paint(shardRenderers[i],Color.Lerp(Tint*.35f,Tint,part*.85f),.04f+part*(c.fullyRestored?glowIntensity*(.65f+wave):.32f));
            }
            for(int i=0;i<runes.Length;i++)
            {
                bool occupied=i<c.members.Count;
                bool lit=occupied&&cachedContributors.Contains(c.members[i].actor);
                bool needed=!c.started||occupied&&(!c.members[i].excused||lit);
                if(runes[i].gameObject.activeSelf!=needed)runes[i].gameObject.SetActive(needed);
                Paint(runeRenderers[i],lit?Tint:new Color(.12f,.18f,.20f),lit?glowIntensity*(c.fullyRestored?1+wave*.8f:.65f):0);
            }
            for(int i=0;i<pulseRings.Length;i++)
            {
                bool visible=c.fullyRestored&&near;pulseRings[i].gameObject.SetActive(visible);if(!visible)continue;
                float phase=Mathf.Repeat((float)(ArchiveTopicSession.Clock-c.restoredAt)*pulseSpeed*.35f+i*.34f,1);
                float scale=Mathf.Lerp(.7f,2.6f,phase);pulseRings[i].localScale=new Vector3(scale,.02f,scale);
                pulseRings[i].localPosition=new Vector3(0,.40f+i*.025f,0);
                Paint(pulseRenderers[i],Color.Lerp(Tint*.18f,Tint,1-phase),glowIntensity*(1-phase)*2);
            }
            for(int i=0;i<motes.Length;i++)
            {
                bool visible=c.fullyRestored&&near;motes[i].gameObject.SetActive(visible);if(!visible)continue;
                float time=(float)ArchiveTopicSession.Clock*pulseSpeed*.55f+i*.7854f;
                motes[i].localPosition=new Vector3(Mathf.Cos(time)*1.15f,.8f+Mathf.Repeat(time*.5f+i*.23f,3.5f),Mathf.Sin(time)*1.15f);
                motes[i].Rotate(0,Time.unscaledDeltaTime*40,0);Paint(moteRenderers[i],Tint,glowIntensity*2);
            }
            if(status!=null&&near&&Camera.main!=null)status.transform.rotation=Camera.main.transform.rotation;
        }
    }
}
