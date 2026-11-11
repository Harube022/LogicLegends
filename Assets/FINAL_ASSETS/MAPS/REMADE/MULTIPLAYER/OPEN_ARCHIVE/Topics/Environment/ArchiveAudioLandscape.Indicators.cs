using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    public enum ArchiveGameplayCue { HeartLost, CriticalHealth, TeammateDown, TeamPing, HuntTargeted, TopicStarted, TopicComplete, MatchWarning, BarrierWarning, ConnectionLost, ConnectionRestored, InterfaceTap, IncorrectAnswer }
    [Serializable] public sealed class ArchiveIndicatorSounds
    {
        public AudioClip heartLost, criticalHealth, teammateDown, teamPing, huntTargeted, topicStarted, topicComplete, timerWarning, connectionLost, connectionRestored, interfaceTap;
        public AudioClip incorrectAnswer;
        public AudioClip Clip(ArchiveGameplayCue cue)
        {
            switch (cue)
            {
                case ArchiveGameplayCue.HeartLost: return heartLost;
                case ArchiveGameplayCue.CriticalHealth: return criticalHealth;
                case ArchiveGameplayCue.TeammateDown: return teammateDown;
                case ArchiveGameplayCue.TeamPing: return teamPing;
                case ArchiveGameplayCue.HuntTargeted: return huntTargeted;
                case ArchiveGameplayCue.TopicStarted: return topicStarted;
                case ArchiveGameplayCue.TopicComplete: return topicComplete;
                case ArchiveGameplayCue.ConnectionLost: return connectionLost;
                case ArchiveGameplayCue.ConnectionRestored: return connectionRestored;
                case ArchiveGameplayCue.InterfaceTap: return interfaceTap;
                case ArchiveGameplayCue.IncorrectAnswer: return incorrectAnswer;
                default: return timerWarning;
            }
        }
    }
    public sealed partial class ArchiveAudioLandscape
    {
        [Header("Gameplay indicators - existing SFX volume control")]
        public ArchiveIndicatorSounds indicators = new ArchiveIndicatorSounds();
        [Range(0, 1)] public float indicatorVolume = .38f;
        public int PlayedIndicatorCues { get; private set; }
        public string LastIndicatorCue { get; private set; } = "";
        public int IndicatorCount(ArchiveGameplayCue cue) => indicatorCounts[(int)cue];
        public AudioSource IndicatorVoice => indicatorVoice;
        AudioSource indicatorVoice;
        ArchiveMatchClockView indicatorClock;
        ArchiveDamageFeedbackGraphic damageFeedback;
        readonly Dictionary<Button, UnityAction> indicatorButtons = new Dictionary<Button, UnityAction>();
        readonly Dictionary<int, int> indicatorHearts = new Dictionary<int, int>();
        readonly HashSet<string> indicatorPings = new HashSet<string>();
        readonly HashSet<string> indicatorHunts = new HashSet<string>();
        readonly float[] indicatorCooldown = new float[(int)ArchiveGameplayCue.IncorrectAnswer + 1];
        readonly int[] indicatorCounts = new int[(int)ArchiveGameplayCue.IncorrectAnswer + 1];
        readonly int[] answerSerials = new int[ArchiveTopicCatalog.Names.Length];
        bool indicatorsSeeded, recoveryObserved, wasRecovering;
        string indicatorMatch = "", barrierCueKey = "";
        string wrongBarrierEncounter = "";
        int lastTopic = -1, completedTopics, sharedWarningMask, barrierWarningMask, voicePriority;
        float indicatorNextRead;
        ArchiveGameplayCue candidate;
        int candidatePriority;

        void EnableIndicators()
        {
            indicatorVoice = Voice("Archive_Gameplay_Indicators", transform, null, false, false);
            indicatorVoice.priority = 65;
            indicatorClock = null; indicatorsSeeded = recoveryObserved = false; indicatorNextRead = 0;
            Array.Clear(indicatorCooldown, 0, indicatorCooldown.Length);
            indicatorButtons.Clear(); indicatorHearts.Clear(); indicatorPings.Clear(); indicatorHunts.Clear();
        }
        bool IndicatorFilm() => session.NightEntranceActive || ArchiveMatchCinematic.Covers(session) ||
            KhezuHuntingController.ActiveMonsters.Any(m => m != null && m.gameObject.scene == gameObject.scene && m.GetComponent<KhezuCinematicJumpscareController>()?.IsPlaying == true);
        void Consider(ArchiveGameplayCue cue, int priority)
        { if (priority > candidatePriority) { candidate = cue; candidatePriority = priority; } }
        void PresentIndicators()
        {
            if (indicatorVoice == null || Time.unscaledTime < indicatorNextRead) return;
            indicatorNextRead = Time.unscaledTime + .1f;
            bool recovering = MultiplayerLobbyService.IsGameplayRecovering;
            if (recoveryObserved && recovering != wasRecovering)
            {
                PlayIndicator(recovering ? ArchiveGameplayCue.ConnectionLost : ArchiveGameplayCue.ConnectionRestored, 110);
                // Recovered checkpoints seed silently; old pings, health and progress are not replayed.
                indicatorsSeeded = false;
            }
            recoveryObserved = true; wasRecovering = recovering;
            if (!session.Ready) { indicatorsSeeded = false; return; }
            if (indicatorClock == null) indicatorClock = session.GetComponent<ArchiveMatchClockView>();
            candidatePriority = -1;
            bool seed = !indicatorsSeeded || indicatorMatch != session.State.matchId;
            if (seed) { indicatorMatch = session.State.matchId; indicatorPings.Clear(); indicatorHunts.Clear(); indicatorHearts.Clear(); }
            bool quietGameplay = seed || recovering || Time.timeScale <= 0 || session.HasEscapedActor(session.LocalActor) || IndicatorFilm();
            bool quiet = quietGameplay || session.TeamWon || session.TeamDefeated;
            bool barrier = session.IsLocalBarrierQuestion;
            var health = session.hunting?.health;
            if (health != null)
            {
                foreach (var state in health.ExportActorHealth())
                {
                    if (!quietGameplay && indicatorHearts.TryGetValue(state.actor, out var previous) && state.hearts < previous)
                    {
                        if (state.actor == session.LocalActor)
                        {
                            PlayIndicator(state.hearts == 1 ? ArchiveGameplayCue.CriticalHealth : ArchiveGameplayCue.HeartLost, 120);
                            if (damageFeedback == null) damageFeedback = ArchiveDamageFeedbackGraphic.Create(transform);
                            damageFeedback.Present(health, state.hearts, previous);
                        }
                        else if (!quiet && state.hearts == 0) Consider(ArchiveGameplayCue.TeammateDown, 95);
                    }
                    indicatorHearts[state.actor] = state.hearts;
                }
            }
            ObserveIncorrectAnswers(seed, quiet);
            foreach (var ping in session.ActiveTeamPings)
                if (indicatorPings.Add(ping.actor + ":" + ping.sequence) && !quiet && !barrier && ArchiveTopicSession.Clock - ping.createdAt < 1.5)
                    Consider(ArchiveGameplayCue.TeamPing, 45);
            foreach (var monster in KhezuHuntingController.ActiveMonsters)
            {
                if (monster == null || monster.gameObject.scene != gameObject.scene || !monster.isActiveAndEnabled || monster.TargetPlayerId != session.LocalActor ||
                    (monster.State != KhezuHuntState.Hunting && monster.State != KhezuHuntState.NavigationBlocked)) continue;
                if (indicatorHunts.Add(monster.GetInstanceID() + ":" + monster.HuntSerial) && !quiet && !barrier) Consider(ArchiveGameplayCue.HuntTargeted, 80);
            }
            int active = session.ActiveTopic, complete = session.State.stations.Count(s => s.phase == 2);
            if (!quiet && !barrier)
            {
                if (complete > completedTopics) Consider(ArchiveGameplayCue.TopicComplete, 65);
                else if (active >= 0 && active != lastTopic) Consider(ArchiveGameplayCue.TopicStarted, 35);
            }
            lastTopic = active; completedTopics = complete;
            int warning = indicatorClock != null ? indicatorClock.WarningMask : 0;
            if (!quiet && !barrier && (warning & ~sharedWarningMask) != 0) Consider(ArchiveGameplayCue.MatchWarning, 70);
            sharedWarningMask = warning;
            if (barrier && session.LearningHunting != null)
            {
                var hunt = session.LearningHunting;
                string key = hunt.GetInstanceID() + ":" + hunt.HuntSerial + ":" + hunt.CaptureSerial;
                float remaining = hunt.BarrierSecondsRemaining;
                int mask = remaining <= 10 ? 3 : remaining <= 30 ? 1 : 0;
                if (seed || barrierCueKey != key) barrierWarningMask = mask;
                else if (!quiet && remaining > 0 && (mask & ~barrierWarningMask) != 0) Consider(ArchiveGameplayCue.BarrierWarning, 85);
                barrierCueKey = key; barrierWarningMask = mask;
            }
            else { barrierCueKey = ""; barrierWarningMask = 0; }
            indicatorsSeeded = true;
            if (candidatePriority >= 0) PlayIndicator(candidate, candidatePriority);
        }

        void ObserveIncorrectAnswers(bool seed, bool quiet)
        {
            foreach (var station in session.State.stations)
            {
                int topic = station.topic;
                if (!quiet && station.answerSerial > answerSerials[topic] && station.lastAnswerResult < 0 &&
                    station.lastAnswerActor == session.LocalActor && ArchiveTopicSession.Clock - station.lastAnswerAt < 1.5)
                    Consider(ArchiveGameplayCue.IncorrectAnswer, 92);
                answerSerials[topic] = station.answerSerial;
            }

            var barrier = session.State.barrier;
            if (seed) wrongBarrierEncounter = "";
            if (barrier == null || barrier.actor != session.LocalActor || barrier.outcome != 2 || barrier.selected < 0) return;
            string encounter = barrier.encounterId + ":" + barrier.huntSerial + ":" + barrier.captureSerial;
            if (!quiet && encounter != wrongBarrierEncounter) Consider(ArchiveGameplayCue.IncorrectAnswer, 92);
            wrongBarrierEncounter = encounter;
        }

        void PlayIndicator(ArchiveGameplayCue cue, int priority)
        {
            var clip = indicators?.Clip(cue); int index = (int)cue;
            if (indicatorVoice == null || clip == null || Time.unscaledTime < indicatorCooldown[index] ||
                (indicatorVoice.isPlaying && priority < voicePriority)) return;
            bool damage = cue == ArchiveGameplayCue.HeartLost || cue == ArchiveGameplayCue.CriticalHealth;
            float cooldown = cue == ArchiveGameplayCue.InterfaceTap ? .10f : damage || cue == ArchiveGameplayCue.IncorrectAnswer ? .12f : .8f;
            indicatorCooldown[index] = Time.unscaledTime + cooldown;
            indicatorVoice.Stop(); indicatorVoice.clip = clip; indicatorVoice.pitch = cue == ArchiveGameplayCue.BarrierWarning ? 1.13f : 1;
            indicatorVoice.volume = Mathf.Clamp01(indicatorVolume * (cue == ArchiveGameplayCue.InterfaceTap ? .22f :
                cue == ArchiveGameplayCue.TopicStarted ? .65f : damage ? 1.2f : 1));
            indicatorVoice.Play(); voicePriority = priority;
            LastIndicatorCue = cue.ToString(); PlayedIndicatorCues++; indicatorCounts[index]++;
        }
        void ScanIndicatorButtons()
        {
            foreach (var stale in indicatorButtons.Keys.Where(button => button == null).ToArray()) indicatorButtons.Remove(stale);
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button.gameObject.scene != gameObject.scene || indicatorButtons.ContainsKey(button)) continue;
                string name = button.name.ToLowerInvariant();
                // Movement, equipment and world interactions already have their own feedback.
                if (name.Contains("jump") || name.Contains("hand") || name.Contains("hotbar") || name.Contains("item_action") || name.Contains("ping")) continue;
                UnityAction action = () => { if (session != null && !IndicatorFilm() && !MultiplayerLobbyService.IsGameplayRecovering) PlayIndicator(ArchiveGameplayCue.InterfaceTap, 0); };
                button.onClick.AddListener(action); indicatorButtons.Add(button, action);
            }
        }
        void DisableIndicators()
        {
            foreach (var pair in indicatorButtons) if (pair.Key != null) pair.Key.onClick.RemoveListener(pair.Value);
            indicatorButtons.Clear(); indicatorHearts.Clear(); indicatorPings.Clear(); indicatorHunts.Clear();
            if (damageFeedback != null) Destroy(damageFeedback.transform.parent.gameObject);
            damageFeedback = null;
            if (indicatorVoice != null) { indicatorVoice.Stop(); Destroy(indicatorVoice.gameObject); }
            indicatorVoice = null; indicatorsSeeded = false;
        }
    }
}
