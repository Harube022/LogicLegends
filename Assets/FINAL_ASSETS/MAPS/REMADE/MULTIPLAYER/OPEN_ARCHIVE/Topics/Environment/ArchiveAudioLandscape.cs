using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    [Serializable] public sealed class ArchiveMonsterSoundSet
    {
        public AudioClip capture, breathing, growl, roar, impact, barrier, footstep, attack;
    }
    /// <summary>Local sound presentation of accepted encounter and inventory state. Sends no gameplay commands.</summary>
    [DefaultExecutionOrder(12010), DisallowMultipleComponent]
    public sealed partial class ArchiveAudioLandscape : MonoBehaviour
    {
        public ArchiveTopicSession session;
        public AudioClip dayBed, nightBed;
        public ArchiveMonsterSoundSet proofDevourer = new ArchiveMonsterSoundSet(), primalAxiom = new ArchiveMonsterSoundSet();
        public AudioClip equip, flashlightOn, flashlightOff, battery, medkit, revival, handoff, dartImpact;
        [Header("Background - existing Music volume control")]
        [Range(0, .4f)] public float dayVolume = .14f, nightVolume = .17f;
        [Range(0, 1)] public float encounterDuck = .22f;
        [Header("Feedback - existing SFX volume control")]
        [Range(0, 1)] public float itemVolume = .40f, monsterVolume = .32f;
        public int PlayedItemCues { get; private set; }
        public int PlayedMonsterCues { get; private set; }
        public float MusicDuck { get; private set; } = 1;
        public int PlayingMusicStems => (day != null && day.isPlaying ? 1 : 0) + (night != null && night.isPlaying ? 1 : 0);
        public string LastItemCue { get; private set; } = "";
        sealed class MonsterVoice
        {
            public KhezuHuntingController monster; public KhezuCinematicJumpscareController cinematic;
            public AudioClip[] original; public ArchiveMonsterSoundSet profile;
            public AudioSource breath, motion, vocal; public Vector3 position;
            public float distance, nextStep, nextGrowl; public KhezuHuntState state;
            public double attackContact;
        }
        sealed class InventoryView
        {
            public int equipped; public bool light; public int[] items;
            public double used;
        }
        readonly Dictionary<KhezuHuntingController, MonsterVoice> monsters = new Dictionary<KhezuHuntingController, MonsterVoice>();
        readonly Dictionary<int, InventoryView> inventories = new Dictionary<int, InventoryView>();
        readonly Dictionary<int, int> hearts = new Dictionary<int, int>();
        readonly Dictionary<AudioSource, bool> legacyMusic = new Dictionary<AudioSource, bool>();
        AudioSource day, night, localItem;
        AudioSource[] spatialItems;
        ArchiveTranquilizerVfx dartVisual;
        int spatialCursor, itemRevision = -1;
        float nextScan;
        string match = "";

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            day = Voice("Archive_Daylight_Background", transform, dayBed, true, false, GameAudioChannel.Music);
            night = Voice("Archive_Night_Background", transform, nightBed, true, false, GameAudioChannel.Music);
            ResetBackgroundMusic();
            localItem = Voice("Archive_Local_Item_Feedback", transform, null, false, false);
            EnableIndicators();
            spatialItems = new AudioSource[3];
            for (int i = 0; i < spatialItems.Length; i++) spatialItems[i] = Voice("Archive_Item_World_" + i, transform, null, false, true);
            nextScan = 0; match = ""; itemRevision = -1; MusicDuck = 1;
        }
        static AudioSource Voice(string name, Transform parent, AudioClip clip, bool loop, bool spatial, GameAudioChannel channel = GameAudioChannel.SoundFX)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.clip = clip; source.volume = 0;
            source.spatialBlend = spatial ? 1 : 0; source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3; source.maxDistance = 38; source.dopplerLevel = 0; source.priority = spatial ? 150 : 170;
            AudioVolumeSettings.Route(source, channel); return source;
        }
        public void ApplyMonsterProfile(KhezuHuntingController monster)
        {
            if (monster == null) return;
            var cinematic = monster.GetComponent<KhezuCinematicJumpscareController>(); if (cinematic == null || cinematic.IsPlaying) return;
            string name = monster.GetComponent<Animator>()?.runtimeAnimatorController?.name ?? "";
            bool primal = name.IndexOf("primal", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("rajang", StringComparison.OrdinalIgnoreCase) >= 0;
            var profile = primal ? primalAxiom : proofDevourer; if (profile.roar == null) return;
            if (Application.isPlaying)
            {
                if (!monsters.TryGetValue(monster, out var track))
                {
                    track = new MonsterVoice { monster = monster, cinematic = cinematic, position = monster.transform.position, state = monster.State,
                        attackContact = monster.FailureHitApplied ? monster.AttackHitAt : 0,
                        original = new[] { cinematic.captureImpact, cinematic.lowBreathing, cinematic.tenseGrowl, cinematic.monsterRoar, cinematic.impactAccent, cinematic.barrierActivation } };
                    track.breath = Voice("Archive_Guardian_Breath", monster.transform, profile.breathing, true, true);
                    track.motion = Voice("Archive_Guardian_Footsteps", monster.transform, null, false, true);
                    track.vocal = Voice("Archive_Guardian_Growl", monster.transform, null, false, true);
                    monsters.Add(monster, track);
                }
                if (track.profile == profile) return;
                track.profile = profile; track.breath.Stop(); track.breath.clip = profile.breathing;
            }
            cinematic.captureImpact = profile.capture; cinematic.lowBreathing = profile.breathing; cinematic.tenseGrowl = profile.growl;
            cinematic.monsterRoar = profile.roar; cinematic.impactAccent = profile.impact; cinematic.barrierActivation = profile.barrier;
        }
        void LateUpdate()
        {
            if (day == null || session == null) return;
            PresentIndicators();
            if (!session.Ready) return;
            if (dartVisual == null)
            {
                dartVisual = session.GetComponent<ArchiveTranquilizerVfx>();
                if (dartVisual != null) dartVisual.ImpactPresented += OnDartImpact;
            }
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + .5f;
                foreach (var monster in KhezuHuntingController.ActiveMonsters) if (monster.gameObject.scene == gameObject.scene) ApplyMonsterProfile(monster);
                foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                    if (source.name == "MainMenuMusic" && !legacyMusic.ContainsKey(source)) { legacyMusic[source] = source.mute; source.mute = true; }
                ScanIndicatorButtons();
            }
            if (match != session.State.matchId)
            {
                match = session.State.matchId; inventories.Clear(); hearts.Clear(); itemRevision = -1;
                ResetBackgroundMusic();
            }
            PresentBackgroundMusic();
            PresentMonsters();
            if (itemRevision != session.State.revision) { itemRevision = session.State.revision; PresentItems(); }
        }
        static void Loop(AudioSource source, float level)
        {
            source.volume = level;
            if (source.clip == null || level < .001f) { if (source.isPlaying) source.Stop(); return; }
            if (!source.isPlaying) source.Play();
        }
        void PresentMonsters()
        {
            Vector3 eye = Camera.main != null ? Camera.main.transform.position : Player.LocalInstance != null ? Player.LocalInstance.transform.position : transform.position;
            foreach (var track in monsters.Values)
            {
                var monster = track.monster;
                bool active = monster != null && monster.isActiveAndEnabled && monster.Initialized;
                if (!active) { if (track.breath != null) track.breath.Stop(); continue; }
                float distance = Vector3.Distance(eye, monster.transform.position);
                bool quiet = track.cinematic.IsPlaying || session.NightEntranceActive || ArchiveMatchCinematic.Covers(session) || monster.IsTranquilized;
                bool sleeping = monster.State == KhezuHuntState.Sleeping || monster.State == KhezuHuntState.NestSleeping;
                Loop(track.breath, !quiet && sleeping && distance < 20 ? monsterVolume * .17f : 0);
                bool hunting = monster.State == KhezuHuntState.Hunting || monster.State == KhezuHuntState.NavigationBlocked;
                float moved = Vector3.Distance(monster.transform.position, track.position); track.position = monster.transform.position;
                if (hunting && !quiet && moved < 5) track.distance += moved; else track.distance = 0;
                if (!quiet && hunting && track.distance > 1.9f && Time.unscaledTime > track.nextStep && distance < 38)
                {
                    track.nextStep = Time.unscaledTime + .34f; track.distance = 0;
                    MonsterCue(track.motion, track.profile.footstep, monsterVolume * .60f);
                }
                bool wake = monster.State == KhezuHuntState.Waking && track.state != monster.State;
                bool growl = hunting && Time.unscaledTime > track.nextGrowl;
                if (!quiet && (wake || growl) && distance < 38)
                {
                    track.nextGrowl = Time.unscaledTime + 10;
                    MonsterCue(track.vocal, track.profile.growl, monsterVolume * .85f);
                }
                if (monster.FailureHitApplied && monster.AttackHitAt > track.attackContact)
                {
                    track.attackContact = monster.AttackHitAt;
                    double age = KhezuHuntingController.EncounterClock - track.attackContact;
                    if (!quiet && age >= 0 && age < 1.25)
                        MonsterCue(track.vocal, track.profile.attack, monsterVolume);
                }
                track.state = monster.State;
            }
        }
        void MonsterCue(AudioSource voice, AudioClip clip, float level)
        { if (clip == null || voice == null) return; voice.volume = level; voice.clip = clip; voice.Play(); PlayedMonsterCues++; }
        void PresentItems()
        {
            var health = session.hunting?.health;
            var currentHearts = health != null ? health.ExportActorHealth().ToDictionary(h => h.actor, h => h.hearts) : new Dictionary<int, int>();
            bool revived = currentHearts.Any(h => h.Value > 0 && hearts.TryGetValue(h.Key, out var old) && old == 0);
            bool healed = currentHearts.Any(h => hearts.TryGetValue(h.Key, out var old) && h.Value > old);
            foreach (var bag in session.State.survival.players)
            {
                if (inventories.TryGetValue(bag.actor, out var old))
                {
                    bool local = bag.actor == session.LocalActor;
                    bool fresh = bag.nextUseAt > old.used && ArchiveTopicSession.Clock - (bag.nextUseAt - .45) < 1.5;
                    if (local && bag.equippedItem != old.equipped) ItemCue(equip, bag.actor, .55f);
                    if (fresh)
                    {
                        bool transferred = Enumerable.Range(0, 6).Any(i => bag.items[i] < old.items[i] &&
                            session.State.survival.players.Any(recipient => recipient.actor != bag.actor &&
                                recipient.items[i] > (inventories.TryGetValue(recipient.actor, out var previous) ? previous.items[i] : 0)));
                        if (transferred) ItemCue(handoff, bag.actor, .65f);
                        else if (bag.flashlightOn != old.light) ItemCue(bag.flashlightOn ? flashlightOn : flashlightOff, bag.actor, .8f);
                        else if (bag.items[3] < old.items[3] && revived) ItemCue(revival, bag.actor, 1);
                        else if (bag.items[2] < old.items[2] && healed) ItemCue(medkit, bag.actor, .85f);
                        else if (bag.items[1] < old.items[1]) ItemCue(battery, bag.actor, .8f);
                        else if (bag.items.Take(5).Where((value, i) => value < old.items[i]).Any()) ItemCue(handoff, bag.actor, .55f);
                    }
                }
            }
            inventories.Clear();
            foreach (var bag in session.State.survival.players)
                inventories[bag.actor] = new InventoryView { equipped = bag.equippedItem, light = bag.flashlightOn, items = (int[])bag.items.Clone(), used = bag.nextUseAt };
            hearts.Clear(); foreach (var pair in currentHearts) hearts[pair.Key] = pair.Value;
        }
        void OnDartImpact(Vector3 position, bool hit)
        { if (hit) WorldItemCue(dartImpact, position, .45f); }
        void ItemCue(AudioClip clip, int actor, float strength)
        {
            if (clip == null) return;
            if (actor == session.LocalActor) { localItem.volume = itemVolume; localItem.PlayOneShot(clip, strength); }
            else { var body = session.ActorPlayer(actor); if (body == null) return; WorldItemCue(clip, body.transform.position + Vector3.up, strength * .6f); }
            LastItemCue = clip.name; PlayedItemCues++;
        }
        void WorldItemCue(AudioClip clip, Vector3 position, float strength)
        {
            if (clip == null || spatialItems == null) return;
            var voice = spatialItems[spatialCursor++ % spatialItems.Length]; voice.transform.position = position; voice.volume = itemVolume * strength; voice.clip = clip; voice.Play();
        }
        void OnDisable()
        {
            DisableIndicators();
            if (dartVisual != null) dartVisual.ImpactPresented -= OnDartImpact;
            dartVisual = null;
            foreach (var pair in legacyMusic) if (pair.Key != null) pair.Key.mute = pair.Value; legacyMusic.Clear();
            foreach (var track in monsters.Values)
            {
                var c = track.cinematic;
                if (c != null && track.original != null)
                { c.captureImpact = track.original[0]; c.lowBreathing = track.original[1]; c.tenseGrowl = track.original[2]; c.monsterRoar = track.original[3]; c.impactAccent = track.original[4]; c.barrierActivation = track.original[5]; }
                foreach (var voice in new[] { track.breath, track.motion, track.vocal }) if (voice != null) { voice.Stop(); Destroy(voice.gameObject); }
            }
            monsters.Clear(); inventories.Clear(); hearts.Clear();
            foreach (var voice in new[] { day, night, localItem }) if (voice != null) { voice.Stop(); Destroy(voice.gameObject); }
            if (spatialItems != null) foreach (var voice in spatialItems) if (voice != null) { voice.Stop(); Destroy(voice.gameObject); }
            day = night = localItem = null; spatialItems = null;
        }
    }
}
