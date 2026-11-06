using System;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        public ArchiveFieldEvents fieldEvents;
        float nextFieldTick;
        bool fieldCancellationPending;
        public void CancelFieldDiversion(KhezuHuntingController monster)
        {
            if(!Ready||!IsAuthority||State.fieldEvents==null||State.fieldEvents.activeBeacon<0||FieldGuardian(State.fieldEvents.beaconMonster)!=monster)return;
            State.fieldEvents.activeBeacon=-1;fieldCancellationPending=true;
        }
        void NormalizeFieldEvents()
        {
            if(State==null)return;
            if(State.fieldEvents==null)State.fieldEvents=new ArchiveFieldEventState();
            var state=State.fieldEvents;
            if(state.beacons==null)state.beacons=new System.Collections.Generic.List<ArchiveBeaconState>();
            if(fieldEvents==null)fieldEvents=GetComponent<ArchiveFieldEvents>();
            if(fieldEvents==null)return;
            foreach(var view in fieldEvents.beacons)
                if(view!=null&&!state.beacons.Any(b=>b!=null&&b.id==view.id))state.beacons.Add(new ArchiveBeaconState{id=view.id});
            state.beacons.RemoveAll(b=>b==null);
        }
        KhezuHuntingController FieldGuardian(int index)=>index==0?hunting:index==1?worldProgression?.secondary:null;
        public bool CanUseBeacon(int actor,int id,out string reason)
        {
            reason="Unavailable";
            if(!Ready||fieldEvents==null||!fieldEvents.isActiveAndEnabled||!CanAct(actor)||NightEntranceActive||Time.timeScale<=0)return false;
            var view=fieldEvents.Beacon(id);var player=ActorPlayer(actor);var state=State.fieldEvents;
            var receipt=state?.beacons?.FirstOrDefault(b=>b.id==id);
            if(view==null||player==null||receipt==null){reason="Unknown beacon";return false;}
            var feet=ArchiveFieldEvents.Feet(player);
            if(Mathf.Abs(feet.y-view.transform.position.y)>2.5f||ArchiveFieldEvents.Horizontal(feet,view.transform.position)>fieldEvents.interactionRange)
            {reason="Move closer";return false;}
            if(Physics.Linecast(feet+Vector3.up*1.2f,view.transform.position+Vector3.up*1.2f,~(1<<7),QueryTriggerInteraction.Ignore))
            {reason="Approach the clear side of the beacon";return false;}
            double remaining=Math.Max(receipt.cooldownUntil,state.globalBeaconCooldownUntil)-Clock;
            if(state.activeBeacon>=0&&Clock<state.beaconEndsAt){reason="Another beacon is resonating";return false;}
            if(remaining>0){reason="Recharging "+Mathf.CeilToInt((float)remaining)+"s";return false;}
            bool guardian=Enumerable.Range(0,2).Select(FieldGuardian).Any(m=>m!=null&&m.CanDivertToBeacon(view.transform.position,true,fieldEvents.guardianHearingRange));
            if(!guardian){reason="No reachable hunting guardian nearby";return false;}
            reason="";return true;
        }
        public bool RequestBeacon(int id)
        {
            if(!CanUseBeacon(LocalActor,id,out string reason)){ShowLocalNotice(reason,3);return false;}
            var record=State.actors.FirstOrDefault(a=>a.actor==LocalActor);
            localSequence=Math.Max(localSequence,record?.lastSequence??0);
            var command=new ArchiveCommand{topic=Math.Max(0,ActiveTopic),action=ArchiveAction.ActivateBeacon,a=id,
                b=State.fieldEvents.beacons.First(b=>b.id==id).serial,sequence=++localSequence};
            StampCommandContext(command);return DispatchCommand(command);
        }
        bool AcceptBeacon(int actor,ArchiveCommand command)
        {
            if(command.contextVersion!=1||command.matchId!=State.matchId||command.sequence<=0||!CanUseBeacon(actor,command.a,out commandRejection))return false;
            var record=State.actors.FirstOrDefault(a=>a.actor==actor);
            var receipt=State.fieldEvents.beacons.First(b=>b.id==command.a);
            if(command.sequence<=(record?.lastSequence??0)||command.b!=receipt.serial)return false;
            var view=fieldEvents.Beacon(command.a);
            int guardian=Enumerable.Range(0,2).Where(i=>FieldGuardian(i)!=null&&FieldGuardian(i).CanDivertToBeacon(view.transform.position,true,fieldEvents.guardianHearingRange))
                .OrderBy(i=>(FieldGuardian(i).transform.position-view.transform.position).sqrMagnitude).FirstOrDefault();
            var state=State.fieldEvents;
            receipt.serial++;receipt.cooldownUntil=Clock+ArchiveFieldRules.BeaconCooldownSeconds;
            state.activeBeacon=command.a;state.beaconActor=actor;state.beaconMonster=guardian;
            state.beaconHuntSerial=FieldGuardian(guardian).HuntSerial;state.beaconTarget=FieldGuardian(guardian).TargetPlayerId;
            state.beaconStartedAt=Clock;state.beaconEndsAt=Clock+ArchiveFieldRules.BeaconSeconds;
            state.globalBeaconCooldownUntil=receipt.cooldownUntil;
            if(record==null){record=new ArchiveActor{actor=actor,displayName=ActorName(actor)};State.actors.Add(record);}
            record.lastSequence=command.sequence;
            Commit();return true;
        }
        bool TickFieldEvents()
        {
            if(!Ready||!IsAuthority||fieldEvents==null||Time.unscaledTime<nextFieldTick)return false;
            nextFieldTick=Time.unscaledTime+.1f;NormalizeFieldEvents();
            var state=State.fieldEvents;bool changed=fieldCancellationPending;fieldCancellationPending=false;
            bool allowed=fieldEvents.isActiveAndEnabled&&MatchTimeAllowsGameplay&&!TeamWon&&!TeamDefeated&&!NightEntranceActive;
            if(state.activeBeacon>=0)
            {
                var monster=FieldGuardian(state.beaconMonster);var view=fieldEvents.Beacon(state.activeBeacon);
                bool valid=allowed&&Clock<state.beaconEndsAt&&view!=null&&monster!=null&&
                    monster.HuntSerial==state.beaconHuntSerial&&monster.TargetPlayerId==state.beaconTarget&&
                    monster.CanDivertToBeacon(view.transform.position,false,fieldEvents.guardianHearingRange+10);
                if(!valid){monster?.CancelBeaconDiversion();state.activeBeacon=-1;changed=true;}
            }
            if(!allowed||PortalOpened)
            {
                if(state.site>=0){state.site=-1;changed=true;}
                return changed;
            }
            if(state.site>=0&&(fieldEvents.Site(state.site)==null||Clock>=state.endsAt+ArchiveFieldRules.DissolveSeconds))
            {state.site=-1;changed=true;}
            if(!fieldEvents.disturbancesEnabled||state.site>=0||Clock<state.nextAllowedAt)return changed;
            int milestone=State.stations.Count(s=>s.phase==2)/2;
            if(milestone<=state.triggeredMilestone||milestone>=5)return changed;
            var players=GetTeamRoster().Where(p=>p.hasPosition&&!p.inactive&&CanAct(p.actor)).Select(p=>p.position).ToArray();
            if(players.Length==0)return changed;
            var candidates=fieldEvents.hazards.Where(v=>v!=null).Select(v=>new{view=v,distance=players.Min(p=>ArchiveFieldEvents.Horizontal(p,v.transform.position))})
                .Where(v=>v.distance>=12).OrderBy(v=>v.distance).ThenBy(v=>v.view.id).ToArray();
            if(candidates.Length==0)return changed;
            var selected=candidates[(state.serial%Math.Min(3,candidates.Length))].view;
            state.serial++;state.site=selected.id;state.kind=(state.serial-1)%3;state.triggeredMilestone=milestone;
            state.warnedAt=Clock;state.activeAt=Clock+ArchiveFieldRules.WarningSeconds;state.endsAt=state.activeAt+ArchiveFieldRules.ActiveSeconds;
            state.nextAllowedAt=Clock+fieldEvents.disturbanceCooldown;return true;
        }
    }
}
