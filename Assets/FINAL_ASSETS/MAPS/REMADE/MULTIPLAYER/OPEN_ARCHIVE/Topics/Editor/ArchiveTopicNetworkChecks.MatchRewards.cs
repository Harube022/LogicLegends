using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveTopicNetworkChecks
    {
        static bool matchRewardsOnly;
        static Action matchRewardsCleanup;
        static TaskCompletionSource<string> rewardReply;
        static int rewardRequests;
        static string rewardRequestJson;
        [Serializable] sealed class RewardWalletFixtureReply { public bool success=true;public int status=200;public string body; }
        const string RewardQaUid="multiplayer-photon-reward-qa",RewardPreference="MultiplayerRewardOutbox.v1."+RewardQaUid;
        const BindingFlags RewardPrivate=BindingFlags.Static|BindingFlags.NonPublic;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Match Rewards (Live Photon)")]
        public static void BeginRewards()
        {
            if(!Application.isPlaying||PhotonNetwork.InRoom||active)throw new InvalidOperationException("Run in fresh offline Play, outside a Photon room.");
            PrepareMatchRewardsFixture();
            try{StartChecks(false,matchRewards:true);}catch{matchRewardsCleanup?.Invoke();matchRewardsCleanup=null;throw;}
        }
        static void SaveMatchRewards()
        {
            Directory.CreateDirectory("Temp/ArchiveMatchRewards");
            File.WriteAllText("Temp/ArchiveMatchRewards/Photon.json",JsonUtility.ToJson(report,true));
        }
        static void PrepareMatchRewardsFixture()
        {
            var transport=typeof(MultiplayerRewardBackendClient).GetField("ValidationTransport",RewardPrivate);
            var uid=typeof(MultiplayerRewardBackendClient).GetField("ValidationUid",RewardPrivate);
            var commerceTransport=typeof(CommerceClient).GetField("ValidationRequest",RewardPrivate);
            var commerceUid=typeof(CommerceClient).GetField("ValidationUid",RewardPrivate);
            var savedTransport=transport.GetValue(null);var savedUid=uid.GetValue(null);
            var savedCommerceTransport=commerceTransport.GetValue(null);var savedCommerceUid=commerceUid.GetValue(null);
            bool hadPreference=PlayerPrefs.HasKey(RewardPreference);string savedPreference=PlayerPrefs.GetString(RewardPreference,"");
            var stores=(IDictionary)typeof(MultiplayerRewardOutbox).GetField("stores",RewardPrivate).GetValue(null);
            object savedStore=stores.Contains(RewardQaUid)?stores[RewardQaUid]:null;
            PlayerPrefs.DeleteKey(RewardPreference);stores.Remove(RewardQaUid);
            rewardRequests=0;rewardRequestJson=null;rewardReply=new TaskCompletionSource<string>();
            uid.SetValue(null,RewardQaUid);
            transport.SetValue(null,new Func<string,string,Task<string>>((path,json)=>
            {
                if(path!="/multiplayer/complete")throw new InvalidOperationException("Unexpected reward fixture endpoint.");
                rewardRequests++;rewardRequestJson=json;return rewardReply.Task;
            }));
            // Wallet refresh listeners must also remain inside the fake account.
            commerceUid.SetValue(null,RewardQaUid);
            commerceTransport.SetValue(null,new Func<string,string,string,int,Task<string>>((url,json,token,timeout)=>
                Task.FromResult(JsonUtility.ToJson(new RewardWalletFixtureReply{body="{\"coins\":1000,\"gems\":2,\"sandbox\":false,\"live\":true,\"inventory\":[]}"}))));
            matchRewardsCleanup=()=>
            {
                // Abort an incomplete fake reply before restoring account hooks.
                rewardReply?.TrySetException(new OperationCanceledException("Reward fixture ended."));
                transport.SetValue(null,savedTransport);uid.SetValue(null,savedUid);
                commerceTransport.SetValue(null,savedCommerceTransport);commerceUid.SetValue(null,savedCommerceUid);
                stores.Remove(RewardQaUid);if(savedStore!=null)stores[RewardQaUid]=savedStore;
                if(hadPreference)PlayerPrefs.SetString(RewardPreference,savedPreference);else PlayerPrefs.DeleteKey(RewardPreference);
                PlayerPrefs.Save();
            };
        }
        static IEnumerator MatchRewardsFlow(Peer guest,string room)
        {
            report.scope="Live private Photon: rendered Editor host and independent Realtime guest. Uses actual accepted T1 rune answers, replay/spoof rejection and authority handoff. A clearly marked complete-topic/crystal/downed terminal fixture exercises frozen per-member reports and pre-return durable capture. Backend replies and identities are fake; no real wallet, deployed endpoint, rendered remote phone or naturally completed ten-topic match is claimed.";
            Status="Live reward participation and handoff";
            session.suppressDefeatForVerification=true;
            if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=true;
            session.hunting.Request(KhezuCommand.Sleep);
            int host=PhotonNetwork.LocalPlayer.ActorNumber;
            Check(session.State.rewards.online&&!session.State.rewards.adminUsed&&session.State.rewards.participants.OrderBy(a=>a).SequenceEqual(new[]{host,guest.Actor}.OrderBy(a=>a)),
                "A fresh real two-player match starts with an online, unassisted reward roster");
            var station=session.stations[0];
            for(int round=0;round<2;round++)
            {
                yield return Move(guest,station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
                yield return Send(guest,0,ArchiveAction.Start);
                var question=session.Question(0);
                yield return Move(guest,station,station.ItemPosition(question.correctChoice,question));
                yield return Send(guest,0,ArchiveAction.BeginChoice,question.correctChoice);
                int nextRound=round+1;
                yield return Wait(()=>session.State.stations[0].round==nextRound&&guest.state?.stations[0].round==nextRound,"Accepted guest rune becomes shared solved receipt",8);
            }
            var guestReport=ArchiveMatchRewardPolicy.Build(session.State,guest.Actor);
            Check(guestReport.participationKeys.Length==2&&guestReport.participationKeys.All(k=>k.StartsWith("work:0:",StringComparison.Ordinal))&&
                ArchiveMatchRewardPolicy.Build(session.State,host).participationKeys.Length==0,
                "Two actual accepted guest rune answers create two unique work credits without crediting an idle host");
            Packet(guest,lastCommand);yield return .2f;
            Check(ArchiveMatchRewardPolicy.Build(session.State,guest.Actor).participationKeys.SequenceEqual(guestReport.participationKeys),
                "Replaying a rewarded guest command cannot duplicate participation credit");
            var fake=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));fake.revision+=1000;
            fake.rewards.support.Add(new ArchiveRewardSupportCredit{actor=guest.Actor,target=host,kind="revive"});
            guest.client.OpRaiseEvent(ArchiveTopicSession.SnapshotEvent,new object[]{ArchiveTopicSession.Channel,JsonUtility.ToJson(fake)},new RaiseEventOptions{Receivers=ReceiverGroup.All},SendOptions.SendReliable);
            yield return .2f;
            Check(session.State.rewards.support.Count==0&&ArchiveMatchRewardPolicy.Build(session.State,guest.Actor).participationKeys.Length==2,
                "A non-master snapshot cannot invent teammate-support reward credit");
            string firstMatch=session.State.matchId;
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(guest.Actor));
            yield return Wait(()=>!PhotonNetwork.IsMasterClient,"Reward authority handed to the independent peer");
            guest.client.CurrentRoom.SetMasterClient(guest.client.CurrentRoom.GetPlayer(host));
            yield return Wait(()=>PhotonNetwork.IsMasterClient,"Rendered reward authority restored");yield return .2f;
            Check(session.State.matchId==firstMatch&&!session.State.rewards.adminUsed&&
                ArchiveMatchRewardPolicy.Build(session.State,guest.Actor).participationKeys.SequenceEqual(guestReport.participationKeys),
                "Master handoff preserves the reward roster, match ID and accepted work ledger");

            Status="Admin exclusion is sticky inside its match";
            Check(session.RequestTopicAdmin(0,session.State.stations[0].revision,false),"The existing topic reset command executes through its real admin path");
            yield return Wait(()=>guest.state?.rewards?.adminUsed==true,"Admin exclusion reaches the independent peer",8);
            var adminPreview=ArchiveMatchRewardPolicy.Build(session.State,guest.Actor);adminPreview.outcome="victory";
            Check(session.State.rewards.adminUsed&&ArchiveMatchRewardPolicy.Calculate(adminPreview).coins==0,
                "Resetting an admin-used topic cannot restore permanent reward eligibility");
            session.Initialize(271828);
            yield return Wait(()=>guest.state?.matchId==session.State.matchId&&session.MatchTimeAllowsGameplay,"Explicit fresh-match fixture starts",25);
            Check(session.State.matchId!=firstMatch&&!session.State.rewards.adminUsed&&session.State.rewards.finalReports.Count==0,
                "A genuinely new match gets a new identity and clears the previous match's admin exclusion");

            Status="Trusted terminal fixture freezes and queues member reports";
            int[] roster={host,guest.Actor};
            session.State.solved.Clear();
            for(int topic=0;topic<10;topic++)
            {
                var state=session.State.stations[topic];state.phase=2;state.round=ArchiveTopicCatalog.RoundCount(topic);state.timerRunning=false;
                for(int round=0;round<ArchiveTopicCatalog.RoundCount(topic);round++)
                    for(int subphase=0;subphase<(topic==2?2:1);subphase++)
                        session.State.solved.Add(new ArchiveSolvedMilestone{topic=topic,round=round,subphase=subphase,resetSerial=state.testResetSerial,
                            contributors=roster.ToArray(),crystalContributors=roster.ToArray()});
                var crystal=session.State.crystals[topic];crystal.started=crystal.closed=crystal.online=crystal.fullyRestored=true;crystal.adminBypass=false;
            }
            // Trusted health fixture proves a previously contributing downed
            // player is not erased from the team reward calculation.
            for(int damage=0;damage<session.hunting.health.MaximumHealth;damage++)
                if(!session.hunting.health.DamageAuthoritativeActor(host))break;
            session.State.exit.opened=session.State.exit.won=true;session.State.exit.wonAt=ArchiveTopicSession.Clock;
            CommitLearningFixture();
            yield return Wait(()=>guest.state?.rewards?.finalReports?.Count==2,"Frozen terminal reward reports replicate",8);
            var hostFinal=session.RewardReport(host);var guestFinal=session.RewardReport(guest.Actor);
            Check(session.State.rewards.finalReports.Count==2&&ArchiveMatchRewardPolicy.Calculate(hostFinal).coins==1000&&
                ArchiveMatchRewardPolicy.Calculate(guestFinal).coins==1000,
                "The trusted full-restoration victory freezes separate capped 1000-coin reports for both members");
            Check(guest.state.rewards.finalReports.All(r=>r.matchId==session.State.matchId&&r.completedTopics.Length==10&&r.restoredTopics.Length==10),
                "The independent peer receives the same frozen terminal topic and crystal evidence");
            Check(session.hunting.health.ActorHearts(host)==0&&ArchiveMatchRewardPolicy.Calculate(hostFinal).tier==3,
                "A downed teammate retains meaningful work and full team reward eligibility");
            Check(rewardRequests==1&&PlayerPrefs.GetString(RewardPreference,"").Contains(session.State.matchId)&&
                MultiplayerRewardOutbox.GetReceipt(session.State.matchId)==null,
                "The terminal report is durably queued before a backend reply or party-return cleanup");
            string queuedJson=rewardRequestJson;
            var freeze=typeof(MultiplayerLobbyService).GetMethod("FreezeFinishedMatchForPartyReturn",BindingFlags.Static|BindingFlags.NonPublic);
            freeze.Invoke(null,null);
            Check(!session.enabled&&PlayerPrefs.GetString(RewardPreference,"").Contains(session.State.matchId)&&rewardRequests==1,
                "The actual retained-party freeze preserves the pending same-match reward report");
            var expected=ArchiveMatchRewardPolicy.Calculate(hostFinal);
            rewardReply.SetResult(JsonUtility.ToJson(new MultiplayerRewardBackendClient.Receipt{matchId=hostFinal.matchId,status="completed",reward=expected.coins,
                baseCoins=expected.baseCoins,crystalCoins=expected.crystalCoins,participationCoins=expected.participationCoins,
                consolationCoins=expected.consolationCoins,tier=expected.tier,coins=expected.coins,gems=2,gemReward=2}));
            yield return Wait(()=>MultiplayerRewardOutbox.GetReceipt(hostFinal.matchId)!=null,"Confirmed fake backend reward arrives",8);
            Check(MultiplayerRewardOutbox.GetReceipt(hostFinal.matchId).reward==1000&&MultiplayerRewardOutbox.Status(hostFinal.matchId).Contains("earned: +"+1000.ToString("N0")),
                "Only the confirmed backend receipt turns the pending local report into earned currency");
            var edited=JsonUtility.FromJson<ArchiveMatchRewardReport>(queuedJson);edited.participationKeys=Array.Empty<string>();
            MultiplayerRewardOutbox.Queue(RewardQaUid,edited);session.CaptureMatchReward();yield return .1f;
            Check(rewardRequests==1&&MultiplayerRewardOutbox.GetReceipt(hostFinal.matchId).reward==1000,
                "Repeated capture or edited same-match reports cannot send a second reward request");
        }
    }
}
