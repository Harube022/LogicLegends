using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public enum ArchiveMusicMood { Daylight, Afternoon, Evening, Night, Hunt }

    public sealed partial class ArchiveAudioLandscape
    {
        [Header("Adaptive score — follows the existing shared world phase")]
        public AudioClip afternoonMusic, eveningMusic, huntMusic;
        [Range(.25f, 8)] public float musicCrossfadeSeconds = 3.5f;
        [Range(.2f, 4)] public float huntCrossfadeSeconds = 1.2f;
        [Range(0, 5)] public float huntReleaseSeconds = 2.5f;
        public ArchiveMusicMood CurrentMusicMood { get; private set; }
        public bool MusicTransitioning => musicTransition;
        public float CurrentMusicLevel => Mathf.Max(day == null ? 0 : day.volume, night == null ? 0 : night.volume);
        AudioSource musicFrom, musicTo;
        float musicProgress, musicDuration, huntedUntil;
        bool musicTransition, musicStarted;

        public static ArchiveMusicMood MoodFor(int worldPhase, bool locallyHunted) => locallyHunted
            ? ArchiveMusicMood.Hunt : (ArchiveMusicMood)Mathf.Clamp(worldPhase, 0, 3);

        AudioClip MusicClip(ArchiveMusicMood mood)
        {
            switch (mood)
            {
                case ArchiveMusicMood.Afternoon: return afternoonMusic != null ? afternoonMusic : dayBed;
                case ArchiveMusicMood.Evening: return eveningMusic != null ? eveningMusic : nightBed;
                case ArchiveMusicMood.Night: return nightBed;
                case ArchiveMusicMood.Hunt: return huntMusic != null ? huntMusic : nightBed;
                default: return dayBed;
            }
        }
        void ResetBackgroundMusic()
        {
            foreach (var voice in new[] { day, night }) if (voice != null) { voice.Stop(); voice.volume = 0; }
            musicFrom = musicTo = null; musicProgress = 0; musicTransition = musicStarted = false; huntedUntil = 0;
            CurrentMusicMood = ArchiveMusicMood.Daylight;
        }
        void PresentBackgroundMusic()
        {
            bool hunted = false;
            foreach (var monster in KhezuHuntingController.ActiveMonsters)
                if (monster != null && monster.isActiveAndEnabled && monster.gameObject.scene == gameObject.scene &&
                    monster.Initialized && !monster.IsTranquilized && monster.TargetPlayerId == session.LocalActor &&
                    (monster.State == KhezuHuntState.Hunting || monster.State == KhezuHuntState.NavigationBlocked))
                { hunted = true; break; }
            if (hunted) huntedUntil = Time.unscaledTime + huntReleaseSeconds;
            bool focus = session.NightEntranceActive || ArchiveMatchCinematic.Covers(session) || session.IsLocalBarrierQuestion ||
                session.TeamWon || session.TeamDefeated;
            foreach (var track in monsters.Values) if (track.cinematic != null && track.cinematic.IsPlaying) { focus = true; break; }
            int phase = session.State.worldTime == null ? 0 : session.State.worldTime.phase;
            UpdateBackgroundMusic(phase, hunted || Time.unscaledTime < huntedUntil, focus,
                MultiplayerLobbyService.IsGameplayRecovering, Time.unscaledDeltaTime);
        }
        void UpdateBackgroundMusic(int phase, bool hunted, bool focus, bool recovering, float deltaTime)
        {
            if (day == null || night == null) return;
            var desired = MoodFor(phase, hunted);
            var clip = MusicClip(desired);
            MusicDuck = Mathf.MoveTowards(MusicDuck, recovering ? 0 : focus ? encounterDuck : 1, Mathf.Max(0, deltaTime) * 2);
            // At most two streaming voices. A rapid admin phase change queues the
            // latest mood until the current fade finishes, rather than spawning tracks.
            if (!musicTransition && clip != null && (!musicStarted || musicFrom == null || musicFrom.clip != clip))
            {
                musicTo = musicFrom == day ? night : day;
                musicTo.Stop(); musicTo.clip = clip; musicTo.volume = 0;
                // Same-tempo loops share the existing clock; no second match timer.
                double offset = ArchiveTopicSession.Clock % Mathf.Max(.01f, clip.length);
                musicTo.timeSamples = Mathf.Clamp((int)(offset * clip.frequency), 0, Mathf.Max(0, clip.samples - 1));
                musicTo.Play();
                musicProgress = 0;
                musicDuration = Mathf.Max(.2f, desired == ArchiveMusicMood.Hunt ? huntCrossfadeSeconds : musicCrossfadeSeconds);
                musicTransition = true; musicStarted = true; CurrentMusicMood = desired;
            }
            float level = Mathf.Lerp(dayVolume, nightVolume, Mathf.Clamp01(phase / 3f)) * MusicDuck;
            if (musicTransition)
            {
                musicProgress = Mathf.Min(1, musicProgress + Mathf.Max(0, deltaTime) / musicDuration);
                float smooth = musicProgress * musicProgress * (3 - 2 * musicProgress);
                if (musicFrom != null) musicFrom.volume = Mathf.Cos(smooth * Mathf.PI * .5f) * level;
                musicTo.volume = Mathf.Sin(smooth * Mathf.PI * .5f) * level;
                if (musicProgress >= 1)
                {
                    if (musicFrom != null) { musicFrom.Stop(); musicFrom.volume = 0; }
                    musicFrom = musicTo; musicTo = null; musicTransition = false;
                }
            }
            else if (musicFrom != null) musicFrom.volume = level;
        }
    }
}
