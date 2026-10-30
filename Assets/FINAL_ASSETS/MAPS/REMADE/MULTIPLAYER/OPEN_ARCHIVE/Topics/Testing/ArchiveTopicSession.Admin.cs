using System;
using System.Linq;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        // Local testing permission comes from the build and the existing room authority, never player properties.
        public bool CanUseTopicAdmin
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Application.isPlaying && Ready && IsAuthority;
#else
                return false;
#endif
            }
        }
        public bool RequestTopicAdmin(int topic, int revision, bool complete)
        {
            if (!CanUseTopicAdmin || topic < 0 || topic >= 10) return false;
            var record = State.actors.FirstOrDefault(a => a.actor == LocalActor);
            localSequence = Math.Max(localSequence, record?.lastSequence ?? 0);
            return Accept(LocalActor,new ArchiveCommand { topic=topic,revision=revision,sequence=++localSequence,
                action=complete?ArchiveAction.AdminCompleteTopic:ArchiveAction.AdminResetTopic });
        }
        bool AcceptTopicAdmin(int actor, ArchiveCommand command)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!CanUseTopicAdmin || actor != LocalActor || command.topic != ActiveTopic || TeamWon || TeamDefeated) return false;
            var previous = State.stations[command.topic];
            var record = State.actors.FirstOrDefault(a => a.actor == actor);
            if (command.revision != previous.revision || command.sequence <= (record?.lastSequence ?? 0)) return false;
            if (record == null) { record = new ArchiveActor { actor=actor,displayName=ActorName(actor) }; State.actors.Add(record); }
            record.lastSequence = command.sequence;
            bool complete = command.action == ArchiveAction.AdminCompleteTopic;
            // Replace only this topic's transient work; late packets cannot match its newer revision.
            State.stations[command.topic] = new ArchiveStationState
            {
                topic=command.topic,revision=previous.revision+1,
                testResetSerial=previous.testResetSerial+(complete?0:1),
                round=complete?ArchiveTopicCatalog.RoundCount(command.topic):0,phase=complete?2:0,
                timerLimit=complete?previous.timerLimit:0,
                timerSavedRemaining=complete?TimerRemaining(previous):0,
                timerAttempt=complete?previous.timerAttempt:0,timerActor=complete?previous.timerActor:0,
                feedback=complete?"Completed by Admin for testing.":"Reset by Admin. Read the Book to restart this topic."
            };
            ResetTopicRelay(command.topic,complete);
            if(crystalRestoration)ResetTopicCrystal(command.topic,complete);
            if (command.topic == 7) ClearMappingInputs();
            if (command.topic == 9) ClearTreeInputs();
            string prefix=command.topic+":";
            foreach(string key in questions.Keys.Where(k=>k.StartsWith(prefix,StringComparison.Ordinal)).ToArray())questions.Remove(key);
            foreach(string key in lastSubmit.Keys.Where(k=>k.Split(':')[1]==command.topic.ToString()).ToArray())lastSubmit.Remove(key);
            if (complete) ArchiveTopicQuestions.Prepare(Question(command.topic),State.stations[command.topic]);
            LocalNotice="Admin "+(complete?"completed":"reset")+" T"+(command.topic+1)+" for testing.";
            Commit();
            return true;
#else
            return false;
#endif
        }
    }
}
