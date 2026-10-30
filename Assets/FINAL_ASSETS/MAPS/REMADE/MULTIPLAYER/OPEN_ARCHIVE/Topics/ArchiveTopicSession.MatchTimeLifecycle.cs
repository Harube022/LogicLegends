using System.Collections.Generic;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        readonly Dictionary<Player,Vector3> timeoutAnchors = new Dictionary<Player,Vector3>();
        string timeoutStoppedMatch;

        // End the existing encounters before their next attack/contact tick. This is
        // presentation cleanup for the checkpointed defeat, never another outcome owner.
        void SyncMatchTimeout()
        {
            if(!TeamDefeated || State.survival.defeatReason!=MatchTimeoutReason)
            {ReleaseMatchTimeout();return;}
            if(timeoutStoppedMatch!=State.matchId)
            {
                timeoutStoppedMatch=State.matchId;
                foreach(var monster in KhezuHuntingController.ActiveMonsters)
                {
                    monster.GetComponent<KhezuCinematicJumpscareController>()?.Cancel();
                    if(!IsAuthority)continue;
                    monster.EndRescueCarry(monster.RescueActor);
                    monster.HoldTranquilized(false);
                    monster.Request(KhezuCommand.Sleep);
                }
                ArchiveTopicUI.Current?.Close();
            }
            int[] actors=PhotonNetwork.InRoom?new[]{LocalActor}:DebugPracticeActors;
            foreach(int actor in actors)
            {
                var player=ActorPlayer(actor);if(player==null)continue;
                if(!timeoutAnchors.TryGetValue(player,out var anchor))timeoutAnchors[player]=anchor=player.transform.position;
                player.AcquireMovementLock(gameObject,"The Archive match time has expired",anchor,500);
            }
        }
        void ReleaseMatchTimeout()
        {
            foreach(var player in timeoutAnchors.Keys.ToArray())if(player!=null)player.ReleaseMovementLock(gameObject);
            timeoutAnchors.Clear();timeoutStoppedMatch=null;
        }
    }
}
