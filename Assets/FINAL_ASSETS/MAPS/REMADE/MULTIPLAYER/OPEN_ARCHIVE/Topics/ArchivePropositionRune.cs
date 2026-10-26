using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics
{
    [DefaultExecutionOrder(11000)]
    public sealed class ArchivePropositionRune : MonoBehaviour
    {
        public Transform labelRoot;
        public TMP_Text letter, symbol, answer, condition;
        public Renderer inlay;
        public LineRenderer channelArc;
        ArchiveTopicStation station;
        ArchivePropositionPresentation art;
        ArchiveTopicPoint point;
        int index,round;
        MaterialPropertyBlock properties;
        Color shown=new Color(-1,-1,-1);
        int arcSteps=-1;
        ParticleSystem sparks;
        LineRenderer ripple;
        MaterialPropertyBlock rippleProperties;
        readonly Vector3[] ripplePoints=new Vector3[49];
        bool wasChannel;
        int shownReceipt=-1;
        double rippleStarted=-100;
        public void Configure(ArchiveTopicStation owner,ArchivePropositionPresentation palette,ArchiveStationState state,ArchiveQuestion q,int choice,ArchiveTopicPoint interaction)
        {
            station=owner;art=palette;point=interaction;index=choice;round=state.round;
            letter.text=((char)('A'+index)).ToString();
            symbol.text=state.phase==1?ArchivePropositionNotation.Symbol(q.items[index]):state.phase==2?"+":"·";
            answer.text=state.phase==1?q.items[index]:state.phase==2?"Team complete":state.phase==ArchiveTopicSession.T1RelayPhase?"Solve the team relay":"Awaiting the Book";
            properties=new MaterialPropertyBlock();
            var bounds=labelRoot.gameObject.AddComponent<ArchiveWorldLabel>();bounds.size=new Vector2(3.9f,1.55f);bounds.point=point;
            channelArc.shadowCastingMode=ShadowCastingMode.Off;channelArc.receiveShadows=false;
            if(art.sparkMesh!=null&&art.sparkMaterial!=null)BuildEffects();
        }
        void LateUpdate()
        {
            if(station==null||station.session==null||!station.session.Ready)return;
            var session=station.session;var state=session.State.stations[0];
            if(Camera.main!=null)
            {
                var forward=labelRoot.position-Camera.main.transform.position;
                if(forward.sqrMagnitude>.01f)labelRoot.rotation=Quaternion.LookRotation(forward,Vector3.up);
            }
            bool active=state.phase==1&&round==state.round;
            bool channel=active&&state.channelActor!=0&&state.channelChoice==index;
            bool focused=active&&point!=null&&ArchiveTopicUI.Current!=null&&ArchiveTopicUI.Current.FocusedPoint==point;
            float fill=channel?Mathf.Clamp01((float)((ArchiveTopicSession.Clock-state.channelStarted)/session.answerChannelSeconds)):0;
            bool result=state.lastAnswerChoice==index&&state.lastAnswerRound==(active?round:round-1)&&ArchiveTopicSession.Clock-state.lastAnswerAt<2.2;
            Color color=channel?art.focus:result?(state.lastAnswerResult==1?art.correct:art.wrong):focused?art.focus:active?art.idle:Color.Lerp(art.idle,Color.gray,.55f);
            if(color!=shown)
            {
                shown=color;properties.SetColor("_BaseColor",color);inlay.SetPropertyBlock(properties);channelArc.SetPropertyBlock(properties);symbol.color=color;
            }
            condition.text=channel?"CHANNELING "+Mathf.RoundToInt(fill*100)+"%":result?(state.lastAnswerResult==1?"CORRECT":"TRY AGAIN"):focused?"TAP HAND / E":active?"STAND IN RING":"";
            int steps=channel?Mathf.Max(2,Mathf.CeilToInt(fill*48)):0;
            if(steps!=arcSteps)
            {
                arcSteps=steps;channelArc.enabled=steps>0;channelArc.positionCount=steps;
                for(int i=0;i<steps;i++)
                {
                    float a=-Mathf.PI*.5f+i/(float)(steps-1)*Mathf.PI*2*fill;
                    channelArc.SetPosition(i,new Vector3(Mathf.Cos(a)*1.04f,.13f,Mathf.Sin(a)*1.04f));
                }
            }
            UpdateEffects(state,channel);
        }
        void BuildEffects()
        {
            sparks=new GameObject("Rune_Channel_Sparks").AddComponent<ParticleSystem>();sparks.transform.SetParent(transform,false);sparks.transform.localPosition=Vector3.up*.2f;
            sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=sparks.main;main.playOnAwake=false;main.loop=true;main.duration=1;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.startLifetime=.65f;main.startSpeed=.05f;main.startSize=.065f;main.maxParticles=24;
            var emission=sparks.emission;emission.enabled=false;emission.rateOverTime=art.channelSparkRate;
            var shape=sparks.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=.85f;shape.rotation=new Vector3(90,0,0);
            var velocity=sparks.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;velocity.y=.75f;
            var renderer=sparks.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=art.sparkMesh;renderer.sharedMaterial=art.sparkMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            ripple=new GameObject("Rune_Energy_Ripple").AddComponent<LineRenderer>();ripple.transform.SetParent(transform,false);ripple.useWorldSpace=false;
            ripple.sharedMaterial=channelArc.sharedMaterial;ripple.shadowCastingMode=ShadowCastingMode.Off;ripple.receiveShadows=false;ripple.positionCount=ripplePoints.Length;ripple.enabled=false;
            rippleProperties=new MaterialPropertyBlock();
        }
        void UpdateEffects(ArchiveStationState state,bool channel)
        {
            if(sparks==null)return;
            bool nearby=Time.timeScale>0&&Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,transform.position)<45;
            var emission=sparks.emission;emission.enabled=nearby&&channel;
            if(nearby&&channel&&!wasChannel)
            {
                var main=sparks.main;main.startColor=art.focus;sparks.Play();StartRipple(art.focus);
            }
            wasChannel=nearby&&channel;
            bool receipt=state.lastAnswerChoice==index&&state.lastAnswerRound==(state.phase==1?round:round-1)&&ArchiveTopicSession.Clock-state.lastAnswerAt<1.2;
            if(nearby&&receipt&&shownReceipt!=state.answerSerial)
            {
                shownReceipt=state.answerSerial;var color=state.lastAnswerResult==1?art.correct:art.wrong;
                var main=sparks.main;main.startColor=color;sparks.Play();sparks.Emit(state.lastAnswerResult==1?12:6);StartRipple(color);
            }
            if(!channel&&sparks.particleCount==0&&sparks.isPlaying)sparks.Stop(false,ParticleSystemStopBehavior.StopEmitting);
            float age=(float)(ArchiveTopicSession.Clock-rippleStarted),t=Mathf.Clamp01(age/.8f);
            ripple.enabled=nearby&&age>=0&&age<.8f;
            if(!ripple.enabled)return;
            ripple.startWidth=ripple.endWidth=.055f*(1-t);
            for(int i=0;i<ripplePoints.Length;i++)
            {
                float a=i/(float)(ripplePoints.Length-1)*Mathf.PI*2,radius=1.04f+t*.42f;
                ripplePoints[i]=new Vector3(Mathf.Cos(a)*radius,.15f,Mathf.Sin(a)*radius);
            }
            ripple.SetPositions(ripplePoints);
        }
        void StartRipple(Color color)
        {
            rippleStarted=ArchiveTopicSession.Clock;rippleProperties.SetColor("_BaseColor",color);ripple.SetPropertyBlock(rippleProperties);
        }
    }
}
