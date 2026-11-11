using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local cues derived from the checkpointed topic clock. Owns no countdown or penalty.</summary>
    [DefaultExecutionOrder(305),DisallowMultipleComponent]
    public sealed class ArchiveTopicTimerPresentation : MonoBehaviour
    {
        ArchiveTopicUI owner;
        AudioSource voice;
        AudioClip notification;
        readonly string[] keys=new string[10];
        readonly float[] previous=new float[10];
        readonly int[] masks=new int[10];
        float nextRead;
        public int PlayedWarnings {get;private set;}
        public int LastWarningSeconds {get;private set;}=-1;
        public int WarningMask(int topic)=>topic>=0&&topic<10?masks[topic]:0;
        [Range(0,1)] public float warningVolume=.18f;

        public void Initialize(ArchiveTopicUI ui)
        {
            owner=ui;notification=Resources.Load<AudioClip>("Audio/Notification");
            var go=new GameObject("Topic_Timer_Warning_Audio");go.transform.SetParent(transform,false);
            voice=go.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;voice.priority=80;
            AudioVolumeSettings.Route(voice,GameAudioChannel.SoundFX);
        }

        public static string Format(ArchiveTopicSession session,ArchiveStationState state)
        {
            int seconds=state.timerOvertime?Mathf.FloorToInt(session.TopicOvertimeSeconds(state)):Mathf.CeilToInt(session.TimerRemaining(state));
            return (state.timerOvertime?"+":"")+(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
        }
        public static Color Tint(ArchiveTopicSession session,ArchiveStationState state,bool darkBackground=false)
        {
            float remaining=session.TimerRemaining(state);
            if(state.timerOvertime)return darkBackground?new Color(1,.80f,.39f):new Color(.70f,.32f,.035f);
            if(state.timerRunning&&remaining<=30)return darkBackground?new Color(1,.61f,.48f):new Color(.75f,.13f,.10f);
            if(state.timerRunning&&remaining<=60)return darkBackground?new Color(1,.85f,.49f):new Color(.70f,.36f,.04f);
            return darkBackground?new Color(.59f,.91f,.82f):new Color(.08f,.36f,.35f);
        }
        public static bool HintCue(ArchiveTopicSession session)
        {
            int topic=session==null?-1:session.ActiveTopic;
            return topic>=0&&session.State.stations[topic].phase==1&&session.State.stations[topic].timerOvertime&&session.CanRequestHint(topic);
        }

        void Update()
        {
            if(Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+.1f;
            var session=owner==null?null:owner.session;
            if(session==null||!session.Ready||session.TeamWon||session.TeamDefeated)return;
            foreach(var state in session.State.stations)
            {
                int i=state.topic;if(i<0||i>=10)continue;
                string key=session.State.matchId+":"+session.State.seed+":"+state.testResetSerial+":"+(i==6?state.round/ArchiveTopicCatalog.SetProblemsPerChallenge:0);
                float remaining=session.TimerRemaining(state);
                // On join/recovery, already passed thresholds are acknowledged silently.
                if(keys[i]!=key)
                {
                    keys[i]=key;previous[i]=state.timerAttempt==0?float.PositiveInfinity:remaining;
                    masks[i]=state.timerAttempt==0?0:state.timerOvertime?7:remaining<=30?3:remaining<=60?1:0;
                }
                if(!state.timerRunning||state.phase==2)continue;
                if(state.timerOvertime)
                {
                    if((masks[i]&4)==0){masks[i]=7;Cue(session,0);}
                }
                else if(remaining<=30&&previous[i]>30&&(masks[i]&2)==0){masks[i]|=3;Cue(session,30);}
                else if(remaining<=60&&previous[i]>60&&(masks[i]&1)==0){masks[i]|=1;Cue(session,60);}
                previous[i]=remaining;
            }
        }
        void Cue(ArchiveTopicSession session,int seconds)
        {
            LastWarningSeconds=seconds;
            // Never compete with a capture question or a full-screen entrance film.
            if(voice==null||notification==null||Time.timeScale<=0||session.IsLocalBarrierQuestion||session.NightEntranceActive||
                ArchiveMatchCinematic.Covers(session))return;
            voice.pitch=seconds==30?1.08f:seconds==0?.84f:.94f;
            voice.PlayOneShot(notification,warningVolume);PlayedWarnings++;
        }
    }
}
