using System;
using System.Linq;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    [Serializable] public sealed class ArchiveWorldTimeState
    {
        public int phase, previousPhase, serial;
        public double changedAt, arrivalStartedAt=-1;
        public bool secondaryReleased;
        public int adminPhaseOverride=-1;
    }
    public sealed partial class ArchiveTopicSession
    {
        public ArchiveWorldProgression worldProgression;
        public bool NightEntranceActive=>Ready&&worldProgression!=null&&State.worldTime!=null&&State.worldTime.phase==3&&
            State.worldTime.arrivalStartedAt>=0&&Clock-State.worldTime.arrivalStartedAt<worldProgression.entranceSeconds&&
            !worldProgression.suppressThreatsForVerification&&!TeamWon&&!TeamDefeated&&!MultiplayerLobbyService.IsGameplayRecovering;
        public static int WorldPhaseForTopics(int completed) => completed>=7?3:completed>=5?2:completed>=3?1:0;
        void NormalizeWorldTime(){if(State.worldTime==null)State.worldTime=new ArchiveWorldTimeState();}
        bool AdvanceWorldTime()
        {
            if(!Ready||!IsAuthority||worldProgression==null)return false;
            NormalizeWorldTime();var world=State.worldTime;int phase=world.adminPhaseOverride>=0?Mathf.Clamp(world.adminPhaseOverride,0,3):WorldPhaseForTopics(State.stations.Count(s=>s.phase==2));bool changed=false;
            if(world.phase!=phase)
            {
                world.previousPhase=world.phase;world.phase=phase;world.changedAt=Clock;world.serial++;changed=true;
                if(phase==3&&world.arrivalStartedAt<0)world.arrivalStartedAt=Clock;
                if(phase<3){world.arrivalStartedAt=-1;world.secondaryReleased=false;}
            }
            if(phase==3&&!world.secondaryReleased&&world.arrivalStartedAt>=0&&Clock>=world.arrivalStartedAt+worldProgression.entranceSeconds+worldProgression.warningSeconds)
            {world.secondaryReleased=true;changed=true;}
            return changed;
        }
    }
}
