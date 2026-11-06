using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    [Serializable] public sealed class ArchiveMatchRewardReport
    {
        public int version=1,actor;
        public string matchId,outcome;
        public bool online,adminUsed;
        public int[] participants=Array.Empty<int>(),completedTopics=Array.Empty<int>(),restoredTopics=Array.Empty<int>();
        public string[] participationKeys=Array.Empty<string>();
    }
    [Serializable] public sealed class ArchiveRewardSupportCredit
    { public int actor,target; public string kind; }
    [Serializable] public sealed class ArchiveRewardGift
    { public int donor,recipient,item; }
    [Serializable] public sealed class ArchiveMatchRewardState
    {
        public int version=1;
        public bool online,adminUsed;
        public List<int> participants=new List<int>();
        public List<ArchiveRewardSupportCredit> support=new List<ArchiveRewardSupportCredit>();
        public List<ArchiveRewardGift> gifts=new List<ArchiveRewardGift>();
        public List<ArchiveMatchRewardReport> finalReports=new List<ArchiveMatchRewardReport>();
    }
    public sealed class ArchiveMatchRewardBreakdown
    {
        public int coins,baseCoins,crystalCoins,participationCoins,consolationCoins,tier;
        public string exclusionReason="";
    }
    /// <summary>Preview only. The authenticated backend recomputes and grants the wallet receipt.</summary>
    public static class ArchiveMatchRewardPolicy
    {
        public const int MaximumCoins=1000,FullParticipationCredits=3;
        public static bool ValidKey(string key,int actor,int[] roster)
        {
            if(string.IsNullOrEmpty(key)||key.Length>64)return false;
            var parts=key.Split(':');
            if(parts.Length==4&&parts[0]=="work"&&int.TryParse(parts[1],out int topic)&&int.TryParse(parts[2],out int round)&&int.TryParse(parts[3],out int subphase))
                return topic>=0&&topic<10&&round>=0&&round<ArchiveTopicCatalog.RoundCount(topic)&&subphase>=0&&subphase<(topic==2?2:1)&&key=="work:"+topic+":"+round+":"+subphase;
            return parts.Length==3&&parts[0]=="support"&&new[]{"medkit","battery","revive","give"}.Contains(parts[1])&&
                int.TryParse(parts[2],out int target)&&target>0&&target!=actor&&roster.Contains(target)&&key=="support:"+parts[1]+":"+target;
        }
        public static ArchiveMatchRewardBreakdown Calculate(ArchiveMatchRewardReport report)
        {
            var result=new ArchiveMatchRewardBreakdown();
            if(report==null){result.exclusionReason="No completed match.";return result;}
            if(report.adminUsed){result.exclusionReason="Admin-assisted match — no permanent rewards.";return result;}
            if(!report.online){result.exclusionReason="Offline practice — no permanent rewards.";return result;}
            var roster=report.participants;var completed=report.completedTopics;var restored=report.restoredTopics;var keys=report.participationKeys;
            if(report.version!=1||!Regex.IsMatch(report.matchId??"","^[a-f0-9]{32}$")||report.actor<=0||
                roster==null||roster.Length<2||roster.Length>4||roster.Any(a=>a<=0)||roster.Distinct().Count()!=roster.Length||!roster.Contains(report.actor)||
                completed==null||completed.Any(t=>t<0||t>=10)||completed.Distinct().Count()!=completed.Length||
                restored==null||restored.Any(t=>!completed.Contains(t))||restored.Distinct().Count()!=restored.Length||
                (report.outcome!="victory"&&report.outcome!="defeat")||report.outcome=="victory"&&completed.Length!=10||
                keys==null||keys.Length>128||keys.Any(key=>!ValidKey(key,report.actor,roster)))
            {result.exclusionReason="Match reward evidence is incomplete.";return result;}
            result.tier=Math.Min(FullParticipationCredits,keys.Distinct().Count());
            if(result.tier==0){result.exclusionReason="No confirmed puzzle work or teammate support.";return result;}
            if(report.outcome=="victory")
            {
                result.baseCoins=600*result.tier/3;
                result.crystalCoins=25*restored.Length*result.tier/3;
                result.participationCoins=50*result.tier;
            }
            else result.consolationCoins=Math.Min(150,15*completed.Length)*result.tier/3;
            result.coins=result.baseCoins+result.crystalCoins+result.participationCoins+result.consolationCoins;
            return result;
        }
        public static ArchiveMatchRewardReport Build(ArchiveTeamState team,int actor)
        {
            var report=new ArchiveMatchRewardReport{matchId=team?.matchId,actor=actor,outcome=team?.survival?.defeated==true?"defeat":team?.exit?.won==true?"victory":""};
            if(team?.rewards==null||team.stations==null||team.stations.Length!=10)return report;
            report.online=team.rewards.online;report.adminUsed=team.rewards.adminUsed||team.crystals?.Any(c=>c.adminBypass)==true;
            report.participants=team.rewards.participants.Distinct().OrderBy(a=>a).ToArray();
            report.completedTopics=Enumerable.Range(0,10).Where(t=>team.stations[t].phase==2&&ArchiveCrystalProgress.Solved(team,t)==ArchiveCrystalProgress.Total(t)).ToArray();
            report.restoredTopics=(team.crystals??new List<ArchiveCrystalState>()).Where(c=>c.fullyRestored&&!c.adminBypass&&report.completedTopics.Contains(c.topic)).Select(c=>c.topic).Distinct().OrderBy(t=>t).ToArray();
            var work=Enumerable.Range(0,10).SelectMany(t=>ArchiveCrystalProgress.Receipts(team,t))
                .Where(r=>(r.crystalContributors??Array.Empty<int>()).Contains(actor)).Select(r=>"work:"+r.topic+":"+r.round+":"+r.subphase);
            var support=team.rewards.support.Where(r=>r.actor==actor).Select(r=>"support:"+r.kind+":"+r.target);
            report.participationKeys=work.Concat(support).Distinct().OrderBy(k=>k).Take(128).ToArray();
            return report;
        }
    }
    public sealed partial class ArchiveTopicSession
    {
        string rewardOwnerUid;
        int rewardOwnerActor;
        bool rewardIdentityBound;
        ArchiveTeamState rewardCountState;
        int rewardCountRevision=-1;
        readonly Dictionary<int,int> rewardCounts=new Dictionary<int,int>();
        public int RewardContributionCount(int actor)
        {
            if(!Ready)return 0;
            if(rewardCountState!=State||rewardCountRevision!=State.revision)
            {rewardCountState=State;rewardCountRevision=State.revision;rewardCounts.Clear();}
            if(!rewardCounts.TryGetValue(actor,out int count))
            {
                var report=RewardReport(actor);
                count=report==null?0:Math.Min(3,report.participationKeys.Distinct().Count());
                rewardCounts[actor]=count;
            }
            return count;
        }
        void BindRewardIdentity()
        {
            if(rewardIdentityBound)return;
            rewardIdentityBound=true;rewardOwnerUid=MultiplayerRewardBackendClient.UserId;
            rewardOwnerActor=PhotonNetwork.InRoom?PhotonNetwork.LocalPlayer.ActorNumber:-1;
        }
        void NormalizeRewards()
        {
            if(State==null)return;
            if(State.rewards==null)State.rewards=new ArchiveMatchRewardState{online=PhotonNetwork.InRoom&&!PhotonNetwork.OfflineMode};
            if(State.rewards.participants==null)State.rewards.participants=new List<int>();
            if(State.rewards.support==null)State.rewards.support=new List<ArchiveRewardSupportCredit>();
            if(State.rewards.gifts==null)State.rewards.gifts=new List<ArchiveRewardGift>();
            if(State.rewards.finalReports==null)State.rewards.finalReports=new List<ArchiveMatchRewardReport>();
            if(IsAuthority&&State.rewards.finalReports.Count==0&&PhotonNetwork.InRoom)
                foreach(var member in PhotonNetwork.PlayerList)
                    if(!State.rewards.participants.Contains(member.ActorNumber))State.rewards.participants.Add(member.ActorNumber);
        }
        public void MarkRewardsAdminUsed()
        {
            if(!Ready||!IsAuthority)return;
            NormalizeRewards();
            // A finished match's published receipt is immutable. ResetMatch
            // creates a new state and marks that new practice match separately.
            if(State.rewards.finalReports.Count>0)return;
            if(State.rewards.adminUsed)return;
            State.rewards.adminUsed=true;
            // Legacy inspector/test buttons do not all call the topic commit path.
            Commit();
        }
        void RecordRewardSupport(int actor,int target,string kind)
        {
            if(!IsAuthority||!Ready||actor==target||actor==0||target==0||TeamWon||TeamDefeated)return;
            NormalizeRewards();
            if(State.rewards.support.Count>=48||State.rewards.support.Any(s=>s.actor==actor&&s.target==target&&s.kind==kind))return;
            State.rewards.support.Add(new ArchiveRewardSupportCredit{actor=actor,target=target,kind=kind});
        }
        void TrackRewardGift(int actor,int target,int item)
        {
            NormalizeRewards();
            var gifts=State.rewards.gifts;
            var forwarded=gifts.FirstOrDefault(g=>g.recipient==actor&&g.item==item);
            if(forwarded!=null)
            {
                // Passing supplies onward preserves the original donor. Returning
                // an unused item to its owner never earns a support receipt.
                if(forwarded.donor==target)gifts.Remove(forwarded);else forwarded.recipient=target;
            }
            else if(gifts.Count<48&&!gifts.Any(g=>g.donor==actor&&g.recipient==target&&g.item==item))
                gifts.Add(new ArchiveRewardGift{donor=actor,recipient=target,item=item});
        }
        void UseRewardGift(int actor,int item)
        {
            NormalizeRewards();
            var gift=State.rewards.gifts.FirstOrDefault(g=>g.recipient==actor&&g.item==item);
            if(gift==null)return;
            RecordRewardSupport(gift.donor,actor,"give");State.rewards.gifts.Remove(gift);
        }
        void FinalizeMatchRewards()
        {
            NormalizeRewards();
            if(!TeamWon&&!TeamDefeated||State.rewards.finalReports.Count>0)return;
            foreach(int actor in State.rewards.participants)State.rewards.finalReports.Add(ArchiveMatchRewardPolicy.Build(State,actor));
        }
        public ArchiveMatchRewardReport RewardReport(int actor)
        {
            if(!Ready)return null;
            return State.rewards?.finalReports?.FirstOrDefault(r=>r.actor==actor)??ArchiveMatchRewardPolicy.Build(State,actor);
        }
        public void CaptureMatchReward()
        {
            BindRewardIdentity();
            if(!Ready||!TeamWon&&!TeamDefeated||string.IsNullOrEmpty(rewardOwnerUid)||rewardOwnerUid!=MultiplayerRewardBackendClient.UserId)return;
            var report=State.rewards?.finalReports?.FirstOrDefault(r=>r.actor==rewardOwnerActor);
            if(report!=null&&report.online)MultiplayerRewardOutbox.Queue(rewardOwnerUid,report);
        }
        public string MatchRewardSummary
        {
            get
            {
                var report=RewardReport(rewardOwnerActor==0?LocalActor:rewardOwnerActor);
                var preview=ArchiveMatchRewardPolicy.Calculate(report);
                if(!string.IsNullOrEmpty(preview.exclusionReason))return preview.exclusionReason;
                if(rewardOwnerUid!=MultiplayerRewardBackendClient.UserId)return "Account changed — this match cannot credit another account.";
                string breakdown=report.outcome=="victory"?"Victory +"+preview.baseCoins+" · Crystals +"+preview.crystalCoins+" · Participation +"+preview.participationCoins:
                    "Completed topics: "+report.completedTopics.Length+" / 10 · Consolation +"+preview.consolationCoins;
                string status=string.IsNullOrEmpty(rewardOwnerUid)?"Sign in before starting a match to save coins.":MultiplayerRewardOutbox.Status(State.matchId);
                return breakdown+"\nContribution "+preview.tier+" / 3 · "+status;
            }
        }
    }
}
