using System;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        public float TopicOvertimeSeconds(ArchiveStationState state)
        {
            if(state==null||!state.timerOvertime)return 0;
            return state.timerRunning?Mathf.Max(0,(float)(TimerClock-state.timerDeadline)):Mathf.Max(0,state.timerSavedOvertime);
        }

        void PauseTopicTimer(ArchiveStationState state)
        {
            if(!state.timerRunning)return;
            state.timerSavedRemaining=TimerRemaining(state);
            state.timerSavedOvertime=TopicOvertimeSeconds(state);
            state.timerRunning=false;
        }
    }
}
