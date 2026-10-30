using System;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Cinematics;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public enum ArchiveAdminTestAction
    {
        VisitTopic,StartTopic,CompleteTopic,ResetTopic,CompleteAllTopics,ResetMatch,SpawnChest,GrantItem,GrantAllItems,ClearInventory,
        SetHearts,DamageHeart,ReviveAtNest,DownAtNest,SpawnTeammate,RemoveTeammate,TeleportToNest,AwakenMonster,SleepMonster,StartHunt,
        PreviewJumpscare,PreviewBarrier,ReleaseCapture,PreviewNight,ReplayEntrance,ReturnDaylight,OpenPortal,RecallTeammate,BarrierSuccess,BarrierFail,
        StartMatchClock,MatchClockOneMinute,ExpireMatchClock
    }
    public sealed partial class ArchiveTopicSession
    {
        public string AdminTestNotice {get;private set;}="Practice fixtures only · no academic credit or rewards outside this match.";
        public bool RequestAdminTest(ArchiveAdminTestAction action,int target=0,int value=0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!CanUseTopicAdmin)return false;
            bool ok=false;NormalizeSurvival();
            var monster=target==1&&worldProgression!=null?worldProgression.secondary:hunting;
            switch(action)
            {
                case ArchiveAdminTestAction.StartMatchClock:
                    if(TeamWon||TeamDefeated)break;
                    var clock=State.matchTime;
                    if(clock!=null&&(clock.phase==ArchiveMatchTimePhase.WaitingForPlayers||clock.phase==ArchiveMatchTimePhase.Countdown))
                    {clock.phase=ArchiveMatchTimePhase.Countdown;clock.countdownStartedAt=Clock-MatchCountdownSeconds;TickMatchTime();ok=true;}break;
                case ArchiveAdminTestAction.MatchClockOneMinute:case ArchiveAdminTestAction.ExpireMatchClock:
                    if(!MatchTimeAllowsGameplay)break;
                    double testDeadline=Clock+(action==ArchiveAdminTestAction.MatchClockOneMinute?60:0);
                    if(State.matchTime.phase==ArchiveMatchTimePhase.Escape)State.matchTime.escapeDeadline=testDeadline;
                    else State.matchTime.deadline=testDeadline;
                    TickMatchTime();ok=true;break;
                case ArchiveAdminTestAction.VisitTopic:
                    ok=target>=0&&target<10&&AdminMove(LocalActor,stations[target].transform.TransformPoint(ArchiveTopicStation.BookPosition+Vector3.back*2));break;
                case ArchiveAdminTestAction.StartTopic:
                    if(target>=0&&target<10&&CanStartTopic(target)&&AdminMove(LocalActor,stations[target].transform.TransformPoint(ArchiveTopicStation.BookPosition+Vector3.back)))
                    {Request(target,ArchiveAction.Start);ok=State.stations[target].phase==1;}break;
                case ArchiveAdminTestAction.CompleteTopic:case ArchiveAdminTestAction.ResetTopic:
                    if(target<0||target>=10)break;
                    var state=State.stations[target];
                    if(action==ArchiveAdminTestAction.CompleteTopic)
                    {state.phase=2;state.round=ArchiveTopicCatalog.RoundCount(target);state.timerRunning=false;state.channelActor=0;state.carries.Clear();ResetTopicCrystal(target,true);CloseTopicRelay(target);state.feedback="Admin completed: no academic credit.";}
                    else {State.stations[target]=new ArchiveStationState{topic=target,revision=state.revision+1,testResetSerial=state.testResetSerial+1};ResetTopicCrystal(target,false);ResetTopicRelay(target,false);State.survival.chests.RemoveAll(c=>c.topic==target);}
                    questions.Clear();ok=true;break;
                case ArchiveAdminTestAction.CompleteAllTopics:case ArchiveAdminTestAction.OpenPortal:
                    for(int i=0;i<10;i++){var t=State.stations[i];t.phase=2;t.round=ArchiveTopicCatalog.RoundCount(i);t.timerRunning=false;t.channelActor=0;t.carries.Clear();ResetTopicCrystal(i,true);CloseTopicRelay(i);}ok=true;break;
                case ArchiveAdminTestAction.ResetMatch:
                    foreach(var m in KhezuHuntingController.ActiveMonsters){m.GetComponent<KhezuCinematicJumpscareController>()?.Cancel();m.EndRescueCarry(m.RescueActor);m.HoldTranquilized(false);m.Request(KhezuCommand.Sleep);}
                    CleanupRescue();Initialize(Guid.NewGuid().GetHashCode());
                    foreach(int actor in AdminTestActors)AdminSetHearts(actor,hunting.health.MaximumHealth);ok=true;break;
                case ArchiveAdminTestAction.SpawnChest:
                    if(target<0||target>=10||value< -1||value>5)break;
                    var topic=State.stations[target];topic.phase=2;topic.round=ArchiveTopicCatalog.RoundCount(target);topic.timerRunning=false;topic.channelActor=0;topic.carries.Clear();ResetTopicCrystal(target,true);CloseTopicRelay(target);
                    State.survival.chests.RemoveAll(c=>c.topic==target);CreateTopicReward(topic,LocalActor);var chest=Chest(target);
                    if(value>=0)chest.item=(ArchiveSurvivalItem)value;chest.completedAt=Clock;AdminMove(LocalActor,stations[target].transform.TransformPoint(ArchiveRewardView.ChestLocalPosition+Vector3.forward*2));ok=true;break;
                case ArchiveAdminTestAction.GrantItem:
                    if(ActorPlayer(target)!=null&&value>=0&&value<6){GrantSurvivalItem(EnsureInventory(target),(ArchiveSurvivalItem)value);ok=true;}break;
                case ArchiveAdminTestAction.GrantAllItems:
                    if(ActorPlayer(target)!=null){var bag=EnsureInventory(target);foreach(ArchiveSurvivalItem item in Enum.GetValues(typeof(ArchiveSurvivalItem)))GrantSurvivalItem(bag,item);bag.items[(int)ArchiveSurvivalItem.Battery]=3;bag.items[(int)ArchiveSurvivalItem.Medkit]=3;bag.items[(int)ArchiveSurvivalItem.RevivalKit]=3;bag.items[(int)ArchiveSurvivalItem.TranquilizerAmmo]=6;ok=true;}break;
                case ArchiveAdminTestAction.ClearInventory:
                    if(ActorPlayer(target)!=null){var bag=EnsureInventory(target);Array.Clear(bag.items,0,6);bag.flashlightOn=false;bag.batterySeconds=0;ok=true;}break;
                case ArchiveAdminTestAction.SetHearts:ok=AdminSetHearts(target,value);break;
                case ArchiveAdminTestAction.DamageHeart:ok=hunting.health.DamageAuthoritativeActor(target);PublishSurvivalHealth();break;
                case ArchiveAdminTestAction.SpawnTeammate:ok=SpawnAdminTeammate();if(ok)hunting.SetPlayerEligibility(-2,true);break;
                case ArchiveAdminTestAction.RemoveTeammate:ok=DebugActorPlayer(-2)!=null;RemoveAdminTeammate();break;
                case ArchiveAdminTestAction.RecallTeammate:
                    ok=Player.LocalInstance!=null&&AdminMove(-2,Player.LocalInstance.transform.position+Player.LocalInstance.transform.right*5);break;
                case ArchiveAdminTestAction.TeleportToNest:ok=AdminMove(LocalActor,hunting.monsterSpawnPoint.position+hunting.monsterSpawnPoint.forward*12);break;
                case ArchiveAdminTestAction.DownAtNest:
                    if(ActorPlayer(target)==null)break;AdminSetHearts(target,0);var anchor=hunting.monsterSpawnPoint.position+hunting.monsterSpawnPoint.right*7;
                    State.survival.downed.RemoveAll(d=>d.actor==target);State.survival.downed.Add(new ArchiveDownedActor{actor=target,phase=2,serial=1,origin=anchor,anchor=anchor,createdAt=Clock,arrivedAt=Clock-2});ok=true;break;
                case ArchiveAdminTestAction.ReviveAtNest:
                    if(Downed(target)!=null){ok=AdminSetHearts(target,1);ReleaseRescueBody(target);State.survival.downed.RemoveAll(d=>d.actor==target);}break;
                case ArchiveAdminTestAction.PreviewNight:case ArchiveAdminTestAction.ReplayEntrance:
                    if(worldProgression==null)break;
                    State.worldTime.adminPhaseOverride=3;State.worldTime.previousPhase=State.worldTime.phase;State.worldTime.phase=3;State.worldTime.changedAt=Clock;State.worldTime.arrivalStartedAt=Clock;State.worldTime.secondaryReleased=false;State.worldTime.serial++;
                    foreach(var m in KhezuHuntingController.ActiveMonsters){m.GetComponent<KhezuCinematicJumpscareController>()?.Cancel();m.Request(KhezuCommand.Sleep);}ok=true;break;
                case ArchiveAdminTestAction.ReturnDaylight:
                    State.worldTime.adminPhaseOverride=0;foreach(var m in KhezuHuntingController.ActiveMonsters)m.Request(KhezuCommand.Sleep);ok=true;break;
                case ArchiveAdminTestAction.AwakenMonster:case ArchiveAdminTestAction.StartHunt:
                    if(monster!=null&&monster.gameObject.activeInHierarchy){monster.Request(KhezuCommand.StartHunting);ok=true;}break;
                case ArchiveAdminTestAction.SleepMonster:
                    if(monster!=null&&monster.gameObject.activeInHierarchy){monster.Request(KhezuCommand.Sleep);ok=true;}break;
                case ArchiveAdminTestAction.PreviewJumpscare:
                    if(monster!=null&&!Photon.Pun.PhotonNetwork.InRoom){monster.GetComponent<KhezuCinematicJumpscareController>().PreviewLocal();ok=true;}break;
                case ArchiveAdminTestAction.PreviewBarrier:
                    if(monster!=null&&monster.Initialized&&!NightEntranceActive&&CanAct(LocalActor)){monster.AdminPreviewCapture(LocalActor,true);ok=true;}break;
                case ArchiveAdminTestAction.ReleaseCapture:
                    if(monster!=null){monster.GetComponent<KhezuCinematicJumpscareController>()?.Cancel();monster.Request(KhezuCommand.ResetHunt);ok=true;}break;
                case ArchiveAdminTestAction.BarrierSuccess:case ArchiveAdminTestAction.BarrierFail:
                    if(monster!=null)ok=monster.ResolveBarrierOutcome(monster.CaughtPlayerId,monster.CaptureSerial,action==ArchiveAdminTestAction.BarrierSuccess,.9f);break;
            }
            AdminTestNotice=ok?"TEST: "+action+" applied. Practice fixture; no academic credit.":"Cannot apply "+action+" now. Check target, active capture/intro, hearts or topic lock. Test teammate is offline only.";
            if(ok)Commit();return ok;
#else
            return false;
#endif
        }
        bool AdminSetHearts(int actor,int value)
        {
            if(!AdminTestActors.Contains(actor)||ActorPlayer(actor)==null||hunting?.health==null)return false;
            var old=hunting.health.ExportActorHealth().FirstOrDefault(h=>h.actor==actor);
            hunting.health.ApplyActorHealthSnapshot(new[]{new HealthManager.ActorHealth{actor=actor,hearts=Mathf.Clamp(value,0,hunting.health.MaximumHealth),lastPenaltySerial=old?.lastPenaltySerial??0}});PublishSurvivalHealth();return true;
        }
        bool AdminMove(int actor,Vector3 target)
        {
            var player=ActorPlayer(actor);if(player==null||player.IsMovementLocked||NightEntranceActive)return false;
            var cc=player.GetComponent<CharacterController>();float footOffset=cc!=null?player.transform.position.y-Water.OpenArchiveWater.Feet(cc).y:0;
            if(Physics.Raycast(target+Vector3.up*15,Vector3.down,out var floor,50,~0,QueryTriggerInteraction.Ignore))target.y=floor.point.y+footOffset+.1f;
            var delta=target-player.transform.position;if(cc!=null)cc.enabled=false;player.transform.position=target;if(cc!=null)cc.enabled=true;
            player.ResetMotionAfterTeleport();if(actor==LocalActor)TeleportCameraSync.ResetAfterMove(player.transform,delta);return true;
        }
    }
}
