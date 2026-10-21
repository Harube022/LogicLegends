using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>T1 art and local feedback only. ArchiveTopicSession remains the sole state owner.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchivePropositionPresentation : MonoBehaviour
    {
        public GameObject runePrefab;
        public AudioClip correctSound;
        public AudioClip channelSound,incorrectSound;
        public Mesh sparkMesh;
        public Material sparkMaterial;
        [Range(0,24)] public int channelSparkRate=10;
        public Renderer[] progressMarks;
        [Range(0,1)] public float feedbackVolume=.28f;
        public Color idle=new Color(.16f,.57f,.54f), focus=new Color(1,.78f,.37f), correct=new Color(.36f,.83f,.55f), wrong=new Color(.94f,.30f,.23f);
        ArchiveTopicStation station;
        AudioSource audioSource;
        int observedAnswer=-1;
        int shownRound=-1;
        string observedChannel="";
        MaterialPropertyBlock markProperties;
        void Awake()
        {
            station=GetComponent<ArchiveTopicStation>();
            markProperties=new MaterialPropertyBlock();
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=1;
            audioSource.rolloffMode=AudioRolloffMode.Linear;audioSource.minDistance=3;audioSource.maxDistance=35;
        }
        void Update()
        {
            if(station.session==null||!station.session.Ready){audioSource.Stop();return;}
            var state=station.session.State.stations[0];
            bool audible=Time.timeScale>0&&Player.LocalInstance!=null&&station.session.CanAct(station.session.LocalActor)&&Vector3.Distance(Player.LocalInstance.transform.position,transform.position)<35;
            if(!audible)audioSource.Stop();
            if(shownRound!=state.round)
            {
                shownRound=state.round;
                for(int i=0;i<progressMarks.Length;i++)
                {
                    markProperties.SetColor("_BaseColor",i<shownRound?correct:idle*.55f);progressMarks[i].SetPropertyBlock(markProperties);
                }
            }
            string channel=state.channelActor==0?"":state.channelActor+":"+state.channelSequence+":"+state.round;
            if(observedAnswer<0){observedAnswer=state.answerSerial;observedChannel=channel;return;}
            if(channel!=observedChannel)
            {
                observedChannel=channel;
                if(audible&&channel!=""&&channelSound!=null)audioSource.PlayOneShot(channelSound,feedbackVolume*.55f);
            }
            if(state.answerSerial==observedAnswer)return;
            bool fresh=state.answerSerial>observedAnswer&&ArchiveTopicSession.Clock-state.lastAnswerAt<2;
            observedAnswer=state.answerSerial;
            if(fresh&&audible)
            {
                var clip=state.lastAnswerResult==1?correctSound:incorrectSound;
                if(clip!=null)audioSource.PlayOneShot(clip,feedbackVolume*(state.lastAnswerResult==1?1:.45f));
            }
        }
        public void BuildRunes(ArchiveTopicStation owner,ArchiveStationState state,ArchiveQuestion q)
        {
            for(int i=0;i<4;i++)
            {
                var obj=Instantiate(runePrefab,owner.workRoot);obj.name="Answer_Rune_"+(char)('A'+i);
                obj.transform.localPosition=owner.ItemPosition(i,q);
                ArchiveTopicPoint point=null;
                if(state.phase==1)
                {
                    point=obj.AddComponent<ArchiveTopicPoint>();point.station=owner;point.role=ArchivePointRole.Choice;point.index=i;
                    point.caption="Channel rune "+(char)('A'+i)+": "+q.items[i];owner.points.Add(point);
                }
                obj.GetComponent<ArchivePropositionRune>().Configure(owner,this,state,q,i,point);
            }
        }
    }
}
