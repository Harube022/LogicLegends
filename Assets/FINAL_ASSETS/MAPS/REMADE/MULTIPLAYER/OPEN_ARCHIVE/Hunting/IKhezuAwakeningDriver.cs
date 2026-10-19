using System;

namespace LogicLegends.OpenArchive.Hunting
{
    public interface IKhezuAwakeningDriver
    {
        int Mistakes { get; }
        int PlayingStage { get; }
        bool IsPreviewing { get; }
        string CurrentState { get; }
        event Action MistakeRequested;
        event Action SleepRequested;
        void SetHuntingControl(bool enabled, bool replica);
        void ApplySharedMistakes(int count);
        void ApplyReplicaCounters(int count, int stage);
    }
}
