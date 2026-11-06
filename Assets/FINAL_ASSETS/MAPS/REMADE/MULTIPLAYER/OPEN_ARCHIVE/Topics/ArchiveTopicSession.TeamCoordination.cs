using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public enum ArchiveTeamPingKind { HelpHere, NeedMedkit, ReviveTeammate, GoToPortal }
    public enum ArchiveTeamObjectiveKind { Topic, Rescue, Portal, Ping }
    [Serializable] public sealed class ArchiveTeamPing
    {
        public int actor, sequence, topic=-1, targetActor;
        public ArchiveTeamPingKind kind;
        public Vector3 position;
        public double createdAt;
    }
    public sealed class ArchiveTeammateInfo
    {
        public int actor, hearts, maxHearts, topic=-1;
        public string name, status;
        public bool local, inactive, healthKnown, hasPosition;
        public Vector3 position;
    }
    public sealed class ArchiveTeamObjective
    {
        public string label;
        public Vector3 position;
        public int topic=-1, actor;
        public ArchiveTeamObjectiveKind kind;
    }

    public sealed partial class ArchiveTopicSession
    {
        public const float TeamPingLifetimeSeconds=12, TeamPingCooldownSeconds=4;
        public ArchiveTeamPing[] ActiveTeamPings=>!Ready||State.teamPings==null?Array.Empty<ArchiveTeamPing>():
            State.teamPings.Where(p=>p!=null&&Clock>=p.createdAt&&Clock-p.createdAt<TeamPingLifetimeSeconds&&TeamActorPresent(p.actor)&&!HasEscapedActor(p.actor))
                .OrderByDescending(p=>p.createdAt).Take(4).ToArray();

        bool TeamActorPresent(int actor)
        {
            if(!Ready||actor==0)return false;
            if(!PhotonNetwork.InRoom)return DebugPracticeActors.Contains(actor)&&ActorPlayer(actor)!=null;
            var member=PhotonNetwork.CurrentRoom.GetPlayer(actor);
            return member!=null&&!member.IsInactive;
        }
        bool CanCommunicate(int actor)=>Ready&&MatchTimeAllowsGameplay&&TeamActorPresent(actor)&&!HasEscapedActor(actor)&&ActorPlayer(actor)!=null;
        public float TeamPingCooldownRemaining(int actor)
        {
            var previous=State?.teamPings?.FirstOrDefault(p=>p!=null&&p.actor==actor);
            return previous==null?0:Mathf.Max(0,(float)(previous.createdAt+TeamPingCooldownSeconds-Clock));
        }
        public bool CanTeamPing(ArchiveTeamPingKind kind,out string reason)=>BuildTeamPing(LocalActor,kind,0,out _,out reason);
        public bool RequestTeamPing(ArchiveTeamPingKind kind,int targetActor=0)
        {
            if(!BuildTeamPing(LocalActor,kind,targetActor,out _,out string reason)){ShowLocalNotice(reason);return false;}
            var record=State.actors.FirstOrDefault(a=>a.actor==LocalActor);
            localSequence=Math.Max(localSequence,record?.lastSequence??0);
            var command=new ArchiveCommand{topic=Mathf.Max(0,ActiveTopic),action=ArchiveAction.TeamPing,a=(int)kind,b=targetActor,sequence=++localSequence};
            StampCommandContext(command);
            return DispatchCommand(command);
        }
        bool AcceptTeamPing(int actor,ArchiveCommand command)
        {
            if(command.contextVersion!=1||command.matchId!=State.matchId||command.sequence<=0||
                !Enum.IsDefined(typeof(ArchiveTeamPingKind),command.a))return false;
            var record=State.actors.FirstOrDefault(a=>a.actor==actor);
            if(command.sequence<=(record?.lastSequence??0))return false;
            if(!BuildTeamPing(actor,(ArchiveTeamPingKind)command.a,command.b,out var ping,out commandRejection))return false;
            if(record==null){record=new ArchiveActor{actor=actor,displayName=ActorName(actor)};State.actors.Add(record);}
            record.lastSequence=command.sequence;
            if(State.teamPings==null)State.teamPings=new List<ArchiveTeamPing>();
            State.teamPings.RemoveAll(p=>p==null||p.actor==actor||!TeamActorPresent(p.actor));
            ping.sequence=command.sequence;State.teamPings.Add(ping);
            // One short-lived receipt per party member, with a hard bound for reconnects.
            while(State.teamPings.Count>4)State.teamPings.RemoveAt(0);
            Commit();return true;
        }
        bool BuildTeamPing(int actor,ArchiveTeamPingKind kind,int requestedTarget,out ArchiveTeamPing ping,out string reason)
        {
            ping=null;reason="";
            if(!Enum.IsDefined(typeof(ArchiveTeamPingKind),kind)){reason="Choose a team signal.";return false;}
            if(!CanCommunicate(actor)){reason="Team signals are available during an active match.";return false;}
            float wait=TeamPingCooldownRemaining(actor);
            if(wait>0){reason="Wait "+Mathf.CeilToInt(wait)+"s before sending another team signal.";return false;}
            var player=ActorPlayer(actor);
            ping=new ArchiveTeamPing{actor=actor,kind=kind,createdAt=Clock,position=TeamFeet(player)};
            if(kind==ArchiveTeamPingKind.HelpHere)
            {
                // Location and topic are selected from the authenticated actor, never client coordinates.
                float nearest=float.MaxValue;
                for(int i=0;i<stations.Length;i++)if(stations[i]!=null&&Near(actor,i,Vector3.zero,14))
                {float distance=(stations[i].transform.position-player.transform.position).sqrMagnitude;if(distance<nearest){nearest=distance;ping.topic=i;}}
            }
            else if(kind==ArchiveTeamPingKind.NeedMedkit)
            {
                var health=hunting?.health;
                bool known=health!=null&&health.HasAuthoritativeActorHealth&&health.ExportActorHealth().Any(h=>h.actor==actor);
                if(!known||health.ActorHearts(actor)<=0||health.ActorHearts(actor)>=health.MaximumHealth)
                {reason="Request a medkit when you have a missing heart. A downed player needs a Revival Kit.";return false;}
                ping.targetActor=actor;
            }
            else if(kind==ArchiveTeamPingKind.ReviveTeammate)
            {
                var fallen=State.survival?.downed?.Where(d=>d!=null&&TeamActorPresent(d.actor)&&IsRevivalTargetAvailable(d.actor)&&
                    (requestedTarget==0||d.actor==requestedTarget)).OrderBy(d=>(d.anchor-player.transform.position).sqrMagnitude).FirstOrDefault();
                if(fallen==null){reason="No connected teammate is waiting for revival at the nest.";return false;}
                ping.targetActor=fallen.actor;ping.position=fallen.anchor;
            }
            else
            {
                if(!PortalOpened||exitPortal==null){reason="The exit portal opens after all ten topics are complete.";return false;}
                ping.position=exitPortal.transform.position;
            }
            return true;
        }
        static Vector3 TeamFeet(Player player)
        {
            var capsule=player.GetComponent<CharacterController>();
            return capsule!=null?new Vector3(capsule.bounds.center.x,capsule.bounds.min.y,capsule.bounds.center.z):player.transform.position;
        }
        public string TeamPingText(ArchiveTeamPing ping)
        {
            if(ping==null)return "";
            string who=ActorName(ping.actor);
            switch(ping.kind)
            {
                case ArchiveTeamPingKind.NeedMedkit:return who+" needs a medkit";
                case ArchiveTeamPingKind.ReviveTeammate:return "Revive "+ActorName(ping.targetActor)+" at the nest";
                case ArchiveTeamPingKind.GoToPortal:return who+": head to the exit portal";
                default:return who+": help "+(ping.topic>=0?"at T"+(ping.topic+1):"here");
            }
        }
        public bool TryGetTeamPingPosition(ArchiveTeamPing ping,out Vector3 position)
        {
            position=Vector3.zero;
            if(ping==null)return false;
            var receipt=ActiveTeamPings.FirstOrDefault(p=>p.actor==ping.actor&&p.sequence==ping.sequence);
            if(receipt==null)return false;
            // Resolve the checkpoint receipt, not mutable fields supplied by a caller.
            ping=receipt;
            if(ping.kind==ArchiveTeamPingKind.ReviveTeammate)
            {
                var fallen=Downed(ping.targetActor);
                if(fallen==null||!TeamActorPresent(ping.targetActor)||!IsRevivalTargetAvailable(ping.targetActor))return false;
                position=fallen.anchor;return true;
            }
            if(ping.kind==ArchiveTeamPingKind.NeedMedkit)
            {
                if(!TeamActorPresent(ping.targetActor)||ActorPlayer(ping.targetActor)==null||hunting?.health==null||
                    !hunting.health.IsActorAlive(ping.targetActor)||hunting.health.ActorHearts(ping.targetActor)>=hunting.health.MaximumHealth)return false;
                position=TeamFeet(ActorPlayer(ping.targetActor));return true;
            }
            if(ping.kind==ArchiveTeamPingKind.GoToPortal&&!PortalOpened)return false;
            position=ping.position;return true;
        }
        public ArchiveTeammateInfo[] GetTeamRoster()
        {
            if(!Ready)return Array.Empty<ArchiveTeammateInfo>();
            int[] actors=PhotonNetwork.InRoom?PhotonNetwork.CurrentRoom.Players.Keys.OrderBy(a=>a).Take(4).ToArray():DebugPracticeActors.Take(4).ToArray();
            var health=hunting?.health;
            var known=health!=null&&health.HasAuthoritativeActorHealth?health.ExportActorHealth().Select(h=>h.actor).ToArray():Array.Empty<int>();
            return actors.Select(actor=>
            {
                var player=ActorPlayer(actor);var member=PhotonNetwork.InRoom?PhotonNetwork.CurrentRoom.GetPlayer(actor):null;
                var row=new ArchiveTeammateInfo{actor=actor,name=ActorName(actor),local=actor==LocalActor,inactive=member!=null&&member.IsInactive,
                    healthKnown=known.Contains(actor),maxHearts=health!=null?health.MaximumHealth:3,hasPosition=player!=null,
                    position=player!=null?TeamFeet(player):Vector3.zero};
                row.hearts=row.healthKnown?health.ActorHearts(actor):0;
                var fallen=Downed(actor);var monsters=KhezuHuntingController.ActiveMonsters;
                if(row.inactive)row.status="Reconnecting";
                else if(HasEscapedActor(actor))row.status="Escaped";
                else if(fallen!=null||row.healthKnown&&row.hearts<=0)row.status=fallen!=null&&fallen.phase==2?"Downed at nest":fallen!=null&&fallen.phase==1?"Carried to nest":"Downed";
                else if(player==null)row.status="Spawning";
                else if(monsters.Any(m=>m.State==KhezuHuntState.Caught&&m.CaughtPlayerId==actor))row.status="Captured";
                else if(monsters.Any(m=>m.FailureStunActive&&m.ResolvedActor==actor))row.status="Stunned";
                else if(monsters.Any(m=>(m.State==KhezuHuntState.Hunting||m.State==KhezuHuntState.NavigationBlocked)&&m.TargetPlayerId==actor))row.status="Being hunted";
                else if(!MatchTimeAllowsGameplay)row.status=TeamDefeated?"Match ended":TeamWon?"Victory":"Preparing";
                else if(ActiveTopic>=0&&Near(actor,ActiveTopic,Vector3.zero,14)){row.topic=ActiveTopic;row.status="At T"+(row.topic+1);}
                else row.status="Exploring";
                return row;
            }).ToArray();
        }
        public bool TryGetTeamObjective(out ArchiveTeamObjective objective)
        {
            objective=null;if(!Ready||!CanAct(LocalActor))return false;
            foreach(var ping in ActiveTeamPings.Where(p=>p.actor!=LocalActor||p.kind==ArchiveTeamPingKind.ReviveTeammate||p.kind==ArchiveTeamPingKind.GoToPortal))
                if(TryGetTeamPingPosition(ping,out var point))
                {objective=new ArchiveTeamObjective{kind=ArchiveTeamObjectiveKind.Ping,label=TeamPingText(ping),position=point,topic=ping.topic,actor=ping.targetActor};return true;}
            if(PortalOpened&&exitPortal!=null)
            {objective=new ArchiveTeamObjective{kind=ArchiveTeamObjectiveKind.Portal,label="Exit portal · northern arch",position=exitPortal.transform.position};return true;}
            var local=ActorPlayer(LocalActor);
            if(CountItem(LocalActor,ArchiveSurvivalItem.RevivalKit)>0)
            {
                var fallen=State.survival?.downed?.Where(d=>d!=null&&d.actor!=LocalActor&&TeamActorPresent(d.actor)&&IsRevivalTargetAvailable(d.actor))
                    .OrderBy(d=>(d.anchor-local.transform.position).sqrMagnitude).FirstOrDefault();
                if(fallen!=null){objective=new ArchiveTeamObjective{kind=ArchiveTeamObjectiveKind.Rescue,label="Revive "+ActorName(fallen.actor)+" at nest",position=fallen.anchor,actor=fallen.actor};return true;}
            }
            int topic=ActiveTopic;
            if(topic<0)topic=Enumerable.Range(0,stations.Length).Where(i=>stations[i]!=null&&State.stations[i].phase!=2)
                .OrderBy(i=>(stations[i].transform.position-local.transform.position).sqrMagnitude).DefaultIfEmpty(-1).First();
            if(topic<0||stations[topic]==null)return false;
            objective=new ArchiveTeamObjective{kind=ArchiveTeamObjectiveKind.Topic,topic=topic,label="T"+(topic+1)+" · "+
                (State.stations[topic].phase==1?"Team challenge active":"Read the Book"),position=stations[topic].transform.TransformPoint(ArchiveTopicStation.BookPosition)};
            return true;
        }
    }
}
