using System;
using System.Linq;
using UnityEngine;
using Photon.Pun;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        public ArchiveT1CoopState Relay(int topic) => Ready && topic >= 0 && topic < 10 && State.topicRelays != null && State.topicRelays.Count > topic ? State.topicRelays[topic] : null;
        public ArchiveCoopLesson RelayLesson(int topic) => ArchiveCoopLesson.Build(topic, State.seed, State.stations[topic].testResetSerial);
        public bool RelayAvailable(int topic)
        {
            var c = Relay(topic);
            return c != null && c.round >= 0 && !c.closed && !c.completed && !TeamWon && ActiveTopic == topic && State.stations[topic].phase != 2;
        }
        void BeginTopicRelay(int topic)
        {
            var old = Relay(topic); if (old == null || old.round >= 0) return;
            var lesson = RelayLesson(topic);
            State.topicRelays[topic] = new ArchiveT1CoopState { policyVersion = 3, topic = topic, serial = old.serial + 1, round = 0,
                connective = lesson.connective, target = lesson.target, startedAt = Clock,
                feedback = "Optional while this topic is active. Main progress never waits for this bonus." };
            State.coop = State.topicRelays[0]; RefreshRelayPolicy(topic);
            if(topic>0)PrepareBonusActivity(topic);
        }
        void CloseTopicRelay(int topic)
        {
            var c = Relay(topic); if (c == null || c.closed) return;
            c.closed = true; c.pConfirmed = c.qConfirmed = false; c.assignmentRevision++;
            c.traversals.Clear();
            c.bonusHeld=-1;c.bonusHolder=0;c.bonusFirst=c.bonusSecond=-1;c.bonusRouteActive=false;
            c.bonusCarries.Clear();
            if (!c.completed) c.feedback = "Closed: the main topic is complete. Unfinished optional work cannot be resumed.";
        }
        void ResetTopicRelay(int topic, bool complete)
        {
            var old = Relay(topic); if (old == null) return;
            State.topicRelays[topic] = new ArchiveT1CoopState { policyVersion = 3, topic = topic, serial = old.serial + 1, closed = complete,
                feedback = complete ? "Closed by Admin for testing. No bonus awarded." : "" };
            State.completedRelays.RemoveAll(c => c.topic == topic); State.coop = State.topicRelays[0];
        }
        int[] RelayTeamActors() => !Ready ? Array.Empty<int>() : !PhotonNetwork.InRoom ? SurvivingActors().Where(a => !HasEscapedActor(a)).ToArray() :
            hunting == null || hunting.health == null || !hunting.health.HasAuthoritativeActorHealth
            ? PhotonNetwork.CurrentRoom.Players.Keys.Where(a => !HasEscapedActor(a)).ToArray()
            : SurvivingActors().Where(a => !HasEscapedActor(a)).ToArray();
        public string RelayModeLabel(int topic) => !PhotonNetwork.InRoom ? "OFFLINE PRACTICE · one player" : Relay(topic)?.reducedTeam == true
            ? "LAST SURVIVOR · practice" : Relay(topic)?.recoveryPending == true ? "TEAM BONUS · teammate reconnecting" : "TEAM BONUS · main solver + support";
        public int[] RelayAvailableActors(int topic) => !Ready ? Array.Empty<int>() :
            (PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Select(a => a.ActorNumber) : new[] { LocalActor }).Where(a => CanAct(a) && Near(a, topic, Vector3.zero, 20)).Distinct().ToArray();
        public bool RelayCanSetInput(int actor, int topic, int input)
        {
            var c = Relay(topic);
            if (topic!=0 || !RelayAvailable(topic) || input < 0 || input > 1 || c.open || !CanAct(actor)) return false;
            var trial = stations[topic].GetComponent<ArchiveT1CoopTrial>();
            return trial != null && Near(actor, topic, trial.SwitchPosition(input), 2.8f);
        }
        public bool RelayCanConfirmInput(int actor, int topic, int input)
        {
            if (!RelayCanSetInput(actor, topic, input)) return false;
            var c = Relay(topic);
            return input == 0 ? c.pSet && c.actorP == actor && !c.pConfirmed : c.qSet && c.actorQ == actor && !c.qConfirmed;
        }
        bool AcceptTopicRelayInput(int actor, ArchiveCommand cmd)
        {
            var c = Relay(cmd.topic);
            if (cmd.topic!=0 || !RelayAvailable(cmd.topic) || c == null || cmd.matchId != State.matchId || cmd.captureSerial != c.serial ||
                cmd.a < 0 || cmd.a > 1 || !FreshLearningSequence(actor, cmd)) return false;
            bool changed = RefreshRelayPolicy(cmd.topic), confirm = cmd.action == ArchiveAction.ConfirmT1Input;
            if (confirm ? cmd.b != c.assignmentRevision || !RelayCanConfirmInput(actor, cmd.topic, cmd.a) : cmd.b < 0 || cmd.b > 1 || !RelayCanSetInput(actor, cmd.topic, cmd.a))
            { if (changed) Commit(); return false; }
            RememberLearningSequence(actor, cmd);
            if (confirm)
            {
                if (cmd.a == 0) c.pConfirmed = true; else c.qConfirmed = true;
                c.feedback = "Confirmed. The other input and a teammate's main-topic contribution are checked together.";
            }
            else
            {
                bool value = cmd.b == 1;
                bool newValue = cmd.a == 0 ? !c.pSet || c.p != value || c.actorP != actor : !c.qSet || c.q != value || c.actorQ != actor;
                if (newValue) { c.pConfirmed = c.qConfirmed = false; c.assignmentRevision++; c.explanation = ""; }
                if (cmd.a == 0) { c.p = value; c.pSet = true; c.actorP = actor; }
                else { c.q = value; c.qSet = true; c.actorQ = actor; }
                c.feedback = "Set both inputs, then confirm each current value. Either support player can handle both runes.";
            }
            TryOpenRelay(cmd.topic); Commit(); return true;
        }
        public void RequestRelayInput(int topic, int input, bool value)
        {
            var c = Relay(topic); if (c == null) return;
            SendLearningCommand(new ArchiveCommand { topic = topic, action = ArchiveAction.SetT1Input, a = input, b = value ? 1 : 0, captureSerial = c.serial });
        }
        public void RequestRelayConfirm(int topic, int input)
        {
            var c = Relay(topic); if (c == null) return;
            SendLearningCommand(new ArchiveCommand { topic = topic, action = ArchiveAction.ConfirmT1Input, a = input, b = c.assignmentRevision, captureSerial = c.serial });
        }
        int[] RelayMainContributors(int topic) => State.solved.Where(s => s.topic == topic).SelectMany(s => s.contributors).Distinct().ToArray();
        bool RelayTeamworkReady(int topic)
        {
            var c = Relay(topic); var main = RelayMainContributors(topic); if (main.Length == 0) return false;
            if (c.reducedTeam) return true;
            var team = RelayTeamActors();
            return main.Concat(new[] { c.actorP, c.actorQ }).Where(team.Contains).Distinct().Count() >= 2;
        }
        void TryOpenRelay(int topic)
        {
            var c = Relay(topic);
            if (!RelayAvailable(topic) || c.open || !c.pSet || !c.qSet || !c.pConfirmed || !c.qConfirmed) return;
            if (!CanAct(c.actorP) || !CanAct(c.actorQ)) return;
            if (!RelayTeamworkReady(topic)) { c.feedback = "Waiting for another teammate to solve part of the main topic. One person cannot earn a team bonus alone."; return; }
            var lesson = RelayLesson(topic);
            if (!lesson.Accepts(c.p, c.q))
            {
                c.attempts++; c.pConfirmed = c.qConfirmed = false; c.assignmentRevision++;
                c.explanation = lesson.freeInputs ? ExplainT1Connective(c.connective, c.p, c.q) : lesson.explanation;
                c.feedback = "Review the claims and confirm again. Optional practice adds no monster mistake.";
                return;
            }
            c.open = true; c.openedAt = Clock;
            c.explanation = lesson.freeInputs ? ExplainT1Connective(c.connective, c.p, c.q) : "P=" + Truth(c.p) + ", Q=" + Truth(c.q) + ". " + lesson.explanation;
            c.feedback = "Correct! Cross the arch from P to Q before the main topic finishes to complete the optional activity.";
        }
        bool RefreshRelayPolicy(int topic)
        {
            var c = Relay(topic); if (c == null || c.open || c.completed) return false;
            var team = RelayTeamActors();
            bool reduced = !MultiplayerLobbyService.IsGameplayRecovering && team.Length == 1;
            bool recovering = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Players.Values.Any(p => p.IsInactive && team.Contains(p.ActorNumber));
            bool changed = c.reducedTeam != reduced || c.recoveryPending != recovering;
            if (c.reducedTeam != reduced) { c.pConfirmed = c.qConfirmed = false; c.assignmentRevision++; }
            c.reducedTeam = reduced; c.recoveryPending = recovering; return changed;
        }
        readonly double[] nextRelayCheckpoint = new double[10];
        void TickTopicRelays()
        {
            if (!Ready) return;
            for (int topic = 0; topic < 10; topic++)
            {
                var c = Relay(topic); if (c == null || c.round < 0 || c.closed || c.completed) continue;
                if (State.stations[topic].phase == 2) { CloseTopicRelay(topic); Commit(); continue; }
                if (!RelayAvailable(topic)) continue;
                if(topic>0){TickBonusActivity(topic);continue;}
                int attempts = c.attempts; bool wasOpen = c.open, changed = RefreshRelayPolicy(topic);
                TryOpenRelay(topic); changed |= attempts != c.attempts;
                var trial = stations[topic].GetComponent<ArchiveT1CoopTrial>();
                if (trial != null && c.open)
                    foreach (int actor in RelayAvailableActors(topic))
                    {
                        if (c.crossed.Contains(actor) || !trial.CrossedFromEntry(actor, ActorPlayer(actor))) continue;
                        c.crossed.Add(actor); c.completed = true;
                        // Reuse the existing participation receipt; no LP or leaderboard system.
                        if (!c.reducedTeam)
                            foreach (int member in new[] { actor, c.actorP, c.actorQ }.Concat(RelayMainContributors(topic)).Distinct()) AddQualityCredit(member, "coop:" + topic);
                        c.feedback = c.reducedTeam ? "Optional practice complete (one player)." : "Optional team bonus complete! Main topic progress continues.";
                        if (!State.completedRelays.Any(v => v.topic == topic)) State.completedRelays.Add(JsonUtility.FromJson<ArchiveT1CoopState>(JsonUtility.ToJson(c)));
                        break;
                    }
                bool checkpoint = c.open && c.traversals.Any(t => t.valid) && Clock >= nextRelayCheckpoint[topic];
                if (checkpoint) nextRelayCheckpoint[topic] = Clock + .5;
                if (changed || wasOpen != c.open || c.completed || checkpoint) Commit();
            }
        }
    }
}
